# HandyExtension

HandyExtension adds Handy's transcription controls to the Microsoft PowerToys Command Palette. It is an independent community project and is not affiliated with the Handy maintainers.

## Commands

- **Toggle Transcription** sends `--toggle-transcription` to Handy.
- **Toggle Transcription with AI Post-processing** sends `--toggle-post-process`.
- **Cancel Transcription** sends `--cancel`.
- **Open Handy** starts Handy without command-line arguments.

Control flags are handled by Handy's single-instance mechanism and are only sent when the Handy process is already running. If it is not running, the extension asks you to open it first; this avoids starting Handy without performing the requested control action. Handy describes its CLI as beta functionality. The `--start-hidden` flag is not exposed because launching it while Handy is already running raises the existing window instead of starting a hidden instance.

## Requirements

- Windows 11 for the PowerToys Command Palette host (the extension package targets Windows 10 version 19041 or later).
- [Microsoft PowerToys](https://developer.microsoft.com/en-us/windows/powertoys/) with Command Palette enabled.
- [Handy for Windows](https://github.com/cjpais/Handy/releases/latest) installed. For remote-control commands, Handy must support its documented CLI flags.

## Install and use

There is not yet a downloadable HandyExtension installer or Gallery listing. Downloading this repository as a ZIP gives you source code, not an installable app. The CI workflow builds and tests the code but deliberately does not upload a package.

For a local development install, use Visual Studio with the **.NET desktop development**, **Windows application development**, and **MSIX Packaging** tooling. Open `HandyExtension.sln`, select x64 or ARM64, then choose **Build > Deploy HandyExtension**. If Windows asks, enable Developer Mode. In Command Palette, run **Reload Command Palette Extension**, then choose Handy. A signed package for regular installation is not available yet.

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
2. Install PowerToys, enable Command Palette, and deploy HandyExtension as described above.
3. Open Command Palette (`Win`+`Alt`+`Space` by default), reload extensions, and select Handy.
4. Confirm **Open Handy** brings the Handy window forward. If it cannot find Handy, set the full `Handy.exe` path in extension settings and retry.
5. With Handy running, select **Toggle Transcription**. Confirm Handy shows that recording started, select the command again, then dictate a short, non-sensitive phrase into a blank Notepad document and confirm it appears.
6. Repeat with **Toggle Transcription with AI Post-processing** if that feature is configured in Handy. Start a recording and select **Cancel Transcription** to confirm it stops without inserting a transcript.
7. Quit Handy fully and select a control command. The extension should ask you to open Handy first, rather than launching it and implying the control action succeeded.
8. Change the configured executable path to an invalid path and confirm the extension reports that Handy was not found; then restore the correct path.

The automated tests cover configured executable paths and `PATH` discovery. They do not record audio. The real microphone and PowerToys checks above must be done on Windows.

## Troubleshooting

- **Handy was not found:** set the full `Handy.exe` path in extension settings.
- **A control command did not act:** open Handy once, confirm the installed Handy version supports the flag, then retry. The extension cannot inspect Handy's recording state.
- **The updated extension is not visible:** redeploy the package and run **Reload Command Palette Extension**.

## Privacy and security

The extension launches Handy locally and passes only the selected CLI flag. It does not access the microphone, audio, transcript history, or network. Process arguments use `ProcessStartInfo.ArgumentList`; no shell is invoked. Report security issues privately through GitHub's Security Advisories for this repository.

## Distribution status and limitations

The project is an early development build. Local packaged deployment is configured, but Store, WinGet, and Extension Gallery distribution are not configured or validated. Automated tests currently cover executable path selection; Handy executable discovery is best-effort, so configure the path explicitly for custom installs. The extension can check whether a Handy process is running but cannot query whether it is recording or confirm that a control action succeeded.

Handy is a separate project. Its CLI details are documented at [cjpais/Handy](https://github.com/cjpais/Handy). The upstream Handy repository uses the MIT license; this project is licensed under MIT as shown in [LICENSE](LICENSE).
