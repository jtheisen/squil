using Humanizer;
using Newtonsoft.Json;

namespace Squil;

public class EntityKey : IEquatable<EntityKey>
{
    String hashable;
    Int32 hashcode;

    public ObjectName TableName { get; }

    public String[] KeyValues { get; }

    public EntityKey(ObjectName tableName, String[] keyValues)
    {
        TableName = tableName;
        KeyValues = keyValues;

        hashable = $"{TableName.Escaped}\ue000{String.Join("\ue000", keyValues)}";
        hashcode = hashable.GetHashCode();
    }

    public override Int32 GetHashCode() => hashcode;

    public override Boolean Equals(Object obj) => obj is EntityKey other ? this.Equals(other) : false;

    public Boolean Equals(EntityKey other) => hashable == other.hashable;

    public static Boolean operator ==(EntityKey left, EntityKey right) => left.Equals(right);
    public static Boolean operator !=(EntityKey left, EntityKey right) => !left.Equals(right);
}

public class ChangeEntry
{
    public EntityKey EntityKey { get; set; }

    public Dictionary<String, String> EditValues { get; set; }
}

public enum EntityEditState
{
    Original,
    Modified,
    Closed
}

public class Entity
{
    public Extent Extent { get; set; }

    public CMTable Table { get; set; }

    public DateTime? SchemaDate { get; set; }

    public Boolean? IsMatching { get; set; }

    public Dictionary<String, String> ColumnValues { get; set; }

    public EntityEditState EditState { get; set; }

    public Dictionary<String, String> EditValues { get; private set; }

    public RelatedEntities[] Related { get; set; }

    public String GetDisplayValue(String columnName, Boolean preferEditValue)
    {
        if (EditValues != null && preferEditValue)
        {
            if (EditValues.ContainsKey(columnName))
            {
                return EditValues[columnName];
            }
        }

        return ColumnValues[columnName];
    }

    public void SetEditValues(ChangeEntry changes)
    {
        if (changes.EntityKey != this.GetEntityKey()) throw new Exception($"Key mismatch");

        foreach (var change in changes.EditValues)
        {
            SetEditValue(change.Key, change.Value);
        }
    }

    public void SetEditValue(String columnName, String value)
    {
        if (EditState == EntityEditState.Closed) throw new Exception("Can't edit closed entity");

        EditState = EntityEditState.Modified;

        if (EditValues == null)
        {
            EditValues = new Dictionary<String, String>();
        }

        EditValues[columnName] = value;
    }
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

public record SqlSelectable(Func<String, String> GetSql, String Alias);

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

    // A bit hacky, this will hopefully go once extents contain more information
    // about selected columns that merely their names.
    public String FocusColumn { get; set; }

    public SqlSelectable[] SqlSelectables { get; set; }

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
    ColumnPage,
    BreadcrumbList,
    BlockList,
    PageList,
    ColumnPageList
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

    public static EntityKey GetEntityKey(this Entity entity)
    {
        var columnValues = from c in entity.Table.PrimaryKey.Columns select entity.ColumnValues[c.c.Name];

        return new EntityKey(entity.Table.Name, columnValues.ToArray());
    }
}
