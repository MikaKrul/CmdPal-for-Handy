using System;
using System.Collections.Generic;
using System.Linq;

namespace HandyExtension;

internal enum HandyMenuPlacement
{
    Home = 0,
    More = 1,
    TopLevel = 2,
    Hidden = 3,
}

/// <summary>
/// Metadata for one command that can appear on the extension's home or More page.
/// Glyph is null when the command uses the Handy logo instead of a Segoe glyph.
/// Order decides a command's position on the page it appears on; the gaps between
/// the home and More orders leave room for insertions without renumbering.
/// This file has no Command Palette dependencies so the test project can compile it directly.
/// </summary>
internal sealed record HandyMenuItemDef(
    string Id,
    string Title,
    string Subtitle,
    string? Glyph,
    HandyMenuPlacement DefaultPlacement,
    int Order);

internal static class HandyMenuItems
{
    public static readonly HandyMenuItemDef[] Items =
    [
        new("searchTranscripts", "Search Transcripts", "Browse history and copy transcription text", "\uE81C", HandyMenuPlacement.Home, 1),
        new("copyLastTranscript", "Copy Last Transcript", "Copy the newest Handy transcript to the clipboard", "\uE8C8", HandyMenuPlacement.Home, 2),
        new("selectMicrophone", "Select Microphone", "Switch Handy's input device (quit Handy first to save)", "\uE8D6", HandyMenuPlacement.Home, 3),
        new("selectModel", "Select Model", "Switch the active model (quit Handy first to save)", "\uEEA1", HandyMenuPlacement.Home, 4),
        new("manageDictionary", "Manage Dictionary", "Add and remove custom words", "\uE82D", HandyMenuPlacement.Home, 5),
        new("addDictionaryWord", "Add Dictionary Word", "Quickly add a word to the custom dictionary", "\uE82E", HandyMenuPlacement.Home, 6),
        new("toggleRecording", "Toggle Recording", "Toggle Handy transcription recording on/off", "\uE720", HandyMenuPlacement.More, 10),
        new("togglePostProcessing", "Transcribe with AI post-processing", "Toggle transcription with Handy's AI post-processing", "\uEB78", HandyMenuPlacement.More, 11),
        new("cancelTranscription", "Cancel transcription", "Stop the current Handy operation", "\uE711", HandyMenuPlacement.More, 12),
        new("pasteLastTranscript", "Paste Last Transcript", "Paste the newest transcript into the active app", "\uE77F", HandyMenuPlacement.More, 13),
        new("selectLanguage", "Select Language", "Set this model's language (quit Handy first to save)", "\uE774", HandyMenuPlacement.More, 14),
        new("postProcessing", "Post-processing", "Choose providers, edit models, fallback and prompts", "\uEB78", HandyMenuPlacement.More, 15),
        new("openRecordingsFolder", "Open Recordings Folder", "Open Handy's recordings directory", "\uE838", HandyMenuPlacement.More, 16),
        new("openHandy", "Open Handy", "Show the Handy window", null, HandyMenuPlacement.More, 17),
    ];

    public static HandyMenuItemDef? Find(string id) => Items.FirstOrDefault(item => item.Id == id);
}
