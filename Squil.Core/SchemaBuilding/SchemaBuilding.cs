using Squil.SchemaBuilding;
using System.Data.SqlClient;

namespace Squil;

public static class SchemaBuildingExtensions
{
    static ISRoot GetISSchema(this SqlConnection connection)
    {
        using var _ = GetCurrentLedger().GroupingScope(nameof(GetISSchema));

        var cmRootForInfSch = new CMRoot("INFORMATION_SCHEMA");
        cmRootForInfSch.Populate(InformationSchemaSchema.GetSchema().CreateCsd());
        cmRootForInfSch.PopulateRoot();
        cmRootForInfSch.Populate(InformationSchemaSchema.GetRelations().ToArray());

        var infSchGenerator = new QueryGenerator(cmRootForInfSch, false);

        var cSchema = infSchGenerator.Query<ISRoot>(connection, new Extent
        {
            RelationName = "",
            Children = new[]
            {
                new Extent
                {
                    RelationName = "INFORMATION_SCHEMA.TABLES",
                    Alias = "t",
                    Children = new[]
                    {
                        new Extent
                        {
                            Order = new DirectedColumnName[] { "ORDINAL_POSITION" },
                            RelationName = "columns",
                            Alias = "c"
                        },

                        new Extent
                        {
                            RelationName = "constraints",
                            Alias = "cnstrnt",
                            Children = new[]
                            {
                                new Extent
                                {
                                    Order = new DirectedColumnName[] { "ORDINAL_POSITION" },
                                    RelationName = "columns",
                                    Alias = "cc"
                                },
                                new Extent
                                {
                                    RelationName = "referential",
                                    Alias = "referential"
                                }
                            }
                        }
                    }
                }
            }
        });

        return cSchema;
    }

    static String GetUsedPagesSql(String a) => $@"
select sum(u.used_pages)
from sys.partitions p
join sys.allocation_units u on p.[partition_id] = u.container_id
where p.[object_id] = {a}.[object_id] and p.index_id = {a}.index_id
";

    static String GetCatalogCommentSql(String _) => $@"
select value from sys.extended_properties ep where class = 0 and name = 'MS_Description' and ep.major_id = 0 and ep.minor_id = 0
";

    static String GetSchemaCommentSql(String a) => $@"
select value from sys.extended_properties ep where class = 3 and name = 'MS_Description' and ep.major_id = {a}.schema_id and ep.minor_id = 0
";

    static String GetTableCommentSql(String a) => $@"
select value from sys.extended_properties ep where class = 1 and name = 'MS_Description' and ep.major_id = {a}.[object_id] and ep.minor_id = 0
";

    static String GetColumnCommentSql(String a) => $@"
select value from sys.extended_properties ep where class = 1 and name = 'MS_Description' and ep.major_id = {a}.[object_id] and ep.minor_id = {a}.column_id
";

    static SysRoot GetSysSchema(this SqlConnection connection)
    {
        using var _ = GetCurrentLedger().GroupingScope(nameof(GetSysSchema));

        var cmRootForSys = new CMRoot("sys");
        cmRootForSys.Populate(SystemSchema.GetSchema().CreateCsd());
        cmRootForSys.PopulateRoot();
        cmRootForSys.Populate(SystemSchema.GetRelations().ToArray());

        var sysGenerator = new QueryGenerator(cmRootForSys, false);

        var cSchema = sysGenerator.Query<SysRoot>(connection, new Extent
        {
            RelationName = "",
            SqlSelectables = new[]
            {
                // doesn't yet work
                new SqlSelectable(GetCatalogCommentSql, "comment"),
            },
            Children = new[]
            {
                new Extent
                {
                    RelationName = "sys.schemas",
                    Alias = "s",
                    SqlSelectables = new[]
                    {
                        new SqlSelectable(GetSchemaCommentSql, "comment"),
                    },
                    Children = new[]
                    {
                            new Extent
                            {
                                RelationName = "tables",
                                Alias = "t",
                                SqlSelectables = new[]
                                {
                                    new SqlSelectable(GetTableCommentSql, "comment"),
                                },
                                Children = new[]
                                {
                                    new Extent
                                    {
                                        Order = new DirectedColumnName[] { "column_id" },
                                        RelationName = "columns",
                                        Alias = "c",
                                        SqlSelectables = new[]
                                        {
                                            new SqlSelectable(GetColumnCommentSql, "comment"),
                                        },
                                        Children = new[]
                                        {
                                            new Extent
                                            {
                                                RelationName = "systemtype",
                                                Alias = "tp"
                                            },
                                            new Extent
                                            {
                                                RelationName = "usertype",
                                                Alias = "tp"
                                            }
                                        }
                                    },
                                    new Extent
                                    {
                                        Order = new DirectedColumnName[] { "index_id" },
                                        RelationName = "indexes",
                                        Alias = "ix",
                                        SqlSelectables = new[]
                                        {
                                            new SqlSelectable(GetUsedPagesSql, "used_pages")
                                        },
                                        Children = new[]
                                        {
                                            new Extent
                                            {
                                                Order = new DirectedColumnName[] { "index_column_id" },
                                                RelationName = "columns",
                                                Alias = "ix_c"
                                            }
                                        }
                                    },
                                    new Extent
                                    {
                                        RelationName = "foreign_keys",
                                        Alias = "fk",
                                        Children = new[]
                                        {
                                            new Extent
                                            {
                                                Order = new DirectedColumnName[] { "constraint_column_id" },
                                                RelationName = "columns",
                                                Alias = "fk_c"
                                            },
                                            new Extent
                                            {
                                                RelationName = "referenced_table",
                                                Alias = "o_r",
                                                Children = new[]
                                                {
                                                    new Extent
                                                    {
                                                        RelationName = "schema",
                                                        Alias = "s"
                                                    }
                                                }
                                            },
                                            new Extent
                                            {
                                                RelationName = "referenced_index",
                                                Alias = "ix_r"
                                            },
                                        }
                                    }
                                }
                            }
                    }
                }
            }
        });

        return cSchema;
    }

    public static CMRoot GetCircularModel(this SqlConnection connection)
    {
        using var _ = GetCurrentLedger().GroupingScope(nameof(GetCircularModel));

        //var isSchema = connection.GetISSchema();
        var sysSchema = connection.GetSysSchema();

        // populate from sysschema

        var cmRootForCs = new CMRoot("business model");
        cmRootForCs.Populate(sysSchema.CreateCsd());
        cmRootForCs.PopulateRoot();
        cmRootForCs.PopulateRelationsFromForeignKeys();
        cmRootForCs.Closeup();

        return cmRootForCs;
    }

    public static DateTime GetSchemaModifiedAt(this SqlConnection connection)
    {
        var cmd = connection.CreateSqlCommandFromSql($"select max(modify_date) from sys.objects");

        var result = cmd.ExecuteScalar();

        var modifiedAt = (DateTime)result;

        return modifiedAt;
    }

    public static async Task<DateTime> GetSchemaModifiedAtAsync(this SqlConnection connection)
    {
        var cmd = connection.CreateSqlCommandFromSql($"select max(modify_date) from sys.objects");

        var result = await cmd.ExecuteScalarAsync();

        var modifiedAt = (DateTime)result;

        return modifiedAt;
    }
}
