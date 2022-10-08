using Squil.SchemaBuilding;
using System.Xml.Serialization;

namespace Squil.Tests;

[TestClass]
public class Playground
{
    [TestMethod]
    public void Tests()
    {
        var serializer = new XmlSerializer(typeof(SysRoot));

        var reader = new StringReader(xml);

        var result = serializer.Deserialize(reader);
    }

    String xml = @"<root>
    <sys.schemas>
        <s name=""dbo"" schema_id=""1"">
            <tables>
                <t object_id=""693577509"" schema_id=""1"" name=""principal"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-08-21T06:20:21.960"">
                    <columns>
                        <c object_id=""693577509"" column_id=""1"" name=""Id"" is_nullable=""0"" />
                        <c object_id=""693577509"" column_id=""2"" name=""name"" is_nullable=""1"" />
                    </columns>
                    <indexes>
                        <ix object_id=""693577509"" index_id=""0"" type=""0"" type_desc=""HEAP"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"" />
                        <ix name=""IX_Id"" object_id=""693577509"" index_id=""2"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""1"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""693577509"" index_id=""2"" index_column_id=""1"" column_id=""1"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                        <ix name=""IX_Name2"" object_id=""693577509"" index_id=""3"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""1"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""693577509"" index_id=""3"" index_column_id=""1"" column_id=""2"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                        <ix name=""IX_Name1"" object_id=""693577509"" index_id=""4"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""1"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""693577509"" index_id=""4"" index_column_id=""1"" column_id=""2"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
                <t object_id=""821577965"" schema_id=""1"" name=""dep"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-08-21T06:20:21.960"">
                    <columns>
                        <c object_id=""821577965"" column_id=""1"" name=""Id"" is_nullable=""0"" />
                        <c object_id=""821577965"" column_id=""2"" name=""PrincipalId"" is_nullable=""1"" />
                        <c object_id=""821577965"" column_id=""3"" name=""name"" is_nullable=""1"" />
                    </columns>
                    <indexes>
                        <ix object_id=""821577965"" index_id=""0"" type=""0"" type_desc=""HEAP"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"" />
                    </indexes>
                    <foreign_keys>
                        <fk object_id=""853578079"" schema_id=""1"" name=""FK_Principal"" type=""F "" type_desc=""FOREIGN_KEY_CONSTRAINT"" modify_date=""2022-08-20T01:55:11.473"" parent_object_id=""821577965"" referenced_object_id=""693577509"" key_index_id=""2"" is_disabled=""0"" is_system_named=""0"">
                            <columns>
                                <fk_c constraint_object_id=""853578079"" constraint_column_id=""1"" parent_object_id=""821577965"" parent_column_id=""2"" referenced_object_id=""693577509"" referenced_column_id=""1"" />
                            </columns>
                            <referenced_table>
                                <o_r object_id=""693577509"" schema_id=""1"" name=""principal"">
                                    <schema>
                                        <s name=""dbo"" schema_id=""1"" />
                                    </schema>
                                </o_r>
                            </referenced_table>
                            <referenced_index>
                                <ix_r name=""IX_Id"" object_id=""693577509"" index_id=""2"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""1"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"" />
                            </referenced_index>
                        </fk>
                        <fk object_id=""901578250"" schema_id=""1"" name=""FK_Name"" type=""F "" type_desc=""FOREIGN_KEY_CONSTRAINT"" modify_date=""2022-08-21T06:20:21.960"" parent_object_id=""821577965"" referenced_object_id=""693577509"" key_index_id=""3"" is_disabled=""0"" is_system_named=""0"">
                            <columns>
                                <fk_c constraint_object_id=""901578250"" constraint_column_id=""1"" parent_object_id=""821577965"" parent_column_id=""3"" referenced_object_id=""693577509"" referenced_column_id=""2"" />
                            </columns>
                            <referenced_table>
                                <o_r object_id=""693577509"" schema_id=""1"" name=""principal"">
                                    <schema>
                                        <s name=""dbo"" schema_id=""1"" />
                                    </schema>
                                </o_r>
                            </referenced_table>
                            <referenced_index>
                                <ix_r name=""IX_Name2"" object_id=""693577509"" index_id=""3"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""1"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"" />
                            </referenced_index>
                        </fk>
                    </foreign_keys>
                </t>
                <t object_id=""869578136"" schema_id=""1"" name=""namefunk"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-08-21T03:09:43.620"">
                    <columns>
                        <c object_id=""869578136"" column_id=""1"" name=""id"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix name=""id"" object_id=""869578136"" index_id=""1"" type=""1"" type_desc=""CLUSTERED"" is_disabled=""0"" is_unique=""1"" is_primary_key=""1"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""869578136"" index_id=""1"" index_column_id=""1"" column_id=""1"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
                <t object_id=""1013578649"" schema_id=""1"" name=""numbers_table"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-08-22T19:19:40.920"">
                    <columns>
                        <c object_id=""1013578649"" column_id=""1"" name=""number"" is_nullable=""0"" />
                        <c object_id=""1013578649"" column_id=""2"" name=""point"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix name=""IX_Numbers"" object_id=""1013578649"" index_id=""1"" type=""1"" type_desc=""CLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1013578649"" index_id=""1"" index_column_id=""1"" column_id=""1"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
                <t object_id=""1093578934"" schema_id=""1"" name=""InterestingIndexes"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-08-24T00:04:02.240"">
                    <columns>
                        <c object_id=""1093578934"" column_id=""1"" name=""Id"" is_nullable=""0"" />
                        <c object_id=""1093578934"" column_id=""2"" name=""Name"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix object_id=""1093578934"" index_id=""0"" type=""0"" type_desc=""HEAP"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"" />
                        <ix name=""IX_InterestingIndexes_Name_2"" object_id=""1093578934"" index_id=""2"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1093578934"" index_id=""2"" index_column_id=""1"" column_id=""2"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                        <ix name=""IX_InterestingIndexes_Name_1"" object_id=""1093578934"" index_id=""3"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1093578934"" index_id=""3"" index_column_id=""1"" column_id=""2"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
                <t object_id=""1205579333"" schema_id=""1"" name=""ColumnsTypes"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-08-28T00:52:03.880"">
                    <columns>
                        <c object_id=""1205579333"" column_id=""1"" name=""Guid"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""2"" name=""String"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""3"" name=""Float"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""4"" name=""Int"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""5"" name=""TinyInt"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""6"" name=""Date"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""7"" name=""Time"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""8"" name=""DateTime"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""9"" name=""DateTime2"" is_nullable=""0"" />
                        <c object_id=""1205579333"" column_id=""10"" name=""DateTimeOffset"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix object_id=""1205579333"" index_id=""0"" type=""0"" type_desc=""HEAP"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"" />
                    </indexes>
                </t>
                <t object_id=""1253579504"" schema_id=""1"" name=""People"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-08-28T22:15:46.940"">
                    <columns>
                        <c object_id=""1253579504"" column_id=""1"" name=""Id"" is_nullable=""0"" />
                        <c object_id=""1253579504"" column_id=""2"" name=""Name"" is_nullable=""0"" />
                        <c object_id=""1253579504"" column_id=""3"" name=""rv"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix name=""PK_People_Id"" object_id=""1253579504"" index_id=""1"" type=""1"" type_desc=""CLUSTERED"" is_disabled=""0"" is_unique=""1"" is_primary_key=""1"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1253579504"" index_id=""1"" index_column_id=""1"" column_id=""1"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                        <ix name=""IX_People_Name_1"" object_id=""1253579504"" index_id=""2"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1253579504"" index_id=""2"" index_column_id=""1"" column_id=""2"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
                <t object_id=""1333579789"" schema_id=""1"" name=""BigNumbers"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-08-30T16:55:44.007"">
                    <columns>
                        <c object_id=""1333579789"" column_id=""1"" name=""col1"" is_nullable=""0"" />
                        <c object_id=""1333579789"" column_id=""2"" name=""col2"" is_nullable=""0"" />
                        <c object_id=""1333579789"" column_id=""3"" name=""col3"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix name=""IX_BigNumbers"" object_id=""1333579789"" index_id=""1"" type=""1"" type_desc=""CLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1333579789"" index_id=""1"" index_column_id=""1"" column_id=""1"" is_descending_key=""0"" is_included_column=""0"" />
                                <ix_c object_id=""1333579789"" index_id=""1"" index_column_id=""2"" column_id=""2"" is_descending_key=""0"" is_included_column=""0"" />
                                <ix_c object_id=""1333579789"" index_id=""1"" index_column_id=""3"" column_id=""3"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
                <t object_id=""1365579903"" schema_id=""1"" name=""DateTimeTests"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-09-03T22:18:36.300"">
                    <columns>
                        <c object_id=""1365579903"" column_id=""1"" name=""time"" is_nullable=""0"" />
                        <c object_id=""1365579903"" column_id=""2"" name=""date"" is_nullable=""0"" />
                        <c object_id=""1365579903"" column_id=""3"" name=""datetimeoffset"" is_nullable=""0"" />
                        <c object_id=""1365579903"" column_id=""4"" name=""datetime"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix name=""IX_DateTimeTests_1"" object_id=""1365579903"" index_id=""1"" type=""1"" type_desc=""CLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1365579903"" index_id=""1"" index_column_id=""1"" column_id=""3"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                        <ix name=""IX_DateTimeTests_4"" object_id=""1365579903"" index_id=""2"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1365579903"" index_id=""2"" index_column_id=""1"" column_id=""4"" is_descending_key=""1"" is_included_column=""0"" />
                            </columns>
                        </ix>
                        <ix name=""IX_DateTimeTests_3"" object_id=""1365579903"" index_id=""3"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1365579903"" index_id=""3"" index_column_id=""1"" column_id=""2"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                        <ix name=""IX_DateTimeTests_2"" object_id=""1365579903"" index_id=""4"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1365579903"" index_id=""4"" index_column_id=""1"" column_id=""1"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
                <t object_id=""1381579960"" schema_id=""1"" name=""BigStrings"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-09-04T01:35:41.707"">
                    <columns>
                        <c object_id=""1381579960"" column_id=""1"" name=""v"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix name=""IX_BigStrings"" object_id=""1381579960"" index_id=""1"" type=""1"" type_desc=""CLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1381579960"" index_id=""1"" index_column_id=""1"" column_id=""1"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
                <t object_id=""1845581613"" schema_id=""1"" name=""CollationTests"" type=""U "" type_desc=""USER_TABLE"" modify_date=""2022-09-06T19:04:21.853"">
                    <columns>
                        <c object_id=""1845581613"" column_id=""1"" name=""i"" is_nullable=""0"" />
                        <c object_id=""1845581613"" column_id=""2"" name=""v"" is_nullable=""0"" />
                    </columns>
                    <indexes>
                        <ix name=""PK_CollationTests"" object_id=""1845581613"" index_id=""1"" type=""1"" type_desc=""CLUSTERED"" is_disabled=""0"" is_unique=""1"" is_primary_key=""1"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1845581613"" index_id=""1"" index_column_id=""1"" column_id=""1"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                        <ix name=""IX_CollationTests_v"" object_id=""1845581613"" index_id=""2"" type=""2"" type_desc=""NONCLUSTERED"" is_disabled=""0"" is_unique=""0"" is_primary_key=""0"" is_unique_constraint=""0"" has_filter=""0"" is_hypothetical=""0"">
                            <columns>
                                <ix_c object_id=""1845581613"" index_id=""2"" index_column_id=""1"" column_id=""2"" is_descending_key=""0"" is_included_column=""0"" />
                            </columns>
                        </ix>
                    </indexes>
                </t>
            </tables>
        </s>
        <s name=""guest"" schema_id=""2"" />
        <s name=""INFORMATION_SCHEMA"" schema_id=""3"" />
        <s name=""sys"" schema_id=""4"" />
        <s name=""jens"" schema_id=""5"" />
        <s name=""db_owner"" schema_id=""16384"" />
        <s name=""db_accessadmin"" schema_id=""16385"" />
        <s name=""db_securityadmin"" schema_id=""16386"" />
        <s name=""db_ddladmin"" schema_id=""16387"" />
        <s name=""db_backupoperator"" schema_id=""16389"" />
        <s name=""db_datareader"" schema_id=""16390"" />
        <s name=""db_datawriter"" schema_id=""16391"" />
        <s name=""db_denydatareader"" schema_id=""16392"" />
        <s name=""db_denydatawriter"" schema_id=""16393"" />
    </sys.schemas>
</root>";
}
