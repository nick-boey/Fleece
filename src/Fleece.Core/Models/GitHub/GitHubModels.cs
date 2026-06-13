namespace Fleece.Core.Models.GitHub;

/// <summary>
/// Result of resolving GitHub credentials via <see cref="Services.Interfaces.IGitHubService"/>.
/// </summary>
public sealed record GitHubAuthResult
{
    /// <summary>Whether a usable token was resolved.</summary>
    public required bool Authenticated { get; init; }

    /// <summary>The authenticated GitHub login, when known.</summary>
    public string? Login { get; init; }

    /// <summary>
    /// Human-readable description of where the token came from
    /// (e.g. <c>gh auth token</c>, <c>GH_TOKEN</c>, <c>config PAT</c>).
    /// </summary>
    public string? TokenSource { get; init; }

    /// <summary>The <c>owner/name</c> repository inferred from the origin remote, when known.</summary>
    public string? Repository { get; init; }
}

/// <summary>Reference to a GitHub issue created or located via the API.</summary>
public sealed record GitHubIssueRef
{
    public required int Number { get; init; }
    public required string Url { get; init; }
}

/// <summary>The subset of a GitHub issue Fleece reads when absorbing.</summary>
public sealed record GitHubIssue
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public string? Body { get; init; }
}
