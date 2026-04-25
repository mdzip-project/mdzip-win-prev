using System.Runtime.InteropServices;
using System.Windows.Forms;
using MDZip.Core;
using MDZip.WinPrev.ComInterop;

namespace MDZip.WinPrev;

/// <summary>
/// COM preview handler for .mdz (MarkdownZip) files in Windows File Explorer.
/// Implements the <see cref="IPreviewHandler"/> and <see cref="IInitializeWithFile"/>
/// interfaces required by the Windows Shell preview-handler architecture.
/// </summary>
/// <remarks>
/// <para>Only <b>document mode</b> archives are supported. Archives whose manifest
/// declares <c>"mode": "project"</c> display an informational message instead of
/// rendering content.</para>
/// <para>
/// Registration is performed by the PowerShell scripts in the <c>scripts/</c>
/// directory.  The CLSID <c>{CA7A244F-7A83-4B5E-9D7A-9F13EF5E8B3A}</c> must
/// be registered under:
/// <list type="bullet">
///   <item><c>HKCR\.mdz\ShellEx\{8895b1c6-b41f-4c1c-a562-0d564250836f}</c></item>
///   <item><c>HKCR\CLSID\{CA7A244F-7A83-4B5E-9D7A-9F13EF5E8B3A}</c></item>
/// </list>
/// </para>
/// </remarks>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("CA7A244F-7A83-4B5E-9D7A-9F13EF5E8B3A")]
[ProgId("MDZip.WinPrev.MdzPreviewHandler")]
public sealed class MdzPreviewHandler :
    IPreviewHandler,
    IInitializeWithFile,
    IOleWindow,
    IPreviewHandlerVisuals
{
    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private string? _filePath;
    private IntPtr _parentHwnd;
    private RECT _previewRect;
    private PreviewPanel? _panel;
    private string? _tempDir;

    private static readonly MdzRenderer Renderer = new();

    // -------------------------------------------------------------------------
    // IInitializeWithFile
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    void IInitializeWithFile.Initialize(string pszFilePath, uint grfMode)
    {
        _filePath = pszFilePath;
    }

    // -------------------------------------------------------------------------
    // IPreviewHandler
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    void IPreviewHandler.SetWindow(IntPtr hwnd, ref RECT prc)
    {
        _parentHwnd = hwnd;
        _previewRect = prc;
        UpdatePanelBounds();
    }

    /// <inheritdoc />
    void IPreviewHandler.SetRect(ref RECT prc)
    {
        _previewRect = prc;
        UpdatePanelBounds();
    }

    /// <inheritdoc />
    void IPreviewHandler.DoPreview()
    {
        if (string.IsNullOrEmpty(_filePath))
            return;

        EnsurePanelCreated();

        try
        {
            _tempDir = ExtractAssetsToTemp(_filePath);
            var html = Renderer.Render(_filePath, _tempDir);
            _panel!.ShowHtml(html);
        }
        catch (ProjectModeNotSupportedException ex)
        {
            _panel!.ShowMessage("Project mode not supported", ex.Message);
        }
        catch (Exception ex)
        {
            _panel!.ShowMessage(
                "Preview unavailable",
                $"The file could not be previewed: {HtmlEncode(ex.Message)}");
        }
    }

    /// <inheritdoc />
    void IPreviewHandler.Unload()
    {
        DisposePanel();
        CleanupTemp();
        _filePath = null;
    }

    /// <inheritdoc />
    void IPreviewHandler.SetFocus()
    {
        _panel?.Focus();
    }

    /// <inheritdoc />
    void IPreviewHandler.QueryFocus(out IntPtr phwnd)
    {
        phwnd = _panel?.Handle ?? IntPtr.Zero;
    }

    /// <inheritdoc />
    [PreserveSig]
    uint IPreviewHandler.TranslateAccelerator(ref MSG pmsg)
    {
        // S_FALSE: message not handled — let the host handle it.
        return 1;
    }

    // -------------------------------------------------------------------------
    // IOleWindow
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    void IOleWindow.GetWindow(out IntPtr phwnd)
    {
        phwnd = _panel?.Handle ?? IntPtr.Zero;
    }

    /// <inheritdoc />
    void IOleWindow.ContextSensitiveHelp(bool fEnterMode) { /* not implemented */ }

    // -------------------------------------------------------------------------
    // IPreviewHandlerVisuals
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    void IPreviewHandlerVisuals.SetBackgroundColor(uint color) { /* not implemented */ }

    /// <inheritdoc />
    void IPreviewHandlerVisuals.SetFont(ref LOGFONT plf) { /* not implemented */ }

    /// <inheritdoc />
    void IPreviewHandlerVisuals.SetTextColor(uint color) { /* not implemented */ }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void EnsurePanelCreated()
    {
        if (_panel is not null)
            return;

        _panel = new PreviewPanel();

        if (_parentHwnd != IntPtr.Zero)
        {
            SetParent(_panel.Handle, _parentHwnd);
            UpdatePanelBounds();
            _panel.Show();
        }
    }

    private void UpdatePanelBounds()
    {
        if (_panel is null)
            return;

        _panel.SetBounds(
            _previewRect.Left,
            _previewRect.Top,
            _previewRect.Right - _previewRect.Left,
            _previewRect.Bottom - _previewRect.Top);
    }

    private void DisposePanel()
    {
        if (_panel is null)
            return;
        _panel.Dispose();
        _panel = null;
    }

    private void CleanupTemp()
    {
        if (_tempDir is null || !Directory.Exists(_tempDir))
            return;
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
        _tempDir = null;
    }

    /// <summary>
    /// Extracts all archive assets to a unique temporary directory so that
    /// relative image references in the rendered HTML resolve correctly.
    /// </summary>
    private static string ExtractAssetsToTemp(string mdzFilePath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mdz-prev-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        MdzArchive.Extract(mdzFilePath, tempDir);
        return tempDir;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    private static string HtmlEncode(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
