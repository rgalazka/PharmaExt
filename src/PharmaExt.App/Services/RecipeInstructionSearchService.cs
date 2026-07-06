using PharmaExt.App.Models;

namespace PharmaExt.App.Services;

public static class RecipeInstructionSearchService
{
    public static bool Matches(QualityDocument document, string searchText)
    {
        var terms = searchText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(term => term.Length > 0)
            .ToArray();

        if (terms.Length == 0)
        {
            return true;
        }

        var haystack = Normalize($"{document.Code} {document.Title} {document.ContentText}");
        return terms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant()
            .Replace("ą", "a")
            .Replace("ć", "c")
            .Replace("ę", "e")
            .Replace("ł", "l")
            .Replace("ń", "n")
            .Replace("ó", "o")
            .Replace("ś", "s")
            .Replace("ż", "z")
            .Replace("ź", "z");
    }
}
