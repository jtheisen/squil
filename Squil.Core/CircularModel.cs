using System.Security.Cryptography;
using System.Text;
using Squil.SchemaBuilding;

namespace Squil;

[DebuggerDisplay("{name}")]
public class CMRoot
{
    String name;
    CMTable rootTable;
    AssocList<ObjectName, CMTable> tables;
    AssocList<ObjectName, CMIndexlike> keys = new AssocList<ObjectName, CMIndexlike>();

    public String Hash { get; }

    public DateTime TimeStamp { get; private set; }

    public IEnumerable<CMIndexlike> GetAllIndexes() =>
        from t in tables.Values
        from i in t.Indexes?.Values ?? Empties<CMIndexlike>.Enumerable
        select i;

    public CMTable GetTable(ObjectName name) => tables[name];
    public IEnumerable<CMTable> GetTables() => tables.Values;

    public CMRoot(String name, String hash = null)
    {
        Hash = hash;
        this.name = name;
    }

    public CMTable RootTable => rootTable;

    SHA1 sha1 = SHA1.Create();

    Double GetHueForName(string name)
    {
        return sha1.ComputeHash(Encoding.UTF8.GetBytes(name))[0] * 360.0 / 255.0;
    }

    public void Populate(CsdRoot root, Boolean includeViews = false)
    {
        var csdTables = root.Tables;

        if (!includeViews)
        {
            csdTables = csdTables.Where(t => t.Type != CsdTableType.View).ToArray();
        }

        tables = csdTables.Select(t => new CMTable
        {
            Csd = t,
            Root = this,
            Name = t.Name,
            Comment = t.Comment,
            UsedKb = t.UsedKb
        }
        ).ToAssocList(t => t.Name);

        foreach (var a in new Abbreviator().Calculate(tables.Values.Select(t => t.Name).ToArray()))
        {
            var table = tables[a.Key];
            table.Abbreviation = a.Value;
            table.Hue = GetHueForName(a.Value);
        }

        foreach (var csdTable in csdTables)
        {
            var table = tables[csdTable.Name];

            table.ColumnsInOrder = csdTable.Columns
                .Select((c, i) => new CMColumn
                {
                    Order = i,
                    Name = c.Name,
                    Comment = c.Comment,
                    SqlType = c.DataType,
                    IsNullable = c.IsNullable,
                    IsIgnoredByDefault = c.IsIgnoredByDefault,
                    IsAssemblyType = c.IsAssemblyType,
                    Type = TypeRegistry.Instance.GetTypeOrNull(c.DataType),
                    MaxLength = c.MaxLength,
                    IsString = c.DataType.EndsWith("char", StringComparison.InvariantCultureIgnoreCase)
                }).ToArray();

            table.Columns = table.ColumnsInOrder.ToDictionary(c => c.Name, c => c);

            (String tag, String reason, String specific)? CheckColumnSupport(IEnumerable<CMColumn> columns)
            {
                foreach (var column in columns)
                {
                    if (!column.Type.IsSupported) return (
                        "Unsupported column type",
                        "One ore more columns are not supported in indexes",
                        $"The unsupported column data type is '{column.Type.Name}' of column '{column.Name}'"
                    );
                }

                return null;
            }

            table.Indexes = (
                from i in csdTable.Keyishs.OfType<CsdIndexlike>()
                let columns = (
                    from csdColumn in i.Columns
                    let cmc = table.Columns[csdColumn.c]
                    select new CMDirectedColumn(cmc, csdColumn.d)
                ).ToArray()
                let support = i.UnsupportedReason ?? CheckColumnSupport(columns.Select(c => c.c))
                where i.Name != null
                select new CMIndexlike
                {
                    Name = i.Name.LastPart,
                    ObjectName = i.Name,
                    IsUnique = i.IsUnique,
                    IsPrimary = i.Type == CsdKeyishType.Pk,
                    IsClustered = i.IsClustered,
                    Table = table,
                    UnsupportedReason = support,
                    Columns = columns,
                    ColumnNames = i.Columns,
                    SerializedColumnNames = i.Columns.SerializeColumnNames(),
                    UsedKb = i.UsedKb
                    
                }
            ).ToAssocList(i => i.Name);

            table.UniqueIndexlikes = table.Indexes.Values
                .Where(i => i.IsUnique)
                .ToAssocList(t => t.Name, "unique indexlike");

            table.PrimaryKey = table.UniqueIndexlikes.Values.FirstOrDefault(i => i.IsPrimary);

            if (table.PrimaryKey != null)
            {
                var cs = table.PrimaryKey.Columns;

                for (var i = 0; i < cs.Length; ++i)
                {
                    cs[i].c.OrderInPrimaryKey = i;
                }
            }

            var clusti = table.Indexes.Values.FirstOrDefault(i => i.IsClustered);

            if (clusti != null)
            {
                var cs = clusti.Columns;

                for (var i = 0; i < cs.Length; ++i)
                {
                    cs[i].c.OrderInClusteredIndex = i;
                }
            }

            foreach (var key in table.UniqueIndexlikes)
            {
                keys.Append(key.Value.ObjectName, key.Value);
            }
        }

        foreach (var csdTable in csdTables)
        {
            var table = tables[csdTable.Name];

            table.ForeignKeys = csdTable.Keyishs.OfType<CsdForeignKey>()
                .Select(c => new CMForeignKey
                {
                    Name = c.Name.LastPart,
                    ObjectName = c.Name,
                    Principal = keys[c.ReferencedIndexlike],
                    Table = table,
                    Columns = c.Columns.Select(cc => new CMDirectedColumn(table.Columns[cc.c], IndexDirection.Unknown)).ToArray(),
                    ColumnNames = c.Columns,
                    SerializedColumnNames = c.Columns.SerializeColumnNames(),
                    UnsupportedReason = c.UnsupportedReason
                })
                .ToAssocList(c => c.Name, "foreign key");

            table.ColumnTuples =
                table.UniqueIndexlikes.Values.Cast<CMColumnTuple>()
                .Concat(table.ForeignKeys.Values)
                .ToAssocList(t => t.Name);

            foreach (var tuple in table.ColumnTuples.Values)
            {
                var hash = new HashSet<String>(tuple.Columns.Select(c => c.c.Name));

                foreach (var key in table.UniqueIndexlikes.Values)
                {
                    if (hash.IsSupersetOf(key.Columns.Select(c => c.c.Name)))
                    {
                        tuple.ContainsKey = true;

                        break;
                    }
                }
            }

            foreach (var fk in table.ForeignKeys.Values)
            {
                var fkColumns = fk.Columns.Select(c => c.c.Name).OrderBy(c => c).ToArray();

                Boolean IsIndexBacking(CMIndexlike index)
                {
                    var ixColumns = index.Columns.Take(fkColumns.Length).Select(c => c.c.Name).OrderBy(c => c);

                    return ixColumns.SequenceEqual(fkColumns);
                }

                fk.BackingIndexes = table.Indexes.Values.Where(IsIndexBacking).ToArray();
            }
        }

        TimeStamp = root.TimeStamp;
    }

