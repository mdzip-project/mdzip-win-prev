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

.PARAMETER IconPath
    Optional path to an .ico, .exe, or .dll file to use as the .mdz shell icon.
    Icon resource indexes are supported, for example: "C:\tools\mdzip.ico,0".
    When omitted, the installer uses ..\mdzip-mark\ico\mdzip-mark-square.ico
    if present, otherwise it falls back to the COM host DLL.

.EXAMPLE
    .\install.ps1

.EXAMPLE
    .\install.ps1 -DllPath "C:\tools\mdz.WinPrev.comhost.dll"

.EXAMPLE
    .\install.ps1 -DllPath "C:\tools\mdz.WinPrev.comhost.dll" -IconPath "C:\tools\mdzip.ico"
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $DllPath,
    [string] $IconPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

$CLSID          = '{CA7A244F-7A83-4B5E-9D7A-9F13EF5E8B3A}'
$APPID          = $CLSID
$PREVIEW_IID    = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
$HANDLER_NAME   = 'MDZip Preview Handler'
$FILE_EXTENSION = '.mdz'
$DEFAULT_PROGID = 'MDZip.Document'

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

if (-not $IconPath) {
    $siblingIconPath = Join-Path $PSScriptRoot '..\..\mdzip-mark\ico\mdzip-mark-square.ico'
    if (Test-Path $siblingIconPath) {
        $IconPath = (Resolve-Path $siblingIconPath).Path
    }
    else {
        $IconPath = "$DllPath,0"
    }
}
elseif ($IconPath -notmatch ',\s*-?\d+\s*$') {
    if (-not (Test-Path $IconPath)) {
        Write-Error "Icon not found: $IconPath"
    }

    $IconPath = (Resolve-Path $IconPath).Path
}
else {
    $iconFilePath = $IconPath -replace ',\s*-?\d+\s*$', ''
    if (-not (Test-Path $iconFilePath)) {
        Write-Error "Icon not found: $iconFilePath"
    }

    $resolvedIconFilePath = (Resolve-Path $iconFilePath).Path
    $iconIndex = [regex]::Match($IconPath, ',\s*(-?\d+)\s*$').Groups[1].Value
    $IconPath = "$resolvedIconFilePath,$iconIndex"
}

Write-Host "Icon path: $IconPath"

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
    Set-ItemProperty -Path $clsidKey -Name 'AppID' -Value $APPID
    New-ItemProperty -Path $clsidKey -Name 'DisableLowILProcessIsolation' -Value 1 -PropertyType DWord -Force | Out-Null

    $inprocKey = "$clsidKey\InprocServer32"
    New-Item -Path $inprocKey -Force | Out-Null
    Set-ItemProperty -Path $inprocKey -Name '(Default)'       -Value $DllPath
    Set-ItemProperty -Path $inprocKey -Name 'ThreadingModel'  -Value 'Apartment'
}

# ---------------------------------------------------------------------------
# Register AppID for a dedicated preview-host surrogate
# ---------------------------------------------------------------------------

Write-Host "Registering AppID $APPID..."

if ($PSCmdlet.ShouldProcess("HKCR:\AppID\$APPID", 'Create registry key')) {
    $appIdKey = "Registry::HKEY_CLASSES_ROOT\AppID\$APPID"

    New-Item -Path $appIdKey -Force | Out-Null
    Set-ItemProperty -Path $appIdKey -Name '(Default)' -Value $HANDLER_NAME
    Set-ItemProperty -Path $appIdKey -Name 'DllSurrogate' -Value 'Prevhost.exe'
}

# ---------------------------------------------------------------------------
# Register .mdz file association
# ---------------------------------------------------------------------------

Write-Host "Registering $FILE_EXTENSION file association..."

if ($PSCmdlet.ShouldProcess("HKCR:\$FILE_EXTENSION", 'Create registry key')) {
    $extKey = "Registry::HKEY_CLASSES_ROOT\$FILE_EXTENSION"

    if (-not (Test-Path $extKey)) {
        New-Item -Path $extKey -Force | Out-Null
        Set-ItemProperty -Path $extKey -Name '(Default)'          -Value $DEFAULT_PROGID
        Set-ItemProperty -Path $extKey -Name 'Content Type'       -Value 'application/vnd.mdzip'
        Set-ItemProperty -Path $extKey -Name 'PerceivedType'      -Value 'document'
    }

    $shellExKey = "$extKey\ShellEx\$PREVIEW_IID"
    New-Item -Path $shellExKey -Force | Out-Null
    Set-ItemProperty -Path $shellExKey -Name '(Default)' -Value $CLSID

    $progId = (Get-ItemProperty -Path $extKey -Name '(Default)' -ErrorAction SilentlyContinue).'(Default)'
    if ([string]::IsNullOrWhiteSpace($progId)) {
        $progId = $DEFAULT_PROGID
    }

    $progIdKey = "Registry::HKEY_CLASSES_ROOT\$progId"
    if (-not (Test-Path $progIdKey)) {
        New-Item -Path $progIdKey -Force | Out-Null
        Set-ItemProperty -Path $progIdKey -Name '(Default)' -Value 'MDZip Document'
    }

    $defaultIconKey = "$progIdKey\DefaultIcon"
    New-Item -Path $defaultIconKey -Force | Out-Null
    Set-ItemProperty -Path $defaultIconKey -Name '(Default)' -Value $IconPath

    $progIdShellExKey = "$progIdKey\ShellEx\$PREVIEW_IID"
    New-Item -Path $progIdShellExKey -Force | Out-Null
    Set-ItemProperty -Path $progIdShellExKey -Name '(Default)' -Value $CLSID
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
