using CSimple.Models;
using CSimple.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSimple.Services
{
    /// <summary>
    /// Service responsible for managing action review and navigation functionality
    /// </summary>
    public interface IActionReviewNavigationService
    {
        Task LoadSelectedActionAsync(
            string selectedActionName,
            List<ActionItem> currentActionItems,
            ObservableCollection<NodeViewModel> nodes,
            Func<int, Task> setCurrentActionStepAsync,
            Action updateCommandStates,
            Action<List<ActionItem>> setCurrentActionItems,
            Action refreshAllNodeStepContent,
            Action notifyPropertyChanged);

        Task<int> SetCurrentActionStepAsync(int newStep, Func<int, Task> setCurrentActionStepAsync);
    }

    public class ActionReviewNavigationService : IActionReviewNavigationService
    {
        private readonly ActionStepNavigationService _actionStepNavigationService;
        private readonly ActionReviewService _actionReviewService;
        private readonly EnsembleModelService _ensembleModelService;

        public ActionReviewNavigationService(
            ActionStepNavigationService actionStepNavigationService,
            ActionReviewService actionReviewService,
            EnsembleModelService ensembleModelService)
        {
            _actionStepNavigationService = actionStepNavigationService;
            _actionReviewService = actionReviewService;
            _ensembleModelService = ensembleModelService;
        }

        public async Task LoadSelectedActionAsync(
            string selectedActionName,
            List<ActionItem> currentActionItems,
            ObservableCollection<NodeViewModel> nodes,
            Func<int, Task> setCurrentActionStepAsync,
            Action updateCommandStates,
            Action<List<ActionItem>> setCurrentActionItems,
            Action refreshAllNodeStepContent,
            Action notifyPropertyChanged)
        {
            try
            {
                Debug.WriteLine($"[ActionReviewNavigationService.LoadSelectedAction] Attempting to load action: {selectedActionName ?? "null"}");

                // Clear the step content cache to ensure fresh data for the new action
                _ensembleModelService.ClearStepContentCache();
                _ensembleModelService.ClearModelNodeActionSteps(nodes);
                Debug.WriteLine($"[ActionReviewNavigationService.LoadSelectedAction] Cleared step content cache and model ActionSteps for action change");

                // Use the ActionStepNavigationService to load the action (without updating commands yet)
                var result = await _actionStepNavigationService.LoadSelectedActionAsync(
                    selectedActionName,
                    nodes,
                    setCurrentActionStepAsync,
                    null); // Don't update commands yet - we'll do it after setting action items

                var newActionItems = result.ActionItems;
                setCurrentActionItems(newActionItems);

                // Update static property for NodeViewModel access
                NodeViewModel.CurrentActionItems = newActionItems;

                Debug.WriteLine($"[ActionReviewNavigationService.LoadSelectedAction] Loaded '{selectedActionName}' with {newActionItems.Count} action items via navigation service.");

                // Force UI refresh after action change
                Debug.WriteLine($"[ActionReviewNavigationService.LoadSelectedAction] Forcing UI refresh for new action");

                // Explicit refresh of all node ActionSteps to ensure UI updates
                refreshAllNodeStepContent();

                // NOW update command states after action items are properly set
                updateCommandStates();
                Debug.WriteLine($"[ActionReviewNavigationService.LoadSelectedAction] Updated command states after setting action items");

                // Notify UI that step content might have changed
                notifyPropertyChanged();

                Debug.WriteLine($"[ActionReviewNavigationService.LoadSelectedAction] Action loading complete, step content updated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionReviewNavigationService.LoadSelectedAction] Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw; // Re-throw to allow caller to handle
            }
        }

        public async Task<int> SetCurrentActionStepAsync(int newStep, Func<int, Task> setCurrentActionStepAsync)
        {
            await setCurrentActionStepAsync(newStep);
            return newStep;
        }
    }
}
