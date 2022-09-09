using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Squil;

public struct ValidationResult
{
    public ColumnType ColumnType { get; set; }

    public String Error { get; set; }

    public String Value { get; set; }

    public String SqlLowerValue { get; set; }

    public String SqlUpperValue { get; set; }

    public Boolean IsKeyValue { get; set; }

    public IndexDirection Direction { get; set; }

    public Boolean IsOk => Error == null;
}

public struct ColumnTypePrecisions
{

}

public static class ColumnTypeExtensions
{
    public static String GetSqlValue(this ValidationResult result) => result.Direction switch
    {
        IndexDirection.Desc => result.SqlUpperValue ?? result.SqlLowerValue,
        _ => result.SqlLowerValue
    };
}

public abstract class ColumnType
{
    public String Name { get; set; }

    public virtual String CssType => null;

    public virtual Boolean IsSupported => true;

    public ValidationResult Validate(String keyValue, String searchValue, IndexDirection direction, ColumnTypePrecisions precisions)
    {
        // We have decided that for now, SQuiL doesn't support nulls as key values. We probably do want
        // to support them as search values as that's far less unusual.

        var value = keyValue ?? searchValue;

        if (value == null)
        {
            return Ok(null);
        }

        var result = Validate(value, precisions);

        result.Value = value;
        result.IsKeyValue = keyValue != null;
        result.Direction = direction;

        return result;
    }

    public virtual void Init() { }

    protected abstract ValidationResult Validate(String text, ColumnTypePrecisions precisions);

    public static ColumnType Create<T>(String name)
        where T : ColumnType, new()
    {
        return new T { Name = name };
    }

    protected ValidationResult Issue(String error)
    {
        return new ValidationResult { ColumnType = this, Error = error };
    }

    protected ValidationResult Ok(String sqlValue, String upperSqlValue = null)
    {
        return new ValidationResult { ColumnType = this, SqlLowerValue = sqlValue, SqlUpperValue = upperSqlValue };
    }
}

public class UnknownColumnType : ColumnType
{
    public override Boolean IsSupported => false;

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        return Ok(text);
    }
}

public class CharacterColumnType : ColumnType
{
    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        return Ok(text);
    }
}

public enum DateTimeEndPositions
{
    Year = 4,
    Month = Year + 3,
    Day = Month + 3,
    Hour = Day + 3,
    Minute = Hour + 3,
    Second = Minute + 3
}

public class DateOrTimeColumnType : ColumnType
{
    static String LowerBasePattern = "0001-01-01 00:00:00";
    static String UpperBasePattern = "9999-12-31 23:59:59";

    String lpattern, upattern;
    String error;

    public Boolean WithDate { get; set; }

    public Boolean WithTime { get; set; }

    public Boolean WithOffset { get; set; }

    public override void Init()
    {
        var range = GetRange();

        lpattern = LowerBasePattern[range];
        upattern = UpperBasePattern[range];

        error = $"text should be of the form {lpattern}";
    }

    Range GetRange()
    {
        if (WithDate && WithTime)
        {
            return new Range(0, 19);
        }
        else if (WithDate)
        {
            return new Range(0, 10);
        }
        else if (WithTime)
        {
            return new Range(11, 19);
        }
        else
        {
            throw new Exception("Neither date nor time was selected");
        }
    }

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        if (text.Length > lpattern.Length) return Issue(error);

        for (var i = 0; i < text.Length; ++i)
        {
            var pid = Char.GetUnicodeCategory(lpattern[i]);
            var tid = Char.GetUnicodeCategory(text[i]);

            var isMatchingClass = pid == UnicodeCategory.SpaceSeparator || pid == UnicodeCategory.DecimalDigitNumber;

            if (pid != tid || (!isMatchingClass && (lpattern[i] != text[i]))) return Issue(error);
        }

        var result = Validate(text);

        if (WithOffset)
        {
            var suffix = DateTimeOffset.Now.Offset.ToString(@"\ \+hh\:mm");

            result.SqlLowerValue += suffix;
            result.SqlUpperValue += suffix;
        }

