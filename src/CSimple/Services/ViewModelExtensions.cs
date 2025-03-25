using System;
using System.Threading.Tasks;
using CSimple.Models;
using CSimple.ViewModels;

namespace CSimple.Services
{
    /// <summary>
    /// Extension methods to integrate various services with view models
    /// </summary>
    public static class ViewModelExtensions
    {
        /// <summary>
        /// Helper method to call TrainModelAsync from service layers
        /// </summary>
        public static async Task<bool> CallTrainModelAsync(this OrientViewModel viewModel, NeuralNetworkService service = null)
        {
            try
            {
                // Call the ViewModel's TrainModelAsync method
                var result = await viewModel.TrainModelAsync();

                // If a service is provided, we could do additional work here
                if (service != null)
                {
                    // Example: Update service-related state or sync with other systems
                    System.Diagnostics.Debug.WriteLine("Neural network service integration enabled");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in TrainModelAsync extension: {ex.Message}");
                return false;
            }
        }
    }
}
