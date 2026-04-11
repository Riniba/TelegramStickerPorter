namespace TelegramStickerPorter;

public class TelegramMessagesOptions : IConfigurableOptions
{
    public string[] StickerInstructions { get; set; } = [];
    public string[] StickerInfo { get; set; } = [];
    public string[] CloneCommandFormatError { get; set; } = [];
    public string[] InvalidStickerLink { get; set; } = [];
    public string[] CloneStarted { get; set; } = [];
    public string[] SourceStickerSetInfo { get; set; } = [];
    public string[] NewPackCreated { get; set; } = [];
    public string[] AddingResources { get; set; } = [];
    public string[] AddingProgress { get; set; } = [];
    public string[] CloneCompleted { get; set; } = [];
    public string[] PartialUploadFailedBlock { get; set; } = [];
    public string[] CloneFailed { get; set; } = [];
    public string[] UnhandledError { get; set; } = [];
}
