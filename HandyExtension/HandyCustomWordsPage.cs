using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

internal sealed partial class HandyCustomWordsPage : ListPage
{
    private readonly HandyLocalData _data;

    public HandyCustomWordsPage(HandyLocalData data)
    {
        _data = data;
        Name = "Custom words";
        Title = "Handy custom words";
        Icon = new IconInfo("\uE8D2");
        PlaceholderText = "Search your custom words";
    }

    public override IListItem[] GetItems()
    {
        try
        {
            var items = new[]
            {
                new ListItem(new AddHandyCustomWordPage(_data))
                {
                    Title = "Add a custom word",
                    Subtitle = "Add a word or phrase to Handy's vocabulary",
                    Icon = new IconInfo("\uE710"),
                },
            }.ToList();

            foreach (var word in _data.ReadCustomWords())
            {
                items.Add(new ListItem(new NoOpCommand())
                {
                    Title = word,
                    Subtitle = "Open More commands to remove this word",
                    Icon = new IconInfo("\uE8D2"),
                    MoreCommands =
                    [
                        new CommandContextItem(new RemoveHandyCustomWordCommand(_data, word))
                        {
                            Title = "Remove custom word",
                            Subtitle = $"Remove '{word}' from Handy's vocabulary",
                        },
                    ],
                });
            }

            return items.ToArray();
        }
        catch
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Handy's settings are unavailable",
                    Subtitle = "Open Handy once, then return here.",
                },
            ];
        }
    }
}

internal sealed partial class AddHandyCustomWordPage : ContentPage
{
    private readonly AddHandyCustomWordForm _form;

    public AddHandyCustomWordPage(HandyLocalData data)
    {
        _form = new AddHandyCustomWordForm(data);
        Name = "Add custom word";
        Title = "Add a custom word";
        Icon = new IconInfo("\uE710");
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class AddHandyCustomWordForm : FormContent
{
    private readonly HandyLocalData _data;

    public AddHandyCustomWordForm(HandyLocalData data)
    {
        _data = data;
        TemplateJson = """
        {
          "type": "AdaptiveCard",
          "version": "1.5",
          "body": [
            {
              "type": "Input.Text",
              "id": "word",
              "label": "Word or phrase",
              "placeholder": "For example, Handy",
              "maxLength": 50,
              "isRequired": true,
              "errorMessage": "Enter a word or phrase."
            }
          ],
          "actions": [
            {
              "type": "Action.Submit",
              "title": "Add to Handy"
            }
          ]
        }
        """;
    }

    public override CommandResult SubmitForm(string payload)
    {
        try
        {
            using var input = JsonDocument.Parse(payload);
            var rawWord = input.RootElement.TryGetProperty("word", out var wordElement)
                ? wordElement.GetString()
                : null;
            var word = HandyLocalData.NormalizeCustomWord(rawWord ?? string.Empty);
            if (word.Length == 0 || word.Length > 50)
            {
                return CommandResult.ShowToast("Enter a word or phrase with 1 to 50 characters.");
            }

            _data.AddCustomWord(word);
            return CommandResult.ShowToast($"Added '{word}'. Reopen Handy to use it.");
        }
        catch (InvalidOperationException ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CommandResult.ShowToast("Handy's settings could not be updated. Check the settings file and try again.");
        }
    }
}

internal sealed partial class RemoveHandyCustomWordCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly string _word;

    public RemoveHandyCustomWordCommand(HandyLocalData data, string word)
    {
        _data = data;
        _word = word;
        Name = "Remove custom word";
        Icon = new IconInfo("\uE74D");
    }

    public override CommandResult Invoke()
    {
        try
        {
            _data.RemoveCustomWord(_word);
            return CommandResult.ShowToast($"Removed '{_word}'. Reopen Handy to apply the change.");
        }
        catch (InvalidOperationException ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CommandResult.ShowToast("Handy's settings could not be updated. Check the settings file and try again.");
        }
    }
}
