using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.Services;

namespace LeadManagement.Desktop.ViewModels
{
    public class UserManagementViewModel : BaseViewModel
    {
        private ObservableCollection<UserModel> _users = new();
        private bool _isLoading = false;

        // ── Add Modal ──────────────────────────────────────────────────
        private bool _isAddUserModalOpen = false;
        private bool _isSaving = false;
        private string _newFullName = string.Empty;
        private string _newUsername = string.Empty;
        private string _newEmail = string.Empty;
        private string _newPassword = string.Empty;
        private string _newRole = "user";

        // ── Edit Modal ─────────────────────────────────────────────────
        private bool _isEditUserModalOpen = false;
        private bool _isUpdating = false;
        private UserModel? _editingUser = null;
        private string _editFullName = string.Empty;
        private string _editUsername = string.Empty;
        private string _editEmail = string.Empty;
        private string _editRole = "user";
        private string _editStatusDisplay = "Active";
        private string _editPassword = string.Empty;
        private string _editFormErrorMessage = string.Empty;

        // ── Shared Feedback ────────────────────────────────────────────
        private string _formErrorMessage = string.Empty;
        private string _statusBannerMessage = string.Empty;
        private bool _isStatusBannerVisible = false;
        private bool _isBannerError = false;

        public UserManagementViewModel()
        {
            RefreshCommand            = new RelayCommand(async () => await LoadUsersAsync());
            OpenAddUserModalCommand   = new RelayCommand(ExecuteOpenAddUserModal);
            CloseAddUserModalCommand  = new RelayCommand(ExecuteCloseAddUserModal);
            CreateUserCommand         = new RelayCommand(async () => await ExecuteCreateUserAsync(), () => CanCreateUser);
            OpenEditUserModalCommand  = new RelayCommand<UserModel>(ExecuteOpenEditUserModal);
            CloseEditUserModalCommand = new RelayCommand(ExecuteCloseEditUserModal);
            UpdateUserCommand         = new RelayCommand(async () => await ExecuteUpdateUserAsync(), () => CanUpdateUser);
            DeleteUserCommand         = new RelayCommand<UserModel>(async (u) => await ExecuteDeleteUserAsync(u));
            DismissBannerCommand      = new RelayCommand(() => IsStatusBannerVisible = false);

            AvailableRoles    = new ObservableCollection<string> { "User", "Admin" };
            AvailableStatuses = new ObservableCollection<string> { "Active", "Inactive" };

            _ = LoadUsersAsync();
        }

        // ── Collections ────────────────────────────────────────────────

        public ObservableCollection<UserModel> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public ObservableCollection<string> AvailableRoles { get; }
        public ObservableCollection<string> AvailableStatuses { get; }

