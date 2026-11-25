using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.Services;
using Microsoft.Win32;

namespace LeadManagement.Desktop.ViewModels
{
    public class LeadMasterlistViewModel : BaseViewModel
    {
        private ObservableCollection<LeadModel> _filteredLeads = new();
        private LeadModel? _selectedLead;
        private string _searchText = string.Empty;
        private bool _isLoading = false;

        private int _totalCount = 0;
        private int _filteredCount = 0;
        private int _pageSize = 1000;
        private int _currentPage = 1;
        private string _sortBy = "id";
        private string _sortDirection = "ASC";

        private bool _isPopulatingFilters = false;
        private CancellationTokenSource? _queryCts;

        public LeadMasterlistViewModel()
        {
            ClearFiltersCommand = new RelayCommand(ExecuteClearFilters);
            RefreshCommand = new RelayCommand(async () => await ReloadAllAsync());
            ExportFilteredCommand = new RelayCommand(ExecuteExportFiltered);
            ExportAllCommand = new RelayCommand(ExecuteExportAll);
            ExportCommand = new RelayCommand(ExecuteExportFiltered);
            CancelExportCommand = new RelayCommand(() =>
            {
                _exportCts?.Cancel();
                ExportModalStatus = "Cancelling export...";
            });

            FirstPageCommand = new RelayCommand(() => GoToPage(1), () => HasPreviousPage);
            PreviousPageCommand = new RelayCommand(() => GoToPage(CurrentPage - 1), () => HasPreviousPage);
            NextPageCommand = new RelayCommand(() => GoToPage(CurrentPage + 1), () => HasNextPage);
            LastPageCommand = new RelayCommand(() => GoToPage(TotalPages), () => HasNextPage);

            CallLeadCommand = new RelayCommand<LeadModel>(ExecuteCallLead);
            OpenLeadProfileCommand = new RelayCommand<LeadModel>(async l => await ExecuteOpenLeadProfileAsync(l));
            CloseLeadProfileCommand = new RelayCommand(() => IsLeadProfileModalOpen = false);
            UpdateLeadProfileCommand = new RelayCommand(async () => await ExecuteUpdateLeadProfileAsync(), () => HasProfileChanges && !IsSavingProfile);
            OpenAssignClientModalCommand = new RelayCommand(ExecuteOpenAssignClientModal);
            CloseAssignClientModalCommand = new RelayCommand(() => IsAssignClientModalOpen = false);
            ConfirmAssignClientCommand = new RelayCommand(async () => await ExecuteConfirmAssignClientAsync(), () => !IsAssigning && SelectedTargetClient != null);
            DismissBannerCommand = new RelayCommand(() => IsBannerVisible = false);

            // Pre-warm Zoom in background for instant click-to-dial
            _ = Task.Run(() => ZoomPhoneService.EnsureZoomRunning());

            // High performance parallel startup: load distinct filter options and fetch initial 1,000 page
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await Task.WhenAll(PopulateFilterListsAsync(), FetchLeadsPageAsync());
        }

        public async Task ReloadAllAsync()
        {
            await Task.WhenAll(PopulateFilterListsAsync(), FetchLeadsPageAsync());
        }

        /// <summary>
        /// Backward-compatible alias for navigation reload.
        /// </summary>
        public Task LoadLeadsAsync() => ReloadAllAsync();

        public ObservableCollection<LeadModel> FilteredLeads
        {
            get => _filteredLeads;
            set => SetProperty(ref _filteredLeads, value);
        }

        public LeadModel? SelectedLead
        {
            get => _selectedLead;
            set
            {
                if (SetProperty(ref _selectedLead, value))
                {
                    OnPropertyChanged(nameof(HasSelectedLead));
                }
            }
        }

