using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Squil;

public struct ValidationResult
{
    public ColumnType ColumnType { get; set; }

    public String Error { get; set; }

    public String Value { get; set; }

    public String SqlValue { get; set; }

    public Boolean IsKeyValue { get; set; }

    public Boolean IsOk => Error == null;
}

public struct ColumnTypePrecisions
{

}

public static class ColumnTypeExtensions
{ 
}

public abstract class ColumnType
{
    public String Name { get; set; }

    public virtual Boolean IsSupported => true;

    public ValidationResult Validate(String keyValue, String searchValue, ColumnTypePrecisions precisions)
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

    protected ValidationResult Ok(String sqlValue)
    {
        return new ValidationResult { ColumnType = this, SqlValue = sqlValue };
    }
}

public class UnknownColumnType : ColumnType
{
    public override Boolean IsSupported => false;

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        return Issue("the data type of this column is unsupported");
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
    String pattern;
    String error;

    public Boolean WithDate { get; set; }

    public Boolean WithTime { get; set; }

    public Boolean WithOffset { get; set; }

    public override void Init()
    {
        if (WithDate && WithTime)
        {
            pattern = "0001-01-01 00:00:00";
        }
        else if (WithDate)
        {
            pattern = "0001-01-01";
        }
        else if (WithTime)
        {
            pattern = "00:00:00";
        }

        error = $"text should be of the form {pattern}";
    }

    protected override ValidationResult Validate(String text, ColumnTypePrecisions precisions)
    {
        if (text.Length > pattern.Length) return Issue(error);

        for (var i = 0; i < text.Length; ++i)
        {
            var pid = Char.GetUnicodeCategory(pattern[i]);
            var tid = Char.GetUnicodeCategory(text[i]);

            var isMatchingClass = pid == UnicodeCategory.SpaceSeparator || pid == UnicodeCategory.DecimalDigitNumber;

            if (pid != tid || (!isMatchingClass && (pattern[i] != text[i]))) return Issue(error);
        }

        var result = Validate(text);

        if (WithOffset)
        {
            result.SqlValue += " +00:00"; // FIXME
        }

        return result;
    }

    ValidationResult Validate(String text)
    {
        if (WithDate)
        {
            if (text.Length < 6)
            {
                return Ok(text + pattern[text.Length..]);
            }
            else if (text.Length == 6 && text[5] == '0')
            {
                return Ok(text + pattern[text.Length..]);
            }
            else if (text.Length == 9 && text[8] == '0')
            {
                return Ok(text + pattern[text.Length..]);
            }
            else if (text.Length <= 11)
            {
                return ParseDateTime(text.TrimEnd('-'));
            }
            else if (WithTime)
            {
                return ParseDateTime(text + pattern[text.Length..]);
            }
        }
        else if (WithTime)
        {
            return ParseTime(text + pattern[text.Length..]);
        }

        return Issue("internal validation error");
    }

    ValidationResult ParseDateTime(String text)
    {
        if (DateTime.TryParse(text, out var result))
        {
            return Ok(result.ToString("o")[..pattern.Length].Replace('T', ' '));
        }
        else
        {
            return Issue($"{text} is not a valid date");
        }
    }

    ValidationResult ParseTime(String text)
    {
        if (TimeSpan.TryParseExact(text, "c", null, out var result))
        {
            return Ok(result.ToString("c")[..pattern.Length]);
        }
        else
        {
            return Issue($"{text} is not a valid time");
        }
    }
}

public class IntegerColumnType : ColumnType
{
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
            new IntegerColumnType { Name = "smallintl" },
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
