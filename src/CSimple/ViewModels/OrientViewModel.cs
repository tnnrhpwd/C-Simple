using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.ViewModels;

public class OrientViewModel : INotifyPropertyChanged
{
    private bool _isTraining;
    public bool IsTraining
    {
        get => _isTraining;
        set
        {
            _isTraining = value;
            OnPropertyChanged(nameof(_isTraining));
        }
    }
    
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null)
            handler(this, new PropertyChangedEventArgs(propertyName));
    }
    public async Task TrainModelAsync()
    {
        IsTraining = true;
        Console.WriteLine("Training model...");    
        await Task.Delay(5000);
        IsTraining = false;
    }
    
    public OrientViewModel()
    {

    }
}
