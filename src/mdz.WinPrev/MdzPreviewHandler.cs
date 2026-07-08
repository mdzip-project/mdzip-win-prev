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
    IObjectWithSite,
    IPreviewHandlerVisuals
{
    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private string? _filePath;
    private IntPtr _parentHwnd;
    private RECT _previewRect;
    private PreviewUiThread? _uiThread;
    private IntPtr _panelHandle;
    private string? _tempDir;
    private object? _site;

    private static readonly MdzRenderer Renderer = new();

    public MdzPreviewHandler()
    {
        DebugLog("constructed");
    }

    // -------------------------------------------------------------------------
    // IInitializeWithFile
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    void IInitializeWithFile.Initialize(string pszFilePath, uint grfMode)
    {
        DebugLog($"Initialize file='{pszFilePath}' mode={grfMode}");
        _filePath = pszFilePath;
    }

    // -------------------------------------------------------------------------
    // IPreviewHandler
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    void IPreviewHandler.SetWindow(IntPtr hwnd, ref RECT prc)
    {
        DebugLog($"SetWindow hwnd={hwnd} rect={FormatRect(prc)}");
        _parentHwnd = hwnd;
        _previewRect = prc;
        UpdatePanelBounds();
    }

    /// <inheritdoc />
    void IPreviewHandler.SetRect(ref RECT prc)
    {
        DebugLog($"SetRect rect={FormatRect(prc)}");
        _previewRect = prc;
        UpdatePanelBounds();
    }

    /// <inheritdoc />
    void IPreviewHandler.DoPreview()
    {
        DebugLog($"DoPreview file='{_filePath ?? "<null>"}'");
        if (string.IsNullOrEmpty(_filePath))
            return;

        EnsurePanelCreated();

        try
        {
            _tempDir = ExtractAssetsToTemp(_filePath);
            DebugLog($"Extracted assets to '{_tempDir}'");
            var html = Renderer.Render(_filePath, _tempDir);
            DebugLog($"Rendered HTML length={html.Length}");
            _uiThread!.ShowHtml(html);
            DebugLog("ShowHtml completed");
        }
        catch (ProjectModeNotSupportedException ex)
        {
            DebugLog("ProjectModeNotSupportedException: " + ex);
            _uiThread!.ShowMessage("Project mode not supported", ex.Message);
        }
        catch (Exception ex)
        {
            DebugLog("Exception: " + ex);
            _uiThread!.ShowMessage(
                "Preview unavailable",
                $"The file could not be previewed: {HtmlEncode(ex.Message)}");
        }
    }

    /// <inheritdoc />
    void IPreviewHandler.Unload()
    {
        DebugLog("Unload");
        DisposePanel();
        CleanupTemp();
        _filePath = null;
    }

    /// <inheritdoc />
    void IPreviewHandler.SetFocus()
    {
        DebugLog("SetFocus");
        _uiThread?.Focus();
    }

    /// <inheritdoc />
    void IPreviewHandler.QueryFocus(out IntPtr phwnd)
    {
        phwnd = _panelHandle;
        DebugLog($"QueryFocus hwnd={phwnd}");
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
        phwnd = _panelHandle;
        DebugLog($"GetWindow hwnd={phwnd}");
    }

    /// <inheritdoc />
    void IOleWindow.ContextSensitiveHelp(bool fEnterMode) { /* not implemented */ }

    // -------------------------------------------------------------------------
    // IObjectWithSite
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    void IObjectWithSite.SetSite(object? pUnkSite)
    {
        DebugLog($"SetSite null={pUnkSite is null}");
        _site = pUnkSite;
    }

    /// <inheritdoc />
    void IObjectWithSite.GetSite(ref Guid riid, out IntPtr ppvSite)
    {
        DebugLog($"GetSite riid={riid}");
        ppvSite = IntPtr.Zero;

        if (_site is null)
        {
            Marshal.ThrowExceptionForHR(unchecked((int)0x80004005)); // E_FAIL
            return;
        }

        var unknown = Marshal.GetIUnknownForObject(_site);
        try
        {
            Marshal.QueryInterface(unknown, ref riid, out ppvSite);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

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
        if (_uiThread is not null)
            return;

        DebugLog("Creating PreviewPanel UI thread");
        _uiThread = new PreviewUiThread(DebugLog);
        _panelHandle = _uiThread.Handle;
        DebugLog($"PreviewPanel handle={_panelHandle}");

        if (_parentHwnd != IntPtr.Zero)
        {
            SetParent(_panelHandle, _parentHwnd);
            UpdatePanelBounds();
            _uiThread.Show();
            DebugLog("PreviewPanel parented and shown");
        }
        else
        {
            DebugLog("PreviewPanel created without parent hwnd");
        }
    }

    private void UpdatePanelBounds()
    {
        if (_uiThread is null)
            return;

        var rect = _previewRect;
        if ((rect.Right - rect.Left <= 0 || rect.Bottom - rect.Top <= 0) &&
            _parentHwnd != IntPtr.Zero &&
            GetClientRect(_parentHwnd, out var parentRect))
        {
            rect = parentRect;
        }

        _uiThread.SetBounds(
            rect.Left,
            rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top);
        DebugLog($"Panel bounds updated to {FormatRect(rect)}");
    }

    private void DisposePanel()
    {
        if (_uiThread is null)
            return;
        _uiThread.Dispose();
        _uiThread = null;
        _panelHandle = IntPtr.Zero;
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    private static string HtmlEncode(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string FormatRect(RECT rect) =>
        $"{rect.Left},{rect.Top},{rect.Right},{rect.Bottom}";

    private static void DebugLog(string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:O} pid={Environment.ProcessId} tid={Environment.CurrentManagedThreadId} {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "mdz-win-prev.log"), line);
        }
        catch
        {
            // Logging must never affect preview activation.
        }
    }

    private sealed class PreviewUiThread : IDisposable
    {
        private readonly Action<string> _log;
        private readonly ManualResetEventSlim _ready = new();
        private readonly Thread _thread;
        private ApplicationContext? _context;
        private PreviewPanel? _panel;
        private Exception? _startupException;

        public PreviewUiThread(Action<string> log)
        {
            _log = log;
            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "MDZip Preview UI",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();

            if (!_ready.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Timed out creating preview UI thread.");

            if (_startupException is not null)
                throw new InvalidOperationException("Preview UI thread failed to start.", _startupException);
        }

        public IntPtr Handle { get; private set; }

        public void Show() => Post(panel => panel.Show());

        public void Focus() => Post(panel => panel.Focus());

        public void SetBounds(int x, int y, int width, int height) =>
            Post(panel => panel.SetBounds(x, y, Math.Max(0, width), Math.Max(0, height)));

        public void ShowHtml(string html) => Post(panel => panel.ShowHtml(html));

        public void ShowMessage(string heading, string detail) =>
            Post(panel => panel.ShowMessage(heading, detail));

        public void Dispose()
        {
            try
            {
                if (_panel is not null && !_panel.IsDisposed)
                {
                    _panel.BeginInvoke(new Action(() =>
                    {
                        _context?.ExitThread();
                        _panel.Dispose();
                    }));
                }
            }
            catch
            {
                // Best-effort shutdown; prevhost will release the process if needed.
            }

            if (_thread.IsAlive)
                _thread.Join(TimeSpan.FromSeconds(1));

            _ready.Dispose();
        }

        private void ThreadMain()
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                _panel = new PreviewPanel();
                _panel.CreateControl();
                Handle = _panel.Handle;
                _context = new ApplicationContext();
                _log($"Preview UI thread ready handle={Handle}");
                _ready.Set();
                Application.Run(_context);
            }
            catch (Exception ex)
            {
                _startupException = ex;
                _log("Preview UI thread exception: " + ex);
                _ready.Set();
            }
        }

        private void Post(Action<PreviewPanel> action)
        {
            var panel = _panel;
            if (panel is null || panel.IsDisposed)
                return;

            try
            {
                if (panel.InvokeRequired)
                    panel.BeginInvoke(new Action(() => action(panel)));
                else
                    action(panel);
            }
            catch (InvalidOperationException)
            {
                // The control may already be tearing down.
            }
        }
    }
}
