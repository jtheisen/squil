using System.IO;
using System.Text;

namespace Squil;

public class UrlEncoder
{
    String acceptableCharacters;

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
        yield return "!*'();:@$,%[]";

        if (context >= EncodingContext.Path) yield return "/";
        if (context >= EncodingContext.QueryKey) yield return "?";
        if (context >= EncodingContext.QueryValue) yield return "=";
        if (context >= EncodingContext.Fragment) yield return "#&";
    }

    UrlEncoder(EncodingContext context)
    {
        acceptableCharacters = String.Join("", GetAcceptableCharacters(context));
    }

    public UrlEncoder(String acceptableCharacters)
    {
        this.acceptableCharacters = acceptableCharacters;
    }

    public void WriteUrlEncoded(TextWriter writer, String text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);

        foreach (var b in utf8)
        {
            var c = (Char)b;

            if (b < 127 && IsAcceptable(c))
            {
                writer.Write((Char)b);
            }
            else
            {
                writer.Write("%{0:x2}", b);
            }
        }
    }

    Boolean IsAcceptable(Char c)
    {
        if (Char.IsLetterOrDigit(c))
        {
            return true;
        }
        else if (acceptableCharacters.Contains(c))
        {
            return true;
        }
        else
        {
            return false;
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
