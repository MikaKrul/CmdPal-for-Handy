# HandyExtension

HandyExtension adds Handy's transcription controls to the Microsoft PowerToys Command Palette. It is an independent community project and is not affiliated with the Handy maintainers.

## Handy in Command Palette

Select **Handy** in PowerToys Command Palette to open the extension's home page. By default it focuses on transcripts and the dictionary; the recording actions and the remaining settings live under its **More** page:

- Browse Handy's local transcription history, open a transcript, and play its saved WAV recording in your default audio player. Use **Load older history** to browse more than 50 entries. **Search Transcripts** searches the *full* saved text - the title, the raw transcription, and the post-processed text - so words deep inside a long transcript are found even though the list only shows a short preview. While searching, the preview centers on the match so the result is visible. An individual item's **More commands** menu copies its transcript, and the home page copies the newest transcript.
- Switch the recording microphone and the installed transcription model, quickly add a dictionary word, or manage the custom dictionary.
- Under **More**: toggle recording, use AI post-processing, or cancel an operation; paste the newest transcript into the active app; set the model's language; configure post-processing (providers, models, fallback, prompts); open the recordings folder; or open Handy.

### Menu layout

The menu layout is yours to change. Use any command's **More commands** (…) menu to pin it to the **top level** of Command Palette, move it between the home page and the More page, or hide it from all menus. A top-level command appears directly in the palette's main list, without opening the Handy page first - handy for favorites like Copy Last Transcript, Toggle Recording, or Add Dictionary Word, and the palette list refreshes immediately. Hidden commands come back through **Customize Menu**: it lists every command with its current placement, and selecting one opens a submenu with **Top level**, **Home page**, **More page**, and **Hidden** options, with the current choice marked. Each row can also be reset to its default, and **Reset all to defaults** undoes every change. The layout is saved in `%LOCALAPPDATA%\HandyExtension\menu.json` as one `"command id": "top" | "home" | "more" | "hidden"` pair per command, so it can also be edited by hand or reset by deleting the file.

History is read directly from Handy's local SQLite database and audio files. Custom words are stored in Handy's local settings file. To avoid Handy overwriting an external settings change, fully quit Handy from its tray menu before adding or removing custom words; then reopen Handy to apply them. Post-processing provider, model, fallback, prompt, and microphone settings use the same local settings file and also require Handy to be fully closed before editing. Every settings change is transactional: the extension verifies the file did not change while the edit was being prepared and refuses to write if it did, and the write itself is atomic - it first writes a complete new file and then swaps it in, keeping the previous version as `settings_store.json.HandyExtension.bak` next to the settings file, so the last state can be restored by hand if a change ever goes wrong. Microphone choices list Windows' active recording devices by the same endpoint names Handy matches, with a **Default** entry that follows the Windows default microphone; the selection takes effect when Handy next starts. The extension does not show or intentionally change API-key values. Installed model choices come from Handy's `--list-models --json` command. The extension does not change Handy's source code and does not send local data over the network. Handy may send a transcript to the post-processing provider configured by the user when that feature is used.

Handy's control flags are handled by its single-instance mechanism and are only sent when Handy is already running. If it is not running, the extension asks you to open it first; this avoids starting Handy without performing the requested control action. Handy describes its CLI as beta functionality. The `--start-hidden` flag is not exposed because launching it while Handy is already running raises the existing window instead of starting a hidden instance.

## Requirements

