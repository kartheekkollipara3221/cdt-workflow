namespace BBH.CP.Atlas.Platform.Tests.Data;

/// <summary>
/// Script 150 (the ClientInflow engine metadata) on the shared database, where the fixture ran it one time after the
/// engine migrations.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class ReleaseP2WorkflowScriptTests(SqlServerDatabaseFixture database)
{
    private const string Script = "150-cfpinflow-workflow.sql";

    [Fact]
    public async Task Script_150_runs_a_second_time_with_no_change_and_one_log_row()
    {
        string before = await MetadataAsync();

        await database.RunScriptFileAsync(ReleaseP2.Path(Script));

        Assert.Equal(before, await MetadataAsync());
        Assert.Contains("\"RouteKey\":\"ClientInflow.FundsReceived\"", before, StringComparison.Ordinal);
        Assert.Equal(1, await database.ScalarAsync($"SELECT COUNT(*) FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2' AND ScriptName = N'{Script}'"));
    }

    /// <summary>Returns the content of the ClientInflow metadata as JSON, with no row IDs (the script adds the recipient rules again).</summary>
    private async Task<string> MetadataAsync() => (string)(await database.ScalarAsync("""
        DECLARE @Definition int = (SELECT Id FROM dbo.WorkflowDefinition WHERE WorkflowKey = N'ClientInflow');
        SELECT CONCAT(
            (SELECT WorkflowKey, DisplayName, BusinessObjectType, IsActive FROM dbo.WorkflowDefinition WHERE Id = @Definition FOR JSON PATH),
            (SELECT VersionNumber, IsActive FROM dbo.WorkflowDefinitionVersion WHERE WorkflowDefinitionId = @Definition ORDER BY VersionNumber FOR JSON PATH),
            (SELECT s.StateKey, s.DisplayName, s.IsTerminal, s.IsActive FROM dbo.WorkflowStateDefinition AS s
             INNER JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = s.WorkflowDefinitionVersionId
             WHERE v.WorkflowDefinitionId = @Definition ORDER BY v.VersionNumber, s.StateKey FOR JSON PATH),
            (SELECT a.ActionKey, a.DisplayName, a.SectionKey, a.ButtonKey, a.ButtonLabel, a.Intent, a.RequiresReason, a.SortOrder, a.IsActive
             FROM dbo.WorkflowActionDefinition AS a INNER JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = a.WorkflowDefinitionVersionId
             WHERE v.WorkflowDefinitionId = @Definition ORDER BY v.VersionNumber, a.ActionKey FOR JSON PATH),
            (SELECT r.RoleKey, r.DisplayName, r.IsActive FROM dbo.WorkflowRoleDefinition AS r
             INNER JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = r.WorkflowDefinitionVersionId
             WHERE v.WorkflowDefinitionId = @Definition ORDER BY v.VersionNumber, r.RoleKey FOR JSON PATH),
            (SELECT n.RouteKey, n.NotificationType, n.SubjectTemplate, n.BodyTemplate, n.HtmlBodyTemplate, n.OverdueSubjectPrefix, n.IsActive
             FROM dbo.NotificationRoute AS n INNER JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = n.WorkflowDefinitionVersionId
             WHERE v.WorkflowDefinitionId = @Definition ORDER BY v.VersionNumber, n.RouteKey FOR JSON PATH),
            (SELECT t.FromStateKey, t.ActionKey, t.ToStateKey, t.NotificationRouteKey, t.IsActive
             FROM dbo.WorkflowTransitionDefinition AS t INNER JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = t.WorkflowDefinitionVersionId
             WHERE v.WorkflowDefinitionId = @Definition ORDER BY v.VersionNumber, t.FromStateKey, t.ActionKey FOR JSON PATH),
            (SELECT n.RouteKey, x.RecipientKind, x.SourceType, x.SourceKey, x.StaticAddress, x.AssignedRoleKey, x.SortOrder, x.IsRequired, x.IsActive
             FROM dbo.NotificationRecipientRule AS x INNER JOIN dbo.NotificationRoute AS n ON n.Id = x.NotificationRouteId
             INNER JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = n.WorkflowDefinitionVersionId
             WHERE v.WorkflowDefinitionId = @Definition ORDER BY v.VersionNumber, n.RouteKey, x.SortOrder FOR JSON PATH));
        """))!;
}
