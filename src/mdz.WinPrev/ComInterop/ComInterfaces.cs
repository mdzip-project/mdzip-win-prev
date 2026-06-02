using System.Runtime.InteropServices;

namespace MDZip.WinPrev.ComInterop;

/// <summary>
/// Core Windows Shell preview handler interface.
/// Implemented by preview handlers to display file content in the Windows
/// Explorer preview pane.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("8895b1c6-b41f-4c1c-a562-0d564250836f")]
internal interface IPreviewHandler
{
    /// <summary>Sets the parent window and bounding rectangle for the preview.</summary>
    void SetWindow(IntPtr hwnd, ref RECT prc);

    /// <summary>Directs the preview handler to change the area within the parent hwnd that it draws into.</summary>
    void SetRect(ref RECT prc);

    /// <summary>Directs the preview handler to load data from the source specified earlier and render it to the previewer window.</summary>
    void DoPreview();

    /// <summary>Directs the preview handler to cease rendering a preview and to release all resources that have been allocated based on the item passed in.</summary>
    void Unload();

    /// <summary>Directs the preview handler to set focus to itself.</summary>
    void SetFocus();

    /// <summary>Directs the preview handler to return the HWND from calling the GetFocus function.</summary>
    void QueryFocus(out IntPtr phwnd);

    /// <summary>Enables low-level keyboard handling by the preview handler.</summary>
    [PreserveSig]
    uint TranslateAccelerator(ref MSG pmsg);
}

/// <summary>
/// Exposes a method that initializes a handler, such as a property handler, thumbnail
/// provider, or preview handler, with a file path.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("b7d14566-0509-4cce-a71f-0a554233bd9b")]
internal interface IInitializeWithFile
{
    /// <summary>Initializes a handler with a file path.</summary>
    void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
}

/// <summary>
/// Provides access to a window handle.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000114-0000-0000-C000-000000000046")]
internal interface IOleWindow
{
    /// <summary>Returns the window handle associated with this object.</summary>
    void GetWindow(out IntPtr phwnd);

    /// <summary>Determines whether context-sensitive help mode should be entered during an in-place activation session.</summary>
    void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);
}

/// <summary>
/// Allows the preview handler to provide visual theming support.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("196bf9a5-b346-4ef0-aa1e-5dcdb76768b1")]
internal interface IPreviewHandlerVisuals
{
    /// <summary>Sets the background color for the preview handler.</summary>
    void SetBackgroundColor(uint color);

    /// <summary>Sets the font attributes for the preview handler.</summary>
    void SetFont(ref LOGFONT plf);

    /// <summary>Sets the text color for the preview handler.</summary>
    void SetTextColor(uint color);
}

/// <summary>
/// Defines a rectangle by the coordinates of its upper-left and lower-right corners.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

/// <summary>
/// Contains message information from a thread's message queue.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public int x;
    public int y;
}

/// <summary>
/// Defines the attributes of a font.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
internal struct LOGFONT
{
    public int lfHeight;
    public int lfWidth;
    public int lfEscapement;
    public int lfOrientation;
    public int lfWeight;
    public byte lfItalic;
    public byte lfUnderline;
    public byte lfStrikeOut;
    public byte lfCharSet;
    public byte lfOutPrecision;
    public byte lfClipPrecision;
    public byte lfQuality;
    public byte lfPitchAndFamily;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string lfFaceName;
}
