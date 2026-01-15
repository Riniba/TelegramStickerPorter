namespace TelegramStickerPorter;

public class TelegramOptions : IConfigurableOptions
{
    public int ApiId { get; set; }
    public string ApiHash { get; set; }
    public string BotToken { get; set; }
    public bool EnabledLogging { get; set; }
    public string LogPath { get; set; }
    public string LogName { get; set; }
}