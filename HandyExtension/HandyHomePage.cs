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
        Icon = new IconInfo("\uE8D4");
        PlaceholderText = "Choose a Handy feature";
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new HandyCommand("Start or stop transcription", "--toggle-transcription", _configuredPath))
        {
            Title = "Start or stop transcription",
            Subtitle = "Toggle Handy recording",
            Icon = new IconInfo("\uE768"),
        },
        new ListItem(new HandyCommand("Transcribe with AI post-processing", "--toggle-post-process", _configuredPath))
        {
            Title = "Transcribe with AI post-processing",
            Subtitle = "Toggle transcription with Handy's AI post-processing",
            Icon = new IconInfo("\uE8D2"),
        },
        new ListItem(new HandyCommand("Cancel transcription", "--cancel", _configuredPath))
        {
            Title = "Cancel transcription",
            Subtitle = "Stop the current Handy operation",
            Icon = new IconInfo("\uE711"),
        },
        new ListItem(new HandyHistoryPage(_data, _configuredPath))
        {
            Title = "History",
            Subtitle = "Browse and play saved recordings",
            Icon = new IconInfo("\uE81C"),
        },
        new ListItem(new HandyCustomWordsPage(_data))
        {
            Title = "Custom words",
            Subtitle = "Add or remove words used by Handy",
            Icon = new IconInfo("\uE8D2"),
        },
        new ListItem(new HandyPostProcessingPage(_data))
        {
            Title = "Post-processing",
            Subtitle = "Choose providers, edit models, fallback and prompts",
            Icon = new IconInfo("\uE8D2"),
        },
        new ListItem(new CopyLastTranscriptCommand(_data))
        {
            Title = "Copy last transcript",
            Subtitle = "Copy the newest Handy transcript to the clipboard",
            Icon = new IconInfo("\uE8C8"),
        },
        new ListItem(new HandyCommand("Open Handy", null, _configuredPath))
        {
            Title = "Open Handy",
            Subtitle = "Show the Handy window",
            Icon = new IconInfo("\uE8A7"),
        },
    ];
}