    public void PopulateRoot()
    {
        // Any key of the dependent is suited here, because any key has the empty column sequence as a prefix.
        // The one specified is the one the root table will lead to.
        var relations = tables.Values.Select(table => new Relation
        {
            Principal = new RelationEnd { Name = table.Name.Simple, TableName = ObjectName.RootName, KeyName = "" },
            Dependent = new RelationEnd { Name = null, TableName = table.Name, KeyName = "" },
        }).ToArray();

        rootTable = new CMTable { Name = ObjectName.RootName, Root = this };

        tables.Prepend(ObjectName.RootName, rootTable);

        rootTable.ColumnTuples = new AssocList<String, CMColumnTuple>();
        rootTable.UniqueIndexlikes = new AssocList<String, CMIndexlike>();
        rootTable.ForeignKeys = new AssocList<String, CMForeignKey>();

        var rootKey = new CMIndexlike() { Name = "", IsPrimary = true, Columns = new CMDirectedColumn[0], Table = rootTable };

        rootTable.UniqueIndexlikes.Prepend("", rootKey);
        rootTable.ColumnTuples.Append("", rootKey);

        foreach (var table in tables.Values)
        {
            table.ForeignKeys.Append("", new CMForeignKey
            {
                Name = "",
                Principal = rootKey,
                Table = table,
                Columns = Empties<CMDirectedColumn>.Array,
                ColumnNames = Empties<DirectedColumnName>.Array,
                BackingIndexes = table.Indexes.Values.Where(i => i.IsSupported).ToArray()
            });
        }

        Populate(relations);
    }

