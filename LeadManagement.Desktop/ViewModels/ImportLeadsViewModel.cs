using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ClosedXML.Excel;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.Services;
using Microsoft.Win32;

namespace LeadManagement.Desktop.ViewModels
{
    public class ImportLeadsViewModel : BaseViewModel
    {
        private readonly List<LeadModel> _allParsedRecords = new();
        private ObservableCollection<LeadModel> _filteredPreviewRecords = new();
        private string _selectedFilePath = string.Empty;
        private string _fileName = string.Empty;
        private string _fileSizeDisplay = string.Empty;
        private string _batchName = string.Empty;
        private string _defaultStatus = "New Lead";
        private string _searchText = string.Empty;
        private bool _isFileLoaded = false;
        private bool _isImporting = false;
        private string _statusMessage = string.Empty;
        private bool _isSuccessBannerVisible = false;
        private bool _isBannerError = false;
        private int _totalRows = 0;

        public ImportLeadsViewModel()
        {
            BrowseFileCommand = new RelayCommand(ExecuteBrowseFile);
            ClearFileCommand = new RelayCommand(ExecuteClearFile);
            CommitImportCommand = new RelayCommand(async () => await ExecuteCommitImportAsync(), () => CanCommit);
            DownloadTemplateCommand = new RelayCommand(ExecuteDownloadTemplate);
            DismissBannerCommand = new RelayCommand(() => IsSuccessBannerVisible = false);

            Statuses = new ObservableCollection<string>
            {
                "New Lead",
                "Qualified",
                "Follow-up",
                "Contacted"
            };

            // Starts completely empty — ready for user to browse or drop a spreadsheet
        }

        public ObservableCollection<LeadModel> FilteredPreviewRecords
        {
            get => _filteredPreviewRecords;
            set => SetProperty(ref _filteredPreviewRecords, value);
        }

        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set => SetProperty(ref _selectedFilePath, value);
        }

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string FileSizeDisplay
        {
            get => _fileSizeDisplay;
            set => SetProperty(ref _fileSizeDisplay, value);
        }

        public string BatchName
        {
            get => _batchName;
            set => SetProperty(ref _batchName, value);
        }

