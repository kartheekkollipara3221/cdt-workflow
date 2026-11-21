using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using BBH.CP.Atlas.Service.Platform.Workflow;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowNotification;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Infrastructure.WorkflowNotification;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>
/// The engine adapter with the real workflow engine and script 150 (module 25, 10; module 10, section 8): the two submits
/// of ClientInflows, the emails, the refusals, the double submit, a later action failure and the state check on load.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class CfpInflowWorkflowTests(SqlServerDatabaseFixture database) : IAsyncLifetime, IDisposable
{
    private const string App = "CFPINFLOW";
    private const string Writer = "wf.write";
    private static readonly byte[] Pdf = "%PDF-1.7 workflow test"u8.ToArray();

    private readonly AtlasTableRegistry _registry = new();
    private readonly CapturingLoggerFactory _logs = new();
    private SectionHarness _harness = null!;
    private ServiceProvider _engine = null!;
    private IServiceScope _scope = null!;

    public async Task InitializeAsync()
    {
        await database.SeedUserAsync(Writer, "Wanda Workflow", App, "Write");
        await using (SqlConnection connection = new(database.ConnectionString))
            await new AtlasColumnMetadataReader().LoadAsync(connection, _registry, ["CFP.Inflow", "CFP.AuditLog", "CFP.InflowAttachment", "AIN.AccountInventory"]);
        _harness = new SectionHarness(database);

        // The engine as the target program.cs registers it, with the stand-in dispatcher and no real email.
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(_harness.Time);
        services.AddScoped<WorkflowActionEnvelopeNormalizer>();
        services.AddScoped<CanonicalRequestHasher>();
        services.AddScoped<WorkflowActionResultBuilder>();
        services.AddScoped<CanonicalWorkflowResultSerializer>();
        services.AddWorkflowInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = database.ConnectionString,
            ["Notifications:Sender"] = "NoOp"
        }).Build());
        services.AddScoped<IWorkflowActionNotificationDispatcher, WorkflowActionNotificationDispatcher>();
        _engine = services.BuildServiceProvider();
        _scope = _engine.CreateScope();
        _harness.Workflow = new AtlasWorkflowAdapter(_scope.ServiceProvider);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _scope?.Dispose();
        _engine?.Dispose();
        _harness?.Dispose();
    }

    [Fact]
    public async Task The_two_submits_move_the_record_and_queue_and_send_E1_E2_then_E3()
    {
        CPAtlasPage first = Svc().SavePage(NewInput("700100"), Files(("FlowAttachments#$#instruction.pdf", Pdf)));
        AssertSaved(first);
        string instance = $"WF-CFP-INFLOW-{first.ID}";

        Assert.Equal("Submitted|Submitted", await Scalar($"SELECT CONCAT_WS('|', WorkflowState, Status) FROM CFP.Inflow WHERE ID = {first.ID}"));
        Assert.Equal("Submitted", await Scalar($"SELECT CurrentStateKey FROM dbo.WorkflowInstance WHERE WorkflowInstanceId = '{instance}'"));
        Assert.Equal(
            "ClientInflow.ActionRequired|Harbor Family Trust Cash Inflow Instruction – Action Required|Sent;"
            + "ClientInflow.SubmittedInformational|Harbor Family Trust Inflow Instruction - Informational|Sent",
            await Scalar($"SELECT STRING_AGG(CONCAT_WS('|', NotificationType, Subject, Status), ';') WITHIN GROUP (ORDER BY Id) FROM dbo.NotificationOutbox WHERE WorkflowInstanceId = '{instance}'"));
        string e1 = (string)(await Scalar($"SELECT TOP (1) CONCAT(Body, NotificationRecipientsJson) FROM dbo.NotificationOutbox WHERE WorkflowInstanceId = '{instance}' ORDER BY Id"))!;
        Assert.Contains("Amount: $1,250,000.50", e1, StringComparison.Ordinal);
        Assert.Contains("Inflow Date: 10/20/2026", e1, StringComparison.Ordinal);
        Assert.Contains($"https://atlas.test/form/CFPINFLOW/{first.ID}", e1, StringComparison.Ordinal);
        Assert.Contains("icg@test.local", e1, StringComparison.Ordinal);
        string e2 = (string)(await Scalar($"SELECT TOP (1) NotificationRecipientsJson FROM dbo.NotificationOutbox WHERE WorkflowInstanceId = '{instance}' ORDER BY Id DESC"))!;
        Assert.Contains("eq@test.local", e2, StringComparison.Ordinal); // Equity list (two addresses)
        Assert.Contains("eq2@test.local", e2, StringComparison.Ordinal);
        Assert.DoesNotContain("fi@test.local", e2, StringComparison.Ordinal);
        Assert.Contains(_logs.Entries, x => x.Contains("Atlas.RecipientSkipped", StringComparison.Ordinal) && x.Contains("Riley Manager", StringComparison.Ordinal));
        Assert.Contains("Submitted", Convert.ToString(await Scalar(
            $"SELECT LogMessage FROM CFP.AuditLog WHERE ReferenceID = {first.ID} AND LogMessage LIKE '%\"Create\"%'"), System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        CPApplicationSection loaded = Svc().LoadPage(new CPAtlasPage { AppCode = App, ID = first.ID });
        CPAtlasPage second = Svc().SavePage(EditInput(first.ID, loaded.Version, ("FundsReceived", "true"), ("CashConfirm", "true")),
            Files(("CashAttachments#$#receipt.pdf", Pdf)));
        AssertSaved(second);

        Assert.Equal("FundsReceived|Funds Received", await Scalar($"SELECT CONCAT_WS('|', WorkflowState, Status) FROM CFP.Inflow WHERE ID = {first.ID}"));
        string e3 = (string)(await Scalar(
            $"SELECT CONCAT_WS('|', Subject, NotificationRecipientsJson) FROM dbo.NotificationOutbox WHERE WorkflowInstanceId = '{instance}' AND NotificationType = 'ClientInflow.FundsReceived'"))!;
        Assert.StartsWith("Harbor Family Trust Cash Inflow - Informational|", e3, StringComparison.Ordinal);
        foreach (string address in new[] { "eq@test.local", "cs@test.local", "imrg@test.local", "star@test.local" })
            Assert.Contains(address, e3, StringComparison.Ordinal);
        Assert.Contains("Submitted", Convert.ToString(await Scalar(
            $"SELECT TOP (1) LogMessage FROM CFP.AuditLog WHERE ReferenceID = {first.ID} AND LogMessage LIKE '%Funds Received%'"), System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_engine_action_changes_nothing_and_gives_the_engine_code()
    {
        CPAtlasPage first = Svc().SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));
        AssertSaved(first);
        CPApplicationSection loaded = Svc().LoadPage(new CPAtlasPage { AppCode = App, ID = first.ID });
        await SetTransitionAsync("ConfirmFunds", active: false);
        try
        {
            CPAtlasPage refused = Svc().SavePage(EditInput(first.ID, loaded.Version, ("FundsReceived", "true"), ("CashConfirm", "true")),
                Files(("CashAttachments#$#receipt.pdf", Pdf)));

            Assert.Equal(0, refused.ID);
            CPPageError error = Assert.Single(refused.Errors!);
            Assert.Equal("", error.Field);
            Assert.StartsWith("The workflow did not accept the submit", error.Message, StringComparison.Ordinal);
            Assert.Equal("Submitted|0|", await Scalar(
                $"SELECT CONCAT_WS('|', WorkflowState, CAST(ISNULL(FundsReceived, 0) AS int), ISNULL(CashSubmittedBy, '')) FROM CFP.Inflow WHERE ID = {first.ID}"));
            Assert.Equal(0, await Scalar($"SELECT COUNT(*) FROM CFP.InflowAttachment WHERE InflowId = {first.ID} AND [Type] = 'CashAttachments'"));
        }
        finally
        {
            await SetTransitionAsync("ConfirmFunds", active: true);
        }
    }

    [Fact]
    public async Task A_failed_audit_write_stops_the_submit_before_the_engine_call()
    {
        object? instances = await Scalar("SELECT COUNT(*) FROM dbo.WorkflowInstance");
        object? outbox = await Scalar("SELECT COUNT(*) FROM dbo.NotificationOutbox");
        await Scalar("ALTER TABLE CFP.AuditLog WITH NOCHECK ADD CONSTRAINT CK_Test_NoAudit CHECK (ReferenceID < 0); SELECT 1;");
        try
        {
            CPAtlasPage refused = Svc().SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));

            Assert.Equal(0, refused.ID);
            Assert.Equal("SaveFailed", Assert.Single(refused.Errors!).Code);
            Assert.Equal(instances, await Scalar("SELECT COUNT(*) FROM dbo.WorkflowInstance"));
            Assert.Equal(outbox, await Scalar("SELECT COUNT(*) FROM dbo.NotificationOutbox"));
            Assert.DoesNotContain(_logs.Entries, x => x.Contains("AtlasCommitFailed", StringComparison.Ordinal));
        }
        finally
        {
            await Scalar("ALTER TABLE CFP.AuditLog DROP CONSTRAINT CK_Test_NoAudit; SELECT 1;");
        }
    }

    [Fact]
    public async Task The_second_submit_keeps_one_audit_entry_with_the_status_line()
    {
        CPAtlasPage first = Svc().SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));
        AssertSaved(first);
        CPApplicationSection loaded = Svc().LoadPage(new CPAtlasPage { AppCode = App, ID = first.ID });
        object? before = await Scalar($"SELECT COUNT(*) FROM CFP.AuditLog WHERE ReferenceID = {first.ID}");

        AssertSaved(Svc().SavePage(EditInput(first.ID, loaded.Version, ("FundsReceived", "true"), ("CashConfirm", "true")),
            Files(("CashAttachments#$#receipt.pdf", Pdf))));

        // One Update entry with the fields and the status line, and one AttachmentAdd entry.
        Assert.Equal(Convert.ToInt32(before) + 2, Convert.ToInt32(await Scalar($"SELECT COUNT(*) FROM CFP.AuditLog WHERE ReferenceID = {first.ID}")));
        string update = Convert.ToString(await Scalar(
            $"SELECT LogMessage FROM CFP.AuditLog WHERE ReferenceID = {first.ID} AND LogMessage LIKE '%\"Update\"%'"), System.Globalization.CultureInfo.InvariantCulture)!;
        Assert.Contains("FundsReceived", update, StringComparison.Ordinal);
        Assert.Contains("\"Old\":\"Submitted\",\"Value\":\"Funds Received\"", update, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_name_with_one_user_gives_the_address_in_E2_and_in_E3_To_and_Cc()
    {
        await Scalar("""
            IF NOT EXISTS (SELECT 1 FROM AIN.AccountInventory WHERE AccountNumber = '700900')
                INSERT INTO AIN.AccountInventory (Account, AccountName, AccountNumber, RelationshipManager, StarAccountNumber,
                    Custodian, CreatedBy, CreatedDate, IsDeleted, RMCC)
                VALUES ('700900 - Mail Test', 'Mail Test', '700900', 'Pat Mail', 'S-700900', 'Custodian One', 'test', GETUTCDATE(), 0,
                    'Casey Mail;Pat Mail');
            IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_User WHERE LanID = 'wf.pat')
                INSERT INTO dbo.CP_Atlas_User (LanID, DisplayName, Email, IsDeleted) VALUES
                ('wf.pat', N'  pat   MAIL ', N'pat@test.local', 0), ('wf.casey', N'Casey Mail', N'casey@test.local', 0);
            SELECT 1;
            """);

        CPAtlasPage first = Svc().SavePage(NewInput("700900"), Files(("FlowAttachments#$#a.pdf", Pdf)));
        AssertSaved(first);
        CPApplicationSection loaded = Svc().LoadPage(new CPAtlasPage { AppCode = App, ID = first.ID });
        AssertSaved(Svc().SavePage(EditInput(first.ID, loaded.Version, ("FundsReceived", "true"), ("CashConfirm", "true")),
            Files(("CashAttachments#$#receipt.pdf", Pdf))));

        string instance = $"WF-CFP-INFLOW-{first.ID}";
        (List<string> e2To, _) = await RecipientsAsync(instance, "ClientInflow.SubmittedInformational");
        (List<string> e3To, List<string> e3Cc) = await RecipientsAsync(instance, "ClientInflow.FundsReceived");
        Assert.Contains("pat@test.local", e2To);
        Assert.Contains("pat@test.local", e3To);
        Assert.Contains("casey@test.local", e3Cc);
        Assert.DoesNotContain(_logs.Entries, x => x.Contains("Atlas.RecipientSkipped", StringComparison.Ordinal));
    }

    /// <summary>Returns the To and Cc addresses of the outbox row of one notification type.</summary>
    private async Task<(List<string> To, List<string> Cc)> RecipientsAsync(string instance, string type)
    {
        string json = (string)(await Scalar(
            $"SELECT TOP (1) NotificationRecipientsJson FROM dbo.NotificationOutbox WHERE WorkflowInstanceId = '{instance}' AND NotificationType = '{type}'"))!;
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        List<string> Read(string name) => [.. document.RootElement.EnumerateObject()
            .Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => x.Value.EnumerateArray()).Select(x => x.GetString()!)];
        return (Read("ToEmailAddresses"), Read("CcEmailAddresses"));
    }

    [Fact]
    public async Task A_later_action_that_gives_another_state_keeps_the_save_and_writes_the_error_log()
    {
        FakeWorkflowAdapter engine = new();
        engine.NewStates["NotifyInformational"] = "Elsewhere";
        using SectionHarness harness = new(database) { Workflow = engine };

        CPAtlasPage first = SvcOf(harness).SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));

        AssertSaved(first);
        Assert.Equal("Submitted", await Scalar($"SELECT WorkflowState FROM CFP.Inflow WHERE ID = {first.ID}"));
        Assert.Contains(_logs.Entries, x => x.Contains("Atlas.WorkflowStepFailed", StringComparison.Ordinal)
            && x.Contains("StateDifference: the record has Submitted, the engine has Elsewhere", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_necessary_recipient_with_no_address_refuses_the_submit_with_no_write()
    {
        using ConfigCopy config = new ConfigCopy().Replace("CFPINFLOW.json",
            "\"relationshipManager\": { \"FromNameField\": \"RelationshipManager\", \"Optional\": true }",
            "\"relationshipManager\": { \"FromNameField\": \"RelationshipManager\", \"Optional\": false }");
        FakeWorkflowAdapter engine = new();
        using SectionHarness harness = new(database, AtlasAccessMode.Enforce, config.Folder) { Workflow = engine };
        object? before = await Scalar("SELECT COUNT(*) FROM CFP.Inflow");

        CPAtlasPage refused = SvcOf(harness).SavePage(NewInput("700200"), Files(("FlowAttachments#$#a.pdf", Pdf))); // Nobody Known

        Assert.Equal(0, refused.ID);
        Assert.Equal("NoRecipient", Assert.Single(refused.Errors!).Code);
        Assert.Equal(before, await Scalar("SELECT COUNT(*) FROM CFP.Inflow"));
        Assert.Empty(engine.Calls);
        Assert.Contains(_logs.Entries, x => x.Contains("Atlas.RecipientSkipped", StringComparison.Ordinal) && x.Contains("RecordId=0 ", StringComparison.Ordinal)
            && x.Contains("Nobody Known", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_engine_exception_refuses_the_submit_and_writes_nothing()
    {
        using SectionHarness harness = new(database) { Workflow = new ThrowingWorkflowAdapter() };
        object? before = await Scalar("SELECT COUNT(*) FROM CFP.Inflow");

        CPAtlasPage refused = SvcOf(harness).SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));

        Assert.Equal(0, refused.ID);
        Assert.Equal("EngineError", Assert.Single(refused.Errors!).Code);
        Assert.Equal(before, await Scalar("SELECT COUNT(*) FROM CFP.Inflow"));
        Assert.Contains(_logs.Entries, x => x.Contains("Atlas.WorkflowStepFailed", StringComparison.Ordinal) && x.Contains("Reason=EngineError", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_write_after_the_engine_action_writes_the_AtlasCommitFailed_log()
    {
        FakeWorkflowAdapter engine = new();
        engine.NewStates["Submit"] = new string('S', 300); // longer than WorkflowState: the state write fails
        using SectionHarness harness = new(database) { Workflow = engine };
        object? before = await Scalar("SELECT COUNT(*) FROM CFP.Inflow");

        CPAtlasPage refused = SvcOf(harness).SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));

        Assert.Equal(0, refused.ID);
        Assert.Equal("SaveFailed", Assert.Single(refused.Errors!).Code);
        Assert.Equal(before, await Scalar("SELECT COUNT(*) FROM CFP.Inflow"));
        Assert.Single(engine.Calls); // the later action does not run
        Assert.Contains(_logs.Entries, x => x.Contains("Atlas.WorkflowStepFailed", StringComparison.Ordinal) && x.Contains("AtlasCommitFailed", StringComparison.Ordinal));
    }

    private ConfigSectionService SvcOf(SectionHarness harness) =>
        new(harness.ModuleServices(_registry, Writer, [CfpRoles.Write with { }], _logs)) { AppCode = App };

    /// <summary>An engine that throws at each action.</summary>
    private sealed class ThrowingWorkflowAdapter : IAtlasWorkflowAdapter
    {
        public AtlasWorkflowResult Run(AtlasWorkflowCall call) => throw new InvalidOperationException("The engine is not available.");

        public string? ReadState(string instanceId, string userId, IReadOnlyList<string> roles) => null;

        public string? DispatchNotifications(AtlasWorkflowResult result) => null;
    }

    [Fact]
    public void The_RecipientSkipped_log_of_a_first_submit_has_the_new_record_ID()
    {
        CPAtlasPage first = Svc().SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));

        AssertSaved(first);
        List<string> skipped = [.. _logs.Entries.Where(x => x.Contains("Atlas.RecipientSkipped", StringComparison.Ordinal))];
        Assert.NotEmpty(skipped);
        Assert.All(skipped, x => Assert.Contains($"RecordId={first.ID} ", x, StringComparison.Ordinal));
    }

    [Fact]
    public void The_log_of_a_refused_file_has_no_line_break_from_the_file_name()
    {
        CPAtlasPage refused = Svc().SavePage(NewInput("700100"), Files(
            ("FlowAttachments#$#a\r\nAtlas.Forged line.exe", Pdf), ("No\r\nControl.pdf", Pdf)));

        Assert.Equal(0, refused.ID);
        List<string> logs = [.. _logs.Entries.Where(x => x.Contains("Atlas.AttachmentRefused", StringComparison.Ordinal))];
        Assert.Equal(2, logs.Count);
        Assert.All(logs, x => Assert.DoesNotContain('\n', x));
        Assert.All(logs, x => Assert.DoesNotContain('\r', x));
    }

    [Fact]
    public async Task A_second_submit_sent_again_with_its_RequestId_changes_nothing_and_sends_no_second_email()
    {
        CPAtlasPage first = Svc().SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));
        AssertSaved(first);
        CPApplicationSection loaded = Svc().LoadPage(new CPAtlasPage { AppCode = App, ID = first.ID });
        CPAtlasPage second = EditInput(first.ID, loaded.Version, ("FundsReceived", "true"), ("CashConfirm", "true"));
        AssertSaved(Svc().SavePage(second, Files(("CashAttachments#$#receipt.pdf", Pdf))));

        CPAtlasPage again = Svc().SavePage(second, Files(("CashAttachments#$#receipt.pdf", Pdf))); // the answer was lost

        Assert.Equal(0, again.ID);
        Assert.NotEmpty(again.Errors!);
        Assert.Equal(1, await Scalar($"SELECT COUNT(*) FROM dbo.NotificationOutbox WHERE WorkflowInstanceId = 'WF-CFP-INFLOW-{first.ID}' AND NotificationType = 'ClientInflow.FundsReceived'"));
        Assert.Equal(1, await Scalar($"SELECT COUNT(*) FROM CFP.InflowAttachment WHERE InflowId = {first.ID} AND [Type] = 'CashAttachments'"));
    }

    [Fact]
    public async Task A_double_submit_with_one_RequestId_makes_one_record()
    {
        CPAtlasPage input = NewInput("700100");
        object? before = await Scalar("SELECT COUNT(*) FROM CFP.Inflow");

        AssertSaved(Svc().SavePage(input, Files(("FlowAttachments#$#a.pdf", Pdf))));
        CPAtlasPage again = Svc().SavePage(input, Files(("FlowAttachments#$#a.pdf", Pdf)));

        Assert.Equal(0, again.ID);
        Assert.Equal("This request was already processed.", Assert.Single(again.Errors!).Message);
        Assert.Equal(Convert.ToInt32(before) + 1, Convert.ToInt32(await Scalar("SELECT COUNT(*) FROM CFP.Inflow")));
    }

    [Fact]
    public async Task A_failed_later_action_keeps_the_save_and_writes_the_error_log()
    {
        await SetTransitionAsync("NotifyInformational", active: false);
        try
        {
            CPAtlasPage first = Svc().SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));

            AssertSaved(first);
            Assert.Equal("Submitted", await Scalar($"SELECT WorkflowState FROM CFP.Inflow WHERE ID = {first.ID}"));
            Assert.Equal(1, await Scalar($"SELECT COUNT(*) FROM dbo.NotificationOutbox WHERE WorkflowInstanceId = 'WF-CFP-INFLOW-{first.ID}'"));
            Assert.Contains(_logs.Entries, x => x.Contains("Atlas.WorkflowStepFailed", StringComparison.Ordinal)
                && x.Contains("NotifyInformational", StringComparison.Ordinal));
        }
        finally
        {
            await SetTransitionAsync("NotifyInformational", active: true);
        }
    }

    [Fact]
    public async Task The_load_writes_the_engine_state_to_a_record_with_another_state()
    {
        CPAtlasPage first = Svc().SavePage(NewInput("700100"), Files(("FlowAttachments#$#a.pdf", Pdf)));
        AssertSaved(first);
        await Scalar($"UPDATE CFP.Inflow SET WorkflowState = 'Draft', Status = 'Draft' WHERE ID = {first.ID}; SELECT 1;");

        CPApplicationSection loaded = Svc().LoadPage(new CPAtlasPage { AppCode = App, ID = first.ID });

        Assert.Equal("Submitted|Submitted", await Scalar($"SELECT CONCAT_WS('|', WorkflowState, Status) FROM CFP.Inflow WHERE ID = {first.ID}"));
        Assert.Equal(1, await Scalar($"SELECT COUNT(*) FROM CFP.AuditLog WHERE ReferenceID = {first.ID} AND LogMessage LIKE '%WorkflowSync%'"));
        Assert.True(loaded.Controls!.Single(x => x.Name == "CashReceipt").Visible); // the rules use the engine state
        Assert.Contains(_logs.Entries, x => x.Contains("Atlas.WorkflowSync", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_empty_template_value_or_no_RequestId_refuses_the_submit_with_no_write()
    {
        object? before = await Scalar("SELECT COUNT(*) FROM CFP.Inflow");

        CPAtlasPage emptyName = Svc().SavePage(NewInput("700400"), Files(("FlowAttachments#$#a.pdf", Pdf))); // no account name
        CPAtlasPage noRequestId = NewInput("700100");
        noRequestId.RequestId = null;
        CPAtlasPage refused = Svc().SavePage(noRequestId, Files(("FlowAttachments#$#a.pdf", Pdf)));

        Assert.Equal(0, emptyName.ID);
        CPPageError error = Assert.Single(emptyName.Errors!);
        Assert.Equal(("AccountName", "Account Name is necessary for the notification."), (error.Field, error.Message));
        Assert.Equal("RequestIdRequired", Assert.Single(refused.Errors!).Code);
        Assert.Equal(before, await Scalar("SELECT COUNT(*) FROM CFP.Inflow"));
    }

    private ConfigSectionService Svc() =>
        new(_harness.ModuleServices(_registry, Writer, [CfpRoles.Write with { }], _logs)) { AppCode = App };

    private Task<object?> Scalar(string sql) => database.ScalarAsync(sql);

    private Task<object?> SetTransitionAsync(string action, bool active) => database.ScalarAsync($"""
        UPDATE t SET IsActive = {(active ? 1 : 0)} FROM dbo.WorkflowTransitionDefinition AS t
        INNER JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = t.WorkflowDefinitionVersionId
        INNER JOIN dbo.WorkflowDefinition AS d ON d.Id = v.WorkflowDefinitionId
        WHERE d.WorkflowKey = 'ClientInflow' AND t.ActionKey = '{action}'; SELECT @@ROWCOUNT;
        """);

    private static void AssertSaved(CPAtlasPage result) =>
        Assert.True(result.ID > 0 && result.Errors is null or { Count: 0 }, string.Join("; ", (result.Errors ?? []).Select(x => $"{x.Field}: {x.Code}: {x.Message}")));

    private static IFormFileCollection Files(params (string Part, byte[] Content)[] files)
    {
        FormFileCollection collection = new();
        foreach ((string part, byte[] content) in files)
            collection.Add(new FormFile(new MemoryStream(content), 0, content.Length, "files", part));
        return collection;
    }

    private static CPAtlasPage EditInput(int id, string? version, params (string Name, string? Value)[] values) => new()
    {
        AppCode = App, ID = id, Version = version, RequestId = Guid.NewGuid().ToString(),
        Outputs = [.. values.Select(x => new CPPageOutput { Name = x.Name, Value = x.Value })]
    };

    private static CPAtlasPage NewInput(string account) => new()
    {
        AppCode = App,
        ID = 0,
        RequestId = Guid.NewGuid().ToString(),
        Outputs =
        [
            new() { Name = "AccountNumber", Value = account },
            new() { Name = "InvestmentType", Value = "Equity" },
            new() { Name = "Amount", Value = "1250000.50" },
            new() { Name = "InflowDate", Value = "2026-10-20" },
            new() { Name = "FlowConfirm", Value = "true" }
        ]
    };

    /// <summary>Keeps the text of each log entry of the platform loggers.</summary>
    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<string> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(Entries);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class Logger(List<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Add(formatter(state, exception));
        }
    }
}