        public bool HasSelectedLead => SelectedLead != null;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(ShowingCountText));
                    OnPropertyChanged(nameof(PaginationSummary));
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    CurrentPage = 1;
                    _ = TriggerQueryAsync(debounced: true, debounceMs: 300);
                }
            }
        }

        // ─── Pagination Properties ───────────────────────────────────────────────

        public List<int> PageSizeOptions { get; } = new() { 100, 200, 500, 1000 };

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value > 0 && SetProperty(ref _pageSize, value))
                {
                    CurrentPage = 1;
                    _ = FetchLeadsPageAsync();
                }
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public int TotalPages => FilteredCount == 0 ? 1 : (int)Math.Ceiling((double)FilteredCount / PageSize);

        public bool HasPreviousPage => CurrentPage > 1;

        public bool HasNextPage => CurrentPage < TotalPages;

        public string PageNumberText => $"Page {CurrentPage:N0} of {TotalPages:N0}";

        public string PaginationSummary
        {
            get
            {
                if (IsLoading) return "Loading records from database...";
                if (FilteredCount == 0) return "No records found matching filters";
                int start = (CurrentPage - 1) * PageSize + 1;
                int end = Math.Min(CurrentPage * PageSize, FilteredCount);
                if (HasActiveFilters)
                {
                    return $"Showing {start:N0}–{end:N0} of {FilteredCount:N0} records (filtered from {TotalCount:N0} total)";
                }
                return $"Showing {start:N0}–{end:N0} of {TotalCount:N0} records";
            }
        }

        public ICommand FirstPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }

        private void GoToPage(int page)
        {
            int maxPages = TotalPages;
            if (page < 1) page = 1;
            if (page > maxPages) page = maxPages;
            if (CurrentPage == page) return;

            CurrentPage = page;
            _ = FetchLeadsPageAsync();
        }

        // ─── Multi-Select Filter Collections (ObservableCollection<FilterItem>) ───

        public ObservableCollection<FilterItem> ClientItems { get; } = new();
        public ObservableCollection<FilterItem> AgentFilterItems { get; } = new();
        public ObservableCollection<ClientModel> AvailableClients { get; } = new();
        public ObservableCollection<string> AllAgentOptions { get; } = new() { "" };
        public ObservableCollection<FilterItem> CityItems { get; } = new();
        public ObservableCollection<FilterItem> CountyItems { get; } = new();
        public ObservableCollection<FilterItem> StateItems { get; } = new();
        public ObservableCollection<FilterItem> SicCodeItems { get; } = new();
        public ObservableCollection<FilterItem> SicDescItems { get; } = new();
        public ObservableCollection<FilterItem> IndustryItems => SicDescItems; // Backward compatibility alias
        public ObservableCollection<FilterItem> AnnualSalesItems { get; } = new();
        public ObservableCollection<FilterItem> NaicsCodeItems { get; } = new();
        public ObservableCollection<FilterItem> NaicsDescItems { get; } = new();
        public ObservableCollection<FilterItem> StatusItems { get; } = new();

        // ─── Assign to Client Modal & Notification State ─────────────────────────
        private bool _isAssignClientModalOpen = false;
        private ClientModel? _selectedTargetClient;
        private string _assignModalSummary = string.Empty;
        private bool _isAssigning = false;
        private string _assignErrorMessage = string.Empty;
        private string _statusBannerMessage = string.Empty;
        private bool _isBannerVisible = false;
        private bool _isBannerError = false;

        public bool IsAssignClientModalOpen
        {
            get => _isAssignClientModalOpen;
            set => SetProperty(ref _isAssignClientModalOpen, value);
        }

        public ClientModel? SelectedTargetClient
        {
            get => _selectedTargetClient;
            set
            {
                if (SetProperty(ref _selectedTargetClient, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string AssignModalSummary
        {
            get => _assignModalSummary;
            set => SetProperty(ref _assignModalSummary, value);
        }

        public bool IsAssigning
        {
            get => _isAssigning;
            set
            {
                if (SetProperty(ref _isAssigning, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string AssignErrorMessage
        {
            get => _assignErrorMessage;
            set
            {
                if (SetProperty(ref _assignErrorMessage, value))
                {
                    OnPropertyChanged(nameof(HasAssignError));
                }
            }
        }

        public bool HasAssignError => !string.IsNullOrEmpty(AssignErrorMessage);

        public string StatusBannerMessage
        {
            get => _statusBannerMessage;
            set => SetProperty(ref _statusBannerMessage, value);
        }

        public bool IsBannerVisible
        {
            get => _isBannerVisible;
            set => SetProperty(ref _isBannerVisible, value);
        }

        public bool IsBannerError
        {
            get => _isBannerError;
            set => SetProperty(ref _isBannerError, value);
        }

        public ICommand CallLeadCommand { get; }
        public ICommand OpenLeadProfileCommand { get; }
        public ICommand CloseLeadProfileCommand { get; }
        public ICommand UpdateLeadProfileCommand { get; }
        public ICommand OpenAssignClientModalCommand { get; }
        public ICommand CloseAssignClientModalCommand { get; }
        public ICommand ConfirmAssignClientCommand { get; }
        public ICommand DismissBannerCommand { get; }

        // ─── Computed Properties ─────────────────────────────────────────────────

        public int TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        public int FilteredCount
        {
            get => _filteredCount;
            private set => SetProperty(ref _filteredCount, value);
        }

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchText) ||
            ClientItems.Any(i => i.IsSelected) ||
            AgentFilterItems.Any(i => i.IsSelected) ||
            CityItems.Any(i => i.IsSelected) ||
            CountyItems.Any(i => i.IsSelected) ||
            StateItems.Any(i => i.IsSelected) ||
            SicCodeItems.Any(i => i.IsSelected) ||
            SicDescItems.Any(i => i.IsSelected) ||
            AnnualSalesItems.Any(i => i.IsSelected) ||
            NaicsCodeItems.Any(i => i.IsSelected) ||
            NaicsDescItems.Any(i => i.IsSelected) ||
            StatusItems.Any(i => i.IsSelected);

        public string ShowingCountText
        {
            get
            {
                if (IsLoading) return "Loading...";
                if (TotalCount == 0) return "0 Leads";
                if (FilteredCount == TotalCount) return $"{TotalCount:N0} Leads";
                return $"Showing {FilteredCount:N0} of {TotalCount:N0} Leads";
            }
        }

        public string AssignToClientButtonText =>
            HasActiveFilters
                ? $"👥 Assign to Client ({FilteredCount:N0})"
                : "👥 Assign to Client";

        public string ExportFilteredButtonText =>
            HasActiveFilters
                ? $"⤓ Export Filtered ({FilteredCount:N0})"
                : "⤓ Export Filtered";

        public string ExportAllButtonText => $"⤓ Export All ({TotalCount:N0})";

        // ─── Export Loading Modal State Properties ───
        private bool _isExportModalOpen = false;
        private string _exportModalTitle = "Exporting Leads to Excel...";
        private string _exportModalSubtitle = string.Empty;
        private string _exportModalStatus = "Preparing data...";
        private double _exportProgressPercentage = 0;
        private bool _isExportIndeterminate = true;
        private CancellationTokenSource? _exportCts;

        public bool IsExportModalOpen
        {
            get => _isExportModalOpen;
            set => SetProperty(ref _isExportModalOpen, value);
        }

        public string ExportModalTitle
        {
            get => _exportModalTitle;
            set => SetProperty(ref _exportModalTitle, value);
        }

        public string ExportModalSubtitle
        {
            get => _exportModalSubtitle;
            set => SetProperty(ref _exportModalSubtitle, value);
        }

        public string ExportModalStatus
        {
            get => _exportModalStatus;
            set => SetProperty(ref _exportModalStatus, value);
        }

        public double ExportProgressPercentage
        {
            get => _exportProgressPercentage;
            set => SetProperty(ref _exportProgressPercentage, value);
        }

        public bool IsExportIndeterminate
        {
            get => _isExportIndeterminate;
            set => SetProperty(ref _isExportIndeterminate, value);
        }

        public ICommand ClearFiltersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportFilteredCommand { get; }
        public ICommand ExportAllCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand CancelExportCommand { get; }

        private static List<string> GetSelected(ObservableCollection<FilterItem> items) =>
            items.Where(i => i.IsSelected).Select(i => i.Name).ToList();

        private void SubscribeToItems(ObservableCollection<FilterItem> items)
        {
            foreach (var item in items)
            {
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(FilterItem.IsSelected) && !_isPopulatingFilters)
                    {
                        CurrentPage = 1;
                        _ = TriggerQueryAsync(debounced: true, debounceMs: 150);
                    }
                };
            }
        }

        private async Task TriggerQueryAsync(bool debounced = false, int debounceMs = 250)
        {
            if (_isPopulatingFilters) return;

            _queryCts?.Cancel();
            _queryCts?.Dispose();
            _queryCts = new CancellationTokenSource();
            var token = _queryCts.Token;

            if (debounced)
            {
                try
                {
                    await Task.Delay(debounceMs, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }

            if (token.IsCancellationRequested) return;

            await FetchLeadsPageAsync(token);
        }

        /// <summary>
        /// Fetches the currently active page directly from MySQL via POST /leads/query.
        /// Loads only the requested page slice (e.g. 1,000 records) in milliseconds.
        /// </summary>
        public async Task FetchLeadsPageAsync(CancellationToken token = default)
        {
            IsLoading = true;
            try
            {
                var req = BuildQueryRequest(limit: PageSize, offset: (CurrentPage - 1) * PageSize);
                var (success, message, data) = await ApiService.Instance.QueryLeadsAsync(req, token);

                if (token.IsCancellationRequested) return;

                if (success && data != null)
                {
                    TotalCount = data.Total;
                    FilteredCount = data.FilteredTotal;
                    FilteredLeads = new ObservableCollection<LeadModel>(data.Leads);

                    // Hook up real-time autosync and assign agent dropdown options for each lead
                    foreach (var lead in FilteredLeads)
                    {
                        lead.AvailableAgentOptions = AllAgentOptions;
                        lead.OnFieldChanged = (l, field) => SyncLeadField(l, field);
                    }

                    if (SelectedLead == null || !FilteredLeads.Contains(SelectedLead))
                    {
                        SelectedLead = FilteredLeads.FirstOrDefault();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(message) && !token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"[LMS Query Warning] {message}");
                }
            }
            catch (OperationCanceledException)
            {
                // Silently swallow query cancellations when newer inputs arrive
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LMS Query Error] {ex.Message}");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsLoading = false;
                    NotifyPaginationProperties();
                }
            }
        }

        private LeadQueryRequest BuildQueryRequest(int limit, int offset)
        {
            var clients = GetSelected(ClientItems);
            var agents = GetSelected(AgentFilterItems);
            var cities = GetSelected(CityItems);
            var counties = GetSelected(CountyItems);
            var states = GetSelected(StateItems);
            var statuses = GetSelected(StatusItems);
            var sicCodes = GetSelected(SicCodeItems);
            var sicDescs = GetSelected(SicDescItems);
            var annualSales = GetSelected(AnnualSalesItems);
            var naicsCodes = GetSelected(NaicsCodeItems);
            var naicsDescs = GetSelected(NaicsDescItems);

            return new LeadQueryRequest
            {
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                Limit = limit,
                Offset = offset,
                SortBy = _sortBy,
                SortDirection = _sortDirection,
                Clients = clients.Count > 0 ? clients : null,
                Agents = agents.Count > 0 ? agents : null,
                Cities = cities.Count > 0 ? cities : null,
                Counties = counties.Count > 0 ? counties : null,
                States = states.Count > 0 ? states : null,
                Statuses = statuses.Count > 0 ? statuses : null,
                SicCodes = sicCodes.Count > 0 ? sicCodes : null,
                SicDescriptions = sicDescs.Count > 0 ? sicDescs : null,
                AnnualSales = annualSales.Count > 0 ? annualSales : null,
                NaicsCodes = naicsCodes.Count > 0 ? naicsCodes : null,
                NaicsDescriptions = naicsDescs.Count > 0 ? naicsDescs : null
            };
        }

        private void NotifyPaginationProperties()
        {
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(PaginationSummary));
            OnPropertyChanged(nameof(PageNumberText));
            OnPropertyChanged(nameof(ShowingCountText));
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(ExportFilteredButtonText));
            OnPropertyChanged(nameof(ExportAllButtonText));
            OnPropertyChanged(nameof(AssignToClientButtonText));
        }

        private void ExecuteClearFilters()
        {
            _isPopulatingFilters = true;
            try
            {
                _searchText = string.Empty;
                OnPropertyChanged(nameof(SearchText));

                foreach (var item in ClientItems) item.IsSelected = false;
                foreach (var item in AgentFilterItems) item.IsSelected = false;
                foreach (var item in CityItems) item.IsSelected = false;
                foreach (var item in CountyItems) item.IsSelected = false;
                foreach (var item in StateItems) item.IsSelected = false;
                foreach (var item in SicCodeItems) item.IsSelected = false;
                foreach (var item in SicDescItems) item.IsSelected = false;
                foreach (var item in AnnualSalesItems) item.IsSelected = false;
                foreach (var item in NaicsCodeItems) item.IsSelected = false;
                foreach (var item in NaicsDescItems) item.IsSelected = false;
                foreach (var item in StatusItems) item.IsSelected = false;
            }
            finally
            {
                _isPopulatingFilters = false;
            }

            CurrentPage = 1;
            _ = TriggerQueryAsync(debounced: false);
        }

        private async Task PopulateFilterListsAsync()
        {
            try
            {
                var filters = await ApiService.Instance.GetLeadFiltersAsync();
                if (filters == null) return;

                _isPopulatingFilters = true;

                void LoadItems(ObservableCollection<FilterItem> collection, IEnumerable<string>? vals)
                {
                    collection.Clear();
                    if (vals != null)
                    {
                        foreach (var v in vals)
                        {
                            if (!string.IsNullOrWhiteSpace(v))
                                collection.Add(new FilterItem { Name = v });
                        }
                    }
                    SubscribeToItems(collection);
                }

                LoadItems(CityItems, filters.Cities);
                LoadItems(CountyItems, filters.Counties);
                LoadItems(StateItems, filters.States);
                LoadItems(SicCodeItems, filters.SicCodes);
                LoadItems(SicDescItems, filters.SicDescriptions);
                LoadItems(AnnualSalesItems, filters.AnnualSales);
                LoadItems(NaicsCodeItems, filters.NaicsCodes);
                LoadItems(NaicsDescItems, filters.NaicsDescriptions);

                var knownStatuses = new[] { "New Lead", "Qualified", "Follow-up", "Contacted" };
                var mergedStatuses = knownStatuses.Union(filters.Statuses ?? new List<string>(), StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
                LoadItems(StatusItems, mergedStatuses);

                // 1. Populate clients dropdown & client filter list
                var (clientsOk, _, clientList) = await ApiService.Instance.GetClientsAsync();
                if (clientsOk && clientList != null)
                {
                    AvailableClients.Clear();
                    var clientNames = new List<string> { "Unassigned" };
                    foreach (var c in clientList)
                    {
                        AvailableClients.Add(c);
                        if (!string.IsNullOrWhiteSpace(c.FullName))
                        {
                            clientNames.Add(c.FullName.Trim());
                        }
                    }
                    LoadItems(ClientItems, clientNames);
                    if (SelectedTargetClient == null)
                    {
                        SelectedTargetClient = AvailableClients.FirstOrDefault();
                    }
                }

                // 2. Populate available agents for the AGENT column dropdown and Agents filter
                var (usersOk, _, userList) = await ApiService.Instance.GetUsersAsync();
                var agentFilterNames = new List<string> { "Unassigned" };
                if (usersOk && userList != null)
                {
                    AllAgentOptions.Clear();
                    AllAgentOptions.Add(""); // Empty for unassigned
                    foreach (var u in userList.Where(u => u.Role == "user" || u.Role == "admin"))
                    {
                        var name = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName.Trim() : u.Username.Trim();
                        if (!string.IsNullOrWhiteSpace(name) && !AllAgentOptions.Contains(name))
                        {
                            AllAgentOptions.Add(name);
                            if (u.Role == "user" && !agentFilterNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                            {
                                agentFilterNames.Add(name);
                            }
                        }
                    }
                }
                if (filters.Agents != null)
                {
                    foreach (var a in filters.Agents)
                    {
                        if (!string.IsNullOrWhiteSpace(a) && !agentFilterNames.Contains(a.Trim(), StringComparer.OrdinalIgnoreCase))
                        {
                            agentFilterNames.Add(a.Trim());
                        }
                    }
                }
                LoadItems(AgentFilterItems, agentFilterNames);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LMS Filter Load Error] {ex.Message}");
            }
            finally
            {
                _isPopulatingFilters = false;
            }
        }

        // ─── Real-Time Autosync on Edit ──────────────────────────────────────────

        private async void SyncLeadField(LeadModel lead, string field)
        {
            if (lead == null || lead.Id <= 0) return;
            try
            {
                object payload = field switch
                {
                    "call_date" => new { call_date = lead.CallDate },
                    "call_disposition" => new { call_disposition = lead.CallDisposition },
                    "assigned_agent" => new { assigned_agent = lead.AssignedAgent },
                    "notes" => new { notes = lead.Notes },
                    "call_count" => new { call_count = lead.CallCount },
                    "cell_number" => new { cell_number = lead.CellNumber },
                    "business_name" => new { business_name = lead.BusinessName },
                    "contact_name" => new { contact_name = lead.ContactName },
                    "email" => new { email = lead.Email },
                    "status" => new { status = lead.Status },
                    _ => new
                    {
                        call_date = lead.CallDate,
                        call_disposition = lead.CallDisposition,
                        assigned_agent = lead.AssignedAgent,
                        notes = lead.Notes,
                        call_count = lead.CallCount
                    }
                };

                await ApiService.Instance.UpdateLeadAsync(lead.Id, payload);

                if (field == "call_disposition" || field == "notes")
                {
                    var currentUser = ApiService.Instance.CurrentUser;
                    if (currentUser != null && !string.Equals(currentUser.Role, "admin", StringComparison.OrdinalIgnoreCase))
                    {
                        await ApiService.Instance.UpdateLeadCallHistoryAsync(
                            lead.Id,
                            lead.LastCallHistoryId,
                            currentUser.Id,
                            lead.CallDisposition,
                            lead.Notes);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Masterlist Field Sync Error] {ex.Message}");
            }
        }

        private void ExecuteCallLead(LeadModel? lead)
        {
            if (lead == null) return;
            lead.CallCount++;
            lead.CallDate = DateTime.Now.ToString("MM/dd/yy", System.Globalization.CultureInfo.InvariantCulture);
            SyncLeadField(lead, "call_count");
            SyncLeadField(lead, "call_date");
            ZoomPhoneService.Dial(lead.CellNumber);

            var currentUser = ApiService.Instance.CurrentUser;
            if (currentUser != null && !string.Equals(currentUser.Role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                var callerName = !string.IsNullOrWhiteSpace(currentUser.FullName)
                    ? currentUser.FullName.Trim()
                    : currentUser.Username.Trim();

                _ = Task.Run(async () =>
                {
                    var (ok, _, entry) = await ApiService.Instance.LogLeadCallHistoryAsync(
                        lead.Id,
                        currentUser.Id,
                        callerName,
                        lead.CallDisposition ?? string.Empty,
                        lead.Notes ?? string.Empty,
                        lead.CallCount,
                        lead.PrimaryClientName);

                    if (ok && entry != null)
                    {
                        lead.LastCallHistoryId = entry.Id;
                    }
                });
            }
        }

        // ─── Lead Profile Modal & Call History ───────────────────────────────────

        private bool _isLeadProfileModalOpen;
        private LeadModel? _profileLead;
        private bool _isSavingProfile;
        private bool _isLoadingProfileHistory;
        private ObservableCollection<LeadAssignedClientInfo> _profileAssignedClients = new();
        private ObservableCollection<LeadCallHistoryEntry> _profileCallHistory = new();

        private string _origCallDate = string.Empty;
        private string _origCallDisposition = string.Empty;
        private string _origAssignedAgent = string.Empty;
        private string _origNotes = string.Empty;
        private string _origCallCount = "1";
        private string _origBusinessName = string.Empty;
        private string _origContactName = string.Empty;
        private string _origTitle = string.Empty;
        private string _origCellNumber = string.Empty;
        private string _origEmail = string.Empty;
        private string _origFax = string.Empty;
        private string _origWebsite = string.Empty;
        private string _origAddress = string.Empty;
        private string _origCity = string.Empty;
        private string _origCounty = string.Empty;
        private string _origState = string.Empty;
        private string _origZipCode = string.Empty;
        private string _origAnnualSales = string.Empty;
        private string _origNumberOfEmployees = string.Empty;
        private string _origSicCode = string.Empty;
        private string _origSicDescription = string.Empty;
        private string _origNaicsCode = string.Empty;
        private string _origNaicsDescription = string.Empty;

        private string _profileCallDate = string.Empty;
        private string _profileCallDisposition = string.Empty;
        private string _profileAssignedAgent = string.Empty;
        private string _profileNotes = string.Empty;
        private string _profileCallCount = "1";
        private string _profileBusinessName = string.Empty;
        private string _profileContactName = string.Empty;
        private string _profileTitle = string.Empty;
        private string _profileCellNumber = string.Empty;
        private string _profileEmail = string.Empty;
        private string _profileFax = string.Empty;
        private string _profileWebsite = string.Empty;
        private string _profileAddress = string.Empty;
        private string _profileCity = string.Empty;
        private string _profileCounty = string.Empty;
        private string _profileState = string.Empty;
        private string _profileZipCode = string.Empty;
        private string _profileAnnualSales = string.Empty;
        private string _profileNumberOfEmployees = string.Empty;
        private string _profileSicCode = string.Empty;
        private string _profileSicDescription = string.Empty;
        private string _profileNaicsCode = string.Empty;
        private string _profileNaicsDescription = string.Empty;

        public bool IsLeadProfileModalOpen { get => _isLeadProfileModalOpen; set => SetProperty(ref _isLeadProfileModalOpen, value); }
        public LeadModel? ProfileLead { get => _profileLead; set => SetProperty(ref _profileLead, value); }
        public bool IsSavingProfile
        {
            get => _isSavingProfile;
            set { if (SetProperty(ref _isSavingProfile, value)) CommandManager.InvalidateRequerySuggested(); }
        }
        public bool IsLoadingProfileHistory { get => _isLoadingProfileHistory; set => SetProperty(ref _isLoadingProfileHistory, value); }

        public ObservableCollection<LeadAssignedClientInfo> ProfileAssignedClients
        {
            get => _profileAssignedClients;
            set { if (SetProperty(ref _profileAssignedClients, value)) OnPropertyChanged(nameof(HasProfileAssignedClients)); }
        }
        public bool HasProfileAssignedClients => ProfileAssignedClients.Count > 0;

        public ObservableCollection<LeadCallHistoryEntry> ProfileCallHistory
        {
            get => _profileCallHistory;
            set { if (SetProperty(ref _profileCallHistory, value)) OnPropertyChanged(nameof(HasProfileCallHistory)); }
        }
        public bool HasProfileCallHistory => ProfileCallHistory.Count > 0;

        private void SetProfileField(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? propName = null)
        {
            if (SetProperty(ref field, value ?? string.Empty, propName))
            {
                OnPropertyChanged(nameof(HasProfileChanges));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ProfileCallDate
        {
            get => _profileCallDate;
            set
            {
                if (SetProperty(ref _profileCallDate, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(ProfileSelectedDate));
                    OnPropertyChanged(nameof(HasProfileChanges));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public DateTime? ProfileSelectedDate
        {
            get => LeadModel.ParseCallDate(ProfileCallDate);
            set
            {
                var formatted = value?.ToString("MM/dd/yy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                if (ProfileCallDate != formatted) ProfileCallDate = formatted;
            }
        }

        public string ProfileCallDisposition { get => _profileCallDisposition; set => SetProfileField(ref _profileCallDisposition, value); }
        public string ProfileAssignedAgent { get => _profileAssignedAgent; set => SetProfileField(ref _profileAssignedAgent, value); }
        public string ProfileNotes { get => _profileNotes; set => SetProfileField(ref _profileNotes, value); }
        public string ProfileCallCount { get => _profileCallCount; set => SetProfileField(ref _profileCallCount, value); }
        public string ProfileBusinessName { get => _profileBusinessName; set => SetProfileField(ref _profileBusinessName, value); }
        public string ProfileContactName { get => _profileContactName; set => SetProfileField(ref _profileContactName, value); }
        public string ProfileTitle { get => _profileTitle; set => SetProfileField(ref _profileTitle, value); }
        public string ProfileCellNumber { get => _profileCellNumber; set => SetProfileField(ref _profileCellNumber, value); }
        public string ProfileEmail { get => _profileEmail; set => SetProfileField(ref _profileEmail, value); }
        public string ProfileFax { get => _profileFax; set => SetProfileField(ref _profileFax, value); }
        public string ProfileWebsite { get => _profileWebsite; set => SetProfileField(ref _profileWebsite, value); }
        public string ProfileAddress { get => _profileAddress; set => SetProfileField(ref _profileAddress, value); }
        public string ProfileCity { get => _profileCity; set => SetProfileField(ref _profileCity, value); }
        public string ProfileCounty { get => _profileCounty; set => SetProfileField(ref _profileCounty, value); }
        public string ProfileState { get => _profileState; set => SetProfileField(ref _profileState, value); }
        public string ProfileZipCode { get => _profileZipCode; set => SetProfileField(ref _profileZipCode, value); }
        public string ProfileAnnualSales { get => _profileAnnualSales; set => SetProfileField(ref _profileAnnualSales, value); }
        public string ProfileNumberOfEmployees { get => _profileNumberOfEmployees; set => SetProfileField(ref _profileNumberOfEmployees, value); }
        public string ProfileSicCode { get => _profileSicCode; set => SetProfileField(ref _profileSicCode, value); }
        public string ProfileSicDescription { get => _profileSicDescription; set => SetProfileField(ref _profileSicDescription, value); }
        public string ProfileNaicsCode { get => _profileNaicsCode; set => SetProfileField(ref _profileNaicsCode, value); }
        public string ProfileNaicsDescription { get => _profileNaicsDescription; set => SetProfileField(ref _profileNaicsDescription, value); }

        public bool HasProfileChanges =>
            _profileCallDate != _origCallDate ||
            _profileCallDisposition != _origCallDisposition ||
            _profileAssignedAgent != _origAssignedAgent ||
            _profileNotes != _origNotes ||
            _profileCallCount != _origCallCount ||
            _profileBusinessName != _origBusinessName ||
            _profileContactName != _origContactName ||
            _profileTitle != _origTitle ||
            _profileCellNumber != _origCellNumber ||
            _profileEmail != _origEmail ||
            _profileFax != _origFax ||
            _profileWebsite != _origWebsite ||
            _profileAddress != _origAddress ||
            _profileCity != _origCity ||
            _profileCounty != _origCounty ||
            _profileState != _origState ||
            _profileZipCode != _origZipCode ||
            _profileAnnualSales != _origAnnualSales ||
            _profileNumberOfEmployees != _origNumberOfEmployees ||
            _profileSicCode != _origSicCode ||
            _profileSicDescription != _origSicDescription ||
            _profileNaicsCode != _origNaicsCode ||
            _profileNaicsDescription != _origNaicsDescription;

        private async Task ExecuteOpenLeadProfileAsync(LeadModel? lead)
        {
            if (lead == null) return;
            ProfileLead = lead;

            _origCallDate = lead.CallDate ?? string.Empty;
            _origCallDisposition = lead.CallDisposition ?? string.Empty;
            _origAssignedAgent = lead.AssignedAgent ?? string.Empty;
            _origNotes = lead.Notes ?? string.Empty;
            _origCallCount = lead.CallCount.ToString();
            _origBusinessName = lead.BusinessName ?? string.Empty;
            _origContactName = lead.ContactName ?? string.Empty;
            _origTitle = lead.Title ?? string.Empty;
            _origCellNumber = lead.CellNumber ?? string.Empty;
            _origEmail = lead.Email ?? string.Empty;
            _origFax = lead.Fax ?? string.Empty;
            _origWebsite = lead.Website ?? string.Empty;
            _origAddress = lead.Address ?? string.Empty;
            _origCity = lead.City ?? string.Empty;
            _origCounty = lead.County ?? string.Empty;
            _origState = lead.State ?? string.Empty;
            _origZipCode = lead.ZipCode ?? string.Empty;
            _origAnnualSales = lead.AnnualSales ?? string.Empty;
            _origNumberOfEmployees = lead.NumberOfEmployees ?? string.Empty;
            _origSicCode = lead.SicCode ?? string.Empty;
            _origSicDescription = lead.SicDescription ?? string.Empty;
            _origNaicsCode = lead.NaicsCode ?? string.Empty;
            _origNaicsDescription = lead.NaicsDescription ?? string.Empty;

            ProfileCallDate = _origCallDate;
            ProfileCallDisposition = _origCallDisposition;
            ProfileAssignedAgent = _origAssignedAgent;
            ProfileNotes = _origNotes;
            ProfileCallCount = _origCallCount;
            ProfileBusinessName = _origBusinessName;
            ProfileContactName = _origContactName;
            ProfileTitle = _origTitle;
            ProfileCellNumber = _origCellNumber;
            ProfileEmail = _origEmail;
            ProfileFax = _origFax;
            ProfileWebsite = _origWebsite;
            ProfileAddress = _origAddress;
            ProfileCity = _origCity;
            ProfileCounty = _origCounty;
            ProfileState = _origState;
            ProfileZipCode = _origZipCode;
            ProfileAnnualSales = _origAnnualSales;
            ProfileNumberOfEmployees = _origNumberOfEmployees;
            ProfileSicCode = _origSicCode;
            ProfileSicDescription = _origSicDescription;
            ProfileNaicsCode = _origNaicsCode;
            ProfileNaicsDescription = _origNaicsDescription;

            OnPropertyChanged(nameof(HasProfileChanges));
            ProfileAssignedClients = new ObservableCollection<LeadAssignedClientInfo>();
            ProfileCallHistory = new ObservableCollection<LeadCallHistoryEntry>();
            IsLeadProfileModalOpen = true;

            IsLoadingProfileHistory = true;
            try
            {
                var (ok, _, histData) = await ApiService.Instance.GetLeadHistoryAsync(lead.Id);
                if (ok && histData != null)
                {
                    ProfileAssignedClients = new ObservableCollection<LeadAssignedClientInfo>(histData.AssignedClients ?? new List<LeadAssignedClientInfo>());
                    ProfileCallHistory = new ObservableCollection<LeadCallHistoryEntry>(histData.History ?? new List<LeadCallHistoryEntry>());
                }
            }
            finally
            {
                IsLoadingProfileHistory = false;
            }
        }

        private async Task ExecuteUpdateLeadProfileAsync()
        {
            if (ProfileLead == null || !HasProfileChanges) return;

            IsSavingProfile = true;
            try
            {
                int parsedCallCount = int.TryParse(ProfileCallCount, out var cc) && cc >= 0 ? cc : ProfileLead.CallCount;
                var payload = new
                {
                    call_date = ProfileCallDate,
                    call_disposition = ProfileCallDisposition,
                    assigned_agent = ProfileAssignedAgent,
                    notes = ProfileNotes,
                    call_count = parsedCallCount,
                    business_name = ProfileBusinessName,
                    contact_name = ProfileContactName,
                    title = ProfileTitle,
                    cell_number = ProfileCellNumber,
                    email = ProfileEmail,
                    fax = ProfileFax,
                    website = ProfileWebsite,
                    address = ProfileAddress,
                    city = ProfileCity,
                    county = ProfileCounty,
                    state = ProfileState,
                    zip_code = ProfileZipCode,
                    annual_sales = ProfileAnnualSales,
                    number_of_employees = ProfileNumberOfEmployees,
                    sic_code = ProfileSicCode,
                    sic_description = ProfileSicDescription,
                    naics_code = ProfileNaicsCode,
                    naics_description = ProfileNaicsDescription
                };

                var (ok, msg, _) = await ApiService.Instance.UpdateLeadAsync(ProfileLead.Id, payload);
                if (ok)
                {
                    // Suppress per-field autosync callbacks while copying back to row model
                    var prevCallback = ProfileLead.OnFieldChanged;
                    ProfileLead.OnFieldChanged = null;
                    try
                    {
                        ProfileLead.CallDate = ProfileCallDate;
                        ProfileLead.CallDisposition = ProfileCallDisposition;
                        ProfileLead.AssignedAgent = ProfileAssignedAgent;
                        ProfileLead.Notes = ProfileNotes;
                        ProfileLead.CallCount = parsedCallCount;
                        ProfileLead.BusinessName = ProfileBusinessName;
                        ProfileLead.ContactName = ProfileContactName;
                        ProfileLead.Title = ProfileTitle;
                        ProfileLead.CellNumber = ProfileCellNumber;
                        ProfileLead.Email = ProfileEmail;
                        ProfileLead.Fax = ProfileFax;
                        ProfileLead.Website = ProfileWebsite;
                        ProfileLead.Address = ProfileAddress;
                        ProfileLead.City = ProfileCity;
                        ProfileLead.County = ProfileCounty;
                        ProfileLead.State = ProfileState;
                        ProfileLead.ZipCode = ProfileZipCode;
                        ProfileLead.AnnualSales = ProfileAnnualSales;
                        ProfileLead.NumberOfEmployees = ProfileNumberOfEmployees;
                        ProfileLead.SicCode = ProfileSicCode;
                        ProfileLead.SicDescription = ProfileSicDescription;
                        ProfileLead.NaicsCode = ProfileNaicsCode;
                        ProfileLead.NaicsDescription = ProfileNaicsDescription;
                    }
                    finally
                    {
                        ProfileLead.OnFieldChanged = prevCallback;
                    }

                    _origCallDate = ProfileCallDate;
                    _origCallDisposition = ProfileCallDisposition;
                    _origAssignedAgent = ProfileAssignedAgent;
                    _origNotes = ProfileNotes;
                    _origCallCount = parsedCallCount.ToString();
                    _origBusinessName = ProfileBusinessName;
                    _origContactName = ProfileContactName;
                    _origTitle = ProfileTitle;
                    _origCellNumber = ProfileCellNumber;
                    _origEmail = ProfileEmail;
                    _origFax = ProfileFax;
                    _origWebsite = ProfileWebsite;
                    _origAddress = ProfileAddress;
                    _origCity = ProfileCity;
                    _origCounty = ProfileCounty;
                    _origState = ProfileState;
                    _origZipCode = ProfileZipCode;
                    _origAnnualSales = ProfileAnnualSales;
                    _origNumberOfEmployees = ProfileNumberOfEmployees;
                    _origSicCode = ProfileSicCode;
                    _origSicDescription = ProfileSicDescription;
                    _origNaicsCode = ProfileNaicsCode;
                    _origNaicsDescription = ProfileNaicsDescription;

                    OnPropertyChanged(nameof(HasProfileChanges));
                    ShowBanner($"Lead '{ProfileBusinessName}' updated successfully.");
                }
                else
                {
                    ShowBanner($"Update failed: {msg}", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowBanner($"Update error: {ex.Message}", isError: true);
            }
            finally
            {
                IsSavingProfile = false;
            }
        }

        // ─── Assign to Client Actions ────────────────────────────────────────────

        private void ExecuteOpenAssignClientModal()
        {
            if (FilteredCount == 0)
            {
                MessageBox.Show("There are no leads matching your current filters to assign.", "Assign to Client", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AssignErrorMessage = string.Empty;
            if (SelectedTargetClient == null)
            {
                SelectedTargetClient = AvailableClients.FirstOrDefault();
            }

            AssignModalSummary = HasActiveFilters
                ? $"Assign all {FilteredCount:N0} currently filtered lead(s) to the selected client."
                : $"Assign all {TotalCount:N0} lead(s) in the database to the selected client.";

            IsAssignClientModalOpen = true;
        }

        private async Task ExecuteConfirmAssignClientAsync()
        {
            if (SelectedTargetClient == null)
            {
                AssignErrorMessage = "Please select a client to assign.";
                return;
            }

            IsAssigning = true;
            AssignErrorMessage = string.Empty;
            try
            {
                var queryReq = BuildQueryRequest(limit: 0, offset: 0);
                var (ok, msg, count) = await ApiService.Instance.AssignLeadsToClientAsync(
                    SelectedTargetClient.Id,
                    leadIds: null,
                    filters: queryReq);

                if (ok)
                {
                    IsAssignClientModalOpen = false;
                    ShowBanner($"Successfully assigned {count:N0} lead(s) to {SelectedTargetClient.FullName}!");
                    await FetchLeadsPageAsync();
                }
                else
                {
                    AssignErrorMessage = msg;
                }
            }
            catch (Exception ex)
            {
                AssignErrorMessage = ex.Message;
            }
            finally
            {
                IsAssigning = false;
            }
        }

        private void ShowBanner(string message, bool isError = false)
        {
            StatusBannerMessage = message;
            IsBannerError = isError;
            IsBannerVisible = true;
            _ = Task.Run(async () =>
            {
                await Task.Delay(4500);
                IsBannerVisible = false;
            });
        }

        /// <summary>
        /// Sends sort parameters to MySQL and resets pagination to Page 1.
        /// </summary>
        public void SortMatchedLeads(string sortMemberPath, ListSortDirection direction)
        {
            _sortBy = sortMemberPath;
            _sortDirection = direction == ListSortDirection.Ascending ? "ASC" : "DESC";
            CurrentPage = 1;
            _ = FetchLeadsPageAsync();
        }

        /// <summary>
        /// Exports all filtered leads matching currently selected criteria directly from database without missing any records.
        /// </summary>
        private async void ExecuteExportFiltered()
        {
            if (FilteredCount == 0)
            {
                MessageBox.Show("There are no leads matching your selection to export.", "Export Filtered Leads", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var title = HasActiveFilters ? "Filtered Leads" : "Lead Masterlist";
            var filePrefix = HasActiveFilters ? "Filtered_Leads" : "Lead_Masterlist";

            var req = BuildQueryRequest(limit: 0, offset: 0);
            await RunExcelExportAsync(req, filePrefix, title, FilteredCount);
        }

        /// <summary>
        /// Exports all leads in the database without any active filters.
        /// </summary>
        private async void ExecuteExportAll()
        {
            if (TotalCount == 0)
            {
                MessageBox.Show("There are no leads in the database to export.", "Export All Leads", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var req = new LeadQueryRequest { Limit = 0, Offset = 0, SortBy = "id", SortDirection = "ASC" };
            await RunExcelExportAsync(req, "All_Leads_Masterlist", "Complete Lead Masterlist", TotalCount);
        }

        /// <summary>
        /// Server-driven Excel exporter: fetches all matching records and streams them to Excel with progress feedback.
        /// </summary>
        private async Task RunExcelExportAsync(LeadQueryRequest queryReq, string filePrefix, string sheetTitle, int expectedCount)
        {
            var dialog = new SaveFileDialog
            {
                Title = $"Export {sheetTitle} to Excel",
                FileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() != true) return;

            string savePath = dialog.FileName;

            _exportCts?.Dispose();
            _exportCts = new CancellationTokenSource();
            var token = _exportCts.Token;

            IsExportModalOpen = true;
            ExportModalTitle = $"Exporting {sheetTitle} to Excel...";
            ExportModalSubtitle = $"{System.IO.Path.GetFileName(savePath)} • ~{expectedCount:N0} Leads";
            ExportModalStatus = "Querying records from server database...";
            ExportProgressPercentage = 0;
            IsExportIndeterminate = true;

            try
            {
                var (success, msg, data) = await ApiService.Instance.QueryLeadsAsync(queryReq, token);
                if (!success || data?.Leads == null || data.Leads.Count == 0)
                {
                    MessageBox.Show(
                        $"Could not retrieve leads from server for export: {msg}",
                        "Export Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var leadsToExport = data.Leads;
                ExportModalSubtitle = $"{System.IO.Path.GetFileName(savePath)} • {leadsToExport.Count:N0} Leads";
                ExportModalStatus = "Building Excel file...";

                var progress = new Progress<(int written, int total, double pct, string msg)>(p =>
                {
                    IsExportIndeterminate = p.total <= 0;
                    ExportProgressPercentage = p.pct;
                    ExportModalStatus = p.msg;
                });

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Leads");

                    string[] headers = new[]
                    {
                        "STATUS", "BUSINESS NAME", "ADDRESS", "CITY", "COUNTY", "STATE", "ZIP CODE",
                        "CELL NUMBER", "FAX", "ANNUAL SALES", "NUMBER OF EMPLOYEE", "TITLE",
                        "CONTACT NAME", "WEBSITE", "SIC CODE", "SIC DESCRIPTION", "NAICS CODE",
                        "NAICS DESCRIPTION", "BATCH NAME"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }
                    worksheet.Row(1).Height = 26;

                    int total = leadsToExport.Count;
                    int row = 2;
                    int counter = 0;

                    foreach (var lead in leadsToExport)
                    {
                        counter++;
                        if (counter % 1000 == 0)
                        {
                            token.ThrowIfCancellationRequested();
                            double pct = (double)counter / total * 100.0;
                            ((IProgress<(int, int, double, string)>)progress).Report((counter, total, pct, $"Writing rows: {counter:N0} of {total:N0} ({pct:F1}%)..."));
                        }

                        worksheet.Cell(row, 1).SetValue(lead.Status);
                        worksheet.Cell(row, 2).SetValue(lead.BusinessName);
                        worksheet.Cell(row, 3).SetValue(lead.Address);
                        worksheet.Cell(row, 4).SetValue(lead.City);
                        worksheet.Cell(row, 5).SetValue(lead.County);
                        worksheet.Cell(row, 6).SetValue(lead.State);
                        worksheet.Cell(row, 7).SetValue(lead.ZipCode);
                        worksheet.Cell(row, 8).SetValue(lead.CellNumber);
                        worksheet.Cell(row, 9).SetValue(lead.Fax);
                        worksheet.Cell(row, 10).SetValue(lead.AnnualSales);
                        worksheet.Cell(row, 11).SetValue(lead.NumberOfEmployees);
                        worksheet.Cell(row, 12).SetValue(lead.Title);
                        worksheet.Cell(row, 13).SetValue(lead.ContactName);
                        worksheet.Cell(row, 14).SetValue(lead.Website);
                        worksheet.Cell(row, 15).SetValue(lead.SicCode);
                        worksheet.Cell(row, 16).SetValue(lead.SicDescription);
                        worksheet.Cell(row, 17).SetValue(lead.NaicsCode);
                        worksheet.Cell(row, 18).SetValue(lead.NaicsDescription);
                        worksheet.Cell(row, 19).SetValue(lead.BatchName);

                        if (row % 2 == 1)
                        {
                            worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                        }
                        worksheet.Row(row).Height = 20;
                        row++;
                    }

                    token.ThrowIfCancellationRequested();
                    ((IProgress<(int, int, double, string)>)progress).Report((total, total, 98.0, "Auto-adjusting column widths and saving..."));

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(savePath);
                }, token);

                _ = ApiService.Instance.LogActivityAsync(
                    "EXPORT",
                    "Exported Leads",
                    $"Exported {leadsToExport.Count:N0} leads to Excel ({System.IO.Path.GetFileName(savePath)})",
                    leadsToExport.Count
                );

                MessageBox.Show(
                    $"Successfully exported {leadsToExport.Count:N0} leads to Excel!\n\nSaved to: {savePath}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                if (System.IO.File.Exists(savePath))
                {
                    try { System.IO.File.Delete(savePath); } catch { }
                }
                MessageBox.Show("Export was cancelled by user.", "Export Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export leads: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExportModalOpen = false;
            }
        }
    }
}
