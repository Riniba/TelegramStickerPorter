namespace TelegramStickerPorter;

public class TelegramOptions : IConfigurableOptions
{
    public int ApiId { get; set; }
    public string ApiHash { get; set; }
    public string BotToken { get; set; }
}