        return result;
    }

    ValidationResult Validate(String text)
    {
        var lc = text.LastOrDefault();

        if (WithDate)
        {
            if (text.StartsWith("0000"))
            {
                return Issue("there is no zero year");
            }
            else if (text.Length < 6)
            {
                return OkWithPattern(text);
            }
            else if (text.Length == 6)
            {
                switch (lc)
                {
                    case '0': return OkWithPattern(text, "", "9-30");
                    case '1': return OkWithPattern(text, "0", "2");
                    default: return Issue("invalid date");
                }
            }
            else
            {
                var month = Int32.Parse(text[5..7]);

                if (month == 0)
                {
                    return Issue("invalid date");
                }
                else if (month > 12)
                {
                    return Issue("invalid date");
                }

                var lastDay = DateTime.Parse(text[..7]).AddMonths(1).AddDays(-1).Day;

                var ld = lastDay.ToString();

                if (text.Length == 7)
                {
                    return OkWithPattern(text, "-", "-" + ld);
                }
                else if (text.Length == 8)
                {
                    return OkWithPattern(text, "", ld);
                }
                else if (text.Length == 9)
                {
                    switch (lc)
                    {
                        case '0': return OkWithPattern(text, "", "9");
                        case '1': return OkWithPattern(text, "0", "9");
                        case '2':
                            if (ld[0] == '2')
                            {
                                return OkWithPattern(text, "0", ld[1..2]);
                            }
                            else
                            {
                                return OkWithPattern(text);
                            }
                        case '3':
                            if (ld[0] == '2')
                            {
                                return Issue("invalid date");
                            }
                            else
                            {
                                return OkWithPattern(text, "0", ld[1..2]);
                            }
                        default:
                            return Issue("invalid date");
                    }
                }
                else
                {
                    var day = Int32.Parse(text[8..10]);

                    if (day > lastDay)
                    {
                        return Issue("invalid date");
                    }

                    if (text.Length >= 11 && !ValidateTime(text[11..], out var issue))
                    {
                        return Issue(issue);
                    }

                    return OkWithPattern(text);
                }
            }
        }
        else if (WithTime)
        {
            return ValidateTime(text, out var issue) ? OkWithPattern(text) : Issue(issue);
        }

        return Issue("internal validation error");
    }

    ValidationResult OkWithPattern(String text, String l = "", String u = "")
    {
        var ls = text.Length + l.Length;
        var us = text.Length + u.Length;

        return Ok(text + l + lpattern[ls..], text + u + upattern[us..]);
    }

    Boolean ValidateTime(String text, out String issue)
    {
        var complete = text + LowerBasePattern[(11 + text.Length)..];

        if (TimeSpan.TryParseExact(complete, "c", null, out var _))
        {
            issue = null;
        }
        else
        {
            issue = $"{text} is not a valid time";
        }

        return issue == null;
    }
}

public class IntegerColumnType : ColumnType
{
    public Boolean IsBit => Name.Equals("bit", StringComparison.InvariantCultureIgnoreCase);

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        if (Int64.TryParse(text, out var number))
        {
            return Ok(number.ToString());
        }

        return Issue($"text is not a valid {Name}");
    }
}

public class DecimalColumnType : ColumnType
{
    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        if (Decimal.TryParse(text, out var number))
        {
            return Ok(number.ToString());
        }

        return Issue($"text is not a valid {Name}");
    }
}

public class FloatColumnType : ColumnType
{
    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        if (Double.TryParse(text, out var number))
        {
            return Ok(number.ToString());
        }

        return Issue($"text is not a valid {Name}");
    }
}

public class GuidColumnType : ColumnType
{
    public override String CssType => "guid";

    static readonly Char[] ValidChars = "-0123456789abcdefABCDEF".ToArray();

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        if (String.IsNullOrWhiteSpace(text))
        {
            return Ok(Guid.Empty.ToString());
        }

        if (text.TrimStart(ValidChars).Length > 0)
        {
            return Issue($"{Name}s must contain only hexadecimal characters and dashes");
        }

        if (text.Length <= 6)
        {
            return Ok("00000000-0000-0000-0000-" + text.PadRight(12, '0'));
        }

        if (text.Length > 36)
        {
            return Issue($"{Name}s can't be longer than 12 characters");
        }

        if (Guid.TryParse(text, out var guid))
        {
            return Ok(guid.ToString());
        }

        return Issue($"text is not a valid {Name}");
    }
}

public class TypeRegistry
{
    public static readonly TypeRegistry Instance = new TypeRegistry();

    Dictionary<String, ColumnType> types;

    public TypeRegistry()
    {
        types = new Dictionary<String, ColumnType>();

        Register(new ColumnType[]
        {
            new CharacterColumnType { Name = "char" },
            new CharacterColumnType { Name = "varchar" },
            new CharacterColumnType { Name = "text" },
            new CharacterColumnType { Name = "nchar" },
            new CharacterColumnType { Name = "nvarchar" },
            new CharacterColumnType { Name = "ntext" },

            new IntegerColumnType { Name = "bigint" },
            new IntegerColumnType { Name = "bit" },
            new IntegerColumnType { Name = "smallint" },
            new IntegerColumnType { Name = "int" },
            new IntegerColumnType { Name = "tinyint" },

            new DecimalColumnType { Name = "decimal" },
            new DecimalColumnType { Name = "numeric" },
            new DecimalColumnType { Name = "money" },
            new DecimalColumnType { Name = "smallmoney" },

            new FloatColumnType { Name = "float" },
            new FloatColumnType { Name = "real" },

            new GuidColumnType { Name = "uniqueidentifier" },

            new DateOrTimeColumnType { Name = "time", WithTime = true },
            new DateOrTimeColumnType { Name = "date", WithDate = true },
            new DateOrTimeColumnType { Name = "datetime", WithDate = true, WithTime = true },
            new DateOrTimeColumnType { Name = "datetime2", WithDate = true, WithTime = true },
            new DateOrTimeColumnType { Name = "datetimeoffset", WithDate = true, WithTime = true, WithOffset = true },

            // missing: binary and specials
        });
    }

    void Register(IEnumerable<ColumnType> types)
    {
        foreach (var type in types)
        {
            type.Init();

            this.types[type.Name] = type;
        }
    }

    public ColumnType GetType(String name)
    {
        return types.Get(name, $"Unknown column type '{name}'");
    }

    public ColumnType GetTypeOrNull(String name)
    {
        if (types.TryGetValue(name, out var type))
        {
            return type;
        }
        else
        {
            return types[name] = new UnknownColumnType { Name = name };
        }
    }
}
