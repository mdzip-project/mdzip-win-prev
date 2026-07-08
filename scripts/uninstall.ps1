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
$APPID          = $CLSID
$PREVIEW_IID    = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
$FILE_EXTENSION = '.mdz'
$DEFAULT_PROGID = 'MDZip.Document'

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
# Remove ShellEx key from the active .mdz ProgID association
# ---------------------------------------------------------------------------

$extKey = "Registry::HKEY_CLASSES_ROOT\$FILE_EXTENSION"
$progId = (Get-ItemProperty -Path $extKey -Name '(Default)' -ErrorAction SilentlyContinue).'(Default)'
if ([string]::IsNullOrWhiteSpace($progId)) {
    $progId = $DEFAULT_PROGID
}

$progIdShellExKey = "Registry::HKEY_CLASSES_ROOT\$progId\ShellEx\$PREVIEW_IID"

if (Test-Path $progIdShellExKey) {
    $handler = (Get-ItemProperty -Path $progIdShellExKey -Name '(Default)' -ErrorAction SilentlyContinue).'(Default)'
    if ($handler -eq $CLSID -and $PSCmdlet.ShouldProcess($progIdShellExKey, 'Remove registry key')) {
        Remove-Item -Path $progIdShellExKey -Recurse
        Write-Host "Removed ProgID ShellEx entry."
    }
}

# ---------------------------------------------------------------------------
# Remove shell icon from the active .mdz ProgID association
# ---------------------------------------------------------------------------

$defaultIconKey = "Registry::HKEY_CLASSES_ROOT\$progId\DefaultIcon"

if (Test-Path $defaultIconKey) {
    $icon = (Get-ItemProperty -Path $defaultIconKey -Name '(Default)' -ErrorAction SilentlyContinue).'(Default)'
    if ($icon -like '*mdz.WinPrev*' -and $PSCmdlet.ShouldProcess($defaultIconKey, 'Remove registry key')) {
        Remove-Item -Path $defaultIconKey -Recurse
        Write-Host "Removed ProgID DefaultIcon entry."
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
# Remove AppID
# ---------------------------------------------------------------------------

$appIdKey = "Registry::HKEY_CLASSES_ROOT\AppID\$APPID"

if (Test-Path $appIdKey) {
    if ($PSCmdlet.ShouldProcess($appIdKey, 'Remove registry key')) {
        Remove-Item -Path $appIdKey -Recurse
        Write-Host "Removed AppID entry."
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
