using System.Net;
using System.Text.Json;
using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.App.StandIn;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BBH.CP.Atlas.Platform.Tests.Api;

/// <summary>The stand-in API on Kestrel (random port) with the test database (spec 12, 11.2 C6).</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class EndpointTests(SqlServerDatabaseFixture database) : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await database.SeedUserAsync("api.owner", "Api Owner", "TTDV2", "Owner");
        await database.SeedUserAsync("api.read", "Api Reader", "TTDV2", "Read");
        await database.SeedUserAsync("api.owner", "Api Owner", "CFPINFLOW", "Write");
        // A row of an app that is not registered: v1/Application hides it (spec 07, 4.6).
        await database.ScalarAsync("""
            IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Application WHERE Code = 'ZZNOREG')
                INSERT INTO dbo.CP_Atlas_Application (Name, Code, IsDeleted) VALUES ('Not registered', 'ZZNOREG', 0);
            DECLARE @App int = (SELECT ID FROM dbo.CP_Atlas_Application WHERE Code = 'ZZNOREG');
            IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Entitlement WHERE CP_Atlas_ApplicationID = @App)
                INSERT INTO dbo.CP_Atlas_Entitlement (CP_Atlas_ApplicationID, Name, IsRead, IsWrite, IsAdmin, IsDeleted) VALUES (@App, 'Read', 1, 0, 0, 0);
            """);
        await database.SeedUserAsync("api.owner", "Api Owner", "ZZNOREG", "Read");
        await database.ScalarAsync("""
            IF OBJECT_ID(N'dbo.Get_Application', N'P') IS NULL
                EXEC(N'CREATE PROCEDURE dbo.Get_Application @LanID NVARCHAR(100) AS
                     SELECT DISTINCT ApplicationId AS ID, a.Name, ApplicationCode AS Code, ParentApplicationId AS ParentID, RoleName AS Role
                     FROM dbo.vw_CP_Atlas_ActiveUserRole r INNER JOIN dbo.CP_Atlas_Application a ON a.ID = r.ApplicationId WHERE r.LanId = @LanID');
            """);

        string configFolder = Path.Combine(TestPaths.AtlasPlatformRoot(), "src", "BBH.CP.Atlas.App", "Config");
        _app = StandInHost.Create(["--environment=Development", "--urls=http://127.0.0.1:0"], builder =>
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DBConnection"] = database.ConnectionString,
                ["AppSettings:ConfigFolder"] = configFolder,
                ["AppSettings:Modules:TTDV2:AccessMode"] = "Enforce",
                ["StandIn:Authentication"] = "Fixed",
                ["StandIn:FixedUser"] = "LOCAL\\api.owner",
                ["StandIn:ActAs:Enabled"] = "true",
                ["StandIn:Users:api.read:DisplayName"] = "Api Reader"
            }));
        await _app.Services.StartAtlasPlatformAsync();
        await _app.StartAsync();

        string address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task A_file_saved_with_ManagePage_downloads_as_an_attachment_with_nosniff()
    {
        byte[] pdf = "%PDF-1.7 endpoint test"u8.ToArray();
        using MultipartFormDataContent form = new();
        form.Add(new ByteArrayContent(pdf), "files", "FlowAttachments#$#client instruction.pdf");
        form.Add(new StringContent("""
            {"AppCode":"CFPINFLOW","ID":0,"Outputs":[{"Name":"AccountNumber","Value":"700100"},{"Name":"InvestmentType","Value":"Equity"},
             {"Name":"Amount","Value":"10"},{"Name":"InflowDate","Value":"2026-10-20"},{"Name":"FlowConfirm","Value":"true"}]}
            """), "input");
        using HttpResponseMessage saved = await _client.PostAsync("v1/ManagePage", form);
        saved.EnsureSuccessStatusCode();
        int id = JsonDocument.Parse(await saved.Content.ReadAsStringAsync()).RootElement.GetProperty("ID").GetInt32();
        Assert.True(id > 0, await saved.Content.ReadAsStringAsync());
        int fileId = Convert.ToInt32(await database.ScalarAsync($"SELECT ID FROM CFP.InflowAttachment WHERE InflowId = {id}"),
            System.Globalization.CultureInfo.InvariantCulture);

        using HttpResponseMessage download = await _client.GetAsync($"v1/Attachment/CFPINFLOW/{fileId}");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("client instruction.pdf", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("nosniff", string.Join(",", download.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());

        using HttpResponseMessage missing = await _client.GetAsync($"v1/Attachment/CFPINFLOW/{fileId + 100000}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using HttpResponseMessage notAnApp = await _client.GetAsync($"v1/Attachment/NOAPP/{fileId}");
        Assert.Equal(HttpStatusCode.NotFound, notAnApp.StatusCode);
    }

    [Fact]
    public async Task Application_adds_FormType_and_HasDashboard_and_hides_apps_that_are_not_registered()
    {
        JsonElement result = await GetResultAsync("v1/Application");

        JsonElement ttdv2 = result.EnumerateArray().Single(x => x.GetProperty("Code").GetString() == "TTDV2");
        Assert.Equal("FORM", ttdv2.GetProperty("FormType").GetString());
        Assert.True(ttdv2.GetProperty("HasDashboard").GetBoolean());
        Assert.DoesNotContain(result.EnumerateArray(), x => x.GetProperty("Code").GetString() == "ZZNOREG");
    }

    [Fact]
    public async Task A_record_is_loaded_saved_and_listed_in_the_audit_log_through_the_API()
    {
        JsonElement section = await GetResultAsync("v1/SectionPage?AppCode=TTDV2&ID=0");
        Assert.Equal("TTDV2", section.GetProperty("AppCode").GetString());
        // The week and year lists follow the clock of the host, so the test sends the defaults of the loaded page.
        string taxYear = ControlValue(section, "TaxYear");
        string weekEnding = ControlValue(section, "WeekEnding");

        string input = JsonSerializer.Serialize(new
        {
            AppCode = "TTDV2",
            ID = 0,
            Outputs = new[]
            {
                new { Name = "Account", Value = "100100 - Smith Family Trust" },
                new { Name = "TaxYear", Value = taxYear },
                new { Name = "Category", Value = "TAX_N" },
                new { Name = "TaxPreparer", Value = "api.owner" },
                new { Name = "WeekEnding", Value = weekEnding },
                new { Name = "WeeklyHour", Value = """[{"MondayMinute":"60","TuesdayMinute":"30"}]""" }
            }
        });
        using HttpResponseMessage save = await _client.PostAsync("v1/ManagePage", new MultipartFormDataContent { { new StringContent(input), "input" } });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        JsonElement page = JsonDocument.Parse(await save.Content.ReadAsStringAsync()).RootElement;
        int id = page.GetProperty("ID").GetInt32();
        Assert.True(id > 0, page.ToString());

        JsonElement loaded = await GetResultAsync($"v1/SectionPage?AppCode=TTDV2&ID={id}");
        Assert.EndsWith($" - {id}", loaded.GetProperty("HeaderName").GetString());

        JsonElement audit = await GetResultAsync($"v1/GetAuditLog?appCode=TTDV2&refID={id}");
        Assert.Equal("Create", audit[0].GetProperty("Event").GetString());

        using HttpResponseMessage removed = await _client.PostAsync($"v1/RemovePage?code=TTDV2&id={id}", null);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
    }

    [Fact]
    public async Task Problems_give_404_and_403_with_a_problem_body()
    {
        using HttpResponseMessage unknown = await _client.GetAsync("v1/SectionPage?AppCode=NOAPP&ID=0");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        using HttpRequestMessage request = new(HttpMethod.Get, "v1/SectionPage?AppCode=TTDV2&ID=0");
        request.Headers.Add(StandInActAsMiddleware.HeaderName, "api.read");
        using HttpResponseMessage denied = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        string body = await denied.Content.ReadAsStringAsync();
        Assert.Contains("Create", body, StringComparison.Ordinal);
        // Review SV-11: the body comes from the ProblemDetailsFactory of the host (type and traceId are added).
        Assert.Contains("\"title\":\"Access denied\"", body, StringComparison.Ordinal);
        Assert.Contains("\"traceId\"", body, StringComparison.Ordinal);
        Assert.Contains("\"type\"", body, StringComparison.Ordinal);
    }

    private async Task<JsonElement> GetResultAsync(string url)
    {
        using HttpResponseMessage response = await _client.GetAsync(url);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{url}: {(int)response.StatusCode} {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("result").Clone();
    }

    private static string ControlValue(JsonElement section, string name)
    {
        foreach (JsonElement control in section.GetProperty("Controls").EnumerateArray())
        {
            if (control.GetProperty("Name").GetString() == name)
                return control.GetProperty("Value").GetString()!;
            if (control.TryGetProperty("ChildControls", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
                foreach (JsonElement child in children.EnumerateArray())
                    if (child.GetProperty("Name").GetString() == name)
                        return child.GetProperty("Value").GetString()!;
        }

        throw new InvalidOperationException($"No control {name}.");
    }
}
