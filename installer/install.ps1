<#
  PowerShell installer scaffold:
  - Copies build files into target installation path
  - Optionally registers shortcuts and does simple configuration
#>

param(
  [string]$SourceDir = ".\dist\win",
  [string]$InstallDir = "$env:ProgramFiles\KSC-Sharp"
)

Write-Host "Installing KSC-Sharp from $SourceDir to $InstallDir"

if (-Not (Test-Path $SourceDir)) {
  Write-Error "Source directory not found: $SourceDir"
  exit 1
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $SourceDir '*') -Destination $InstallDir -Recurse -Force

# Create start menu shortcut
$WScriptShell = New-Object -ComObject WScript.Shell
$ShortcutPath = Join-Path ([Environment]::GetFolderPath("Programs")) "KSC-Sharp.lnk"
$Shortcut = $WScriptShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = Join-Path $InstallDir "KSC-Sharp.App.exe"
$Shortcut.Save()

Write-Host "Installation complete. Shortcut created at $ShortcutPath"
