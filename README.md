# HandyExtension

HandyExtension adds Handy's transcription controls to the Microsoft PowerToys Command Palette. It is an independent community project and is not affiliated with the Handy maintainers.

## Handy in Command Palette

Select **Handy** in PowerToys Command Palette to open the extension's home page. From there you can:

- Toggle recording, use AI post-processing or cancel an operation, paste the newest transcript into the active app, switch the installed transcription model and its language, or open Handy.
- Browse Handy's local transcription history, open a transcript, and play its saved WAV recording in your default audio player. Use **Load older history** to browse more than 50 entries.
- Copy the newest transcript, or use an individual history item's **More commands** menu to copy its transcript. The **Search Transcripts** page filters the saved history.
- Quickly add a dictionary word, manage existing custom words, and open the recordings folder.
- Choose a post-processing provider; edit its model and fallback model; configure the custom endpoint and API format; toggle post-processing and fallback; and create, edit, select, or remove prompts.

History is read directly from Handy's local SQLite database and audio files. Custom words are stored in Handy's local settings file. To avoid Handy overwriting an external settings change, fully quit Handy from its tray menu before adding or removing custom words; then reopen Handy to apply them. Post-processing provider, model, fallback, and prompt settings use the same local settings file and also require Handy to be fully closed before editing. The extension does not show or intentionally change API-key values. Installed model choices come from Handy's `--list-models --json` command. The extension does not change Handy's source code and does not send local data over the network. Handy may send a transcript to the post-processing provider configured by the user when that feature is used.

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

Open `HandyExtension.sln` in a recent Visual Studio installation with the workloads above. The project targets `net10.0-windows10.0.26100.0`; restore NuGet packages, then build the solution in Release for x64 or ARM64. The test project covers configured path validation and `PATH` discovery. GitHub Actions runs these tests and a Windows x64 Release build for pushes and pull requests to `main` or `develop`.

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
4. Confirm the home page shows Toggle Recording, Copy Last Transcript, Paste Last Transcript, Select Model, Select Language, Manage Dictionary, Search Transcripts, and Open Recordings Folder. Test copy and paste using a harmless short transcript, with a blank Notepad document as the active app. Quit Handy before selecting a model or language; reopen Handy afterward to apply the change. In Search Transcripts, use an item's More commands menu to copy its transcript or open its WAV in the default player.
5. Open **Custom words**. Fully quit Handy from the tray menu, add a temporary word, then reopen Handy and confirm it appears in Handy's custom words settings. Quit Handy again, remove the temporary word through Command Palette, and reopen Handy to confirm it is gone.
6. With Handy open, choose a downloaded model and a language it supports. Then fully quit Handy, open **Post-processing**, change the active provider or model, and save a prompt. Reopen Handy and verify the settings took effect. Quit Handy before making further extension-side settings edits.
7. With Handy running, select **Start or stop transcription**. Confirm Handy shows that recording started, select the command again, then dictate a short, non-sensitive phrase into a blank Notepad document and confirm it appears. Repeat with AI post-processing if configured, and use **Cancel transcription** to confirm it stops without inserting a transcript.
8. If Handy is not found, set the full `Handy.exe` path in extension settings and retry.

The automated tests cover executable path selection and discovery; they do not record audio. History, custom words, and post-processing settings require a Windows end-to-end check against an installed Handy profile. GitHub Actions runs the automated tests and Windows x64 Release build for pushes and pull requests to `main` or `develop`. A separate manually started workflow builds, signs, verifies, and publishes the versioned MSIX GitHub Release.

## Troubleshooting

- **Handy was not found:** set the full `Handy.exe` path in extension settings.
- **A control command did not act:** open Handy once, confirm the installed Handy version supports the flag, then retry. The extension cannot inspect Handy's recording state.
- **Windows says the publisher is unknown or the package is untrusted:** install the release's `.cer` into **Local Machine > Trusted People**, then try the MSIX again.
- **The updated extension is not visible:** install the updated MSIX and run **Reload Command Palette Extension**.
- **An update fails with error `0x80073CF8` ("the user canceled the install request"):** the installed package is still in use by the running Command Palette. Fully exit PowerToys from its tray menu, which also stops the Command Palette host and any loaded extension, then install the MSIX again and start PowerToys. If the dialog keeps failing, close PowerToys and run `Add-AppxPackage -Path .\HandyExtension.msix` in PowerShell instead.

## Privacy and security

The extension launches Handy locally for user-selected controls and for its local model list. It reads Handy history and recordings only from the local profile; audio playback is opened through the Windows default app. Custom word changes update Handy's local settings file only when Handy is closed. The extension itself does not send history, recordings, transcripts, custom words, post-processing settings, or API-key values over the network. Handy may send transcripts to the configured post-processing provider when that feature is used. The extension does not show or intentionally change API-key values. Installed model choices come from Handy's `--list-models --json` command. Process arguments use `ProcessStartInfo.ArgumentList`; no shell is invoked. Report security issues privately through GitHub's Security Advisories for this repository.

## Distribution status and limitations

The project is an early development build. Local packaged deployment is configured, but Store, WinGet, and Extension Gallery distribution are not configured or validated. Automated tests cover executable path selection; Handy executable discovery is best-effort, so configure the path explicitly for custom installs. History and custom word access depend on Handy's current local file layout and are manually validated against an installed Handy profile. The extension can check whether a Handy process is running but cannot query whether it is recording or confirm that a control action succeeded.

Handy is a separate project. Its CLI details are documented at [cjpais/Handy](https://github.com/cjpais/Handy). The upstream Handy repository uses the MIT license; this project is licensed under MIT as shown in [LICENSE](LICENSE).
