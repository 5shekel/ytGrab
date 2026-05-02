# YtGrab

Simple Windows tray app that watches the clipboard for `http`/`https` links and downloads them with `yt-dlp`.

## Requirements

- Windows
- .NET 8 runtime, or publish as a self-contained app

If `yt-dlp` or `ffmpeg` are not available on `PATH`, YtGrab downloads them into `%APPDATA%\YtGrab\bin` and uses those local copies.

## Behavior

- Lives in the system tray only.
- Right-click the tray icon for settings.
- Saves downloads to `%USERPROFILE%\Downloads\YtGrab` by default.
- Uses the sanitized media title as the output filename. For YouTube links, YtGrab asks YouTube oEmbed for the title before download so filenames do not fall back to ids when `yt-dlp` metadata has an empty title.
- Uses WhatsApp-friendly output settings: MP4 output, H.264 MP4 video preferred, M4A audio preferred, and 720p max selection.
- Shows a Windows tray notification when complete.
- Optionally beeps and opens the output folder when complete.

## Build

```powershell
dotnet build
```

## Publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Installer

```powershell
dotnet build Installer\YtGrab.Installer.wixproj -c Release
```

The MSI is written to `Installer\bin\Release\YtGrab.msi`.
