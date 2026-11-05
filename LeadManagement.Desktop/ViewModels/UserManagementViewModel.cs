using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.Services;

namespace LeadManagement.Desktop.ViewModels
{
    public class UserManagementViewModel : BaseViewModel
    {
        private ObservableCollection<UserModel> _users = new();
        private bool _isLoading = false;
        private bool _isAddUserModalOpen = false;
        private bool _isSaving = false;

        // Form Fields
        private string _newFullName = string.Empty;
        private string _newUsername = string.Empty;
        private string _newEmail = string.Empty;
        private string _newPassword = string.Empty;
        private string _newRole = "user"; // "user" or "admin"

        // Feedback & Banners
        private string _formErrorMessage = string.Empty;
        private string _statusBannerMessage = string.Empty;
        private bool _isStatusBannerVisible = false;
        private bool _isBannerError = false;

        public UserManagementViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadUsersAsync());
            OpenAddUserModalCommand = new RelayCommand(ExecuteOpenAddUserModal);
            CloseAddUserModalCommand = new RelayCommand(ExecuteCloseAddUserModal);
            CreateUserCommand = new RelayCommand(async () => await ExecuteCreateUserAsync(), () => CanCreateUser);
            DismissBannerCommand = new RelayCommand(() => IsStatusBannerVisible = false);

            AvailableRoles = new ObservableCollection<string> { "user", "admin" };

            // Load users initially
            _ = LoadUsersAsync();
        }

        public ObservableCollection<UserModel> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(UserCountText));
                }
            }
        }

        public int TotalUsers => Users.Count;
        public int TotalAgents => Users.Count(u => !u.IsAdmin);
        public int TotalAdmins => Users.Count(u => u.IsAdmin);

        public string UserCountText
        {
            get
            {
                if (IsLoading) return "Loading accounts...";
                return $"{TotalUsers} Registered Accounts ({TotalAdmins} Admin, {TotalAgents} Agents)";
            }
        }

        public bool IsAddUserModalOpen
        {
            get => _isAddUserModalOpen;
            set => SetProperty(ref _isAddUserModalOpen, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                if (SetProperty(ref _isSaving, value))
                {
                    OnPropertyChanged(nameof(CanCreateUser));
                    OnPropertyChanged(nameof(SaveButtonText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SaveButtonText => IsSaving ? "Creating Account..." : "Create Account";

        public ObservableCollection<string> AvailableRoles { get; }

        // Form Properties
        public string NewFullName
        {
            get => _newFullName;
            set
            {
                if (SetProperty(ref _newFullName, value))
                {
                    FormErrorMessage = string.Empty;
                }
            }
        }

        public string NewUsername
        {
            get => _newUsername;
            set
            {
                if (SetProperty(ref _newUsername, value))
                {
                    FormErrorMessage = string.Empty;
                }
            }
        }

        public string NewEmail
        {
            get => _newEmail;
            set
            {
                if (SetProperty(ref _newEmail, value))
                {
                    FormErrorMessage = string.Empty;
                }
            }
        }

        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                {
                    FormErrorMessage = string.Empty;
                }
            }
        }

        public string NewRole
        {
            get => _newRole;
            set => SetProperty(ref _newRole, value);
        }

        public string FormErrorMessage
        {
            get => _formErrorMessage;
            set
            {
                if (SetProperty(ref _formErrorMessage, value))
                {
                    OnPropertyChanged(nameof(HasFormError));
                }
            }
        }

        public bool HasFormError => !string.IsNullOrEmpty(FormErrorMessage);

        public string StatusBannerMessage
        {
            get => _statusBannerMessage;
            set => SetProperty(ref _statusBannerMessage, value);
        }

        public bool IsStatusBannerVisible
        {
            get => _isStatusBannerVisible;
            set => SetProperty(ref _isStatusBannerVisible, value);
        }

        public bool IsBannerError
        {
            get => _isBannerError;
            set => SetProperty(ref _isBannerError, value);
        }

        public bool CanCreateUser => !IsSaving;

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand OpenAddUserModalCommand { get; }
        public ICommand CloseAddUserModalCommand { get; }
        public ICommand CreateUserCommand { get; }
        public ICommand DismissBannerCommand { get; }

        public async Task LoadUsersAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                var (success, _, users) = await ApiService.Instance.GetUsersAsync();

                if (success && users != null)
                {
                    Users = new ObservableCollection<UserModel>(users);
                }
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(TotalUsers));
                OnPropertyChanged(nameof(TotalAgents));
                OnPropertyChanged(nameof(TotalAdmins));
                OnPropertyChanged(nameof(UserCountText));
            }
        }

        private void ExecuteOpenAddUserModal()
        {
            NewFullName = string.Empty;
            NewUsername = string.Empty;
            NewEmail = string.Empty;
            NewPassword = string.Empty;
            NewRole = "user";
            FormErrorMessage = string.Empty;
            IsAddUserModalOpen = true;
        }

        private void ExecuteCloseAddUserModal()
        {
            IsAddUserModalOpen = false;
            FormErrorMessage = string.Empty;
        }

        private async Task ExecuteCreateUserAsync()
        {
            // Validation
            if (string.IsNullOrWhiteSpace(NewFullName))
            {
                FormErrorMessage = "Please enter the user's full name.";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewUsername))
            {
                FormErrorMessage = "Please enter a username.";
                return;
            }

            if (NewUsername.Trim().Length < 3)
            {
                FormErrorMessage = "Username must be at least 3 characters long.";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                FormErrorMessage = "Please enter a password.";
                return;
            }

            if (NewPassword.Length < 6)
            {
                FormErrorMessage = "Password must be at least 6 characters long.";
                return;
            }

            IsSaving = true;
            FormErrorMessage = string.Empty;

            try
            {
                var (success, message, createdUser) = await ApiService.Instance.CreateUserAsync(
                    NewUsername,
                    NewPassword,
                    NewFullName,
                    NewEmail,
                    NewRole
                );

                if (success)
                {
                    IsAddUserModalOpen = false;
                    StatusBannerMessage = $"✓ Account '{NewUsername}' created successfully as {(NewRole == "admin" ? "Administrator" : "Standard User (Agent)")}.";
                    IsBannerError = false;
                    IsStatusBannerVisible = true;

                    // Reload user list
                    await LoadUsersAsync();
                }
                else
                {
                    FormErrorMessage = message;
                }
            }
            catch (Exception ex)
            {
                FormErrorMessage = $"Error creating account: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}

