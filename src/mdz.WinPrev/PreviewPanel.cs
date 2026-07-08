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

    /// <summary>Displays the given HTML string in the browser control.</summary>
    public void ShowHtml(string html)
    {
        if (_browser is not null)
        {
            _browser.AllowNavigation = true;
            _browser.DocumentText = html;
            _browser.AllowNavigation = false;
            return;
        }

        if (_fallbackText is null)
            return;

        _fallbackText.Text = HtmlToReadableText(html);
        _fallbackText.SelectionStart = 0;
        _fallbackText.SelectionLength = 0;
    }

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
