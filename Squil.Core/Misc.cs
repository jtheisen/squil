using TaskLedgering;

namespace Squil;

public enum QuerySearchMode
{
    Seek,
    Scan
}

public enum LocationQueryOperationType
{
    Insert = 1
}

public static class StaticSqlAliases
{
    public static String IsMatchingAlias = "__is-matching";
    public static String SchemaDateAlias = "__schema-date";
}

public static class MiscExtensions
{
    public static String ToSqlServerStringLiteral(this String s)
    {
        return $"'{s.Replace("'", "''")}'";
    }

    public static String ToSqlServerStringLiteralOrNull(this String s)
    {
        return s?.ToSqlServerStringLiteral() ?? "null";
    }

    public static String ToSqlServerLikeLiteralContent(this String s)
    {
        return s.Replace("'", "''").Replace("[", "[[").Replace("%", "[%]").Replace("_", "[_]");
    }

    public static String GetReportString(this Object o)
    {
        if (o is String s)
        {
            return s;
        }
        else if (o is IReportResult r)
        {
            return r.ToReportString();
        }
        else
        {
            return null;
        }
    }
}
