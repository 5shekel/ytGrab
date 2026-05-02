# Agent Notes

## Project Shape

- Single .NET 8 Windows Forms tray app; no solution file, tests, CI, or package lockfiles are present.
- Entry point is `Program.cs`, which runs `TrayAppContext`; there is no visible main form.
- Runtime user settings live in `%APPDATA%\YtGrab\settings.json`; downloaded helper tools live in `%APPDATA%\YtGrab\bin`.

## Commands

- Build: `dotnet build`
- Publish release exe: `dotnet publish -c Release`
- The project file already sets `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, and disables symbols, so the publish output should be a single `YtGrab.exe` in `bin\Release\net8.0-windows\win-x64\publish\`.

## Runtime Behavior To Preserve

- `ToolManager` should prefer existing `yt-dlp.exe`/`ffmpeg.exe` on `PATH`; if missing, it downloads them into `%APPDATA%\YtGrab\bin`.
- Downloads are intentionally WhatsApp-friendly: MP4 merge/recode, H.264 MP4 video preferred, M4A audio preferred, and 720p max format selection.
- YouTube filenames use oEmbed first because `yt-dlp` can return an empty title for some YouTube links; non-YouTube links fall back to the `yt-dlp` output template `%(title,id)s`.
- Clipboard watching accepts any `http`/`https` URL, not just YouTube, because `yt-dlp` supports many sites.

## Gotchas

- `YtGrab.exe` may be locked if the tray app is running; close it from the tray before rebuilding/publishing if copy warnings appear.
- Keep `bin/`, `obj/`, and `*.user` untracked; release binaries are uploaded via GitHub Releases, not committed.
