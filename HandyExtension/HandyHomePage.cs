using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

internal sealed partial class HandyHomePage : ListPage
{
    private readonly HandyLocalData _data;
    private readonly Func<string?> _configuredPath;

    public HandyHomePage(Func<string?> configuredPath)
    {
        _configuredPath = configuredPath;
        _data = new HandyLocalData(configuredPath);
        Name = "Handy";
        Title = "Handy";
        Icon = IconHelpers.FromRelativePath("Assets\\HandyLogo.png");
        PlaceholderText = "Choose a Handy feature";
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new HandyHistoryPage(_data, _configuredPath))
        {
            Title = "Search Transcripts",
            Subtitle = "Browse history and copy transcription text",
            Icon = new IconInfo("\uE81C"),
        },
        new ListItem(new HandyTranscriptionModelsPage(_data))
        {
            Title = "Select Model",
            Subtitle = "Switch the active model (quit Handy first to save)",
            Icon = new IconInfo("\uEEA1"),
        },
        new ListItem(new HandyCustomWordsPage(_data))
        {
            Title = "Manage Dictionary",
            Subtitle = "Add and remove custom words",
            Icon = new IconInfo("\uE82D"),
        },
        new ListItem(new AddHandyCustomWordPage(_data))
        {
            Title = "Add Dictionary Word",
            Subtitle = "Quickly add a word to the custom dictionary",
            Icon = new IconInfo("\uE82E"),
        },
        new ListItem(new HandyMorePage(_data, _configuredPath))
        {
            Title = "More",
            Subtitle = "Recording actions, microphone, language, post-processing and folders",
            Icon = new IconInfo("\uE712"),
        },
    ];
}

internal sealed partial class HandyMorePage : ListPage
{
    private readonly HandyLocalData _data;
    private readonly Func<string?> _configuredPath;

    public HandyMorePage(HandyLocalData data, Func<string?> configuredPath)
    {
        _data = data;
        _configuredPath = configuredPath;
        Name = "More";
        Title = "More Handy features";
        Icon = new IconInfo("\uE712");
        PlaceholderText = "Choose a Handy feature";
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new HandyCommand("Toggle Recording", "--toggle-transcription", _configuredPath))
        {
            Title = "Toggle Recording",
            Subtitle = "Toggle Handy transcription recording on/off",
            Icon = new IconInfo("\uE720"),
        },
        new ListItem(new HandyCommand("Transcribe with AI post-processing", "--toggle-post-process", _configuredPath))
        {
            Title = "Transcribe with AI post-processing",
            Subtitle = "Toggle transcription with Handy's AI post-processing",
            Icon = new IconInfo("\uEB78"),
        },
        new ListItem(new HandyCommand("Cancel transcription", "--cancel", _configuredPath))
        {
            Title = "Cancel transcription",
            Subtitle = "Stop the current Handy operation",
            Icon = new IconInfo("\uE711"),
        },
        new ListItem(new CopyLastTranscriptCommand(_data))
        {
            Title = "Copy Last Transcript",
            Subtitle = "Copy the newest Handy transcript to the clipboard",
            Icon = new IconInfo("\uE8C8"),
        },
        new ListItem(new PasteLastTranscriptCommand(_data))
        {
            Title = "Paste Last Transcript",
            Subtitle = "Paste the newest transcript into the active app",
            Icon = new IconInfo("\uE77F"),
        },
        new ListItem(new HandyMicrophonesPage(_data))
        {
            Title = "Select Microphone",
            Subtitle = "Switch Handy's input device (quit Handy first to save)",
            Icon = new IconInfo("\uE8D6"),
        },
        new ListItem(new HandyTranscriptionLanguagesPage(_data))
        {
            Title = "Select Language",
            Subtitle = "Set this model's language (quit Handy first to save)",
            Icon = new IconInfo("\uE774"),
        },
        new ListItem(new HandyPostProcessingPage(_data))
        {
            Title = "Post-processing",
            Subtitle = "Choose providers, edit models, fallback and prompts",
            Icon = new IconInfo("\uEB78"),
        },
        new ListItem(new OpenHandyRecordingsFolderCommand(_data))
        {
            Title = "Open Recordings Folder",
            Subtitle = "Open Handy's recordings directory",
            Icon = new IconInfo("\uE838"),
        },
        new ListItem(new HandyCommand("Open Handy", null, _configuredPath))
        {
            Title = "Open Handy",
            Subtitle = "Show the Handy window",
            Icon = IconHelpers.FromRelativePath("Assets\\HandyLogo.png"),
        },
    ];
}
