using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HandyExtension;

internal sealed partial class HandyPostProcessingPage : ListPage
{
    private readonly HandyLocalData _data;

    public HandyPostProcessingPage(HandyLocalData data)
    {
        _data = data;
        Name = "Post-processing";
        Title = "Handy post-processing";
        Icon = new IconInfo("\uE8D2");
    }

    public override IListItem[] GetItems()
    {
        try
        {
            var settings = _data.ReadPostProcessingSettings();
            var items = new System.Collections.Generic.List<IListItem>
            {
                new ListItem(new ToggleHandyPostProcessingCommand(_data, settings.Enabled))
                {
                    Title = settings.Enabled ? "Turn post-processing off" : "Turn post-processing on",
                    Subtitle = "Enable or disable AI post-processing",
                    Icon = new IconInfo("\uE7E8"),
                },
                new ListItem(new ToggleHandyPostProcessingFallbackCommand(_data, settings.FallbackEnabled))
                {
                    Title = settings.FallbackEnabled ? "Turn fallback model off" : "Turn fallback model on",
                    Subtitle = "Enable or disable the fallback model",
                    Icon = new IconInfo("\uE8D2"),
                },
                new ListItem(new HandyPostProcessingPromptsPage(_data))
                {
                    Title = "Prompts",
                    Subtitle = "Create, edit, select, and remove post-processing prompts",
                    Icon = new IconInfo("\uE8A5"),
                },
            };

            items.AddRange(settings.Providers.Select(provider => new ListItem(new HandyPostProcessingProviderPage(_data, provider))
            {
                Title = provider.Label,
                Subtitle = $"{(provider.IsActive ? "Active provider · " : string.Empty)}Model: {EmptyAsUnset(provider.Model)} · Fallback: {EmptyAsUnset(provider.FallbackModel)}",
                Icon = new IconInfo(provider.IsActive ? "\uE8FB" : "\uE8D2"),
                MoreCommands =
                [
                    new CommandContextItem(new SetHandyPostProcessingProviderCommand(_data, provider.Id))
                    {
                        Title = provider.IsActive ? "Current provider" : "Use this provider",
                        Subtitle = $"Select {provider.Label} for post-processing",
                    },
                ],
            })));
            return items.ToArray();
        }
        catch
        {
            return [new ListItem(new NoOpCommand()) { Title = "Handy's settings are unavailable", Subtitle = "Open Handy once, then return here." }];
        }
    }

    private static string EmptyAsUnset(string value) => string.IsNullOrWhiteSpace(value) ? "not set" : value;
}

internal sealed partial class HandyPostProcessingProviderPage : ListPage
{
    private readonly HandyLocalData _data;
    private readonly HandyPostProcessingProvider _provider;

    public HandyPostProcessingProviderPage(HandyLocalData data, HandyPostProcessingProvider provider)
    {
        _data = data;
        _provider = provider;
        Name = "Provider";
        Title = provider.Label;
        Icon = new IconInfo("\uE8D2");
    }

    public override IListItem[] GetItems()
    {
        var items = new System.Collections.Generic.List<IListItem>
        {
            new ListItem(new EditHandyPostProcessingProviderPage(_data, _provider))
            {
                Title = "Edit models and provider settings",
                Subtitle = "Set the main model and fallback model",
                Icon = new IconInfo("\uE70F"),
            },
            new ListItem(new SetHandyPostProcessingProviderCommand(_data, _provider.Id))
            {
                Title = _provider.IsActive ? "Current provider" : "Use this provider",
                Subtitle = "Make this the active post-processing provider",
                Icon = new IconInfo("\uE8FB"),
            },
        };

        if (string.Equals(_provider.Id, "custom", StringComparison.Ordinal))
        {
            items[0] = new ListItem(new EditHandyPostProcessingProviderPage(_data, _provider))
            {
                Title = "Edit model and custom endpoint",
                Subtitle = "Set the model, fallback model, endpoint URL, and API format",
                Icon = new IconInfo("\uE70F"),
            };
        }

        return items.ToArray();
    }
}

internal sealed partial class EditHandyPostProcessingProviderPage : ContentPage
{
    private readonly EditHandyPostProcessingProviderForm _form;

