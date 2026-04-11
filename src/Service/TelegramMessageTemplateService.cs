using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TelegramStickerPorter;

public class TelegramMessageTemplateService
{
    private static readonly Regex PlaceholderPattern = new(@"\{(?<key>[A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private readonly IOptionsMonitor<TelegramMessagesOptions> _optionsMonitor;

    public TelegramMessageTemplateService(IOptionsMonitor<TelegramMessagesOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public string RenderStickerInstructions()
        => RenderTemplate(Current.StickerInstructions, nameof(TelegramMessagesOptions.StickerInstructions));

    public string RenderStickerInfo()
        => RenderTemplate(Current.StickerInfo, nameof(TelegramMessagesOptions.StickerInfo));

    public string RenderCloneCommandFormatError()
        => RenderTemplate(Current.CloneCommandFormatError, nameof(TelegramMessagesOptions.CloneCommandFormatError));

    public string RenderInvalidStickerLink()
        => RenderTemplate(Current.InvalidStickerLink, nameof(TelegramMessagesOptions.InvalidStickerLink));

    public string RenderCloneStarted()
        => RenderTemplate(Current.CloneStarted, nameof(TelegramMessagesOptions.CloneStarted));

    public string RenderSourceStickerSetInfo(string title, int stickerCount, string stickerType)
        => RenderTemplate(Current.SourceStickerSetInfo, nameof(TelegramMessagesOptions.SourceStickerSetInfo), new Dictionary<string, string>
        {
            ["Title"] = Html(title),
            ["StickerCount"] = stickerCount.ToString(),
            ["StickerType"] = Html(stickerType)
        });

    public string RenderNewPackCreated(string title)
        => RenderTemplate(Current.NewPackCreated, nameof(TelegramMessagesOptions.NewPackCreated), new Dictionary<string, string>
        {
            ["Title"] = Html(title)
        });

    public string RenderAddingResources()
        => RenderTemplate(Current.AddingResources, nameof(TelegramMessagesOptions.AddingResources));

    public string RenderAddingProgress(int current, int total)
        => RenderTemplate(Current.AddingProgress, nameof(TelegramMessagesOptions.AddingProgress), new Dictionary<string, string>
        {
            ["Current"] = current.ToString(),
            ["Total"] = total.ToString()
        });

    public string RenderCloneCompleted(string title, int stickerCount, string packLink, IEnumerable<string> stickerErrors)
    {
        var encodedErrors = stickerErrors
            .Where(static error => !string.IsNullOrWhiteSpace(error))
            .Select(Html)
            .ToArray();

        var errorBlock = encodedErrors.Length == 0
            ? string.Empty
            : $"\n\n{RenderTemplate(Current.PartialUploadFailedBlock, nameof(TelegramMessagesOptions.PartialUploadFailedBlock), new Dictionary<string, string>
            {
                ["Errors"] = string.Join('\n', encodedErrors)
            })}";

        return RenderTemplate(Current.CloneCompleted, nameof(TelegramMessagesOptions.CloneCompleted), new Dictionary<string, string>
        {
            ["Title"] = Html(title),
            ["StickerCount"] = stickerCount.ToString(),
            ["PackLink"] = Html(packLink),
            ["ErrorBlock"] = errorBlock
        });
    }

    public string RenderCloneFailed(string errorMessage)
        => RenderTemplate(Current.CloneFailed, nameof(TelegramMessagesOptions.CloneFailed), new Dictionary<string, string>
        {
            ["ErrorMessage"] = Html(errorMessage)
        });

    public string RenderUnhandledError(string errorMessage)
        => RenderTemplate(Current.UnhandledError, nameof(TelegramMessagesOptions.UnhandledError), new Dictionary<string, string>
        {
            ["ErrorMessage"] = Html(errorMessage)
        });

    private TelegramMessagesOptions Current => _optionsMonitor.CurrentValue;

    private string RenderTemplate(IEnumerable<string> lines, string templateName, IReadOnlyDictionary<string, string> values = null)
    {
        var template = JoinLines(lines, templateName);

        if (values == null || values.Count == 0)
        {
            return template;
        }

        return PlaceholderPattern.Replace(template, match =>
        {
            var placeholderKey = match.Groups["key"].Value;
            return values.TryGetValue(placeholderKey, out var value) ? value : match.Value;
        });
    }

    private static string JoinLines(IEnumerable<string> lines, string templateName)
    {
        var templateLines = lines?.ToArray() ?? [];
        if (templateLines.Length == 0)
        {
            throw new InvalidOperationException($"未找到消息模板配置：{templateName}");
        }

        return string.Join('\n', templateLines);
    }

    private static string Html(string value)
        => WebUtility.HtmlEncode(value ?? string.Empty);
}
