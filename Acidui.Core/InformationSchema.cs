using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Acidui
{
    [XmlRoot("root")]
    public class ISRoot
    {
        [XmlArray("INFORMATION_SCHEMA_TABLES")]
        public ISTable[] Tables { get; set; }
    }

    [XmlType("t")]
    [DebuggerDisplay("{TABLE_NAME}")]
    public class ISTable
    {
        static ISColumn[] emptyColumns = new ISColumn[0];

        static ISConstraint[] emptyConstraints = new ISConstraint[0];

        [XmlAttribute]
        public String TABLE_NAME { get; set; }

        [XmlArray("columns")]
        public ISColumn[] Columns { get; set; } = emptyColumns;

        [XmlArray("constraints")]
        public ISConstraint[] Constraints { get; set; } = emptyConstraints;
    }

    [XmlType("c")]
    [DebuggerDisplay("{COLUMN_NAME}")]
    public class ISColumn
    {
        [XmlAttribute]
        public String COLUMN_NAME { get; set; }

        [XmlAttribute]
        public Int32 ORDINAL_POSITION { get; set; }

        [XmlAttribute]
        public String IS_NULLABLE { get; set; }

        [XmlAttribute]
        public String DATA_TYPE { get; set; }
    }

    [XmlType("cnstrnt")]
    [DebuggerDisplay("{CONSTRAINT_NAME}")]
    public class ISConstraint
    {
        [XmlAttribute]
        public String CONSTRAINT_NAME { get; set; }

        [XmlAttribute]
        public String CONSTRAINT_TYPE { get; set; }

        [XmlArray("columns")]
        public ISConstraintColumn[] Columns { get; set; }

        [XmlArray("referential")]
        public ISReferentialConstraint[] Referentials { get; set; }
    }

    [XmlType("referential")]
    public class ISReferentialConstraint
    {
        [XmlAttribute]
        public String UNIQUE_CONSTRAINT_NAME { get; set; }
    }

    [XmlType("cc")]
    [DebuggerDisplay("{COLUMN_NAME}")]
    public class ISConstraintColumn
    {
        [XmlAttribute]
        public String COLUMN_NAME { get; set; }
    }

    public static class InformationSchemaSchema
    {
        public static ISRoot GetSchema()
        {
            return new ISRoot
            {
                Tables = new[]
                {
                    MakeISTable("TABLES", GetKeyColumns("TABLE").Concat(new[] { "TABLE_TYPE" /* complete */ })),
                    MakeISTable("COLUMNS", GetKeyColumns("TABLE").Concat(new [] { "COLUMN_NAME", "DATA_TYPE", "ORDINAL_POSITION" })),
                    MakeISTable("TABLE_CONSTRAINTS",
                        GetKeyColumns("CONSTRAINT"),
                        GetKeyColumns("TABLE"),
                        new [] { "CONSTRAINT_TYPE" }),
                    MakeISTable("REFERENTIAL_CONSTRAINTS",
                        GetKeyColumns("CONSTRAINT"),
                        GetKeyColumns("UNIQUE_CONSTRAINT"),
                        new [] { "MATCH_OPTION", "UPDATE_RULE", "DELETE_RULE" }),
                    MakeISTable("KEY_COLUMN_USAGE",
                        GetKeyColumns("TABLE"),
                        GetKeyColumns("CONSTRAINT"),
                        new [] { "COLUMN_NAME", "ORDINAL_POSITION" }),
                }
            };
        }

        public static ISTable MakeISTable(String name, params IEnumerable<String>[] columns)
        {
            return MakeISTable(name, columns.SelectMany(cls => cls).ToArray());
        }

        public static ISTable MakeISTable(String name, IEnumerable<String> columns)
        {
            return new ISTable
            {
                TABLE_NAME = GetTableName(name),
                Columns = columns.Select(n => new ISColumn { COLUMN_NAME = n }).ToArray()
            };
        }

        public static IEnumerable<Relation> GetRelations()
        {
            var tableKeyColumnNames = GetKeyColumns("TABLE").ToArray();
            var constraintKeyColumnNames = GetKeyColumns("CONSTRAINT").ToArray();

            yield return new Relation
            {
                Principal = new RelationEnd { TableName = GetTableName("TABLES"), Name = "columns", ColumnNames = tableKeyColumnNames },
                Dependent = new RelationEnd { TableName = GetTableName("COLUMNS"), Name = "table", ColumnNames = tableKeyColumnNames }
            };

            yield return new Relation
            {
                Principal = new RelationEnd { TableName = GetTableName("TABLES"), Name = "constraints", ColumnNames = tableKeyColumnNames },
                Dependent = new RelationEnd { TableName = GetTableName("TABLE_CONSTRAINTS"), Name = "table", ColumnNames = tableKeyColumnNames }
            };

            yield return new Relation
            {
                Principal = new RelationEnd { TableName = GetTableName("TABLE_CONSTRAINTS"), Name = "columns", ColumnNames = constraintKeyColumnNames },
                Dependent = new RelationEnd { TableName = GetTableName("KEY_COLUMN_USAGE"), Name = "constraint", ColumnNames = constraintKeyColumnNames }
            };

            yield return new Relation
            {
                Principal = new RelationEnd { TableName = GetTableName("TABLE_CONSTRAINTS"), Name = "referential", ColumnNames = constraintKeyColumnNames },
                Dependent = new RelationEnd { TableName = GetTableName("REFERENTIAL_CONSTRAINTS"), Name = "constraint", ColumnNames = constraintKeyColumnNames }
            };

        }

        private static IEnumerable<String> GetKeyColumns(String type) => BaseKeyNames.Select(n => $"{type}_{n}");

        static String GetTableName(String name)
            => $"INFORMATION_SCHEMA.{name}";

        static String[] BaseKeyNames = new[] { "CATALOG", "SCHEMA", "NAME" };
    }
}
