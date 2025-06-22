namespace CSimple.Services;

public interface ITrayService
{
    void Initialize();

    Action ClickHandler { get; set; }

    // Add progress notification methods
    void ShowProgress(string title, string message, double progress);
    void UpdateProgress(double progress, string message = null);
    void HideProgress();
    void ShowCompletionNotification(string title, string message);
}
