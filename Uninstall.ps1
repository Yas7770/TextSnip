$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
if ([System.Windows.Forms.MessageBox]::Show('Remove TextSnip from this account?', 'Uninstall TextSnip', 'YesNo', 'Question') -ne 'Yes') { exit }
$installFolder = Join-Path $env:LOCALAPPDATA 'TextSnip'
if ([IO.Path]::GetFullPath($PSScriptRoot) -ne [IO.Path]::GetFullPath($installFolder)) { throw 'Unexpected installation directory.' }
Get-Process TextSnip -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $installFolder 'TextSnip.exe') } | Stop-Process -Force
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name TextSnip -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\TextSnip' -Recurse -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TextSnip' -Recurse -ErrorAction SilentlyContinue
foreach ($shortcutFolder in @([Environment]::GetFolderPath('DesktopDirectory'), [Environment]::GetFolderPath('Programs'))) {
    Remove-Item -LiteralPath (Join-Path $shortcutFolder 'TextSnip.lnk') -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath $installFolder -Recurse -Force
[System.Windows.Forms.MessageBox]::Show('TextSnip was removed.', 'TextSnip') | Out-Null
