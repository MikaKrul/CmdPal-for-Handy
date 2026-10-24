using System;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

/// <summary>
/// The extension's home page. Which commands appear here is driven by the menu layout
/// in HandyMenuConfig: every command placed on the home page shows up here, the rest
/// live on the More page, and hidden commands only appear in Customize Menu.
/// </summary>
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

    internal void RaiseMenuLayoutChanged() => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var placements = HandyMenuConfig.Load();
        var items = HandyMenuItems.Items
            .Where(definition => placements.TryGetValue(definition.Id, out var placement) && placement == HandyMenuPlacement.Home)
            .OrderBy(definition => definition.Order)
            .Select(definition => new ListItem(HandyMenuRegistry.CreateCommand(definition, _data, _configuredPath))
            {
                Title = definition.Title,
                Subtitle = definition.Subtitle,
                Icon = HandyMenuRegistry.Icon(definition),
                MoreCommands = HandyMenuContextItems.MoveOrHide(definition, RaiseMenuLayoutChanged),
            })
            .ToList();

        items.Add(new ListItem(new HandyCustomizeMenuPage())
        {
            Title = "Customize Menu",
            Subtitle = "Choose what appears on the home and More pages",
            Icon = new IconInfo("\uE713"),
        });
        items.Add(new ListItem(new HandyMorePage(_data, _configuredPath))
        {
            Title = "More",
            Subtitle = "The remaining Handy commands",
            Icon = new IconInfo("\uE712"),
        });

        return [.. items];
    }
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

    internal void RaiseMenuLayoutChanged() => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var placements = HandyMenuConfig.Load();
        var items = HandyMenuItems.Items
            .Where(definition => placements.TryGetValue(definition.Id, out var placement) && placement == HandyMenuPlacement.More)
            .OrderBy(definition => definition.Order)
            .Select(definition => new ListItem(HandyMenuRegistry.CreateCommand(definition, _data, _configuredPath))
            {
                Title = definition.Title,
                Subtitle = definition.Subtitle,
                Icon = HandyMenuRegistry.Icon(definition),
                MoreCommands = HandyMenuContextItems.ShowOrHide(definition, RaiseMenuLayoutChanged),
            })
            .ToList();

        items.Add(new ListItem(new HandyCustomizeMenuPage())
        {
            Title = "Customize Menu",
            Subtitle = "Choose what appears on the home and More pages",
            Icon = new IconInfo("\uE713"),
        });

        return [.. items];
    }
}
