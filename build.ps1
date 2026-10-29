$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$buildOutput = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $buildOutput | Out-Null
& (Join-Path $PSScriptRoot 'make-logo.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /win32icon:TextSnip.ico "/out:$buildOutput\TextSnip.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll TextSnip.cs ModernUi.cs CropPreview.cs
if ($LASTEXITCODE) { throw 'Application compilation failed.' }
Copy-Item Ocr.ps1 $buildOutput
& $compiler /nologo /target:winexe /win32icon:TextSnip.ico "/out:$buildOutput\TextSnip-Setup.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll "/resource:$buildOutput\TextSnip.exe,TextSnip.exe" /resource:Ocr.ps1,Ocr.ps1 /resource:Uninstall.ps1,Uninstall.ps1 Setup.cs
if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
