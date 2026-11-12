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

        // Loading Modal State (for upload and export operations with cancellation)
        private bool _isLoadingModalOpen;
        private string _loadingModalTitle = string.Empty;
        private string _loadingModalMessage = string.Empty;
        private string _loadingModalDetail = string.Empty;
        private System.Threading.CancellationTokenSource? _loadingCts;

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
            RefreshClientLeadsCommand = new RelayCommand<ClientModel>(async c => await LoadClientLeadsAsync(c));
            ClosePreviewCommand  = new RelayCommand(ExecuteClosePreview);
            DismissBannerCommand = new RelayCommand(ExecuteDismissBanner);
            CallLeadCommand      = new RelayCommand<LeadModel>(ExecuteCallLead);
            CancelLoadingModalCommand = new RelayCommand(ExecuteCancelLoadingModal);
            ExportFileCommand    = new RelayCommand<ClientFileModel>(async f => await ExecuteExportFileAsync(f));

            // Pre-warm Zoom Workplace in background so click-to-dial opens instantaneously
            _ = Task.Run(() => ZoomPhoneService.EnsureZoomRunning());
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

        /// <summary>Master list of color-coded call dispositions matching Google Sheets palette.</summary>
        public List<CallDispositionItem> CallDispositions => CallDispositionHelper.Dispositions;

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

        // Loading Modal properties
        public bool IsLoadingModalOpen
        {
            get => _isLoadingModalOpen;
            set => SetProperty(ref _isLoadingModalOpen, value);
        }

        public string LoadingModalTitle
        {
            get => _loadingModalTitle;
            set => SetProperty(ref _loadingModalTitle, value);
        }

        public string LoadingModalMessage
        {
            get => _loadingModalMessage;
            set => SetProperty(ref _loadingModalMessage, value);
        }

        public string LoadingModalDetail
        {
            get => _loadingModalDetail;
            set => SetProperty(ref _loadingModalDetail, value);
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
        public ICommand RefreshClientLeadsCommand { get; }
        public ICommand ClosePreviewCommand { get; }
        public ICommand DismissBannerCommand { get; }
        public ICommand CallLeadCommand { get; }
        public ICommand CancelLoadingModalCommand { get; }
        public ICommand ExportFileCommand { get; }

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
                        RebuildAgentSelection(client);
                        if (expandedIds.Contains(client.Id))
                        {
                            client.IsExpanded = true;
                            _ = LoadClientLeadsAsync(client);
                        }
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
        private async void ExecuteToggleExpand(ClientModel? client)
        {
            if (client == null) return;
            client.IsExpanded = !client.IsExpanded;
            if (client.IsExpanded && (client.Leads == null || client.Leads.Count == 0))
            {
                await LoadClientLeadsAsync(client);
            }
        }

        public async Task LoadClientLeadsAsync(ClientModel? client)
        {
            if (client == null) return;
            client.IsLoadingLeads = true;
            try
            {
                var (ok, msg, leads) = await ApiService.Instance.GetClientLeadsAsync(client.Id);
                if (ok && leads != null)
                {
                    foreach (var lead in leads)
                    {
                        lead.AvailableAgentOptions = client.AssignedAgentNames;
                        lead.OnFieldChanged = (l, field) => SyncLeadField(l, field);
                    }
                    client.Leads = new ObservableCollection<LeadModel>(leads);
                }
                else
                {
                    client.Leads = new ObservableCollection<LeadModel>();
                }
            }
            catch (Exception ex)
            {
                ShowBanner($"Failed to load leads for '{client.FullName}': {ex.Message}", isError: true);
            }
            finally
            {
                client.IsLoadingLeads = false;
            }
        }

        private void SyncLeadField(LeadModel lead, string fieldName)
        {
            if (lead == null || lead.Id <= 0) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var payload = new Dictionary<string, object>();
                    switch (fieldName)
                    {
                        case "call_date":
                            payload["call_date"] = lead.CallDate;
                            break;
                        case "call_disposition":
                            payload["call_disposition"] = lead.CallDisposition;
                            break;
                        case "assigned_agent":
                            payload["assigned_agent"] = lead.AssignedAgent;
                            var foundAgent = _allAgents.FirstOrDefault(a => a.FullName.Equals(lead.AssignedAgent, StringComparison.OrdinalIgnoreCase));
                            if (foundAgent != null)
                            {
                                payload["assigned_agent_id"] = foundAgent.Id;
                                lead.AssignedAgentId = foundAgent.Id;
                            }
                            else if (string.IsNullOrEmpty(lead.AssignedAgent))
                            {
                                payload["assigned_agent_id"] = null!;
                                lead.AssignedAgentId = null;
                            }
                            break;
                        case "notes":
                            payload["notes"] = lead.Notes;
                            break;
                        case "call_count":
                            payload["call_count"] = lead.CallCount;
                            break;
                        case "cell_number":
                            payload["cell_number"] = lead.CellNumber;
                            break;
                        case "business_name":
                            payload["business_name"] = lead.BusinessName;
                            break;
                        case "contact_name":
                            payload["contact_name"] = lead.ContactName;
                            break;
                        case "email":
                            payload["email"] = lead.Email;
                            break;
                    }

                    if (payload.Count > 0)
                    {
                        await ApiService.Instance.UpdateLeadAsync(lead.Id, payload);
                    }
                }
                catch
                {
                    // Background sync
                }
            });
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

        private void ExecuteCancelLoadingModal()
        {
            try
            {
                _loadingCts?.Cancel();
            }
            catch { }
            IsLoadingModalOpen = false;
        }

        private async Task ProcessFileUploadAsync(ClientModel client, string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var ext      = fileInfo.Extension.ToLowerInvariant();
            var sizeDisplay = $"{fileInfo.Length / 1024.0:F1} KB";

            _loadingCts?.Dispose();
            _loadingCts = new System.Threading.CancellationTokenSource();
            var ct = _loadingCts.Token;

            LoadingModalTitle   = "Uploading Leads File";
            LoadingModalMessage = $"Reading and parsing '{fileInfo.Name}'...";
            LoadingModalDetail  = "Detecting columns and validating lead records...";
            IsLoadingModalOpen  = true;

            try
            {
                List<LeadModel>? leads = null;
                await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    leads = ext == ".csv"
                        ? ParseCsvFile(filePath)
                        : ParseExcelFile(filePath);
                    ct.ThrowIfCancellationRequested();
                }, ct);

                if (leads == null || leads.Count == 0)
                {
                    ShowBanner("No data rows found in the selected file.", isError: true);
                    return;
                }

                LoadingModalMessage = $"Uploading {leads.Count:N0} leads for '{client.FullName}'...";
                LoadingModalDetail  = "Sending records to server and creating file entry...";

                var (ok, msg, _) = await ApiService.Instance.UploadClientFileAsync(
                    client.Id, fileInfo.Name, sizeDisplay, leads, ct);

                if (ok)
                {
                    client.IsExpanded = true;
                    ShowBanner($"✓ {leads.Count:N0} leads uploaded to '{client.FullName}' from '{fileInfo.Name}'.", isError: false);
                    await LoadClientsAsync(preserveBanner: true);
                    var updatedClient = Clients.FirstOrDefault(c => c.Id == client.Id);
                    if (updatedClient != null)
                    {
                        updatedClient.IsExpanded = true;
                        await LoadClientLeadsAsync(updatedClient);
                    }
                }
                else
                {
                    if (ct.IsCancellationRequested)
                    {
                        ShowBanner("Upload cancelled by user.", isError: false);
                    }
                    else
                    {
                        ShowBanner($"Upload failed: {msg}", isError: true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ShowBanner("Upload cancelled by user.", isError: false);
            }
            catch (Exception ex)
            {
                ShowBanner($"Could not parse or upload file: {ex.Message}", isError: true);
            }
            finally
            {
                IsLoadingModalOpen = false;
            }
        }

        private async Task ExecuteExportFileAsync(ClientFileModel? file)
        {
            if (file == null) return;

            var baseName = Path.GetFileNameWithoutExtension(file.FileName);
            var isCsv = file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

            var sfd = new SaveFileDialog
            {
                Title = $"Export Updated Leads for {file.ClientName}",
                FileName = $"{baseName}_updated{(isCsv ? ".csv" : ".xlsx")}",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                FilterIndex = isCsv ? 2 : 1
            };

            if (sfd.ShowDialog() != true) return;

            var savePath = sfd.FileName;
            var isSaveCsv = savePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

            _loadingCts?.Dispose();
            _loadingCts = new System.Threading.CancellationTokenSource();
            var ct = _loadingCts.Token;

            LoadingModalTitle   = "Exporting Leads";
            LoadingModalMessage = $"Fetching updated leads for '{file.FileName}'...";
            LoadingModalDetail  = "Retrieving real-time dispositions, call counts, and agent notes...";
            IsLoadingModalOpen  = true;

            try
            {
                var (ok, msg, data) = await ApiService.Instance.GetClientFileLeadsAsync(file.Id, ct);
                if (!ok || data?.Leads == null)
                {
                    if (ct.IsCancellationRequested) return;
                    ShowBanner($"Export failed: {msg}", isError: true);
                    return;
                }

                var leads = data.Leads;
                LoadingModalMessage = $"Writing {leads.Count:N0} records to '{Path.GetFileName(savePath)}'...";
                LoadingModalDetail  = "Building formatted spreadsheet with updated CRM columns...";

                await Task.Run(() =>
                {
                    var headers = new[]
                    {
                        "Call Date", "Call Disposition", "Assigned Agent", "Notes", "# Of Call",
                        "Cell Number", "Business Name", "Title", "Contact Name", "Email",
                        "Address", "City", "County", "State", "Zip Code", "Fax",
                        "Annual Sales", "Number of Employees", "Website", "SIC Code",
                        "SIC Description", "NAICS Code", "NAICS Description", "Status"
                    };

                    if (isSaveCsv)
                    {
                        using var writer = new StreamWriter(savePath, false, System.Text.Encoding.UTF8);
                        string EscapeCsv(string? val)
                        {
                            if (val == null) return "";
                            if (val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
                                return $"\"{val.Replace("\"", "\"\"")}\"";
                            return val;
                        }

                        writer.WriteLine(string.Join(",", headers.Select(EscapeCsv)));
                        foreach (var lead in leads)
                        {
                            ct.ThrowIfCancellationRequested();
                            var rowVals = new[]
                            {
                                lead.CallDate, lead.CallDisposition, lead.AssignedAgent, lead.Notes, lead.CallCount.ToString(),
                                lead.CellNumber, lead.BusinessName, lead.Title, lead.ContactName, lead.Email,
                                lead.Address, lead.City, lead.County, lead.State, lead.ZipCode, lead.Fax,
                                lead.AnnualSales, lead.NumberOfEmployees, lead.Website, lead.SicCode,
                                lead.SicDescription, lead.NaicsCode, lead.NaicsDescription, lead.Status
                            };
                            writer.WriteLine(string.Join(",", rowVals.Select(EscapeCsv)));
                        }
                    }
                    else
                    {
                        using var workbook = new XLWorkbook();
                        var ws = workbook.Worksheets.Add("Updated Leads");

                        for (int col = 0; col < headers.Length; col++)
                        {
                            var cell = ws.Cell(1, col + 1);
                            cell.Value = headers[col];
                            cell.Style.Font.Bold = true;
                            cell.Style.Font.FontColor = XLColor.White;
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        int row = 2;
                        foreach (var lead in leads)
                        {
                            ct.ThrowIfCancellationRequested();
                            ws.Cell(row, 1).SetValue(lead.CallDate ?? string.Empty);
                            ws.Cell(row, 2).SetValue(lead.CallDisposition ?? string.Empty);
                            ws.Cell(row, 3).SetValue(lead.AssignedAgent ?? string.Empty);
                            ws.Cell(row, 4).SetValue(lead.Notes ?? string.Empty);
                            ws.Cell(row, 5).SetValue(lead.CallCount);
                            ws.Cell(row, 6).SetValue(lead.CellNumber ?? string.Empty);
                            ws.Cell(row, 7).SetValue(lead.BusinessName ?? string.Empty);
                            ws.Cell(row, 8).SetValue(lead.Title ?? string.Empty);
                            ws.Cell(row, 9).SetValue(lead.ContactName ?? string.Empty);
                            ws.Cell(row, 10).SetValue(lead.Email ?? string.Empty);
                            ws.Cell(row, 11).SetValue(lead.Address ?? string.Empty);
                            ws.Cell(row, 12).SetValue(lead.City ?? string.Empty);
                            ws.Cell(row, 13).SetValue(lead.County ?? string.Empty);
                            ws.Cell(row, 14).SetValue(lead.State ?? string.Empty);
                            ws.Cell(row, 15).SetValue(lead.ZipCode ?? string.Empty);
                            ws.Cell(row, 16).SetValue(lead.Fax ?? string.Empty);
                            ws.Cell(row, 17).SetValue(lead.AnnualSales ?? string.Empty);
                            ws.Cell(row, 18).SetValue(lead.NumberOfEmployees ?? string.Empty);
                            ws.Cell(row, 19).SetValue(lead.Website ?? string.Empty);
                            ws.Cell(row, 20).SetValue(lead.SicCode ?? string.Empty);
                            ws.Cell(row, 21).SetValue(lead.SicDescription ?? string.Empty);
                            ws.Cell(row, 22).SetValue(lead.NaicsCode ?? string.Empty);
                            ws.Cell(row, 23).SetValue(lead.NaicsDescription ?? string.Empty);
                            ws.Cell(row, 24).SetValue(lead.Status ?? string.Empty);
                            row++;
                        }

                        ws.Columns().AdjustToContents();
                        workbook.SaveAs(savePath);
                    }
                }, ct);

                ShowBanner($"✓ Exported {leads.Count:N0} updated leads to '{Path.GetFileName(savePath)}'.", isError: false);
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(savePath)) File.Delete(savePath); } catch { }
                ShowBanner("Export cancelled by user.", isError: false);
            }
            catch (Exception ex)
            {
                ShowBanner($"Export error: {ex.Message}", isError: true);
            }
            finally
            {
                IsLoadingModalOpen = false;
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
                var updatedClient = Clients.FirstOrDefault(c => c.Id == file.ClientId);
                if (updatedClient != null && updatedClient.IsExpanded)
                {
                    await LoadClientLeadsAsync(updatedClient);
                }
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

            // Ensure Zoom is running in background while leads are being viewed
            _ = Task.Run(() => ZoomPhoneService.EnsureZoomRunning());

            try
            {
                var (ok, msg, data) = await ApiService.Instance.GetClientFileLeadsAsync(file.Id);
                if (ok && data?.Leads != null)
                {
                    _allPreviewLeads = data.Leads;
                    var client = Clients.FirstOrDefault(c => c.Id == file.ClientId);
                    var agentNames = client?.AssignedAgentNames ?? new ObservableCollection<string> { "" };

                    foreach (var lead in _allPreviewLeads)
                    {
                        lead.AvailableAgentOptions = agentNames;
                        lead.OnFieldChanged = (l, field) => SyncLeadField(l, field);
                    }

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
                    (l.Email?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.AssignedAgent?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.CallDisposition?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
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

        private void ExecuteCallLead(LeadModel? lead)
        {
            if (lead == null) return;

            if (string.IsNullOrWhiteSpace(lead.CellNumber))
            {
                ShowBanner("Please provide a phone number before calling.", isError: true);
                return;
            }

            var (success, error) = ZoomPhoneService.Dial(lead.CellNumber);
            if (!success)
            {
                ShowBanner(error ?? "Could not initiate call via Zoom.", isError: true);
                return;
            }

            // Auto-increment the call count as recommended by manager
            lead.CallCount++;
            ShowBanner($"✓ Calling {lead.CellNumber} via Zoom Phone. Call count updated to {lead.CallCount}.", isError: false);
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

            var agentNames = new ObservableCollection<string> { "" };
            if (client.AssignedAgents != null)
            {
                foreach (var agent in client.AssignedAgents)
                {
                    if (!string.IsNullOrWhiteSpace(agent.FullName))
                    {
                        agentNames.Add(agent.FullName);
                    }
                }
            }
            client.AssignedAgentNames = agentNames;

            if (client.Leads != null)
            {
                foreach (var lead in client.Leads)
                {
                    lead.AvailableAgentOptions = client.AssignedAgentNames;
                }
            }
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
                // Pass 1: Exact matches first
                foreach (var a in aliases)
                {
                    for (int i = 0; i < headers.Count; i++)
                    {
                        if (string.Equals(headers[i], a, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                }

                // Pass 2: Substring matches (excluding generic collisions)
                foreach (var a in aliases)
                {
                    // Generic words like "name" should not loosely match "business name"
                    if (a.Equals("name", StringComparison.OrdinalIgnoreCase))
                        continue;

                    for (int i = 0; i < headers.Count; i++)
                    {
                        var h = headers[i];
                        if (a.Contains("contact") && (h.Contains("business") || h.Contains("company")))
                            continue;

                        if (h.Contains(a, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                }
                return -1;
            }

            int idxDate     = GetCol("date", "call date");
            int idxDisp     = GetCol("call disposition", "call composition", "disposition", "composition");
            int idxAgent    = GetCol("agent assigned", "assigned agent", "agent");
            int idxNotes    = GetCol("notes", "note");
            int idxCalls    = GetCol("of cal", "# of cal", "# of call", "# of calls", "call count", "number of call", "calls");
            int idxBusiness = GetCol("business name", "company name", "business", "company");
            int idxAddress  = GetCol("address", "street");
            int idxCity     = GetCol("city");
            int idxCounty   = GetCol("county");
            int idxState    = GetCol("state");
            int idxZip      = GetCol("zip code", "zip", "postal");
            int idxCell     = GetCol("cell number", "cell", "phone", "telephone", "mobile");
            int idxEmail    = GetCol("email", "e-mail");
            int idxFax      = GetCol("fax");
            int idxSales    = GetCol("annual sales", "sales");
            int idxEmp      = GetCol("number of employee", "employees", "number of employees");
            int idxTitle    = GetCol("title", "job title");
            int idxContact  = GetCol("contact name", "contact person", "contact", "contact_name", "full name", "person name", "contactname", "name");
            int idxWeb      = GetCol("website", "web", "url", "site");
            int idxSicCode  = GetCol("sic code", "sic");
            int idxSicDesc  = GetCol("sic description", "sic desc");
            int idxNaics    = GetCol("naics code", "naics");
            int idxNaicsD   = GetCol("naics description", "naics desc");

            if (idxContact == idxBusiness)
            {
                idxContact = -1;
            }

            string SafeGet(string[] parts, int idx, int fallback, string def = "")
            {
                int t = idx >= 0 ? idx : fallback;
                if (t >= 0 && t < parts.Length)
                { var v = parts[t].Trim(); return string.IsNullOrEmpty(v) ? def : v; }
                return def;
            }

            string CleanContactName(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
                var trimmed = raw.Trim();
                if (trimmed.Equals("contact name", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("contact", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("contact_name", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }
                return trimmed;
            }

            int rowId = 1;
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = SplitCsvLine(line);

                var bName = SafeGet(parts, idxBusiness, 0, "Unnamed Business");
                if (bName.Equals("business name", StringComparison.OrdinalIgnoreCase) ||
                    bName.Equals("business", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip repeated header row
                }

                var rawDate = SafeGet(parts, idxDate, -1);
                var dateVal = !string.IsNullOrWhiteSpace(rawDate) ? rawDate : string.Empty;
                var rawCalls = SafeGet(parts, idxCalls, -1);
                int callCount = int.TryParse(rawCalls, out var cc) && cc > 0 ? cc : 1;

                var cName = CleanContactName(SafeGet(parts, idxContact, 11));

                records.Add(new LeadModel
                {
                    Id               = rowId++,
                    CallDate         = dateVal,
                    CallDisposition  = SafeGet(parts, idxDisp, -1),
                    AssignedAgent    = SafeGet(parts, idxAgent, -1),
                    Notes            = SafeGet(parts, idxNotes, -1),
                    CallCount        = callCount,
                    BusinessName     = bName,
                    Address          = SafeGet(parts, idxAddress,  1),
                    City             = SafeGet(parts, idxCity,     2),
                    County           = SafeGet(parts, idxCounty,   3),
                    State            = SafeGet(parts, idxState,    4, "NE"),
                    ZipCode          = SafeGet(parts, idxZip,      5),
                    CellNumber       = SafeGet(parts, idxCell,     6),
                    Email            = SafeGet(parts, idxEmail,    -1),
                    Fax              = SafeGet(parts, idxFax,      7),
                    AnnualSales      = SafeGet(parts, idxSales,    8, "Less than 500,000"),
                    NumberOfEmployees= SafeGet(parts, idxEmp,      9, "1 to 4"),
                    Title            = SafeGet(parts, idxTitle,   10),
                    ContactName      = cName,
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
                // Pass 1: Exact matches first
                foreach (var a in aliases)
                {
                    for (int i = 0; i < headers.Count; i++)
                    {
                        if (string.Equals(headers[i], a, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                }

                // Pass 2: Substring matches (excluding generic collisions)
                foreach (var a in aliases)
                {
                    // Generic words like "name" should not loosely match "business name"
                    if (a.Equals("name", StringComparison.OrdinalIgnoreCase))
                        continue;

                    for (int i = 0; i < headers.Count; i++)
                    {
                        var h = headers[i];
                        if (a.Contains("contact") && (h.Contains("business") || h.Contains("company")))
                            continue;

                        if (h.Contains(a, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                }
                return -1;
            }

            int idxDate     = GetCol("date", "call date");
            int idxDisp     = GetCol("call disposition", "call composition", "disposition", "composition");
            int idxAgent    = GetCol("agent assigned", "assigned agent", "agent");
            int idxNotes    = GetCol("notes", "note");
            int idxCalls    = GetCol("of cal", "# of cal", "# of call", "# of calls", "call count", "number of call", "calls");
            int idxBusiness = GetCol("business name", "company name", "business", "company");
            int idxAddress  = GetCol("address", "street");
            int idxCity     = GetCol("city");
            int idxCounty   = GetCol("county");
            int idxState    = GetCol("state");
            int idxZip      = GetCol("zip code", "zip", "postal");
            int idxCell     = GetCol("cell number", "cell", "phone", "telephone", "mobile");
            int idxEmail    = GetCol("email", "e-mail");
            int idxFax      = GetCol("fax");
            int idxSales    = GetCol("annual sales", "sales");
            int idxEmp      = GetCol("number of employee", "employees", "number of employees");
            int idxTitle    = GetCol("title", "job title");
            int idxContact  = GetCol("contact name", "contact person", "contact", "contact_name", "full name", "person name", "contactname", "name");
            int idxWeb      = GetCol("website", "web", "url", "site");
            int idxSicCode  = GetCol("sic code", "sic");
            int idxSicDesc  = GetCol("sic description", "sic desc");
            int idxNaics    = GetCol("naics code", "naics");
            int idxNaicsD   = GetCol("naics description", "naics desc");

            if (idxContact == idxBusiness)
            {
                idxContact = -1;
            }

            string SafeGet(string[] parts, int idx, int fallback, string def = "")
            {
                int t = idx >= 0 ? idx : fallback;
                if (t >= 0 && t < parts.Length)
                { var v = parts[t].Trim(); return string.IsNullOrEmpty(v) ? def : v; }
                return def;
            }

            string CleanContactName(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
                var trimmed = raw.Trim();
                if (trimmed.Equals("contact name", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("contact", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("contact_name", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }
                return trimmed;
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

                var bName = SafeGet(parts, idxBusiness, 0, "Unnamed Business");
                if (bName.Equals("business name", StringComparison.OrdinalIgnoreCase) ||
                    bName.Equals("business", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip repeated header row
                }

                var rawDate = SafeGet(parts, idxDate, -1);
                var dateVal = !string.IsNullOrWhiteSpace(rawDate) ? rawDate : string.Empty;
                var rawCalls = SafeGet(parts, idxCalls, -1);
                int callCount = int.TryParse(rawCalls, out var cc) && cc > 0 ? cc : 1;
                var cName = CleanContactName(SafeGet(parts, idxContact, 11));

                records.Add(new LeadModel
                {
                    Id               = rowId++,
                    CallDate         = dateVal,
                    CallDisposition  = SafeGet(parts, idxDisp, -1),
                    AssignedAgent    = SafeGet(parts, idxAgent, -1),
                    Notes            = SafeGet(parts, idxNotes, -1),
                    CallCount        = callCount,
                    BusinessName     = bName,
                    Address          = SafeGet(parts, idxAddress,  1),
                    City             = SafeGet(parts, idxCity,     2),
                    County           = SafeGet(parts, idxCounty,   3),
                    State            = SafeGet(parts, idxState,    4, "NE"),
                    ZipCode          = SafeGet(parts, idxZip,      5),
                    CellNumber       = SafeGet(parts, idxCell,     6),
                    Email            = SafeGet(parts, idxEmail,    -1),
                    Fax              = SafeGet(parts, idxFax,      7),
                    AnnualSales      = SafeGet(parts, idxSales,    8, "Less than 500,000"),
                    NumberOfEmployees= SafeGet(parts, idxEmp,      9, "1 to 4"),
                    Title            = SafeGet(parts, idxTitle,   10),
                    ContactName      = cName,
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
