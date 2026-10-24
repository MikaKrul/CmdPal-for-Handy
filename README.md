# HandyExtension

A PowerToys Command Palette extension for [Handy](https://github.com/cjpais/Handy), the speech-to-text app. This is an independent community project and is not affiliated with the Handy maintainers.

## What you can do

Open Command Palette (`Win`+`Alt`+`Space` by default) and select **Handy**:

- Search your transcription history. The search reads the full saved text, raw and post-processed, not just the visible preview, and the list centers the preview on the match. **Load older history** pages past the first 50 entries.
- Copy the newest transcript to the clipboard, or paste it into the active app.
- Open a transcript, play its recording in your default audio player, or open the recordings folder.
- Add and remove custom dictionary words.
- Choose the microphone, the transcription model, its language, and post-processing (provider, model, fallback, prompts).
- Start and stop transcription, with or without AI post-processing, and cancel a running operation. These control commands only work while Handy is running, and the extension asks you to open Handy first if it is not. Handy documents its CLI flags as beta functionality.

## Change the menu

Every command has a **More commands** (…) menu. Use it to pin a favorite to the **top level** of the palette, move it between the home page and the More page, or hide it. A pinned command shows up directly in the palette's main list, next to the Handy page, and the list refreshes right away.

**Customize Menu**, at the bottom of both pages, lists every command with its current placement. Selecting one opens a submenu with Top level, Home page, More page, and Hidden, with the current choice marked. You can reset one command or all of them.

The layout is saved in `%LOCALAPPDATA%\HandyExtension\menu.json` as one `"command id": "top" | "home" | "more" | "hidden"` pair per command. Edit it by hand, or delete the file to reset everything.

## Requirements

- Windows 11 with [Microsoft PowerToys](https://developer.microsoft.com/en-us/windows/powertoys/) and Command Palette enabled. The extension package itself targets Windows 10 version 19041 or later.
- [Handy for Windows](https://github.com/cjpais/Handy/releases/latest) installed.

## Install

Download the newest [release](https://github.com/MikaKrul/HandyExtension/releases/latest). It contains the signed MSIX, the public signing certificate, and a SHA-256 checksum. You do not need Visual Studio or the .NET SDK.

1. The package is signed with a self-signed certificate, so trust the publisher once. In PowerShell as Administrator, from the download folder, run:

   ```powershell
   Import-Certificate -FilePath .\HandyExtension.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

   Put it in Trusted People, not in Trusted Root Certification Authorities. After this one-time step, updates install without touching the certificate again.

2. Exit PowerToys from its tray menu, then run `Add-AppxPackage -Path .\HandyExtension.msix`, or open the MSIX and choose Install. Exiting PowerToys matters: while the Command Palette runs, it keeps the installed package in use and an update fails with error `0x80073CF8`.

3. Start PowerToys, open Command Palette, reload extensions, and select **Handy**.

The release workflow builds and signs the MSIX on GitHub Actions. The private signing key is an encrypted secret there and never enters the repository or the release. Report security issues privately through GitHub Security Advisories for this repository.

If Handy is not found automatically, set **Handy executable path** in the extension settings to the full path of `Handy.exe`. Discovery checks `PATH` and the common per-user and machine install folders; other layouts need the explicit setting.

## Settings changes

The extension writes custom words, microphone, model, language, and post-processing choices directly to Handy's local `settings_store.json`. Fully quit Handy from its tray menu first: if Handy is running, it overwrites the file on exit and your change is lost.

Every change is protected three ways:

1. The extension refuses to write while a Handy process is running.
2. Just before saving, it re-reads the file and refuses if anything changed it in the meantime.
3. The write is atomic, and the previous version stays behind as `settings_store.json.HandyExtension.bak` next to the settings file, so you can restore by hand if something ever goes wrong.

Reopen Handy after a change to apply it. Microphone lists Windows' active recording devices with a **Default** entry that follows the Windows default microphone.

## Development

Open `HandyExtension.sln` in Visual Studio, or use the .NET SDK from the repository root:

```powershell
dotnet restore HandyExtension.sln
dotnet test HandyExtension.Tests/HandyExtension.Tests.csproj --configuration Release --no-restore -p:Platform=x64
dotnet build HandyExtension.sln --configuration Release --no-restore -p:Platform=x64
```

The extension targets `net10.0-windows10.0.26100.0`. The tests cover executable discovery, menu layout persistence, the history database (paging, full-text search over raw and post-processed text, missing or corrupt databases and columns), settings robustness (corrupt JSON, concurrent modification, backups), and audio file name validation. GitHub Actions runs the tests and a Windows x64 Release build for pushes and pull requests to `main` or `develop`. A separate, manually started workflow publishes the versioned MSIX release.

## Manual end-to-end test

The automated tests do not record audio. Walk through this against an installed Handy profile:

1. Record a short, non-sensitive phrase with Handy, with a blank Notepad document as the target.
2. In Search Transcripts, search for a word that occurs only deep inside that transcript, past the first 100 characters. The entry must appear with the preview centered on the match, and a nonsense word must show "No matching transcripts".
3. Use an item's More commands menu to copy the transcript and to play the WAV.
4. Test Copy Last Transcript and Paste Last Transcript against Notepad.
5. Fully quit Handy. Add a custom word through the extension, reopen Handy, and confirm it appears in Handy's settings. Remove the word the same way and confirm it is gone.
6. Quit Handy again. Change the microphone, model, language, and a post-processing setting one by one, reopening Handy after each change to confirm it took effect. Select Microphone must list Windows' recording devices with the current choice marked.
7. With Handy running, test start and stop transcription, AI post-processing, and cancel transcription.
8. Pin a command to top level and confirm it appears in the palette's main list. Hide a command from its More commands menu and restore it through Customize Menu. Reset all to defaults and confirm the layout is back to stock.

## Troubleshooting

- Handy is not found: set the full `Handy.exe` path in the extension settings.
- A control command does nothing: open Handy first, and confirm the installed version supports the flag. The extension cannot see Handy's recording state.
- An update fails with `0x80073CF8`: exit PowerToys from its tray menu, install the MSIX again, and start PowerToys.
- The installed update is not visible: run **Reload Command Palette Extension**.

## Privacy

The extension runs locally. It reads history, recordings, and settings from the local profile and sends nothing over the network. It does not show or change API-key values. Handy itself may send a transcript to the post-processing provider you configured when you use that feature.

## Status and license

Early development build. Store, WinGet, and Extension Gallery distribution are not configured or validated. History and settings access depend on Handy's current local file layout; the tests exercise that layout with generated databases and settings files, and a Windows end-to-end check against an installed Handy profile is still required.

Handy is a separate project by [cjpais](https://github.com/cjpais/Handy). Both projects use the MIT license; see [LICENSE](LICENSE).
