using System.Globalization;
using System.Numerics;
using System.Text;

namespace Squil;

public struct ValidationResult
{
    public Int32 No { get; set; }

    public ColumnType ColumnType { get; set; }

    public String Error { get; set; }

    public String Value { get; set; }

    public String SqlLowerValue { get; set; }

    public String SqlUpperValue { get; set; }

    public Boolean IsKeyValue { get; set; }

    public IndexDirection Direction { get; set; }

    public Boolean IsOk => Error == null;
}

public record ScanOptionResult(ScanOperator Operator, String Value);

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

    public virtual Boolean UseSpecialValueForKeysOnInsert => false;

    public virtual String SpecialValueOrNull => null;

    public ValidationResult Validate(Int32 no, String keyValue, String searchValue, IndexDirection direction, ColumnTypePrecisions precisions)
    {
        // We have decided that for now, SQuiL doesn't support nulls as key values. We probably do want
        // to support them as search values as that's far less unusual.

        var value = keyValue ?? searchValue;

        if (value == null)
        {
            return Ok(null);
        }

        ValidationResult result = default;

        if (keyValue != null)
        {
            result.SqlUpperValue = result.SqlLowerValue = value;
            result.Direction = direction;
        }
        else
        {
            result = Validate(value, precisions);
        }

        result.No = no;
        result.Value = value;
        result.IsKeyValue = keyValue != null;
        result.Direction = direction;

        return result;
    }

    public virtual void Init() { }

    protected abstract ValidationResult Validate(String text, ColumnTypePrecisions precisions);

    public virtual ScanOptionResult GetScanOptionOrNull(String value) => null;

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
    public override String SpecialValueOrNull => "";

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        return Ok(text);
    }

    public override ScanOptionResult GetScanOptionOrNull(String value)
    {
        return new ScanOptionResult(ScanOperator.Substring, value);
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

    public Int32 MinYear { get; set; } = 1;

    public Boolean WithDate { get; set; }

    public Boolean WithTime { get; set; }

    public Boolean WithOffset { get; set; }

    public override Boolean UseSpecialValueForKeysOnInsert => true;

    public override String SpecialValueOrNull
    {
        get
        {
            var now = DateTimeOffset.Now;

            var format = GetDotNetPattern();

            return now.ToString(format);
        }
    }

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

            if (i == 10)
            {
                if (tid != UnicodeCategory.SpaceSeparator && text[i] != 'T' && text[i] != 't') return Issue(error);

                if (text[i] != ' ')
                {
                    var builder = new StringBuilder(text);
                    builder[i] = ' ';
                    text = builder.ToString();
                }
            }
            else
            {
                if (pid != tid) return Issue(error);

                if (pid != UnicodeCategory.DecimalDigitNumber && lpattern[i] != text[i]) return Issue(error);                
            }
        }

        var result = Validate(text);

        if (result.IsOk && MinYear > 1)
        {
            var year = Int32.Parse(result.SqlLowerValue[..4]);

            if (year < MinYear) return Issue($"The earliest year in a {Name} is {MinYear}");
        }

        if (result.IsOk && WithOffset)
        {
            var offset = DateTimeOffset.Now.Offset;

            result.SqlLowerValue = GetWithOffset(result.SqlLowerValue, offset);
            result.SqlUpperValue = GetWithOffset(result.SqlUpperValue, offset);
        }

        return result;
    }

    static readonly Int64 minTicks = DateTime.MinValue.Ticks;
    static readonly Int64 maxTicks = DateTime.MaxValue.Ticks;

    String GetWithOffset(String source, TimeSpan offset)
    {
        var parsed = DateTime.Parse(source);

        return ToOffset(parsed, offset).ToString("yyyy-MM-dd HH:mm:ss K");
    }

    DateTimeOffset ToOffset(DateTime datetime, TimeSpan offset)
    {
        var ticks = Math.Clamp(datetime.Ticks - offset.Ticks, minTicks, maxTicks);

        return new DateTimeOffset(ticks + offset.Ticks, offset);
    }

    String GetDotNetPattern()
    {
        var builder = new List<String>();

        if (WithDate)
        {
            builder.Add("yyyy-MM-dd");
        }

        if (WithTime)
        {
            builder.Add("HH:mm:ss");
        }

        if (WithOffset)
        {
            builder.Add("K");
        }

        return String.Join(' ', builder);
    }

    ValidationResult Validate(String text)
    {
        var lc = text.LastOrDefault();

        if (WithDate)
        {
            if (text.Length == 0)
            {
                return OkWithPattern(MinYear.ToString().PadLeft(4, '0'));
            }
            else if (text.StartsWith("0000"))
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

                var lastDay = text.StartsWith("9999-12") ? 31 : DateTime.Parse(text[..7]).AddMonths(1).AddDays(-1).Day;

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

    public Int32 Bits { get; set; }

    public Boolean IsSigned { get; set; } = true;

    public override String SpecialValueOrNull => "0";

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        if (String.IsNullOrWhiteSpace(text))
        {
            return Ok((-1 << Bits).ToString(), (1 << Bits).ToString());
        }
        else if (TryParse(text, out var normalized, out var error))
        {
            return Ok(normalized);
        }
        else
        {
            return Issue(error);
        }
    }

    public override ScanOptionResult GetScanOptionOrNull(String value)
    {
        if (TryParse(value, out var normalized, out _))
        {
            return new ScanOptionResult(ScanOperator.Equal, normalized);
        }
        else
        {
            return null;
        }
    }

    Boolean TryParse(String text, out String normalized, out String error)
    {
        normalized = error = null;

        if (BigInteger.TryParse(text, NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            error = CheckRange(number);

            normalized = number.ToString();
        }
        else
        {
            error = $"text is not a valid integer";
        }

        return error == null;
    }

    String CheckRange(BigInteger value)
    {
        if (!IsSigned && value < 0)
        {
            return "the value must be non-negative";
        }

        var maxValue = BigInteger.One << Bits;
        var minValue = -BigInteger.One << Bits;

        if (value > maxValue)
        {
            return $"value can't be greater than {maxValue}";
        }
        else if (value < minValue)
        {
            return $"value can't be smaller than {minValue}";
        }
        else
        {
            return null;
        }
    }
}

public class DecimalColumnType : ColumnType
{
    public override String SpecialValueOrNull => "0";

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
    public override String SpecialValueOrNull => "0";

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

    public override Boolean UseSpecialValueForKeysOnInsert => true;

    public override String SpecialValueOrNull => Guid.NewGuid().ToString();

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
            if (text.IndexOf('-') >= 0)
            {
                return Issue($"text is not a valid {Name}");
            }
            else
            {
                return Ok("00000000-0000-0000-0000-" + text.PadRight(12, '0'));
            }
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

    public override ScanOptionResult GetScanOptionOrNull(String value)
    {
        if (Guid.TryParse(value, out var guid))
        {
            return new ScanOptionResult(ScanOperator.Equal, guid.ToString());
        }
        else
        {
            return null;
        }
    }
}

public class BinaryColumnType : ColumnType
{
    public override Boolean IsSupported => false;

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        throw new NotImplementedException();
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

            new BinaryColumnType { Name = "binary" },
            new BinaryColumnType { Name = "varbinary" },
            new BinaryColumnType { Name = "image" },

            new IntegerColumnType { Name = "bit", Bits = 1, IsSigned = false },
            new IntegerColumnType { Name = "tinyint", Bits = 7 },
            new IntegerColumnType { Name = "smallint", Bits = 15 },
            new IntegerColumnType { Name = "int", Bits = 31 },
            new IntegerColumnType { Name = "bigint", Bits = 63 },

            new DecimalColumnType { Name = "decimal" },
            new DecimalColumnType { Name = "numeric" },
            new DecimalColumnType { Name = "money" },
            new DecimalColumnType { Name = "smallmoney" },

            new FloatColumnType { Name = "float" },
            new FloatColumnType { Name = "real" },

            new GuidColumnType { Name = "uniqueidentifier" },

            new DateOrTimeColumnType { Name = "time", WithTime = true },
            new DateOrTimeColumnType { Name = "date", WithDate = true },
            new DateOrTimeColumnType { Name = "datetime", WithDate = true, WithTime = true, MinYear = 1753 },
            new DateOrTimeColumnType { Name = "datetime2", WithDate = true, WithTime = true },
            new DateOrTimeColumnType { Name = "datetimeoffset", WithDate = true, WithTime = true, WithOffset = true },

            // missing: specials
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