        public bool IsFileLoaded
        {
            get => _isFileLoaded;
            set
            {
                if (SetProperty(ref _isFileLoaded, value))
                {
                    OnPropertyChanged(nameof(CanCommit));
                    OnPropertyChanged(nameof(ShowingRowsText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsImporting
        {
            get => _isImporting;
            set
            {
                if (SetProperty(ref _isImporting, value))
                {
                    OnPropertyChanged(nameof(CanCommit));
                    OnPropertyChanged(nameof(CommitButtonText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanCommit => IsFileLoaded && TotalRows > 0 && !IsImporting;

        public string CommitButtonText => IsImporting ? "Importing to Database..." : "✓ Confirm & Commit Batch";

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsSuccessBannerVisible
        {
            get => _isSuccessBannerVisible;
            set => SetProperty(ref _isSuccessBannerVisible, value);
        }

        public bool IsBannerError
        {
            get => _isBannerError;
            set => SetProperty(ref _isBannerError, value);
        }

        public string DefaultStatus
        {
            get => _defaultStatus;
            set
            {
                if (SetProperty(ref _defaultStatus, value))
                {
                    // Dynamically update status across all loaded preview records
                    foreach (var record in _allParsedRecords)
                    {
                        record.Status = value;
                    }
                    ApplyFilter();
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

        public int TotalRows
        {
            get => _totalRows;
            set
            {
                if (SetProperty(ref _totalRows, value))
                {
                    OnPropertyChanged(nameof(CanCommit));
                    OnPropertyChanged(nameof(ShowingRowsText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public int FilteredCount => FilteredPreviewRecords.Count;

        public string ShowingRowsText
        {
            get
            {
                if (!IsFileLoaded || TotalRows == 0)
                    return "No Data Loaded";
                if (FilteredPreviewRecords.Count == TotalRows)
                    return $"Showing All {TotalRows} Rows";
                return $"Showing {FilteredPreviewRecords.Count} of {TotalRows} Rows";
            }
        }

        public ObservableCollection<string> Statuses { get; }

        public ICommand BrowseFileCommand { get; }
        public ICommand ClearFileCommand { get; }
        public ICommand CommitImportCommand { get; }
        public ICommand DownloadTemplateCommand { get; }
        public ICommand DismissBannerCommand { get; }

        private void ExecuteBrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Leads Spreadsheet (CSV or Excel)",
                Filter = "Spreadsheets (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                var fileInfo = new FileInfo(dialog.FileName);
                SelectedFilePath = fileInfo.FullName;
                FileName = fileInfo.Name;
                FileSizeDisplay = $"{fileInfo.Length / 1024.0:F1} KB";
                
                // Automatically set batch name from file name if empty
                if (string.IsNullOrWhiteSpace(BatchName))
                {
                    BatchName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                }

                var ext = fileInfo.Extension.ToLowerInvariant();
                LogInfo($"Selected file for import: {fileInfo.FullName} (Extension: {ext}, Size: {FileSizeDisplay})");
                if (ext == ".xlsx" || ext == ".xls")
                    ParseExcelFile(fileInfo.FullName);
                else
                    ParseCsvFile(fileInfo.FullName);
            }
        }

        private void ExecuteClearFile()
        {
            SelectedFilePath = string.Empty;
            FileName = string.Empty;
            FileSizeDisplay = string.Empty;
            BatchName = string.Empty;
            IsFileLoaded = false;
            IsSuccessBannerVisible = false;
            IsBannerError = false;
            SearchText = string.Empty;
            _allParsedRecords.Clear();
            FilteredPreviewRecords = new ObservableCollection<LeadModel>();
            TotalRows = 0;
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(ShowingRowsText));
        }

        /// <summary>
        /// Asynchronously commits the loaded leads batch into the MySQL database via the PHP API.
        /// </summary>
        private async Task ExecuteCommitImportAsync()
        {
            if (!IsFileLoaded || _allParsedRecords.Count == 0) return;

            IsImporting = true;
            IsSuccessBannerVisible = false;

            try
            {
                // Fallback batch name if left blank
                if (string.IsNullOrWhiteSpace(BatchName))
                {
                    BatchName = !string.IsNullOrWhiteSpace(FileName)
                        ? Path.GetFileNameWithoutExtension(FileName)
                        : $"Batch_{DateTime.Now:yyyyMMdd_HHmmss}";
                }

                // Ensure every record carries the current batch name and status
                foreach (var record in _allParsedRecords)
                {
                    record.BatchName = BatchName;
                    record.Status = DefaultStatus;
                }

                int withWeb = _allParsedRecords.Count(r => !string.IsNullOrWhiteSpace(r.Website));
                LogInfo($"Committing batch '{BatchName}' with {_allParsedRecords.Count} leads ({withWeb} with website) to API...");

                var (success, message, data) = await ApiService.Instance.ImportLeadsAsync(
                    BatchName,
                    FileName,
                    DefaultStatus,
                    _allParsedRecords
                );

                if (success)
                {
                    int insertedCount = data?.InsertedCount ?? TotalRows;
                    int batchId = data?.BatchId ?? 1;

                    StatusMessage = $"✓ Successfully imported {insertedCount} leads into the database under batch '{BatchName}'.";
                    IsBannerError = false;
                    IsSuccessBannerVisible = true;

                    // Cleanly reset file and preview state after successful import
                    _allParsedRecords.Clear();
                    FilteredPreviewRecords = new ObservableCollection<LeadModel>();
                    SelectedFilePath = string.Empty;
                    FileName = string.Empty;
                    FileSizeDisplay = string.Empty;
                    BatchName = string.Empty;
                    TotalRows = 0;
                    IsFileLoaded = false;
                    OnPropertyChanged(nameof(FilteredCount));
                    OnPropertyChanged(nameof(ShowingRowsText));
                }
                else
                {
                    StatusMessage = $"⚠ Import failed: {message}";
                    IsBannerError = true;
                    IsSuccessBannerVisible = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"⚠ An error occurred during import: {ex.Message}";
                IsBannerError = true;
                IsSuccessBannerVisible = true;
            }
            finally
            {
                IsImporting = false;
            }
        }

        private void ExecuteDownloadTemplate()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Leads CSV Template",
                FileName = "LMS_Leads_Template.csv",
                Filter = "CSV Files (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var header = "Business Name,Address,City,County,State,Zip Code,Cell Number,FAX,Annual Sales,Number Of Employee,Title,Contact Name,Website,SIC Code,SIC Description,NAICS Code,NAICS Description\n";
                    var example = "ROSEYLNS RESTAURANT,604 HIGHWAY 6,ASHLAND,SAUNDERS,NE,68003,4025212323,,Less than 500,000,5 to 9,,MS ROSELYN SMITH,,58120000,EATING PLACES,722511,FULL-SERVICE RESTAURANT\n";
                    File.WriteAllText(dialog.FileName, header + example);
                    StatusMessage = $"Template saved successfully to {dialog.FileName}";
                    IsBannerError = false;
                    IsSuccessBannerVisible = true;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Could not save template: {ex.Message}";
                    IsBannerError = true;
                    IsSuccessBannerVisible = true;
                }
            }
        }

        private static void LogInfo(string message)
        {
            var formatted = $"[LMS] {message}";
            Console.WriteLine(formatted);
            System.Diagnostics.Debug.WriteLine(formatted);
        }

        private void ParseCsvFile(string path)
        {
            try
            {
                LogInfo($"=== Parsing CSV File: {path} ===");
                _allParsedRecords.Clear();
                var lines = File.ReadAllLines(path);
                if (lines.Length > 1)
                {
                    // Map headers dynamically
                    var headerLine = lines[0];
                    var headers = SplitCsvLine(headerLine)
                        .Select(h => h.Trim().ToLowerInvariant().Replace("_", " "))
                        .ToList();

                    LogInfo($"[CSV] Total lines: {lines.Length}. Detected {headers.Count} headers: {string.Join(" | ", headers)}");

                    int GetColIndex(params string[] aliases)
                    {
                        for (int i = 0; i < headers.Count; i++)
                        {
                            foreach (var alias in aliases)
                            {
                                if (headers[i].Equals(alias, StringComparison.OrdinalIgnoreCase) ||
                                    headers[i].Contains(alias, StringComparison.OrdinalIgnoreCase))
                                {
                                    return i;
                                }
                            }
                        }
                        return -1;
                    }

                    int idxBusiness = GetColIndex("business name", "company", "business");
                    int idxAddress  = GetColIndex("address", "street");
                    int idxCity     = GetColIndex("city");
                    int idxCounty   = GetColIndex("county");
                    int idxState    = GetColIndex("state");
                    int idxZip      = GetColIndex("zip code", "zip", "postal");
                    int idxCell     = GetColIndex("cell number", "cell", "phone", "telephone", "mobile");
                    int idxFax      = GetColIndex("fax");
                    int idxSales    = GetColIndex("annual sales", "sales");
                    int idxEmp      = GetColIndex("number of employee", "employees", "number of employees");
                    int idxTitle    = GetColIndex("title", "job title");
                    int idxContact  = GetColIndex("contact name", "contact", "name");
                    int idxWeb      = GetColIndex("website", "web", "url", "site");
                    int idxSicCode  = GetColIndex("sic code", "sic");
                    int idxSicDesc  = GetColIndex("sic description", "sic desc");
                    int idxNaicsCod = GetColIndex("naics code", "naics");
                    int idxNaicsDes = GetColIndex("naics description", "naics desc");

                    LogInfo($"[CSV Mapping] idxWeb={idxWeb} ('{(idxWeb >= 0 && idxWeb < headers.Count ? headers[idxWeb] : "NOT MATCHED")}'), idxBusiness={idxBusiness}, idxContact={idxContact}");

                    var dataRows = lines.Skip(1).ToList();
                    int rowId = 1;

                    string SafeGet(string[] parts, int index, int fallbackIndex, string defaultVal = "")
                    {
                        int target = index >= 0 ? index : fallbackIndex;
                        if (target >= 0 && target < parts.Length)
                        {
                            var val = parts[target].Trim();
                            return string.IsNullOrEmpty(val) ? defaultVal : val;
                        }
                        return defaultVal;
                    }

                    foreach (var line in dataRows)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = SplitCsvLine(line);

                        var lead = new LeadModel
                        {
                            Id = rowId++,
                            BatchName = BatchName,
                            BusinessName = SafeGet(parts, idxBusiness, 0, "Unnamed Business"),
                            Address = SafeGet(parts, idxAddress, 1),
                            City = SafeGet(parts, idxCity, 2),
                            County = SafeGet(parts, idxCounty, 3),
                            State = SafeGet(parts, idxState, 4, "NE"),
                            ZipCode = SafeGet(parts, idxZip, 5),
                            CellNumber = SafeGet(parts, idxCell, 6),
                            Fax = SafeGet(parts, idxFax, 7),
                            AnnualSales = SafeGet(parts, idxSales, 8, "Less than 500,000"),
                            NumberOfEmployees = SafeGet(parts, idxEmp, 9, "1 to 4"),
                            Title = SafeGet(parts, idxTitle, 10),
                            ContactName = SafeGet(parts, idxContact, 11),
                            Website = SafeGet(parts, idxWeb, 12),
                            SicCode = SafeGet(parts, idxSicCode, 13, "58120000"),
                            SicDescription = SafeGet(parts, idxSicDesc, 14, "EATING PLACES"),
                            NaicsCode = SafeGet(parts, idxNaicsCod, 15, "722511"),
                            NaicsDescription = SafeGet(parts, idxNaicsDes, 16, "RESTAURANTS"),
                            Status = DefaultStatus
                        };

                        if (lead.Id <= 5)
                        {
                            LogInfo($"[CSV Row {lead.Id}] Business='{lead.BusinessName}', Contact='{lead.ContactName}', Website='{lead.Website}'");
                        }

                        _allParsedRecords.Add(lead);
                    }

                    TotalRows = _allParsedRecords.Count;
                    IsFileLoaded = true;
                    int withWebCount = _allParsedRecords.Count(r => !string.IsNullOrWhiteSpace(r.Website));
                    LogInfo($"[CSV Done] Loaded {_allParsedRecords.Count} records. Records with website: {withWebCount}.");
                    ApplyFilter();
                    return;
                }
            }
            catch (Exception ex)
            {
                LogInfo($"[CSV ERROR] Could not parse CSV file: {ex.Message}\n{ex.StackTrace}");
                StatusMessage = $"Could not parse spreadsheet: {ex.Message}";
                IsBannerError = true;
                IsSuccessBannerVisible = true;
                ExecuteClearFile();
            }
        }

        /// <summary>
        /// Parses an .xlsx or .xls file using ClosedXML and populates the preview records list.
        /// Uses the same dynamic column-detection logic as ParseCsvFile.
        /// </summary>
        private void ParseExcelFile(string path)
        {
            try
            {
                LogInfo($"=== Parsing Excel File: {path} ===");
                _allParsedRecords.Clear();

                using var workbook = new XLWorkbook(path);
                var worksheet = workbook.Worksheets.First();

                var usedRange = worksheet.RangeUsed();
                if (usedRange == null)
                {
                    LogInfo("[Excel WARN] Worksheet RangeUsed is null (file appears empty).");
                    StatusMessage = "The selected Excel file appears to be empty.";
                    IsBannerError = true;
                    IsSuccessBannerVisible = true;
                    return;
                }

                var rows = usedRange.RowsUsed().ToList();
                if (rows.Count <= 1)
                {
                    LogInfo($"[Excel WARN] Worksheet has {rows.Count} rows (header only or empty).");
                    StatusMessage = "No data rows found in the Excel file (only a header row was detected).";
                    IsBannerError = true;
                    IsSuccessBannerVisible = true;
                    return;
                }

                // --- Header detection (row 1) ---
                var headerRow = rows[0];
                var headers = headerRow.Cells()
                    .Select(c => (c.IsEmpty() ? string.Empty : c.GetFormattedString()).Trim().ToLowerInvariant().Replace("_", " "))
                    .ToList();

                LogInfo($"[Excel] Worksheet '{worksheet.Name}' rows={rows.Count}. Headers ({headers.Count}): {string.Join(" | ", headers)}");

                int GetColIndex(params string[] aliases)
                {
                    for (int i = 0; i < headers.Count; i++)
                    {
                        foreach (var alias in aliases)
                        {
                            if (headers[i].Equals(alias, StringComparison.OrdinalIgnoreCase) ||
                                headers[i].Contains(alias, StringComparison.OrdinalIgnoreCase))
                                return i;
                        }
                    }
                    return -1;
                }

                int idxBusiness = GetColIndex("business name", "company", "business");
                int idxAddress  = GetColIndex("address", "street");
                int idxCity     = GetColIndex("city");
                int idxCounty   = GetColIndex("county");
                int idxState    = GetColIndex("state");
                int idxZip      = GetColIndex("zip code", "zip", "postal");
                int idxCell     = GetColIndex("cell number", "cell", "phone", "telephone", "mobile");
                int idxFax      = GetColIndex("fax");
                int idxSales    = GetColIndex("annual sales", "sales");
                int idxEmp      = GetColIndex("number of employee", "employees", "number of employees");
                int idxTitle    = GetColIndex("title", "job title");
                int idxContact  = GetColIndex("contact name", "contact", "name");
                int idxWeb      = GetColIndex("website", "web", "url", "site");
                int idxSicCode  = GetColIndex("sic code", "sic");
                int idxSicDesc  = GetColIndex("sic description", "sic desc");
                int idxNaicsCod = GetColIndex("naics code", "naics");
                int idxNaicsDes = GetColIndex("naics description", "naics desc");

                LogInfo($"[Excel Mapping] idxWeb={idxWeb} ('{(idxWeb >= 0 && idxWeb < headers.Count ? headers[idxWeb] : "NOT MATCHED")}'), idxBusiness={idxBusiness}, idxContact={idxContact}");

                // --- Data rows (row 2 onward) ---
                string SafeGet(string[] parts, int index, int fallbackIndex, string defaultVal = "")
                {
                    int target = index >= 0 ? index : fallbackIndex;
                    if (target >= 0 && target < parts.Length)
                    {
                        var val = parts[target].Trim();
                        return string.IsNullOrEmpty(val) ? defaultVal : val;
                    }
                    return defaultVal;
                }

                int rowId = 1;
                int colCount = headers.Count;

                foreach (var row in rows.Skip(1))
                {
                    // Read each cell safely using GetFormattedString, and fallback to Hyperlink address if present
                    var parts = Enumerable.Range(1, colCount)
                        .Select(colNum =>
                        {
                            var cell = row.Cell(colNum);
                            if (cell.IsEmpty()) return string.Empty;
                            var val = cell.GetFormattedString().Trim();
                            if (string.IsNullOrEmpty(val) && cell.HasHyperlink)
                            {
                                val = cell.GetHyperlink().ExternalAddress?.ToString()?.Trim() ?? string.Empty;
                            }
                            return val;
                        })
                        .ToArray();

                    // Skip completely blank rows
                    if (parts.All(string.IsNullOrWhiteSpace)) continue;

                    var lead = new LeadModel
                    {
                        Id               = rowId++,
                        BatchName        = BatchName,
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
                        NaicsCode        = SafeGet(parts, idxNaicsCod,15, "722511"),
                        NaicsDescription = SafeGet(parts, idxNaicsDes,16, "RESTAURANTS"),
                        Status           = DefaultStatus
                    };

                    if (lead.Id <= 5)
                    {
                        LogInfo($"[Excel Row {lead.Id}] Business='{lead.BusinessName}', Contact='{lead.ContactName}', Website='{lead.Website}'");
                    }

                    _allParsedRecords.Add(lead);
                }

                TotalRows    = _allParsedRecords.Count;
                IsFileLoaded = true;
                int withWebCount = _allParsedRecords.Count(r => !string.IsNullOrWhiteSpace(r.Website));
                LogInfo($"[Excel Done] Loaded {_allParsedRecords.Count} records. Records with website: {withWebCount}.");
                ApplyFilter();
            }
            catch (Exception ex)
            {
                LogInfo($"[Excel ERROR] Could not parse Excel file: {ex.Message}\n{ex.StackTrace}");
                StatusMessage = $"Could not parse Excel file: {ex.Message}";
                IsBannerError = true;
                IsSuccessBannerVisible = true;
                ExecuteClearFile();
            }
        }

        private string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim('"', ' '));
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim('"', ' '));
            return result.ToArray();
        }

        private void ApplyFilter()
        {
            IEnumerable<LeadModel> query = _allParsedRecords;

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
                    (l.SicDescription?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Title?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Website?.ToLowerInvariant().Contains(term) ?? false) ||
                    (l.Address?.ToLowerInvariant().Contains(term) ?? false));
            }

            FilteredPreviewRecords = new ObservableCollection<LeadModel>(query);
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(ShowingRowsText));
        }
    }
}
