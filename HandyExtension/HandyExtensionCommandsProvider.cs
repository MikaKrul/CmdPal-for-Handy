// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

public partial class HandyExtensionCommandsProvider : CommandProvider
{
    private readonly CommandItem _homePageItem;
    private readonly HandyLocalData _data;
    private readonly Func<string?> _configuredPath;
    private readonly Settings _settings;

    public HandyExtensionCommandsProvider()
    {
        DisplayName = "Handy";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _settings = new Settings();
        _settings.Add(new TextSetting(
            "handyExecutablePath",
            "Handy executable path",
            "Optional full path to handy.exe. Leave blank to search PATH and common install folders.",
            string.Empty));
        Settings = _settings;

        _configuredPath = () => _settings.GetSetting<string>("handyExecutablePath");
        _data = new HandyLocalData(_configuredPath);
        _homePageItem = new CommandItem(new HandyHomePage(_configuredPath))
        {
            Title = "Handy",
            Subtitle = "Transcription, history, and custom words",
            Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png"),
        };

        // The command list depends on the saved menu layout, so it can change at runtime.
        Frozen = false;
        HandyMenuConfig.PlacementChanged += RefreshTopLevelCommands;
    }

    private void RefreshTopLevelCommands() => RaiseItemsChanged();

    public override ICommandItem[] TopLevelCommands()
    {
        var commands = new List<ICommandItem> { _homePageItem };
        try
        {
            var placements = HandyMenuConfig.Load();
            commands.AddRange(HandyMenuItems.Items
                .Where(definition => placements.TryGetValue(definition.Id, out var placement) && placement == HandyMenuPlacement.TopLevel)
                .OrderBy(definition => definition.Order)
                .Select(definition => new CommandItem(HandyMenuRegistry.CreateCommand(definition, _data, _configuredPath))
                {
                    Title = definition.Title,
                    Subtitle = definition.Subtitle,
                    Icon = HandyMenuRegistry.Icon(definition),
                }));
        }
        catch (Exception)
        {
            // An unreadable menu layout falls back to just the Handy home page command.
        }

        return commands.ToArray();
    }
}
