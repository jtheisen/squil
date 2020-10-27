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
        Dictionary<String, CMTable> tables;
        Dictionary<String, CMDomesticKey> keys;

        public CMTable GetTable(String name) => tables[name];

        String GetNameForTable(String isTableName)
            => new String(isTableName);

        public CMRoot(String name)
        {
            this.name = name;
            tables = new Dictionary<String, CMTable>();
            keys = new Dictionary<String, CMDomesticKey>();
        }

        public CMTable RootTable => rootTable;

        public void Populate(ISRoot isRoot)
        {
            tables = isRoot.Tables.Select(t => new CMTable
            {
                Root = this,
                Name = GetNameForTable(t.TABLE_NAME)
            }
            ).ToDictionary(t => t.Name, t => t);

            foreach (var isTable in isRoot.Tables)
            {
                var table = tables[GetNameForTable(isTable.TABLE_NAME)];

                table.ColumnsInOrder = isTable.Columns.Select(c => new CMColumn
                {
                    Name = c.COLUMN_NAME
                }).ToArray();

                table.Columns = table.ColumnsInOrder.ToDictionary(c => c.Name, c => c);

                table.DomesticKeys = isTable.Constraints
                    .Where(c => c.CONSTRAINT_TYPE == "PRIMARY KEY" || c.CONSTRAINT_TYPE == "UNIQUE KEY")
                    .Select(c => new CMDomesticKey
                    {
                        Name = c.CONSTRAINT_NAME,
                        IsPrimary = c.CONSTRAINT_TYPE == "PRIMARY KEY",
                        Table = table,
                        Columns = c.Columns.Select(cc => table.Columns[cc.COLUMN_NAME]).ToArray()
                    })
                    .ToDictionary(c => c.Name, c => c);

                foreach (var key in table.DomesticKeys)
                {
                    keys.Add(key.Key, key.Value);
                }
            }

            foreach (var isTable in isRoot.Tables)
            {
                var table = tables[GetNameForTable(isTable.TABLE_NAME)];

                table.ForeignKeys = isTable.Constraints
                    .Where(c => c.CONSTRAINT_TYPE == "FOREIGN KEY")
                    .Select(c => new CMForeignKey
                    {
                        Name = c.CONSTRAINT_NAME,
                        Principal = keys[c.Referentials.Single("Unexpectedly no unique referential entry in foreign key constraint").UNIQUE_CONSTRAINT_NAME],
                        Table = table,
                        Columns = c.Columns.Select(cc => table.Columns[cc.COLUMN_NAME]).ToArray()
                    })
                    .ToDictionary(c => c.Name, c => c);
            }
        }

        //public void PopulatePseudoIndexesFromKeys()
        //{

        //}

        public void PopulateRoot()
        {
            var relations = tables.Values.Select(table => new Relation
            {
                Principal = new RelationEnd { Name = table.Name, TableName = "" },
                Dependent = new RelationEnd { Name = null, TableName = table.Name },
            }).ToArray();

            rootTable = tables[""] = new CMTable { Name = "" };

            Populate(relations);
        }

        public void Populate(IEnumerable<Relation> relations)
        {
            foreach (var relation in relations)
            {
                var principalTable = GetTable(relation.Principal.TableName);
                var dependentTable = GetTable(relation.Dependent.TableName);

                static CMRelationEnd MakeRelationEnd(RelationEnd end, CMTable table) => new CMRelationEnd
                {
                    Name = end.Name,
                    Table = table,
                    Columns = end.ColumnNames.Select(n => table.ColumnsInOrder
                        .Where(c => c.Name == n)
                        .Single($"Could not resolve column '{n}' in table '{table.Name}'")
                    ).ToArray()
                };

                var principalEnd = MakeRelationEnd(relation.Principal, principalTable);
                var dependentEnd = MakeRelationEnd(relation.Dependent, dependentTable);

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
                    yield return new Relation
                    {
                        Dependent = new RelationEnd { TableName = table.Name, Name = fk.Name, ColumnNames = fk.Columns.Select(c => c.Name).ToArray() },
                        Principal = new RelationEnd { TableName = fk.Principal.Table.Name, Name = fk.Name, ColumnNames = fk.Principal.Columns.Select(c => c.Name).ToArray() }
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

        public String Name { get; set; }

        public CMColumn[] ColumnsInOrder { get; set; } = noColumns;

        public Dictionary<String, CMColumn> Columns = new Dictionary<String, CMColumn>();

        public Dictionary<String, CMDomesticKey> DomesticKeys { get; set; }
        public Dictionary<String, CMForeignKey> ForeignKeys { get; set; }

        //public Dictionary<String, CMIndex> Indexes { get; set; }
    }

    public class ExtentFactory
    {
        HashSet<CMRelationEnd> path = new HashSet<CMRelationEnd>();

        public Extent CreateExtent(CMTable table, Int32 depth = 2)
        {
            var root = table.Root.RootTable;

            var rootRelation = root.Relations[table.Name];

            return CreateExtent(rootRelation, depth);
        }

        public IEnumerable<Extent> CreateExtents(CMTable table, Int32 depth)
        {
            return table.Relations.Values
                .Where(e => !path.Contains(e))
                .Select(e => CreateExtent(e, depth - 1))
                .Where(e => e != null)
                ;
        }

        public Extent CreateExtent(CMRelationEnd end, Int32 depth)
        {
            if (depth < 0) return null;

            path.Add(end);

            try
            {
                return new Extent
                {
                    RelationName = end.OtherEnd.Name,
                    Children = CreateExtents(end.Table, depth - 1).ToArray(),
                    Columns = end.Table.ColumnsInOrder.Select(c => c.Name).ToArray()
                };
            }
            finally
            {
                path.Remove(end);
            }
        }
    }

    [DebuggerDisplay("{Name}")]
    public class CMKey
    {
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

        public CMKey Key { get; set; }

        public CMColumn[] Columns { get; set; }

        public CMRelationEnd OtherEnd { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public class CMColumn
    {
        public String Name { get; set; }
    }
}
