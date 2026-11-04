using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class LeadMasterlistViewModel : BaseViewModel
    {
        private readonly List<LeadModel> _allLeadsMaster = new();
        private ObservableCollection<LeadModel> _filteredLeads = new();
        private LeadModel? _selectedLead;
        private string _searchText = string.Empty;
        private bool _isLoading = false;

        public LeadMasterlistViewModel()
        {
            ClearFiltersCommand = new RelayCommand(ExecuteClearFilters);
            RefreshCommand = new RelayCommand(async () => await LoadLeadsAsync());
            ExportFilteredCommand = new RelayCommand(ExecuteExportFiltered);
            ExportAllCommand = new RelayCommand(ExecuteExportAll);
            ExportCommand = new RelayCommand(ExecuteExportFiltered); // Default export action

            // Fetch real leads from MySQL database on startup
            _ = LoadLeadsAsync();
        }

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
                    ApplyFilter();
                }
            }
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

        public int TotalCount => _allLeadsMaster.Count;
        public int FilteredCount => FilteredLeads.Count;

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

        public string ExportAllButtonText => $"⤓ Export All";

        public ICommand ClearFiltersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportFilteredCommand { get; }
        public ICommand ExportAllCommand { get; }
        public ICommand ExportCommand { get; }

        // ─── Helper: Extract selected names as a HashSet for fast OR matching ─────

        private static HashSet<string> GetSelected(ObservableCollection<FilterItem> items) =>
            new(items.Where(i => i.IsSelected).Select(i => i.Name), StringComparer.OrdinalIgnoreCase);

        // ─── Subscribe to FilterItem changes so filter re-runs on every checkbox toggle ─

        private void SubscribeToItems(ObservableCollection<FilterItem> items)
        {
            foreach (var item in items)
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(FilterItem.IsSelected))
                        ApplyFilter();
                };
        }

        /// <summary>
        /// Fetches all leads directly from the MySQL database via the PHP REST API.
        /// Passes limit=0 to retrieve the complete dataset without truncation.
        /// </summary>
        public async Task LoadLeadsAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                var (success, _, data) = await ApiService.Instance.GetLeadsAsync(limit: 0);

                if (success && data?.Leads != null)
                {
                    _allLeadsMaster.Clear();
                    _allLeadsMaster.AddRange(data.Leads);

                    int withWeb = data.Leads.Count(l => !string.IsNullOrWhiteSpace(l.Website));
                    var msg = $"[LMS Masterlist] Fetched {data.Leads.Count} leads from API. {withWeb} have website.";
                    Console.WriteLine(msg);
                    System.Diagnostics.Debug.WriteLine(msg);

                    PopulateFilterLists();
                    ApplyFilter();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load leads from database: {ex.Message}", "Database Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(FilteredCount));
                OnPropertyChanged(nameof(ShowingCountText));
                OnPropertyChanged(nameof(HasActiveFilters));
                OnPropertyChanged(nameof(ExportFilteredButtonText));
                OnPropertyChanged(nameof(ExportAllButtonText));
            }
        }

        private void ExecuteClearFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            // Deselect all items in every filter collection
            foreach (var item in CityItems) item.IsSelected = false;
            foreach (var item in CountyItems) item.IsSelected = false;
            foreach (var item in StateItems) item.IsSelected = false;
            foreach (var item in IndustryItems) item.IsSelected = false;
            foreach (var item in AnnualSalesItems) item.IsSelected = false;
            foreach (var item in NaicsCodeItems) item.IsSelected = false;
            foreach (var item in NaicsDescItems) item.IsSelected = false;
            foreach (var item in StatusItems) item.IsSelected = false;

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<LeadModel> query = _allLeadsMaster;

            // 1. Text Search Filter across multiple attributes
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim().ToLowerInvariant();
                query = query.Where(l =>
                    (l.BusinessName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.ContactName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.CellNumber?.Contains(term) ?? false) ||
                    (l.City?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.County?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.State?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.ZipCode?.Contains(term) ?? false) ||
                    (l.AnnualSales?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.SicCode?.Contains(term) ?? false) ||
                    (l.SicDescription?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.NaicsCode?.Contains(term) ?? false) ||
                    (l.NaicsDescription?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Title?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Website?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Address?.ToLowerInvariant().Contains(term) ?? false));
            }

            // 2. City Filter (OR within selected cities, skip if none selected)
            var selectedCities = GetSelected(CityItems);
            if (selectedCities.Count > 0)
                query = query.Where(l => selectedCities.Contains(l.City ?? string.Empty));

            // 3. County Filter (OR within selected counties)
            var selectedCounties = GetSelected(CountyItems);
            if (selectedCounties.Count > 0)
                query = query.Where(l => selectedCounties.Contains(l.County ?? string.Empty));

            // 4. State Filter (OR within selected states)
            var selectedStates = GetSelected(StateItems);
            if (selectedStates.Count > 0)
                query = query.Where(l => selectedStates.Contains(l.State ?? string.Empty));

            // 5. Industry / SIC Filter (OR within selected industries)
            var selectedIndustries = GetSelected(IndustryItems);
            if (selectedIndustries.Count > 0)
                query = query.Where(l => selectedIndustries.Contains(l.SicDescription ?? string.Empty));

            // 6. Annual Sales Filter (OR within selected sales ranges)
            var selectedSales = GetSelected(AnnualSalesItems);
            if (selectedSales.Count > 0)
                query = query.Where(l => selectedSales.Contains(l.AnnualSales ?? string.Empty));

            // 7. NAICS Code Filter (OR within selected codes)
            var selectedNaicsCodes = GetSelected(NaicsCodeItems);
            if (selectedNaicsCodes.Count > 0)
                query = query.Where(l => selectedNaicsCodes.Contains(l.NaicsCode ?? string.Empty));

            // 8. NAICS Description Filter (OR within selected descriptions)
            var selectedNaicsDescs = GetSelected(NaicsDescItems);
            if (selectedNaicsDescs.Count > 0)
                query = query.Where(l => selectedNaicsDescs.Contains(l.NaicsDescription ?? string.Empty));

            // 9. Status Filter (OR within selected statuses)
            var selectedStatuses = GetSelected(StatusItems);
            if (selectedStatuses.Count > 0)
                query = query.Where(l => selectedStatuses.Contains(l.Status ?? string.Empty));

            FilteredLeads = new ObservableCollection<LeadModel>(query);
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ShowingCountText));
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(ExportFilteredButtonText));
            OnPropertyChanged(nameof(ExportAllButtonText));

            // Keep selected lead if still in filtered list
            if (SelectedLead == null || !FilteredLeads.Contains(SelectedLead))
            {
                SelectedLead = FilteredLeads.FirstOrDefault();
            }
        }

        private void PopulateFilterLists()
        {
            // Helper: build FilterItem list, subscribe, and load into collection
            void Populate(ObservableCollection<FilterItem> collection, IEnumerable<string> values)
            {
                collection.Clear();
                foreach (var val in values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v))
                {
                    collection.Add(new FilterItem { Name = val });
                }
                SubscribeToItems(collection);
            }

            // 1. Cities
            Populate(CityItems, _allLeadsMaster.Select(l => l.City!));

            // 2. Counties
            Populate(CountyItems, _allLeadsMaster.Select(l => l.County!));

            // 3. States
            Populate(StateItems, _allLeadsMaster.Select(l => l.State!));

            // 4. Industries (SIC Descriptions)
            Populate(IndustryItems, _allLeadsMaster.Select(l => l.SicDescription!));

            // 5. Annual Sales
            Populate(AnnualSalesItems, _allLeadsMaster.Select(l => l.AnnualSales!));

            // 6. NAICS Codes
            Populate(NaicsCodeItems, _allLeadsMaster.Select(l => l.NaicsCode!));

            // 7. NAICS Descriptions
            Populate(NaicsDescItems, _allLeadsMaster.Select(l => l.NaicsDescription!));

            // 8. Statuses (seed known values first, then add any from data)
            var knownStatuses = new[] { "New Lead", "Qualified", "Follow-up", "Contacted" };
            var dataStatuses = _allLeadsMaster.Select(l => l.Status ?? string.Empty)
                                              .Where(s => !string.IsNullOrWhiteSpace(s))
                                              .Distinct();
            var allStatuses = knownStatuses.Union(dataStatuses, StringComparer.OrdinalIgnoreCase)
                                           .OrderBy(s => s);
            StatusItems.Clear();
            foreach (var s in allStatuses)
                StatusItems.Add(new FilterItem { Name = s });
            SubscribeToItems(StatusItems);
        }

        /// <summary>
        /// Exports only the currently filtered leads to an Excel workbook.
        /// </summary>
        private void ExecuteExportFiltered()
        {
            var leadsToExport = FilteredLeads.ToList();
            var title = HasActiveFilters ? "Filtered Leads" : "Lead Masterlist";
            var filePrefix = HasActiveFilters ? "Filtered_Leads" : "Lead_Masterlist";
            ExportLeadsToExcel(leadsToExport, filePrefix, title);
        }

        /// <summary>
        /// Exports all leads in the database without any active filters applied.
        /// </summary>
        private void ExecuteExportAll()
        {
            var leadsToExport = _allLeadsMaster.ToList();
            ExportLeadsToExcel(leadsToExport, "All_Leads_Masterlist", "Complete Lead Masterlist");
        }

        /// <summary>
        /// Shared Excel exporter using ClosedXML with formatted headers and auto-adjusted columns.
        /// </summary>
        private void ExportLeadsToExcel(List<LeadModel> leadsToExport, string filePrefix, string sheetTitle)
        {
            if (leadsToExport.Count == 0)
            {
                MessageBox.Show("There are no leads matching your selection to export.", "Export Leads", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = $"Export {sheetTitle} to Excel",
                FileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Leads");

                    // 1. Column Header Names (All 18 table columns + batch metadata)
                    string[] headers = new[]
                    {
                        "STATUS",
                        "BUSINESS NAME",
                        "ADDRESS",
                        "CITY",
                        "COUNTY",
                        "STATE",
                        "ZIP CODE",
                        "CELL NUMBER",
                        "FAX",
                        "ANNUAL SALES",
                        "NUMBER OF EMPLOYEE",
                        "TITLE",
                        "CONTACT NAME",
                        "WEBSITE",
                        "SIC CODE",
                        "SIC DESCRIPTION",
                        "NAICS CODE",
                        "NAICS DESCRIPTION",
                        "BATCH NAME"
                    };

                    // Format Header Row: Bold text, dark navy background, centered
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B"); // Navy branding
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }
                    worksheet.Row(1).Height = 26;

                    // 2. Data Rows
                    int row = 2;
                    foreach (var lead in leadsToExport)
                    {
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

                        // Subtle alternating zebra fill for readability
                        if (row % 2 == 1)
                        {
                            worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                        }
                        worksheet.Row(row).Height = 20;
                        row++;
                    }

                    // Auto-fit all column widths based on contents
                    worksheet.Columns().AdjustToContents();

                    workbook.SaveAs(dialog.FileName);

                    MessageBox.Show(
                        $"Successfully exported {leadsToExport.Count:N0} leads to Excel!\n\nSaved to: {dialog.FileName}",
                        "Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to export leads: {ex.Message}",
                        "Export Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }
}
