# Preview Handler Lessons Learned

This note captures the architecture that is currently working and the failure
patterns to avoid in future iterations. Windows Explorer preview handlers are
sensitive to COM registration, process hosting, HWND parenting, apartment state,
and UI message loops; changing several of those at once makes failures hard to
diagnose.

## Known Working Architecture

The current `mdz.WinPrev` implementation works with this shape:

- .NET COM hosting generates `mdz.WinPrev.comhost.dll`.
- The handler is registered as an `InprocServer32` COM server.
- The handler uses an `AppID` with `DllSurrogate = Prevhost.exe`, so Explorer
  loads it through the preview-host surrogate instead of directly in Explorer.
- The COM class implements the core preview handler interfaces directly:
  `IPreviewHandler`, `IInitializeWithFile`, `IOleWindow`, `IObjectWithSite`, and
  `IPreviewHandlerVisuals`.
- The preview UI is created on a dedicated STA WinForms thread with its own
  `Application.Run` message loop.
- The preview HWND is parented into the Explorer-provided preview window.
- Rendered Markdown is displayed with the legacy WinForms `WebBrowser` control.
- Archive assets are extracted to a temporary folder and referenced with a
  `file:///` base URI.

This combination is intentionally conservative. It keeps COM activation,
WinForms lifetime, and rendered content display as simple as possible.

## Avoid Repeating

Do not switch the handler wholesale to a `LocalServer32` out-of-process COM EXE
unless there is a specific reason and a dedicated test plan. Earlier attempts
used that model and introduced extra failure modes around class factory
registration, COM lifetime, and cross-process HWND parenting.

Do not mix registration models. A project should not have source code or scripts
that register `LocalServer32` while an installer registers `InprocServer32` for
the same CLSID. That makes activation behavior ambiguous and can leave Explorer
loading a different binary path than expected.

Do not initialize WebView2 before a real WinForms handle and STA message pump are
established. Earlier attempts initialized WebView2 from the control constructor;
that can push async continuations onto the wrong context and has been associated
with DPI/thread-context failures.

Do not call cross-process `SetParent` synchronously from a `LocalServer32`
preview handler. In that model, Explorer may be blocked waiting for the COM call
to return while the preview server sends window messages back to Explorer's UI
thread, which can deadlock.

Do not change the COM CLSID casually. Once a handler is registered, stale CLSID
keys and preview-handler associations are easy to leave behind and hard to spot.

Do not debug Explorer preview failures without logging to a low-friction path
such as `%TEMP%`. Preview handler activation often fails silently from the user
interface.

## WebView2 Migration Guidance

PowerToys' Markdown previewer uses WebView2, and matching that direction is a
reasonable long-term goal. The safest migration path is to change only the
preview surface first:

- Keep the current `comhost.dll` / `InprocServer32` / `Prevhost.exe` registration
  model.
- Keep the dedicated STA WinForms UI thread.
- Keep the existing preview handler COM interfaces and file initialization path.
- Replace only `PreviewPanel`'s display implementation with a WebView2-backed
  control.
- Initialize WebView2 after the WinForms control handle exists and on the STA UI
  thread.
- Give WebView2 a stable user data folder under `%TEMP%` or another writable
  low-friction location.
- Disable unnecessary browser features for preview use, especially script,
  devtools, dialogs, host objects, password save, and autofill.
- Intercept or block external navigation. If opening links is desired, allow only
  explicit user-initiated `http` and `https` links and launch them externally.
- Keep a fallback text/error view so a missing or broken WebView2 runtime does
  not make the whole preview handler appear dead.

After a WebView2 panel works under the existing COM architecture, asset loading
can be revisited. The current temp-folder extraction is simple and reliable; a
custom virtual host or request-interception scheme should be treated as a
separate follow-up change.

## Useful References

- Current working COM registration: `scripts/install.ps1`
- Current COM handler and STA UI thread: `src/mdz.WinPrev/MdzPreviewHandler.cs`
- Current legacy browser host: `src/mdz.WinPrev/PreviewPanel.cs`
- Markdown-to-HTML renderer: `src/mdz.WinPrev/MdzRenderer.cs`
- Prior failed/experimental sibling repositories:
  - `../mdzip-win-preview`
  - `../../mdz-win-preview`
