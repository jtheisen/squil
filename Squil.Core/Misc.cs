using System;
using System.Collections.Generic;
using System.Text;

namespace Squil;

public static class MiscExtensions
{
    public static String ToSqlServerStringLiteral(this String s)
    {
        return $"'{s.Replace("'", "''")}'";
    }
}
