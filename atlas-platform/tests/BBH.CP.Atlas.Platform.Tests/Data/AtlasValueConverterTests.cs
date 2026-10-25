using System.Globalization;
using BBH.CP.Atlas.Data.Platform;

namespace BBH.CP.Atlas.Platform.Tests.Data;

public class AtlasValueConverterTests
{
    private static AtlasColumn Column(string type, bool nullable = true, int? maxLength = null, byte precision = 0, byte scale = 0,
        bool identity = false) =>
        new("Field", type, nullable, maxLength, precision, scale, identity, IsComputed: false);

    [Theory]
    [InlineData("90", 90)]
    [InlineData("0", 0)]
    [InlineData("-5", -5)]
    [InlineData(" 1440 ", 1440)]
    public void Int_accepts_whole_numbers(string wire, int expected)
    {
        AtlasConversionResult result = AtlasValueConverter.FromWire(wire, Column("int"));

        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1:30")]
    [InlineData("1,000")]
    [InlineData("abc")]
    public void Int_refuses_other_text(string wire)
    {
        AtlasConversionResult result = AtlasValueConverter.FromWire(wire, Column("int"));

        Assert.False(result.Success);
        Assert.Equal(AtlasValueConverter.InvalidNumber, result.ErrorCode);
    }

    [Fact]
    public void Smallint_checks_the_range()
    {
        Assert.Equal((short)2026, AtlasValueConverter.FromWire("2026", Column("smallint")).Value);
        Assert.Equal(AtlasValueConverter.OutOfRange, AtlasValueConverter.FromWire("40000", Column("smallint")).ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_is_null_for_a_nullable_column(string? wire)
    {
        AtlasConversionResult result = AtlasValueConverter.FromWire(wire, Column("int"));

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Empty_is_refused_for_a_not_null_column()
    {
        Assert.Equal(AtlasValueConverter.Required, AtlasValueConverter.FromWire("", Column("int", nullable: false)).ErrorCode);
    }

    [Fact]
    public void Database_generated_columns_are_not_writable()
    {
        Assert.Equal(AtlasValueConverter.NotWritable, AtlasValueConverter.FromWire("5", Column("int", identity: true)).ErrorCode);
        Assert.Equal(AtlasValueConverter.NotWritable, AtlasValueConverter.FromWire("AAAA", Column("timestamp")).ErrorCode);
    }

    [Theory]
    [InlineData("1250000.50", "1250000.50")]
    [InlineData("-3.1", "-3.1")]
    [InlineData("7", "7")]
    public void Decimal_uses_the_invariant_format(string wire, string expected)
    {
        AtlasConversionResult result = AtlasValueConverter.FromWire(wire, Column("decimal", precision: 19, scale: 2));

        Assert.True(result.Success);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), result.Value);
    }

    [Theory]
    [InlineData("1,250,000.50", AtlasValueConverter.InvalidNumber)]
    [InlineData("1250000,50", AtlasValueConverter.InvalidNumber)]
    [InlineData("1.234", AtlasValueConverter.InvalidNumber)]
    [InlineData("100000000000000000.00", AtlasValueConverter.OutOfRange)]
    public void Decimal_refuses_group_separators_extra_decimals_and_overflow(string wire, string code)
    {
        Assert.Equal(code, AtlasValueConverter.FromWire(wire, Column("decimal", precision: 19, scale: 2)).ErrorCode);
    }

    [Fact]
    public void The_server_culture_has_no_effect()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(1.5m, AtlasValueConverter.FromWire("1.5", Column("decimal", precision: 9, scale: 2)).Value);
            Assert.Equal("1.5", AtlasValueConverter.ToWire(1.5m, Column("decimal", precision: 9, scale: 2)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Date_accepts_only_iso_dates()
    {
        Assert.Equal(new DateTime(2026, 10, 25), AtlasValueConverter.FromWire("2026-10-25", Column("date")).Value);
        Assert.Equal(AtlasValueConverter.InvalidDate, AtlasValueConverter.FromWire("10/25/2026", Column("date")).ErrorCode);
        Assert.Equal(AtlasValueConverter.InvalidDate, AtlasValueConverter.FromWire("2026-02-30", Column("date")).ErrorCode);
    }

    [Theory]
    [InlineData("2026-10-25T14:30:00Z", 14)]
    [InlineData("2026-10-25T10:30:00-04:00", 14)]
    [InlineData("2026-10-25T14:30:00", 14)]
    public void Datetime_is_stored_as_utc(string wire, int expectedUtcHour)
    {
        AtlasConversionResult result = AtlasValueConverter.FromWire(wire, Column("datetime"));

        Assert.True(result.Success);
        DateTime value = Assert.IsType<DateTime>(result.Value);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(expectedUtcHour, value.Hour);
    }

    [Fact]
    public void Datetime_refuses_dates_before_1753()
    {
        Assert.Equal(AtlasValueConverter.OutOfRange, AtlasValueConverter.FromWire("1700-01-01", Column("datetime")).ErrorCode);
        Assert.True(AtlasValueConverter.FromWire("1700-01-01", Column("datetime2")).Success);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("Yes", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("No", false)]
    [InlineData("0", false)]
    public void Bit_accepts_true_false_yes_no(string wire, bool expected)
    {
        Assert.Equal(expected, AtlasValueConverter.FromWire(wire, Column("bit")).Value);
    }

    [Fact]
    public void Bit_refuses_other_text()
    {
        Assert.Equal(AtlasValueConverter.InvalidBoolean, AtlasValueConverter.FromWire("maybe", Column("bit")).ErrorCode);
    }

    [Fact]
    public void Text_checks_the_column_length_and_keeps_spaces()
    {
        Assert.Equal(" Smith Trust ", AtlasValueConverter.FromWire(" Smith Trust ", Column("varchar", maxLength: 20)).Value);
        Assert.Equal(AtlasValueConverter.TooLong, AtlasValueConverter.FromWire(new string('x', 21), Column("varchar", maxLength: 20)).ErrorCode);
        Assert.True(AtlasValueConverter.FromWire(new string('x', 9000), Column("varchar", maxLength: -1)).Success);
    }

    [Fact]
    public void Unsupported_types_give_a_clear_error()
    {
        Assert.Equal(AtlasValueConverter.UnsupportedType, AtlasValueConverter.FromWire("x", Column("xml")).ErrorCode);
    }

    [Fact]
    public void ToWire_uses_one_format_for_each_type()
    {
        Assert.Equal("2026-10-25", AtlasValueConverter.ToWire(new DateTime(2026, 10, 25), Column("date")));
        Assert.Equal("2026-10-25T14:30:00.000Z", AtlasValueConverter.ToWire(new DateTime(2026, 10, 25, 14, 30, 0), Column("datetime")));
        Assert.Equal("true", AtlasValueConverter.ToWire(true, Column("bit")));
        Assert.Equal("90", AtlasValueConverter.ToWire(90, Column("int")));
        Assert.Equal("AQI=", AtlasValueConverter.ToWire(new byte[] { 1, 2 }, Column("timestamp")));
        Assert.Null(AtlasValueConverter.ToWire(null, Column("int")));
    }
}
