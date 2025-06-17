using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CSimple.Models;
using CSimple.Pages;

namespace CSimple
{
    public static class ActionStepGrouping
    {
        public static void GroupSimilarActions(List<StepViewModel> steps, ObservableCollection<StepViewModel> ActionSteps)
        {
            if (steps == null || steps.Count == 0)
            {
                Debug.WriteLine("No steps to group");
                return;
            }

            try
            {
                // Constants for grouping configuration
                const int MIN_GROUP_SIZE = 3; // Minimum number of similar actions to form a group
                const int MAX_MOUSE_MOVES_BEFORE_GROUPING = 4; // Show this many individual moves before grouping

                int currentIndex = 0;
                int displayIndex = 1; // For user-visible indexing (starts at 1)

                while (currentIndex < steps.Count)
                {
                    var currentStep = steps[currentIndex];

                    // Check if we can start a grouping from this step
                    bool canGroup = false;
                    string groupType = "";

                    // 1. Check for consecutive mouse movements
                    if (currentStep.IsMouseMove && currentIndex + MIN_GROUP_SIZE <= steps.Count)
                    {
                        int mouseMoveCount = 1;
                        for (int i = currentIndex + 1; i < steps.Count; i++)
                        {
                            if (steps[i].IsMouseMove)
                                mouseMoveCount++;
                            else
                                break;
                        }

                        if (mouseMoveCount >= MIN_GROUP_SIZE)
                        {
                            canGroup = true;
                            groupType = "MouseMove";
                        }
                    }
                    // Grouping for Mouse Clicks
                    else if (currentStep.IsMouseButton && currentIndex + MIN_GROUP_SIZE <= steps.Count)
                    {
                        int mouseClickCount = 1;
                        for (int i = currentIndex + 1; i < steps.Count; i++)
                        {
                            if (steps[i].IsMouseButton &&
                                steps[i].MouseButtonType == currentStep.MouseButtonType &&
                                steps[i].MouseButtonAction == currentStep.MouseButtonAction)
                                mouseClickCount++;
                            else
                                break;
                        }

                        if (mouseClickCount >= MIN_GROUP_SIZE)
                        {
                            canGroup = true;
                            groupType = "MouseClick";
                        }
                    }

                    // 2. Check for consecutive key presses of the same key
                    else if (!string.IsNullOrEmpty(currentStep.KeyName) && currentIndex + MIN_GROUP_SIZE <= steps.Count)
                    {
                        int sameKeyCount = 1;
                        string keyName = currentStep.KeyName;

                        for (int i = currentIndex + 1; i < steps.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(steps[i].KeyName) && steps[i].KeyName == keyName)
                                sameKeyCount++;
                            else
                                break;
                        }

                        if (sameKeyCount >= MIN_GROUP_SIZE)
                        {
                            canGroup = true;
                            groupType = "KeyPress";
                        }
                    }

                    // 3. Process the grouping or individual step
                    if (canGroup)
                    {
                        if (groupType == "MouseMove")
                        {
                            // Count consecutive mouse moves
                            int mouseMoveCount = 0;
                            for (int i = currentIndex; i < steps.Count; i++)
                            {
                                if (steps[i].IsMouseMove)
                                    mouseMoveCount++;
                                else
                                    break;
                            }

                            // Add individual mouse moves at the beginning for context
                            int individualMovesToShow = Math.Min(MAX_MOUSE_MOVES_BEFORE_GROUPING, mouseMoveCount / 2);
                            for (int j = 0; j < individualMovesToShow && currentIndex + j < steps.Count; j++)
                            {
                                var step = steps[currentIndex + j];
                                step.Index = displayIndex.ToString();
                                ActionSteps.Add(step);
                                displayIndex++;
                            }

                            // Skip if all moves are shown individually
                            if (mouseMoveCount <= individualMovesToShow)
                            {
                                currentIndex += mouseMoveCount;
                                continue;
                            }

                            // Create group for remaining moves
                            var groupedMoves = new List<ActionItem>();
                            DateTime firstTimestamp = DateTime.MaxValue;
                            DateTime lastTimestamp = DateTime.MinValue;

                            for (int j = individualMovesToShow; j < mouseMoveCount && currentIndex + j < steps.Count; j++)
                            {
                                if (steps[currentIndex + j].RawData != null)
                                {
                                    groupedMoves.Add(steps[currentIndex + j].RawData);

                                    if (DateTime.TryParse(steps[currentIndex + j].Timestamp, out DateTime timestamp) && timestamp < firstTimestamp)
                                        firstTimestamp = timestamp;
                                    if (DateTime.TryParse(steps[currentIndex + j].Timestamp, out timestamp) && timestamp > lastTimestamp)
                                        lastTimestamp = timestamp;
                                }
                            }

                            if (groupedMoves.Any())
                            {
                                var firstPoint = groupedMoves.First().Coordinates;
                                var lastPoint = groupedMoves.Last().Coordinates;

                                // Fix: Add null checks for coordinates
                                int firstX = firstPoint?.X ?? 0;
                                int firstY = firstPoint?.Y ?? 0;
                                int lastX = lastPoint?.X ?? 0;
                                int lastY = lastPoint?.Y ?? 0;

                                // Create a grouped step with null-safe coordinate handling
                                ActionSteps.Add(new StepViewModel
                                {
                                    Index = displayIndex.ToString(),
                                    Description = $"Mouse Movement Path ({groupedMoves.Count} steps)",
                                    IsGrouped = true,
                                    GroupCount = groupedMoves.Count,
                                    GroupType = "Mouse Movements",
                                    Duration = (lastTimestamp - firstTimestamp).TotalSeconds.ToString("0.00") + "s",
                                    GroupDuration = lastTimestamp - firstTimestamp,
                                    IsMouseMove = true,
                                    GroupedItems = groupedMoves,
                                    Timestamp = firstTimestamp.ToString(),
                                    RawData = new ActionItem
                                    {
                                        EventType = 512, // Mouse move
                                        Coordinates = new Coordinates { X = firstX, Y = firstY },
                                        DeltaX = lastX - firstX,
                                        DeltaY = lastY - firstY
                                    }
                                });
                                displayIndex++;
                            }

                            // Add the last few individual moves for context
                            int lastMovesToShow = Math.Min(2, mouseMoveCount - individualMovesToShow);
                            for (int j = 0; j < lastMovesToShow; j++)
                            {
                                int index = currentIndex + mouseMoveCount - lastMovesToShow + j;
                                if (index < steps.Count)
                                {
                                    var step = steps[index];
                                    step.Index = displayIndex.ToString();
                                    ActionSteps.Add(step);
                                    displayIndex++;
                                }
                            }

                            currentIndex += mouseMoveCount;
                        }
                        else if (groupType == "MouseClick")
                        {
                            // Count consecutive mouse clicks
                            int mouseClickCount = 0;
                            for (int i = currentIndex; i < steps.Count; i++)
                            {
                                if (steps[i].IsMouseButton &&
                                    steps[i].MouseButtonType == currentStep.MouseButtonType &&
                                    steps[i].MouseButtonAction == currentStep.MouseButtonAction)
                                    mouseClickCount++;
                                else
                                    break;
                            }

                            // Add first key event individually
                            ActionSteps.Add(steps[currentIndex]);
                            steps[currentIndex].Index = displayIndex.ToString();
                            displayIndex++;

                            // Group the middle key events if there are enough
                            if (mouseClickCount > 3)
                            {
                                var groupedItems = new List<ActionItem>();
                                DateTime firstTimestamp;
                                DateTime.TryParse(steps[currentIndex + 1].Timestamp, out firstTimestamp);
                                DateTime lastTimestamp;
                                DateTime.TryParse(steps[currentIndex + mouseClickCount - 2 >= currentIndex + 1
                                    ? mouseClickCount - 2 : 1].Timestamp, out lastTimestamp);

                                for (int j = 1; j < mouseClickCount - 1 && currentIndex + j < steps.Count; j++)
                                {
                                    if (steps[currentIndex + j].RawData != null)
                                        groupedItems.Add(steps[currentIndex + j].RawData);
                                }

                                if (groupedItems.Any())
                                {
                                    ActionSteps.Add(new StepViewModel
                                    {
                                        Index = displayIndex.ToString(),
                                        Description = $"Repeated {currentStep.MouseButtonType} Click {currentStep.MouseButtonAction} ({groupedItems.Count} times)",
                                        IsGrouped = true,
                                        GroupCount = groupedItems.Count,
                                        GroupType = "Mouse Click Repetition",
                                        MouseButtonType = currentStep.MouseButtonType,
                                        MouseButtonAction = currentStep.MouseButtonAction,
                                        Duration = (lastTimestamp - firstTimestamp).TotalSeconds.ToString("0.00") + "s",
                                        GroupDuration = lastTimestamp - firstTimestamp,
                                        GroupedItems = groupedItems,
                                        Timestamp = firstTimestamp.ToString()
                                    });
                                    displayIndex++;
                                }
                            }

                            // Add the last key event if there are at least 2 events
                            if (currentIndex + mouseClickCount - 1 >= currentIndex + 1 && currentIndex + mouseClickCount - 1 < steps.Count)
                            {
                                steps[currentIndex + mouseClickCount - 1].Index = displayIndex.ToString();
                                ActionSteps.Add(steps[currentIndex + mouseClickCount - 1]);
                                displayIndex++;
                            }

                            currentIndex += mouseClickCount;
                        }
                        else if (groupType == "KeyPress")
                        {
                            string keyName = currentStep.KeyName;
                            int sameKeyCount = 0;
                            for (int i = currentIndex; i < steps.Count; i++)
                            {
                                if (!string.IsNullOrEmpty(steps[i].KeyName) && steps[i].KeyName == keyName)
                                    sameKeyCount++;
                                else
                                    break;
                            }

                            // Add first key event individually
                            ActionSteps.Add(steps[currentIndex]);
                            steps[currentIndex].Index = displayIndex.ToString();
                            displayIndex++;

                            // Group the middle key events if there are enough
                            if (sameKeyCount > 3)
                            {
                                var groupedItems = new List<ActionItem>();
                                DateTime.TryParse(steps[currentIndex + 1].Timestamp, out DateTime firstTimestamp);
                                DateTime.TryParse(steps[currentIndex + sameKeyCount - 2 >= currentIndex + 1
                                    ? sameKeyCount - 2 : 1].Timestamp, out DateTime lastTimestamp);

                                for (int j = 1; j < sameKeyCount - 1 && currentIndex + j < steps.Count; j++)
                                {
                                    if (steps[currentIndex + j].RawData != null)
                                        groupedItems.Add(steps[currentIndex + j].RawData);
                                }

                                if (groupedItems.Any())
                                {
                                    ActionSteps.Add(new StepViewModel
                                    {
                                        Index = displayIndex.ToString(),
                                        Description = $"Repeated Key {keyName} ({groupedItems.Count} times)",
                                        IsGrouped = true,
                                        GroupCount = groupedItems.Count,
                                        GroupType = "Key Repetition",
                                        KeyName = keyName,
                                        KeyCode = currentStep.KeyCode,
                                        Duration = (lastTimestamp - firstTimestamp).TotalSeconds.ToString("0.00") + "s",
                                        GroupDuration = lastTimestamp - firstTimestamp,
                                        GroupedItems = groupedItems,
                                        Timestamp = firstTimestamp.ToString()
                                    });
                                    displayIndex++;
                                }
                            }

                            // Add the last key event if there are at least 2 events
                            if (currentIndex + sameKeyCount - 1 >= currentIndex + 1 && currentIndex + sameKeyCount - 1 < steps.Count)
                            {
                                steps[currentIndex + sameKeyCount - 1].Index = displayIndex.ToString();
                                ActionSteps.Add(steps[currentIndex + sameKeyCount - 1]);
                                displayIndex++;
                            }

                            currentIndex += sameKeyCount;
                        }
                    }
                    else
                    {
                        // Add individual step
                        currentStep.Index = displayIndex.ToString();
                        ActionSteps.Add(currentStep);
                        displayIndex++;
                        currentIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GroupSimilarActions: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // If grouping fails, fall back to adding all steps individually
                for (int i = 0; i < steps.Count; i++)
                {
                    steps[i].Index = (i + 1).ToString();
                    ActionSteps.Add(steps[i]);
                }
            }
        }
    }
}
