using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.Services;

namespace LeadManagement.Desktop.ViewModels
{
    public class UserDashboardViewModel : BaseViewModel
    {
        private string _agentName = "Agent";
        private string _currentUserView = "UserDashboard";
        private bool _isSidebarCollapsed = false;

        // Metric cards for the agent
        private string _pendingCallsCount = "19";
        private string _qualifiedLeadsCount = "14";
        private string _followUpsCount = "15";

        // Assigned Clients & Files State
        private ObservableCollection<ClientModel> _assignedClients = new();
        private ObservableCollection<ClientModel> _filteredAssignedClients = new();
        private bool _isLoadingClients = false;
        private string _searchClientText = string.Empty;

        // File Leads Preview Modal State
        private bool _isPreviewLeadsOpen = false;
        private bool _isLoadingPreviewLeads = false;
        private string _previewModalTitle = "Client Leads Preview";
        private string _previewModalSubtitle = string.Empty;
        private string _previewSearchText = string.Empty;
        private readonly List<LeadModel> _allFileLeads = new();
        private ObservableCollection<LeadModel> _filteredFilePreviewLeads = new();

        public UserDashboardViewModel()
        {
            NavigateUserCommand = new RelayCommand(param =>
            {
                if (param is string dest && !string.IsNullOrWhiteSpace(dest))
                {
                    CurrentUserView = dest;
                }
            });

            ToggleSidebarCommand = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);
            RefreshClientsCommand = new RelayCommand(async () => await LoadAssignedClientsAsync());
            ToggleExpandCommand = new RelayCommand<ClientModel>(c =>
            {
                if (c != null)
                {
                    c.IsExpanded = !c.IsExpanded;
                }
            });
            PreviewFileLeadsCommand = new RelayCommand<ClientFileModel>(async f => await ExecutePreviewFileLeadsAsync(f));
            ClosePreviewLeadsCommand = new RelayCommand(ExecuteClosePreviewLeads);

            // Mock initial assigned leads for preview calling queue
            MockAssignedLeads = new ObservableCollection<LeadModel>
            {
                new LeadModel
                {
                    Id = 1,
                    BusinessName = "Blue Sky Bistro & Cafe",
                    ContactName = "Sarah Jenkins",
                    Title = "General Manager",
                    CellNumber = "4025550192",
                    City = "Omaha",
                    State = "NE",
                    Status = "New Lead"
                },
                new LeadModel
                {
                    Id = 2,
                    BusinessName = "Midwest Logistics Solutions",
                    ContactName = "David Miller",
                    Title = "Operations Director",
                    CellNumber = "4025553819",
                    City = "Lincoln",
                    State = "NE",
                    Status = "Follow-up"
                },
                new LeadModel
                {
                    Id = 3,
                    BusinessName = "Prairie Valley Medical",
                    ContactName = "Dr. Robert Chen",
                    Title = "Managing Partner",
                    CellNumber = "3085559281",
                    City = "Grand Island",
                    State = "NE",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 4,
                    BusinessName = "Heartland Construction Group",
                    ContactName = "Marcus Turner",
                    Title = "VP Procurement",
                    CellNumber = "4025556271",
                    City = "Bellevue",
                    State = "NE",
                    Status = "Contacted"
                }
            };
        }

        // --- Core Properties ---

        public string AgentName
        {
            get => _agentName;
            set => SetProperty(ref _agentName, value);
        }

        public string CurrentUserView
        {
            get => _currentUserView;
            set
            {
                if (SetProperty(ref _currentUserView, value))
                {
                    OnPropertyChanged(nameof(IsDashboardActive));
                    OnPropertyChanged(nameof(IsAssignedClientsActive));
                    OnPropertyChanged(nameof(IsCallingQueueActive));
                    OnPropertyChanged(nameof(IsTasksActive));
                    OnPropertyChanged(nameof(IsProfileActive));
                }
            }
        }

        public bool IsDashboardActive => CurrentUserView == "UserDashboard";
        public bool IsAssignedClientsActive => CurrentUserView == "AssignedClients";
        public bool IsCallingQueueActive => CurrentUserView == "MyLeads";
        public bool IsTasksActive => CurrentUserView == "Tasks";
        public bool IsProfileActive => CurrentUserView == "Profile";

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

        // --- Assigned Clients & Files Properties ---

        public ObservableCollection<ClientModel> AssignedClients
        {
            get => _assignedClients;
            set
            {
                if (SetProperty(ref _assignedClients, value))
                {
                    UpdateComputedMetrics();
                }
            }
        }

