namespace DuplicateSnifferCli.Engine;

/// <summary>
/// Case-insensitive wildcard matcher supporting <c>*</c> (any sequence) and <c>?</c> (one char).
/// Backward-compatible with the original C++ <c>match()</c> implementation.
/// </summary>
public static class WildcardMatcher
{
    /// <summary>
    /// Returns true if <paramref name="text"/> matches <paramref name="pattern"/> in its entirety.
    /// Comparison is case-insensitive (ASCII upper-case fold, matching the C++ <c>toupper</c> behavior).
    /// </summary>
    public static bool Matches(string pattern, string text)
    {
        if (pattern is null) throw new ArgumentNullException(nameof(pattern));
        if (text is null) throw new ArgumentNullException(nameof(text));
        return MatchesCore(pattern.AsSpan(), text.AsSpan());
    }

    private static bool MatchesCore(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text)
    {
        int p = 0, t = 0;
        int starP = -1, starT = -1;

        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '?')
            {
                p++;
                t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                starP = p++;
                starT = t;
            }
            else if (p < pattern.Length && ToUpper(pattern[p]) == ToUpper(text[t]))
            {
                p++;
                t++;
            }
            else if (starP != -1)
            {
                p = starP + 1;
                t = ++starT;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
            p++;

        return p == pattern.Length;
    }

    private static char ToUpper(char c) => (c >= 'a' && c <= 'z') ? (char)(c - 32) : c;
}
