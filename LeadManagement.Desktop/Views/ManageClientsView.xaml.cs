using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.ViewModels;

namespace LeadManagement.Desktop.Views
{
    public partial class ManageClientsView : UserControl
    {
        public ManageClientsView()
        {
            InitializeComponent();
        }

        private async void ManageClientsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ManageClientsViewModel vm)
            {
                await vm.LoadClientsAsync();
            }
        }

        /// <summary>
        /// Handles DatePicker selection without relying on WPF binding UpdateSourceTrigger,
        /// which can misbehave with row virtualization causing the "jumping date" bug.
        /// We read the DataContext directly from the DatePicker's containing row.
        /// </summary>
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DatePicker dp) return;

            // Find the DataGridRow that owns this DatePicker
            var row = FindAncestor<DataGridRow>(dp);
            if (row?.DataContext is not LeadModel lead) return;

            // Only update if the value actually changed — prevents feedback loop
            var picked = dp.SelectedDate;
            if (lead.SelectedDate == picked) return;

            lead.SelectedDate = picked;
        }

        private static T? FindAncestor<T>(System.Windows.DependencyObject child) where T : System.Windows.DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T match) return match;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