        // ── Counts / Header ────────────────────────────────────────────

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    OnPropertyChanged(nameof(UserCountText));
            }
        }

        public int TotalUsers  => Users.Count;
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

        // ── Add Modal Properties ───────────────────────────────────────

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
        public bool   CanCreateUser  => !IsSaving;

        public string NewFullName
        {
            get => _newFullName;
            set { if (SetProperty(ref _newFullName, value)) FormErrorMessage = string.Empty; }
        }
        public string NewUsername
        {
            get => _newUsername;
            set { if (SetProperty(ref _newUsername, value)) FormErrorMessage = string.Empty; }
        }
        public string NewEmail
        {
            get => _newEmail;
            set { if (SetProperty(ref _newEmail, value)) FormErrorMessage = string.Empty; }
        }
        public string NewPassword
        {
            get => _newPassword;
            set { if (SetProperty(ref _newPassword, value)) FormErrorMessage = string.Empty; }
        }
        public string NewRole
        {
            get => _newRole;
            set => SetProperty(ref _newRole, value);
        }

        public string FormErrorMessage
        {
            get => _formErrorMessage;
            set { if (SetProperty(ref _formErrorMessage, value)) OnPropertyChanged(nameof(HasFormError)); }
        }
        public bool HasFormError => !string.IsNullOrEmpty(FormErrorMessage);

        // ── Edit Modal Properties ──────────────────────────────────────

        public bool IsEditUserModalOpen
        {
            get => _isEditUserModalOpen;
            set => SetProperty(ref _isEditUserModalOpen, value);
        }

        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                if (SetProperty(ref _isUpdating, value))
                {
                    OnPropertyChanged(nameof(CanUpdateUser));
                    OnPropertyChanged(nameof(UpdateButtonText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string UpdateButtonText => IsUpdating ? "Saving Changes..." : "Save Changes";
        public bool   CanUpdateUser    => !IsUpdating;

        public string EditModalSubtitle => _editingUser != null
            ? $"Editing account for {_editingUser.FullName} (@{_editingUser.Username})"
            : "Edit user account details.";

        public string EditFullName
        {
            get => _editFullName;
            set { if (SetProperty(ref _editFullName, value)) EditFormErrorMessage = string.Empty; }
        }
        public string EditUsername
        {
            get => _editUsername;
            set { if (SetProperty(ref _editUsername, value)) EditFormErrorMessage = string.Empty; }
        }
        public string EditEmail
        {
            get => _editEmail;
            set => SetProperty(ref _editEmail, value);
        }
        public string EditRole
        {
            get => _editRole;
            set => SetProperty(ref _editRole, value);
        }
        public string EditStatusDisplay
        {
            get => _editStatusDisplay;
            set => SetProperty(ref _editStatusDisplay, value);
        }
        public string EditPassword
        {
            get => _editPassword;
            set { if (SetProperty(ref _editPassword, value)) EditFormErrorMessage = string.Empty; }
        }

        public string EditFormErrorMessage
        {
            get => _editFormErrorMessage;
            set { if (SetProperty(ref _editFormErrorMessage, value)) OnPropertyChanged(nameof(HasEditFormError)); }
        }
        public bool HasEditFormError => !string.IsNullOrEmpty(EditFormErrorMessage);

        // ── Shared Banner ──────────────────────────────────────────────

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

        // ── Commands ───────────────────────────────────────────────────

        public ICommand RefreshCommand            { get; }
        public ICommand OpenAddUserModalCommand   { get; }
        public ICommand CloseAddUserModalCommand  { get; }
        public ICommand CreateUserCommand         { get; }
        public ICommand OpenEditUserModalCommand  { get; }
        public ICommand CloseEditUserModalCommand { get; }
        public ICommand UpdateUserCommand         { get; }
        public ICommand DeleteUserCommand         { get; }
        public ICommand DismissBannerCommand      { get; }

        // ── Load ───────────────────────────────────────────────────────

        public async Task LoadUsersAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var (success, _, users) = await ApiService.Instance.GetUsersAsync();
                if (success && users != null)
                    Users = new ObservableCollection<UserModel>(users);
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

        // ── Add User ───────────────────────────────────────────────────

        private void ExecuteOpenAddUserModal()
        {
            NewFullName = NewUsername = NewEmail = NewPassword = string.Empty;
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
            if (string.IsNullOrWhiteSpace(NewFullName))  { FormErrorMessage = "Please enter the user's full name."; return; }
            if (string.IsNullOrWhiteSpace(NewUsername))  { FormErrorMessage = "Please enter a username."; return; }
            if (NewUsername.Trim().Length < 3)            { FormErrorMessage = "Username must be at least 3 characters."; return; }
            if (string.IsNullOrWhiteSpace(NewPassword))  { FormErrorMessage = "Please enter a password."; return; }
            if (NewPassword.Length < 6)                   { FormErrorMessage = "Password must be at least 6 characters."; return; }

            IsSaving = true;
            FormErrorMessage = string.Empty;

            try
            {
                var (success, message, _) = await ApiService.Instance.CreateUserAsync(
                    NewUsername, NewPassword, NewFullName, NewEmail, NewRole);

                if (success)
                {
                    IsAddUserModalOpen = false;
                    ShowBanner($"✓ Account '{NewUsername}' created as {(NewRole == "admin" ? "Administrator" : "Agent (User)")}.", isError: false);
                    await LoadUsersAsync();
                }
                else
                {
                    FormErrorMessage = message;
                }
            }
            catch (Exception ex)
            {
                FormErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ── Edit User ──────────────────────────────────────────────────

        private void ExecuteOpenEditUserModal(UserModel? user)
        {
            if (user == null) return;
            _editingUser = user;

            EditFullName       = user.FullName;
            EditUsername       = user.Username;
            EditEmail          = user.Email ?? string.Empty;
            EditRole           = user.Role;
            EditStatusDisplay  = user.IsActive == 1 ? "Active" : "Inactive";
            EditPassword       = string.Empty;
            EditFormErrorMessage = string.Empty;

            OnPropertyChanged(nameof(EditModalSubtitle));
            IsEditUserModalOpen = true;
        }

        private void ExecuteCloseEditUserModal()
        {
            IsEditUserModalOpen = false;
            EditFormErrorMessage = string.Empty;
            _editingUser = null;
        }

        private async Task ExecuteUpdateUserAsync()
        {
            if (_editingUser == null) return;

            if (string.IsNullOrWhiteSpace(EditFullName))  { EditFormErrorMessage = "Full name cannot be empty."; return; }
            if (string.IsNullOrWhiteSpace(EditUsername))  { EditFormErrorMessage = "Username cannot be empty."; return; }
            if (EditUsername.Trim().Length < 3)            { EditFormErrorMessage = "Username must be at least 3 characters."; return; }
            if (!string.IsNullOrEmpty(EditPassword) && EditPassword.Length < 6)
            {
                EditFormErrorMessage = "New password must be at least 6 characters.";
                return;
            }

            IsUpdating = true;
            EditFormErrorMessage = string.Empty;

            try
            {
                int isActive = EditStatusDisplay == "Active" ? 1 : 0;

                var (success, message, updatedUser) = await ApiService.Instance.UpdateUserAsync(
                    _editingUser.Id,
                    EditUsername,
                    EditFullName,
                    EditEmail,
                    EditRole,
                    isActive,
                    string.IsNullOrWhiteSpace(EditPassword) ? null : EditPassword);

                if (success && updatedUser != null)
                {
                    IsEditUserModalOpen = false;
                    ShowBanner($"✓ Account '{updatedUser.Username}' updated successfully.", isError: false);
                    await LoadUsersAsync();
                }
                else
                {
                    EditFormErrorMessage = message;
                }
            }
            catch (Exception ex)
            {
                EditFormErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsUpdating = false;
            }
        }

        // ── Delete User ────────────────────────────────────────────────

        private async Task ExecuteDeleteUserAsync(UserModel? user)
        {
            if (user == null) return;

            var confirm = MessageBox.Show(
                $"Are you sure you want to permanently delete the account for:\n\n👤  {user.FullName}  (@{user.Username})\n\nThis action cannot be undone.",
                "Confirm Delete User",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var (success, message) = await ApiService.Instance.DeleteUserAsync(user.Id);

                if (success)
                {
                    ShowBanner($"✓ Account '{user.Username}' deleted successfully.", isError: false);
                    await LoadUsersAsync();
                }
                else
                {
                    ShowBanner($"⚠ Could not delete account: {message}", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowBanner($"Error: {ex.Message}", isError: true);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────

        private void ShowBanner(string message, bool isError)
        {
            StatusBannerMessage = message;
            IsBannerError = isError;
            IsStatusBannerVisible = true;
        }
    }
}
