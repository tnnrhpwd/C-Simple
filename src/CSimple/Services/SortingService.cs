using System.Collections.Generic;
using System.Linq;
using CSimple.Models;

namespace CSimple.Services
{
    public class SortingService
    {
        public List<ActionGroup> SortActionGroups(List<ActionGroup> actionGroups, string selectedSortOption)
        {
            if (actionGroups == null || actionGroups.Count == 0)
                return actionGroups;

            switch (selectedSortOption)
            {
                case "Date (Newest First)":
                    return actionGroups.OrderByDescending(a => a.CreatedAt ?? DateTime.MinValue).ToList();
                case "Date (Oldest First)":
                    return actionGroups.OrderBy(a => a.CreatedAt ?? DateTime.MinValue).ToList();
                case "Name (A-Z)":
                    return actionGroups.OrderBy(a => a.ActionName).ToList();
                case "Name (Z-A)":
                    return actionGroups.OrderByDescending(a => a.ActionName).ToList();
                case "Type":
                    return actionGroups.OrderBy(a => a.ActionType).ToList();
                case "Steps Count":
                    return actionGroups.OrderByDescending(a => a.ActionArray?.Count ?? 0).ToList();
                case "Usage Count":
                    return actionGroups.OrderByDescending(a => a.UsageCount).ToList();
                case "Size (Largest First)":
                    return actionGroups.OrderByDescending(a => a.Size).ToList();
                case "Size (Smallest First)":
                    return actionGroups.OrderBy(a => a.Size).ToList();
                default:
                    return actionGroups;
            }
        }
    }
}