- Windows 11 for the PowerToys Command Palette host (the extension package targets Windows 10 version 19041 or later).
- [Microsoft PowerToys](https://developer.microsoft.com/en-us/windows/powertoys/) with Command Palette enabled.
- [Handy for Windows](https://github.com/cjpais/Handy/releases/latest) installed. For remote-control commands, Handy must support its documented CLI flags.

## Install and use

Download the latest [HandyExtension release](https://github.com/MikaKrul/HandyExtension/releases/latest). It contains the signed MSIX package, the public signing certificate, and a SHA-256 checksum. You do not need Visual Studio or the .NET SDK to install it.

The package uses a self-signed certificate. Windows does not trust it automatically, so install the included public certificate once before installing or updating the MSIX. Windows App Installer checks the **Local Machine > Trusted People** store, so this step requires administrator approval:

1. Download `HandyExtension.cer` and `HandyExtension.msix` from the release.
2. Open PowerShell as Administrator. From the folder containing the downloaded certificate, run `Import-Certificate -FilePath .\HandyExtension.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`. Alternatively, double-click the `.cer`, choose **Install Certificate**, select **Local Machine**, then place it in **Trusted People**. Do not install it in **Trusted Root Certification Authorities**.
3. Open `HandyExtension.msix` and choose **Install**. For future updates signed by this project certificate, install the new MSIX; you do not need to import the certificate again. Before installing an update, exit PowerToys from its tray menu: while the Command Palette is running it keeps the installed package in use, and the update fails with error `0x80073CF8`.
4. Install and open Handy, ensure PowerToys Command Palette is enabled, then run **Reload Command Palette Extension** and choose Handy.

This certificate identifies the package publisher and lets Windows verify that the signed package has not changed. It is self-signed, not issued by a public certificate authority; you are choosing to trust this publisher on this computer. The release workflow builds and signs the MSIX on GitHub Actions. Its private key is stored as encrypted GitHub Actions secrets and is never included in the repository or release.

If Handy is not found automatically, open Command Palette extension settings and set **Handy executable path** to the full path of `Handy.exe`. The setting is local to the extension host. Automatic discovery checks `PATH` and common per-user and machine installation folders; other installation layouts can use the explicit setting.

## Development

Open `HandyExtension.sln` in a recent Visual Studio installation with the workloads above. The project targets `net10.0-windows10.0.26100.0`; restore NuGet packages, then build the solution in Release for x64 or ARM64. The test project covers configured path validation, `PATH` discovery, menu layout persistence, history database paging and full-text search, settings file robustness (corrupt JSON, missing settings object, concurrent modification, backup creation), and recorded-audio file name safety. GitHub Actions runs these tests and a Windows x64 Release build for pushes and pull requests to `main` or `develop`.

Useful checks when the .NET SDK is installed:

```powershell
dotnet restore HandyExtension.sln
dotnet test HandyExtension.Tests/HandyExtension.Tests.csproj --configuration Release --no-restore -p:Platform=x64
dotnet build HandyExtension.sln --configuration Release --no-restore -p:Platform=x64
```

## Manual end-to-end test

1. Install and open Handy. Complete its first-run setup and confirm its microphone works.
2. Install PowerToys, enable Command Palette, and install HandyExtension as described above.
3. Open Command Palette (`Win`+`Alt`+`Space` by default), reload extensions, and select **Handy**.
4. Confirm the home page shows Search Transcripts, Copy Last Transcript, Select Microphone, Select Model, Manage Dictionary, Add Dictionary Word, Customize Menu, and More. Open **More** and confirm it shows Toggle Recording, Transcribe with AI post-processing, Cancel transcription, Paste Last Transcript, Select Language, Post-processing, Open Recordings Folder, Open Handy, and Customize Menu. Test copy and paste using a harmless short transcript, with a blank Notepad document as the active app. Quit Handy before selecting a microphone, model, or language; reopen Handy afterward to apply the change. In Select Microphone, confirm Windows' recording devices are listed and the current choice is marked. In Search Transcripts, use an item's More commands menu to copy its transcript or open its WAV in the default player. Then search for a word that occurs only deep inside a long transcript, beyond the first 100 characters: confirm the entry is found, the preview centers on the match, and "No matching transcripts" appears for a term that is nowhere in the history.
5. Open **Customize Menu**. Select **Select Model**, confirm a submenu opens with Top level, Home page, More page, and Hidden and that the current choice is marked, choose **More page**, and confirm the placement changes. Use its More commands menu to reset it to the default. On the home page, use **Copy Last Transcript**'s More commands menu to choose **Pin to top level**, close the palette, reopen it, and confirm the command now appears directly in the palette's main list next to the Handy page; then set it back to the home page from Customize Menu. Also use a command's More commands menu to hide **Add Dictionary Word**, confirm it disappears from the home page, then restore it from Customize Menu. Select **Reset all to defaults** and confirm every placement returns to its default and the pinned command disappears from the top level.
6. Open **Custom words**. Fully quit Handy from the tray menu, add a temporary word, then reopen Handy and confirm it appears in Handy's custom words settings. Quit Handy again, remove the temporary word through Command Palette, and reopen Handy to confirm it is gone.
7. With Handy open, choose a downloaded model and a language it supports. Then fully quit Handy, open **Post-processing**, change the active provider or model, and save a prompt. Reopen Handy and verify the settings took effect. Quit Handy before making further extension-side settings edits.
8. With Handy running, select **Start or stop transcription**. Confirm Handy shows that recording started, select the command again, then dictate a short, non-sensitive phrase into a blank Notepad document and confirm it appears. Repeat with AI post-processing if configured, and use **Cancel transcription** to confirm it stops without inserting a transcript.
9. If Handy is not found, set the full `Handy.exe` path in extension settings and retry.

The automated tests cover executable path selection and discovery, menu layout persistence, the history database (paging, full-text search across raw and post-processed text, LIKE-wildcard escaping, missing or corrupt databases and columns), settings file robustness (corrupt JSON, missing settings object, concurrent modification, backups), and audio file name validation; they do not record audio. History, custom words, and post-processing settings require a Windows end-to-end check against an installed Handy profile. GitHub Actions runs the automated tests and Windows x64 Release build for pushes and pull requests to `main` or `develop`. A separate manually started workflow builds, signs, verifies, and publishes the versioned MSIX GitHub Release.

## Troubleshooting

- **Handy was not found:** set the full `Handy.exe` path in extension settings.
- **A control command did not act:** open Handy once, confirm the installed Handy version supports the flag, then retry. The extension cannot inspect Handy's recording state.
- **Windows says the publisher is unknown or the package is untrusted:** install the release's `.cer` into **Local Machine > Trusted People**, then try the MSIX again.
- **The updated extension is not visible:** install the updated MSIX and run **Reload Command Palette Extension**.
- **An update fails with error `0x80073CF8` ("the user canceled the install request"):** the installed package is still in use by the running Command Palette. Fully exit PowerToys from its tray menu, which also stops the Command Palette host and any loaded extension, then install the MSIX again and start PowerToys. If the dialog keeps failing, close PowerToys and run `Add-AppxPackage -Path .\HandyExtension.msix` in PowerShell instead.

## Privacy and security

The extension launches Handy locally for user-selected controls and for its local model list. It reads Handy history and recordings only from the local profile; audio playback is opened through the Windows default app. Custom word changes update Handy's local settings file only when Handy is closed. The extension itself does not send history, recordings, transcripts, custom words, post-processing settings, or API-key values over the network. Handy may send transcripts to the configured post-processing provider when that feature is used. The extension does not show or intentionally change API-key values. Installed model choices come from Handy's `--list-models --json` command; microphone names come from Windows' local device enumeration. Process arguments use `ProcessStartInfo.ArgumentList`; no shell is invoked. Report security issues privately through GitHub's Security Advisories for this repository.

## Distribution status and limitations

The project is an early development build. Local packaged deployment is configured, but Store, WinGet, and Extension Gallery distribution are not configured or validated. Automated tests cover executable path selection, menu layout persistence, history database and settings file robustness, and audio file name validation; Handy executable discovery is best-effort, so configure the path explicitly for custom installs. History and custom word access depend on Handy's current local file layout; the tests exercise that layout with generated databases and settings files, and a Windows end-to-end check against an installed Handy profile remains required. The extension can check whether a Handy process is running but cannot query whether it is recording or confirm that a control action succeeded.

Handy is a separate project. Its CLI details are documented at [cjpais/Handy](https://github.com/cjpais/Handy). The upstream Handy repository uses the MIT license; this project is licensed under MIT as shown in [LICENSE](LICENSE).
