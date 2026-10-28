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
    public class ImportHistoryViewModel : BaseViewModel
    {
        private readonly List<ImportBatchModel> _allBatches = new();
        private ObservableCollection<ImportBatchModel> _filteredBatches = new();
        private ImportBatchModel? _selectedBatch;
        private string _searchText = string.Empty;
        private bool _isLoading = false;

        // Preview Section State
        private bool _isPreviewOpen = false;
        private ImportBatchModel? _previewBatch;
        private ObservableCollection<LeadModel> _previewLeads = new();
        private ObservableCollection<LeadModel> _filteredPreviewLeads = new();
        private readonly List<LeadModel> _allPreviewLeads = new();
        private string _previewSearchText = string.Empty;
        private bool _isLoadingPreview = false;

        public ImportHistoryViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadBatchesAsync());
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            PreviewBatchCommand = new RelayCommand(async (param) => await ExecutePreviewBatchAsync(param));
            ClosePreviewCommand = new RelayCommand(ExecuteClosePreview);
            ExportBatchCommand = new RelayCommand(async (param) => await ExecuteExportBatchAsync(param));
            DeleteBatchCommand = new RelayCommand(async (param) => await ExecuteDeleteBatchAsync(param));

            // Load batches from the database
            _ = LoadBatchesAsync();
        }

        // --- Batch Master List Properties ---

        public ObservableCollection<ImportBatchModel> FilteredBatches
        {
            get => _filteredBatches;
            set => SetProperty(ref _filteredBatches, value);
        }

        public ImportBatchModel? SelectedBatch
        {
            get => _selectedBatch;
            set => SetProperty(ref _selectedBatch, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyBatchFilter();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(ShowingBatchesText));
                }
            }
        }

        public int TotalBatches => _allBatches.Count;
        public int TotalLeadsImported => _allBatches.Sum(b => b.TotalLeads);

        public string ShowingBatchesText
        {
            get
            {
                if (IsLoading) return "Loading batches...";
                if (TotalBatches == 0) return "No Batches Found";
                if (FilteredBatches.Count == TotalBatches)
                    return $"{TotalBatches} {(TotalBatches == 1 ? "Batch" : "Batches")} • {TotalLeadsImported:N0} Total Leads";
                return $"Showing {FilteredBatches.Count} of {TotalBatches} Batches";
            }
        }

        // --- Preview Section Properties ---

        public bool IsPreviewOpen
        {
            get => _isPreviewOpen;
            set => SetProperty(ref _isPreviewOpen, value);
        }

        public ImportBatchModel? PreviewBatch
        {
            get => _previewBatch;
            set
            {
                if (SetProperty(ref _previewBatch, value))
                {
                    OnPropertyChanged(nameof(PreviewHeaderTitle));
                }
            }
        }

        public string PreviewHeaderTitle =>
            PreviewBatch != null ? $"Batch #{PreviewBatch.Id}: {PreviewBatch.BatchName}" : "Batch Leads Preview";

        public ObservableCollection<LeadModel> FilteredPreviewLeads
        {
            get => _filteredPreviewLeads;
            set => SetProperty(ref _filteredPreviewLeads, value);
        }

        public string PreviewSearchText
        {
            get => _previewSearchText;
            set
            {
                if (SetProperty(ref _previewSearchText, value))
                {
                    ApplyPreviewFilter();
                }
            }
        }

        public bool IsLoadingPreview
        {
            get => _isLoadingPreview;
            set
            {
                if (SetProperty(ref _isLoadingPreview, value))
                {
                    OnPropertyChanged(nameof(PreviewRowsText));
                }
            }
        }

        public string PreviewRowsText
        {
            get
            {
                if (IsLoadingPreview) return "Loading batch leads...";
                int total = _allPreviewLeads.Count;
                if (total == 0) return "No Records";
                if (FilteredPreviewLeads.Count == total) return $"{total:N0} Records";
                return $"Showing {FilteredPreviewLeads.Count:N0} of {total:N0} Records";
            }
        }

        // --- Commands ---

        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand PreviewBatchCommand { get; }
        public ICommand ClosePreviewCommand { get; }
        public ICommand ExportBatchCommand { get; }
        public ICommand DeleteBatchCommand { get; }

        /// <summary>
        /// Fetches all import batches from the database.
        /// </summary>
        public async Task LoadBatchesAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                var (success, _, batches) = await ApiService.Instance.GetBatchesAsync();

                if (success && batches != null)
                {
                    _allBatches.Clear();
                    _allBatches.AddRange(batches);

                    ApplyBatchFilter();

                    // If currently previewing a batch that no longer exists, close the preview
                    if (PreviewBatch != null && !_allBatches.Any(b => b.Id == PreviewBatch.Id))
                    {
                        ExecuteClosePreview();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load import batches: {ex.Message}", "Database Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(TotalBatches));
                OnPropertyChanged(nameof(TotalLeadsImported));
                OnPropertyChanged(nameof(ShowingBatchesText));
            }
        }

        private void ApplyBatchFilter()
        {
            IEnumerable<ImportBatchModel> query = _allBatches;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim().ToLowerInvariant();
                query = query.Where(b =>
                    (b.BatchName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (b.FileName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (b.ImportedByUsername?.ToLowerInvariant().Contains(term) ?? false) ||
                    (b.InitialStatus?.ToLowerInvariant().Contains(term) ?? false) ||
                    b.Id.ToString().Contains(term));
            }

            FilteredBatches = new ObservableCollection<ImportBatchModel>(query);
            OnPropertyChanged(nameof(ShowingBatchesText));
        }

        /// <summary>
        /// Opens the preview drawer and loads the leads for the selected batch.
        /// </summary>
        private async Task ExecutePreviewBatchAsync(object? parameter)
        {
            if (parameter is not ImportBatchModel batch) return;

            PreviewBatch = batch;
            IsPreviewOpen = true;
            IsLoadingPreview = true;
            PreviewSearchText = string.Empty;

            try
            {
                var (success, _, leads) = await ApiService.Instance.GetBatchLeadsAsync(batch.Id);

                _allPreviewLeads.Clear();
                if (success && leads != null)
                {
                    _allPreviewLeads.AddRange(leads);
                }

                ApplyPreviewFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load leads for batch '{batch.BatchName}': {ex.Message}", "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoadingPreview = false;
                OnPropertyChanged(nameof(PreviewRowsText));
            }
        }

        private void ExecuteClosePreview()
        {
            IsPreviewOpen = false;
            PreviewBatch = null;
            _allPreviewLeads.Clear();
            FilteredPreviewLeads = new ObservableCollection<LeadModel>();
            PreviewSearchText = string.Empty;
            OnPropertyChanged(nameof(PreviewRowsText));
        }

        private void ApplyPreviewFilter()
        {
            IEnumerable<LeadModel> query = _allPreviewLeads;

            if (!string.IsNullOrWhiteSpace(PreviewSearchText))
            {
                var term = PreviewSearchText.Trim().ToLowerInvariant();
                query = query.Where(l =>
                    (l.BusinessName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.ContactName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.CellNumber?.Contains(term) ?? false) ||
                    (l.City?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Address?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Title?.ToLowerInvariant().Contains(term) ?? false));
            }

            FilteredPreviewLeads = new ObservableCollection<LeadModel>(query);
            OnPropertyChanged(nameof(PreviewRowsText));
        }

        /// <summary>
        /// Exports only the specific batch and all its leads into an Excel (.xlsx) file with bold headers.
        /// </summary>
        private async Task ExecuteExportBatchAsync(object? parameter)
        {
            var batch = parameter as ImportBatchModel ?? PreviewBatch;
            if (batch == null) return;

            List<LeadModel> leadsToExport;

            // If this batch is currently loaded in preview, use those leads; otherwise fetch them
            if (PreviewBatch != null && PreviewBatch.Id == batch.Id && _allPreviewLeads.Count > 0)
            {
                leadsToExport = _allPreviewLeads.ToList();
            }
            else
            {
                var (success, msg, fetchedLeads) = await ApiService.Instance.GetBatchLeadsAsync(batch.Id);
                if (!success || fetchedLeads == null || fetchedLeads.Count == 0)
                {
                    MessageBox.Show($"Could not retrieve leads for batch '{batch.BatchName}' to export: {msg}", "Export Batch", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                leadsToExport = fetchedLeads;
            }

            // Clean filename
            string safeBatchName = string.Join("_", batch.BatchName.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(safeBatchName)) safeBatchName = $"Batch_{batch.Id}";

            var dialog = new SaveFileDialog
            {
                Title = $"Export Batch '{batch.BatchName}' to Excel",
                FileName = $"{safeBatchName}_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    string safeSheetName = safeBatchName.Length > 28 ? safeBatchName.Substring(0, 28) : safeBatchName;
                    var worksheet = workbook.Worksheets.Add(safeSheetName);

                    // 1. Column Header Names (All 18 columns + batch metadata)
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
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
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
                        worksheet.Cell(row, 19).SetValue(string.IsNullOrWhiteSpace(lead.BatchName) ? batch.BatchName : lead.BatchName);

                        if (row % 2 == 1)
                        {
                            worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                        }
                        worksheet.Row(row).Height = 20;
                        row++;
                    }

                    // Auto-fit all column widths
                    worksheet.Columns().AdjustToContents();

                    workbook.SaveAs(dialog.FileName);

                    MessageBox.Show(
                        $"Successfully exported batch '{batch.BatchName}' ({leadsToExport.Count} leads) to Excel!\n\nSaved to: {dialog.FileName}",
                        "Batch Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to export batch leads: {ex.Message}",
                        "Export Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Permanently deletes a batch and all of its leads from the database.
        /// </summary>
        private async Task ExecuteDeleteBatchAsync(object? parameter)
        {
            if (parameter is not ImportBatchModel batch) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete batch '{batch.BatchName}'?\n\nThis will permanently delete this batch record and all {batch.TotalLeads:N0} leads associated with it from the central database.\n\nThis action cannot be undone.",
                "Confirm Batch Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var (success, message) = await ApiService.Instance.DeleteBatchAsync(batch.Id);

                if (success)
                {
                    // If preview was open for this batch, close it
                    if (PreviewBatch?.Id == batch.Id)
                    {
                        ExecuteClosePreview();
                    }

                    // Refresh batches
                    await LoadBatchesAsync();

                    MessageBox.Show(
                        message,
                        "Batch Deleted",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Could not delete batch: {message}",
                        "Delete Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error deleting batch: {ex.Message}",
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}

