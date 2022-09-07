using Squil.Core;
using ColorHelper;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Data.Common;

namespace Squil
{
    [DebuggerDisplay("{name}")]
    public class CMRoot
    {
        String name;
        CMTable rootTable;
        Dictionary<ObjectName, CMTable> tables;
        Dictionary<ObjectName, CMIndexlike> keys;

        public IEnumerable<CMIndexlike> GetAllIndexes() =>
            from t in tables.Values
            from i in t.Indexes?.Values ?? Empties<CMIndexlike>.Enumerable
            select i;

        public CMTable GetTable(ObjectName name) => tables[name];

        public CMRoot(String name)
        {
            this.name = name;
            tables = new Dictionary<ObjectName, CMTable>();
            keys = new Dictionary<ObjectName, CMIndexlike>();
        }

        public CMTable RootTable => rootTable;

        SHA1 sha1 = SHA1.Create();

        Double GetHueForName(string name)
        {
            return sha1.ComputeHash(Encoding.UTF8.GetBytes(name))[0] * 360.0 / 255.0;
        }

        public void Populate(ISRoot isRoot, SysRoot sysRoot = null, Boolean includeViews = false)
        {
            var sysTables = sysRoot?.Apply(r => (
                from s in r.Schemas
                from t in s.Tables
                select (s, t)
            ).ToLookup(p => (p.s.Name, p.t.Name), p => p.t));

            var isTables = isRoot.Tables.ToArray();

            if (!includeViews)
            {
                isTables = isTables.Where(t => t.Type != "VIEW").ToArray();
            }

            tables = isTables.Select(t => new CMTable
            {
                Root = this,
                Name = t.GetName(),
            }
            ).ToDictionary(t => t.Name, t => t);

            foreach (var a in new Abbreviator().Calculate(tables.Values.Select(t => t.Name).ToArray()))
            {
                var table = tables[a.Key];
                table.Abbreviation = a.Value;
                table.Hue = GetHueForName(a.Value);
            }

            foreach (var isTable in isTables)
            {
                var table = tables[isTable.GetName()];

                var sysTable = sysTables?[(isTable.Schema, isTable.Name)].Single($"Can't find sys table for table {isTable.Schema}.{isTable.Name}");

                table.ColumnsInOrder = isTable.Columns
                    .Where(c => c.DATA_TYPE != "varbinary" && c.DATA_TYPE != "geography")
                    .Select((c, i) => new CMColumn
                    {
                        Order = i,
                        Name = c.COLUMN_NAME,
                        SqlType = c.DATA_TYPE,
                        IsNullable = c.IS_NULLABLE == "YES",
                        Type = TypeRegistry.Instance.GetTypeOrNull(c.DATA_TYPE),
                        IsString = c.DATA_TYPE.EndsWith("char", StringComparison.InvariantCultureIgnoreCase)
                    }).ToArray();

                table.Columns = table.ColumnsInOrder.ToDictionary(c => c.Name, c => c);

                var sysColumns = sysTable?.Columns.ToLookup(c => c.ColumnId, c => c);

                (String tag, String reason)? CheckColumnSupport(IEnumerable<CMColumn> columns)
                {
                    foreach (var column in columns)
                    {
                        if (!column.Type.IsSupported) return ("column", $"unsupported column data type");
                    }

                    return null;
                }

                table.Indexes = (sysTable?.Indexes ?? Empties<SysIndex>.Array).Apply(indexes =>
                    from i in indexes
                    let columns = (
                        from ci in i.Columns
                        from c in sysColumns[ci.ColumnId]
                        let cmc = table.Columns[c.Name]
                        select new CMDirectedColumn(cmc, ci.IsDescendingKey ? IndexDirection.Desc : IndexDirection.Asc)
                    ).ToArray()
                    let support = i.CheckIntrinsicSupport() ?? CheckColumnSupport(columns.Select(c => c.c))
                    where i.Name != null
                    select new CMIndexlike
                    {
                        Name = i.Name,
                        ObjectName = new ObjectName(isTable.Catalog, isTable.Schema, i.Name),
                        IsUnique = i.IsUnique,
                        IsPrimary = i.IsPrimary,
                        Table = table,
                        UnsupportedTag = support?.tag,
                        UnsupportedReason = support?.reason,
                        Columns = columns
                    }
                ).ToDictionary(i => i.Name, i => i);

                var keysUnknownFromIndexes = (
                    from c in isTable.Constraints
                    // When constraints appear as indexes, the index version has more complete information
                    where !table.Indexes.ContainsKey(c.Name)
                    where c.CONSTRAINT_TYPE == "PRIMARY KEY" || c.CONSTRAINT_TYPE == "UNIQUE KEY"
                    select new CMIndexlike
                    {
                        ObjectName = c.GetName(),
                        Name = c.Name,
                        IsPrimary = c.CONSTRAINT_TYPE == "PRIMARY KEY",
                        Table = table,
                        Columns = c.Columns.Select(cc => new CMDirectedColumn(table.Columns[cc.COLUMN_NAME], IndexDirection.Unknown)).ToArray()
                    }
                ).ToArray();

                table.UniqueIndexlikes = table.Indexes.Values
                    .Cast<CMIndexlike>()
                    .Where(i => i.IsUnique)
                    .Concat(keysUnknownFromIndexes)
                    .ToDictionary(t => t.Name, t => t);

                table.PrimaryKey = table.UniqueIndexlikes.Values.FirstOrDefault(i => i.IsPrimary);

                foreach (var key in table.UniqueIndexlikes)
                {
                    keys.Add(key.Value.ObjectName, key.Value);
                }
            }

            foreach (var isTable in isTables)
            {
                var table = tables[isTable.GetName()];

                table.ForeignKeys = isTable.Constraints
                    .Where(c => c.CONSTRAINT_TYPE == "FOREIGN KEY")
                    .Select(c => new CMForeignKey
                    {
                        Name = c.Name,
                        Principal = keys[c.Referentials.Single("Unexpectedly no unique referential entry in foreign key constraint").GetName()],
                        Table = table,
                        Columns = c.Columns.Select(cc => new CMDirectedColumn(table.Columns[cc.COLUMN_NAME], IndexDirection.Unknown)).ToArray()
                    })
                    .ToDictionary(c => c.Name, c => c);

                table.ColumnTuples = new Dictionary<String, CMColumnTuple>();

                foreach (var p in table.UniqueIndexlikes) table.ColumnTuples[p.Key] = p.Value;
                foreach (var p in table.ForeignKeys) table.ColumnTuples[p.Key] = p.Value;

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

            rootTable = tables[ObjectName.RootName] = new CMTable { Name = ObjectName.RootName, Root = this };

            rootTable.ColumnTuples = new Dictionary<String, CMColumnTuple>();
            rootTable.UniqueIndexlikes = new Dictionary<String, CMIndexlike>();
            rootTable.ForeignKeys = new Dictionary<String, CMForeignKey>();

            var rootKey = rootTable.UniqueIndexlikes[""] = new CMIndexlike() { Name = "", IsPrimary = true, Columns = new CMDirectedColumn[0], Table = rootTable };

            rootTable.ColumnTuples[""] = rootKey;

            foreach (var table in tables.Values)
            {
                table.ForeignKeys.Add("", new CMForeignKey
                {
                    Name = "",
                    Principal = rootKey,
                    Table = table,
                    Columns = rootKey.Columns
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
                t.RelationsForTable =
                    t.Relations.Values.ToLookup(r => r.Table.Name);
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
                    candidateColumns.Where(c => c.Name.Contains("name", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault() ??
                    candidateColumns.FirstOrDefault();

                table.PrimaryNameColumn?.Apply(c => c.IsPrimaryName = true);
            }
        }
    }

    [DebuggerDisplay("{Name}")]
    public class CMTable
    {
        public static readonly CMColumn[] noColumns = new CMColumn[0];

        public CMRoot Root { get; set; }

        public Dictionary<String, CMRelationEnd> Relations { get; } = new Dictionary<String, CMRelationEnd>();

        public ILookup<ObjectName, CMRelationEnd> RelationsForTable { get; set; }

        public ObjectName Name { get; set; }

        public String Abbreviation { get; set; } = "?";

        public Double Hue { get; set; } = 0;

        public CMColumn PrimaryNameColumn { get; set; }

        public CMColumn[] ColumnsInOrder { get; set; } = noColumns;

        public Dictionary<String, CMColumn> Columns = new Dictionary<String, CMColumn>();

        public CMIndexlike PrimaryKey { get; set; }

        public Dictionary<String, CMIndexlike> Indexes { get; set; }
        public Dictionary<String, CMColumnTuple> ColumnTuples { get; set; }
        public Dictionary<String, CMIndexlike> UniqueIndexlikes { get; set; }
        public Dictionary<String, CMForeignKey> ForeignKeys { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public abstract class CMColumnTuple
    {
        public String Name { get; set; }

        public ObjectName ObjectName { get; set; }

        public CMTable Table { get; set; }

        public CMDirectedColumn[] Columns { get; set; }

        public Boolean ContainsKey { get; set; }

        public abstract Boolean IsDomestic { get; }
    }

    public class CMIndexlike : CMColumnTuple
    {
        public override Boolean IsDomestic => true;

        public Boolean IsPrimary { get; set; }

        public Boolean IsUnique { get; set; }

        public Boolean IsSupported => UnsupportedTag == null;

        public String UnsupportedTag { get; set; }
        public String UnsupportedReason { get; set; }
    }

    public class CMForeignKey : CMColumnTuple
    {
        public override Boolean IsDomestic => false;

        public CMIndexlike[] BackingIndexes { get; set; }

        public CMIndexlike Principal { get; set; }


        public IEnumerable<CMIndexlike> GetIndexes() => Table.Name.IsRootName ? Table.Indexes.Values : BackingIndexes;
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

        public CMRelationEnd AmbiguouslyTypedWitness => OtherEnd.Table.RelationsForTable[Table.Name].Where(r => !r.IsMany && r != this).FirstOrDefault();

        public CMColumnTuple Key { get; set; }

        // redudant if we always demand a key
        public CMColumn[] Columns { get; set; }

        public CMRelationEnd OtherEnd { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public class CMColumn
    {
        public Int32 Order { get; set; }

        public String Name { get; set; }

        public String SqlType { get; set; }

        public String Escaped => Name.EscapeNamePart();

        public Boolean IsNullable { get; set; }

        public ColumnType Type { get; set; }

        public Boolean IsString { get; set; }

        public Boolean IsPrimaryName { get; set; }
    }

    public static class CMExtensions
    {
        public static CMForeignKey GetForeignKey(this CMRelationEnd end)
        {
            if (end.Key is CMForeignKey fk)
            {
                return fk;
            }
            else if (end.OtherEnd.Key is CMForeignKey ofk)
            {
                return ofk;
            }
            else
            {
                return null;
            }
        }

        public static IEnumerable<CMIndexlike> StartsWith(this IEnumerable<CMIndexlike> indexes, IEnumerable<String> columns)
        {
            var prefixColumns = columns.OrderBy(c => c).ToArray();

            Boolean HasIndexMatchingPrefix(CMIndexlike index)
            {
                var ixColumns = index.Columns
                    .Take(prefixColumns.Length)
                    .Select(c => c.c.Name)
                    .OrderBy(c => c);

                return ixColumns.SequenceEqual(prefixColumns);
            }

            return indexes.Where(HasIndexMatchingPrefix);
        }
    }
}
