using Fleece.Core.Models;

namespace Fleece.Core.Services.Interfaces;

/// <summary>
/// Validates JSON schema against known Issue properties.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Set of known property names for the Issue type (camelCase).
    /// </summary>
    IReadOnlySet<string> KnownIssueProperties { get; }

    /// <summary>
    /// Validates JSONL content and returns diagnostic information.
    /// </summary>
    /// <param name="filePath">Path to the file being validated (for diagnostics).</param>
    /// <param name="content">The JSONL content to validate.</param>
    /// <returns>Diagnostic information about the content.</returns>
    ParseDiagnostic ValidateJsonlContent(string filePath, string content);

    /// <summary>
    /// Extracts unknown property names from a single JSON line.
    /// </summary>
    /// <param name="jsonLine">A single line of JSON.</param>
    /// <returns>Set of property names that are not known Issue properties.</returns>
    IReadOnlySet<string> ExtractUnknownProperties(string jsonLine);
}