    public void Populate(IEnumerable<Relation> relations)
    {
        foreach (var relation in relations)
        {
            var principalTable = GetTable(relation.Principal.TableName);
            var dependentTable = GetTable(relation.Dependent.TableName);

            static CMRelationEnd MakeRelationEnd(Boolean isPrincipalEnd, RelationEnd end, CMTable table, CMColumnTuple key) => new CMRelationEnd
            {
                Name = end.Name,
                Table = table,
                IsPrincipalEnd = isPrincipalEnd,
                IsMany = !key?.ContainsKey ?? true,
                Key = key,
                Columns = end.ColumnNames.Select(n => table.ColumnsInOrder
                    .Where(c => c.Name == n)
                    .Single($"Could not resolve column '{n}' in table '{table.Name.LastPart}'")
                ).ToArray()
            };

            var principalEnd = MakeRelationEnd(true, relation.Principal, principalTable, relation.Principal.KeyName?.Apply(n => principalTable.UniqueIndexlikes[n]));
            var dependentEnd = MakeRelationEnd(false, relation.Dependent, dependentTable, relation.Dependent.KeyName?.Apply(n => dependentTable.ForeignKeys[n]));

            principalEnd.OtherEnd = dependentEnd;
            dependentEnd.OtherEnd = principalEnd;

            principalEnd.Name?.Apply(n => principalTable.Relations.Add(n, dependentEnd));
            dependentEnd.Name?.Apply(n => dependentTable.Relations.Add(n, principalEnd));
        }
    }

    public void PopulateRelationsFromForeignKeys()
    {
        Populate(CreateRelations());
    }

    public IEnumerable<Relation> CreateRelations()
    {
        foreach (var table in tables.Values)
        {
            if (table.ForeignKeys == null) continue;

            foreach (var fk in table.ForeignKeys.Values)
            {
                // Those are cared for with better end names
                if (fk.Principal.Table == rootTable) continue;

                if (fk.UnsupportedReason != null) continue;

                if (fk.Principal.UnsupportedReason != null) continue;

                yield return new Relation
                {
                    Dependent = new RelationEnd { TableName = table.Name, Name = "D_" + fk.Name, KeyName = fk.Name, ColumnNames = fk.Columns.Select(c => c.c.Name).ToArray() },
                    Principal = new RelationEnd { TableName = fk.Principal.Table.Name, Name = "P_" + fk.Name, KeyName = fk.Principal.Name, ColumnNames = fk.Principal.Columns.Select(c => c.c.Name).ToArray() }
                };
            }
        }
    }

    public void Closeup()
    {
        CalculateUniquelyTypedRelations();
        CalculatePrimaryNames();
    }

    void CalculateUniquelyTypedRelations()
    {
        foreach (var t in tables.Values)
        {
            t.RelationsForTable =
                t.Relations.Values.ToLookup(r => r.Table.Name);
        }
    }

