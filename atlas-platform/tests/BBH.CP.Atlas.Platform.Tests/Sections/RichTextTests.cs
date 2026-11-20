using BBH.CP.Atlas.Service.Platform.Sections;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>The server clean-up of rich text (module 25, 9.2) and its plain text.</summary>
public class RichTextTests
{
    [Theory]
    [InlineData("<p>Hello <strong>world</strong></p>", "<p>Hello <strong>world</strong></p>")]
    [InlineData("<p onclick=\"alert(1)\">A</p><script>alert(2)</script>", "<p>A</p>")]
    [InlineData("<p style=\"color:red\" class=\"x\">A</p>", "<p>A</p>")]
    [InlineData("<p>A<img src=\"x.png\" onerror=\"alert(1)\"></p>", "<p>A</p>")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "<a rel=\"noopener noreferrer\">x</a>")]
    [InlineData("<a href=\"https://example.com/a\" target=\"_blank\">x</a>", "<a href=\"https://example.com/a\" rel=\"noopener noreferrer\">x</a>")]
    [InlineData("<a href=\"mailto:a@b.com\">m</a>", "<a href=\"mailto:a@b.com\" rel=\"noopener noreferrer\">m</a>")]
    [InlineData("<figure class=\"table\"><table><tbody><tr><td colspan=\"2\">c</td></tr></tbody></table></figure>",
        "<figure class=\"table\"><table><tbody><tr><td colspan=\"2\">c</td></tr></tbody></table></figure>")]
    [InlineData("<figure class=\"image\"><table><tbody><tr><td class=\"table\">c</td></tr></tbody></table></figure>",
        "<figure><table><tbody><tr><td>c</td></tr></tbody></table></figure>")]
    [InlineData("<h1>T</h1><h2>H</h2><span>kept text</span><iframe src=\"https://x\"></iframe>", "T<h2>H</h2>kept text")]
    [InlineData("<ul><li>a</li></ul><ol><li>b</li></ol><blockquote>q</blockquote><p><u>u</u><s>s</s><em>e</em><i>i</i><b>b</b><br></p>",
        "<ul><li>a</li></ul><ol><li>b</li></ol><blockquote>q</blockquote><p><u>u</u><s>s</s><em>e</em><i>i</i><b>b</b><br></p>")]
    public void Sanitize_keeps_only_the_allowed_tags_attributes_and_links(string html, string expected) =>
        Assert.Equal(expected, AtlasRichText.Sanitize(html));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("<p>&nbsp;</p>")]
    [InlineData("<p></p><p> </p>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=\"x.png\">")]
    public void A_value_with_no_text_is_null(string? html) => Assert.Null(AtlasRichText.Sanitize(html));

    [Fact]
    public void The_plain_text_has_no_tags_decodes_the_entities_and_puts_each_block_on_a_line()
    {
        string? text = AtlasRichText.ToPlainText(
            "<h2>Wire</h2><p>Amount &amp; date:&nbsp; <strong>10</strong></p><ul><li>one</li><li>two</li></ul>"
            + "<figure class=\"table\"><table><tbody><tr><td>a</td><td>b</td></tr></tbody></table></figure>");

        Assert.Equal("Wire\nAmount & date: 10\none\ntwo\na b", text);
    }

    [Fact]
    public void The_clean_up_of_a_clean_value_gives_the_same_value()
    {
        string clean = AtlasRichText.Sanitize("<p>A <a href=\"https://x.org\">link</a></p><figure class=\"table\"><table><tbody><tr><td>c</td></tr></tbody></table></figure>")!;

        Assert.Equal(clean, AtlasRichText.Sanitize(clean));
    }
}
