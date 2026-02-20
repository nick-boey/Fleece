using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class StorageServiceProviderTests
{
    private IStorageService _defaultStorageService = null!;
    private IJsonlSerializer _serializer = null!;
    private ISchemaValidator _schemaValidator = null!;
    private StorageServiceProvider _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _defaultStorageService = Substitute.For<IStorageService>();
        _serializer = Substitute.For<IJsonlSerializer>();
        _schemaValidator = Substitute.For<ISchemaValidator>();
        _sut = new StorageServiceProvider(_defaultStorageService, _serializer, _schemaValidator);
    }

    [Test]
    public void GetStorageService_ReturnsDefaultService_WhenNoCustomPath()
    {
        var result = _sut.GetStorageService(null);

        result.Should().BeSameAs(_defaultStorageService);
    }

    [Test]
    public void GetStorageService_ReturnsDefaultService_WhenEmptyPath()
    {
        var result = _sut.GetStorageService("");

        result.Should().BeSameAs(_defaultStorageService);
    }

    [Test]
    public void GetStorageService_ReturnsDefaultService_WhenWhitespacePath()
    {
        var result = _sut.GetStorageService("   ");

        result.Should().BeSameAs(_defaultStorageService);
    }

    [Test]
    public void GetStorageService_ReturnsSingleFileStorageService_WhenCustomPathProvided()
    {
        var result = _sut.GetStorageService("/path/to/custom.jsonl");

        result.Should().BeOfType<SingleFileStorageService>();
    }

    [Test]
    public void GetStorageService_ReturnsNewInstance_ForEachCustomPath()
    {
        var result1 = _sut.GetStorageService("/path/to/file1.jsonl");
        var result2 = _sut.GetStorageService("/path/to/file2.jsonl");

        result1.Should().NotBeSameAs(result2);
    }
}
