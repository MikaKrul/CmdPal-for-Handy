using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace HandyExtension;

internal sealed record HandyHistoryEntry(
    long Id,
    string FileName,
    long Timestamp,
    bool Saved,
    string Title,
    string TranscriptionText,
    string? PostProcessedText)
{
    public string TextToCopy => string.IsNullOrWhiteSpace(PostProcessedText) ? TranscriptionText : PostProcessedText;
}

internal sealed record HandyPostProcessingProvider(
    string Id,
    string Label,
    string BaseUrl,
    string ApiFormat,
    string Model,
    string FallbackModel,
    bool IsActive);

internal sealed record HandyPostProcessingPrompt(string Id, string Name, string Prompt, bool IsSelected);

internal sealed record HandyPostProcessingSettings(
    bool Enabled,
    bool FallbackEnabled,
    IReadOnlyList<HandyPostProcessingProvider> Providers,
    IReadOnlyList<HandyPostProcessingPrompt> Prompts);

internal sealed class HandyLocalData
{
    private const int HistoryPageSize = 50;
    private const string HandyIdentifier = "com.pais.handy";
    private const string PortableMarkerContents = "Handy Portable Mode";
    private readonly Func<string?> _configuredPath;

    public HandyLocalData(Func<string?> configuredPath)
    {
        _configuredPath = configuredPath;
    }

