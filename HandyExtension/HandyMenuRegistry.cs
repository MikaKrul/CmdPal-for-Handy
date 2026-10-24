using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

/// <summary>
/// Creates the page or command behind a menu item definition. The definitions live in
/// HandyMenuItems.cs so the test project can compile the layout data without any
/// Command Palette dependencies; this file wires each definition to its real command.
/// </summary>
internal static class HandyMenuRegistry
{
    public static ICommand CreateCommand(HandyMenuItemDef definition, HandyLocalData data, Func<string?> configuredPath) => definition.Id switch
    {
        "searchTranscripts" => new HandyHistoryPage(data, configuredPath),
        "copyLastTranscript" => new CopyLastTranscriptCommand(data),
        "selectMicrophone" => new HandyMicrophonesPage(data),
        "selectModel" => new HandyTranscriptionModelsPage(data),
        "manageDictionary" => new HandyCustomWordsPage(data),
        "addDictionaryWord" => new AddHandyCustomWordPage(data),
        "toggleRecording" => new HandyCommand("Toggle Recording", "--toggle-transcription", configuredPath),
        "togglePostProcessing" => new HandyCommand("Transcribe with AI post-processing", "--toggle-post-process", configuredPath),
        "cancelTranscription" => new HandyCommand("Cancel transcription", "--cancel", configuredPath),
        "pasteLastTranscript" => new PasteLastTranscriptCommand(data),
        "selectLanguage" => new HandyTranscriptionLanguagesPage(data),
        "postProcessing" => new HandyPostProcessingPage(data),
        "openRecordingsFolder" => new OpenHandyRecordingsFolderCommand(data),
        "openHandy" => new HandyCommand("Open Handy", null, configuredPath),
        _ => throw new ArgumentOutOfRangeException(nameof(definition), $"Unknown menu item '{definition.Id}'."),
    };

    public static IconInfo Icon(HandyMenuItemDef definition) =>
        definition.Glyph is { } glyph ? new IconInfo(glyph) : IconHelpers.FromRelativePath("Assets\\HandyLogo.png");
}
