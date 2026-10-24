using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

internal sealed partial class HandyTranscriptionModelsPage : ListPage
{
    private readonly HandyLocalData _data;

    public HandyTranscriptionModelsPage(HandyLocalData data)
    {
        _data = data;
        Name = "Select model";
        Title = "Handy transcription models";
        Icon = new IconInfo("\uE8D2");
    }

    public override IListItem[] GetItems()
    {
        try
        {
            var models = _data.ReadAvailableTranscriptionModels();
            if (models.Count == 0)
            {
                return [new ListItem(new NoOpCommand()) { Title = "No downloaded models found", Subtitle = "Open Handy and download a transcription model first." }];
            }

            return models.Select(model => (IListItem)new ListItem(new SelectHandyModelCommand(_data, model.Id))
            {
                Title = model.Name,
                Subtitle = $"{model.Id}{(model.IsSelected ? " - Current model" : string.Empty)}",
                Icon = new IconInfo(model.IsSelected ? "\uE8FB" : "\uE8D2"),
            }).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or JsonException)
        {
            return [new ListItem(new NoOpCommand()) { Title = "Handy's model list is unavailable", Subtitle = "Open Handy once, then try again." }];
        }
    }
}

internal sealed partial class HandyTranscriptionLanguagesPage : ListPage
{
    private readonly HandyLocalData _data;

    public HandyTranscriptionLanguagesPage(HandyLocalData data)
    {
        _data = data;
        Name = "Select language";
        Title = "Handy transcription language";
        Icon = new IconInfo("\uE8D2");
    }

    public override IListItem[] GetItems()
    {
        try
        {
            var model = _data.ReadAvailableTranscriptionModels().FirstOrDefault(item => item.IsSelected);
            if (model is null)
            {
                return [new ListItem(new NoOpCommand()) { Title = "No downloaded model selected", Subtitle = "Choose an installed model first." }];
            }

            var selectedLanguage = _data.ReadSelectedLanguage();
            var items = new System.Collections.Generic.List<IListItem>();
            if (model.SupportsLanguageDetection)
            {
                items.Add(new ListItem(new SelectHandyLanguageCommand(_data, "auto"))
                {
                    Title = "Auto-detect",
                    Subtitle = string.Equals(selectedLanguage, "auto", StringComparison.Ordinal) ? "Current language setting" : "Let Handy detect the spoken language",
                    Icon = new IconInfo(string.Equals(selectedLanguage, "auto", StringComparison.Ordinal) ? "\uE8FB" : "\uE8D2"),
                });
            }

            items.AddRange(model.SupportedLanguages.Select(language => (IListItem)new ListItem(new SelectHandyLanguageCommand(_data, language))
            {
                Title = LanguageName(language),
                Subtitle = string.Equals(selectedLanguage, language, StringComparison.Ordinal) ? $"{language} - Current language" : language,
                Icon = new IconInfo(string.Equals(selectedLanguage, language, StringComparison.Ordinal) ? "\uE8FB" : "\uE8D2"),
            })));
            return items.Count > 0 ? items.ToArray() : [new ListItem(new NoOpCommand()) { Title = "This model has no language choices", Subtitle = "Handy will use this model's built-in language behavior." }];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or JsonException)
        {
            return [new ListItem(new NoOpCommand()) { Title = "Handy's language list is unavailable", Subtitle = "Open Handy once, then try again." }];
        }
    }

    private static string LanguageName(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }
}

internal sealed partial class SelectHandyModelCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly string _modelId;

    public SelectHandyModelCommand(HandyLocalData data, string modelId)
    {
        _data = data;
        _modelId = modelId;
        Name = "Select model";
    }

    public override CommandResult Invoke() => Run(() => _data.SetSelectedModel(_modelId));

    internal static CommandResult Run(Action action)
    {
        try
        {
            action();
            return CommandResult.ShowToast("Saved. Reopen Handy to use the new setting.");
        }
        catch (InvalidOperationException ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException or JsonException)
        {
            return CommandResult.ShowToast("Handy's model settings could not be updated.");
        }
    }
}

internal sealed partial class SelectHandyLanguageCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly string _language;

    public SelectHandyLanguageCommand(HandyLocalData data, string language)
    {
        _data = data;
        _language = language;
        Name = "Select language";
    }

    public override CommandResult Invoke() => SelectHandyModelCommand.Run(() => _data.SetSelectedLanguage(_language));
}

internal sealed partial class OpenHandyRecordingsFolderCommand : InvokableCommand
{
    private readonly HandyLocalData _data;

    public OpenHandyRecordingsFolderCommand(HandyLocalData data)
    {
        _data = data;
        Name = "Open recordings folder";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var recordingsPath = _data.GetRecordingsDirectory();
            Directory.CreateDirectory(recordingsPath);
            Process.Start(new ProcessStartInfo(recordingsPath) { UseShellExecute = true });
            return CommandResult.Hide();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return CommandResult.ShowToast("Handy's recordings folder could not be opened.");
        }
    }
}

internal sealed partial class PasteLastTranscriptCommand : InvokableCommand
{
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyV = 0x56;
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private readonly HandyLocalData _data;

    public PasteLastTranscriptCommand(HandyLocalData data)
    {
        _data = data;
        Name = "Paste last transcript";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var entries = _data.ReadHistory(null, out _);
            var transcript = entries.Count > 0 ? entries[0].TextToCopy : null;
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return CommandResult.ShowToast("Handy has no transcript to paste yet.");
            }

            var copy = new CopyTextCommand(transcript)
            {
                Result = CommandResult.ShowToast("Pasting the last transcript into the active app."),
            };
            var result = (CommandResult)copy.Invoke();
            _ = Task.Run(async () =>
            {
                await Task.Delay(650).ConfigureAwait(false);
                var inputs = new[]
                {
                    KeyboardInput(VirtualKeyControl, 0),
                    KeyboardInput(VirtualKeyV, 0),
                    KeyboardInput(VirtualKeyV, KeyUp),
                    KeyboardInput(VirtualKeyControl, KeyUp),
                };
                _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
            });
            return result;
        }
        catch
        {
            return CommandResult.ShowToast("Handy's history is unavailable. Open Handy once and try again.");
        }
    }

    private static NativeInput KeyboardInput(ushort key, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInputData { VirtualKey = key, Flags = flags } },
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, NativeInput[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInputData Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
