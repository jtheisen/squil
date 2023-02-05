using System.IO;
using System.Text;

namespace Squil;

public class UrlEncoder
{
    Boolean[] table;

    enum EncodingContext
    {
        PathPart,
        Path,
        QueryKey,
        QueryValue,
        Fragment
    }

    static IEnumerable<String> GetAcceptableCharacters(EncodingContext context)
    {
        // always fine
        yield return "-_.~";

        // fine in web urls after the host
        yield return "!*'();:@+$,%[]";

        if (context >= EncodingContext.Path) yield return "/";
        if (context >= EncodingContext.QueryKey) yield return "?";
        if (context >= EncodingContext.QueryValue) yield return "=";
        if (context >= EncodingContext.Fragment) yield return "#&";
    }

    UrlEncoder(EncodingContext context)
    {
        var acceptable = String.Join("", GetAcceptableCharacters(context));

        table = Enumerable.Range(0, 256).Select(i =>
        {
            if (i > 127) return true;

            var c = (Char)i;

            if (Char.IsLetterOrDigit(c)) return false;

            if (acceptable.Contains(c)) return false;

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

    static UrlEncoder[] encoders;

    static UrlEncoder()
    {
        encoders = Enum.GetValues<EncodingContext>().Select(c => new UrlEncoder(c)).ToArray();
    }

    static String UrlEncode(String text, EncodingContext context) => encoders[(int)context].Encode(text);

    public static String UrlEncodePath(String text) => UrlEncode(text, EncodingContext.Path);
    public static String UrlEncodePathPart(String text) => UrlEncode(text, EncodingContext.PathPart);
    public static String UrlEncodeQueryKey(String text) => UrlEncode(text, EncodingContext.QueryKey);
    public static String UrlEncodeQueryValue(String text) => UrlEncode(text, EncodingContext.QueryValue);
    public static String UrlEncodeFragment(String text) => UrlEncode(text, EncodingContext.Fragment);
}
