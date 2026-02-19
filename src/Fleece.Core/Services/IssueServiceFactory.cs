using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Factory for creating IssueService instances with custom storage.
/// </summary>
public sealed class IssueServiceFactory : IIssueServiceFactory
{
    private readonly IIssueService _defaultIssueService;
    private readonly IStorageServiceProvider _storageServiceProvider;
    private readonly IIdGenerator _idGenerator;
    private readonly IGitConfigService _gitConfigService;

    public IssueServiceFactory(
        IIssueService defaultIssueService,
        IStorageServiceProvider storageServiceProvider,
        IIdGenerator idGenerator,
        IGitConfigService gitConfigService)
    {
        _defaultIssueService = defaultIssueService;
        _storageServiceProvider = storageServiceProvider;
        _idGenerator = idGenerator;
        _gitConfigService = gitConfigService;
    }

    /// <inheritdoc/>
    public IIssueService Create(IStorageService storageService)
    {
        return new IssueService(storageService, _idGenerator, _gitConfigService);
    }

    /// <inheritdoc/>
    public IIssueService GetIssueService(string? customFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(customFilePath))
        {
            return _defaultIssueService;
        }

        var storageService = _storageServiceProvider.GetStorageService(customFilePath);
        return Create(storageService);
    }
}
