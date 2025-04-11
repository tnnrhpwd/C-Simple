using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CSimple.Models;

namespace CSimple.Services
{
    public class FilteringService
    {
        public List<ActionGroup> FilterActions(IEnumerable<ActionGroup> actionGroups, string searchText, string selectedCategory)
        {
            IEnumerable<ActionGroup> baseList = actionGroups;

            if (!string.IsNullOrEmpty(selectedCategory) && selectedCategory != "All Categories")
            {
                baseList = baseList.Where(a => a.Category == selectedCategory);
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                baseList = baseList.Where(a =>
                    a.ActionName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (a.Description != null && a.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                    (a.ActionType != null && a.ActionType.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
            }

            return baseList.ToList();
        }
    }
}
