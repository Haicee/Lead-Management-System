using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using LeadManagement.Desktop.Models;

namespace LeadManagement.Desktop.Controls
{
    /// <summary>
    /// A reusable multi-select checkbox dropdown control with an inline search bar.
    /// Displays a ToggleButton that shows the selection summary (e.g. "All Cities" or "3 selected"),
    /// and opens a Popup with a live-search field and a scrollable checklist of FilterItem entries.
    /// OR logic: selecting multiple items shows leads matching any of them.
    /// </summary>
    public partial class MultiSelectDropdown : UserControl, INotifyPropertyChanged
    {
        public MultiSelectDropdown()
        {
            InitializeComponent();
        }

        // ─── Dependency Properties ───────────────────────────────────────

        /// <summary>The full collection of FilterItem options for this filter category.</summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(ObservableCollection<FilterItem>),
                typeof(MultiSelectDropdown),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public ObservableCollection<FilterItem> ItemsSource
        {
            get => (ObservableCollection<FilterItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>Placeholder text shown on the button when nothing is selected (e.g. "All Counties").</summary>
        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(
                nameof(HeaderText),
                typeof(string),
                typeof(MultiSelectDropdown),
                new PropertyMetadata("Select...", OnHeaderTextChanged));

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        // ─── View-state Properties (INotifyPropertyChanged) ───────────────

        private bool _isDropdownOpen;
        public bool IsDropdownOpen
        {
            get => _isDropdownOpen;
            set
            {
                _isDropdownOpen = value;
                OnPropertyChanged();

                // Clear search text whenever the dropdown is closed
                if (!value)
                    SearchText = string.Empty;
            }
        }

        private string _searchText = string.Empty;

        /// <summary>Live-filter text typed inside the popup search box.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplySearch();
            }
        }

        /// <summary>Summary text displayed on the toggle button face.</summary>
        public string SummaryText
        {
            get
            {
                if (ItemsSource == null) return HeaderText;
                int count = ItemsSource.Count(i => i.IsSelected);
                return count == 0 ? HeaderText : $"{count} selected";
            }
        }

        /// <summary>True when at least one item is checked — drives the blue highlight style on the button.</summary>
        public bool HasSelection => ItemsSource?.Any(i => i.IsSelected) == true;

        /// <summary>Shows how many items match the current search, e.g. "12 of 48".</summary>
        public string FilteredCountText
        {
            get
            {
                if (ItemsSource == null) return string.Empty;
                int total = ItemsSource.Count;
                if (string.IsNullOrWhiteSpace(_searchText)) return $"{total} items";
                int shown = ItemsSource.Count(i => MatchesSearch(i.Name));
                return $"{shown} of {total}";
            }
        }

        // ─── Dependency Property Callbacks ────────────────────────────────

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MultiSelectDropdown)d;

            // Unsubscribe from old collection events
            if (e.OldValue is ObservableCollection<FilterItem> old)
            {
                old.CollectionChanged -= ctrl.Items_CollectionChanged;
                foreach (var item in old)
                    item.PropertyChanged -= ctrl.Item_PropertyChanged;
            }

            // Subscribe to new collection events
            if (e.NewValue is ObservableCollection<FilterItem> newColl)
            {
                newColl.CollectionChanged += ctrl.Items_CollectionChanged;
                foreach (var item in newColl)
                    item.PropertyChanged += ctrl.Item_PropertyChanged;
            }

            ctrl.ApplySearch();
            ctrl.RefreshSummary();
        }

        private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MultiSelectDropdown)d).RefreshSummary();
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (FilterItem item in e.OldItems)
                    item.PropertyChanged -= Item_PropertyChanged;

            if (e.NewItems != null)
                foreach (FilterItem item in e.NewItems)
                    item.PropertyChanged += Item_PropertyChanged;

            ApplySearch();
            RefreshSummary();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterItem.IsSelected))
                RefreshSummary();
        }

        private void RefreshSummary()
        {
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(FilteredCountText));
        }

        // ─── Search Filtering ─────────────────────────────────────────────

        private bool MatchesSearch(string name)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            return name.Contains(_searchText.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Rebuilds the popup ItemsControl with only items matching the current search text.
        /// Only items whose name matches the search query are shown (whether checked or not).
        /// </summary>
        private void ApplySearch()
        {
            if (PART_ItemsList == null || ItemsSource == null) return;

            var filtered = ItemsSource
                .Where(i => MatchesSearch(i.Name))
                .ToList();

            PART_ItemsList.ItemsSource = filtered;

            // Show "no matches" state only when search is active and nothing found
            bool hasSearchTerm = !string.IsNullOrWhiteSpace(_searchText);
            bool noResults = hasSearchTerm && filtered.Count == 0;

            if (PART_EmptyState != null)
                PART_EmptyState.Visibility = noResults ? Visibility.Visible : Visibility.Collapsed;

            OnPropertyChanged(nameof(FilteredCountText));
        }

        // ─── Popup Opened: auto-focus search box ──────────────────────────

        private void PART_Popup_Opened(object sender, System.EventArgs e)
        {
            // Reset search and re-apply so list is fresh each time popup opens
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            ApplySearch();

            // Auto-focus the search box so user can start typing immediately
            PART_SearchBox?.Focus();
        }

        // ─── Button Handlers ──────────────────────────────────────────────

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsSource == null) return;
            // Select All applies only to the currently visible (filtered) items
            var visible = PART_ItemsList.ItemsSource as System.Collections.Generic.List<FilterItem>;
            var targets = visible ?? ItemsSource.ToList();
            foreach (var item in targets)
                item.IsSelected = true;
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsSource == null) return;
            // Clear applies only to currently visible (filtered) items if a search is active, otherwise clears all
            var visible = PART_ItemsList.ItemsSource as System.Collections.Generic.List<FilterItem>;
            var targets = (!string.IsNullOrWhiteSpace(_searchText) && visible != null) ? visible : (System.Collections.Generic.IEnumerable<FilterItem>)ItemsSource;
            foreach (var item in targets)
                item.IsSelected = false;
        }

        // ─── INotifyPropertyChanged ───────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
