using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace HandyExtension;

/// <summary>
/// Loads and saves each menu command's placement (home page, More page, or hidden)
/// in a JSON file in %LOCALAPPDATA%\HandyExtension\menu.json. The file only drives
/// the extension's own menu layout; it is not read by Handy.
/// This file has no Command Palette dependencies so the test project can compile it directly.
/// </summary>
internal static class HandyMenuConfig
{
    public static string DefaultStorePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HandyExtension",
        "menu.json");

    public static Dictionary<string, HandyMenuPlacement> Load(string? storePath = null)
    {
        var path = storePath ?? DefaultStorePath;
        var placements = new Dictionary<string, HandyMenuPlacement>();

        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject root)
            {
                foreach (var (id, value) in root)
                {
                    if (HandyMenuItems.Find(id) is not null
                        && TryParsePlacement(value?.GetValue<string>(), out var placement))
                    {
                        placements[id] = placement;
                    }
                }
            }
        }
        catch (Exception)
        {
            // An unreadable or corrupt file falls back to the defaults below.
            placements.Clear();
        }

        foreach (var definition in HandyMenuItems.Items)
        {
            placements.TryAdd(definition.Id, definition.DefaultPlacement);
        }

        return placements;
    }

    public static void Save(Dictionary<string, HandyMenuPlacement> placements, string? storePath = null)
    {
        var path = storePath ?? DefaultStorePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var root = new JsonObject();
        foreach (var (id, placement) in placements.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            root[id] = ToStorageValue(placement);
        }

        File.WriteAllText(path, root.ToJsonString());
    }

    public static void SetPlacement(string id, HandyMenuPlacement placement, string? storePath = null)
    {
        var placements = Load(storePath);
        placements[id] = placement;
        Save(placements, storePath);
    }

    public static void ResetPlacement(string id, string? storePath = null)
    {
        var placements = Load(storePath);
        var placement = HandyMenuItems.Find(id)?.DefaultPlacement ?? HandyMenuPlacement.More;
        placements[id] = placement;
        Save(placements, storePath);
    }

    public static void ResetAll(string? storePath = null)
    {
        var path = storePath ?? DefaultStorePath;
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // A missing or locked file needs no reset.
        }
    }

    public static bool TryParsePlacement(string? value, out HandyMenuPlacement placement)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "home":
                placement = HandyMenuPlacement.Home;
                return true;
            case "more":
                placement = HandyMenuPlacement.More;
                return true;
            case "hidden":
                placement = HandyMenuPlacement.Hidden;
                return true;
            default:
                placement = HandyMenuPlacement.More;
                return false;
        }
    }

    public static string ToStorageValue(HandyMenuPlacement placement) => placement switch
    {
        HandyMenuPlacement.Home => "home",
        HandyMenuPlacement.More => "more",
        _ => "hidden",
    };

    public static string ToPlacementLabel(HandyMenuPlacement placement) => placement switch
    {
        HandyMenuPlacement.Home => "Home page",
        HandyMenuPlacement.More => "More page",
        _ => "Hidden",
    };
}
