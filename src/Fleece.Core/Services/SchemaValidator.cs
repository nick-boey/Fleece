using System.Text.Json;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

/// <summary>
/// Validates JSON schema against known Issue properties.
/// </summary>
public sealed class SchemaValidator : ISchemaValidator
{
    private static readonly Lazy<HashSet<string>> KnownPropertiesCache = new(BuildKnownProperties);

    public IReadOnlySet<string> KnownIssueProperties => KnownPropertiesCache.Value;

    public ParseDiagnostic ValidateJsonlContent(string filePath, string content)
    {
        var lines = content.Split('\n', StringSplitOptions.None);
        var totalRows = 0;
        var parsedRows = 0;
        var failedRows = 0;
        var unknownProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parseErrors = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            totalRows++;

            try
            {
                using var doc = JsonDocument.Parse(line);
                parsedRows++;

                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (!KnownIssueProperties.Contains(property.Name))
                    {
                        unknownProperties.Add(property.Name);
                    }
                }
            }
            catch (JsonException ex)
            {
                failedRows++;
                parseErrors.Add($"Line {i + 1}: {ex.Message}");
            }
        }

        return new ParseDiagnostic
        {
            FilePath = filePath,
            TotalRows = totalRows,
            ParsedRows = parsedRows,
            FailedRows = failedRows,
            UnknownProperties = unknownProperties,
            ParseErrors = parseErrors
        };
    }

    public IReadOnlySet<string> ExtractUnknownProperties(string jsonLine)
    {
        var unknownProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return unknownProperties;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonLine);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!KnownIssueProperties.Contains(property.Name))
                {
                    unknownProperties.Add(property.Name);
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON - return empty set
        }

        return unknownProperties;
    }

    private static HashSet<string> BuildKnownProperties()
    {
        var properties = typeof(Issue).GetProperties();
        var knownProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in properties)
        {
            // Convert to camelCase (first char lowercase)
            var name = property.Name;
            var camelCase = char.ToLowerInvariant(name[0]) + name[1..];
            knownProperties.Add(camelCase);
        }

        return knownProperties;
    }
}
