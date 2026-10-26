using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform.Dashboard;

namespace BBH.CP.Atlas.Platform.Tests.Dashboard;

/// <summary>Spec 03, 4.7: names only from the metadata, values only as parameters.</summary>
public class TabQueryBuilderTests
{
    private static readonly AtlasTable Table = new("TTDV2", "General",
    [
        new AtlasColumn("ID", "int", false, null, 10, 0, true, false),
        new AtlasColumn("AccountNumber", "varchar", true, 100, 0, 0, false, false),
        new AtlasColumn("TaxYear", "smallint", true, null, 5, 0, false, false),
        new AtlasColumn("CreatedBy", "nvarchar", true, 50, 0, 0, false, false),
        new AtlasColumn("TaxPreparerLanId", "nvarchar", true, 50, 0, 0, false, false),
        new AtlasColumn("ModifiedDate", "datetime", true, null, 23, 3, false, false),
        new AtlasColumn("IsDeleted", "bit", true, null, 1, 0, false, false)
    ]);

    [Fact]
    public void A_tab_with_an_Any_group_gives_OR_inside_AND_with_parameters_only()
    {
        CPAtlasTab tab = new()
        {
            ID = "Mine",
            Columns = [new() { Name = "AccountNumber" }, new() { Name = "TaxYear" }],
            Filter =
            [
                new() { Any = [new() { Column = "CreatedBy", Op = "Eq", Value = "{LanId}" }, new() { Column = "TaxPreparerLanId", Value = "{LanId}" }] },
                new() { Column = "TaxYear", Op = "In", Values = ["2025", "2026"] }
            ],
            OrderBy = ["ModifiedDate DESC"]
        };

        AtlasTabQuery query = AtlasTabQueryBuilder.Build(Table, tab, x => x == "{LanId}" ? "jdoe" : x);

        Assert.Equal(
            "SELECT [ID], [AccountNumber], [TaxYear] FROM [TTDV2].[General] WHERE ISNULL([IsDeleted], 0) = 0 " +
            "AND ([CreatedBy] = @p0 OR [TaxPreparerLanId] = @p1) AND [TaxYear] IN (@p2, @p3) ORDER BY [ModifiedDate] DESC;",
            query.Sql);
        Assert.Equal(["jdoe", "jdoe", (short)2025, (short)2026], query.Parameters.Select(x => x.Value));
        Assert.Equal(System.Data.SqlDbType.NVarChar, query.Parameters[0].SqlDbType);
    }

    [Fact]
    public void A_NOT_NULL_IsDeleted_column_gives_a_condition_that_matches_the_filtered_index()
    {
        // Review DA-08: "[IsDeleted] = 0" implies the index filter; ISNULL(...) is kept for a nullable column.
        AtlasTable notNull = new("APP", "General",
        [
            new AtlasColumn("ID", "int", false, null, 10, 0, true, false),
            new AtlasColumn("CreatedBy", "nvarchar", true, 50, 0, 0, false, false),
            new AtlasColumn("IsDeleted", "bit", false, null, 1, 0, false, false, HasDefault: true)
        ]);
        CPAtlasTab tab = new() { ID = "Mine", Columns = [new() { Name = "CreatedBy" }] };

        Assert.Contains("WHERE [IsDeleted] = 0", AtlasTabQueryBuilder.Build(notNull, tab, x => x).Sql, StringComparison.Ordinal);
        Assert.Contains("WHERE ISNULL([IsDeleted], 0) = 0", AtlasTabQueryBuilder.Build(Table, tab, x => x).Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_with_SQL_text_stays_a_parameter()
    {
        CPAtlasTab tab = new()
        {
            ID = "T",
            Columns = [new() { Name = "AccountNumber" }],
            Filter = [new() { Column = "AccountNumber", Op = "Eq", Value = "x'; DROP TABLE TTDV2.General; --" }]
        };

        AtlasTabQuery query = AtlasTabQueryBuilder.Build(Table, tab, x => x);

        Assert.DoesNotContain("DROP", query.Sql, StringComparison.Ordinal);
        Assert.Equal("x'; DROP TABLE TTDV2.General; --", Assert.Single(query.Parameters).Value);
        Assert.EndsWith("ORDER BY [ID] DESC;", query.Sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Columns", "Password")]
    [InlineData("Filter", "Nope")]
    [InlineData("OrderBy", "ID; DROP TABLE x")]
    [InlineData("OrderBy", "ID SIDEWAYS")]
    [InlineData("Op", "Like")]
    public void A_name_or_operator_that_is_not_known_is_refused(string part, string value)
    {
        CPAtlasTab tab = new() { ID = "T", Columns = [new() { Name = "AccountNumber" }] };
        switch (part)
        {
            case "Columns": tab.Columns.Add(new CPTabColumn { Name = value }); break;
            case "Filter": tab.Filter = [new() { Column = value, Value = "1" }]; break;
            case "OrderBy": tab.OrderBy = [value]; break;
            case "Op": tab.Filter = [new() { Column = "AccountNumber", Op = value, Value = "1" }]; break;
        }

        Assert.Throws<InvalidOperationException>(() => AtlasTabQueryBuilder.Build(Table, tab, x => x));
    }

    [Fact]
    public void A_nested_Any_group_is_refused()
    {
        CPAtlasTab tab = new()
        {
            ID = "T",
            Columns = [new() { Name = "AccountNumber" }],
            Filter = [new() { Any = [new() { Any = [new() { Column = "CreatedBy", Value = "a" }] }] }]
        };

        Assert.Throws<InvalidOperationException>(() => AtlasTabQueryBuilder.Build(Table, tab, x => x));
    }

    [Fact]
    public void A_bad_filter_value_for_the_column_type_is_refused()
    {
        CPAtlasTab tab = new() { ID = "T", Columns = [new() { Name = "TaxYear" }], Filter = [new() { Column = "TaxYear", Value = "abc" }] };
        Assert.Throws<InvalidOperationException>(() => AtlasTabQueryBuilder.Build(Table, tab, x => x));
    }
}
