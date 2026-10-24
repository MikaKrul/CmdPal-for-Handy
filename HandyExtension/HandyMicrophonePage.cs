using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

internal sealed partial class HandyMicrophonesPage : ListPage
{
    private readonly HandyLocalData _data;

    public HandyMicrophonesPage(HandyLocalData data)
    {
        _data = data;
        Name = "Select microphone";
        Title = "Handy microphone";
        Icon = new IconInfo("\uE8D6");
    }

    public override IListItem[] GetItems()
    {
        List<HandyInputDevice> devices;
        try
        {
            devices = WindowsAudioInputDevices.ListInputDevices().ToList();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
        {
            return [new ListItem(new NoOpCommand()) { Title = "Windows microphones could not be listed", Subtitle = "Try again." }];
        }

        string? selected;
        try
        {
            selected = _data.ReadSelectedMicrophone();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return [new ListItem(new NoOpCommand()) { Title = "Handy's settings are unavailable", Subtitle = "Open Handy once, then try again." }];
        }

        var items = new List<IListItem>
        {
            new ListItem(new SelectHandyMicrophoneCommand(_data, null))
            {
                Title = "Default",
                Subtitle = selected is null ? "Current microphone setting" : "Use the Windows default microphone",
                Icon = new IconInfo(selected is null ? "\uE8FB" : "\uE8D6"),
            },
        };

        items.AddRange(devices.Select(device => (IListItem)new ListItem(new SelectHandyMicrophoneCommand(_data, device.Name))
        {
            Title = device.Name,
            Subtitle = $"{(device.IsDefault ? "Current Windows default device" : "Use as Handy's microphone")}{(string.Equals(device.Name, selected, StringComparison.Ordinal) ? " - Current microphone" : string.Empty)}",
            Icon = new IconInfo(string.Equals(device.Name, selected, StringComparison.Ordinal) ? "\uE8FB" : "\uE8D6"),
        }));

        if (devices.Count == 0)
        {
            items.Add(new ListItem(new NoOpCommand()) { Title = "No microphones found", Subtitle = "Connect or enable a microphone, then try again." });
        }

        return items.ToArray();
    }
}

internal sealed partial class SelectHandyMicrophoneCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly string? _deviceName;

    public SelectHandyMicrophoneCommand(HandyLocalData data, string? deviceName)
    {
        _data = data;
        _deviceName = deviceName;
        Name = "Select microphone";
        Icon = new IconInfo("\uE8D6");
    }

    public override CommandResult Invoke()
    {
        try
        {
            _data.SetSelectedMicrophone(_deviceName);
            return CommandResult.ShowToast("Saved. Reopen Handy to use the new setting.");
        }
        catch (InvalidOperationException ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException or JsonException)
        {
            return CommandResult.ShowToast("Handy's microphone setting could not be updated.");
        }
    }
}
