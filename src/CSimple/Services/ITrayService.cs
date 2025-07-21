namespace CSimple.Services;

public interface ITrayService
{
    void Initialize();

    Action ClickHandler { get; set; }

    // Context menu support
    Action StartListenHandler { get; set; }
    Action StopListenHandler { get; set; }
    Action ShowSettingsHandler { get; set; }
    Action QuitApplicationHandler { get; set; }
    Func<bool> IsListeningCallback { get; set; }

    // Add progress notification methods
    void ShowProgress(string title, string message, double progress);
    void UpdateProgress(double progress, string message = null);
    void HideProgress();
    void ShowCompletionNotification(string title, string message);
}
