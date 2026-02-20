namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Factory interface for creating IssueService instances with custom storage.
/// </summary>
public interface IIssueServiceFactory
{
    /// <summary>
    /// Creates an IssueService that uses the specified storage service.
    /// </summary>
    /// <param name="storageService">The storage service to use.</param>
    /// <returns>An IssueService instance configured with the given storage.</returns>
    IIssueService Create(IStorageService storageService);

    /// <summary>
    /// Gets an IssueService for the given custom file path, or the default if no path is specified.
    /// </summary>
    /// <param name="customFilePath">Optional path to a custom JSONL issues file.</param>
    /// <returns>An IssueService instance.</returns>
    IIssueService GetIssueService(string? customFilePath = null);
}
