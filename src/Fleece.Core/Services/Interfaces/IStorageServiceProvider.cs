namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Factory interface for obtaining storage services.
/// </summary>
public interface IStorageServiceProvider
{
    /// <summary>
    /// Gets the appropriate storage service based on the provided file path.
    /// </summary>
    /// <param name="customFilePath">
    /// Optional path to a custom JSONL issues file. If null, returns the default storage service.
    /// </param>
    /// <returns>A storage service configured for the appropriate source.</returns>
    IStorageService GetStorageService(string? customFilePath = null);
}
