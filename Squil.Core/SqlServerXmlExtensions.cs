using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace Squil;

public static class SqlServerXmlExtensions
{
    static XmlCharType xmlCharType = XmlCharType.Instance;

    public static String EscapeSqlServerXmlName(this String name)
    {
        var writer = new StringWriter();

        int flag = XmlCharType.fNCStartNameSC;

        var n = name.Length;

        for (int i = 0; i < n; ++i)
        {
            var c = name[i];

            var isEscape = c == '_' && i < n - 1 && name[i + 1] == 'x';

            var isValid = (xmlCharType.charProperties[c] & flag) != 0;

            if (!isEscape && isValid)
            {
                writer.Write(c);
            }
            else
            {
                writer.Write("_x");
                writer.Write(((int)c).ToString("X4"));
                writer.Write('_');
            }

            flag = XmlCharType.fNCNameSC;
        }

        return writer.ToString();
    }

    static Regex sqlServerNameEscapePattern = new Regex("_x([0-9A-Fa-f]{4})_");

    public static String UnescapeSqlServerXmlName(this String name)
        => sqlServerNameEscapePattern.Replace(name, m => Char.ConvertFromUtf32(Int32.Parse(m.Groups[1].Value, NumberStyles.HexNumber)));
}
