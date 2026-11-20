using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using BBH.CP.Atlas.Data.DTO;
using Ganss.Xss;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>
/// Cleans the HTML of a rich text control and gives its plain text.
/// </summary>
/// <remarks>
/// The allow list is one constant of the platform, so an app JSON cannot make it weaker. It keeps the text formats,
/// headings, lists, links, block quotes and tables of the editor. It removes pictures, styles, scripts and event
/// attributes. A link can use only http, https or mailto, and gets rel="noopener noreferrer".
/// </remarks>
public static partial class AtlasRichText
{
    /// <summary>The control type of a rich text control.</summary>
    public const string ControlType = "richtext";

    /// <summary>The largest value after the clean-up, in characters.</summary>
    public const int MaxLength = 200_000;

    private static readonly string[] Tags =
        ["p", "br", "strong", "b", "em", "i", "u", "s", "h2", "h3", "h4", "ul", "ol", "li", "blockquote", "a",
         "figure", "table", "thead", "tbody", "tr", "th", "td"];

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "h2", "h3", "h4", "ul", "ol", "li", "blockquote", "figure", "table", "thead", "tbody", "tr"
    };

    private static readonly HashSet<string> DropWithContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "template", "noscript", "iframe", "object", "embed", "svg", "math", "textarea", "select", "title", "head"
    };

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    /// <summary>Returns true for a rich text control.</summary>
    public static bool IsRichText(CPApplicationControl? control) =>
        string.Equals(control?.Type, ControlType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the HTML with only the allowed tags, attributes and link schemes.
    /// </summary>
    /// <returns>Null when the value is empty, or when it has no text and no table after the clean-up.</returns>
    public static string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        string clean = Sanitizer.Sanitize(html).Trim();
        bool hasTable = clean.Contains("<table", StringComparison.OrdinalIgnoreCase);
        return clean.Length == 0 || (!hasTable && ToPlainText(clean) is null) ? null : clean;
    }

    /// <summary>
    /// Returns the text of the HTML with no tags and with the entities decoded. Each block starts a new line.
    /// </summary>
    /// <returns>Null for a value with no text.</returns>
    public static string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        IElement? body = new HtmlParser().ParseDocument("<body>" + html + "</body>").Body;
        if (body == null)
            return null;

        StringBuilder text = new();
        Append(body, text);
        string[] lines = [.. text.ToString().Split('\n')
            .Select(x => Spaces().Replace(x, " ").Trim())
            .Where(x => x.Length > 0)];
        return lines.Length == 0 ? null : string.Join("\n", lines);
    }

    private static void Append(INode node, StringBuilder text)
    {
        foreach (INode child in node.ChildNodes)
        {
            if (child.NodeType == NodeType.Text)
            {
                text.Append(child.TextContent);
                continue;
            }

            if (child is not IElement element)
                continue;

            bool block = BlockTags.Contains(element.LocalName);
            if (block)
                text.Append('\n');
            Append(element, text);
            if (element.LocalName is "td" or "th")
                text.Append(' ');
            if (block)
                text.Append('\n');
        }
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        HtmlSanitizer sanitizer = new(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(Tags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(["href", "colspan", "rowspan", "class"], StringComparer.OrdinalIgnoreCase),
            AllowedClasses = new HashSet<string>(["table"], StringComparer.OrdinalIgnoreCase),
            AllowedSchemes = new HashSet<string>(["http", "https", "mailto"], StringComparer.OrdinalIgnoreCase),
            UriAttributes = new HashSet<string>(["href"], StringComparer.OrdinalIgnoreCase),
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            AllowedAtRules = new HashSet<AngleSharp.Css.Dom.CssRuleType>()
        });
        sanitizer.KeepChildNodes = true; // the text of a removed tag stays, for example of a <span>
        sanitizer.RemovingTag += (_, e) =>
        {
            // The content of these tags is code or a second document, not text. It goes with the tag.
            if (DropWithContent.Contains(e.Tag.LocalName))
                e.Tag.InnerHtml = string.Empty;
        };
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element)
                return;
            // The editor puts a table in <figure class="table">. The class is not allowed on other elements.
            if (element.LocalName != "figure" && element.HasAttribute("class"))
                element.RemoveAttribute("class");
            if (element.LocalName == "a")
                element.SetAttribute("rel", "noopener noreferrer");
        };
        return sanitizer;
    }

    [GeneratedRegex(@"[ \t\r ]+")]
    private static partial Regex Spaces();
}
