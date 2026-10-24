using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

internal sealed partial class HandyHistoryPage : ListPage
{
    private readonly HandyLocalData _data;
    private readonly Func<string?> _configuredPath;
    private readonly long? _beforeId;

    public HandyHistoryPage(HandyLocalData data, Func<string?> configuredPath, long? beforeId = null)
    {
        _data = data;
        _configuredPath = configuredPath;
        _beforeId = beforeId;
        Name = "History";
        Title = "Handy history";
        Icon = new IconInfo("\uE81C");
        PlaceholderText = "Search your Handy history";
    }

    public override IListItem[] GetItems()
    {
        try
        {
            var entries = _data.ReadHistory(_beforeId, out var hasMore);
            if (entries.Count == 0)
            {
                return MessageSection("No saved recordings", "Handy's history database has no more entries.");
            }

            var items = entries.Select(entry => new ListItem(new PlayHistoryRecordingCommand(_data, entry))
            {
                Title = string.IsNullOrWhiteSpace(entry.Title) ? FormatTimestamp(entry.Timestamp) : entry.Title,
                Subtitle = Preview(entry.TranscriptionText),
                Icon = new IconInfo("\uE714"),
                MoreCommands =
                [
                    new CommandContextItem(new HandyHistoryDetailPage(_data, entry))
                    {
                        Title = "View transcript",
                        Subtitle = "Read the saved transcription",
                    },
                    new CommandContextItem(new PlayHistoryRecordingCommand(_data, entry))
                    {
                        Title = "Play recording",
                        Subtitle = "Open the saved audio in your default player",
                    },
                ],
            }).ToList();

            if (hasMore)
            {
                items.Add(new ListItem(new HandyHistoryPage(_data, _configuredPath, entries[^1].Id))
                {
                    Title = "Load older history",
                    Subtitle = "Show the next page of saved recordings",
                    Icon = new IconInfo("\uE72A"),
                });
            }

            return items.ToArray();
        }
        catch
        {
            return MessageSection("History is unavailable", "Open Handy once and try again. History stays on this PC.");
        }
    }

    private static IListItem[] MessageSection(string title, string subtitle) =>
        [new ListItem(new NoOpCommand()) { Title = title, Subtitle = subtitle }];

    private static string FormatTimestamp(long timestamp)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return "Handy recording";
        }
    }

    private static string Preview(string text)
    {
        var preview = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return preview.Length <= 100 ? preview : preview[..97] + "\u2026";
    }
}

internal sealed partial class PlayHistoryRecordingCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly HandyHistoryEntry _entry;

    public PlayHistoryRecordingCommand(HandyLocalData data, HandyHistoryEntry entry)
    {
        _data = data;
        _entry = entry;
        Name = "Play recording";
        Icon = new IconInfo("\uE768");
    }

    public override CommandResult Invoke()
    {
        try
        {
            var path = _data.FindRecording(_entry);
            if (path is null)
            {
                return CommandResult.ShowToast("Handy's audio file is no longer available. Check Handy's recording retention settings.");
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return CommandResult.Hide();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return CommandResult.ShowToast("Handy could not open this recording.");
        }
    }
}

internal sealed partial class HandyHistoryDetailPage : ContentPage
{
    private readonly HandyHistoryEntry _entry;

    public HandyHistoryDetailPage(HandyLocalData data, HandyHistoryEntry entry)
    {
        _entry = entry;
        Title = string.IsNullOrWhiteSpace(entry.Title) ? "Handy transcript" : entry.Title;
        Name = "Transcript";
        Icon = new IconInfo("\uE8A5");
        Commands =
        [
            new CommandContextItem(new PlayHistoryRecordingCommand(data, entry))
            {
                Title = "Play recording",
                Subtitle = "Open the saved audio in your default player",
            },
        ];
    }

    public override IContent[] GetContent()
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(_entry.Timestamp).ToLocalTime().ToString("f", CultureInfo.CurrentCulture);
        var transcript = string.IsNullOrWhiteSpace(_entry.TranscriptionText)
            ? "No transcription text was saved for this recording."
            : _entry.TranscriptionText;
        return
        [
            new MarkdownContent($"**{timestamp}**\n\n{transcript}"),
        ];
    }
}
