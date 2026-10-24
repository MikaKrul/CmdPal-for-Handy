using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

/// <summary>
/// The "More commands" (…) actions for menu rows plus the Customize Menu page.
/// Every placement change is saved through HandyMenuConfig and followed by a
/// refresh of the page the row lives on, so the row moves or disappears immediately.
/// </summary>
internal static class HandyMenuContextItems
{
    public static IContextItem[] MoveOrHide(HandyMenuItemDef definition, Action refresh) =>
    [
        new CommandContextItem(new HandyPlacementCommand(definition, HandyMenuPlacement.More, refresh))
        {
            Title = "Move to More page",
            Subtitle = "Show this command on the More page",
        },
        new CommandContextItem(new HandyPlacementCommand(definition, HandyMenuPlacement.Hidden, refresh))
        {
            Title = "Hide from menus",
            Subtitle = "Restore it from Customize Menu",
        },
    ];

    public static IContextItem[] ShowOrHide(HandyMenuItemDef definition, Action refresh) =>
    [
        new CommandContextItem(new HandyPlacementCommand(definition, HandyMenuPlacement.Home, refresh))
        {
            Title = "Show on home page",
            Subtitle = "Pin this command to the home page",
        },
        new CommandContextItem(new HandyPlacementCommand(definition, HandyMenuPlacement.Hidden, refresh))
        {
            Title = "Hide from menus",
            Subtitle = "Restore it from Customize Menu",
        },
    ];

    public static IContextItem[] All(HandyMenuItemDef definition, Action refresh) =>
    [
        new CommandContextItem(new HandyPlacementCommand(definition, HandyMenuPlacement.Home, refresh))
        {
            Title = "Show on home page",
            Subtitle = "Pin this command to the home page",
        },
        new CommandContextItem(new HandyPlacementCommand(definition, HandyMenuPlacement.More, refresh))
        {
            Title = "Move to More page",
            Subtitle = "Show this command on the More page",
        },
        new CommandContextItem(new HandyPlacementCommand(definition, HandyMenuPlacement.Hidden, refresh))
        {
            Title = "Hide from menus",
            Subtitle = "Remove this command from both pages",
        },
        new CommandContextItem(new HandyResetPlacementCommand(definition, refresh))
        {
            Title = "Reset to default",
            Subtitle = "Restore the default placement for this command",
        },
    ];
}

internal sealed partial class HandyPlacementCommand : InvokableCommand
{
    private readonly HandyMenuItemDef _definition;
    private readonly HandyMenuPlacement _placement;
    private readonly Action _refresh;

    public HandyPlacementCommand(HandyMenuItemDef definition, HandyMenuPlacement placement, Action refresh)
    {
        _definition = definition;
        _placement = placement;
        _refresh = refresh;
        Name = placement switch
        {
            HandyMenuPlacement.Home => "Show on home page",
            HandyMenuPlacement.More => "Move to More page",
            _ => "Hide from menus",
        };
    }

    public override ICommandResult Invoke()
    {
        try
        {
            HandyMenuConfig.SetPlacement(_definition.Id, _placement);
        }
        catch (Exception)
        {
            return CommandResult.ShowToast("The menu layout could not be saved. Check menu.json in %LOCALAPPDATA%\\HandyExtension.");
        }

        _refresh();
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class HandyCyclePlacementCommand : InvokableCommand
{
    private readonly HandyMenuItemDef _definition;
    private readonly Action _refresh;

    public HandyCyclePlacementCommand(HandyMenuItemDef definition, Action refresh)
    {
        _definition = definition;
        _refresh = refresh;
        Name = $"Change where '{definition.Title}' appears";
    }

    public override ICommandResult Invoke()
    {
        var placements = HandyMenuConfig.Load();
        placements.TryGetValue(_definition.Id, out var current);
        var next = current switch
        {
            HandyMenuPlacement.Home => HandyMenuPlacement.More,
            HandyMenuPlacement.More => HandyMenuPlacement.Hidden,
            _ => HandyMenuPlacement.Home,
        };

        try
        {
            HandyMenuConfig.SetPlacement(_definition.Id, next);
        }
        catch (Exception)
        {
            return CommandResult.ShowToast("The menu layout could not be saved. Check menu.json in %LOCALAPPDATA%\\HandyExtension.");
        }

        _refresh();
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class HandyResetPlacementCommand : InvokableCommand
{
    private readonly HandyMenuItemDef _definition;
    private readonly Action _refresh;

    public HandyResetPlacementCommand(HandyMenuItemDef definition, Action refresh)
    {
        _definition = definition;
        _refresh = refresh;
        Name = "Reset to default";
    }

    public override ICommandResult Invoke()
    {
        try
        {
            HandyMenuConfig.ResetPlacement(_definition.Id);
        }
        catch (Exception)
        {
            return CommandResult.ShowToast("The menu layout could not be saved. Check menu.json in %LOCALAPPDATA%\\HandyExtension.");
        }

        _refresh();
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class HandyResetMenuCommand : InvokableCommand
{
    private readonly Action _refresh;

    public HandyResetMenuCommand(Action refresh)
    {
        _refresh = refresh;
        Name = "Reset all to defaults";
    }

    public override ICommandResult Invoke()
    {
        try
        {
            HandyMenuConfig.ResetAll();
        }
        catch (Exception)
        {
            return CommandResult.ShowToast("The menu layout could not be reset. Check menu.json in %LOCALAPPDATA%\\HandyExtension.");
        }

        _refresh();
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class HandyCustomizeMenuPage : ListPage
{
    public HandyCustomizeMenuPage()
    {
        Name = "Customize Menu";
        Title = "Customize the Handy menu";
        Icon = new IconInfo("\uE713");
        PlaceholderText = "Select a command to move it to the home page, More page, or hidden";
    }

    internal void RaiseMenuLayoutChanged() => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var placements = HandyMenuConfig.Load();
        var items = HandyMenuItems.Items
            .OrderBy(definition => PlacementGroup(placements, definition))
            .ThenBy(definition => definition.Order)
            .Select(definition => new ListItem(new HandyCyclePlacementCommand(definition, RaiseMenuLayoutChanged))
            {
                Title = definition.Title,
                Subtitle = HandyMenuConfig.ToPlacementLabel(Placement(placements, definition)),
                Icon = HandyMenuRegistry.Icon(definition),
                MoreCommands = HandyMenuContextItems.All(definition, RaiseMenuLayoutChanged),
            })
            .ToList();

        items.Add(new ListItem(new HandyResetMenuCommand(RaiseMenuLayoutChanged))
        {
            Title = "Reset all to defaults",
            Subtitle = "Undo every menu layout change",
            Icon = new IconInfo("\uE74D"),
        });

        return [.. items];
    }

    private static HandyMenuPlacement Placement(Dictionary<string, HandyMenuPlacement> placements, HandyMenuItemDef definition) =>
        placements.TryGetValue(definition.Id, out var placement) ? placement : definition.DefaultPlacement;

    private static int PlacementGroup(Dictionary<string, HandyMenuPlacement> placements, HandyMenuItemDef definition) =>
        (int)Placement(placements, definition);
}
