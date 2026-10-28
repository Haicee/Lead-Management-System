using System.Windows.Input;
using LeadManagement.Desktop.Services;

namespace LeadManagement.Desktop.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private bool _isLoggedIn = false; // Starts on the Login page
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _currentView = "Dashboard";
        private bool _isSidebarCollapsed = false;
        private bool _isLoggingIn = false;
        private string _loginErrorMessage = string.Empty;
        private string _activeUserDisplay = string.Empty;
        private string _activeUserRole = string.Empty;

        // Dashboard Placeholder Summary Metrics
        private string _totalLeads = "1,280";
        private string _newLeads = "+142";
        private string _qualifiedLeads = "486";
        private string _conversionRate = "24.5%";

        public MainViewModel()
        {
            // Commands
            LoginCommand = new RelayCommand(async () => await ExecuteLoginAsync(), () => !IsLoggingIn);
            LogoutCommand = new RelayCommand(ExecuteLogout);
            NavigateCommand = new RelayCommand(ExecuteNavigate);
            ToggleSidebarCommand = new RelayCommand(ExecuteToggleSidebar);
        }

        public string AppTitle => "Leads Management System";
        public string BackendStatus => "Online";

        public LeadMasterlistViewModel Masterlist { get; } = new();
        public ImportLeadsViewModel ImportLeads { get; } = new();
        public ImportHistoryViewModel ImportHistory { get; } = new();

        // --- Active User Display (populated from API response) ---

        public string ActiveUser
        {
            get => _activeUserDisplay;
            set => SetProperty(ref _activeUserDisplay, value);
        }

        public string ActiveUserRole
        {
            get => _activeUserRole;
            set => SetProperty(ref _activeUserRole, value);
        }

        // --- Sidebar ---

        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(SidebarWidth));
                }
            }
        }

        public double SidebarWidth => _isSidebarCollapsed ? 68.0 : 230.0;

        // --- Login State ---

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set => SetProperty(ref _isLoggedIn, value);
        }

        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    // Clear error when user starts typing
                    LoginErrorMessage = string.Empty;
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    LoginErrorMessage = string.Empty;
                }
            }
        }

        /// <summary>
        /// True while the login API call is in progress — used to show a spinner and disable the button.
        /// </summary>
        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set
            {
                if (SetProperty(ref _isLoggingIn, value))
                {
                    OnPropertyChanged(nameof(LoginButtonText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Error message displayed below the login form when auth fails.
        /// </summary>
        public string LoginErrorMessage
        {
            get => _loginErrorMessage;
            set
            {
                if (SetProperty(ref _loginErrorMessage, value))
                {
                    OnPropertyChanged(nameof(HasLoginError));
                }
            }
        }

        public bool HasLoginError => !string.IsNullOrEmpty(LoginErrorMessage);
        public string LoginButtonText => IsLoggingIn ? "Signing In..." : "Sign In to Dashboard";

        // --- View Navigation ---

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

        // --- Commands ---

        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand NavigateCommand { get; }
        public ICommand ToggleSidebarCommand { get; }

        private void ExecuteToggleSidebar()
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
        }

        /// <summary>
        /// Calls the PHP backend to authenticate the user.
        /// On success, sets IsLoggedIn = true and populates user info.
        /// On failure, shows a clean error message in the UI.
        /// </summary>
        private async Task ExecuteLoginAsync()
        {
            // Client-side validation
            if (string.IsNullOrWhiteSpace(Username))
            {
                LoginErrorMessage = "Please enter your username.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                LoginErrorMessage = "Please enter your password.";
                return;
            }

            IsLoggingIn = true;
            LoginErrorMessage = string.Empty;

            try
            {
                var (success, message) = await ApiService.Instance.LoginAsync(Username, Password);

                if (success)
                {
                    // Populate user display info from the API response
                    var user = ApiService.Instance.CurrentUser;
                    if (user != null)
                    {
                        ActiveUser = user.FullName;
                        ActiveUserRole = user.Role == "admin" ? "Administrator" : "User";
                    }

                    // Clear sensitive data from memory
                    Password = string.Empty;

                    // Transition to the main dashboard
                    IsLoggedIn = true;
                    CurrentView = "Dashboard";
                }
                else
                {
                    LoginErrorMessage = message;
                }
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        private void ExecuteLogout()
        {
            ApiService.Instance.Logout();

            // Reset all login state
            IsLoggedIn = false;
            Username = string.Empty;
            Password = string.Empty;
            LoginErrorMessage = string.Empty;
            ActiveUser = string.Empty;
            ActiveUserRole = string.Empty;
            CurrentView = "Dashboard";
        }

        private void ExecuteNavigate(object? parameter)
        {
            if (parameter is string destination && !string.IsNullOrWhiteSpace(destination))
            {
                CurrentView = destination;
                if (destination == "LeadMasterlist")
                {
                    _ = Masterlist.LoadLeadsAsync();
                }
                else if (destination == "ImportHistory")
                {
                    _ = ImportHistory.LoadBatchesAsync();
                }
            }
        }
    }
}
