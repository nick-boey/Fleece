using System.Diagnostics;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class GitConfigService : IGitConfigService
{
    private readonly ISettingsService? _settingsService;

    public GitConfigService()
    {
    }

    public GitConfigService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string? GetUserName()
    {
        // First check settings for identity override
        if (_settingsService is not null)
        {
            try
            {
                var settings = _settingsService.GetEffectiveSettingsAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(settings.Identity))
                {
                    return settings.Identity;
                }
            }
            catch
            {
                // Fall back to git config if settings load fails
            }
        }

        // Fall back to git config
        return GetGitUserName();
    }

    private static string? GetGitUserName()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "config user.name",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return null;
            }

            var trimmed = output.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
        catch
        {
            return null;
        }
    }
}
