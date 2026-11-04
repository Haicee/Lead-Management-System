using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LeadManagement.Desktop.Models
{
    /// <summary>
    /// Represents a single selectable option in a multi-select filter dropdown.
    /// Implements INotifyPropertyChanged so the UI reflects checkbox state changes in real-time.
    /// </summary>
    public class FilterItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>Display label shown in the dropdown list and used for filter matching.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether this item is currently checked in the multi-select dropdown.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