        public ObservableCollection<ClientModel> FilteredAssignedClients
        {
            get => _filteredAssignedClients;
            set => SetProperty(ref _filteredAssignedClients, value);
        }

        public bool IsLoadingClients
        {
            get => _isLoadingClients;
            set
            {
                if (SetProperty(ref _isLoadingClients, value))
                {
                    OnPropertyChanged(nameof(HasNoAssignedClients));
                }
            }
        }

        public string SearchClientText
        {
            get => _searchClientText;
            set
            {
                if (SetProperty(ref _searchClientText, value))
                {
                    ApplyClientFilter();
                }
            }
        }

        public int TotalAssignedClients => AssignedClients?.Count ?? 0;
        public int TotalAssignedFiles => AssignedClients?.Sum(c => c.FileCount) ?? 0;
        public int TotalAssignedLeads => AssignedClients?.Sum(c => c.TotalLeads) ?? 0;

        public bool HasNoAssignedClients => !IsLoadingClients && TotalAssignedClients == 0;

        public string AssignedClientsDisplay => TotalAssignedClients.ToString();
        public string TotalFilesDisplay => TotalAssignedFiles.ToString();
        public string TotalLeadsDisplay => TotalAssignedLeads > 0 ? TotalAssignedLeads.ToString("N0") : "0";

        public string ClientsSummaryText
        {
            get
            {
                if (IsLoadingClients) return "Loading assigned clients...";
                if (TotalAssignedClients == 0) return "No Clients Assigned";
                return $"{TotalAssignedClients} {(TotalAssignedClients == 1 ? "Client" : "Clients")} • {TotalAssignedFiles} Files • {TotalAssignedLeads:N0} Total Leads";
            }
        }

        // --- Metrics Cards ---

        public string PendingCallsCount
        {
            get => _pendingCallsCount;
            set => SetProperty(ref _pendingCallsCount, value);
        }

        public string QualifiedLeadsCount
        {
            get => _qualifiedLeadsCount;
            set => SetProperty(ref _qualifiedLeadsCount, value);
        }

        public string FollowUpsCount
        {
            get => _followUpsCount;
            set => SetProperty(ref _followUpsCount, value);
        }

        public ObservableCollection<LeadModel> MockAssignedLeads { get; }

        // --- File Leads Preview Properties ---

        public bool IsPreviewLeadsOpen
        {
            get => _isPreviewLeadsOpen;
            set => SetProperty(ref _isPreviewLeadsOpen, value);
        }

        public bool IsLoadingPreviewLeads
        {
            get => _isLoadingPreviewLeads;
            set => SetProperty(ref _isLoadingPreviewLeads, value);
        }

        public string PreviewModalTitle
        {
            get => _previewModalTitle;
            set => SetProperty(ref _previewModalTitle, value);
        }

        public string PreviewModalSubtitle
        {
            get => _previewModalSubtitle;
            set => SetProperty(ref _previewModalSubtitle, value);
        }

        public string PreviewSearchText
        {
            get => _previewSearchText;
            set
            {
                if (SetProperty(ref _previewSearchText, value))
                {
                    ApplyFileLeadsFilter();
                }
            }
        }

        public ObservableCollection<LeadModel> FilteredFilePreviewLeads
        {
            get => _filteredFilePreviewLeads;
            set => SetProperty(ref _filteredFilePreviewLeads, value);
        }

        public string PreviewCountText
        {
            get
            {
                if (IsLoadingPreviewLeads) return "Loading leads...";
                int total = _allFileLeads.Count;
                if (total == 0) return "0 Records";
                if (FilteredFilePreviewLeads.Count == total) return $"{total:N0} Records";
                return $"Showing {FilteredFilePreviewLeads.Count:N0} of {total:N0} Records";
            }
        }

        // --- Commands ---

        public ICommand NavigateUserCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand RefreshClientsCommand { get; }
        public ICommand ToggleExpandCommand { get; }
        public ICommand PreviewFileLeadsCommand { get; }
        public ICommand ClosePreviewLeadsCommand { get; }

        // --- Public Loading Method ---

