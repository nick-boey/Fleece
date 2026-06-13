using System.Diagnostics;
using Fleece.Core.Models.GitHub;
using Fleece.Core.Services.Interfaces;
using Octokit;

namespace Fleece.GitHub;

/// <summary>
/// OctoKit-backed <see cref="IGitHubService"/>. Isolated in its own assembly so that
/// <c>Fleece.Core</c> carries no OctoKit dependency.
/// </summary>
/// <remarks>
/// Token resolution order is <c>gh auth token</c> → <c>GH_TOKEN</c>/<c>GITHUB_TOKEN</c> →
/// a config-stored PAT (git config <c>fleece.githubToken</c>). The target repository is parsed
/// from <c>git remote get-url origin</c>.
/// </remarks>
public sealed class GitHubService : IGitHubService
{
    private const string ProductName = "fleece";
    private const string ConfigTokenKey = "fleece.githubToken";

    private readonly ISettingsService _settingsService;
    private readonly IGitService _gitService;

    public GitHubService(ISettingsService settingsService, IGitService gitService)
    {
        _settingsService = settingsService;
        _gitService = gitService;
    }

    /// <inheritdoc />
    public async Task<GitHubAuthResult> ResolveAuthAsync(CancellationToken cancellationToken = default)
    {
        var repository = ResolveRepository();
        var token = await ResolveTokenAsync(cancellationToken).ConfigureAwait(false);

        if (token is null)
        {
            return new GitHubAuthResult
            {
                Authenticated = false,
                Repository = repository?.ToString(),
            };
        }

        // A token that fails to round-trip a user lookup is not "usable".
        try
        {
            var client = CreateClient(token.Value);
            var user = await client.User.Current().ConfigureAwait(false);
            return new GitHubAuthResult
            {
                Authenticated = true,
                Login = user.Login,
                TokenSource = token.Source,
                Repository = repository?.ToString(),
            };
        }
        catch (Exception)
        {
            return new GitHubAuthResult
            {
                Authenticated = false,
                TokenSource = token.Source,
                Repository = repository?.ToString(),
            };
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetCurrentLoginAsync(CancellationToken cancellationToken = default)
    {
        var token = await ResolveTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return null;
        }

        try
        {
            var client = CreateClient(token.Value);
            var user = await client.User.Current().ConfigureAwait(false);
            return user.Login;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GitHubIssueRef> CreateIssueAsync(string title, string body, CancellationToken cancellationToken = default)
    {
        var (client, owner, name) = await RequireClientAsync(cancellationToken).ConfigureAwait(false);
        var created = await client.Issue.Create(owner, name, new NewIssue(title) { Body = body }).ConfigureAwait(false);
        return new GitHubIssueRef
        {
            Number = created.Number,
            Url = created.HtmlUrl,
        };
    }

    /// <inheritdoc />
    public async Task<GitHubIssue> GetIssueAsync(int number, CancellationToken cancellationToken = default)
    {
        var (client, owner, name) = await RequireClientAsync(cancellationToken).ConfigureAwait(false);
        var issue = await client.Issue.Get(owner, name, number).ConfigureAwait(false);
        return new GitHubIssue
        {
            Number = issue.Number,
            Title = issue.Title,
            Body = issue.Body,
        };
    }

    /// <inheritdoc />
    public async Task AddCommentAsync(int number, string body, CancellationToken cancellationToken = default)
    {
        var (client, owner, name) = await RequireClientAsync(cancellationToken).ConfigureAwait(false);
        await client.Issue.Comment.Create(owner, name, number, body).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AssignAsync(int number, string login, CancellationToken cancellationToken = default)
    {
        var (client, owner, name) = await RequireClientAsync(cancellationToken).ConfigureAwait(false);
        var update = new IssueUpdate();
        update.AddAssignee(login);
        await client.Issue.Update(owner, name, number, update).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves an authenticated client plus the target repository, throwing when either the token
    /// or the origin remote cannot be resolved. Commands gate on <see cref="ResolveAuthAsync"/>
    /// first, so this throw path is defensive.
    /// </summary>
    private async Task<(GitHubClient Client, string Owner, string Name)> RequireClientAsync(CancellationToken cancellationToken)
    {
        var token = await ResolveTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            throw new InvalidOperationException("No usable GitHub token could be resolved. Run 'fleece auth' for guidance.");
        }

        var repository = ResolveRepository()
            ?? throw new InvalidOperationException("Could not determine the target repository from the 'origin' remote.");

        return (CreateClient(token.Value), repository.Owner, repository.Name);
    }

    private static GitHubClient CreateClient(string token)
        => new(new ProductHeaderValue(ProductName))
        {
            Credentials = new Credentials(token),
        };

    /// <summary>
    /// Resolves a token in the order <c>gh auth token</c> → <c>GH_TOKEN</c>/<c>GITHUB_TOKEN</c>
    /// → config-stored PAT, returning the value and a human-readable source label, or null.
    /// </summary>
    private async Task<ResolvedToken?> ResolveTokenAsync(CancellationToken cancellationToken)
    {
        var ghToken = await TryGetGhCliTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(ghToken))
        {
            return new ResolvedToken(ghToken.Trim(), "gh auth token");
        }

        var envGhToken = Environment.GetEnvironmentVariable("GH_TOKEN");
        if (!string.IsNullOrWhiteSpace(envGhToken))
        {
            return new ResolvedToken(envGhToken.Trim(), "GH_TOKEN");
        }

        var envGitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(envGitHubToken))
        {
            return new ResolvedToken(envGitHubToken.Trim(), "GITHUB_TOKEN");
        }

        var configToken = TryGetConfigPat();
        if (!string.IsNullOrWhiteSpace(configToken))
        {
            return new ResolvedToken(configToken.Trim(), "config PAT");
        }

        return null;
    }

    /// <summary>Shells out to the GitHub CLI for a token, returning null when it is unavailable.</summary>
    private static async Task<string?> TryGetGhCliTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo("gh", "auth token")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
        }
        catch (Exception)
        {
            // gh not installed / not on PATH — fall through to the next source.
            return null;
        }
    }

