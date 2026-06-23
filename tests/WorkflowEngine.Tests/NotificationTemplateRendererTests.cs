using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Tests;

public sealed class NotificationTemplateRendererTests
{
    [Fact]
    public void RenderReplacesTokensWithContextValues()
    {
        var renderer = new NotificationTemplateRenderer();

        var result = renderer.Render(
            "Hello {{submitterDisplayName}}, account {{accountShortName}}",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["submitterDisplayName"] = "Kollipara, Kartheek",
                ["accountShortName"] = "EFG"
            },
            fieldPrefix: "workflow.notificationContext");

        Assert.True(result.Success);
        Assert.Equal("Hello Kollipara, Kartheek, account EFG", result.Rendered);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void RenderReportsMissingTokens()
    {
        var renderer = new NotificationTemplateRenderer();

        var result = renderer.Render(
            "Account {{accountShortName}} for {{deskName}}",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["accountShortName"] = "EFG"
            },
            fieldPrefix: "workflow.notificationContext");

        Assert.False(result.Success);
        Assert.Equal("Account EFG for {{deskName}}", result.Rendered);
        var error = Assert.Single(result.Errors);
        Assert.Equal("workflow.notificationContext.deskName", error.Field);
        Assert.Equal("Template token 'deskName' requires workflow.notificationContext.deskName.", error.Message);
    }

    [Fact]
    public void RenderTreatsWhitespaceValueAsMissing()
    {
        var renderer = new NotificationTemplateRenderer();

        var result = renderer.Render(
            "Sharepoint link = {{tradeLink}}",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["tradeLink"] = " "
            },
            fieldPrefix: "workflow.notificationContext");

        Assert.False(result.Success);
        Assert.Equal("workflow.notificationContext.tradeLink", Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void RenderHtmlEscapesTokenValuesAndPreservesTemplateMarkup()
    {
        var renderer = new NotificationTemplateRenderer();

        var result = renderer.RenderHtml(
            """Account <strong>{{accountDisplayName}}</strong> <a href="{{tradeLink}}">Open trade</a>""",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["accountDisplayName"] = """EFG <script>alert("x")</script>""",
                ["tradeLink"] = """https://sharepoint.example.test/trades/1050?desk=Taxable&status=open"""
            },
            fieldPrefix: "workflow.notificationContext");

        Assert.True(result.Success);
        Assert.Equal(
            """Account <strong>EFG &lt;script&gt;alert(&quot;x&quot;)&lt;/script&gt;</strong> <a href="https://sharepoint.example.test/trades/1050?desk=Taxable&amp;status=open">Open trade</a>""",
            result.Rendered);
        Assert.Empty(result.Errors);
    }
}
