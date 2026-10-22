using System.Windows.Input;

namespace LeadManagement.Desktop.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private bool _isLoggedIn = false; // Starts on the Login page
        private string _username = "admin";
        private string _password = string.Empty;
        private string _currentView = "Dashboard";

        // Dashboard Placeholder Summary Metrics
        private string _totalLeads = "1,280";
        private string _newLeads = "+142";
        private string _qualifiedLeads = "486";
        private string _conversionRate = "24.5%";

        public MainViewModel()
        {
            // Commands
            LoginCommand = new RelayCommand(ExecuteLogin);
            LogoutCommand = new RelayCommand(ExecuteLogout);
            NavigateCommand = new RelayCommand(ExecuteNavigate);
        }

        public string AppTitle => "Lead Management System";
        public string ActiveUser => "Administrator";
        public string BackendStatus => "Local Mode (Offline Test)";

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set => SetProperty(ref _isLoggedIn, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    OnPropertyChanged(nameof(IsDashboardActive));
                    OnPropertyChanged(nameof(IsMasterlistActive));
                    OnPropertyChanged(nameof(IsImportLeadsActive));
                    OnPropertyChanged(nameof(IsImportHistoryActive));
                    OnPropertyChanged(nameof(IsSettingsActive));
                }
            }
        }

        // Active State Helpers for Sidebar Buttons
        public bool IsDashboardActive => CurrentView == "Dashboard";
        public bool IsMasterlistActive => CurrentView == "LeadMasterlist";
        public bool IsImportLeadsActive => CurrentView == "ImportLeads";
        public bool IsImportHistoryActive => CurrentView == "ImportHistory";
        public bool IsSettingsActive => CurrentView == "Settings";

        // Metric Card Properties
        public string TotalLeads
        {
            get => _totalLeads;
            set => SetProperty(ref _totalLeads, value);
        }

        public string NewLeads
        {
            get => _newLeads;
            set => SetProperty(ref _newLeads, value);
        }

        public string QualifiedLeads
        {
            get => _qualifiedLeads;
            set => SetProperty(ref _qualifiedLeads, value);
        }

        public string ConversionRate
        {
            get => _conversionRate;
            set => SetProperty(ref _conversionRate, value);
        }

        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand NavigateCommand { get; }

        private void ExecuteLogin()
        {
            // Direct entry for testing without database connection
            IsLoggedIn = true;
            CurrentView = "Dashboard";
        }

        private void ExecuteLogout()
        {
            IsLoggedIn = false;
            CurrentView = "Dashboard";
        }

        private void ExecuteNavigate(object? parameter)
        {
            if (parameter is string destination && !string.IsNullOrWhiteSpace(destination))
            {
                CurrentView = destination;
            }
        }
    }
}
