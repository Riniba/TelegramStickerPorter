namespace TelegramStickerPorter;

public class StickerService
{
    private const string CloneCommandPrefix = "克隆#";
    private readonly ILogger<StickerService> _logger;
    private readonly MessageService _messageService;
    private readonly TelegramMessageTemplateService _messageTemplateService;

    public StickerService(
        ILogger<StickerService> logger,
        MessageService messageService,
        TelegramMessageTemplateService messageTemplateService)
    {
        _logger = logger;
        _messageService = messageService;
        _messageTemplateService = messageTemplateService;
    }

    public async Task SendStickerInstructionsAsync(Bot bot, Telegram.Bot.Types.Message msg)
    {
        await _messageService.SendMessageAsync(
            bot,
            msg.Chat.Id,
            _messageTemplateService.RenderStickerInstructions(),
            replyParameters: msg);
    }

    public async Task SendStickerInfoAsync(Bot bot, Telegram.Bot.Types.Message msg)
    {
        await _messageService.SendMessageAsync(
            bot,
            msg.Chat.Id,
            _messageTemplateService.RenderStickerInfo(),
            replyParameters: msg);
    }

    public async Task HandleCloneCommandAsync(Bot bot, Telegram.Bot.Types.Message msg)
    {
        try
        {
            if (!TryParseCloneCommand(msg.Text, out var newStickerSetTitle, out var stickerUrl))
            {
                await _messageService.SendMessageAsync(
                    bot,
                    msg.Chat.Id,
                    _messageTemplateService.RenderCloneCommandFormatError(),
                    replyParameters: msg);
                return;
            }

            if (!TryExtractStickerSetName(stickerUrl, out var sourceStickerSetName))
            {
                await _messageService.SendMessageAsync(
                    bot,
                    msg.Chat.Id,
                    _messageTemplateService.RenderInvalidStickerLink(),
                    replyParameters: msg);
                return;
            }

            var statusMessageId = await _messageService.SendMessageAsync(
                bot,
                msg.Chat.Id,
                _messageTemplateService.RenderCloneStarted());

            _ = Task.Run(async () => await ProcessCloneStickerTaskAsync(
                bot, msg, statusMessageId, sourceStickerSetName, newStickerSetTitle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理克隆命令时发生异常");
            await _messageService.SendMessageAsync(
                bot,
                msg.Chat.Id,
                _messageTemplateService.RenderUnhandledError(ex.Message));
        }
    }

    private async Task ProcessCloneStickerTaskAsync(
        Bot bot,
        Telegram.Bot.Types.Message msg,
        int statusMessageId,
        string sourceStickerSetName,
        string newStickerSetTitle)
    {
        List<string> stickerErrors = new();

        try
        {
            var me = await bot.GetMe();
            string botUsername = me.Username?.ToLowerInvariant();
            string newPackName = GeneratePackName(botUsername);

            var sourceSet = await bot.GetStickerSet(sourceStickerSetName);

            await _messageService.EditMessageAsync(
                bot,
                msg.Chat.Id,
                statusMessageId,
                _messageTemplateService.RenderSourceStickerSetInfo(
                    sourceSet.Title,
                    sourceSet.Stickers.Length,
                    sourceSet.StickerType.ToString()));

            var itemsForNewSet = sourceSet.Stickers
                .Select(item => new InputSticker(
                    sticker: item.FileId,
                    format: DetermineStickerFormat(item),
                    emojiList: item.Emoji?.Split() ?? new[] { "🙂" }))
                .ToList();

            if (!itemsForNewSet.Any())
                throw Oops.Oh("源包中未找到贴纸");

            await bot.CreateNewStickerSet(
                userId: msg.From.Id,
                name: newPackName,
                title: newStickerSetTitle,
                stickers: new[] { itemsForNewSet[0] },
                stickerType: sourceSet.StickerType);

            await _messageService.EditMessageAsync(
                bot,
                msg.Chat.Id,
                statusMessageId,
                _messageTemplateService.RenderNewPackCreated(newStickerSetTitle));

            if (itemsForNewSet.Count > 1)
            {
                await _messageService.EditMessageAsync(
                    bot,
                    msg.Chat.Id,
                    statusMessageId,
                    _messageTemplateService.RenderAddingResources());

                for (int i = 1; i < itemsForNewSet.Count; i++)
                {
                    try
                    {
                        await _messageService.EditMessageAsync(
                            bot,
                            msg.Chat.Id,
                            statusMessageId,
                            _messageTemplateService.RenderAddingProgress(i, itemsForNewSet.Count - 1));

                        await bot.AddStickerToSet(
                            userId: msg.From.Id,
                            name: newPackName,
                            sticker: itemsForNewSet[i]);
                    }
                    catch (Exception stickerEx)
                    {
                        string errorMsg = $"贴纸 {i} 添加失败: {stickerEx.Message}";
                        stickerErrors.Add(errorMsg);
                        _logger.LogError(stickerEx, "贴纸 {StickerIndex} 添加失败 - 用户ID: {UserId}, 包名: {PackName}", i, msg.From.Id, newPackName);
                    }

                    await Task.Delay(100);
                }
            }

            string packLink = BuildStickerSetLink(sourceSet.StickerType, newPackName);

            await _messageService.EditMessageAsync(
                bot,
                msg.Chat.Id,
                statusMessageId,
                _messageTemplateService.RenderCloneCompleted(
                    newStickerSetTitle,
                    itemsForNewSet.Count,
                    packLink,
                    stickerErrors));
        }
        catch (Exception ex)
        {
            await _messageService.EditMessageAsync(
                bot,
                msg.Chat.Id,
                statusMessageId,
                _messageTemplateService.RenderCloneFailed(ex.Message));

            _logger.LogError(ex, "克隆贴纸包时发生错误 - 用户ID: {UserId}, 源包: {SourceStickerSetName}", msg.From.Id, sourceStickerSetName);
        }
    }

    private StickerFormat DetermineStickerFormat(Sticker sticker)
    {
        if (sticker == null)
            throw Oops.Oh("贴纸对象不能为空");

        return sticker.IsVideo ? StickerFormat.Video :
               sticker.IsAnimated ? StickerFormat.Animated :
               StickerFormat.Static;
    }

    private void ValidatePackName(string packName)
    {
        if (string.IsNullOrEmpty(packName))
            throw Oops.Oh("包名称不能为空");

        if (!packName.All(c => char.IsLetterOrDigit(c) || c == '_'))
            throw Oops.Oh("包名称只能包含字母、数字和下划线");
    }

    private string GeneratePackName(string botUsername)
    {
        if (string.IsNullOrEmpty(botUsername))
            throw Oops.Oh("Bot用户名不能为空");

        string randomId = Guid.NewGuid().ToString("N")[..8];
        string packName = $"pack_{randomId}_by_{botUsername}";

        ValidatePackName(packName);
        return packName;
    }

    private static bool TryParseCloneCommand(string messageText, out string newStickerSetTitle, out string stickerUrl)
    {
        newStickerSetTitle = null;
        stickerUrl = null;

        if (string.IsNullOrWhiteSpace(messageText) ||
            !messageText.StartsWith(CloneCommandPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = messageText[CloneCommandPrefix.Length..].Trim();
        var separatorIndex = payload.LastIndexOf('#');

        if (separatorIndex <= 0 || separatorIndex >= payload.Length - 1)
        {
            return false;
        }

        newStickerSetTitle = payload[..separatorIndex].Trim();
        stickerUrl = payload[(separatorIndex + 1)..].Trim();

        return !string.IsNullOrWhiteSpace(newStickerSetTitle)
            && !string.IsNullOrWhiteSpace(stickerUrl);
    }

    private static bool TryExtractStickerSetName(string stickerUrl, out string sourceStickerSetName)
    {
        sourceStickerSetName = null;

        if (!Uri.TryCreate(stickerUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(uri.Host, "t.me", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "telegram.me", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 2)
        {
            return false;
        }

        var route = segments[0];
        if (!string.Equals(route, "addstickers", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(route, "addemoji", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        sourceStickerSetName = Uri.UnescapeDataString(segments[1]).Trim();
        return !string.IsNullOrWhiteSpace(sourceStickerSetName);
    }

    private static string BuildStickerSetLink(StickerType stickerType, string stickerSetName)
    {
        var route = stickerType switch
        {
            StickerType.CustomEmoji => "addemoji",
            StickerType.Regular or StickerType.Mask => "addstickers",
            _ => "addstickers"
        };

        return $"https://t.me/{route}/{stickerSetName}";
    }
}
