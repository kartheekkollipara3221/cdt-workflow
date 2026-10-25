using System.Text.Json;
using System.Text.Json.Serialization;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service;
using BBH.CP.Atlas.Service.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BBH.CP.Atlas.Platform.Tests;

// C0 smoke tests: the stand-ins compile against the shapes that the platform needs.
public class StandInSmokeTests
{
    private static readonly JsonSerializerOptions TolerantOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Current_TTD_json_reads_into_the_stand_in_DTOs()
    {
        string json = File.ReadAllText(TestPaths.TargetConfigFile("TTD.json.txt"));

        CPAtlasApplicationConfig? config = JsonSerializer.Deserialize<CPAtlasApplicationConfig>(json, TolerantOptions);

        Assert.NotNull(config);
        Assert.NotNull(config.Section);
        Assert.NotNull(config.Section.FindControlByName("WeeklyHour")?.Grid);
        Assert.NotNull(config.Section.FindControlByName("TaxPreparerName"));
        Assert.NotNull(config.Section.FindButton("Submit"));
    }

    [Fact]
    public void A_section_service_builds_on_the_stand_in_base_class()
    {
        SmokeSectionService service = new();

        CPApplicationSection section = ((ISectionService)service).LoadPage(new CPAtlasPage { ID = 0 });

        Assert.Equal("Smoke", section.Name);
        Assert.Throws<NotSupportedException>(() => service.LoadDashboard());
    }

    private sealed class SmokeSectionService : CPApplicationService, ISectionService
    {
        public CPApplicationSection LoadPage(CPAtlasPage page) => new() { Name = "Smoke" };

        public override CPAtlasPage SavePage(CPAtlasPage input, IFormFileCollection files) => input;

        public override CPAtlasPage RemovePage(int id) => new() { ID = id };
    }
}
