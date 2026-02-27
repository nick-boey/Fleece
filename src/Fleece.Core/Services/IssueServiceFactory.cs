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
    private readonly ITagService _tagService;

    public IssueServiceFactory(
        IIssueService defaultIssueService,
        IStorageServiceProvider storageServiceProvider,
        IIdGenerator idGenerator,
        IGitConfigService gitConfigService,
        ITagService tagService)
    {
        _defaultIssueService = defaultIssueService;
        _storageServiceProvider = storageServiceProvider;
        _idGenerator = idGenerator;
        _gitConfigService = gitConfigService;
        _tagService = tagService;
    }

    /// <inheritdoc/>
    public IIssueService Create(IStorageService storageService)
    {
        return new IssueService(storageService, _idGenerator, _gitConfigService, _tagService);
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
