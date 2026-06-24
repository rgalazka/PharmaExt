using System.Text;

namespace PharmaExt.App.Pdf;

public static class Code128Barcode
{
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    ];

    public static IReadOnlyList<bool> CreateModules(string value)
    {
        var text = Sanitize(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = " ";
        }

        var codes = new List<int> { 104 };
        foreach (var character in text)
        {
            codes.Add(character - 32);
        }

        var checksum = 104;
        for (var index = 1; index < codes.Count; index++)
        {
            checksum += codes[index] * index;
        }

        codes.Add(checksum % 103);
        codes.Add(106);

        var modules = new List<bool>();
        modules.AddRange(Enumerable.Repeat(false, 10));
        foreach (var code in codes)
        {
            var drawBar = true;
            foreach (var widthChar in Patterns[code])
            {
                var width = widthChar - '0';
                modules.AddRange(Enumerable.Repeat(drawBar, width));
                drawBar = !drawBar;
            }
        }

        modules.AddRange(Enumerable.Repeat(false, 10));
        return modules;
    }

    public static string CreateSvg(string value, int height = 34)
    {
        var modules = CreateModules(value);
        var builder = new StringBuilder();
        builder.Append($"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {modules.Count} {height}" preserveAspectRatio="none">""");
        builder.Append("""<rect width="100%" height="100%" fill="white"/>""");

        for (var index = 0; index < modules.Count; index++)
        {
            if (modules[index])
            {
                builder.Append($"""<rect x="{index}" y="0" width="1" height="{height}" fill="black"/>""");
            }
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim())
        {
            builder.Append(character is >= ' ' and <= '~' ? character : '?');
        }

        return builder.ToString();
    }
}
