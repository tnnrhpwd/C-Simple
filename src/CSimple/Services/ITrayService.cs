namespace CSimple.Services;

public interface ITrayService
{
    void Initialize();

    Action ClickHandler { get; set; }
}
