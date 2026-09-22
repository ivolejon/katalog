using Katalog.Api.Domain;
using Katalog.Api.Features.Labels;
using Katalog.Api.Features.Releases.Polling;

namespace Katalog.Api.Tests.Unit;

public sealed class LabelSlugTests
{
    [Theory]
    [InlineData("Ninja Tune", "ninja-tune")]
    [InlineData("Rune Grammofon", "rune-grammofon")]
    [InlineData("Åsa & Co", "åsa-co")]
    [InlineData("  Hyperdub  ", "hyperdub")]
    [InlineData("!!!", "")]
    [InlineData("Boys Noize Records", "boys-noize-records")]
    public void From_ProducesExpectedSlug(string name, string expected)
    {
        Assert.Equal(expected, LabelSlug.From(name));
    }
}

public sealed class ReleasePollerParseTests
{
    [Fact]
    public void ParseAlbumType_MapsKnownValues()
    {
        Assert.Equal(AlbumType.Album, ReleasePoller.ParseAlbumType("album"));
        Assert.Equal(AlbumType.Single, ReleasePoller.ParseAlbumType("single"));
        Assert.Equal(AlbumType.Compilation, ReleasePoller.ParseAlbumType("compilation"));
        Assert.Equal(AlbumType.Album, ReleasePoller.ParseAlbumType("unknown"));
    }

    [Fact]
    public void ParseReleaseDate_DayPrecision_ParsesFullDate()
    {
        var (date, precision) = ReleasePoller.ParseReleaseDate("2024-05-17", "day");

        Assert.Equal(new DateOnly(2024, 5, 17), date);
        Assert.Equal(ReleaseDatePrecision.Day, precision);
    }

    [Fact]
    public void ParseReleaseDate_MonthPrecision_NormalizesToFirstOfMonth()
    {
        var (date, precision) = ReleasePoller.ParseReleaseDate("2024-05", "month");

        Assert.Equal(new DateOnly(2024, 5, 1), date);
        Assert.Equal(ReleaseDatePrecision.Month, precision);
    }

    [Fact]
    public void ParseReleaseDate_YearPrecision_NormalizesToFirstOfYear()
    {
        var (date, precision) = ReleasePoller.ParseReleaseDate("2024", "year");

        Assert.Equal(new DateOnly(2024, 1, 1), date);
        Assert.Equal(ReleaseDatePrecision.Year, precision);
    }

    [Fact]
    public void ParseReleaseDate_MissingValue_ReturnsNullDate()
    {
        var (date, precision) = ReleasePoller.ParseReleaseDate(null, null);

        Assert.Null(date);
        Assert.Equal(ReleaseDatePrecision.Day, precision);
    }
}
