using System;
using System.Collections.Generic;
using System.Text;

namespace Squil;

public enum QuerySearchMode
{
    Seek,
    Scan
}

public static class MiscExtensions
{
    public static String ToSqlServerStringLiteral(this String s)
    {
        return $"'{s.Replace("'", "''")}'";
    }

    public static String ToSqlServerLikeLiteralContent(this String s)
    {
        return s.Replace("'", "''").Replace("[", "[[").Replace("%", "[%]").Replace("_", "[_]");
    }
}
