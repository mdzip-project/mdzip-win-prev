<#
.SYNOPSIS
    Registers the MDZip Windows file explorer preview handler for .mdz files.

.DESCRIPTION
    Installs the mdz.WinPrev COM preview handler by writing the required registry
    entries for the Windows Shell preview handler infrastructure.

    Registration is per-machine (HKLM) by default.  Run as Administrator.

.PARAMETER DllPath
    Absolute path to mdz.WinPrev.comhost.dll produced by the build.
    Defaults to the DLL found in the same directory as this script.

.EXAMPLE
    .\install.ps1

.EXAMPLE
    .\install.ps1 -DllPath "C:\tools\mdz.WinPrev.comhost.dll"
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $DllPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

$CLSID          = '{CA7A244F-7A83-4B5E-9D7A-9F13EF5E8B3A}'
$PREVIEW_IID    = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
$HANDLER_NAME   = 'MDZip Preview Handler'
$FILE_EXTENSION = '.mdz'

# ---------------------------------------------------------------------------
# Locate DLL
# ---------------------------------------------------------------------------

if (-not $DllPath) {
    $DllPath = Join-Path $PSScriptRoot 'mdz.WinPrev.comhost.dll'
}

if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found: $DllPath`nBuild the project first: dotnet build src/mdz.WinPrev/mdz.WinPrev.csproj -c Release"
}

$DllPath = (Resolve-Path $DllPath).Path
Write-Host "DLL path: $DllPath"

# ---------------------------------------------------------------------------
# Administrator check
# ---------------------------------------------------------------------------

$principal = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error 'This script must be run as Administrator.'
}

# ---------------------------------------------------------------------------
# Register CLSID
# ---------------------------------------------------------------------------

Write-Host "Registering CLSID $CLSID..."

if ($PSCmdlet.ShouldProcess("HKCR:\CLSID\$CLSID", 'Create registry key')) {
    $clsidKey = "Registry::HKEY_CLASSES_ROOT\CLSID\$CLSID"

    New-Item -Path $clsidKey -Force | Out-Null
    Set-ItemProperty -Path $clsidKey -Name '(Default)' -Value $HANDLER_NAME

    $inprocKey = "$clsidKey\InprocServer32"
    New-Item -Path $inprocKey -Force | Out-Null
    Set-ItemProperty -Path $inprocKey -Name '(Default)'       -Value $DllPath
    Set-ItemProperty -Path $inprocKey -Name 'ThreadingModel'  -Value 'Apartment'
}

# ---------------------------------------------------------------------------
# Register .mdz file association
# ---------------------------------------------------------------------------

Write-Host "Registering $FILE_EXTENSION file association..."

if ($PSCmdlet.ShouldProcess("HKCR:\$FILE_EXTENSION", 'Create registry key')) {
    $extKey = "Registry::HKEY_CLASSES_ROOT\$FILE_EXTENSION"

    if (-not (Test-Path $extKey)) {
        New-Item -Path $extKey -Force | Out-Null
        Set-ItemProperty -Path $extKey -Name '(Default)'          -Value 'MDZip.Document'
        Set-ItemProperty -Path $extKey -Name 'Content Type'       -Value 'application/vnd.mdzip'
        Set-ItemProperty -Path $extKey -Name 'PerceivedType'      -Value 'document'
    }

    $shellExKey = "$extKey\ShellEx\$PREVIEW_IID"
    New-Item -Path $shellExKey -Force | Out-Null
    Set-ItemProperty -Path $shellExKey -Name '(Default)' -Value $CLSID
}

# ---------------------------------------------------------------------------
# Add to the preview-handler list (optional, for discoverability)
# ---------------------------------------------------------------------------

$listKey = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers'

if ($PSCmdlet.ShouldProcess($listKey, "Add $CLSID")) {
    if (-not (Test-Path $listKey)) {
        New-Item -Path $listKey -Force | Out-Null
    }
    Set-ItemProperty -Path $listKey -Name $CLSID -Value $HANDLER_NAME
}

# ---------------------------------------------------------------------------
# Notify shell of the change
# ---------------------------------------------------------------------------

if ($PSCmdlet.ShouldProcess('Shell', 'Notify change')) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Shell32 {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
'@
    [Shell32]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
}

Write-Host "`nInstallation complete.  You may need to restart Windows Explorer for changes to take effect." -ForegroundColor Green
