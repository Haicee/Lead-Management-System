using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ClosedXML.Excel;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.Services;
using Microsoft.Win32;

namespace LeadManagement.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for the Agent Portal.
    /// Exclusively displays clients and lead files assigned to the logged-in agent,
    /// with folder-based organization, lead preview with live call dialer, and file export.
    /// </summary>
    public class UserDashboardViewModel : BaseViewModel
    {
        private string _agentName = "Agent";
        private string _currentUserView = "MyLeads";
        private bool _isSidebarCollapsed = false;

        private ObservableCollection<ClientModel> _assignedClients = new();
        private bool _isLoading;
        private string _statusBannerMessage = string.Empty;
        private bool _isBannerVisible;
        private bool _isBannerError;

        // Preview State
        private bool _isPreviewOpen;
        private string _previewTitle = string.Empty;
        private string _previewSubtitle = string.Empty;
        private string _previewSearchText = string.Empty;
        private bool _isLoadingPreview;
        private ObservableCollection<LeadModel> _filteredPreviewLeads = new();
        private List<LeadModel> _allPreviewLeads = new();

        // Loading Modal State (for Export with cancel support)
        private bool _isLoadingModalOpen;
        private string _loadingModalTitle = string.Empty;
        private string _loadingModalMessage = string.Empty;
        private string _loadingModalDetail = string.Empty;
        private CancellationTokenSource? _loadingCts;

        public UserDashboardViewModel()
        {
            NavigateUserCommand       = new RelayCommand(param =>
            {
                if (param is string dest && !string.IsNullOrWhiteSpace(dest))
                {
                    CurrentUserView = dest;
                }
            });

            ToggleSidebarCommand      = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);
            RefreshCommand            = new RelayCommand(async () => await LoadAssignedClientsAsync());
            ToggleExpandCommand       = new RelayCommand<ClientModel>(ExecuteToggleExpand);
            PreviewFileCommand        = new RelayCommand<ClientFileModel>(async f => await ExecutePreviewFileAsync(f));
            ClosePreviewCommand       = new RelayCommand(ExecuteClosePreview);
            CallLeadCommand           = new RelayCommand<LeadModel>(ExecuteCallLead);
            ExportFileCommand         = new RelayCommand<ClientFileModel>(async f => await ExecuteExportFileAsync(f));
            CancelLoadingModalCommand = new RelayCommand(ExecuteCancelLoadingModal);
            DismissBannerCommand      = new RelayCommand(() => IsBannerVisible = false);

            // Pre-warm Zoom Workplace in background
            _ = Task.Run(() => ZoomPhoneService.EnsureZoomRunning());
        }

        // ----------------------------------------------------------------
        // Properties
        // ----------------------------------------------------------------
        public string AgentName
        {
            get => _agentName;
            set => SetProperty(ref _agentName, value);
        }

        public string CurrentUserView
        {
            get => _currentUserView;
            set => SetProperty(ref _currentUserView, value);
        }

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

        public ObservableCollection<ClientModel> AssignedClients
        {
            get => _assignedClients;
            set
            {
                if (SetProperty(ref _assignedClients, value))
                {
                    OnPropertyChanged(nameof(HasNoAssignedClients));
                    OnPropertyChanged(nameof(TotalAssignedLeadsCount));
                }
            }
        }

        public bool HasNoAssignedClients => _assignedClients.Count == 0;

        public int TotalAssignedLeadsCount => _assignedClients.Sum(c => c.TotalLeads);

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

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

        // Preview properties
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

        // Loading modal properties
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
        public ICommand NavigateUserCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ToggleExpandCommand { get; }
        public ICommand PreviewFileCommand { get; }
        public ICommand ClosePreviewCommand { get; }
        public ICommand CallLeadCommand { get; }
        public ICommand ExportFileCommand { get; }
        public ICommand CancelLoadingModalCommand { get; }
        public ICommand DismissBannerCommand { get; }

        // ----------------------------------------------------------------
        // Data Loading
        // ----------------------------------------------------------------
        public async Task LoadAssignedClientsAsync()
        {
            IsLoading = true;
            IsBannerVisible = false;

            try
            {
                var currentUser = ApiService.Instance.CurrentUser;
                var currentUserId = currentUser?.Id ?? 0;

                var (ok, msg, allClients) = await ApiService.Instance.GetClientsAsync(currentUserId > 0 ? currentUserId : null);
                if (ok && allClients != null)
                {
                    // Filter exclusively to clients assigned to this agent
                    var assigned = allClients
                        .Where(c => c.AssignedAgents != null && c.AssignedAgents.Any(a => a.Id == currentUserId))
                        .ToList();

                    // If expanded state previously existed, preserve it
                    var prevExpandedIds = new HashSet<int>(_assignedClients.Where(c => c.IsExpanded).Select(c => c.Id));
                    foreach (var c in assigned)
                    {
                        if (prevExpandedIds.Contains(c.Id) || assigned.Count == 1)
                        {
                            c.IsExpanded = true;
                        }
                    }

                    AssignedClients = new ObservableCollection<ClientModel>(assigned);
                }
                else
                {
                    ShowBanner($"Failed to load assigned clients: {msg}", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowBanner($"Error loading assigned clients: {ex.Message}", isError: true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteToggleExpand(ClientModel? client)
        {
            if (client == null) return;
            client.IsExpanded = !client.IsExpanded;
        }

        // ----------------------------------------------------------------
        // Preview File Leads
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
                    var client = AssignedClients.FirstOrDefault(c => c.Id == file.ClientId);
                    var agentNames = client?.AssignedAgentNames ?? new ObservableCollection<string> { AgentName };

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

        // ----------------------------------------------------------------
        // Zoom Phone Click-to-Dial & Auto-Incrementing Call Count
        // ----------------------------------------------------------------
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

            // Auto-increment the call count
            lead.CallCount++;
            ShowBanner($"✓ Calling {lead.CellNumber} via Zoom Phone. Call count updated to {lead.CallCount}.", isError: false);
        }

        // ----------------------------------------------------------------
        // Live Database Sync on Lead Field Edit
        // ----------------------------------------------------------------
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
                    // Soft fail in background
                }
            });
        }

        // ----------------------------------------------------------------
        // Export Updated Leads
        // ----------------------------------------------------------------
        private void ExecuteCancelLoadingModal()
        {
            try
            {
                _loadingCts?.Cancel();
            }
            catch { }
            IsLoadingModalOpen = false;
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
            _loadingCts = new CancellationTokenSource();
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
                        using var writer = new StreamWriter(savePath, false, Encoding.UTF8);
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

        private void ShowBanner(string message, bool isError)
        {
            StatusBannerMessage = message;
            IsBannerError       = isError;
            IsBannerVisible     = true;
        }
    }
}
