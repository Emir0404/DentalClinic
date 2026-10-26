using System.Text;
using System.Text.RegularExpressions;

namespace DentalClinic.Services;

/// <summary>Pravi adresu od naziva: "Dr. Đorđe Šarić" postaje "dr-djordje-saric". Koristi se za nove doktore i usluge.</summary>
public static partial class Slugger
{
    public static string Make(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            sb.Append(ch switch
            {
                'č' or 'ć' => "c",
                'š' => "s",
                'ž' => "z",
                'đ' => "dj",
                _ => ch.ToString(),
            });
        }
        // ostali znakovi s dijakriticima (npr. é, ö) se rastavljaju pa im se kvačica odbaci
        var normalized = sb.ToString().Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        var slug = NonAlnum().Replace(ascii, "-").Trim('-');
        return slug.Length == 0 ? "stavka" : slug;
    }

    /// <summary>Vraća slug koji još niko ne koristi: "dr-ana", pa "dr-ana-2", "dr-ana-3"...</summary>
    public static string Unique(string text, Func<string, bool> exists)
    {
        var baseSlug = Make(text);
        var slug = baseSlug;
        for (var i = 2; exists(slug); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlnum();
}
