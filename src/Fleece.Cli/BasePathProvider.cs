namespace Fleece.Cli;

/// <summary>
/// Wraps the project base path that was passed to <see cref="CliApp.RunAsync"/>.
/// Lets DI-resolved commands (e.g., <see cref="Commands.InstallCommand"/>) target
/// the same path as the core services without re-reading the OS current directory.
/// </summary>
public sealed record BasePathProvider(string BasePath);
