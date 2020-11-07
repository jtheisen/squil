using Acidui.Core;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Acidui
{
    [DebuggerDisplay("{name}")]
    public class CMRoot
    {
        String name;
        CMTable rootTable;
        Dictionary<ObjectName, CMTable> tables;
        Dictionary<ObjectName, CMDomesticKey> keys;

        public CMTable GetTable(ObjectName name) => tables[name];



        public CMRoot(String name)
        {
            this.name = name;
            tables = new Dictionary<ObjectName, CMTable>();
            keys = new Dictionary<ObjectName, CMDomesticKey>();
        }

        public CMTable RootTable => rootTable;

        public void Populate(ISRoot isRoot)
        {
            tables = isRoot.Tables.Select(t => new CMTable
            {
                Root = this,
                Name = t.GetName(),
            }
            ).ToDictionary(t => t.Name, t => t);

            foreach (var a in new Abbreviator().Calculate(tables.Values.Select(t => t.Name).ToArray()))
            {
                tables[a.Key].Abbreviation = a.Value;
            }

            foreach (var isTable in isRoot.Tables)
            {
                var table = tables[isTable.GetName()];

                table.ColumnsInOrder = isTable.Columns.Select((c, i) => new CMColumn
                {
                    Order = i,
                    Name = c.COLUMN_NAME
                }).ToArray();

                table.Columns = table.ColumnsInOrder.ToDictionary(c => c.Name, c => c);

                table.PrimaryNameColumn =
                    table.ColumnsInOrder.Where(c => c.Name.Equals("name", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault() ??
                    table.ColumnsInOrder.Where(c => c.Name.Contains("name", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault() ?? null;

                table.PrimaryNameColumn?.Apply(c => c.IsPrimaryName = true);

                table.DomesticKeys = isTable.Constraints
                    .Where(c => c.CONSTRAINT_TYPE == "PRIMARY KEY" || c.CONSTRAINT_TYPE == "UNIQUE KEY")
                    .Select(c => new CMDomesticKey
                    {
                        ObjectName = c.GetName(),
                        Name = c.Name,
                        IsPrimary = c.CONSTRAINT_TYPE == "PRIMARY KEY",
                        Table = table,
                        Columns = c.Columns.Select(cc => table.Columns[cc.COLUMN_NAME]).ToArray()
                    })
                    .ToDictionary(c => c.Name, c => c);

                foreach (var key in table.DomesticKeys)
                {
                    if (key.Value.IsPrimary) table.PrimaryKey = key.Value;

                    keys.Add(key.Value.ObjectName, key.Value);
                }
            }

            foreach (var isTable in isRoot.Tables)
            {
                var table = tables[isTable.GetName()];

                table.ForeignKeys = isTable.Constraints
                    .Where(c => c.CONSTRAINT_TYPE == "FOREIGN KEY")
                    .Select(c => new CMForeignKey
                    {
                        Name = c.Name,
                        Principal = keys[c.Referentials.Single("Unexpectedly no unique referential entry in foreign key constraint").GetName()],
                        Table = table,
                        Columns = c.Columns.Select(cc => table.Columns[cc.COLUMN_NAME]).ToArray()
                    })
                    .ToDictionary(c => c.Name, c => c);

                table.Keys = new Dictionary<String, CMKey>();

                foreach (var p in table.DomesticKeys) table.Keys[p.Key] = p.Value;
                foreach (var p in table.ForeignKeys) table.Keys[p.Key] = p.Value;
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

            rootTable.DomesticKeys = new Dictionary<String, CMDomesticKey>();
            rootTable.ForeignKeys = new Dictionary<String, CMForeignKey>();
            rootTable.Keys = new Dictionary<String, CMKey>();

            var rootKey = rootTable.DomesticKeys[""] = new CMDomesticKey() { Name = "", IsPrimary = true, Columns = new CMColumn[0], Table = rootTable };

            rootTable.Keys[""] = rootKey;

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

                static CMRelationEnd MakeRelationEnd(Boolean isMany, RelationEnd end, CMTable table, CMKey key) => new CMRelationEnd
                {
                    Name = end.Name,
                    Table = table,
                    IsMany = isMany,
                    Key = key,
                    Columns = end.ColumnNames.Select(n => table.ColumnsInOrder
                        .Where(c => c.Name == n)
                        .Single($"Could not resolve column '{n}' in table '{table.Name.LastPart}'")
                    ).ToArray()
                };

                var principalEnd = MakeRelationEnd(false, relation.Principal, principalTable, relation.Principal.KeyName?.Apply(n => principalTable.DomesticKeys[n]));
                var dependentEnd = MakeRelationEnd(true, relation.Dependent, dependentTable, relation.Dependent.KeyName?.Apply(n => dependentTable.ForeignKeys[n]));

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
                        Dependent = new RelationEnd { TableName = table.Name, Name = fk.Name, KeyName = fk.Name, ColumnNames = fk.Columns.Select(c => c.Name).ToArray() },
                        Principal = new RelationEnd { TableName = fk.Principal.Table.Name, Name = fk.Name, KeyName = fk.Principal.Name, ColumnNames = fk.Principal.Columns.Select(c => c.Name).ToArray() }
                    };
                }
            }
        }
    }

    [DebuggerDisplay("{Name}")]
    public class CMTable
    {
        public static readonly CMColumn[] noColumns = new CMColumn[0];

        public CMRoot Root { get; set; }

        public Dictionary<String, CMRelationEnd> Relations { get; } = new Dictionary<String, CMRelationEnd>();

        public ObjectName Name { get; set; }

        public String Abbreviation { get; set; } = "◯";

        public CMColumn PrimaryNameColumn { get; set; }

        public CMColumn[] ColumnsInOrder { get; set; } = noColumns;

        public Dictionary<String, CMColumn> Columns = new Dictionary<String, CMColumn>();

        public CMDomesticKey PrimaryKey { get; set; }

        public Dictionary<String, CMKey> Keys { get; set; }
        public Dictionary<String, CMDomesticKey> DomesticKeys { get; set; }
        public Dictionary<String, CMForeignKey> ForeignKeys { get; set; }

        //public Dictionary<String, CMIndex> Indexes { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public class CMKey
    {
        public ObjectName ObjectName { get; set; }

        // The name is not unique accross schemas, but it is within a table (and a schema).
        public String Name { get; set; }

        public CMTable Table { get; set; }

        public CMColumn[] Columns { get; set; }
    }

    public class CMDomesticKey : CMKey
    {
        public Boolean IsPrimary { get; set; }
    }

    public class CMForeignKey : CMKey
    {
        public CMDomesticKey Principal { get; set; }
    }

    //[DebuggerDisplay("{Name}")]
    //public class CMIndex
    //{
    //    public String Name { get; set; }

    //    public Boolean IsReal { get; set; }

    //    public CMTable Table { get; set; }

    //    public CMColumn[] Columns { get; set; }
    //}

    [DebuggerDisplay("{OtherEnd.Name}->{Table.Name}")]
    public class CMRelationEnd
    {
        public String Name { get; set; }

        public CMTable Table { get; set; }

        public Boolean IsMany { get; set; }

        public CMKey Key { get; set; }

        // redudant if we always demand a key
        public CMColumn[] Columns { get; set; }

        public CMRelationEnd OtherEnd { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public class CMColumn
    {
        public Int32 Order { get; set; }

        public String Name { get; set; }

        public Boolean IsPrimaryName { get; set; }
    }
}
