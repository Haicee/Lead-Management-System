using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
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

        // ─── Modal State Properties ───
        private bool _isParsingModalOpen = false;
        private string _parsingModalTitle = "Reading Spreadsheet...";
        private string _parsingModalStatus = "Processing records...";
        private double _parsingProgressPercentage = 0;
        private bool _isParsingIndeterminate = true;

        private bool _isCommittingModalOpen = false;
        private string _commitModalTitle = "Committing Leads to Database...";
        private string _commitModalStatus = "Preparing batch...";
        private double _commitProgressPercentage = 0;
        private bool _isCommittingIndeterminate = false;

        private CancellationTokenSource? _parseCts;
        private CancellationTokenSource? _commitCts;

        public ImportLeadsViewModel()
        {
            BrowseFileCommand = new RelayCommand(ExecuteBrowseFile);
            ClearFileCommand = new RelayCommand(ExecuteClearFile);
            CommitImportCommand = new RelayCommand(async () => await ExecuteCommitImportAsync(), () => CanCommit);
            DownloadTemplateCommand = new RelayCommand(ExecuteDownloadTemplate);
            DismissBannerCommand = new RelayCommand(() => IsSuccessBannerVisible = false);

            CancelParseCommand = new RelayCommand(() =>
            {
                LogInfo("User requested cancellation of file reading/parsing.");
                _parseCts?.Cancel();
                ParsingModalStatus = "Cancelling file read...";
            });

            CancelCommitCommand = new RelayCommand(() =>
            {
                LogInfo("User requested cancellation of database commit.");
                _commitCts?.Cancel();
                CommitModalStatus = "Cancelling commit (waiting for current chunk to finish)...";
            });

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
                    return $"Showing All {TotalRows:N0} Rows";
                return $"Showing Preview ({FilteredPreviewRecords.Count:N0} of {TotalRows:N0} Total Rows)";
            }
        }

        // ─── Modal Bindings ───

        public bool IsParsingModalOpen
        {
            get => _isParsingModalOpen;
            set => SetProperty(ref _isParsingModalOpen, value);
        }

        public string ParsingModalTitle
        {
            get => _parsingModalTitle;
            set => SetProperty(ref _parsingModalTitle, value);
        }

        public string ParsingModalStatus
        {
            get => _parsingModalStatus;
            set => SetProperty(ref _parsingModalStatus, value);
        }

        public double ParsingProgressPercentage
        {
            get => _parsingProgressPercentage;
            set => SetProperty(ref _parsingProgressPercentage, value);
        }

        public bool IsParsingIndeterminate
        {
            get => _isParsingIndeterminate;
            set => SetProperty(ref _isParsingIndeterminate, value);
        }

        public bool IsCommittingModalOpen
        {
            get => _isCommittingModalOpen;
            set => SetProperty(ref _isCommittingModalOpen, value);
        }

        public string CommitModalTitle
        {
            get => _commitModalTitle;
            set => SetProperty(ref _commitModalTitle, value);
        }

        public string CommitModalStatus
        {
            get => _commitModalStatus;
            set => SetProperty(ref _commitModalStatus, value);
        }

        public double CommitProgressPercentage
        {
            get => _commitProgressPercentage;
            set => SetProperty(ref _commitProgressPercentage, value);
        }

        public bool IsCommittingIndeterminate
        {
            get => _isCommittingIndeterminate;
            set => SetProperty(ref _isCommittingIndeterminate, value);
        }

        public ObservableCollection<string> Statuses { get; }

        public ICommand BrowseFileCommand { get; }
        public ICommand ClearFileCommand { get; }
        public ICommand CommitImportCommand { get; }
        public ICommand DownloadTemplateCommand { get; }
        public ICommand DismissBannerCommand { get; }
        public ICommand CancelParseCommand { get; }
        public ICommand CancelCommitCommand { get; }

        private async void ExecuteBrowseFile()
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

                _parseCts?.Dispose();
                _parseCts = new CancellationTokenSource();

                IsParsingModalOpen = true;
                ParsingModalTitle = "Reading Spreadsheet...";
                ParsingModalStatus = $"Opening '{FileName}' ({FileSizeDisplay})...";
                IsParsingIndeterminate = true;
                ParsingProgressPercentage = 0;
                IsSuccessBannerVisible = false;

                try
                {
                    var progress = new Progress<(int read, int total, double pct, string msg)>(p =>
                    {
                        IsParsingIndeterminate = p.total <= 0;
                        ParsingProgressPercentage = p.pct;
                        ParsingModalStatus = p.msg;
                    });

                    var token = _parseCts.Token;

                    List<LeadModel>? parsedList = null;
                    if (ext == ".xlsx" || ext == ".xls")
                    {
                        parsedList = await Task.Run(() => ParseExcelFile(fileInfo.FullName, progress, token));
                    }
                    else
                    {
                        parsedList = await Task.Run(() => ParseCsvFile(fileInfo.FullName, progress, token));
                    }

                    if (token.IsCancellationRequested || parsedList == null)
                    {
                        LogInfo("File import cancelled by user. Reverting state.");
                        ExecuteClearFile();
                        StatusMessage = "Spreadsheet reading was cancelled. No data loaded.";
                        IsBannerError = false;
                        IsSuccessBannerVisible = true;
                        return;
                    }

                    _allParsedRecords.Clear();
                    _allParsedRecords.AddRange(parsedList);

                    TotalRows = _allParsedRecords.Count;
                    IsFileLoaded = true;

                    int withWebCount = _allParsedRecords.Count(r => !string.IsNullOrWhiteSpace(r.Website));
                    LogInfo($"[Done] Loaded {_allParsedRecords.Count} records. Records with website: {withWebCount}.");

                    ApplyFilter();
                }
                catch (OperationCanceledException)
                {
                    LogInfo("Parsing operation cancelled.");
                    ExecuteClearFile();
                    StatusMessage = "Spreadsheet reading was cancelled. No data loaded.";
                    IsBannerError = false;
                    IsSuccessBannerVisible = true;
                }
                catch (Exception ex)
                {
                    LogInfo($"[ERROR] Could not parse file: {ex.Message}");
                    StatusMessage = $"Could not parse spreadsheet: {ex.Message}";
                    IsBannerError = true;
                    IsSuccessBannerVisible = true;
                    ExecuteClearFile();
                }
                finally
                {
                    IsParsingModalOpen = false;
                }
            }
        }

        private void ExecuteClearFile()
        {
            SelectedFilePath = string.Empty;
            FileName = string.Empty;
            FileSizeDisplay = string.Empty;
            BatchName = string.Empty;
            IsFileLoaded = false;
            SearchText = string.Empty;
            _allParsedRecords.Clear();
            FilteredPreviewRecords = new ObservableCollection<LeadModel>();
            TotalRows = 0;
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(ShowingRowsText));
        }

        /// <summary>
        /// Asynchronously commits the loaded leads batch into the MySQL database in chunks via the PHP API.
        /// </summary>
        private async Task ExecuteCommitImportAsync()
        {
            if (!IsFileLoaded || _allParsedRecords.Count == 0 || IsImporting) return;

            IsImporting = true;
            IsSuccessBannerVisible = false;

            _commitCts?.Dispose();
            _commitCts = new CancellationTokenSource();

            IsCommittingModalOpen = true;
            CommitModalTitle = "Committing Leads to Database...";
            CommitModalStatus = $"Preparing {_allParsedRecords.Count:N0} leads for commit...";
            CommitProgressPercentage = 0;
            IsCommittingIndeterminate = false;

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
                LogInfo($"Committing batch '{BatchName}' with {_allParsedRecords.Count:N0} leads ({withWeb:N0} with website) to API in chunks...");

                var progress = new Progress<(int Processed, int Total, double Percentage, string StatusText)>(p =>
                {
                    CommitProgressPercentage = p.Percentage;
                    CommitModalStatus = p.StatusText;
                });

                var (success, message, data) = await ApiService.Instance.ImportLeadsAsync(
                    BatchName,
                    FileName,
                    DefaultStatus,
                    _allParsedRecords,
                    progress,
                    _commitCts.Token
                );

                if (success)
                {
                    int insertedCount = data?.InsertedCount ?? TotalRows;

                    StatusMessage = $"✓ Successfully imported {insertedCount:N0} leads into the database under batch '{BatchName}'.";
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
                    StatusMessage = $"⚠ Import result: {message}";
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
                IsCommittingModalOpen = false;
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

        private List<LeadModel>? ParseCsvFile(
            string path,
            IProgress<(int read, int total, double pct, string msg)>? progress,
            CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            progress?.Report((0, 0, 0, "Reading CSV lines..."));

            var lines = File.ReadAllLines(path);
            if (lines.Length <= 1)
            {
                throw new InvalidOperationException("The CSV file is empty or only contains a header.");
            }

            var headerLine = lines[0];
            var headers = SplitCsvLine(headerLine)
                .Select(h => h.Trim().ToLowerInvariant().Replace("_", " "))
                .ToList();

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

            int totalDataLines = lines.Length - 1;
            var result = new List<LeadModel>(totalDataLines);
            int rowId = 1;

            for (int i = 1; i < lines.Length; i++)
            {
                if (i % 1000 == 0)
                {
                    if (token.IsCancellationRequested) return null;
                    double pct = (double)i / totalDataLines * 100.0;
                    progress?.Report((i, totalDataLines, pct, $"Parsed {i:N0} of {totalDataLines:N0} rows ({pct:F1}%)..."));
                }

                var line = lines[i];
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

                result.Add(lead);
            }

            progress?.Report((totalDataLines, totalDataLines, 100.0, $"Loaded {result.Count:N0} rows."));
            return result;
        }

        private List<LeadModel>? ParseExcelFile(
            string path,
            IProgress<(int read, int total, double pct, string msg)>? progress,
            CancellationToken token)
        {
            if (token.IsCancellationRequested) return null;
            progress?.Report((0, 0, 0, "Opening Excel workbook..."));

            using var workbook = new XLWorkbook(path);
            var worksheet = workbook.Worksheets.First();

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                throw new InvalidOperationException("The selected Excel file appears to be empty.");
            }

            if (token.IsCancellationRequested) return null;
            progress?.Report((0, 0, 0, "Analyzing rows..."));

            var rows = usedRange.RowsUsed().ToList();
            if (rows.Count <= 1)
            {
                throw new InvalidOperationException("No data rows found in the Excel file (only a header row detected).");
            }

            int totalDataRows = rows.Count - 1;

            // --- Header detection (row 1) ---
            var headerRow = rows[0];
            var headers = headerRow.Cells()
                .Select(c => (c.IsEmpty() ? string.Empty : c.GetFormattedString()).Trim().ToLowerInvariant().Replace("_", " "))
                .ToList();

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

            var result = new List<LeadModel>(totalDataRows);
            int rowId = 1;
            int colCount = headers.Count;
            int counter = 0;

            foreach (var row in rows.Skip(1))
            {
                counter++;
                if (counter % 1000 == 0)
                {
                    if (token.IsCancellationRequested) return null;
                    double pct = (double)counter / totalDataRows * 100.0;
                    progress?.Report((counter, totalDataRows, pct, $"Parsed {counter:N0} of {totalDataRows:N0} rows ({pct:F1}%)..."));
                }

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

                result.Add(lead);
            }

            progress?.Report((totalDataRows, totalDataRows, 100.0, $"Finished loading {result.Count:N0} rows."));
            return result;
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

            // Preview first 500 rows to ensure instantaneous UI rendering
            var previewItems = query.Take(500).ToList();
            FilteredPreviewRecords = new ObservableCollection<LeadModel>(previewItems);
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(ShowingRowsText));
        }
    }
}
