using System.Windows;
using System.Windows.Controls;
using LeadManagement.Desktop.ViewModels;

namespace LeadManagement.Desktop.Views
{
    public partial class ImportHistoryView : UserControl
    {
        public ImportHistoryView()
        {
            InitializeComponent();
            Loaded += ImportHistoryView_Loaded;
        }

        private void ImportHistoryView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ImportHistoryViewModel vm)
            {
                _ = vm.LoadBatchesAsync();
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
