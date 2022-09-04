using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Squil;

public enum IndexDirection
{
    Unknown = 0,
    Asc = 1,
    Desc = -1
}

public static class IndexDirectionExtensions
{
    public static IndexDirection Invert(this IndexDirection direction) => (IndexDirection)(-(int)direction);

    public static Char GetSymbol(this IndexDirection direction) => direction switch
    {
        IndexDirection.Asc => '+',
        IndexDirection.Desc => '-',
        _ => '?'
    };

    public static String GetSqlSuffix(this IndexDirection direction) => direction switch
    {
        IndexDirection.Asc => " asc",
        IndexDirection.Desc => " desc",
        _ => ""
    };

    public static String GetOperator(this IndexDirection direction) => direction switch
    {
        IndexDirection.Asc => ">=",
        IndexDirection.Desc => "<=",
        _ => ">="
    };

    public static String GetPrettyOperator(this IndexDirection direction) => direction switch
    {
        IndexDirection.Asc => "≥",
        IndexDirection.Desc => "≤",
        _ => ">="
    };

    public static String GetOperator(this IndexDirection direction, Boolean useEquality)
        => useEquality ? "=" : direction.GetOperator();
}

[DebuggerDisplay("{ToString()}")]
public struct CMDirectedColumn
{
    public IndexDirection d;

    public CMColumn c;

    public DirectedColumnName Name => new DirectedColumnName(c.Name, d);

    public CMDirectedColumn(CMColumn column, IndexDirection direction = IndexDirection.Unknown)
    {
        c = column;
        d = direction;
    }

    public override String ToString() => $"{d.GetSymbol()}{c.Name}";

    public static implicit operator CMColumn(CMDirectedColumn self) => self.c;
    public static CMDirectedColumn operator ~(CMDirectedColumn self) => self with { d = self.d.Invert() };
}

[DebuggerDisplay("{ToString()}")]
public struct DirectedColumnName
{
    public IndexDirection d;

    public String c;

    public DirectedColumnName(String column, IndexDirection direction = IndexDirection.Unknown)
    {
        c = column;
        d = direction;
    }

    public override String ToString() => $"{d.GetSymbol()}{c}";

    public String Sql => c.EscapeNamePart();

    public static implicit operator String(DirectedColumnName self) => self.c;
    public static implicit operator DirectedColumnName(String column) => new DirectedColumnName(column);
    public static DirectedColumnName operator ~(DirectedColumnName self) => self with { d = self.d.Invert() };
}