    public EditHandyPostProcessingProviderPage(HandyLocalData data, HandyPostProcessingProvider provider)
    {
        _form = new EditHandyPostProcessingProviderForm(data, provider);
        Name = "Edit provider";
        Title = $"Edit {provider.Label}";
        Icon = new IconInfo("\uE70F");
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class EditHandyPostProcessingProviderForm : FormContent
{
    private readonly HandyLocalData _data;
    private readonly HandyPostProcessingProvider _provider;
    private readonly bool _isCustom;

    public EditHandyPostProcessingProviderForm(HandyLocalData data, HandyPostProcessingProvider provider)
    {
        _data = data;
        _provider = provider;
        _isCustom = string.Equals(provider.Id, "custom", StringComparison.Ordinal);
        var customFields = _isCustom ? """
            ,
            {
              "type": "Input.Text",
              "id": "baseUrl",
              "label": "Base URL",
              "placeholder": "https://example.com/v1",
              "isRequired": true,
              "errorMessage": "Enter the custom provider base URL."
            },
            {
              "type": "Input.ChoiceSet",
              "id": "apiFormat",
              "label": "API format",
              "style": "compact",
              "choices": [
                { "title": "OpenAI compatible", "value": "openai" },
                { "title": "Anthropic Messages", "value": "anthropic" }
              ]
            }
            """ : string.Empty;
        TemplateJson = $$"""
        {
          "type": "AdaptiveCard",
          "version": "1.5",
          "body": [
            { "type": "Input.Text", "id": "model", "label": "Main model", "value": "${model}", "placeholder": "For example, gpt-4o-mini", "isRequired": true },
            { "type": "Input.Text", "id": "fallbackModel", "label": "Fallback model", "value": "${fallbackModel}", "placeholder": "Optional" }
            {{customFields}}
          ],
          "actions": [ { "type": "Action.Submit", "title": "Save provider settings" } ]
        }
        """;
        DataJson = JsonSerializer.Serialize(new { model = provider.Model, fallbackModel = provider.FallbackModel, baseUrl = provider.BaseUrl, apiFormat = provider.ApiFormat });
    }

    public override CommandResult SubmitForm(string payload)
    {
        try
        {
            using var input = JsonDocument.Parse(payload);
            var model = ReadString(input.RootElement, "model").Trim();
            var fallbackModel = ReadString(input.RootElement, "fallbackModel").Trim();
            var baseUrl = _isCustom ? ReadString(input.RootElement, "baseUrl").Trim() : _provider.BaseUrl;
            var apiFormat = _isCustom ? ReadString(input.RootElement, "apiFormat") : _provider.ApiFormat;
            if (model.Length == 0)
            {
                return CommandResult.ShowToast("Enter a model identifier before saving.");
            }

            if (_isCustom && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
            {
                return CommandResult.ShowToast("Enter a valid HTTP or HTTPS base URL.");
            }

            _data.UpdatePostProcessingProvider(_provider.Id, model, fallbackModel, baseUrl, apiFormat);
            return CommandResult.ShowToast("Post-processing settings saved. Reopen Handy to apply them.");
        }
        catch (InvalidOperationException ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CommandResult.ShowToast("Handy's post-processing settings could not be updated.");
        }
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}

internal sealed partial class HandyPostProcessingPromptsPage : ListPage
{
    private readonly HandyLocalData _data;

    public HandyPostProcessingPromptsPage(HandyLocalData data)
    {
        _data = data;
        Name = "Prompts";
        Title = "Post-processing prompts";
        Icon = new IconInfo("\uE8A5");
    }

    public override IListItem[] GetItems()
    {
        try
        {
            var settings = _data.ReadPostProcessingSettings();
            var items = new System.Collections.Generic.List<IListItem>
            {
                new ListItem(new EditHandyPostProcessingPromptPage(_data, null))
                {
                    Title = "Add a prompt",
                    Subtitle = "Create instructions for post-processing",
                    Icon = new IconInfo("\uE710"),
                },
            };

            items.AddRange(settings.Prompts.Select(prompt => new ListItem(new EditHandyPostProcessingPromptPage(_data, prompt))
            {
                Title = prompt.Name,
                Subtitle = prompt.IsSelected ? "Selected prompt · Click to edit" : "Click to edit this prompt",
                Icon = new IconInfo(prompt.IsSelected ? "\uE8FB" : "\uE8A5"),
                MoreCommands =
                [
                    new CommandContextItem(new SelectHandyPostProcessingPromptCommand(_data, prompt.Id))
                    {
                        Title = prompt.IsSelected ? "Selected prompt" : "Use this prompt",
                        Subtitle = "Select the prompt for post-processing",
                    },
                    new CommandContextItem(new RemoveHandyPostProcessingPromptCommand(_data, prompt.Id))
                    {
                        Title = "Remove prompt",
                        Subtitle = "Delete this post-processing prompt",
                    },
                ],
            })));
            return items.ToArray();
        }
        catch
        {
            return [new ListItem(new NoOpCommand()) { Title = "Handy's settings are unavailable", Subtitle = "Open Handy once, then return here." }];
        }
    }
}

internal sealed partial class EditHandyPostProcessingPromptPage : ContentPage
{
    private readonly EditHandyPostProcessingPromptForm _form;

    public EditHandyPostProcessingPromptPage(HandyLocalData data, HandyPostProcessingPrompt? prompt)
    {
        _form = new EditHandyPostProcessingPromptForm(data, prompt);
        Name = prompt is null ? "Add prompt" : "Edit prompt";
        Title = prompt is null ? "Add a prompt" : $"Edit {prompt.Name}";
        Icon = new IconInfo("\uE8A5");
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class EditHandyPostProcessingPromptForm : FormContent
{
    private readonly HandyLocalData _data;
    private readonly string? _id;

    public EditHandyPostProcessingPromptForm(HandyLocalData data, HandyPostProcessingPrompt? prompt)
    {
        _data = data;
        _id = prompt?.Id;
        TemplateJson = """
        {
          "type": "AdaptiveCard",
          "version": "1.5",
          "body": [
            { "type": "Input.Text", "id": "name", "label": "Prompt name", "value": "${name}", "isRequired": true, "maxLength": 80 },
            { "type": "Input.Text", "id": "prompt", "label": "Instructions", "value": "${prompt}", "isMultiline": true, "isRequired": true, "maxLength": 4000, "placeholder": "Describe how Handy should post-process transcripts." }
          ],
          "actions": [ { "type": "Action.Submit", "title": "Save prompt" } ]
        }
        """;
        DataJson = JsonSerializer.Serialize(new { name = prompt?.Name ?? string.Empty, prompt = prompt?.Prompt ?? string.Empty });
    }

    public override CommandResult SubmitForm(string payload)
    {
        try
        {
            using var input = JsonDocument.Parse(payload);
            var name = input.RootElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString()?.Trim() ?? string.Empty : string.Empty;
            var prompt = input.RootElement.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString()?.Trim() ?? string.Empty : string.Empty;
            if (name.Length is 0 or > 80 || prompt.Length is 0 or > 4000)
            {
                return CommandResult.ShowToast("Enter a prompt name and instructions (up to 4,000 characters).");
            }

            _data.SavePostProcessingPrompt(_id, name, prompt);
            return CommandResult.ShowToast("Prompt saved. Reopen Handy to apply it.");
        }
        catch (InvalidOperationException ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CommandResult.ShowToast("Handy's prompt settings could not be updated.");
        }
    }
}

internal sealed partial class ToggleHandyPostProcessingCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly bool _enabled;

    public ToggleHandyPostProcessingCommand(HandyLocalData data, bool enabled)
    {
        _data = data;
        _enabled = enabled;
        Name = enabled ? "Turn post-processing off" : "Turn post-processing on";
    }

    public override CommandResult Invoke() => Save(() => _data.SetPostProcessingEnabled(!_enabled));

    internal static CommandResult Save(Action action)
    {
        try
        {
            action();
            return CommandResult.ShowToast("Post-processing setting saved. Reopen Handy to apply it.");
        }
        catch (InvalidOperationException ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CommandResult.ShowToast("Handy's settings could not be updated.");
        }
    }
}

internal sealed partial class ToggleHandyPostProcessingFallbackCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly bool _enabled;

    public ToggleHandyPostProcessingFallbackCommand(HandyLocalData data, bool enabled)
    {
        _data = data;
        _enabled = enabled;
        Name = enabled ? "Turn fallback model off" : "Turn fallback model on";
    }

    public override CommandResult Invoke() => ToggleHandyPostProcessingCommand.Save(() => _data.SetFallbackEnabled(!_enabled));
}

internal sealed partial class SetHandyPostProcessingProviderCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly string _providerId;

    public SetHandyPostProcessingProviderCommand(HandyLocalData data, string providerId)
    {
        _data = data;
        _providerId = providerId;
        Name = "Use this provider";
    }

    public override CommandResult Invoke() => ToggleHandyPostProcessingCommand.Save(() => _data.SetActivePostProcessingProvider(_providerId));
}

internal sealed partial class SelectHandyPostProcessingPromptCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly string _promptId;

    public SelectHandyPostProcessingPromptCommand(HandyLocalData data, string promptId)
    {
        _data = data;
        _promptId = promptId;
        Name = "Select prompt";
    }

    public override CommandResult Invoke() => ToggleHandyPostProcessingCommand.Save(() => _data.SelectPostProcessingPrompt(_promptId));
}

internal sealed partial class RemoveHandyPostProcessingPromptCommand : InvokableCommand
{
    private readonly HandyLocalData _data;
    private readonly string _promptId;

    public RemoveHandyPostProcessingPromptCommand(HandyLocalData data, string promptId)
    {
        _data = data;
        _promptId = promptId;
        Name = "Remove prompt";
    }

    public override CommandResult Invoke() => ToggleHandyPostProcessingCommand.Save(() => _data.RemovePostProcessingPrompt(_promptId));
}