    void CalculatePrimaryNames()
    {
        foreach (var table in tables.Values)
        {
            var foreignNames = new HashSet<String>(
                from r in table.Relations.Values
                where r.IsMany
                from c in r.Columns
                select c.Name
            );

            var candidateColumns =
                table.ColumnsInOrder.Where(c => c.IsString && !foreignNames.Contains(c.Name)).ToArray();

            table.PrimaryNameColumn =
                candidateColumns.Where(c => c.Name.Equals("name", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault() ??
                candidateColumns.Where(c => c.Name.Equals("title", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault() ??
                candidateColumns.Where(c => c.Name.Contains("name", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault() ??
                candidateColumns.Where(c => c.Name.Contains("title", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault() ??
                candidateColumns.FirstOrDefault();

            table.PrimaryNameColumn?.Apply(c => c.IsPrimaryName = true);
        }
    }
}

[DebuggerDisplay("{Name}")]
public class CMTable : IWithUsedKb
{
    public static readonly CMColumn[] noColumns = new CMColumn[0];

    public Boolean IsRoot => Root.RootTable == this;

    public CMRoot Root { get; set; }

    public String Comment { get; set; }

    public CsdTable Csd { get; set; }

    public Dictionary<String, CMRelationEnd> Relations { get; } = new Dictionary<String, CMRelationEnd>();

    public ILookup<ObjectName, CMRelationEnd> RelationsForTable { get; set; }

    public ObjectName Name { get; set; }

    public String Abbreviation { get; set; } = "?";

    public Double Hue { get; set; } = 0;

    public CMColumn PrimaryNameColumn { get; set; }

    public CMColumn[] ColumnsInOrder { get; set; } = noColumns;

    public Dictionary<String, CMColumn> Columns = new Dictionary<String, CMColumn>();

    public Int32? UsedKb { get; set; }

    public CMIndexlike PrimaryKey { get; set; }

    public AssocList<String, CMIndexlike> Indexes { get; set; } = new AssocList<String, CMIndexlike>();
    public AssocList<String, CMColumnTuple> ColumnTuples { get; set; } = new AssocList<String, CMColumnTuple>();
    public AssocList<String, CMIndexlike> UniqueIndexlikes { get; set; } = new AssocList<String, CMIndexlike>();
    public AssocList<String, CMForeignKey> ForeignKeys { get; set; } = new AssocList<String, CMForeignKey>();
}

[DebuggerDisplay("{Name}")]
public abstract class CMColumnTuple
{
    public String Name { get; set; }

    public ObjectName ObjectName { get; set; }

    public CMTable Table { get; set; }

    public CMDirectedColumn[] Columns { get; set; }

    public DirectedColumnName[] ColumnNames { get; set; }

    public String SerializedColumnNames { get; set; }

    public Boolean ContainsKey { get; set; }

    public abstract Boolean IsDomestic { get; }
}

public class CMIndexlike : CMColumnTuple, IWithUsedKb
{
    public override Boolean IsDomestic => true;

    public Boolean IsPrimary { get; set; }

    public Boolean IsUnique { get; set; }

    public Boolean IsClustered { get; set; }

    public Int32? UsedKb { get; set; }

    public Boolean IsSupported => UnsupportedReason == null;

    public CsdUnsupportedReason UnsupportedReason { get; set; }
}

public class CMForeignKey : CMColumnTuple
{
    public override Boolean IsDomestic => false;

    public CMIndexlike[] BackingIndexes { get; set; } = Empties<CMIndexlike>.Array;

    public CMIndexlike Principal { get; set; }

    public CsdUnsupportedReason UnsupportedReason { get; set; }
}

[DebuggerDisplay("{OtherEnd.Name}->{Table.Name}")]
public class CMRelationEnd
{
    public String Name { get; set; }

    public CMTable Table { get; set; }

    public Boolean IsPrincipalEnd { get; set; }

    public Boolean IsMany { get; set; }

    // When this relation is the only one on this side to connect
    // the respective table on the other in the singular.
    public Boolean IsUniquelyTyped => AmbiguouslyTypedWitness == null;

    public CMRelationEnd AmbiguouslyTypedWitness => OtherEnd.Table.RelationsForTable[Table.Name].Where(r => IsMany == r.IsMany && r != this).FirstOrDefault();

    public CMIndexlike GetIndex() => Key is CMForeignKey fk ? fk.BackingIndexes.FirstOrDefault() : Key as CMIndexlike;

    public CMColumnTuple Key { get; set; }

    // redudant if we always demand a key
    public CMColumn[] Columns { get; set; }

    public CMRelationEnd OtherEnd { get; set; }

    public CMForeignKey ForeignKey
        => Key is CMForeignKey fk ? fk : OtherEnd.Key as CMForeignKey;


    #region Columns for UI

    public enum ColumnForUiType
    {
        Shared,
        Distinctive,
        Order
    }

    public (CMColumn c, ColumnForUiType t)[] ColumnsForUi => cachedColumnsForUi ??= GetColumnsForUi();

    (CMColumn, ColumnForUiType)[] cachedColumnsForUi;

    IEnumerable<CMRelationEnd> SameTableRelations
        => OtherEnd.Table.RelationsForTable[Table.Name].Where(r => IsMany == r.IsMany && r != this);

    IEnumerable<(CMColumn, ColumnForUiType)> GetFkColumnsForUi() =>
        from c in ForeignKey.Columns
        let isShared = SameTableRelations.All(r => r.ForeignKey.ColumnNames.Contains(c.Name))
        select (c.c, isShared ? ColumnForUiType.Shared : ColumnForUiType.Distinctive)
        ;

    (CMColumn, ColumnForUiType)[] GetColumnsForUi()
    {
        var columnsForUi = GetFkColumnsForUi().ToArray();

        if (IsMany && GetIndex() is CMIndexlike i)
        {
            return columnsForUi
                .Concat(i.Columns
                    .Skip(columnsForUi.Length)
                    .Select(ic => (ic.c, ColumnForUiType.Order)))
                .ToArray();
        }
        else
        {
            return columnsForUi;
        }
    }

    #endregion
}

[DebuggerDisplay("{Name}")]
public class CMColumn
{
    public Int32 Order { get; set; }

    public String Name { get; set; }

    public String Comment { get; set; }

    public String SqlType { get; set; }

    public Int32? OrderInPrimaryKey { get; set; }

    public Int32 OrderInClusteredIndex { get; set; }

    public String Escaped => Name.EscapeNamePart();

    public Boolean IsIgnoredByDefault { get; set; }

    public Boolean IsNullable { get; set; }

    public Boolean IsAssemblyType { get; set; }

    public ColumnType Type { get; set; }

    public Int32 MaxLength { get; set; }

    public Boolean IsString { get; set; }

    public Boolean IsPrimaryName { get; set; }
}

public interface IWithUsedKb
{
    public Int32? UsedKb { get; }
}

public static class CMExtensions
{
    public static IEnumerable<CMIndexlike> StartsWith(this IEnumerable<CMIndexlike> indexes, IEnumerable<String> columns, Boolean isPrefix = false, Boolean not = false)
    {
        var prefixColumns = columns.OrderBy(c => c).ToArray();

        Boolean HasIndexMatchingPrefix(CMIndexlike index)
        {
            var ixColumns = index.Columns
                .Take(prefixColumns.Length)
                .Select(c => c.c.Name)
                .OrderBy(c => c);

            return not != (ixColumns.SequenceEqual(prefixColumns) && (!isPrefix || index.Columns.Length > prefixColumns.Length));
        }

        return indexes.Where(HasIndexMatchingPrefix);
    }

    public static String SerializeColumnNames(this IEnumerable<DirectedColumnName> names)
    {
        return String.Join("", names.Select(n => $"{n.d.GetSymbolOnlyForDescending()}{n.Sql}"));
    }
}