    public IReadOnlyList<string> ReadCustomWords()
    {
        var storePath = GetSettingsStorePath();
        if (!File.Exists(storePath))
        {
            return Array.Empty<string>();
        }

        var root = JsonNode.Parse(File.ReadAllText(storePath)) as JsonObject;
        var words = root?["settings"]?["custom_words"] as JsonArray;
        return words?
            .Select(node => node?.GetValue<string>())
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word!)
            .ToArray() ?? Array.Empty<string>();
    }

    public IReadOnlyList<HandyHistoryEntry> ReadHistory(long? beforeId, out bool hasMore)
    {
        var historyPath = Path.Combine(GetDataDirectory(), "history.db");
        if (!File.Exists(historyPath))
        {
            hasMore = false;
            return Array.Empty<HandyHistoryEntry>();
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = historyPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
            Pooling = false,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        var hasPostProcessedText = false;
        using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText = "PRAGMA table_info(transcription_history)";
            using var schemaReader = schemaCommand.ExecuteReader();
            while (schemaReader.Read())
            {
                hasPostProcessedText |= string.Equals(schemaReader.GetString(1), "post_processed_text", StringComparison.OrdinalIgnoreCase);
            }
        }

        var postProcessedColumn = hasPostProcessedText ? "post_processed_text" : "NULL";
        command.CommandText = beforeId.HasValue
            ? $"SELECT id, file_name, timestamp, saved, title, transcription_text, {postProcessedColumn} FROM transcription_history WHERE id < $beforeId ORDER BY id DESC LIMIT $limit"
            : $"SELECT id, file_name, timestamp, saved, title, transcription_text, {postProcessedColumn} FROM transcription_history ORDER BY id DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit", HistoryPageSize + 1);
        if (beforeId.HasValue)
        {
            command.Parameters.AddWithValue("$beforeId", beforeId.Value);
        }

        var entries = new List<HandyHistoryEntry>(HistoryPageSize + 1);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new HandyHistoryEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetBoolean(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        hasMore = entries.Count > HistoryPageSize;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        return entries;
    }

    public string? FindRecording(HandyHistoryEntry entry)
    {
        var fileName = Path.GetFileName(entry.FileName);
        if (!string.Equals(fileName, entry.FileName, StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(GetDataDirectory(), "recordings", fileName));
        var recordingsDirectory = Path.GetFullPath(Path.Combine(GetDataDirectory(), "recordings") + Path.DirectorySeparatorChar);
        if (!path.StartsWith(recordingsDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return null;
        }

        return path;
    }

    public void AddCustomWord(string word)
    {
        UpdateCustomWords(words =>
        {
            if (words.Contains(word, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"'{word}' is already in Handy's custom words.");
            }

            words.Add(word);
            return words;
        });
    }

    public void RemoveCustomWord(string word)
    {
        UpdateCustomWords(words =>
        {
            words.RemoveAll(existing => string.Equals(existing, word, StringComparison.Ordinal));
            return words;
        });
    }

    public HandyPostProcessingSettings ReadPostProcessingSettings()
    {
        var settings = ReadSettingsObject();
        var providerId = settings["post_process_provider_id"]?.GetValue<string>() ?? "openai";
        var models = settings["post_process_models"] as JsonObject ?? new JsonObject();
        var fallbackModels = settings["post_process_fallback_models"] as JsonObject ?? new JsonObject();
        var providers = (settings["post_process_providers"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(provider =>
            {
                var id = provider["id"]?.GetValue<string>() ?? string.Empty;
                return new HandyPostProcessingProvider(
                    id,
                    provider["label"]?.GetValue<string>() ?? id,
                    provider["base_url"]?.GetValue<string>() ?? string.Empty,
                    provider["api_format"]?.GetValue<string>() ?? "openai",
                    models[id]?.GetValue<string>() ?? string.Empty,
                    fallbackModels[id]?.GetValue<string>() ?? string.Empty,
                    string.Equals(id, providerId, StringComparison.Ordinal));
            })
            .Where(provider => provider.Id.Length > 0)
            .ToArray();
        var selectedPromptId = settings["post_process_selected_prompt_id"]?.GetValue<string>();
        var prompts = (settings["post_process_prompts"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(prompt => new HandyPostProcessingPrompt(
                prompt["id"]?.GetValue<string>() ?? string.Empty,
                prompt["name"]?.GetValue<string>() ?? string.Empty,
                prompt["prompt"]?.GetValue<string>() ?? string.Empty,
                string.Equals(prompt["id"]?.GetValue<string>(), selectedPromptId, StringComparison.Ordinal)))
            .Where(prompt => prompt.Id.Length > 0)
            .ToArray();

        return new HandyPostProcessingSettings(
            settings["post_process_enabled"]?.GetValue<bool>() ?? false,
            settings["post_process_fallback_enabled"]?.GetValue<bool>() ?? false,
            providers,
            prompts);
    }

    public void SetActivePostProcessingProvider(string providerId) => UpdateSettings(settings =>
    {
        EnsureProviderExists(settings, providerId);
        settings["post_process_provider_id"] = providerId;
    });

    public void SetPostProcessingEnabled(bool enabled) => UpdateSettings(settings => settings["post_process_enabled"] = enabled);

    public void SetFallbackEnabled(bool enabled) => UpdateSettings(settings => settings["post_process_fallback_enabled"] = enabled);

    public void UpdatePostProcessingProvider(string providerId, string model, string fallbackModel, string baseUrl, string apiFormat) => UpdateSettings(settings =>
    {
        var provider = EnsureProviderExists(settings, providerId);
        EnsureProviderModelMap(settings, "post_process_models")[providerId] = model;
        EnsureProviderModelMap(settings, "post_process_fallback_models")[providerId] = fallbackModel;
        if (string.Equals(providerId, "custom", StringComparison.Ordinal))
        {
            provider["base_url"] = baseUrl.Trim();
            provider["api_format"] = apiFormat;
        }
    });

    public void SavePostProcessingPrompt(string? id, string name, string prompt)
    {
        UpdateSettings(settings =>
        {
            var prompts = settings["post_process_prompts"] as JsonArray;
            if (prompts is null)
            {
                prompts = new JsonArray();
                settings["post_process_prompts"] = prompts;
            }
            var promptId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("D") : id;
            var existing = prompts.OfType<JsonObject>().FirstOrDefault(item => string.Equals(item["id"]?.GetValue<string>(), promptId, StringComparison.Ordinal));
            if (existing is null)
            {
                prompts.Add((JsonNode)new JsonObject { ["id"] = promptId, ["name"] = name.Trim(), ["prompt"] = prompt.Trim() });
            }
            else
            {
                existing["name"] = name.Trim();
                existing["prompt"] = prompt.Trim();
            }
        });
    }

    public void SelectPostProcessingPrompt(string? id) => UpdateSettings(settings => settings["post_process_selected_prompt_id"] = id);

    public void RemovePostProcessingPrompt(string id) => UpdateSettings(settings =>
    {
        if (settings["post_process_prompts"] is JsonArray prompts)
        {
            var existing = prompts.OfType<JsonObject>().FirstOrDefault(item => string.Equals(item["id"]?.GetValue<string>(), id, StringComparison.Ordinal));
            if (existing is not null)
            {
                prompts.Remove(existing);
            }
        }

        if (string.Equals(settings["post_process_selected_prompt_id"]?.GetValue<string>(), id, StringComparison.Ordinal))
        {
            settings["post_process_selected_prompt_id"] = null;
        }
    });

    public static string NormalizeCustomWord(string word) =>
        string.Join(' ', word.Replace("<", string.Empty, StringComparison.Ordinal)
            .Replace(">", string.Empty, StringComparison.Ordinal)
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static bool IsHandyRunning()
    {
        var processes = Process.GetProcessesByName("Handy");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private void UpdateCustomWords(Func<List<string>, List<string>> update)
    {
        UpdateSettings(settings =>
        {
            var wordArray = settings["custom_words"] as JsonArray ?? new JsonArray();
            var words = wordArray
                .Select(node => node?.GetValue<string>())
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Select(word => word!)
                .ToList();
            var updatedWords = update(words);
            settings["custom_words"] = new JsonArray(updatedWords
                .Select(word => (JsonNode?)JsonValue.Create(word))
                .ToArray());
        });
    }

    private JsonObject ReadSettingsObject()
    {
        var storePath = GetSettingsStorePath();
        if (!File.Exists(storePath))
        {
            throw new FileNotFoundException("Handy's settings were not found. Open Handy once before managing settings.");
        }

        var root = JsonNode.Parse(File.ReadAllBytes(storePath)) as JsonObject
            ?? throw new InvalidDataException("Handy's settings file is not valid JSON.");
        return root["settings"] as JsonObject
            ?? throw new InvalidDataException("Handy's settings file does not contain the expected settings object.");
    }

    private void UpdateSettings(Action<JsonObject> update)
    {
        if (IsHandyRunning())
        {
            throw new InvalidOperationException("Quit Handy from its tray menu before changing settings, then reopen Handy to apply the change.");
        }

        var settingsPath = GetSettingsStorePath();
        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException("Handy's settings were not found. Open Handy once before managing settings.");
        }

        var originalBytes = File.ReadAllBytes(settingsPath);
        var root = JsonNode.Parse(originalBytes) as JsonObject
            ?? throw new InvalidDataException("Handy's settings file is not valid JSON.");
        var settings = root["settings"] as JsonObject
            ?? throw new InvalidDataException("Handy's settings file does not contain the expected settings object.");
        update(settings);

        var directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("Handy's settings folder could not be located.");
        var temporaryPath = Path.Combine(directory, $"settings_store.{Guid.NewGuid():N}.tmp");
        try
        {
            var jsonBytes = Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(jsonBytes);
                stream.Flush(flushToDisk: true);
            }

            if (IsHandyRunning())
            {
                throw new InvalidOperationException("Handy started while the change was being prepared. Quit Handy and try again.");
            }

            var currentBytes = File.ReadAllBytes(settingsPath);
            if (!originalBytes.SequenceEqual(currentBytes))
            {
                throw new IOException("Handy's settings changed while the update was being prepared. Try again.");
            }

            File.Replace(temporaryPath, settingsPath, null);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static JsonObject EnsureProviderExists(JsonObject settings, string providerId)
    {
        var provider = (settings["post_process_providers"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(item["id"]?.GetValue<string>(), providerId, StringComparison.Ordinal));
        return provider ?? throw new InvalidOperationException("That post-processing provider is not configured in Handy.");
    }

    private static JsonObject EnsureProviderModelMap(JsonObject settings, string key)
    {
        if (settings[key] is JsonObject map)
        {
            return map;
        }

        var newMap = new JsonObject();
        settings[key] = newMap;
        return newMap;
    }

    private string GetSettingsStorePath() => Path.Combine(GetDataDirectory(), "settings_store.json");

    private string GetDataDirectory()
    {
        var executable = HandyExecutableLocator.Find(_configuredPath());
        if (executable is not null)
        {
            var executableDirectory = Path.GetDirectoryName(executable);
            if (!string.IsNullOrEmpty(executableDirectory))
            {
                var markerPath = Path.Combine(executableDirectory, "portable");
                var portableDataPath = Path.Combine(executableDirectory, "Data");
                var validMarker = File.Exists(markerPath) && File.ReadAllText(markerPath).Trim().StartsWith(PortableMarkerContents, StringComparison.Ordinal);
                if (validMarker || (File.Exists(markerPath) && Directory.Exists(portableDataPath)))
                {
                    return portableDataPath;
                }
            }
        }

        var roamingData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(roamingData))
        {
            throw new DirectoryNotFoundException("The Windows application data folder could not be located.");
        }

        return Path.Combine(roamingData, HandyIdentifier);
    }
}

