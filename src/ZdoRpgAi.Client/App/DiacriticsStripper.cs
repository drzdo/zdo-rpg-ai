using System.Globalization;
using System.Text;

namespace ZdoRpgAi.Client.App;

public static class DiacriticsStripper {
    private static readonly Dictionary<char, string> ExtraReplacements = new() {
        ['ł'] = "l",
        ['Ł'] = "L",
        ['ð'] = "d",
        ['Ð'] = "D",
        ['þ'] = "th",
        ['Þ'] = "Th",
        ['æ'] = "ae",
        ['Æ'] = "Ae",
        ['ø'] = "o",
        ['Ø'] = "O",
        ['ß'] = "ss",
        ['đ'] = "d",
        ['Đ'] = "D",
        ['œ'] = "oe",
        ['Œ'] = "Oe",
        ['ħ'] = "h",
        ['Ħ'] = "H",
        ['ı'] = "i",
    };

    public static string Strip(string text) {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text) {
            if (ExtraReplacements.TryGetValue(c, out var replacement))
                sb.Append(replacement);
            else
                sb.Append(c);
        }

        var normalized = sb.ToString().Normalize(NormalizationForm.FormD);
        sb.Clear();
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
