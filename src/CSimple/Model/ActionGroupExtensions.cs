using System;
using System.Collections.Generic;
using System.Reflection;

namespace CSimple
{
    /// <summary>
    /// Extension methods for ActionGroup to safely access properties that may not exist
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

                // Try to get Files from Tag
                var tagProperty = actionGroup.GetType().GetProperty("Tag");
                if (tagProperty != null)
                {
                    var tag = tagProperty.GetValue(actionGroup);
                    return tag as List<ActionFile>;
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
                    return;
                }

                // Try to set Files to Tag
                var tagProperty = actionGroup.GetType().GetProperty("Tag");
                if (tagProperty != null)
                {
                    tagProperty.SetValue(actionGroup, files);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
