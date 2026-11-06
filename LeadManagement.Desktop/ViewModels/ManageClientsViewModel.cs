using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.Services;
using Microsoft.Win32;

namespace LeadManagement.Desktop.ViewModels
{
    /// <summary>
    /// Wraps a UserModel with an IsSelected flag for the agent-assignment checkbox list.
    /// </summary>
    public class AgentSelectionItem : BaseViewModel
    {
        private bool _isSelected;

        public UserModel Agent { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public AgentSelectionItem(UserModel agent, bool selected = false)
        {
            Agent = agent;
            _isSelected = selected;
        }
    }

    /// <summary>
    /// ViewModel for the Manage Clients page.
    /// Handles CRUD for clients, file uploads per client, and agent assignment.
    /// </summary>
    public class ManageClientsViewModel : BaseViewModel
    {
        // ----------------------------------------------------------------
        // State
        // ----------------------------------------------------------------
        private ObservableCollection<ClientModel> _clients = new();
        private bool _isLoading;
        private bool _isAddClientModalOpen;
        private string _newFirstName = string.Empty;
        private string _newLastName = string.Empty;
        private bool _isSaving;
        private string _formErrorMessage = string.Empty;
        private string _statusBannerMessage = string.Empty;
        private bool _isBannerVisible;
        private bool _isBannerError;

        // Preview state
        private bool _isPreviewOpen;
        private string _previewTitle = string.Empty;
        private string _previewSubtitle = string.Empty;
        private string _previewSearchText = string.Empty;
        private bool _isLoadingPreview;
        private ObservableCollection<LeadModel> _previewLeads = new();
        private ObservableCollection<LeadModel> _filteredPreviewLeads = new();
        private List<LeadModel> _allPreviewLeads = new();

        // The full agent list (all role=user accounts) — loaded once
        private List<UserModel> _allAgents = new();

        // Per-client selection items (keyed by client id)
        private readonly Dictionary<int, ObservableCollection<AgentSelectionItem>> _agentSelectionMap = new();

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------
        public ManageClientsViewModel()
        {
            LoadClientsCommand   = new RelayCommand(async () => await LoadClientsAsync());
            RefreshCommand       = new RelayCommand(async () => await LoadClientsAsync());
            OpenAddClientModalCommand  = new RelayCommand(ExecuteOpenAddClientModal);
            CloseAddClientModalCommand = new RelayCommand(ExecuteCloseAddClientModal);
            CreateClientCommand  = new RelayCommand(async () => await ExecuteCreateClientAsync(), () => !IsSaving);
            BrowseFileCommand    = new RelayCommand<ClientModel>(async c => await ExecuteBrowseFileAsync(c));
            ToggleExpandCommand  = new RelayCommand<ClientModel>(ExecuteToggleExpand);
            ToggleAssignDropdownCommand = new RelayCommand<ClientModel>(ExecuteToggleAssignDropdown);
            SaveAgentAssignmentCommand  = new RelayCommand<ClientModel>(async c => await ExecuteSaveAgentAssignmentAsync(c));
            DeleteClientCommand  = new RelayCommand<ClientModel>(async c => await ExecuteDeleteClientAsync(c));
            DeleteFileCommand    = new RelayCommand<ClientFileModel>(async f => await ExecuteDeleteFileAsync(f));
            PreviewFileCommand   = new RelayCommand<ClientFileModel>(async f => await ExecutePreviewFileAsync(f));
            ClosePreviewCommand  = new RelayCommand(ExecuteClosePreview);
            DismissBannerCommand = new RelayCommand(ExecuteDismissBanner);
        }

