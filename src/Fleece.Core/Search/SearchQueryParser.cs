using System.Text;

namespace Fleece.Core.Search;

/// <summary>
/// Parses search query strings into SearchQuery objects.
/// </summary>
public sealed class SearchQueryParser
{
    /// <summary>
    /// Field name to token type mapping (case-insensitive).
    /// </summary>
    private static readonly Dictionary<string, SearchTokenType> FieldMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["status"] = SearchTokenType.StatusFilter,
        ["type"] = SearchTokenType.TypeFilter,
        ["priority"] = SearchTokenType.PriorityFilter,
        ["assigned"] = SearchTokenType.AssignedFilter,
        ["tag"] = SearchTokenType.TagFilter,
        ["linkedpr"] = SearchTokenType.LinkedPrFilter,
        ["pr"] = SearchTokenType.LinkedPrFilter,
        ["id"] = SearchTokenType.IdFilter
    };

    /// <summary>
    /// Parses a search query string into a SearchQuery.
    /// </summary>
    /// <param name="query">The query string to parse. Can be null or empty.</param>
    /// <returns>The parsed SearchQuery.</returns>
    public SearchQuery Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return SearchQuery.Empty;
        }

        var tokens = new List<SearchToken>();
        var span = query.AsSpan();
        int pos = 0;

        while (pos < span.Length)
        {
            // Skip whitespace
            while (pos < span.Length && char.IsWhiteSpace(span[pos]))
            {
                pos++;
            }

            if (pos >= span.Length)
            {
                break;
            }

            // Check for negation
            bool isNegated = false;
            if (span[pos] == '-')
            {
                isNegated = true;
                pos++;

                // If nothing follows the dash, treat it as a text token
                if (pos >= span.Length || char.IsWhiteSpace(span[pos]))
                {
                    tokens.Add(new SearchToken
                    {
                        Type = SearchTokenType.Text,
                        IsNegated = false,
                        Values = ["-"]
                    });
                    continue;
                }
            }

            // Try to parse as a field:value token
            var token = TryParseFieldToken(span, ref pos, isNegated);
            if (token is not null)
            {
                tokens.Add(token);
            }
            else
            {
                // Parse as text token
                var textToken = ParseTextToken(span, ref pos, isNegated);
                if (textToken is not null)
                {
                    tokens.Add(textToken);
                }
            }
        }

        return new SearchQuery { Tokens = tokens };
    }

    /// <summary>
    /// Tries to parse a field:value token starting at the given position.
    /// Returns null if the current position doesn't represent a field token.
    /// </summary>
    private static SearchToken? TryParseFieldToken(ReadOnlySpan<char> span, ref int pos, bool isNegated)
    {
        int startPos = pos;

        // Find the colon
        int colonPos = -1;
        int scanPos = pos;
        while (scanPos < span.Length && !char.IsWhiteSpace(span[scanPos]))
        {
            if (span[scanPos] == ':')
            {
                colonPos = scanPos;
                break;
            }
            scanPos++;
        }

        if (colonPos < 0)
        {
            return null; // No colon found, not a field token
        }

        // Extract field name
        var fieldName = span.Slice(pos, colonPos - pos).ToString();

        // Check if it's a known field
        if (!FieldMapping.TryGetValue(fieldName, out var tokenType))
        {
            // Unknown field - treat the whole thing as text
            return null;
        }

        // Move past the colon
        pos = colonPos + 1;

        // If nothing after colon, return null (ignore empty field filter)
        if (pos >= span.Length || char.IsWhiteSpace(span[pos]))
        {
            return null;
        }

        // Parse the value(s)
        var values = ParseFieldValues(span, ref pos);

        if (values.Count == 0)
        {
            return null;
        }

        return new SearchToken
        {
            Type = tokenType,
            IsNegated = isNegated,
            Values = values
        };
    }

    /// <summary>
    /// Parses field values, handling multi-value syntax (comma-separated, semicolon-terminated).
    /// Multi-value mode is only enabled if we find a comma BEFORE the first whitespace or semicolon,
    /// AND there's a semicolon that terminates the multi-value list.
    /// </summary>
    private static List<string> ParseFieldValues(ReadOnlySpan<char> span, ref int pos)
    {
        // Scan ahead to determine if this is multi-value syntax
        // Multi-value requires: comma appears before whitespace, and semicolon appears after comma
        bool isMultiValue = false;
        int semicolonPos = -1;

        for (int i = pos; i < span.Length; i++)
        {
            char c = span[i];
            if (c == ',')
            {
                // Found comma - now look for semicolon
                for (int j = i + 1; j < span.Length; j++)
                {
                    if (span[j] == ';')
                    {
                        isMultiValue = true;
                        semicolonPos = j;
                        break;
                    }
                }
                break;
            }
            else if (char.IsWhiteSpace(c) || c == ';')
            {
                // Hit whitespace or semicolon before any comma - single value
                break;
            }
        }

        var values = new List<string>();
        var currentValue = new StringBuilder();

        if (isMultiValue)
        {
            // Multi-value mode: parse until semicolon (spaces are allowed)
            while (pos < span.Length)
            {
                char c = span[pos];

                if (c == ';')
                {
                    // Semicolon terminates multi-value
                    pos++;

                    // Add current value if non-empty
                    var trimmed = currentValue.ToString().Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        values.Add(trimmed);
                    }
                    break;
                }
                else if (c == ',')
                {
                    // Comma separates values within multi-value
                    var trimmed = currentValue.ToString().Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        values.Add(trimmed);
                    }
                    currentValue.Clear();
                    pos++;
                }
                else
                {
                    currentValue.Append(c);
                    pos++;
                }
            }
        }
        else
        {
            // Single-value mode: parse until whitespace or semicolon
            while (pos < span.Length && !char.IsWhiteSpace(span[pos]) && span[pos] != ';')
            {
                currentValue.Append(span[pos]);
                pos++;
            }

            // If we hit a semicolon, consume it (it terminates a single-value too)
            if (pos < span.Length && span[pos] == ';')
            {
                pos++;
            }

            var trimmed = currentValue.ToString().Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                values.Add(trimmed);
            }
        }

        return values;
    }

    /// <summary>
    /// Parses a text token (unquoted word).
    /// </summary>
    private static SearchToken? ParseTextToken(ReadOnlySpan<char> span, ref int pos, bool isNegated)
    {
        var start = pos;

        // Read until whitespace or end
        while (pos < span.Length && !char.IsWhiteSpace(span[pos]))
        {
            pos++;
        }

        if (pos == start)
        {
            return null;
        }

        var text = span.Slice(start, pos - start).ToString();

        // If it looks like "field:value" but we couldn't parse it as a field,
        // it should be treated as text
        return new SearchToken
        {
            Type = SearchTokenType.Text,
            IsNegated = isNegated,
            Values = [text]
        };
    }
}
