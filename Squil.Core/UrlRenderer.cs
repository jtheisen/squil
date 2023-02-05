using System.IO;
using System.Text;
using static System.Uri;

namespace Squil;

public class UrlRenderer
{
    private readonly String baseUrl;

    public static String BlazorDefeatingDummySegment = "blazor-defeating-dummy-segment";

    public UrlRenderer(String baseUrl)
    {
        this.baseUrl = baseUrl;
    }

    public String RenderUrl(IEnumerable<String> segments, IEnumerable<(String prefix, String key, String value)> queryParams)
    {
        var path = String.Join('/', from s in segments where s != null select EscapeDataString(s));

        var query = String.Join('&', from p in queryParams where p.key != null select $"{p.prefix}{EscapeDataString(p.key)}={EscapeDataString(p.value)}");

        var lastSlashI = Math.Max(0, path.LastIndexOf('/'));

        var haveDot = path.IndexOf('.', lastSlashI) >= 0;

        if (haveDot)
        {
            path = $"{path}/{BlazorDefeatingDummySegment}";
        }

        if (String.IsNullOrEmpty(query))
        {
            return $"{baseUrl}/{path}";
        }
        else
        {
            return $"{baseUrl}/{path}?{query}";
        }
    }
}


public class UrlEncoder
{
    Boolean[] table;

    public UrlEncoder()
    {
        var considered = "-_.~" + "!*'();:@+$,/%[]?"; //+ "=&#";

        table = Enumerable.Range(0, 256).Select(i =>
        {
            if (i > 127) return true;

            var c = (Char)i;

            if (Char.IsLetterOrDigit(c)) return false;

            if (considered.Contains(c)) return false;

            return true;
        }).ToArray();
    }

    public void WriteUrlEncoded(TextWriter writer, String text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);

        foreach (var b in utf8)
        {
            var i = (Int32)b;

            if (table[i])
            {
                writer.Write("%{0:x2}", i);
            }
            else
            {
                writer.Write((Char)b);
            }
        }
    }

    public String Encode(String text)
    {
        var writer = new StringWriter();
        WriteUrlEncoded(writer, text);
        return writer.ToString();
    }
}
