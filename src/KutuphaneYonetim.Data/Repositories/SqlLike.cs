namespace KutuphaneYonetim.Data.Repositories;

/// <summary>Builds LIKE patterns from user text so that wildcard characters are matched literally.</summary>
internal static class SqlLike
{
    /// <summary>Escape character passed to <c>EF.Functions.Like</c> together with the pattern.</summary>
    public const string Escape = "\\";

    /// <summary>A pattern that matches <paramref name="text"/> anywhere in the value.</summary>
    public static string Contains(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var escaped = text
            .Replace(Escape, Escape + Escape, StringComparison.Ordinal)
            .Replace("%", Escape + "%", StringComparison.Ordinal)
            .Replace("_", Escape + "_", StringComparison.Ordinal)
            .Replace("[", Escape + "[", StringComparison.Ordinal);
        return "%" + escaped + "%";
    }

    /// <summary>Page bounds for Skip/Take.</summary>
    public static int Skip(int page, int pageSize) => (Math.Max(page, 1) - 1) * pageSize;
}
