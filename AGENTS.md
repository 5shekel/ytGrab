# Agent Notes

## Project Shape

- Single .NET 8 Windows Forms tray app; no solution file, tests, or package lockfiles are present.
- Entry point is `Program.cs`, which runs `TrayAppContext`; there is no visible main form.
- Runtime user settings live in `%APPDATA%\YtGrab\settings.json`; downloaded helper tools live in `%APPDATA%\YtGrab\bin`.

## Commands

- Build: `dotnet build`
- Publish release exe: `dotnet publish -c Release`
- Build MSI installer: `dotnet build Installer\YtGrab.Installer.wixproj -c Release`
- The project file already sets `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, and disables symbols, so the publish output should be a single `YtGrab.exe` in `bin\Release\net8.0-windows\win-x64\publish\`.
- The WiX installer project publishes into `Installer\obj\<Configuration>\publish` before packaging and writes `Installer\bin\Release\YtGrab.msi`.
- There is no automated test suite; use `dotnet build`, `dotnet publish -c Release`, and the MSI build for release verification.

## Release Workflow

- `.github/workflows/release.yml` runs on `workflow_dispatch` and pushed tags matching `v*`.
- Manual workflow runs build and upload artifacts; tag runs also create the GitHub Release.
- Release artifacts are the single-file `YtGrab.exe` plus `Installer/bin/Release/YtGrab.msi`.
- Release tags should match the project `<Version>` value with a leading `v`, for example `0.1.1` -> `v0.1.1`.
- Keep `YtGrab.csproj` `<Version>` and `Installer/YtGrab.Installer.wixproj` `<ProductVersion>` in sync.
- The tray menu version uses `AssemblyInformationalVersion`: `v<Version> <7-char git hash>`. The hash comes from `git rev-parse --short=7 HEAD` during build, so keep workflow checkout `fetch-depth: 0`.
- Keep `CHANGELOG.md` updated with user-visible/release workflow changes before committing.

## Runtime Behavior To Preserve

- `ToolManager` should prefer existing `yt-dlp.exe`/`ffmpeg.exe` on `PATH`; if missing, it downloads them into `%APPDATA%\YtGrab\bin`.
- Downloads are intentionally WhatsApp-friendly: MP4 merge/recode, H.264 MP4 video preferred, M4A audio preferred, and 720p max format selection.
- YouTube filenames use oEmbed first because `yt-dlp` can return an empty title for some YouTube links; non-YouTube links fall back to the `yt-dlp` output template `%(title,id)s`.
- Clipboard watching accepts any `http`/`https` URL, not just YouTube, because `yt-dlp` supports many sites.

## Gotchas

- `YtGrab.exe` may be locked if the tray app is running; close it from the tray before rebuilding/publishing if copy warnings appear.
- Keep `bin/`, `obj/`, and `*.user` untracked; release binaries are uploaded via GitHub Releases, not committed.
- If committing for the current maintainer, use author `yair <yair99@gmail.com>` without changing git config.
