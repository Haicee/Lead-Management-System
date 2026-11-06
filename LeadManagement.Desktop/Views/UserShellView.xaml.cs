using System.Windows;
using System.Windows.Controls;
using LeadManagement.Desktop.ViewModels;

namespace LeadManagement.Desktop.Views
{
    public partial class UserShellView : UserControl
    {
        public UserShellView()
        {
            InitializeComponent();
        }

        private async void UserShellView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel mainVm)
            {
                if (mainVm.UserDashboard.AssignedClients.Count == 0)
                {
                    await mainVm.UserDashboard.LoadAssignedClientsAsync();
                }
            }
        }
    }
}
