using System.Windows.Forms;

namespace MDZip.WinPrev;

/// <summary>
/// A Windows Forms panel that displays HTML content using the built-in
/// <see cref="WebBrowser"/> control.
/// </summary>
internal sealed class PreviewPanel : UserControl
{
    private readonly WebBrowser _browser;

    public PreviewPanel()
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
        Dock = DockStyle.Fill;
    }

    /// <summary>Displays the given HTML string in the browser control.</summary>
    public void ShowHtml(string html)
    {
        _browser.AllowNavigation = true;
        _browser.DocumentText = html;
        _browser.AllowNavigation = false;
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
            _browser.Dispose();
        base.Dispose(disposing);
    }
}
