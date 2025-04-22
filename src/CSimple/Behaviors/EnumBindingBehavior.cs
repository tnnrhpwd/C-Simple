using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Reflection;

namespace CSimple.Behaviors
{
    public class EnumBindingBehavior : Behavior<Picker>
    {
        protected override void OnAttachedTo(Picker picker)
        {
            base.OnAttachedTo(picker);
            picker.Loaded += Picker_Loaded;
            picker.BindingContextChanged += Picker_BindingContextChanged;
        }

        protected override void OnDetachingFrom(Picker picker)
        {
            picker.Loaded -= Picker_Loaded;
            picker.BindingContextChanged -= Picker_BindingContextChanged;
            base.OnDetachingFrom(picker);
        }

        private void Picker_BindingContextChanged(object sender, EventArgs e)
        {
            UpdatePickerSelection((Picker)sender);
        }

        private void Picker_Loaded(object sender, EventArgs e)
        {
            UpdatePickerSelection((Picker)sender);
        }

        private void UpdatePickerSelection(Picker picker)
        {
            if (picker.BindingContext == null || picker.ItemsSource == null) return;

            try
            {
                // MAUI doesn't have a GetBinding method, so we need to use reflection
                // or just assume the binding is always "InputType" for our specific use case
                string bindingPropertyName = "InputType"; // Hard-coded assumption for our use case

                var propInfo = picker.BindingContext.GetType().GetProperty(bindingPropertyName);
                if (propInfo == null)
                {
                    Debug.WriteLine($"Property {bindingPropertyName} not found on {picker.BindingContext.GetType().Name}");
                    return;
                }

                var currentValue = propInfo.GetValue(picker.BindingContext);
                if (currentValue == null)
                {
                    Debug.WriteLine($"Current value of {bindingPropertyName} is null");
                    return;
                }

                // Find the index of the current value in the ItemsSource
                int index = -1;
                for (int i = 0; i < picker.ItemsSource.Count; i++)
                {
                    if (picker.ItemsSource[i].Equals(currentValue))
                    {
                        index = i;
                        break;
                    }
                }

                // Set the selected index
                if (index >= 0)
                {
                    picker.SelectedIndex = index;
                    Debug.WriteLine($"EnumBindingBehavior: Set picker to index {index} for value {currentValue}");
                }
                else
                {
                    Debug.WriteLine($"Value {currentValue} not found in ItemsSource");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in EnumBindingBehavior: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
