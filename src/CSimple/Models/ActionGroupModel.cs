using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple
{

    public class ActionModifier
    {
        public string ModifierName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
        public Func<ActionItem, int> Condition { get; set; }
        public Action<ActionItem> ModifyAction { get; set; }
    }

    public class ActionItem
    {
        public object Timestamp { get; set; }
        public int EventType { get; set; }
        public int KeyCode { get; set; }
        public int Duration { get; set; }

        // Enhanced mouse movement properties
        public Coordinates Coordinates { get; set; }
        public int DeltaX { get; set; }
        public int DeltaY { get; set; }
        public uint MouseData { get; set; }
        public uint Flags { get; set; }
        public bool IsLeftButtonDown { get; set; }
        public bool IsRightButtonDown { get; set; }
        public bool IsMiddleButtonDown { get; set; }
        public long TimeSinceLastMoveMs { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }

        // Touch input properties
        public bool IsTouch { get; set; }
        public int TouchId { get; set; }
        public int TouchAction { get; set; } // 0=None, 1=Down, 2=Move, 3=Up, 4=Cancel
        public int TouchWidth { get; set; }  // Contact width in device units
        public int TouchHeight { get; set; } // Contact height in device units
        public float Pressure { get; set; }  // Touch pressure if available

        public override string ToString()
        {
            if (IsTouch)
            {
                string actionName = TouchAction == 1 ? "Down" :
                                   TouchAction == 2 ? "Move" :
                                   TouchAction == 3 ? "Up" :
                                   TouchAction == 4 ? "Cancel" : "Unknown";

                return $"Touch {actionName} at X:{Coordinates?.X ?? 0}, Y:{Coordinates?.Y ?? 0}, ID:{TouchId}";
            }
            if (EventType == 256 || EventType == 257) // Keyboard events
                return $"Key {KeyCode} {(EventType == 256 ? "Down" : "Up")}";
            else if (EventType == 512) // Mouse move
                return $"Mouse Move to X:{Coordinates?.X ?? 0}, Y:{Coordinates?.Y ?? 0} (Delta:{DeltaX},{DeltaY})";
            else if (EventType == 0x0201) // Left mouse button down
                return $"Left Click at X:{Coordinates?.X ?? 0}, Y:{Coordinates?.Y ?? 0}";
            else if (EventType == 0x0204) // Right mouse button down
                return $"Right Click at X:{Coordinates?.X ?? 0}, Y:{Coordinates?.Y ?? 0}";
            else
                return $"Action Type:{EventType} at {Timestamp}";
        }
    }

    public class Coordinates
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int AbsoluteX { get; set; }
        public int AbsoluteY { get; set; }
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
    }

    public class ModelAssignment
    {
        public string ModelId { get; set; }
        public string ModelName { get; set; }
        public string ModelType { get; set; }
        public DateTime AssignedDate { get; set; } = DateTime.Now;
    }

    public class ActionFile
    {
        public string Filename { get; set; }
        public string ContentType { get; set; }
        public string Data { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public bool IsProcessed { get; set; } = false;
    }

    /// <summary>
    /// Extension methods for ActionGroup
    /// </summary>
    public static class ActionGroupExtensions
    {
        /// <summary>
        /// Gets the Files property from an ActionGroup or returns null if it doesn't exist
        /// </summary>
        public static List<ActionFile> GetFiles(this ActionGroup actionGroup)
        {
            if (actionGroup == null) return null;

            try
            {
                // Try to get Files via reflection
                var filesProperty = actionGroup.GetType().GetProperty("Files");
                if (filesProperty != null)
                {
                    return filesProperty.GetValue(actionGroup) as List<ActionFile>;
                }
            }
            catch
            {
                // Ignore errors and return null
            }

            return null;
        }

        /// <summary>
        /// Sets the Files property on an ActionGroup if it exists
        /// </summary>
        public static void SetFiles(this ActionGroup actionGroup, List<ActionFile> files)
        {
            if (actionGroup == null) return;

            try
            {
                // Try to set Files via reflection
                var filesProperty = actionGroup.GetType().GetProperty("Files");
                if (filesProperty != null)
                {
                    filesProperty.SetValue(actionGroup, files);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
