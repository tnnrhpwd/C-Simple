using System;
using Microsoft.Maui.Controls;

namespace CSimple.Behaviors
{
    /// <summary>
    /// Behavior that helps bind Enum values to Pickers.
    /// This behavior automatically converts between the enum values and their string representations.
    /// </summary>
    public class EnumBindingBehavior : Behavior<Picker>
    {
        private Picker _picker;
        private Type _enumType;
        private bool _isAttached;

        /// <summary>
        /// Called when the behavior is attached to a control.
        /// </summary>
        protected override void OnAttachedTo(Picker bindable)
        {
            base.OnAttachedTo(bindable);
            _picker = bindable;
            _isAttached = true;

            // Subscribe to necessary events
            _picker.BindingContextChanged += OnBindingContextChanged;
            _picker.PropertyChanged += OnPickerPropertyChanged;
        }

        /// <summary>
        /// Called when the behavior is detached from a control.
        /// </summary>
        protected override void OnDetachingFrom(Picker bindable)
        {
            base.OnDetachingFrom(bindable);

            // Unsubscribe from events
            if (_picker != null)
            {
                _picker.BindingContextChanged -= OnBindingContextChanged;
                _picker.PropertyChanged -= OnPickerPropertyChanged;
            }

            _picker = null;
            _isAttached = false;
        }

        private void OnBindingContextChanged(object sender, EventArgs e)
        {
            if (_isAttached && _picker != null)
            {
                // Check if ItemsSource is an Enum and update enum type
                if (_picker.ItemsSource != null && _picker.ItemsSource is Array array && array.Length > 0)
                {
                    var firstItem = array.GetValue(0);
                    if (firstItem != null && firstItem.GetType().IsEnum)
                    {
                        _enumType = firstItem.GetType();
                    }
                }
            }
        }

        private void OnPickerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!_isAttached || _picker == null) return;

            // Handle SelectedItem change
            if (e.PropertyName == nameof(Picker.SelectedItem) && _enumType != null)
            {                // Get the selected enum value
                var selectedValue = _picker.SelectedItem;
                if (selectedValue != null && selectedValue is Enum enumValue)
                {
                    // Make sure the selected value is properly set as the enum value
                    // This ensures proper binding even when displaying string representations
                    _picker.SetValue(Picker.SelectedItemProperty, enumValue);
                }
            }

            // Handle ItemsSource change
            else if (e.PropertyName == nameof(Picker.ItemsSource))
            {
                // Update enum type if ItemsSource is an enum array
                if (_picker.ItemsSource != null && _picker.ItemsSource is Array array && array.Length > 0)
                {
                    var firstItem = array.GetValue(0);
                    if (firstItem != null && firstItem.GetType().IsEnum)
                    {
                        _enumType = firstItem.GetType();
                    }
                }
            }
        }
    }
}
