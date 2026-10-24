// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

public partial class HandyExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
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

        _commands =
        [
            new CommandItem(new HandyHomePage(() => _settings.GetSetting<string>("handyExecutablePath")))
            {
                Title = "Handy",
                Subtitle = "Transcription, history, and custom words",
                Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png"),
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}
