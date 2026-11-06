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

        public ObservableCollection<FilterItem> CityItems { get; } = new();
        public ObservableCollection<FilterItem> CountyItems { get; } = new();
        public ObservableCollection<FilterItem> StateItems { get; } = new();
        public ObservableCollection<FilterItem> IndustryItems { get; } = new();
        public ObservableCollection<FilterItem> AnnualSalesItems { get; } = new();
        public ObservableCollection<FilterItem> NaicsCodeItems { get; } = new();
        public ObservableCollection<FilterItem> NaicsDescItems { get; } = new();
        public ObservableCollection<FilterItem> StatusItems { get; } = new();

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
            CityItems.Any(i => i.IsSelected) ||
            CountyItems.Any(i => i.IsSelected) ||
            StateItems.Any(i => i.IsSelected) ||
            IndustryItems.Any(i => i.IsSelected) ||
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
            var cities = GetSelected(CityItems);
            var counties = GetSelected(CountyItems);
            var states = GetSelected(StateItems);
            var statuses = GetSelected(StatusItems);
            var industries = GetSelected(IndustryItems);
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
                Cities = cities.Count > 0 ? cities : null,
                Counties = counties.Count > 0 ? counties : null,
                States = states.Count > 0 ? states : null,
                Statuses = statuses.Count > 0 ? statuses : null,
                Industries = industries.Count > 0 ? industries : null,
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
        }

        private void ExecuteClearFilters()
        {
            _isPopulatingFilters = true;
            try
            {
                _searchText = string.Empty;
                OnPropertyChanged(nameof(SearchText));

                foreach (var item in CityItems) item.IsSelected = false;
                foreach (var item in CountyItems) item.IsSelected = false;
                foreach (var item in StateItems) item.IsSelected = false;
                foreach (var item in IndustryItems) item.IsSelected = false;
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
                LoadItems(IndustryItems, filters.Industries);
                LoadItems(AnnualSalesItems, filters.AnnualSales);
                LoadItems(NaicsCodeItems, filters.NaicsCodes);
                LoadItems(NaicsDescItems, filters.NaicsDescriptions);

                var knownStatuses = new[] { "New Lead", "Qualified", "Follow-up", "Contacted" };
                var mergedStatuses = knownStatuses.Union(filters.Statuses ?? new List<string>(), StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
                LoadItems(StatusItems, mergedStatuses);
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
