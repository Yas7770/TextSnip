# TextSnip

A Windows tray app for capturing screen text and screenshots.

![TextSnip preview](docs/preview.png)

## Features

- Capture text with **Ctrl + Shift + 2** and review it before copying.
- Copy selected text or the entire result.
- Capture screenshots with **Ctrl + Shift + 3**.
- Crop screenshots, save PNG files, or copy images.
- Choose a capture delay of 0, 3, or 5 seconds.
- Switch between Playful and Minimal styles, with independent dark mode.
- Choose a custom accent colour in Playful mode.
- Start quietly in the Windows system tray.

Screen captures and text recognition are processed locally. OCR can make mistakes; review important text before using it.

## Download and install

Open this repository's **Releases** section and download `TextSnip-Setup.exe` or `TextSnip-Portable.zip` from release **v2.4.0**.

Exit an existing copy from its tray menu before installing an update. The app is unsigned, so Windows may show a publisher or reputation warning.

For the portable version, extract the ZIP and keep `TextSnip.exe` and `Ocr.ps1` together.

## Requirements

- Windows 10 or Windows 11.
- Windows PowerShell 5.1 and .NET Framework 4.5 or later.
- An installed Windows OCR language for text recognition.

## Use

TextSnip starts in the system tray. Right-click its icon and choose **Open**, **Capture text**, **Take screenshot**, **Settings**, or **Exit**.

The **New capture** button follows the selected **Text** or **Screenshot** tab. Capturing does not automatically replace your clipboard.

In the Screenshot tab, choose **Crop**, drag over the part to keep, then choose **Apply crop**. **Cancel** keeps the original image.

See [the usage guide](docs/USAGE.txt) for more details.

## Build from source

On Windows, open PowerShell in this repository's root directory and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The script uses the .NET Framework compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and generates the icon assets locally. No NuGet packages are needed.

Build output appears in `dist/`:

- `TextSnip.exe`
- `Ocr.ps1`
- `TextSnip-Setup.exe`

## Source files

| File | Purpose |
| --- | --- |
| `TextSnip.cs` | Main application, screen capture, settings, and themes |
| `ModernUi.cs` | UI controls and appearance |
| `CropPreview.cs` | Screenshot preview and crop selection |
| `Ocr.ps1` | Local Windows OCR integration |
| `Setup.cs` | Per-user installer |
| `Uninstall.ps1` | Uninstaller |
| `make-logo.ps1` | Logo and icon generation |
| `build.ps1` | Build script |

## Licence

TextSnip is available under the [MIT License](LICENSE).
