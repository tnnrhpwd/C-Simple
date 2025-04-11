using System;
using System.Collections.ObjectModel;
using System.Linq;
using CSimple.Models;

namespace CSimple.Services
{
    public class ActionGroupService
    {
        private readonly ActionService _actionService;

        public ActionGroupService(ActionService actionService)
        {
            _actionService = actionService;
        }

        public void UpdateActionGroupsFromDataItems(ObservableCollection<DataItem> dataItems, ObservableCollection<ActionGroup> actionGroups, ObservableCollection<ActionGroup> allActionGroups)
        {
            actionGroups.Clear();

            foreach (var item in dataItems)
            {
                if (item?.Data?.ActionGroupObject != null)
                {
                    // Make sure the action group has all required properties for display
                    var actionGroup = item.Data.ActionGroupObject;

                    // Set additional metadata for display
                    if (string.IsNullOrEmpty(actionGroup.ActionName))
                    {
                        actionGroup.ActionName = "Unnamed Action";
                    }

                    // CRITICAL FIX: Always use original database timestamp, never generate random dates
                    // or override with defaults. This ensures we show the actual creation date.
                    if (actionGroup.CreatedAt == null || actionGroup.CreatedAt == default(DateTime))
                    {
                        // Use the database timestamp directly
                        actionGroup.CreatedAt = item.createdAt;
                    }

                    // Preserve the IsLocal flag - don't override it
                    // actionGroup.IsLocal = actionGroup.IsLocal; 

                    // Set default values for new properties if they don't exist
                    actionGroup.Category = _actionService.DetermineCategory(actionGroup);
                    actionGroup.UsageCount = actionGroup.UsageCount > 0 ? actionGroup.UsageCount : new Random().Next(1, 20);
                    actionGroup.SuccessRate = actionGroup.SuccessRate > 0 ? actionGroup.SuccessRate : (double)new Random().Next(70, 100) / 100;
                    actionGroup.IsPartOfTraining = actionGroup.IsPartOfTraining; // Preserve existing value
                    actionGroup.IsChained = actionGroup.IsChained; // Preserve existing value
                    actionGroup.HasMetrics = true; // Show metrics by default
                    actionGroup.Description = actionGroup.Description ?? $"Action for {actionGroup.ActionName}";

                    // FIX: Always use the database timestamp instead of generating random dates
                    if (actionGroup.CreatedAt == null || actionGroup.CreatedAt == default(DateTime))
                    {
                        // Always use the item's creation date from the database
                        actionGroup.CreatedAt = item.createdAt;

                        // If item.createdAt is also default, it's truly a new item, so use current time
                        if (actionGroup.CreatedAt == default(DateTime))
                        {
                            actionGroup.CreatedAt = DateTime.Now;
                        }
                    }

                    // Determine action type if not set
                    if (string.IsNullOrEmpty(actionGroup.ActionType))
                    {
                        actionGroup.ActionType = _actionService.DetermineActionTypeFromSteps(actionGroup);
                    }

                    // Add to the collection - only add if not already present to prevent duplicates
                    if (!actionGroups.Any(ag =>
                        (!string.IsNullOrEmpty(actionGroup.Id.ToString()) && actionGroup.Id.ToString() == ag.Id.ToString()) ||
                        (!string.IsNullOrEmpty(actionGroup.ActionName) && actionGroup.ActionName == ag.ActionName)))
                    {
                        actionGroups.Add(actionGroup);
                    }
                }
            }

            // Update allActionGroups
            allActionGroups.Clear();
            foreach (var actionGroup in actionGroups)
            {
                allActionGroups.Add(actionGroup);
            }
        }
    }
}
