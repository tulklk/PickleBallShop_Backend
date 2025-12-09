using System.Text;
using System.Text.RegularExpressions;

namespace PickleBall.Infrastructure.Helpers;

public static class SlugHelper
{
    /// <summary>
    /// Generate SEO-friendly slug from Vietnamese text
    /// Example: "Áo Polo Pickleball Performance" -> "ao-polo-pickleball-performance"
    /// </summary>
    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Remove Vietnamese accents
        var normalized = RemoveVietnameseAccents(text);
        
        // Convert to lowercase
        normalized = normalized.ToLowerInvariant();
        
        // Replace spaces with hyphens
        normalized = Regex.Replace(normalized, @"\s+", "-", RegexOptions.Compiled);
        
        // Remove all invalid characters except hyphens
        normalized = Regex.Replace(normalized, @"[^a-z0-9\-]", "", RegexOptions.Compiled);
        
        // Replace multiple hyphens with single hyphen
        normalized = Regex.Replace(normalized, @"\-+", "-", RegexOptions.Compiled);
        
        // Trim hyphens from start and end
        normalized = normalized.Trim('-');
        
        return normalized;
    }

    /// <summary>
    /// Generate unique slug by appending number if slug already exists
    /// </summary>
    public static string GenerateUniqueSlug(string baseSlug, Func<string, Task<bool>> slugExistsAsync)
    {
        return GenerateUniqueSlugAsync(baseSlug, slugExistsAsync).GetAwaiter().GetResult();
    }

    public static async Task<string> GenerateUniqueSlugAsync(string baseSlug, Func<string, Task<bool>> slugExistsAsync)
    {
        if (string.IsNullOrWhiteSpace(baseSlug))
            return string.Empty;

        var slug = baseSlug;
        var counter = 1;

        while (await slugExistsAsync(slug))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        return slug;
    }

    /// <summary>
    /// Remove Vietnamese accents from text
    /// </summary>
    private static string RemoveVietnameseAccents(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}

