namespace CSimple.Services
{
    public interface IOnTrainModelClickedService
    {
        Task HandleTrainModelAsync(object bindingContext);
    }

    public class OnTrainModelClickedService : IOnTrainModelClickedService
    {
        public async Task HandleTrainModelAsync(object bindingContext)
        {
            var vm = bindingContext as CSimple.ViewModels.OrientViewModel;
            if (vm != null) await vm.TrainModelAsync();
            Console.WriteLine("Model trained via OnTrainModelClickedService.");
        }
    }
}
