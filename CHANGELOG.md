# Changelog

## Unreleased

- Nothing yet.

## 0.1.3

- Launch YtGrab automatically after a successful MSI install.
- Add a tray menu option to start YtGrab with Windows, enabled by default.
- Add a pink circle application icon for Windows shell surfaces like Task Manager startup.
- Treat same-version MSI installs as upgrades to prevent duplicate Installed Apps entries.
- Register startup from the MSI and close any running YtGrab before replacing files.
- Prevent multiple YtGrab tray instances from running at the same time.

## 0.1.2

- Add an MSI installer artifact to the release workflow.
- Update release workflow actions to Node.js 24-compatible versions.

## 0.1.1

- Add a GitHub Actions release workflow that publishes the Windows executable from tagged source.
- Opt release workflow JavaScript actions into Node.js 24.
- Show the release version and 7-character git hash in the tray menu.

## 0.1.0

- Show the release version in the tray menu.
