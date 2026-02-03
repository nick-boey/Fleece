namespace Fleece.Core.Utilities;

/// <summary>
/// Utility class for generating lexicographically sortable rank strings.
/// Used to maintain ordering of items in a list without needing to update all items when one is moved.
/// </summary>
public static class LexoRank
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";
    private const int DefaultLength = 3;

    /// <summary>
    /// Generates an array of sequential rank strings for initializing or migrating items.
    /// </summary>
    /// <param name="count">The number of ranks to generate.</param>
    /// <returns>An array of rank strings in ascending order.</returns>
    public static string[] GenerateInitialRanks(int count)
    {
        if (count == 0)
        {
            return [];
        }

        var ranks = new string[count];
        for (var i = 0; i < count; i++)
        {
            ranks[i] = IndexToRank(i);
        }
        return ranks;
    }

    /// <summary>
    /// Generates a rank string that falls between two existing ranks.
    /// </summary>
    /// <param name="before">The rank that should come before the result, or null if inserting at the beginning.</param>
    /// <param name="after">The rank that should come after the result, or null if inserting at the end.</param>
    /// <returns>A rank string that sorts between the given ranks.</returns>
    /// <exception cref="ArgumentException">Thrown when before is greater than or equal to after.</exception>
    public static string GetMiddleRank(string? before, string? after)
    {
        if (before is null && after is null)
        {
            // Return middle of the range
            return new string('n', DefaultLength);
        }

        if (before is null)
        {
            // Insert at beginning - return something before 'after'
            return GetRankBefore(after!);
        }

        if (after is null)
        {
            // Insert at end - return something after 'before'
            return GetRankAfter(before);
        }

        // Insert between two ranks
        var comparison = string.Compare(before, after, StringComparison.Ordinal);
        if (comparison >= 0)
        {
            throw new ArgumentException($"'before' ({before}) must be less than 'after' ({after})");
        }

        return GetRankBetween(before, after);
    }

    private static string IndexToRank(int index)
    {
        var chars = new char[DefaultLength];
        for (var i = DefaultLength - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[index % Alphabet.Length];
            index /= Alphabet.Length;
        }
        return new string(chars);
    }

    private static string GetRankBefore(string after)
    {
        // Try to find a character we can decrement
        var chars = after.ToCharArray();

        for (var i = chars.Length - 1; i >= 0; i--)
        {
            var charIndex = Alphabet.IndexOf(chars[i]);
            if (charIndex > 0)
            {
                // We can decrement this character
                chars[i] = Alphabet[charIndex / 2];
                return new string(chars);
            }
        }

        // All characters are 'a', prepend an 'a' and use middle
        return "a" + new string('n', after.Length);
    }

    private static string GetRankAfter(string before)
    {
        // Try to find a character we can increment
        var chars = before.ToCharArray();

        for (var i = chars.Length - 1; i >= 0; i--)
        {
            var charIndex = Alphabet.IndexOf(chars[i]);
            if (charIndex < Alphabet.Length - 1)
            {
                // We can increment this character - pick midpoint between current and 'z'
                chars[i] = Alphabet[(charIndex + Alphabet.Length) / 2];
                return new string(chars);
            }
        }

        // All characters are 'z', append 'n' to go after
        return before + "n";
    }

    private static string GetRankBetween(string before, string after)
    {
        // Normalize lengths
        var maxLen = Math.Max(before.Length, after.Length);
        var beforePadded = before.PadRight(maxLen, 'a');
        var afterPadded = after.PadRight(maxLen, 'a');

        var result = new char[maxLen];

        for (var i = 0; i < maxLen; i++)
        {
            var beforeIndex = Alphabet.IndexOf(beforePadded[i]);
            var afterIndex = Alphabet.IndexOf(afterPadded[i]);

            if (beforeIndex < afterIndex - 1)
            {
                // There's room between these characters
                result[i] = Alphabet[(beforeIndex + afterIndex) / 2];
                // Fill remaining with first char position
                for (var j = i + 1; j < maxLen; j++)
                {
                    result[j] = beforePadded[j];
                }
                return new string(result).TrimEnd('a').PadRight(DefaultLength, 'a');
            }

            if (beforeIndex < afterIndex)
            {
                // Adjacent characters, copy before and continue to next position
                result[i] = beforePadded[i];
            }
            else
            {
                // Same character, copy and continue
                result[i] = beforePadded[i];
            }
        }

        // Need to extend precision
        return before + "n";
    }
}
