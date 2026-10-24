using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

/// <summary>
/// The Search Transcripts page. Unlike a plain ListPage, which the host filters on
/// title and subtitle only, this dynamic page performs its own full-text search:
/// the search term matches anywhere in the title, the raw transcription, or the
/// post-processed text - not just the visible preview.
/// </summary>
internal sealed partial class HandyHistoryPage : DynamicListPage
{
    private readonly HandyLocalData _data;
    private readonly Func<string?> _configuredPath;
    private readonly long? _beforeId;

    public HandyHistoryPage(HandyLocalData data, Func<string?> configuredPath, long? beforeId = null, string? initialSearch = null)
    {
        _data = data;
        _configuredPath = configuredPath;
        _beforeId = beforeId;
        Name = "Search Transcripts";
        Title = "Handy transcript history";
        Icon = new IconInfo("\uE81C");
        PlaceholderText = "Search your Handy history - full transcript text is searched";
        if (!string.IsNullOrWhiteSpace(initialSearch))
        {
            SetSearchNoUpdate(initialSearch);
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        try
        {
            var search = SearchText;
            var hasSearch = !string.IsNullOrWhiteSpace(search);
            var entries = _data.ReadHistory(_beforeId, out var hasMore, search);
            if (entries.Count == 0)
            {
                if (hasSearch)
                {
                    return MessageSection(
                        _beforeId is null ? "No matching transcripts" : "No older matching transcripts",
                        $"No saved transcript contains '{TrimForDisplay(search!)}'.");
                }

                return _beforeId is null
                    ? MessageSection("No saved recordings", "Handy's history database has no more entries.")
                    : MessageSection("No older recordings", "Handy's history database has no older entries.");
            }

            var items = entries.Select(entry => new ListItem(new PlayHistoryRecordingCommand(_data, entry))
            {
                Title = string.IsNullOrWhiteSpace(entry.Title) ? FormatTimestamp(entry.Timestamp) : entry.Title,
                Subtitle = Preview(entry, search),
                Icon = new IconInfo("\uE8A5"),
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
                    new CommandContextItem(new CopyTextCommand(entry.TextToCopy))
                    {
                        Title = "Copy transcript",
                        Subtitle = "Copy this transcript to the clipboard",
                    },
                ],
            }).ToList();

            if (hasMore)
            {
                items.Add(new ListItem(new HandyHistoryPage(_data, _configuredPath, entries[^1].Id, search))
                {
                    Title = "Load older history",
                    Subtitle = hasSearch ? "Show older transcripts matching your search" : "Show the next page of saved recordings",
                    Icon = new IconInfo("\uE70D"),
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

    private static string TrimForDisplay(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 40 ? trimmed : trimmed[..37] + "\u2026";
    }

    /// <summary>
    /// Builds the one-line preview. With an active search it centers the window on
    /// the first match (raw or post-processed text), so results are visible even
    /// when the found word sits deep inside a long transcript.
    /// </summary>
    private static string Preview(HandyHistoryEntry entry, string? search)
    {
        var text = entry.TranscriptionText;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var index = IndexOf(text, search);
            if (index < 0 && entry.PostProcessedText is { } processed)
            {
                var processedIndex = IndexOf(processed, search);
                if (processedIndex >= 0)
                {
                    text = processed;
                    index = processedIndex;
                }
            }

            if (index >= 0)
            {
                var windowStart = Math.Max(0, index - 40);
                var windowEnd = Math.Min(text.Length, index + search.Length + 40);
                return (windowStart > 0 ? "\u2026" : string.Empty)
                    + text[windowStart..windowEnd]
                    + (windowEnd < text.Length ? "\u2026" : string.Empty);
            }
        }

        var preview = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return preview.Length <= 100 ? preview : preview[..97] + "\u2026";
    }

    private static int IndexOf(string text, string value) => text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
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
            new CommandContextItem(new CopyTextCommand(entry.TextToCopy))
            {
                Title = "Copy transcript",
                Subtitle = "Copy this transcript to the clipboard",
            },
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
        var transcript = string.IsNullOrWhiteSpace(_entry.TextToCopy)
            ? "No transcription text was saved for this recording."
            : _entry.TextToCopy;
        return
        [
            new MarkdownContent($"**{timestamp}**\n\n{transcript}"),
        ];
    }
}

internal sealed partial class CopyLastTranscriptCommand : InvokableCommand
{
    private readonly HandyLocalData _data;

    public CopyLastTranscriptCommand(HandyLocalData data)
    {
        _data = data;
        Name = "Copy last transcript";
        Icon = new IconInfo("\uE8C8");
    }

    public override CommandResult Invoke()
    {
        try
        {
            var entries = _data.ReadHistory(null, out _, limit: 1);
            var transcript = entries.Count > 0 ? entries[0].TextToCopy : null;
            return string.IsNullOrWhiteSpace(transcript)
                ? CommandResult.ShowToast("Handy has no transcript to copy yet.")
                : (CommandResult)new CopyTextCommand(transcript).Invoke();
        }
        catch
        {
            return CommandResult.ShowToast("Handy's history is unavailable. Open Handy once and try again.");
        }
    }
}
