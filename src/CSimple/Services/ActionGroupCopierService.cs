using System;
using System.Linq;
using CSimple.Models;
using System.Diagnostics;
using System.Collections.Generic;

namespace CSimple.Services
{
    public class ActionGroupCopierService
    {
        public ActionGroup SafelyCopyActionGroup(ActionGroup source)
        {
            if (source == null) return null;

            try
            {
                var copy = new ActionGroup
                {
                    Id = source.Id,
                    ActionName = source.ActionName,
                    Description = source.Description,
                    Category = source.Category,
                    ActionType = source.ActionType ?? "Local Action",
                    IsSelected = false, // Reset selection state for detail page
                    IsSimulating = false // Ensure simulation is off
                };

                // Safely copy action array
                if (source.ActionArray != null)
                {
                    copy.ActionArray = source.ActionArray.Select(a => new ActionItem
                    {
                        EventType = a.EventType,
                        KeyCode = a.KeyCode,
                        Duration = a.Duration,
                        Timestamp = a.Timestamp,
                        Coordinates = source.ActionArray.Select(a => a.Coordinates != null ? new Coordinates
                        {
                            X = a.Coordinates.X,
                            Y = a.Coordinates.Y
                        } : null).FirstOrDefault()
                    }).ToList();
                }

                // Safely copy files using the extension method
                var sourceFiles = ActionGroupExtensions.GetFiles(source);
                if (sourceFiles != null)
                {
                    var copiedFiles = sourceFiles.Select(f => new ActionFile
                    {
                        Filename = f.Filename,
                        ContentType = f.ContentType,
                        Data = f.Data,
                        AddedAt = f.AddedAt,
                        IsProcessed = f.IsProcessed
                    }).ToList();

                    copy.SetFiles(copiedFiles);
                }

                return copy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying ActionGroup: {ex.Message}");
                return new ActionGroup { ActionName = "Error copying action" };
            }
        }
    }
}
