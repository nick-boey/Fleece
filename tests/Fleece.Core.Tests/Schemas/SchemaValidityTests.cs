using System.Reflection;
using System.Text.Json;
using Fleece.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Schemas;

[TestFixture]
public class SchemaValidityTests
{
    private JsonDocument _issueSchema = null!;
    private JsonDocument _changeRecordSchema = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _issueSchema = LoadEmbeddedSchema("issue.schema.json");
        _changeRecordSchema = LoadEmbeddedSchema("change-record.schema.json");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _issueSchema.Dispose();
        _changeRecordSchema.Dispose();
    }

    [Test]
    public void IssueSchema_IsValidJsonSchema()
    {
        _issueSchema.RootElement.GetProperty("$schema").GetString()
            .Should().Be("http://json-schema.org/draft-07/schema#");

        _issueSchema.RootElement.GetProperty("type").GetString()
            .Should().Be("object");

        _issueSchema.RootElement.GetProperty("required").GetArrayLength()
            .Should().BeGreaterThan(0);

        _issueSchema.RootElement.TryGetProperty("properties", out _)
            .Should().BeTrue();
    }

    [Test]
    public void ChangeRecordSchema_IsValidJsonSchema()
    {
        _changeRecordSchema.RootElement.GetProperty("$schema").GetString()
            .Should().Be("http://json-schema.org/draft-07/schema#");

        _changeRecordSchema.RootElement.GetProperty("type").GetString()
            .Should().Be("object");

        _changeRecordSchema.RootElement.GetProperty("required").GetArrayLength()
            .Should().BeGreaterThan(0);

        _changeRecordSchema.RootElement.TryGetProperty("properties", out _)
            .Should().BeTrue();
    }

    [Test]
    public void IssueSchema_ContainsAllModelProperties()
    {
        var schemaProperties = _issueSchema.RootElement.GetProperty("properties");

        var issueProperties = typeof(Issue).GetProperties()
            .Select(ToCamelCase)
            .ToHashSet();

        foreach (var prop in issueProperties)
        {
            schemaProperties.TryGetProperty(prop, out _)
                .Should().BeTrue($"Schema should contain property '{prop}'");
        }
    }

    [Test]
    public void IssueSchema_DoesNotHaveExtraProperties()
    {
        var schemaProperties = _issueSchema.RootElement.GetProperty("properties")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var issueProperties = typeof(Issue).GetProperties()
            .Select(ToCamelCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        schemaProperties.Should().BeEquivalentTo(issueProperties);
    }

    [Test]
    public void ChangeRecordSchema_ContainsAllModelProperties()
    {
        var schemaProperties = _changeRecordSchema.RootElement.GetProperty("properties");

        var changeRecordProperties = typeof(ChangeRecord).GetProperties()
            .Select(ToCamelCase)
            .ToHashSet();

        foreach (var prop in changeRecordProperties)
        {
            schemaProperties.TryGetProperty(prop, out _)
                .Should().BeTrue($"Schema should contain property '{prop}'");
        }
    }

    [Test]
    public void ChangeRecordSchema_DoesNotHaveExtraProperties()
    {
        var schemaProperties = _changeRecordSchema.RootElement.GetProperty("properties")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var changeRecordProperties = typeof(ChangeRecord).GetProperties()
            .Select(ToCamelCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        schemaProperties.Should().BeEquivalentTo(changeRecordProperties);
    }

    [Test]
    public void IssueSchema_IssueStatusEnum_MatchesCSharpEnum()
    {
        var schemaStatuses = _issueSchema.RootElement
            .GetProperty("$defs")
            .GetProperty("issueStatus")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet();

        var csharpStatuses = Enum.GetNames<IssueStatus>().ToHashSet();

        schemaStatuses.Should().BeEquivalentTo(csharpStatuses);
    }

    [Test]
    public void IssueSchema_IssueTypeEnum_MatchesCSharpEnum()
    {
        var schemaTypes = _issueSchema.RootElement
            .GetProperty("$defs")
            .GetProperty("issueType")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet();

        var csharpTypes = Enum.GetNames<IssueType>().ToHashSet();

        schemaTypes.Should().BeEquivalentTo(csharpTypes);
    }

    [Test]
    public void ChangeRecordSchema_ChangeTypeEnum_MatchesCSharpEnum()
    {
        var schemaChangeTypes = _changeRecordSchema.RootElement
            .GetProperty("$defs")
            .GetProperty("changeType")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet();

        var csharpChangeTypes = Enum.GetNames<ChangeType>().ToHashSet();

        schemaChangeTypes.Should().BeEquivalentTo(csharpChangeTypes);
    }

    [Test]
    public void IssueSchema_QuestionDefinition_MatchesCSharpModel()
    {
        var questionDef = _issueSchema.RootElement
            .GetProperty("$defs")
            .GetProperty("question")
            .GetProperty("properties");

        var questionProperties = typeof(Question).GetProperties()
            .Select(ToCamelCase)
            .ToHashSet();

        var schemaQuestionProps = questionDef
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        schemaQuestionProps.Should().BeEquivalentTo(questionProperties);
    }

    [Test]
    public void ChangeRecordSchema_PropertyChangeDefinition_MatchesCSharpModel()
    {
        var propertyChangeDef = _changeRecordSchema.RootElement
            .GetProperty("$defs")
            .GetProperty("propertyChange")
            .GetProperty("properties");

        var propertyChangeProperties = typeof(PropertyChange).GetProperties()
            .Select(ToCamelCase)
            .ToHashSet();

        var schemaPropertyChangeProps = propertyChangeDef
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        schemaPropertyChangeProps.Should().BeEquivalentTo(propertyChangeProperties);
    }

    [Test]
    public void IssueSchema_RequiredFields_AreCorrect()
    {
        var requiredFields = _issueSchema.RootElement
            .GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet();

        // These are the required properties from the C# model
        requiredFields.Should().Contain("id");
        requiredFields.Should().Contain("title");
        requiredFields.Should().Contain("status");
        requiredFields.Should().Contain("type");
        requiredFields.Should().Contain("lastUpdate");
    }

    [Test]
    public void ChangeRecordSchema_RequiredFields_AreCorrect()
    {
        var requiredFields = _changeRecordSchema.RootElement
            .GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet();

        // These are the required properties from the C# model
        requiredFields.Should().Contain("changeId");
        requiredFields.Should().Contain("issueId");
        requiredFields.Should().Contain("type");
        requiredFields.Should().Contain("changedBy");
        requiredFields.Should().Contain("changedAt");
    }

    [Test]
    public void IssueSchema_HasAdditionalPropertiesFalse()
    {
        _issueSchema.RootElement
            .GetProperty("additionalProperties")
            .GetBoolean()
            .Should().BeFalse();
    }

    [Test]
    public void ChangeRecordSchema_HasAdditionalPropertiesFalse()
    {
        _changeRecordSchema.RootElement
            .GetProperty("additionalProperties")
            .GetBoolean()
            .Should().BeFalse();
    }

    private static JsonDocument LoadEmbeddedSchema(string schemaName)
    {
        var assembly = typeof(Issue).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(schemaName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            throw new InvalidOperationException(
                $"Could not find embedded resource ending with '{schemaName}'. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonDocument.Parse(stream);
    }

    private static string ToCamelCase(PropertyInfo property)
    {
        var name = property.Name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
