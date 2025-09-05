using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CSimple.Models;
using CSimple.ViewModels;

namespace CSimple.Services
{
    /// <summary>
    /// Service for handling action step navigation, including forward/backward stepping,
    /// action loading, and step content management for the action review functionality
    /// </summary>
    public class ActionStepNavigationService
    {
        private readonly ActionReviewService _actionReviewService;

        public ActionStepNavigationService(ActionReviewService actionReviewService)
        {
            _actionReviewService = actionReviewService ?? throw new ArgumentNullException(nameof(actionReviewService));
        }

        /// <summary>
        /// Handles step forward navigation with bounds checking and command state updates
        /// </summary>
        public async Task<bool> ExecuteStepForwardAsync(
            int currentActionStep,
            List<ActionItem> currentActionItems,
            Func<int, Task> setCurrentActionStep,
            Action updateCommands)
        {
            if (currentActionItems == null || currentActionStep >= currentActionItems.Count - 1)
            {
                Debug.WriteLine($"[ActionStepNavigationService.ExecuteStepForward] Cannot step forward. CurrentActionStep: {currentActionStep}, TotalItems: {currentActionItems?.Count ?? 0}");
                updateCommands?.Invoke(); // Re-evaluate CanExecute
                return false;
            }

            var newStep = currentActionStep + 1;
            Debug.WriteLine($"[ActionStepNavigationService.ExecuteStepForward] CurrentActionStep incremented to: {newStep}");

            await setCurrentActionStep(newStep);
            updateCommands?.Invoke();

            return true;
        }

        /// <summary>
        /// Handles step backward navigation with bounds checking and command state updates
        /// </summary>
        public async Task<bool> ExecuteStepBackwardAsync(
            int currentActionStep,
            Func<int, Task> setCurrentActionStep,
            Action updateCommands)
        {
            if (currentActionStep <= 0)
            {
                Debug.WriteLine($"[ActionStepNavigationService.ExecuteStepBackward] Cannot step backward. CurrentActionStep: {currentActionStep}");
                updateCommands?.Invoke(); // Re-evaluate CanExecute
                return false;
            }

            var newStep = currentActionStep - 1;
            Debug.WriteLine($"[ActionStepNavigationService.ExecuteStepBackward] CurrentActionStep decremented to: {newStep}");

            await setCurrentActionStep(newStep);
            updateCommands?.Invoke();

            return true;
        }

        /// <summary>
        /// Resets the action to the beginning (step 0)
        /// </summary>
        public async Task ExecuteResetActionAsync(
            int currentActionStep,
            Func<int, Task> setCurrentActionStep,
            Action updateCommands)
        {
            Debug.WriteLine($"[ActionStepNavigationService.ExecuteResetAction] Resetting action. CurrentActionStep was: {currentActionStep}");

            await setCurrentActionStep(0);
            updateCommands?.Invoke();

            Debug.WriteLine($"[ActionStepNavigationService.ExecuteResetAction] Action reset. CurrentActionStep is now: 0");
        }

        /// <summary>
        /// Loads a selected action and initializes the step navigation state
        /// </summary>
        public async Task<ActionLoadResult> LoadSelectedActionAsync(
            string selectedReviewActionName,
            ObservableCollection<NodeViewModel> nodes,
            Func<int, Task> setCurrentActionStep,
            Action updateCommands)
        {
            try
            {
                Debug.WriteLine($"[ActionStepNavigationService.LoadSelectedAction] Attempting to load action: {selectedReviewActionName ?? "null"}");

                // Reset current state
                await setCurrentActionStep(0); // Set to 0, so first StepForward goes to step 1 (index 0)

                // Clear ActionSteps for all input and model nodes to ensure fresh data
                foreach (var nodeVM in nodes.Where(n => n.Type == NodeType.Input || n.Type == NodeType.Model))
                {
                    nodeVM.ActionSteps.Clear();
                    Debug.WriteLine($"[ActionStepNavigationService.LoadSelectedAction] Cleared ActionSteps for {nodeVM.Type} Node: {nodeVM.Name}");
                }

                // Use the ActionReviewService to load the action data
                var actionReviewData = await _actionReviewService.LoadSelectedActionAsync(selectedReviewActionName, nodes);

                Debug.WriteLine($"[ActionStepNavigationService.LoadSelectedAction] Loaded '{selectedReviewActionName}' with {actionReviewData.ActionItems.Count} action items via service.");

                return new ActionLoadResult
                {
                    Success = true,
                    ActionItems = actionReviewData.ActionItems
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionStepNavigationService.LoadSelectedAction] Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return new ActionLoadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                updateCommands?.Invoke();
            }
        }

        /// <summary>
        /// Loads action step data for the current step
        /// </summary>
        public async Task LoadActionStepDataAsync(
            int currentActionStep,
            List<ActionItem> currentActionItems,
            Action updateStepContent)
        {
            try
            {
                await _actionReviewService.LoadActionStepDataAsync(currentActionStep, currentActionItems);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionStepNavigationService.LoadActionStepData] Error: {ex.Message}");
            }
            finally
            {
                updateStepContent?.Invoke(); // This is the most important call here.
            }
        }

        /// <summary>
        /// Updates step content using the action review service
        /// </summary>
        public (string ContentType, string Content) UpdateStepContent(
            NodeViewModel selectedNode,
            int currentActionStep,
            List<ActionItem> currentActionItems,
            string selectedReviewActionName)
        {
            var stepContentData = _actionReviewService.UpdateStepContent(
                selectedNode,
                currentActionStep,
                currentActionItems,
                selectedReviewActionName);

            return (stepContentData.ContentType, stepContentData.Content);
        }

        /// <summary>
        /// Checks if step forward navigation is possible
        /// </summary>
        public bool CanStepForward(string selectedReviewActionName, List<ActionItem> currentActionItems, int currentActionStep)
        {
            bool hasAction = !string.IsNullOrEmpty(selectedReviewActionName);
            bool hasItems = currentActionItems != null;
            int itemCount = currentActionItems?.Count ?? 0;
            bool canStep = hasAction && hasItems && currentActionStep < itemCount - 1;

            Debug.WriteLine($"[CanStepForward] Action: '{selectedReviewActionName ?? "null"}', " +
                          $"Items: {itemCount}, Step: {currentActionStep}, " +
                          $"CanStep: {canStep} (hasAction: {hasAction}, hasItems: {hasItems}, stepCheck: {currentActionStep} < {itemCount - 1})");

            return canStep;
        }

        /// <summary>
        /// Checks if step backward navigation is possible
        /// </summary>
        public bool CanStepBackward(string selectedReviewActionName, int currentActionStep)
        {
            bool hasAction = !string.IsNullOrEmpty(selectedReviewActionName);
            bool canStep = hasAction && currentActionStep > 0;

            Debug.WriteLine($"[CanStepBackward] Action: '{selectedReviewActionName ?? "null"}', " +
                          $"Step: {currentActionStep}, CanStep: {canStep} (hasAction: {hasAction}, stepCheck: {currentActionStep} > 0)");

            return canStep;
        }

        /// <summary>
        /// Checks if action reset is possible
        /// </summary>
        public bool CanResetAction(string selectedReviewActionName)
        {
            return !string.IsNullOrEmpty(selectedReviewActionName);
        }
    }

    /// <summary>
    /// Result of loading an action
    /// </summary>
    public class ActionLoadResult
    {
        public bool Success { get; set; }
        public List<ActionItem> ActionItems { get; set; } = new List<ActionItem>();
        public string ErrorMessage { get; set; }
    }
}
