using Humanizer;
using Newtonsoft.Json;

namespace Squil;

public class DebugEntity
{
    public Dictionary<String, String> Columns { get; set; }

    public Dictionary<String, DebugEntity[]> Relations { get; set; }
}

public class Entity
{
    public DateTime? SchemaDate { get; set; }

    public Boolean? IsMatching { get; set; }

    public Dictionary<String, String> ColumnValues { get; set; }

    public RelatedEntities[] Related { get; set; }
}

[DebuggerDisplay("{RelationName}")]
public class RelatedEntities
{
    public String RelationName { get; set; }

    public ObjectName TableName { get; set; }

    [JsonIgnore]
    public CMRelationEnd RelationEnd { get; set; }

    [JsonIgnore]
    public Extent Extent { get; set; }

    public Entity[] List { get; set; }
}

public enum ScanOperator
{
    Equal,
    Substring
}

public record ScanMatchOption(DirectedColumnName Column, ScanOperator Operator, String Value);

[DebuggerDisplay("{DebuggerDisplay}")]
public class Extent
{
    public String RelationName { get; set; }

    public String RelationAlias { get; set; }

    public String RelationPrettyName { get; set; }

    public Boolean IgnoreOnRender { get; set; }

    public ExtentFlavor Flavor { get; set; }

    public String IndexName { get; set; }

    public Int32? Limit { get; set; }

    public String Alias { get; set; }

    public String[] Columns { get; set; }

    public DirectedColumnName[] Order { get; set; }

    public String[] Values { get; set; }

    public Int32 KeyValueCount { get; set; }

    public Extent[] Children { get; set; }

    public String ScanValue { get; set; }

    public ScanMatchOption[] ScanMatchOptions { get; set; }

    String DebuggerDisplay => RelationName ?? "<root>";
}

[DebuggerDisplay("{ToString()}")]
public struct ExtentFlavor
{
    public ExtentFlavorType type;

    public Int32 depth;

    public String GetCssValue(Boolean isLeaf)
    {
        return type.ToString().Kebaberize();
    }

    public override string ToString()
    {
        return $"{type}-{depth}";
    }

    public static implicit operator ExtentFlavor((ExtentFlavorType type, Int32 depth) flavor)
    {
        return new ExtentFlavor { type = flavor.type, depth = flavor.depth };
    }
}

public enum ExtentFlavorType
{
    None,
    Existence,
    Inline,
    Flow1,
    Flow3,
    Breadcrumb,
    Block,
    Page,
    BreadcrumbList,
    BlockList,
    PageList
}

public class Relation
{
    public RelationEnd Principal { get; set; }
    public RelationEnd Dependent { get; set; }
}

public class RelationEnd
{
    public static String[] EmptyColumnArrays = new String[0];

    public String Name { get; set; }

    public ObjectName TableName { get; set; }

    public String KeyName { get; set; }

    // redudant if we always demand a key
    public String[] ColumnNames { get; set; } = EmptyColumnArrays;
}

public struct RelatedEntitiesListItemAnnotationInfo
{
    public bool wasSearch;

    public int matchCount;
    public int afterCount;

    public IndexDirection direction;

    public string column;
    public string value;
}

public static class Extensions
{
    public static String GetRelationAlias(this Extent extent)
        => extent.RelationAlias ?? extent.RelationName.EscapeSqlServerXmlName();
}