        /// <summary>
        /// Fetches clients assigned to the currently logged-in agent along with their uploaded files.
        /// </summary>
        public async Task LoadAssignedClientsAsync()
        {
            if (IsLoadingClients) return;

            IsLoadingClients = true;
            try
            {
                var currentUser = ApiService.Instance.CurrentUser;
                int? agentId = currentUser?.Id;

                var (success, _, clients) = await ApiService.Instance.GetClientsAsync(agentId);

                if (success && clients != null)
                {
                    // By default, expand clients if there's only 1 or 2, for quick viewing
                    foreach (var c in clients)
                    {
                        if (clients.Count <= 3)
                        {
                            c.IsExpanded = true;
                        }
                    }

                    AssignedClients = new ObservableCollection<ClientModel>(clients);
                    ApplyClientFilter();
                }
                else
                {
                    AssignedClients = new ObservableCollection<ClientModel>();
                    FilteredAssignedClients = new ObservableCollection<ClientModel>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading assigned clients: {ex.Message}");
            }
            finally
            {
                IsLoadingClients = false;
                UpdateComputedMetrics();
            }
        }

        private void ApplyClientFilter()
        {
            IEnumerable<ClientModel> query = AssignedClients;

            if (!string.IsNullOrWhiteSpace(SearchClientText))
            {
                var term = SearchClientText.Trim().ToLowerInvariant();
                query = query.Where(c =>
                    c.FullName.ToLowerInvariant().Contains(term) ||
                    c.FirstName.ToLowerInvariant().Contains(term) ||
                    c.LastName.ToLowerInvariant().Contains(term) ||
                    (c.Files != null && c.Files.Any(f => f.FileName.ToLowerInvariant().Contains(term))));
            }

            FilteredAssignedClients = new ObservableCollection<ClientModel>(query);
        }

        private void UpdateComputedMetrics()
        {
            OnPropertyChanged(nameof(TotalAssignedClients));
            OnPropertyChanged(nameof(TotalAssignedFiles));
            OnPropertyChanged(nameof(TotalAssignedLeads));
            OnPropertyChanged(nameof(AssignedClientsDisplay));
            OnPropertyChanged(nameof(TotalFilesDisplay));
            OnPropertyChanged(nameof(TotalLeadsDisplay));
            OnPropertyChanged(nameof(ClientsSummaryText));
            OnPropertyChanged(nameof(HasNoAssignedClients));
        }

        // --- File Leads Preview ---

        private async Task ExecutePreviewFileLeadsAsync(ClientFileModel? file)
        {
            if (file == null) return;

            PreviewModalTitle = $"📄 {file.FileName}";
            PreviewModalSubtitle = $"{file.ClientName} • {file.FormattedLeadCount} • Uploaded {file.FormattedDate}";
            IsPreviewLeadsOpen = true;
            IsLoadingPreviewLeads = true;
            PreviewSearchText = string.Empty;
            _allFileLeads.Clear();
            FilteredFilePreviewLeads = new ObservableCollection<LeadModel>();

            try
            {
                if (file.BatchId.HasValue && file.BatchId.Value > 0)
                {
                    var (success, _, leads) = await ApiService.Instance.GetBatchLeadsAsync(file.BatchId.Value);
                    if (success && leads != null)
                    {
                        _allFileLeads.AddRange(leads);
                    }
                }

                ApplyFileLeadsFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error previewing file leads: {ex.Message}");
            }
            finally
            {
                IsLoadingPreviewLeads = false;
                OnPropertyChanged(nameof(PreviewCountText));
            }
        }

        private void ApplyFileLeadsFilter()
        {
            IEnumerable<LeadModel> query = _allFileLeads;

            if (!string.IsNullOrWhiteSpace(PreviewSearchText))
            {
                var term = PreviewSearchText.Trim().ToLowerInvariant();
                query = query.Where(l =>
                    (l.BusinessName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.ContactName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.CellNumber?.Contains(term) ?? false) ||
                    (l.City?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Status?.ToLowerInvariant().Contains(term) ?? false));
            }

            FilteredFilePreviewLeads = new ObservableCollection<LeadModel>(query);
            OnPropertyChanged(nameof(PreviewCountText));
        }

        private void ExecuteClosePreviewLeads()
        {
            IsPreviewLeadsOpen = false;
            _allFileLeads.Clear();
            FilteredFilePreviewLeads = new ObservableCollection<LeadModel>();
            PreviewSearchText = string.Empty;
        }

        public void Reset()
        {
            AssignedClients.Clear();
            FilteredAssignedClients.Clear();
            _allFileLeads.Clear();
            FilteredFilePreviewLeads.Clear();
            SearchClientText = string.Empty;
            IsPreviewLeadsOpen = false;
            CurrentUserView = "UserDashboard";
            UpdateComputedMetrics();
        }
    }
}
