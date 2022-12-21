using Humanizer;
using Newtonsoft.Json;
using System.Xml.Linq;
using static Squil.StaticSqlAliases;

namespace Squil;

public class EntityKey : IEquatable<EntityKey>
{
    String hashable;
    Int32 hashcode;

    public ObjectName TableName { get; }

    public (String c, String v)[] KeyColumnsAndValues { get; }

    public Dictionary<String, String> GetKeyColumnsAndValuesDictionary() => KeyColumnsAndValues.ToDictionary(p => p.c, p => p.v);

    public EntityKey(ObjectName tableName, (String c, String v)[] keyColumnsAndValues)
    {
        TableName = tableName;
        KeyColumnsAndValues = keyColumnsAndValues;

        foreach (var kv in KeyColumnsAndValues)
        {
            if (kv.v == null) throw new Exception($"Value for column {kv.c} is null, which is invalid for key values");
        }

        hashable = $"{TableName.Escaped}\ue000{String.Join("\ue000", from p in keyColumnsAndValues select p.v)}";
        hashcode = hashable.GetHashCode();
    }

    public override Int32 GetHashCode() => hashcode;

    public override Boolean Equals(Object obj) => obj is EntityKey other ? this.Equals(other) : false;

    public Boolean Equals(EntityKey other) => hashable == other?.hashable;

    public static Boolean operator ==(EntityKey left, EntityKey right) => left.Equals(right);
    public static Boolean operator !=(EntityKey left, EntityKey right) => !left.Equals(right);
}

public enum ChangeOperationType
{
    Update,
    Insert,
    Delete
}

public class ChangeEntry
{
    public ChangeOperationType Type { get; set; }

    public ObjectName Table { get; set; }

    public EntityKey EntityKey { get; set; }

    public Dictionary<String, String> EditValues { get; set; }

    public Boolean IsKeyed => EntityKey is not null;

    public static ChangeEntry Update(EntityKey key, Dictionary<String, String> values)
        => new ChangeEntry { Type = ChangeOperationType.Update, Table = key.TableName, EntityKey = key, EditValues = values };

    public static ChangeEntry Insert(EntityKey key, Dictionary < String, String> values )
        => new ChangeEntry { Type = ChangeOperationType.Insert, Table = key.TableName, EntityKey = key, EditValues = values };

    public static ChangeEntry Insert(ObjectName table, Dictionary<String, String> values)
        => new ChangeEntry { Type = ChangeOperationType.Insert, Table = table, EditValues = values };

    public static ChangeEntry Delete(EntityKey key)
        => new ChangeEntry { Type = ChangeOperationType.Delete, Table = key.TableName, EntityKey = key };
}

public enum EntityEditState
{
    Original,
    Modified,
    Closed
}

public class Entity : IMapping<String, String>
{
    public Extent Extent { get; set; }

    public CMTable Table { get; set; }

    public Boolean IsUnkeyed { get; set; }

    public DateTime? SchemaDate { get; set; }

    public Boolean? IsMatching { get; set; }

    public Dictionary<String, String> ColumnValues { get; set; }

    public EntityEditState EditState { get; set; }

    public Dictionary<String, String> EditValues { get; private set; }

    public RelatedEntities[] Related { get; set; }

    String IMapping<String, String>.GetValue(String key) => ColumnValues[key];

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

    public static Extent GetSubExtent(this Extent extent, String alias)
        => extent.Children.FirstOrDefault(e => e.RelationAlias == alias);

    public static Extent GetPrimariesSubExtent(this Extent extent)
        => extent.GetSubExtent(PrimariesRelationAlias);

    public static Extent GetPrincipalSubExtent(this Extent extent)
        => extent.GetSubExtent(PrincipalRelationAlias);

    public static EntityKey GetEntityKey(this Entity entity)
    {
        if (entity.IsUnkeyed) throw new Exception("Can't get key from unkeyed entity");

        var columnsAndValues = from c in entity.Table.PrimaryKey.Columns select (c.c.Name, entity.ColumnValues[c.c.Name]);

        return new EntityKey(entity.Table.Name, columnsAndValues.ToArray());
    }

    public static Entity MakeEntity(this Extent extent, CMTable table, XElement element, Boolean isRoot = false)
    {
        var data = element.Attributes().ToDictionary(a => a.Name.LocalName.UnescapeSqlServerXmlName(), a => a.Value);

        return new Entity
        {
            Extent = extent,
            Table = table,
            SchemaDate = isRoot ? data.GetOrDefault(SchemaDateAlias)?.Apply(DateTime.Parse) : null,
            IsMatching = data.GetOrDefault(IsMatchingAlias)?.Apply(im => im == "1"),
            ColumnValues = extent.Columns?.ToDictionary(c => c, c => data.GetValueOrDefault(c)) ?? Empties<String, String>.Dictionary,
            Related = extent.Children?.Select(c => MakeEntities(c, table, element.Element(XName.Get(c.GetRelationAlias())))).ToArray()
        };
    }

    public static RelatedEntities MakeEntities(this Extent extent, CMTable parentTable, XElement element)
    {
        var forwardEnd = parentTable.Relations.GetValueOrDefault(extent.RelationName) ?? throw new Exception(
            $"Can't find relation for name {extent.RelationName} in table {parentTable.Name.LastPart ?? "<root>"}"
        );

        var table = forwardEnd.OtherEnd.Table;

        return new RelatedEntities
        {
            Extent = extent,
            RelationEnd = forwardEnd,
            RelationName = extent.RelationName,
            TableName = forwardEnd.Table.Name,
            List = element?.Elements().Select(e => MakeEntity(extent, forwardEnd.Table, e)).ToArray() ?? new Entity[0]
        };
    }

    public static Entity MakeDummyEntity(this Extent extent, CMTable table)
    {
        return new Entity
        {
            Extent = extent,
            Table = table,
            IsUnkeyed = true,
            ColumnValues = extent.Columns?.ToDictionary(c => c, c => null as String) ?? Empties<String, String>.Dictionary,
            Related = extent.Children?.Select(c => MakeDummyEntities(c, table)).ToArray()
        };
    }

    public static RelatedEntities MakeDummyEntities(this Extent extent, CMTable parentTable)
    {
        var forwardEnd = parentTable.Relations.GetValueOrDefault(extent.RelationName) ?? throw new Exception(
            $"Can't find relation for name {extent.RelationName} in table {parentTable.Name.LastPart ?? "<root>"}"
        );

        return new RelatedEntities
        {
            Extent = extent,
            RelationEnd = forwardEnd,
            RelationName = extent.RelationName,
            TableName = forwardEnd.Table.Name,
            List = new[] { MakeDummyEntity(extent, forwardEnd.Table) }
        };
    }

}
