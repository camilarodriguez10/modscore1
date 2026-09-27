using Modernization.Insights;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// El catálogo de fin de vida: que la foto de endoflife.date carga, que cada versión cae en
/// su ciclo y que el estado se calcula contra la fecha, no contra una corrida anterior.
/// </summary>
public sealed class EndOfLifeCatalogTests
{
    private static readonly EndOfLifeCatalog Catalog = EndOfLifeCatalog.Default;

    [Fact]
    public void TheSnapshotLoadsAndNamesItsSource()
    {
        Assert.Equal("eol-catalog-v1", Catalog.Version);
        Assert.Equal("endoflife.date", Catalog.Source);
        Assert.Equal(new DateOnly(2026, 9, 26), Catalog.RetrievedOn);
        Assert.Empty(Catalog.Problems());
        Assert.All(Catalog.Products, p => Assert.StartsWith("https://endoflife.date/", p.SourceUrl, StringComparison.Ordinal));

        // Los productos que declara un repositorio típico del portafolio.
        foreach (var key in new[] { "nodejs", "dotnet", "dotnetfx", "oracle-jdk", "python", "spring-boot", "ubuntu", "mssqlserver" })
        {
            Assert.NotNull(Catalog.Find(key));
        }
    }

    [Fact]
    public void TheDatesAreTheOnesEndOfLifeDatePublishes()
    {
        Assert.Equal(new DateOnly(2026, 4, 30), Catalog.Match("nodejs", "20.18.0")!.Eol);
        Assert.Equal(new DateOnly(2026, 11, 10), Catalog.Match("dotnet", "8.0.11")!.Eol);
        Assert.Equal(new DateOnly(2022, 4, 26), Catalog.Match("dotnetfx", "4.6.1")!.Eol);
        Assert.Equal(new DateOnly(2020, 1, 1), Catalog.Match("python", "2.7.18")!.Eol);
        Assert.Equal("2019", Catalog.Match("mssqlserver", "15.0")!.Label);
    }

    [Theory]
    [InlineData("python", "3.10.2", "3.10")]
    [InlineData("python", "3.1.4", "3.1")]
    [InlineData("log4j", "2.12.4", "2.12")]
    [InlineData("log4j", "2.24.3", "2")]
    [InlineData("log4j", "1.2.17", "1")]
    [InlineData("oracle-jdk", "1.8.0_392", "8")]
    [InlineData("oracle-jdk", "17.0.12", "17")]
    [InlineData("mssqlserver", "15.0.4375.4", "15.0")]
    [InlineData("mssqlserver", "13.0-sp3", "13.0-sp3")]
    [InlineData("go", "v1.21.13", "1.21")]
    [InlineData("windows-server", "2012-r2", "2012-r2")]
    public void AVersionFallsInTheLongestCycleThatStartsIt(string product, string version, string cycle)
        => Assert.Equal(cycle, Catalog.Match(product, version)?.Cycle);

    [Theory]
    [InlineData("websphere", "9.0.5")]
    [InlineData("angular", "8.2.14")]
    [InlineData("nodejs", "")]
    public void WhatTheCatalogDoesNotKnowHasNoCycle(string product, string version)
        => Assert.Null(Catalog.Match(product, version));

    [Fact]
    public void TheStatusDependsOnTheDateAndTheWindow()
    {
        var cycle = new EndOfLifeCycle
        {
            Cycle = "1",
            SupportEnds = new DateOnly(2026, 1, 1),
            Eol = new DateOnly(2027, 1, 1),
        };

        Assert.Equal(EndOfLifeStatus.Supported, cycle.StatusOn(new DateOnly(2025, 6, 1), 6));
        Assert.Equal(EndOfLifeStatus.SecurityOnly, cycle.StatusOn(new DateOnly(2026, 3, 1), 6));
        Assert.Equal(EndOfLifeStatus.Approaching, cycle.StatusOn(new DateOnly(2026, 7, 1), 6));
        Assert.Equal(EndOfLifeStatus.SecurityOnly, cycle.StatusOn(new DateOnly(2026, 7, 1), 5));
        Assert.Equal(EndOfLifeStatus.EndOfLife, cycle.StatusOn(new DateOnly(2027, 1, 1), 6));
    }

    [Fact]
    public void ADiscontinuedCycleIsPastItsEndWithoutADate()
    {
        var jquery = Catalog.Match("jquery", "1.12.4")!;

        Assert.True(jquery.Discontinued);
        Assert.Null(jquery.Eol);
        Assert.Equal(EndOfLifeStatus.EndOfLife, jquery.StatusOn(new DateOnly(2026, 9, 26), 12));
        Assert.Null(jquery.MonthsToEol(new DateOnly(2026, 9, 26)));
    }

    [Theory]
    [InlineData("2026-01-20", "2026-03-10", 1)]
    [InlineData("2026-01-10", "2026-03-10", 2)]
    [InlineData("2026-03-10", "2026-03-10", 0)]
    [InlineData("2026-03-10", "2026-01-20", -1)]
    [InlineData("2026-03-10", "2026-01-10", -2)]
    public void MonthsToTheEndAreWholeMonths(string asOf, string eol, int expected)
    {
        var cycle = new EndOfLifeCycle { Cycle = "x", Eol = DateOnly.Parse(eol, System.Globalization.CultureInfo.InvariantCulture) };

        Assert.Equal(expected, cycle.MonthsToEol(DateOnly.Parse(asOf, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void AnInconsistentCatalogIsRejected()
    {
        const string duplicated = """
            { "version": "x", "source": "s", "sourceUrl": "u", "retrievedOn": "2026-09-26",
              "products": [
                { "key": "a", "name": "A", "kind": "runtime", "sourceUrl": "u", "cycles": [ { "cycle": "1" }, { "cycle": "1" } ] } ] }
            """;
        const string contradictory = """
            { "version": "x", "source": "s", "sourceUrl": "u", "retrievedOn": "2026-09-26",
              "products": [
                { "key": "a", "name": "A", "kind": "runtime", "sourceUrl": "u", "cycles": [ { "cycle": "1", "eol": "2020-01-01", "discontinued": true } ] } ] }
            """;
        const string unknownKind = """
            { "version": "x", "source": "s", "sourceUrl": "u", "retrievedOn": "2026-09-26",
              "products": [ { "key": "a", "name": "A", "kind": "gadget", "sourceUrl": "u", "cycles": [ { "cycle": "1" } ] } ] }
            """;

        Assert.Throws<EndOfLifeCatalogException>(() => EndOfLifeCatalog.Parse(duplicated));
        Assert.Throws<EndOfLifeCatalogException>(() => EndOfLifeCatalog.Parse(contradictory));
        Assert.Throws<EndOfLifeCatalogException>(() => EndOfLifeCatalog.Parse(unknownKind));
        Assert.Throws<EndOfLifeCatalogException>(() => EndOfLifeCatalog.Parse("{ not json"));
    }
}
