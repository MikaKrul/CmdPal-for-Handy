using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandyExtension.Tests;

/// <summary>
/// Robustness tests for reading and writing Handy's local files: the SQLite
/// history database, the JSON settings store, and recorded audio file names.
/// These matter because the extension depends on Handy's internal file
/// structure, which may change in future Handy versions.
/// </summary>
[TestClass]
public sealed class HandyLocalDataTests
{
    private string _directory = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"HandyLocalData tests {Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string DataDirectory
    {
        get
        {
            var path = Path.Combine(_directory, "data");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    private string SettingsStorePath => Path.Combine(DataDirectory, "settings_store.json");

    private static HandyLocalData CreateData(string dataDirectory) => new(() => null, dataDirectory);

    private sealed record HistoryRow(
        long Id,
        string FileName,
        long Timestamp,
        bool Saved,
        string? Title,
        string? Text,
        string? PostProcessed = null);

    private static void WriteHistoryDatabase(string dataDirectory, bool withPostProcessedColumn, params HistoryRow[] rows)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "history.db"),
            Pooling = false,
        }.ToString());
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = withPostProcessedColumn
                ? "CREATE TABLE transcription_history (id INTEGER PRIMARY KEY, file_name TEXT NOT NULL, timestamp INTEGER NOT NULL, saved INTEGER NOT NULL, title TEXT, transcription_text TEXT, post_processed_text TEXT)"
                : "CREATE TABLE transcription_history (id INTEGER PRIMARY KEY, file_name TEXT NOT NULL, timestamp INTEGER NOT NULL, saved INTEGER NOT NULL, title TEXT, transcription_text TEXT)";
            create.ExecuteNonQuery();
        }

        foreach (var row in rows)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = withPostProcessedColumn
                ? "INSERT INTO transcription_history (id, file_name, timestamp, saved, title, transcription_text, post_processed_text) VALUES ($id, $fileName, $timestamp, $saved, $title, $text, $postProcessed)"
                : "INSERT INTO transcription_history (id, file_name, timestamp, saved, title, transcription_text) VALUES ($id, $fileName, $timestamp, $saved, $title, $text)";
            insert.Parameters.AddWithValue("$id", row.Id);
            insert.Parameters.AddWithValue("$fileName", row.FileName);
            insert.Parameters.AddWithValue("$timestamp", row.Timestamp);
            insert.Parameters.AddWithValue("$saved", row.Saved ? 1L : 0L);
            insert.Parameters.AddWithValue("$title", (object?)row.Title ?? DBNull.Value);
            insert.Parameters.AddWithValue("$text", (object?)row.Text ?? DBNull.Value);
            if (withPostProcessedColumn)
            {
                insert.Parameters.AddWithValue("$postProcessed", (object?)row.PostProcessed ?? DBNull.Value);
            }

            insert.ExecuteNonQuery();
        }
    }

    private static HistoryRow[] ManyRows(int count) =>
        [.. Enumerable.Range(1, count).Select(index => new HistoryRow(index, $"rec{index}.wav", 1_700_000_000L + index, Saved: true, $"Row {index}", $"Transcript number {index}"))];

    [TestMethod]
    public void ReadHistoryPagesFiftyEntriesAndReportsMore()
    {
        var dataDirectory = DataDirectory;
        WriteHistoryDatabase(dataDirectory, withPostProcessedColumn: false, ManyRows(55));
        var data = CreateData(dataDirectory);

        var firstPage = data.ReadHistory(null, out var hasMore);

        Assert.AreEqual(50, firstPage.Count);
        Assert.IsTrue(hasMore);
        Assert.AreEqual(55, firstPage[0].Id);
        Assert.IsNull(firstPage[0].PostProcessedText);

        var secondPage = data.ReadHistory(firstPage[^1].Id, out var secondHasMore);

        Assert.AreEqual(5, secondPage.Count);
        Assert.IsFalse(secondHasMore);
        Assert.AreEqual(5, secondPage[0].Id);
    }

    [TestMethod]
    public void ReadHistoryFindsTermBeyondVisiblePreview()
    {
        var filler = string.Join(' ', Enumerable.Repeat("meeting notes about quarterly targets", 10));
        var text = filler + " and then a xylophone appeared at the end of the story";
        Assert.IsFalse(text[..100].Contains("xylophone", StringComparison.Ordinal));

        var dataDirectory = DataDirectory;
        WriteHistoryDatabase(dataDirectory, withPostProcessedColumn: false,
            new HistoryRow(1, "long.wav", 1_700_000_001L, true, "Long recording", text),
            new HistoryRow(2, "short.wav", 1_700_000_002L, true, "Short recording", "hello world"));
        var data = CreateData(dataDirectory);

        var matches = data.ReadHistory(null, out _, "xylophone");

        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual(1, matches[0].Id);
        Assert.IsTrue(matches[0].TranscriptionText.Contains("xylophone", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ReadHistoryMatchesPostProcessedText()
    {
        var dataDirectory = DataDirectory;
        WriteHistoryDatabase(dataDirectory, withPostProcessedColumn: true,
            new HistoryRow(1, "a.wav", 1_700_000_001L, true, "Raw only", "the raw transcription text", null),
            new HistoryRow(2, "b.wav", 1_700_000_002L, true, "Polished", "the raw transcription text", "the polished text mentions a xylophone"));
        var data = CreateData(dataDirectory);

        var matches = data.ReadHistory(null, out _, "xylophone");

        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual(2, matches[0].Id);
        Assert.AreEqual("the polished text mentions a xylophone", matches[0].PostProcessedText);
    }

    [TestMethod]
    public void ReadHistoryTreatsLikeWildcardsLiterally()
    {
        var dataDirectory = DataDirectory;
        WriteHistoryDatabase(dataDirectory, withPostProcessedColumn: false,
            new HistoryRow(1, "growth.wav", 1_700_000_001L, true, "Growth", "quarterly 100%_growth was reported and C:\\recordings was backed up"));
        var data = CreateData(dataDirectory);

        Assert.AreEqual(1, data.ReadHistory(null, out _, "100%_growth").Count);
        Assert.AreEqual(1, data.ReadHistory(null, out _, "C:\\recordings").Count);

        // Without escaping, these patterns would match through SQL wildcards.
        Assert.AreEqual(0, data.ReadHistory(null, out _, "1_0%_growth").Count);
        Assert.AreEqual(0, data.ReadHistory(null, out _, "100%growth").Count);
    }

    [TestMethod]
    public void ReadHistoryMissingDatabaseReturnsEmpty()
    {
        var data = CreateData(DataDirectory);

        var entries = data.ReadHistory(null, out var hasMore, "anything");

        Assert.AreEqual(0, entries.Count);
        Assert.IsFalse(hasMore);
    }

    [TestMethod]
    public void ReadHistoryCorruptDatabaseThrowsSqliteException()
    {
        var dataDirectory = DataDirectory;
        File.WriteAllText(Path.Combine(dataDirectory, "history.db"), "this is not a sqlite database");
        var data = CreateData(dataDirectory);

        Assert.ThrowsExactly<SqliteException>(() => data.ReadHistory(null, out _));
    }

    [TestMethod]
    public void ReadHistoryMissingColumnThrowsSqliteException()
    {
        var dataDirectory = DataDirectory;
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "history.db"),
            Pooling = false,
        }.ToString()))
        {
            connection.Open();
            using (var create = connection.CreateCommand())
            {
                create.CommandText = "CREATE TABLE transcription_history (id INTEGER PRIMARY KEY, file_name TEXT, timestamp INTEGER, saved INTEGER)";
                create.ExecuteNonQuery();
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO transcription_history (id, file_name, timestamp, saved) VALUES (1, 'a.wav', 1700000000, 1)";
            insert.ExecuteNonQuery();
        }

        var data = CreateData(dataDirectory);
        Assert.ThrowsExactly<SqliteException>(() => data.ReadHistory(null, out _));
    }

    [TestMethod]
    public void ReadHistoryReadsNullCellsAsEmptyText()
    {
        var dataDirectory = DataDirectory;
        WriteHistoryDatabase(dataDirectory, withPostProcessedColumn: true,
            new HistoryRow(1, "a.wav", 1_700_000_001L, true, null, null, null));
        var data = CreateData(dataDirectory);

        var entries = data.ReadHistory(null, out _);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(string.Empty, entries[0].Title);
        Assert.AreEqual(string.Empty, entries[0].TranscriptionText);
        Assert.IsNull(entries[0].PostProcessedText);
    }

    [TestMethod]
    public void ReadCustomWordsMissingFileReturnsEmpty()
    {
        var data = CreateData(DataDirectory);

        Assert.AreEqual(0, data.ReadCustomWords().Count);
    }

    [TestMethod]
    public void CorruptSettingsFileThrowsJsonException()
    {
        var dataDirectory = DataDirectory;
        var storePath = Path.Combine(dataDirectory, "settings_store.json");
        File.WriteAllText(storePath, "{ this is not json");
        var data = CreateData(dataDirectory);

        // JsonReaderException derives from JsonException, which is what the UI catches.
        Assert.Throws<JsonException>(() => data.ReadSelectedMicrophone());
        Assert.Throws<JsonException>(() => data.AddCustomWord("hello"));
        Assert.AreEqual("{ this is not json", File.ReadAllText(storePath));
    }

    [TestMethod]
    public void SettingsWithoutSettingsObjectThrowsInvalidData()
    {
        var dataDirectory = DataDirectory;
        var storePath = Path.Combine(dataDirectory, "settings_store.json");
        File.WriteAllText(storePath, "{\"something_else\": {}}");
        var data = CreateData(dataDirectory);

        Assert.ThrowsExactly<InvalidDataException>(() => data.ReadSelectedMicrophone());
        Assert.ThrowsExactly<InvalidDataException>(() => data.AddCustomWord("hello"));
        Assert.AreEqual("{\"something_else\": {}}", File.ReadAllText(storePath));
    }

    [TestMethod]
    public void AddCustomWordOnMissingSettingsFileThrows()
    {
        var data = CreateData(DataDirectory);

        Assert.ThrowsExactly<FileNotFoundException>(() => data.AddCustomWord("hello"));
    }

    [TestMethod]
    public void AddCustomWordWritesBackupOfPreviousSettings()
    {
        var dataDirectory = DataDirectory;
        var storePath = SettingsStorePath;
        var originalContents = "{\"settings\":{\"custom_words\":[\"existing\"]}}";
        File.WriteAllText(storePath, originalContents);
        var data = CreateData(dataDirectory);

        data.AddCustomWord("hello");

        var updated = File.ReadAllText(storePath);
        Assert.IsTrue(updated.Contains("hello"));
        Assert.IsTrue(updated.Contains("existing"));

        var backupPath = Path.Combine(dataDirectory, "settings_store.json.HandyExtension.bak");
        Assert.IsTrue(File.Exists(backupPath));
        var backup = File.ReadAllText(backupPath);
        Assert.AreEqual(originalContents, backup);
        Assert.IsFalse(backup.Contains("hello"));
    }

    [TestMethod]
    public void UpdateSettingsRefusesWhenFileChangedAfterRead()
    {
        var dataDirectory = DataDirectory;
        var storePath = SettingsStorePath;
        File.WriteAllText(storePath, "{\"settings\":{\"custom_words\":[]}}");
        var data = CreateData(dataDirectory);

        Assert.ThrowsExactly<IOException>(() => data.UpdateSettings(settings =>
        {
            File.WriteAllText(storePath, "{\"settings\":{\"injected_by_other_writer\":true}}");
            settings["custom_words"] = new JsonArray("hello");
        }));

        var finalContents = File.ReadAllText(storePath);
        Assert.IsTrue(finalContents.Contains("injected_by_other_writer"));
        Assert.IsFalse(finalContents.Contains("hello"));
        Assert.AreEqual(0, Directory.GetFiles(dataDirectory, "*.tmp").Length);
        Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, "settings_store.json.HandyExtension.bak")));
    }

    [TestMethod]
    public void FindRecordingReturnsFileInsideRecordingsFolder()
    {
        var dataDirectory = DataDirectory;
        var recordings = Path.Combine(dataDirectory, "recordings");
        Directory.CreateDirectory(recordings);
        var audioPath = Path.Combine(recordings, "a.wav");
        File.WriteAllText(audioPath, "audio");
        var data = CreateData(dataDirectory);

        var found = data.FindRecording(new HandyHistoryEntry(1, "a.wav", 1_700_000_000L, true, "Title", "Text", null));

        Assert.AreEqual(audioPath, found);
    }

    [TestMethod]
    public void FindRecordingRejectsUnsafeAndMissingFileNames()
    {
        var dataDirectory = DataDirectory;
        var recordings = Path.Combine(dataDirectory, "recordings");
        Directory.CreateDirectory(recordings);
        File.WriteAllText(Path.Combine(recordings, "present.wav"), "audio");
        var data = CreateData(dataDirectory);

        var entry = new HandyHistoryEntry(1, "present.wav", 1_700_000_000L, true, "Title", "Text", null);

        Assert.IsNull(data.FindRecording(entry with { FileName = "..\\..\\evil.wav" }));
        Assert.IsNull(data.FindRecording(entry with { FileName = "C:\\Windows\\evil.wav" }));
        Assert.IsNull(data.FindRecording(entry with { FileName = "missing.wav" }));
        Assert.IsNotNull(data.FindRecording(entry));
    }
}
