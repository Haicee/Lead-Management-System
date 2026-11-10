using System.ComponentModel;
using System.Windows.Controls;
using LeadManagement.Desktop.ViewModels;

namespace LeadManagement.Desktop.Views
{
    public partial class LeadMasterlistView : UserControl
    {
        public LeadMasterlistView()
        {
            InitializeComponent();
        }

        private void OnDataGridSorting(object sender, DataGridSortingEventArgs e)
        {
            if (DataContext is LeadMasterlistViewModel vm)
            {
                e.Handled = true;
                var column = e.Column;
                var sortMemberPath = column.SortMemberPath;
                if (string.IsNullOrEmpty(sortMemberPath)) return;

                var newDirection = (column.SortDirection != ListSortDirection.Ascending)
                    ? ListSortDirection.Ascending
                    : ListSortDirection.Descending;

                if (sender is DataGrid dg)
                {
                    foreach (var col in dg.Columns)
                    {
                        if (col != column) col.SortDirection = null;
                    }
                }

                column.SortDirection = newDirection;
                vm.SortMatchedLeads(sortMemberPath, newDirection);
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
