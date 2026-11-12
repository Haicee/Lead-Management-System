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
    }
}
