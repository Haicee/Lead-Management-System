using System.Windows;
using System.Windows.Controls;
using LeadManagement.Desktop.ViewModels;

namespace LeadManagement.Desktop.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            IsVisibleChanged += LoginView_IsVisibleChanged;
        }

        private void LoginView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                LoginPasswordBox.Clear();
            }
        }

        /// <summary>
        /// Syncs the PasswordBox value into the ViewModel whenever it changes.
        /// PasswordBox doesn't support binding for security reasons, so we use code-behind.
        /// </summary>
        private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Password = LoginPasswordBox.Password;
            }
        }

        /// <summary>
        /// Clears the PasswordBox when the login view loads.
        /// </summary>
        private void LoginView_Loaded(object sender, RoutedEventArgs e)
        {
            LoginPasswordBox.Clear();
        }
    }
}
