using Fleece.Core.Serialization;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Factory for obtaining storage services based on runtime configuration.
/// </summary>
public sealed class StorageServiceProvider : IStorageServiceProvider
{
    private readonly IStorageService _defaultStorageService;
    private readonly IJsonlSerializer _serializer;
    private readonly ISchemaValidator _schemaValidator;

    public StorageServiceProvider(
        IStorageService defaultStorageService,
        IJsonlSerializer serializer,
        ISchemaValidator schemaValidator)
    {
        _defaultStorageService = defaultStorageService;
        _serializer = serializer;
        _schemaValidator = schemaValidator;
    }

    /// <inheritdoc/>
    public IStorageService GetStorageService(string? customFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(customFilePath))
        {
            return _defaultStorageService;
        }

        return new SingleFileStorageService(customFilePath, _serializer, _schemaValidator);
    }
}