    /// <summary>Reads a config-stored PAT from git config <c>fleece.githubToken</c>.</summary>
    private string? TryGetConfigPat()
    {
        var (exitCode, output, _) = _gitService.RunGitCommand($"config --get {ConfigTokenKey}");
        return exitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output.Trim() : null;
    }

    /// <summary>Infers the <c>owner/name</c> repository from the <c>origin</c> remote URL.</summary>
    private RepositoryRef? ResolveRepository()
    {
        var (exitCode, output, _) = _gitService.RunGitCommand("remote get-url origin");
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        return ParseRepository(output.Trim());
    }

    /// <summary>
    /// Parses an <c>owner/name</c> repository from SSH (<c>git@github.com:owner/name.git</c>),
    /// scp-less SSH (<c>ssh://git@github.com/owner/name.git</c>), or HTTPS
    /// (<c>https://github.com/owner/name.git</c>) remote URLs.
    /// </summary>
    internal static RepositoryRef? ParseRepository(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        var url = remoteUrl.Trim();
        string path;

        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            // git@github.com:owner/name.git
            var colon = url.IndexOf(':');
            if (colon < 0)
            {
                return null;
            }

            path = url[(colon + 1)..];
        }
        else if (url.Contains("://", StringComparison.Ordinal))
        {
            // https://github.com/owner/name.git or ssh://git@github.com/owner/name.git
            var schemeEnd = url.IndexOf("://", StringComparison.Ordinal) + 3;
            var afterScheme = url[schemeEnd..];
            var firstSlash = afterScheme.IndexOf('/');
            if (firstSlash < 0)
            {
                return null;
            }

            path = afterScheme[(firstSlash + 1)..];
        }
        else
        {
            return null;
        }

        path = path.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        var owner = segments[0];
        var name = segments[^1];
        return new RepositoryRef(owner, name);
    }

    private sealed record ResolvedToken(string Value, string Source);

    /// <summary>An <c>owner/name</c> GitHub repository reference parsed from a remote URL.</summary>
    internal sealed record RepositoryRef(string Owner, string Name)
    {
        public override string ToString() => $"{Owner}/{Name}";
    }
}
