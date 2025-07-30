using System;
using System.Diagnostics;
using CSimple.ViewModels;

namespace CSimple.Services
{
    public interface ICommandManagementService
    {
        void InitializeCommands(OrientPageViewModel viewModel);
    }

    public class CommandManagementService : ICommandManagementService
    {
        public void InitializeCommands(OrientPageViewModel viewModel)
        {
            // This service will be used to call the initialization method in the ViewModel
            viewModel.InitializeCommands();
        }
    }
}
