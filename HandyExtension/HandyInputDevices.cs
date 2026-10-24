using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace HandyExtension;

internal sealed record HandyInputDevice(string Name, bool IsDefault);

/// <summary>
/// Lists Windows' active audio capture endpoints. Handy matches its stored
/// microphone by the endpoint's friendly name (the "Microphone (Adapter)"
/// string cpal reads from the MMDevice property store); Windows' device
/// enumeration reports the same names.
/// </summary>
internal static class WindowsAudioInputDevices
{
    public static IReadOnlyList<HandyInputDevice> ListInputDevices() =>
        Task.Run(async () =>
        {
            var endpoints = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
            var defaultId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
            var devices = new List<HandyInputDevice>();
            foreach (var endpoint in endpoints)
            {
                if (!endpoint.IsEnabled || string.IsNullOrWhiteSpace(endpoint.Name))
                {
                    continue;
                }

                devices.Add(new HandyInputDevice(
                    endpoint.Name,
                    !string.IsNullOrEmpty(defaultId) && string.Equals(endpoint.Id, defaultId, StringComparison.Ordinal)));
            }

            return (IReadOnlyList<HandyInputDevice>)devices;
        }).GetAwaiter().GetResult();
}
