<#
.SYNOPSIS
    Unregisters the MDZip Windows file explorer preview handler for .mdz files.

.DESCRIPTION
    Removes the registry entries installed by install.ps1.

.EXAMPLE
    .\uninstall.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$CLSID          = '{CA7A244F-7A83-4B5E-9D7A-9F13EF5E8B3A}'
$PREVIEW_IID    = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
$FILE_EXTENSION = '.mdz'

# ---------------------------------------------------------------------------
# Administrator check
# ---------------------------------------------------------------------------

$principal = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error 'This script must be run as Administrator.'
}

# ---------------------------------------------------------------------------
# Remove preview-handler list entry
# ---------------------------------------------------------------------------

$listKey = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers'

if (Test-Path $listKey) {
    if ($PSCmdlet.ShouldProcess($listKey, "Remove $CLSID value")) {
        $prop = Get-ItemProperty -Path $listKey -Name $CLSID -ErrorAction SilentlyContinue
        if ($null -ne $prop) {
            Remove-ItemProperty -Path $listKey -Name $CLSID
            Write-Host "Removed PreviewHandlers entry."
        }
    }
}

# ---------------------------------------------------------------------------
# Remove ShellEx key from .mdz association
# ---------------------------------------------------------------------------

$shellExKey = "Registry::HKEY_CLASSES_ROOT\$FILE_EXTENSION\ShellEx\$PREVIEW_IID"

if (Test-Path $shellExKey) {
    if ($PSCmdlet.ShouldProcess($shellExKey, 'Remove registry key')) {
        Remove-Item -Path $shellExKey -Recurse
        Write-Host "Removed ShellEx entry."
    }
}

# ---------------------------------------------------------------------------
# Remove CLSID
# ---------------------------------------------------------------------------

$clsidKey = "Registry::HKEY_CLASSES_ROOT\CLSID\$CLSID"

if (Test-Path $clsidKey) {
    if ($PSCmdlet.ShouldProcess($clsidKey, 'Remove registry key')) {
        Remove-Item -Path $clsidKey -Recurse
        Write-Host "Removed CLSID entry."
    }
}

# ---------------------------------------------------------------------------
# Notify shell of the change
# ---------------------------------------------------------------------------

if ($PSCmdlet.ShouldProcess('Shell', 'Notify change')) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Shell32Uninstall {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
'@
    [Shell32Uninstall]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
}

Write-Host "`nUninstallation complete." -ForegroundColor Green
