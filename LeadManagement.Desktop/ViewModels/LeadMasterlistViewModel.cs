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
        private string _selectedCity = "All Cities";
        private string _selectedIndustry = "All Industries";
        private string _selectedStatus = "All Statuses";
        private bool _isLoading = false;

        public LeadMasterlistViewModel()
        {
            ClearFiltersCommand = new RelayCommand(ExecuteClearFilters);
            RefreshCommand = new RelayCommand(async () => await LoadLeadsAsync());
            ExportCommand = new RelayCommand(ExecuteExport);

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

        public ObservableCollection<string> Cities { get; } = new();
        public ObservableCollection<string> Industries { get; } = new();
        public ObservableCollection<string> Statuses { get; } = new();

        public string SelectedCity
        {
            get => _selectedCity;
            set
            {
                if (SetProperty(ref _selectedCity, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string SelectedIndustry
        {
            get => _selectedIndustry;
            set
            {
                if (SetProperty(ref _selectedIndustry, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                {
                    ApplyFilter();
                }
            }
        }

        public int TotalCount => _allLeadsMaster.Count;
        public int FilteredCount => FilteredLeads.Count;

        public string ShowingCountText
        {
            get
            {
                if (IsLoading) return "Loading...";
                if (TotalCount == 0) return "0 Leads";
                if (FilteredCount == TotalCount) return $"{TotalCount} Leads";
                return $"Showing {FilteredCount} of {TotalCount} Leads";
            }
        }

        public ICommand ClearFiltersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }

        /// <summary>
        /// Fetches all leads directly from the MySQL database via the PHP REST API.
        /// </summary>
        public async Task LoadLeadsAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                var (success, _, data) = await ApiService.Instance.GetLeadsAsync(limit: 50000);

                if (success && data?.Leads != null)
                {
                    _allLeadsMaster.Clear();
                    _allLeadsMaster.AddRange(data.Leads);

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
            }
        }

        private void ExecuteClearFilters()
        {
            _searchText = string.Empty;
            _selectedCity = "All Cities";
            _selectedIndustry = "All Industries";
            _selectedStatus = "All Statuses";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedCity));
            OnPropertyChanged(nameof(SelectedIndustry));
            OnPropertyChanged(nameof(SelectedStatus));

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
                    (l.ZipCode?.Contains(term) ?? false) ||
                    (l.SicCode?.Contains(term) ?? false) ||
                    (l.SicDescription?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.NaicsCode?.Contains(term) ?? false) ||
                    (l.NaicsDescription?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Title?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Website?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Address?.ToLowerInvariant().Contains(term) ?? false));
            }

            // 2. City Filter
            if (!string.IsNullOrWhiteSpace(SelectedCity) && SelectedCity != "All Cities")
            {
                query = query.Where(l => string.Equals(l.City, SelectedCity, StringComparison.OrdinalIgnoreCase));
            }

            // 3. Industry / SIC Filter
            if (!string.IsNullOrWhiteSpace(SelectedIndustry) && SelectedIndustry != "All Industries")
            {
                query = query.Where(l => string.Equals(l.SicDescription, SelectedIndustry, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Status Filter
            if (!string.IsNullOrWhiteSpace(SelectedStatus) && SelectedStatus != "All Statuses")
            {
                query = query.Where(l => string.Equals(l.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase));
            }

            FilteredLeads = new ObservableCollection<LeadModel>(query);
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ShowingCountText));

            // Select first lead by default if current selection is not in filtered list
            if (SelectedLead == null || !FilteredLeads.Contains(SelectedLead))
            {
                SelectedLead = FilteredLeads.FirstOrDefault();
            }
        }

        private void PopulateFilterLists()
        {
            // Populate Distinct Cities
            var currentCity = SelectedCity;
            Cities.Clear();
            Cities.Add("All Cities");
            foreach (var city in _allLeadsMaster.Select(l => l.City).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c))
            {
                Cities.Add(city);
            }
            if (Cities.Contains(currentCity)) SelectedCity = currentCity;

            // Populate Distinct Industries (SIC Descriptions)
            var currentInd = SelectedIndustry;
            Industries.Clear();
            Industries.Add("All Industries");
            foreach (var ind in _allLeadsMaster.Select(l => l.SicDescription).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().OrderBy(i => i))
            {
                Industries.Add(ind);
            }
            if (Industries.Contains(currentInd)) SelectedIndustry = currentInd;

            // Populate Statuses
            var currentStat = SelectedStatus;
            Statuses.Clear();
            Statuses.Add("All Statuses");
            Statuses.Add("New Lead");
            Statuses.Add("Qualified");
            Statuses.Add("Follow-up");
            Statuses.Add("Contacted");
            foreach (var s in _allLeadsMaster.Select(l => l.Status).Where(st => !string.IsNullOrWhiteSpace(st)).Distinct())
            {
                if (!Statuses.Contains(s)) Statuses.Add(s);
            }
            if (Statuses.Contains(currentStat)) SelectedStatus = currentStat;
        }

        /// <summary>
        /// Exports all currently filtered leads to a genuine Excel (.xlsx) workbook with bold headers.
        /// </summary>
        private void ExecuteExport()
        {
            var leadsToExport = FilteredLeads.ToList();
            if (leadsToExport.Count == 0)
            {
                MessageBox.Show("There are no leads in the table to export.", "Export Leads", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Leads to Excel",
                FileName = $"Leads_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Lead Masterlist");

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

                    // Format Header Row: Bold text, dark background, centered, and padded height
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
                        $"Successfully exported {leadsToExport.Count} leads to Excel!\n\nSaved to: {dialog.FileName}",
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
