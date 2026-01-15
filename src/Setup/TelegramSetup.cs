namespace TelegramStickerPorter;

public static class TelegramSetup
{
    public static IServiceCollection AddTelegram(this IServiceCollection services)
    {
        ConfigureWTelegramLogging();
        services.AddConfigurableOptions<TelegramOptions>();
        services.AddSingleton<TelegramBotService>();
        services.AddHostedService(sp => sp.GetRequiredService<TelegramBotService>());
        services.AddSingleton<MessageService>();
        services.AddSingleton<StickerService>();

        return services;
    }

    private static void ConfigureWTelegramLogging()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "TelegramBot.log");
        var logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8) { AutoFlush = true };

        WTelegram.Helpers.Log = (lvl, str) =>
        {
            var logLevel = "TDIWE!"[lvl];
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            lock (logWriter)
            {
                logWriter.WriteLine($"{timestamp} [{logLevel}] {str}");
            }
        };
    }
}