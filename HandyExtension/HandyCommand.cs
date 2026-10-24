using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

internal sealed partial class HandyCommand : InvokableCommand
{
    private readonly Func<string?> _configuredPath;
    private readonly string? _argument;

    public HandyCommand(string name, string? argument, Func<string?> configuredPath)
    {
        Name = name;
        _argument = argument;
        _configuredPath = configuredPath;
        Icon = new IconInfo("\uE768");
    }

    public override ICommandResult Invoke()
    {
        try
        {
            var executable = HandyExecutableLocator.Find(_configuredPath());
            if (executable is null)
            {
                return CommandResult.ShowToast("Handy was not found. Install Handy or set its executable path in extension settings.");
            }

            if (_argument is not null && !IsHandyRunning())
            {
                return CommandResult.ShowToast("Open Handy first, then run this control command again.");
            }

            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            if (_argument is not null)
            {
                startInfo.ArgumentList.Add(_argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return CommandResult.ShowToast("Handy could not be started.");
            }

            return CommandResult.Hide();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return CommandResult.ShowToast($"Could not start Handy: {ex.Message}");
        }
    }

    private static bool IsHandyRunning()
    {
        var processes = Process.GetProcessesByName("Handy");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
