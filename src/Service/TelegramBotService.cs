namespace TelegramStickerPorter;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly StickerService _stickerService;
    private readonly SemaphoreSlim _restartLock = new(1, 1);
    private readonly object _botSync = new();
    private readonly TelegramOptions _options;
    private Bot _bot;
    private bool _hasCompletedInitialStartup;

    public TelegramBotService(
        ILogger<TelegramBotService> logger,
        StickerService stickerService)
    {
        _logger = logger;
        _stickerService = stickerService;
        _options = App.GetConfig<TelegramOptions>("Telegram") ?? throw Oops.Oh("未在配置中找到 Telegram 节点");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EnsureBotInitializedAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("机器人后台服务已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "机器人启动失败");
        }
    }

    public Task EnsureBotInitializedAsync(CancellationToken cancellationToken = default)
        => InitializeInternalAsync(forceRestart: false, cancellationToken);

    public Task RestartBotAsync(CancellationToken cancellationToken = default)
        => InitializeInternalAsync(forceRestart: true, cancellationToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止机器人后台服务");
        StopBot();
        await base.StopAsync(cancellationToken);
    }

    public bool HasActiveBot => GetCurrentBot() != null;

    public void StopBot()
    {
        var botToStop = SwapBot(null);
        StopBotInstance(botToStop);
    }

    public async Task<bool> CanPingTelegramAsync()
    {
        var bot = GetCurrentBot();

        if (bot == null)
        {
            _logger.LogWarning("机器人实例不存在，无法检测连接");
            return false;
        }

        try
        {
            var cmds = await bot.GetMyCommands();
            return cmds != null;
        }
        catch (ObjectDisposedException)
        {
            _logger.LogWarning("机器人实例已被释放，无法检测连接");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检测机器人连接失败");
            return false;
        }
    }

    private async Task InitializeInternalAsync(bool forceRestart, CancellationToken cancellationToken)
    {
        await _restartLock.WaitAsync(cancellationToken);
        Bot candidateBot = null;

        try
        {
            if (!forceRestart && HasActiveBot)
            {
                _logger.LogDebug("检测到机器人已在线，跳过初始化");
                return;
            }

            _logger.LogInformation(forceRestart ? "正在重新初始化机器人..." : "正在初始化机器人...");

            candidateBot = CreateBot();
            var me = await candidateBot.GetMe();
            _logger.LogInformation("机器人启动: @{Username}", me.Username);

            var dropPendingUpdates = _options.DropPendingUpdatesOnStartup && !_hasCompletedInitialStartup;
            await ConfigureBotAsync(candidateBot, dropPendingUpdates);

            var previousBot = SwapBot(candidateBot);
            candidateBot = null;
            StopBotInstance(previousBot);

            _hasCompletedInitialStartup = true;
        }
        finally
        {
            StopBotInstance(candidateBot);
            _restartLock.Release();
        }
    }

    private async Task ConfigureBotAsync(Bot bot, bool dropPendingUpdates)
    {
        var commands = new[]
        {
            new Telegram.Bot.Types.BotCommand { Command = "start", Description = "启动机器人" },
            new Telegram.Bot.Types.BotCommand { Command = "info", Description = "关于" },
        };

        foreach (var cmd in commands)
        {
            _logger.LogInformation("命令：{Command} 描述：{Description}", cmd.Command, cmd.Description);
        }

        await bot.SetMyCommands(commands, new BotCommandScopeAllPrivateChats());

        if (dropPendingUpdates)
        {
            await bot.DropPendingUpdates();
            _logger.LogInformation("机器人已按配置丢弃首次启动前的未处理更新");
        }
        else
        {
            _logger.LogInformation("机器人保留未处理的更新");
        }

        ConfigureErrorHandling(bot);
        ConfigureMessageHandling(bot);
    }

    private void ConfigureErrorHandling(Bot bot)
    {
        bot.WantUnknownTLUpdates = true;
        bot.OnError += (e, s) =>
        {
            _logger.LogError("机器人错误: {Error}", e);
            return Task.CompletedTask;
        };
    }

    private void ConfigureMessageHandling(Bot bot)
    {
        bot.OnMessage += async (msg, type) => await OnMessageAsync(bot, msg, type);
        bot.OnUpdate += update =>
        {
            _logger.LogInformation("机器人处理更新");
            ProcessUpdate(bot, update);
            return Task.CompletedTask;
        };

        _logger.LogInformation("机器人监听中...");
    }

    private async Task OnMessageAsync(Bot bot, WTelegram.Types.Message msg, UpdateType type)
    {
        if (msg.Chat.Type != ChatType.Group && msg.Chat.Type != ChatType.Supergroup)
        {
            await HandlePrivateAsync(bot, msg);
        }
    }

    private void ProcessUpdate(Bot bot, WTelegram.Types.Update update)
    {
        if (update.Type != UpdateType.Unknown) return;

        if (update.TLUpdate is TL.UpdateDeleteChannelMessages udcm)
            _logger.LogInformation("{Count} 条消息被删除，来源：{Source}", udcm.messages.Length, bot.Chat(udcm.channel_id)?.Title);
        else if (update.TLUpdate is TL.UpdateDeleteMessages udm)
            _logger.LogInformation("{Count} 条消息被删除，来源：用户或小型私聊群组", udm.messages.Length);
        else if (update.TLUpdate is TL.UpdateReadChannelOutbox urco)
            _logger.LogInformation("某人阅读了 {Source} 的消息，直到消息 ID: {MessageId}", bot.Chat(urco.channel_id)?.Title, urco.max_id);
    }

    private async Task HandlePrivateAsync(Bot bot, WTelegram.Types.Message msg)
    {
        if (msg.Text == null) return;

        var text = msg.Text.ToLower();

        if (text.StartsWith("/start") || text == "/clonepack" || text == "clonepack" || text == "克隆" || text == "贴纸" || text == "tiezhi" || text == "表情" || text == "biaoqing" || text == "emoji" || text == "stickers")
        {
            await _stickerService.SendStickerInstructionsAsync(bot, msg);
        }
        else if (text.StartsWith("克隆#"))
        {
            await _stickerService.HandleCloneCommandAsync(bot, msg);
        }
        else if (text.StartsWith("/info"))
        {
            await _stickerService.SendStickerInfoAsync(bot, msg);
        }
    }

    private Bot CreateBot()
    {
        try
        {
            var basePath = AppContext.BaseDirectory;
            var dbPath = Path.Combine(basePath, "TelegramBot.sqlite");
            var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");

            var bot = new Bot(
                _options.BotToken,
                _options.ApiId,
                _options.ApiHash,
                connection,
                SqlCommands.Sqlite);

            _logger.LogInformation("创建新机器人实例成功");
            return bot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建机器人实例失败");
            throw Oops.Oh(ex, "启动机器人时发生错误");
        }
    }

    private Bot GetCurrentBot()
    {
        lock (_botSync)
        {
            return _bot;
        }
    }

    private Bot SwapBot(Bot newBot)
    {
        lock (_botSync)
        {
            var previousBot = _bot;
            _bot = newBot;
            return previousBot;
        }
    }

    private void StopBotInstance(Bot bot)
    {
        if (bot == null) return;

        try
        {
            bot.Dispose();
            _logger.LogInformation("机器人实例已释放");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放机器人实例时出错");
        }
    }
}