        // ----------------------------------------------------------------
        // Properties
        // ----------------------------------------------------------------
        public ObservableCollection<ClientModel> Clients
        {
            get => _clients;
            set => SetProperty(ref _clients, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsAddClientModalOpen
        {
            get => _isAddClientModalOpen;
            set => SetProperty(ref _isAddClientModalOpen, value);
        }

        public string NewFirstName
        {
            get => _newFirstName;
            set
            {
                if (SetProperty(ref _newFirstName, value))
                    FormErrorMessage = string.Empty;
            }
        }

        public string NewLastName
        {
            get => _newLastName;
            set
            {
                if (SetProperty(ref _newLastName, value))
                    FormErrorMessage = string.Empty;
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                if (SetProperty(ref _isSaving, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public string FormErrorMessage
        {
            get => _formErrorMessage;
            set
            {
                if (SetProperty(ref _formErrorMessage, value))
                    OnPropertyChanged(nameof(HasFormError));
            }
        }

        public bool HasFormError => !string.IsNullOrEmpty(FormErrorMessage);

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

        public bool HasNoAgents => _allAgents.Count == 0;

        // --- Preview Properties ---
        public bool IsPreviewOpen
        {
            get => _isPreviewOpen;
            set => SetProperty(ref _isPreviewOpen, value);
        }

        public string PreviewTitle
        {
            get => _previewTitle;
            set => SetProperty(ref _previewTitle, value);
        }

        public string PreviewSubtitle
        {
            get => _previewSubtitle;
            set => SetProperty(ref _previewSubtitle, value);
        }

        public string PreviewSearchText
        {
            get => _previewSearchText;
            set
            {
                if (SetProperty(ref _previewSearchText, value))
                    ApplyPreviewSearch();
            }
        }

        public bool IsLoadingPreview
        {
            get => _isLoadingPreview;
            set => SetProperty(ref _isLoadingPreview, value);
        }

        public ObservableCollection<LeadModel> FilteredPreviewLeads
        {
            get => _filteredPreviewLeads;
            set => SetProperty(ref _filteredPreviewLeads, value);
        }

        // ----------------------------------------------------------------
        // Commands
        // ----------------------------------------------------------------
        public ICommand LoadClientsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenAddClientModalCommand { get; }
        public ICommand CloseAddClientModalCommand { get; }
        public ICommand CreateClientCommand { get; }
        public ICommand BrowseFileCommand { get; }
        public ICommand ToggleExpandCommand { get; }
        public ICommand ToggleAssignDropdownCommand { get; }
        public ICommand SaveAgentAssignmentCommand { get; }
        public ICommand DeleteClientCommand { get; }
        public ICommand DeleteFileCommand { get; }
        public ICommand PreviewFileCommand { get; }
        public ICommand ClosePreviewCommand { get; }
        public ICommand DismissBannerCommand { get; }

        // ----------------------------------------------------------------
        // Agent selection helpers (called from the View code-behind)
        // ----------------------------------------------------------------
        public ObservableCollection<AgentSelectionItem> GetAgentSelectionItems(int clientId)
        {
            if (_agentSelectionMap.TryGetValue(clientId, out var items))
                return items;
            return new ObservableCollection<AgentSelectionItem>();
        }

        // ----------------------------------------------------------------
        // Public entry point called by MainViewModel when navigating here
        // ----------------------------------------------------------------
        public async Task LoadClientsAsync(bool preserveBanner = false)
        {
            IsLoading = true;
            if (!preserveBanner)
            {
                IsBannerVisible = false;
            }

            try
            {
                // Save currently expanded client IDs so folder state is preserved
                var expandedIds = new HashSet<int>(Clients.Where(c => c.IsExpanded).Select(c => c.Id));

                // Load agents (users) in parallel with clients
                var agentsTask  = ApiService.Instance.GetUsersAsync();
                var clientsTask = ApiService.Instance.GetClientsAsync();

                await Task.WhenAll(agentsTask, clientsTask);

                var (aOk, _, agents) = agentsTask.Result;
                if (aOk && agents != null)
                {
                    _allAgents = agents.Where(u => u.Role == "user").ToList();
                    OnPropertyChanged(nameof(HasNoAgents));
                }

                var (cOk, cMsg, clients) = clientsTask.Result;
                if (cOk && clients != null)
                {
                    // Rebuild agent-selection maps and restore expansion state
                    _agentSelectionMap.Clear();
                    foreach (var client in clients)
                    {
                        if (expandedIds.Contains(client.Id))
                        {
                            client.IsExpanded = true;
                        }
                        RebuildAgentSelection(client);
                    }

                    Clients = new ObservableCollection<ClientModel>(clients);
                }
                else
                {
                    ShowBanner($"Could not load clients: {cMsg}", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowBanner($"Unexpected error: {ex.Message}", isError: true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ----------------------------------------------------------------
        // Add Client Modal
        // ----------------------------------------------------------------
        private void ExecuteOpenAddClientModal()
        {
            NewFirstName     = string.Empty;
            NewLastName      = string.Empty;
            FormErrorMessage = string.Empty;
            IsAddClientModalOpen = true;
        }

        private void ExecuteCloseAddClientModal()
        {
            IsAddClientModalOpen = false;
        }

        private async Task ExecuteCreateClientAsync()
        {
            if (string.IsNullOrWhiteSpace(NewFirstName))
            {
                FormErrorMessage = "First name is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(NewLastName))
            {
                FormErrorMessage = "Last name is required.";
                return;
            }

            IsSaving = true;
            FormErrorMessage = string.Empty;
            try
            {
                var (ok, msg, client) = await ApiService.Instance.CreateClientAsync(NewFirstName, NewLastName);
                if (ok && client != null)
                {
                    IsAddClientModalOpen = false;
                    ShowBanner($"✓ Client '{client.FullName}' added successfully.", isError: false);
                    await LoadClientsAsync(preserveBanner: true);
                }
                else
                {
                    FormErrorMessage = msg;
                }
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ----------------------------------------------------------------
        // Expand / Collapse client card
        // ----------------------------------------------------------------
        private void ExecuteToggleExpand(ClientModel? client)
        {
            if (client == null) return;
            client.IsExpanded = !client.IsExpanded;
        }

        // ----------------------------------------------------------------
        // File upload — browse dialog
        // ----------------------------------------------------------------
        private async Task ExecuteBrowseFileAsync(ClientModel? client)
        {
            if (client == null) return;

            var dialog = new OpenFileDialog
            {
                Title  = $"Select Leads File for {client.FullName}",
                Filter = "Spreadsheets (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true) return;

            await ProcessFileUploadAsync(client, dialog.FileName);
        }

        // Called from View code-behind on drag-drop (kept for backward compat)
        public async Task HandleDroppedFileAsync(ClientModel client, string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".csv" && ext != ".xlsx" && ext != ".xls")
            {
                ShowBanner("Only .csv, .xlsx and .xls files are supported.", isError: true);
                return;
            }
            await ProcessFileUploadAsync(client, filePath);
        }

        private async Task ProcessFileUploadAsync(ClientModel client, string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var ext      = fileInfo.Extension.ToLowerInvariant();
            var sizeDisplay = $"{fileInfo.Length / 1024.0:F1} KB";

            ShowBanner($"Parsing '{fileInfo.Name}'…", isError: false);

            List<LeadModel>? leads;
            try
            {
                leads = ext == ".csv"
                    ? ParseCsvFile(filePath)
                    : ParseExcelFile(filePath);
            }
            catch (Exception ex)
            {
                ShowBanner($"Could not parse file: {ex.Message}", isError: true);
                return;
            }

            if (leads == null || leads.Count == 0)
            {
                ShowBanner("No data rows found in the selected file.", isError: true);
                return;
            }

            ShowBanner($"Uploading {leads.Count} leads for '{client.FullName}'…", isError: false);

            var (ok, msg, _) = await ApiService.Instance.UploadClientFileAsync(
                client.Id, fileInfo.Name, sizeDisplay, leads);

            if (ok)
            {
                client.IsExpanded = true;
                ShowBanner($"✓ {leads.Count} leads uploaded to '{client.FullName}' from '{fileInfo.Name}'.", isError: false);
                await LoadClientsAsync(preserveBanner: true);
            }
            else
            {
                ShowBanner($"Upload failed: {msg}", isError: true);
            }
        }

        // ----------------------------------------------------------------
        // Delete file
        // ----------------------------------------------------------------
        private async Task ExecuteDeleteFileAsync(ClientFileModel? file)
        {
            if (file == null) return;

            var confirm = System.Windows.MessageBox.Show(
                $"Delete '{file.FileName}'?\n\nThis will permanently remove the file and all {file.TotalLeads:N0} leads associated with it.",
                "Delete File",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            var (ok, msg) = await ApiService.Instance.DeleteClientFileAsync(file.Id);
            if (ok)
            {
                // If the preview is open for this file, close it
                if (IsPreviewOpen && PreviewTitle.Contains(file.FileName))
                    ExecuteClosePreview();

                ShowBanner($"✓ File '{file.FileName}' and its leads were deleted.", isError: false);
                await LoadClientsAsync(preserveBanner: true);
            }
            else
            {
                ShowBanner($"Delete failed: {msg}", isError: true);
            }
        }

        // ----------------------------------------------------------------
        // Preview file leads
        // ----------------------------------------------------------------
        private async Task ExecutePreviewFileAsync(ClientFileModel? file)
        {
            if (file == null) return;

            IsLoadingPreview = true;
            IsPreviewOpen    = true;
            PreviewTitle     = file.FileName;
            PreviewSubtitle  = $"{file.ClientName}  ·  {file.TotalLeads:N0} leads  ·  {file.FileSize}  ·  {file.FormattedDate}";
            PreviewSearchText = string.Empty;
            _allPreviewLeads.Clear();
            FilteredPreviewLeads = new ObservableCollection<LeadModel>();

            try
            {
                var (ok, msg, data) = await ApiService.Instance.GetClientFileLeadsAsync(file.Id);
                if (ok && data?.Leads != null)
                {
                    _allPreviewLeads = data.Leads;
                    PreviewSubtitle  = $"{file.ClientName}  ·  {_allPreviewLeads.Count:N0} leads  ·  {file.FileSize}  ·  {file.FormattedDate}";
                    ApplyPreviewSearch();
                }
                else
                {
                    ShowBanner($"Could not load preview: {msg}", isError: true);
                    IsPreviewOpen = false;
                }
            }
            catch (Exception ex)
            {
                ShowBanner($"Preview error: {ex.Message}", isError: true);
                IsPreviewOpen = false;
            }
            finally
            {
                IsLoadingPreview = false;
            }
        }

        private void ApplyPreviewSearch()
        {
            var q = PreviewSearchText?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(q))
            {
                FilteredPreviewLeads = new ObservableCollection<LeadModel>(_allPreviewLeads);
            }
            else
            {
                var filtered = _allPreviewLeads.Where(l =>
                    (l.BusinessName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.ContactName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.City?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.State?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.CellNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.Status?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
                FilteredPreviewLeads = new ObservableCollection<LeadModel>(filtered);
            }
        }

        private void ExecuteClosePreview()
        {
            IsPreviewOpen = false;
            PreviewSearchText = string.Empty;
            _allPreviewLeads.Clear();
            FilteredPreviewLeads = new ObservableCollection<LeadModel>();
        }

        // ----------------------------------------------------------------
        // Assign agents dropdown
        // ----------------------------------------------------------------
        private void ExecuteToggleAssignDropdown(ClientModel? client)
        {
            if (client == null) return;

            bool targetState = !client.IsAssignDropdownOpen;

            // Close all others first without modifying collection
            foreach (var c in Clients)
            {
                if (!ReferenceEquals(c, client) && c.IsAssignDropdownOpen)
                {
                    c.IsAssignDropdownOpen = false;
                }
            }

            client.IsAssignDropdownOpen = targetState;
        }

        private async Task ExecuteSaveAgentAssignmentAsync(ClientModel? client)
        {
            if (client == null) return;

            var selectedIds = client.AgentSelectionItems
                .OfType<AgentSelectionItem>()
                .Where(i => i.IsSelected)
                .Select(i => i.Agent.Id)
                .ToList();

            var (ok, msg) = await ApiService.Instance.AssignAgentsToClientAsync(client.Id, selectedIds);

            client.IsAssignDropdownOpen = false;

            if (ok)
            {
                ShowBanner($"✓ Agents updated for '{client.FullName}'.", isError: false);
                await LoadClientsAsync(preserveBanner: true);
            }
            else
            {
                ShowBanner($"Assignment failed: {msg}", isError: true);
            }
        }

        // ----------------------------------------------------------------
        // Delete client
        // ----------------------------------------------------------------
        private async Task ExecuteDeleteClientAsync(ClientModel? client)
        {
            if (client == null) return;

            var confirm = System.Windows.MessageBox.Show(
                $"Delete client '{client.FullName}'?\n\nThis will permanently remove the client and all {client.FileCount} file(s) with {client.TotalLeads:N0} leads.",
                "Delete Client",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            var (ok, msg) = await ApiService.Instance.DeleteClientAsync(client.Id);
            if (ok)
            {
                if (IsPreviewOpen)
                    ExecuteClosePreview();

                ShowBanner($"✓ Client '{client.FullName}' and all associated files deleted.", isError: false);
                await LoadClientsAsync(preserveBanner: true);
            }
            else
            {
                ShowBanner($"Delete failed: {msg}", isError: true);
            }
        }

        // ----------------------------------------------------------------
        // Banner
        // ----------------------------------------------------------------
        private void ExecuteDismissBanner() => IsBannerVisible = false;

        private void ShowBanner(string message, bool isError)
        {
            StatusBannerMessage = message;
            IsBannerError       = isError;
            IsBannerVisible     = true;
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------
        private void RebuildAgentSelection(ClientModel client)
        {
            var assignedIds = new HashSet<int>(client.AssignedAgents?.Select(a => a.Id) ?? Enumerable.Empty<int>());
            var items = new System.Collections.ObjectModel.ObservableCollection<object>(
                _allAgents.Select(a => (object)new AgentSelectionItem(a, assignedIds.Contains(a.Id)))
            );
            client.AgentSelectionItems = items;
            _agentSelectionMap[client.Id] = new ObservableCollection<AgentSelectionItem>(
                items.Cast<AgentSelectionItem>()
            );
        }



        // ----------------------------------------------------------------
        // File parsing (reused from ImportLeadsViewModel)
        // ----------------------------------------------------------------
        private static List<LeadModel> ParseCsvFile(string path)
        {
            var records = new List<LeadModel>();
            var lines   = File.ReadAllLines(path);
            if (lines.Length <= 1) return records;

            var headers = SplitCsvLine(lines[0])
                .Select(h => h.Trim().ToLowerInvariant().Replace("_", " ")).ToList();

            int GetCol(params string[] aliases)
            {
                for (int i = 0; i < headers.Count; i++)
                    foreach (var a in aliases)
                        if (headers[i].Equals(a, StringComparison.OrdinalIgnoreCase) ||
                            headers[i].Contains(a, StringComparison.OrdinalIgnoreCase))
                            return i;
                return -1;
            }

            int idxBusiness = GetCol("business name","company","business");
            int idxAddress  = GetCol("address","street");
            int idxCity     = GetCol("city");
            int idxCounty   = GetCol("county");
            int idxState    = GetCol("state");
            int idxZip      = GetCol("zip code","zip","postal");
            int idxCell     = GetCol("cell number","cell","phone","telephone","mobile");
            int idxFax      = GetCol("fax");
            int idxSales    = GetCol("annual sales","sales");
            int idxEmp      = GetCol("number of employee","employees");
            int idxTitle    = GetCol("title","job title");
            int idxContact  = GetCol("contact name","contact","name");
            int idxWeb      = GetCol("website","web","url","site");
            int idxSicCode  = GetCol("sic code","sic");
            int idxSicDesc  = GetCol("sic description","sic desc");
            int idxNaics    = GetCol("naics code","naics");
            int idxNaicsD   = GetCol("naics description","naics desc");

            string SafeGet(string[] parts, int idx, int fallback, string def = "")
            {
                int t = idx >= 0 ? idx : fallback;
                if (t >= 0 && t < parts.Length)
                { var v = parts[t].Trim(); return string.IsNullOrEmpty(v) ? def : v; }
                return def;
            }

            int rowId = 1;
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = SplitCsvLine(line);
                records.Add(new LeadModel
                {
                    Id               = rowId++,
                    BusinessName     = SafeGet(parts, idxBusiness, 0, "Unnamed Business"),
                    Address          = SafeGet(parts, idxAddress,  1),
                    City             = SafeGet(parts, idxCity,     2),
                    County           = SafeGet(parts, idxCounty,   3),
                    State            = SafeGet(parts, idxState,    4, "NE"),
                    ZipCode          = SafeGet(parts, idxZip,      5),
                    CellNumber       = SafeGet(parts, idxCell,     6),
                    Fax              = SafeGet(parts, idxFax,      7),
                    AnnualSales      = SafeGet(parts, idxSales,    8, "Less than 500,000"),
                    NumberOfEmployees= SafeGet(parts, idxEmp,      9, "1 to 4"),
                    Title            = SafeGet(parts, idxTitle,   10),
                    ContactName      = SafeGet(parts, idxContact, 11),
                    Website          = SafeGet(parts, idxWeb,     12),
                    SicCode          = SafeGet(parts, idxSicCode, 13, "58120000"),
                    SicDescription   = SafeGet(parts, idxSicDesc, 14, "EATING PLACES"),
                    NaicsCode        = SafeGet(parts, idxNaics,   15, "722511"),
                    NaicsDescription = SafeGet(parts, idxNaicsD,  16, "RESTAURANTS"),
                    Status           = "New Lead"
                });
            }
            return records;
        }

        private static List<LeadModel> ParseExcelFile(string path)
        {
            var records = new List<LeadModel>();
            using var workbook   = new XLWorkbook(path);
            var worksheet        = workbook.Worksheets.First();
            var usedRange        = worksheet.RangeUsed();
            if (usedRange == null) return records;

            var rows = usedRange.RowsUsed().ToList();
            if (rows.Count <= 1) return records;

            var headers = rows[0].Cells()
                .Select(c => (c.IsEmpty() ? string.Empty : c.GetFormattedString()).Trim().ToLowerInvariant().Replace("_", " "))
                .ToList();

            int GetCol(params string[] aliases)
            {
                for (int i = 0; i < headers.Count; i++)
                    foreach (var a in aliases)
                        if (headers[i].Equals(a, StringComparison.OrdinalIgnoreCase) ||
                            headers[i].Contains(a, StringComparison.OrdinalIgnoreCase))
                            return i;
                return -1;
            }

            int idxBusiness = GetCol("business name","company","business");
            int idxAddress  = GetCol("address","street");
            int idxCity     = GetCol("city");
            int idxCounty   = GetCol("county");
            int idxState    = GetCol("state");
            int idxZip      = GetCol("zip code","zip","postal");
            int idxCell     = GetCol("cell number","cell","phone","telephone","mobile");
            int idxFax      = GetCol("fax");
            int idxSales    = GetCol("annual sales","sales");
            int idxEmp      = GetCol("number of employee","employees");
            int idxTitle    = GetCol("title","job title");
            int idxContact  = GetCol("contact name","contact","name");
            int idxWeb      = GetCol("website","web","url","site");
            int idxSicCode  = GetCol("sic code","sic");
            int idxSicDesc  = GetCol("sic description","sic desc");
            int idxNaics    = GetCol("naics code","naics");
            int idxNaicsD   = GetCol("naics description","naics desc");

            string SafeGet(string[] parts, int idx, int fallback, string def = "")
            {
                int t = idx >= 0 ? idx : fallback;
                if (t >= 0 && t < parts.Length)
                { var v = parts[t].Trim(); return string.IsNullOrEmpty(v) ? def : v; }
                return def;
            }

            int colCount = headers.Count;
            int rowId    = 1;

            foreach (var row in rows.Skip(1))
            {
                var parts = Enumerable.Range(1, colCount).Select(col =>
                {
                    var cell = row.Cell(col);
                    if (cell.IsEmpty()) return string.Empty;
                    var val = cell.GetFormattedString().Trim();
                    if (string.IsNullOrEmpty(val) && cell.HasHyperlink)
                        val = cell.GetHyperlink().ExternalAddress?.ToString()?.Trim() ?? string.Empty;
                    return val;
                }).ToArray();

                if (parts.All(string.IsNullOrWhiteSpace)) continue;

                records.Add(new LeadModel
                {
                    Id               = rowId++,
                    BusinessName     = SafeGet(parts, idxBusiness, 0, "Unnamed Business"),
                    Address          = SafeGet(parts, idxAddress,  1),
                    City             = SafeGet(parts, idxCity,     2),
                    County           = SafeGet(parts, idxCounty,   3),
                    State            = SafeGet(parts, idxState,    4, "NE"),
                    ZipCode          = SafeGet(parts, idxZip,      5),
                    CellNumber       = SafeGet(parts, idxCell,     6),
                    Fax              = SafeGet(parts, idxFax,      7),
                    AnnualSales      = SafeGet(parts, idxSales,    8, "Less than 500,000"),
                    NumberOfEmployees= SafeGet(parts, idxEmp,      9, "1 to 4"),
                    Title            = SafeGet(parts, idxTitle,   10),
                    ContactName      = SafeGet(parts, idxContact, 11),
                    Website          = SafeGet(parts, idxWeb,     12),
                    SicCode          = SafeGet(parts, idxSicCode, 13, "58120000"),
                    SicDescription   = SafeGet(parts, idxSicDesc, 14, "EATING PLACES"),
                    NaicsCode        = SafeGet(parts, idxNaics,   15, "722511"),
                    NaicsDescription = SafeGet(parts, idxNaicsD,  16, "RESTAURANTS"),
                    Status           = "New Lead"
                });
            }
            return records;
        }

        private static string[] SplitCsvLine(string line)
        {
            var result  = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();
            foreach (char c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; }
                else if (c == ',' && !inQuotes) { result.Add(current.ToString().Trim('"', ' ')); current.Clear(); }
                else { current.Append(c); }
            }
            result.Add(current.ToString().Trim('"', ' '));
            return result.ToArray();
        }
    }
}
