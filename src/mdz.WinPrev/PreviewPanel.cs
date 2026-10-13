using System.Windows.Forms;

namespace MDZip.WinPrev;

/// <summary>
/// A Windows Forms panel that displays HTML content using the built-in
/// <see cref="WebBrowser"/> control.
/// </summary>
internal sealed class PreviewPanel : UserControl
{
    private readonly WebBrowser? _browser;
    private readonly RichTextBox? _fallbackText;

    public PreviewPanel()
    {
        try
        {
            _browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled = false,
                AllowNavigation = false,
                AllowWebBrowserDrop = false,
                ScrollBarsEnabled = true,
            };

            Controls.Add(_browser);
        }
        catch
        {
            _fallbackText = CreateFallbackTextBox();
            Controls.Add(_fallbackText);
        }

        Dock = DockStyle.Fill;
    }

    private static RichTextBox CreateFallbackTextBox()
    {
        return new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            DetectUrls = true,
            BackColor = System.Drawing.Color.White,
            ForeColor = System.Drawing.Color.FromArgb(26, 26, 26),
            Font = new System.Drawing.Font("Segoe UI", 10F),
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };
    }

    /// <summary>
    /// Displays rendered document HTML. With <paramref name="folder"/> (the preview's
    /// temp folder), the HTML is written there and shown in a sandboxed iframe, so the
    /// browser never runs scripts from the document (MDZip spec 13.2). The WebBrowser
    /// control runs them otherwise, and its download-control ambient property is not
    /// reachable from .NET's WebBrowserSite. Both pages are files so they share a
    /// security zone; an iframe from a file inside an about:blank page doesn't load.
    /// </summary>
    public void ShowHtml(string html, string? folder = null)
    {
        if (_browser is not null)
        {
            _browser.AllowNavigation = true;
            if (folder is null)
            {
                _browser.DocumentText = html;
            }
            else
            {
                var stem = "~mdz-preview-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(Path.Combine(folder, stem + "-document.html"), html, System.Text.Encoding.UTF8);
                var page = Path.Combine(folder, stem + ".html");
                File.WriteAllText(page, SandboxPage(stem + "-document.html"), System.Text.Encoding.UTF8);
                _browser.Navigate(new Uri(page));
            }
            _browser.AllowNavigation = false;
            return;
        }

        if (_fallbackText is null)
            return;

        _fallbackText.Text = HtmlToReadableText(html);
        _fallbackText.SelectionStart = 0;
        _fallbackText.SelectionLength = 0;
    }

    /// <summary>
    /// The outer page: a full-size iframe whose sandbox omits allow-scripts (and forms,
    /// popups and top navigation). allow-same-origin lets the document load its images
    /// from the same folder. IE=edge is needed for the sandbox attribute (IE 10+).
    /// </summary>
    internal static string SandboxPage(string documentFileName) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
        "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
        "<style>html,body,iframe{margin:0;padding:0;border:0;width:100%;height:100%;overflow:hidden;display:block}</style>" +
        "</head><body>" +
        $"<iframe sandbox=\"allow-same-origin\" src=\"{System.Net.WebUtility.HtmlEncode(documentFileName)}\"></iframe>" +
        "</body></html>";

    /// <summary>Displays a plain-text error or informational message.</summary>
    public void ShowMessage(string heading, string detail)
    {
        var html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>" +
            "body { font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif; padding: 24px; color: #1a1a1a; background: #fff; }" +
            "h2 { color: #b91c1c; margin-bottom: 8px; }" +
            "p { color: #374151; line-height: 1.6; }" +
            "</style></head><body>" +
            $"<h2>{heading}</h2><p>{detail}</p>" +
            "</body></html>";

        ShowHtml(html);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _browser?.Dispose();
            _fallbackText?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static string HtmlToReadableText(string html)
    {
        var text = html
            .Replace("\r", string.Empty)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h1>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h2>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</h3>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<li>", "- ", StringComparison.OrdinalIgnoreCase)
            .Replace("</tr>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</td>", "\t", StringComparison.OrdinalIgnoreCase)
            .Replace("</th>", "\t", StringComparison.OrdinalIgnoreCase);

        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            "<head>.*?</head>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            "<[^>]+>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        text = System.Net.WebUtility.HtmlDecode(text);
        return System.Text.RegularExpressions.Regex.Replace(text.Trim(), "\n{3,}", "\n\n");
    }
}
