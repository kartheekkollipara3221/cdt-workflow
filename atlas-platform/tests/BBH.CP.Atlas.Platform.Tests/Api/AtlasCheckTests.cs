using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.App.StandIn;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BBH.CP.Atlas.Platform.Tests.Api;

/// <summary>Spec 12, section 8: --atlas-check returns 0 for TTDV2 and 1 for a manifest with no registration line.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class AtlasCheckTests(SqlServerDatabaseFixture database) : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "atlas-check-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly string RealConfig = Path.Combine(TestPaths.AtlasPlatformRoot(), "src", "BBH.CP.Atlas.App", "Config");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_TTDV2_files_pass_with_exit_code_0()
    {
        (int code, string report) = await CheckAsync(RealConfig);

        Assert.True(code == 0, report);
        Assert.Contains("0 error(s)", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_name_report_gives_Info_lines_that_are_not_counted()
    {
        // The test database has the local AIN sample accounts and no users with those names.
        (int code, string report) = await CheckAsync(RealConfig);

        Assert.True(code == 0, report);
        Assert.Contains("0 error(s), 0 warning(s).", report, StringComparison.Ordinal);
        Assert.Contains("INFO CFPINFLOW: AIN.AccountInventory AccountNumber 700100: RelationshipManager \"Riley Manager\": no user", report, StringComparison.Ordinal);
        Assert.DoesNotContain("700500", report, StringComparison.Ordinal); // a deleted AIN row is not checked
    }

    [Theory]
    [InlineData("\"RMCC\": \"RMCC\"", "\"NoColumn\": \"RMCC\"", "names 'NoColumn', which is not a control or field of the form, or a column of the table.")]
    [InlineData("\"RMCC\": \"RMCC\"", "\"CreatedBy\": \"RMCC\"", "names the system column 'CreatedBy'")]
    [InlineData("\"LookupTables\": [ \"AIN.AccountInventory\" ]", "\"LookupTables\": [ ]", "is not in the LookupTables of the manifest.")]
    [InlineData("\"Data\": \"AccountName,", "\"Data\": \"NoSuchColumn,AccountName,", "Data names 'NoSuchColumn', which is not a column of AIN.AccountInventory.")]
    public async Task A_bad_data_only_target_or_options_source_is_an_error(string oldText, string newText, string message)
    {
        CopyRealConfig();
        string file = Path.Combine(_folder, oldText.Contains("LookupTables", StringComparison.Ordinal) ? "CFPINFLOW.app.json" : "CFPINFLOW.json");
        string text = File.ReadAllText(file);
        Assert.Contains(oldText, text, StringComparison.Ordinal);
        File.WriteAllText(file, text.Replace(oldText, newText, StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR CFPINFLOW:", report, StringComparison.Ordinal);
        Assert.Contains(message, report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"Action\": { \"Type\": \"Save\" },", "\"Action\": { \"Type\": \"Close\" },",
        "the form has Workflow Steps and 0 buttons with the action Save; a form with steps has exactly one submit button.")]
    [InlineData("\"Action\": { \"Type\": \"Close\" }", "\"Action\": { \"Type\": \"Save\" }",
        "the form has Workflow Steps and 2 buttons with the action Save")]
    [InlineData("\"Draft\": {", "\"Drafted\": {", "the Workflow step 'Drafted' is not a key of StatusLabels.")]
    [InlineData("\"BusinessObjectPrefix\": \"CFP-INFLOW-\",", "", "the Workflow has Steps but no BusinessObjectPrefix.")]
    [InlineData("{ \"Start\": \"Submit\",", "{ \"Execute\": \"Submit\",",
        "the Workflow step 'Draft': engine action 1 must be a Start (the first action of the InitialState step).")]
    [InlineData("\"Recipients\": [ \"icg\" ]", "\"Recipients\": [ \"nobody\" ]",
        "the Workflow step 'Draft': the recipient key 'nobody' has no definition in Workflow.Recipients.")]
    [InlineData("\"Equity\": [ \"Equity\" ],", "\"Bonds\": [ \"Equity\" ],",
        "the Workflow recipient 'investmentLists': the Map value 'Bonds' is not an option of 'InvestmentType'.")]
    [InlineData("\"amount\": { \"Field\": \"Amount\",", "\"amount\": { \"Field\": \"Amt\",",
        "the Workflow template value 'amount' names the field 'Amt', which is not a field of the form or a column of the table.")]
    [InlineData("\"imrg\": { \"List\": \"Imrg\" },", "\"imrg\": { \"List\": \"NoSuchList\" },",
        "the Workflow list 'NoSuchList' has no address: set AppSettings:Modules:CFPINFLOW:Workflow:Lists:NoSuchList.")]
    [InlineData("\"InitialState\": \"Draft\",", "\"InitialState\": \"New\",", "the Workflow InitialState 'New' is not a key of StatusLabels.")]
    [InlineData("\"StateField\": \"WorkflowState\",", "\"StateField\": \"State\",", "the Workflow StateField 'State' is not a column of CFP.Inflow.")]
    [InlineData("\"StateField\": \"WorkflowState\",", "", "the Workflow has Steps but no StateField.")]
    [InlineData("\"CashSubmittedDate\": \"{Now}\"", "\"NoColumn\": \"{Now}\"",
        "the Set of the Workflow step 'Submitted' names 'NoColumn', which is not a control or field of the form, or a column of the table.")]
    [InlineData("\"CashSubmittedDate\": \"{Now}\"", "\"ModifiedDate\": \"{Now}\"",
        "the Set of the Workflow step 'Submitted' names the system column 'ModifiedDate', which only the server writes.")]
    [InlineData("\"CashSubmittedDate\": \"{Now}\"", "\"CashSubmittedDate\": \"{Later}\"",
        "the Set of the Workflow step 'Submitted' has the token '{Later}' for 'CashSubmittedDate', which is not known")]
    public async Task A_form_with_steps_and_a_bad_binding_or_not_one_submit_button_is_an_error(string oldText, string newText, string message)
    {
        CopyRealConfig();
        string file = Path.Combine(_folder, "CFPINFLOW.json");
        string text = File.ReadAllText(file);
        Assert.Contains(oldText, text, StringComparison.Ordinal);
        File.WriteAllText(file, text.Replace(oldText, newText, StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR CFPINFLOW: " + message, report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CFPINFLOW.app.json", "\"RecordId\": \"InflowId\"", "\"RecordId\": \"Nope\"",
        "the attachment table CFP.InflowAttachment has no column 'Nope' (RecordId).")]
    [InlineData("CFPINFLOW.app.json", "\"ControlName\": \"Type\"", "\"ControlName\": \"SizeBytes\"",
        "the column 'SizeBytes' (ControlName) of CFP.InflowAttachment must be a text column (it is bigint).")]
    [InlineData("CFPINFLOW.app.json", "\"AttachmentTable\": \"CFP.InflowAttachment\",", "\"AttachmentTable\": null,",
        "the file picker 'FlowAttachments' needs an AttachmentTable in the manifest.")]
    [InlineData("CFPINFLOW.json", "\"Name\": \"FlowAttachments\",", "\"Name\": \"FlowAttachments\", \"Accept\": [ \".pdf\", \".exe\" ],",
        "the Accept of 'FlowAttachments' has '.exe', which is not in the platform list")]
    [InlineData("CFPINFLOW.json", "\"Name\": \"FlowAttachments\",", "\"Name\": \"FlowAttachments\", \"MaxSizeMb\": 20,",
        "the MaxSizeMb of 'FlowAttachments' must be 1 to 10.")]
    [InlineData("CFPINFLOW.json", "\"Name\": \"FlowAttachments\",", "\"Name\": \"FlowAttachments\", \"MaxFiles\": 0,",
        "the MaxFiles of 'FlowAttachments' must be more than 0.")]
    public async Task A_bad_attachment_table_or_file_picker_limit_is_an_error(string fileName, string oldText, string newText, string message)
    {
        CopyRealConfig();
        string file = Path.Combine(_folder, fileName);
        string text = File.ReadAllText(file);
        Assert.Contains(oldText, text, StringComparison.Ordinal);
        File.WriteAllText(file, text.Replace(oldText, newText, StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR CFPINFLOW: " + message, report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manifest_with_no_registration_line_gives_exit_code_1()
    {
        CopyRealConfig();
        File.WriteAllText(Path.Combine(_folder, "PBSP.app.json"), """{ "Code": "PBSP", "Form": "TTDV2.json", "Table": "TTDV2.General" }""");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR PBSP: has a manifest but no registration line", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_form_field_or_tab_column_that_is_not_in_the_table_is_an_error()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Field\": \"Category\"", "\"Field\": \"Kategory\"", StringComparison.Ordinal));
        string dashboard = Path.Combine(_folder, "TTDV2.Dashboard.json");
        File.WriteAllText(dashboard, File.ReadAllText(dashboard).Replace("\"Name\": \"AccountName\"", "\"Name\": \"ClientName\"", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("names the column 'Kategory'", report, StringComparison.Ordinal);
        Assert.Contains("names the column 'ClientName'", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_HeaderFromDate_that_names_no_control_of_the_form_is_an_error()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Control\": \"WeekEnding\", \"AddDays\": -6", "\"Control\": \"WeekEnd\", \"AddDays\": -6", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("the HeaderFromDate of the column 'MondayMinute' names 'WeekEnd'", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_rule_role_is_a_warning_only()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Delete\": [ \"Owner\" ]", "\"Delete\": [ \"Owner\", \"Auditor\" ]", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.True(code == 0, report);
        Assert.Contains("WARNING TTDV2: the role 'Auditor'", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_form_without_a_Permissions_block_is_an_error()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Permissions\":", "\"PermissionsOff\":", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR TTDV2: the form has no Permissions block", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Permissions_block_with_no_Create_Edit_or_Delete_key_is_an_error()
    {
        // Review 3 RV-05: a missing key allows the operation for each role of the app.
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form)
            .Replace("\"Delete\": [ \"Owner\" ],", "", StringComparison.Ordinal)
            .Replace("\"Create\": [ \"Prepare\", \"Owner\" ],", "", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR TTDV2: the Permissions block has no Create key", report, StringComparison.Ordinal);
        Assert.Contains("ERROR TTDV2: the Permissions block has no Delete key", report, StringComparison.Ordinal);
        Assert.DoesNotContain("has no Edit key", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_table_name_with_no_schema_is_an_error_of_its_app_not_a_crash()
    {
        // Review 3 RV-06: before, ParseFullName threw out of the load, so all apps were disabled and the check crashed.
        CopyRealConfig();
        string manifest = Path.Combine(_folder, "TTDV2.app.json");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"TTDV2.AuditLog\"", "\"AuditLog\"", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR TTDV2: AuditTable 'AuditLog' must have the form Schema.Name", report, StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR *:", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_setting_that_is_not_valid_is_an_error()
    {
        (int code, string report) = await CheckAsync(RealConfig, new() { ["AppSettings:Modules:TTDV2:AccessMode"] = "Enfroce" });

        Assert.Equal(1, code);
        Assert.Contains("ERROR TTDV2: the setting AppSettings:Modules:TTDV2:AccessMode = 'Enfroce' is not valid", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_table_with_an_unsupported_column_or_key_type_is_an_error()
    {
        string name = "BadTypes" + Guid.NewGuid().ToString("N")[..6];
        await database.ScalarAsync($"CREATE TABLE TTDV2.{name} (ID bigint IDENTITY PRIMARY KEY, Notes xml NULL, Shift time NULL);");
        try
        {
            CopyRealConfig();
            string manifest = Path.Combine(_folder, "TTDV2.app.json");
            File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"TTDV2.AuditLog\"", $"\"TTDV2.{name}\"", StringComparison.Ordinal));

            (int code, string report) = await CheckAsync(_folder);

            Assert.Equal(1, code);
            Assert.Contains($"the ID column must be an int IDENTITY column (it is bigint)", report, StringComparison.Ordinal);
            Assert.Contains("the column 'Notes' has the type 'xml'", report, StringComparison.Ordinal);
            Assert.Contains("the column 'Shift' has the type 'time'", report, StringComparison.Ordinal);
        }
        finally
        {
            await database.ScalarAsync($"DROP TABLE TTDV2.{name};");
        }
    }

    [Fact]
    public async Task An_AuditTable_with_the_old_RefID_and_RefType_names_is_an_error()
    {
        string name = "OldAudit" + Guid.NewGuid().ToString("N")[..6];
        await database.ScalarAsync($"CREATE TABLE TTDV2.{name} (ID int IDENTITY PRIMARY KEY, RefID int NULL, RefType varchar(50) NULL, CreatedBy nvarchar(50) NULL, CreatedDate datetime NULL, LogMessage nvarchar(max) NULL);");
        try
        {
            CopyRealConfig();
            string manifest = Path.Combine(_folder, "TTDV2.app.json");
            File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"TTDV2.AuditLog\"", $"\"TTDV2.{name}\"", StringComparison.Ordinal));

            (int code, string report) = await CheckAsync(_folder);

            Assert.Equal(1, code);
            Assert.Contains($"AuditTable 'TTDV2.{name}' has no ReferenceID column. The table has the old names RefID and RefType", report, StringComparison.Ordinal);
            Assert.Contains($"AuditTable 'TTDV2.{name}' has no ReferenceType column.", report, StringComparison.Ordinal);
        }
        finally
        {
            await database.ScalarAsync($"DROP TABLE TTDV2.{name};");
        }
    }

    [Fact]
    public async Task A_manifest_file_that_cannot_be_read_is_an_error_and_the_other_apps_are_still_checked()
    {
        CopyRealConfig();
        File.WriteAllText(Path.Combine(_folder, "BROKEN.app.json"), """{ "Code": """);

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR *: BROKEN.app.json cannot be read", report, StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR TTDV2", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_control_type_with_no_client_renderer_is_a_warning()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Type\": \"textarea\"", "\"Type\": \"signaturepad\"", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.True(code == 0, report);
        Assert.Contains("WARNING TTDV2: the control 'WorkDescription' has the type 'signaturepad'", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_misspelled_property_is_a_warning_with_its_path()
    {
        CopyRealConfig();
        ReplaceInForm("\"Validator\": { \"Required\": true }", "\"Validator\": { \"Requried\": true }");

        (int code, string report) = await CheckAsync(_folder);

        Assert.True(code == 0, report);
        Assert.Contains("WARNING TTDV2: TTDV2.json: 'Section.Controls[", report, StringComparison.Ordinal);
        Assert.Contains(".Validator.Requried' is not a property of CPValidator", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_effect_target_that_is_not_a_control_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"Clear\": [ \"AccountNumber\", \"AccountName\" ]", "\"Clear\": [ \"AccountNumber\", \"AcountName\" ]");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("names 'AcountName', which is not a control or field of the form", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Set_or_Clear_effect_cycle_is_an_error()
    {
        // Review 3 RV-15: a control that clears itself would run its effects again and again in the client.
        CopyRealConfig();
        ReplaceInForm("\"Clear\": [ \"AccountNumber\", \"AccountName\" ]", "\"Clear\": [ \"AccountNumber\", \"AccountName\", \"Account\" ]");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("the effects of 'Account' change a control whose effects change 'Account' again", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dashboard_tab_with_a_Procedure_is_an_error_until_the_platform_runs_procedures()
    {
        // Review 3 (RV-18 follow-up): the platform dashboard gives rows only for Source: Table tabs.
        CopyRealConfig();
        string dashboard = Path.Combine(_folder, "TTDV2.Dashboard.json");
        string text = File.ReadAllText(dashboard);
        int at = text.IndexOf("\"Source\": \"Table\"", StringComparison.Ordinal);
        Assert.True(at > 0);
        File.WriteAllText(dashboard, string.Concat(text.AsSpan(0, at), "\"Procedure\": \"TTDV2.Get_Current\"", text.AsSpan(at + "\"Source\": \"Table\"".Length)));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("has a Procedure; the platform dashboard runs only Source: Table tabs", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Computed_field_over_a_text_column_is_an_error()
    {
        // Review 3 RV-L02: before, each save of the app gave a 500.
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        string text = File.ReadAllText(form);
        int at = text.IndexOf("MondayMinute", text.IndexOf("\"Computed\"", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.True(at > 0);
        File.WriteAllText(form, string.Concat(text.AsSpan(0, at), "AccountName", text.AsSpan(at + "MondayMinute".Length)));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("uses 'AccountName', which is not a number column", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_grid_cell_code_of_another_app_and_mixed_time_settings_are_warnings()
    {
        // Review 3 RV-L12 and RV-L04.
        CopyRealConfig();
        ReplaceInForm("\"code\": \"TTDV2\", \"ctrlname\": \"Monday\"", "\"code\": \"TTD\", \"ctrlname\": \"Monday\"");

        string manifest = Path.Combine(_folder, "TTDV2.app.json");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"Code\": \"TTDV2\",", "\"Code\": \"TTDV2\", \"SystemTime\": \"Local\",", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(0, code);
        Assert.Contains("column 'MondayMinute' has cellRendererParams.code 'TTD', not 'TTDV2'", report, StringComparison.Ordinal);
        Assert.Contains("SystemTime is Local but AuditTime is Utc", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_default_token_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"TaxPreparer\": \"{User.LanID}\"", "\"TaxPreparer\": \"{Todya}\"");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("the default token '{Todya}' of 'TaxPreparer' is not known", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pattern_that_does_not_compile_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"Validator\": { \"Required\": true }", "\"Validator\": { \"Required\": true, \"Pattern\": \"[0-9\" }");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("is not a valid regular expression", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_effect_On_value_other_than_Change_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"On\": \"Change\"", "\"On\": \"Load\"");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("an effect of 'WeekEnding' has On 'Load'", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_newer_SchemaVersion_is_an_error()
    {
        CopyRealConfig();
        string manifest = Path.Combine(_folder, "TTDV2.app.json");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("{", "{ \"SchemaVersion\": 2,", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("the manifest SchemaVersion 2 is newer than this platform", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_SoftDelete_field_other_than_IsDeleted_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"SoftDelete\": { \"Field\": \"IsDeleted\" }", "\"SoftDelete\": { \"Field\": \"Category\" }");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("SoftDelete names 'Category'; the platform reads only IsDeleted", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_not_null_bit_column_with_a_default_that_nothing_sets_is_a_warning()
    {
        string column = "Flag" + Guid.NewGuid().ToString("N")[..6];
        await database.ScalarAsync($"ALTER TABLE TTDV2.General ADD {column} bit NOT NULL CONSTRAINT DF_{column} DEFAULT 1;");
        try
        {
            (int code, string report) = await CheckAsync(RealConfig);

            Assert.True(code == 0, report);
            Assert.Contains($"WARNING TTDV2: the column '{column}' of TTDV2.General is NOT NULL bit with a default", report, StringComparison.Ordinal);
        }
        finally
        {
            await database.ScalarAsync($"ALTER TABLE TTDV2.General DROP CONSTRAINT DF_{column}; ALTER TABLE TTDV2.General DROP COLUMN {column};");
        }
    }

    [Fact]
    public async Task A_shared_AuditTable_without_AuditRefTypes_is_a_warning()
    {
        CopyRealConfig();
        File.WriteAllText(Path.Combine(_folder, "PBSP.app.json"),
            """{ "Code": "PBSP", "Form": "TTDV2.json", "Table": "TTDV2.General", "AuditTable": "TTDV2.AuditLog", "AuditRefTypes": [ "PBSP" ] }""");

        (_, string report) = await CheckAsync(_folder);

        Assert.Contains("WARNING TTDV2: the AuditTable TTDV2.AuditLog is shared with PBSP", report, StringComparison.Ordinal);
        Assert.DoesNotContain("WARNING PBSP: the AuditTable", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_host_loads_the_platform_when_program_cs_does_not_call_StartAtlasPlatformAsync()
    {
        // Review HO-06: the hosted service of AddAtlasPlatform runs the start-up; a second call does nothing.
        await using WebApplication app = StandInHost.Create(["--environment=Development", "--urls=http://127.0.0.1:0"], builder =>
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DBConnection"] = database.ConnectionString,
                ["AppSettings:ConfigFolder"] = RealConfig
            }));
        AtlasTableRegistry registry = app.Services.GetRequiredService<AtlasTableRegistry>();
        Assert.False(registry.IsLoaded);

        await app.StartAsync();
        try
        {
            Assert.True(registry.IsLoaded);
            Assert.True(registry.TryGet("TTDV2.General", out _));
            await app.Services.StartAtlasPlatformAsync(); // the program.cs line of INTEGRATION.md stays valid
            Assert.Same(registry, app.Services.GetRequiredService<AtlasTableRegistry>());
            Assert.True(registry.TryGet("TTDV2.General", out _));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Stop")]
    [InlineData("DisableModule")]
    [InlineData("Off")]
    public async Task OnStartupError_decides_if_one_bad_app_stops_the_host(string? mode)
    {
        // Review HO-04 and AR-01: DisableModule is the default and starts the host without the bad app, so the current
        // apps in the same host keep working. Only an explicit Stop keeps spec 07, 4.8. A value that is not valid also
        // starts the host and gives an error for all platform apps.
        CopyRealConfig();
        ReplaceInForm("\"Field\": \"Category\"", "\"Field\": \"Kategory\"");
        await using WebApplication app = StandInHost.Create(["--environment=Development", "--urls=http://127.0.0.1:0"], builder =>
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DBConnection"] = database.ConnectionString,
                ["AppSettings:ConfigFolder"] = _folder,
                [AtlasPlatformStartup.OnStartupErrorKey] = mode
            }));

        if (mode == "Stop")
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
            Assert.Contains("names the column 'Kategory'", error.Message, StringComparison.Ordinal);
            return;
        }

        await app.StartAsync();
        try
        {
            using IServiceScope scope = app.Services.CreateScope();
            IAtlasModuleResolver resolver = scope.ServiceProvider.GetRequiredService<IAtlasModuleResolver>();
            Assert.False(resolver.IsPlatformApp("TTDV2"));
            AtlasProblemException error = Assert.Throws<AtlasProblemException>(() => resolver.Get<IAtlasModule>("TTDV2", "form"));
            Assert.Equal(404, error.StatusCode);
            Assert.Contains("the start-up check found errors", error.Message, StringComparison.Ordinal);
            Assert.True(app.Services.GetRequiredService<AtlasDisabledApps>().IsDisabled("TTDV2", out string? reason));
            if (mode == "Off")
                Assert.Contains("is not valid (allowed: DisableModule, Stop)", reason, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task A_start_up_failure_that_is_not_a_check_error_does_not_stop_the_host_by_default()
    {
        // Review AR-01: with no setting, a failure while the metadata loads (here: no database) disables the platform
        // apps; the host starts, so the current apps in the same process keep working.
        CopyRealConfig();
        await using WebApplication app = StandInHost.Create(["--environment=Development", "--urls=http://127.0.0.1:0"], builder =>
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DBConnection"] = "Server=127.0.0.1,1;Database=none;User Id=x;Password=x;Connect Timeout=1;Encrypt=False",
                ["AppSettings:ConfigFolder"] = _folder
            }));

        await app.StartAsync();
        try
        {
            using IServiceScope scope = app.Services.CreateScope();
            Assert.False(scope.ServiceProvider.GetRequiredService<IAtlasModuleResolver>().IsPlatformApp("TTDV2"));
            Assert.True(app.Services.GetRequiredService<AtlasDisabledApps>().IsDisabled("TTDV2", out _));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task A_setting_OnStartupError_that_is_not_valid_is_a_check_error()
    {
        CopyRealConfig();

        (int code, string report) = await CheckAsync(_folder, new() { [AtlasPlatformStartup.OnStartupErrorKey] = "Off" });

        Assert.Equal(1, code);
        Assert.Contains("is not valid (allowed: DisableModule, Stop)", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_current_app_taken_over_with_no_SystemTime_is_a_warning()
    {
        // Review DA-11: P4 moves current apps in place; their rows have local times.
        CopyRealConfig();
        File.WriteAllText(Path.Combine(_folder, "TTD.app.json"), """{ "Code": "TTD", "Form": "TTDV2.json", "Table": "TTDV2.General" }""");

        (_, string report) = await CheckAsync(_folder);

        Assert.Contains("WARNING TTD: is a current app taken over by the platform but the manifest has no SystemTime", report, StringComparison.Ordinal);
        Assert.DoesNotContain("WARNING TTDV2: is a current app", report, StringComparison.Ordinal);
    }

    private void ReplaceInForm(string oldText, string newText)
    {
        string form = Path.Combine(_folder, "TTDV2.json");
        string text = File.ReadAllText(form);
        Assert.Contains(oldText, text, StringComparison.Ordinal);
        File.WriteAllText(form, text.Replace(oldText, newText, StringComparison.Ordinal));
    }

    private void CopyRealConfig()
    {
        Directory.CreateDirectory(_folder);
        foreach (string file in Directory.GetFiles(RealConfig, "*.json"))
            File.Copy(file, Path.Combine(_folder, Path.GetFileName(file)));
    }

    private async Task<(int Code, string Report)> CheckAsync(string configFolder, Dictionary<string, string?>? settings = null)
    {
        Dictionary<string, string?> values = new(settings ?? [])
        {
            ["ConnectionStrings:DBConnection"] = database.ConnectionString,
            ["AppSettings:ConfigFolder"] = configFolder
        };
        await using WebApplication app = StandInHost.Create(["--environment=Development"], builder =>
            builder.Configuration.AddInMemoryCollection(values));
        using StringWriter output = new();
        int code = await app.Services.RunAtlasCheckAsync(output);
        return (code, output.ToString());
    }
}
