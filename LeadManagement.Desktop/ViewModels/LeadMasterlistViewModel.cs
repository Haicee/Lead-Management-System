using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using LeadManagement.Desktop.Models;

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

        public LeadMasterlistViewModel()
        {
            ClearFiltersCommand = new RelayCommand(ExecuteClearFilters);
            RefreshCommand = new RelayCommand(ExecuteRefresh);
            ExportCommand = new RelayCommand(ExecuteExport);

            LoadSampleLeads();
            PopulateFilterLists();
            ApplyFilter();
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

        public ICommand ClearFiltersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }

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

        private void ExecuteRefresh()
        {
            ApplyFilter();
        }

        private void ExecuteExport()
        {
            // Placeholder for CSV export functionality
        }

        private void ApplyFilter()
        {
            IEnumerable<LeadModel> query = _allLeadsMaster;

            // 1. Text Search Filter
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

            // 3. Industry Filter
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

            // Select first lead by default if current selection is not in filtered list
            if (SelectedLead == null || !FilteredLeads.Contains(SelectedLead))
            {
                SelectedLead = FilteredLeads.FirstOrDefault();
            }
        }

        private void PopulateFilterLists()
        {
            Cities.Clear();
            Cities.Add("All Cities");
            foreach (var city in _allLeadsMaster.Select(l => l.City).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c))
            {
                Cities.Add(city);
            }

            Industries.Clear();
            Industries.Add("All Industries");
            foreach (var ind in _allLeadsMaster.Select(l => l.SicDescription).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().OrderBy(i => i))
            {
                Industries.Add(ind);
            }

            Statuses.Clear();
            Statuses.Add("All Statuses");
            Statuses.Add("New Lead");
            Statuses.Add("Qualified");
            Statuses.Add("Follow-up");
            Statuses.Add("Contacted");
        }

        private void LoadSampleLeads()
        {
            _allLeadsMaster.Clear();
            _allLeadsMaster.AddRange(new[]
            {
                new LeadModel
                {
                    Id = 1,
                    BusinessName = "ROSEYLNS RESTAURANT",
                    Address = "604 HIGHWAY 6",
                    City = "ASHLAND",
                    County = "SAUNDERS",
                    State = "NE",
                    ZipCode = "68003",
                    CellNumber = "4025212323",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "OWNER",
                    ContactName = "MS ROSELYN SMITH",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "722511",
                    NaicsDescription = "FULL-SERVICE RESTAURANT",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 2,
                    BusinessName = "TAQUERIA GONZALEZ",
                    Address = "313 W INDIANA AVE",
                    City = "NORFOLK",
                    County = "MADISON",
                    State = "NE",
                    ZipCode = "68701",
                    CellNumber = "4029925207",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "PRINCIPAL",
                    ContactName = "MS KARINA GONZALEZ",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "17116",
                    NaicsDescription = "RESTAURANTS",
                    Status = "New Lead"
                },
                new LeadModel
                {
                    Id = 3,
                    BusinessName = "VIETNAMESE NOODLES",
                    Address = "1220 RIVERSIDE BLVD",
                    City = "NORFOLK",
                    County = "MADISON",
                    State = "NE",
                    ZipCode = "68701",
                    CellNumber = "4023167546",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "MANAGER",
                    ContactName = "MR PHAM NGUYEN",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "17116",
                    NaicsDescription = "RESTAURANTS",
                    Status = "Follow-up"
                },
                new LeadModel
                {
                    Id = 4,
                    BusinessName = "BUCKS SEPTIC PUMPING",
                    Address = "55053 US HIGHWAY",
                    City = "NORFOLK",
                    County = "MADISON",
                    State = "NE",
                    ZipCode = "68701",
                    CellNumber = "4028601169",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "PRINCIPAL",
                    ContactName = "MR MARK BUCKINGHAM",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "PLUMBERS",
                    NaicsCode = "110331",
                    NaicsDescription = "PLUMBING SERVICES",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 5,
                    BusinessName = "EAT FIT GO HEALTHY",
                    Address = "1304 RIVERSIDE BLVD",
                    City = "NORFOLK",
                    County = "MADISON",
                    State = "NE",
                    ZipCode = "68701",
                    CellNumber = "4023167298",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "BRANCH MANAGER",
                    ContactName = "MR TODD UHLIR",
                    Website = "EATFITGO.COM",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "722511",
                    NaicsDescription = "FULL-SERVICE RESTAURANT",
                    Status = "New Lead"
                },
                new LeadModel
                {
                    Id = 6,
                    BusinessName = "SUZYMAE'S",
                    Address = "1630 WASHINGTON ST",
                    City = "BLAIR",
                    County = "WASHINGTON",
                    State = "NE",
                    ZipCode = "68008",
                    CellNumber = "4022782406",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "PRINCIPAL",
                    ContactName = "MS CHANDRA PETERSEN",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "BAKERIES",
                    NaicsCode = "18106",
                    NaicsDescription = "BAKERIES & BREAD",
                    Status = "Contacted"
                },
                new LeadModel
                {
                    Id = 7,
                    BusinessName = "BIG RED BARN SERVICE",
                    Address = "702 W NORFOLK AVE",
                    City = "NORFOLK",
                    County = "MADISON",
                    State = "NE",
                    ZipCode = "68701",
                    CellNumber = "4023213397",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "10 to 19",
                    Title = "SYSTEMS MANAGER",
                    ContactName = "MR MICHAEL BEHRENS",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "722511",
                    NaicsDescription = "FULL-SERVICE RESTAURANT",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 8,
                    BusinessName = "AROMA CIRCUIT",
                    Address = "1201 S 13TH ST",
                    City = "NORFOLK",
                    County = "MADISON",
                    State = "NE",
                    ZipCode = "68701",
                    CellNumber = "4023167342",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "10 to 19",
                    Title = "DIRECTOR",
                    ContactName = "MR SANJAY PATEL",
                    Website = "AROMACIRCUIT.COM",
                    SicCode = "58120107",
                    SicDescription = "INDIAN/PAKISTAN RESTAURANT",
                    NaicsCode = "722511",
                    NaicsDescription = "FULL-SERVICE RESTAURANT",
                    Status = "Follow-up"
                },
                new LeadModel
                {
                    Id = 9,
                    BusinessName = "USA STEAK BUFFET",
                    Address = "1950 BIG ISLAND RD",
                    City = "FREMONT",
                    County = "DODGE",
                    State = "NE",
                    ZipCode = "68025",
                    CellNumber = "4028585011",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "GENERAL MANAGER",
                    ContactName = "MR BRADLEY COOPER",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "722511",
                    NaicsDescription = "BUFFET RESTAURANTS",
                    Status = "New Lead"
                },
                new LeadModel
                {
                    Id = 10,
                    BusinessName = "NAPOLI'S ITALIAN RESTAURANT",
                    Address = "35 W 6TH ST",
                    City = "FREMONT",
                    County = "DODGE",
                    State = "NE",
                    ZipCode = "68025",
                    CellNumber = "4026204082",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "HEAD CHEF / OWNER",
                    ContactName = "MR MARCO NAPOLI",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "ITALIAN RESTAURANT",
                    NaicsCode = "17116",
                    NaicsDescription = "RESTAURANTS",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 11,
                    BusinessName = "NEBRASKA CHAPTER",
                    Address = "1425 W 9TH ST",
                    City = "FREMONT",
                    County = "DODGE",
                    State = "NE",
                    ZipCode = "68025",
                    CellNumber = "4025479613",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "PRINCIPAL",
                    ContactName = "MR RICHARD DELOI",
                    Website = "ACINEBRASKA.ORG",
                    SicCode = "58120000",
                    SicDescription = "PROFESSIONAL ASSOC",
                    NaicsCode = "12102",
                    NaicsDescription = "PROFESSIONAL ORGANIZATIONS",
                    Status = "Contacted"
                },
                new LeadModel
                {
                    Id = 12,
                    BusinessName = "CHITOS MEXICAN RESTAURANT",
                    Address = "35 W 6TH ST",
                    City = "FREMONT",
                    County = "DODGE",
                    State = "NE",
                    ZipCode = "68025",
                    CellNumber = "4026204314",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "PRINCIPAL",
                    ContactName = "MR AUGUSTIN RAMIREZ",
                    Website = "",
                    SicCode = "58120112",
                    SicDescription = "MEXICAN RESTAURANT",
                    NaicsCode = "722511",
                    NaicsDescription = "FULL-SERVICE RESTAURANT",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 13,
                    BusinessName = "JIREH RESTAURANT",
                    Address = "635 N MAIN ST STE B",
                    City = "FREMONT",
                    County = "DODGE",
                    State = "NE",
                    ZipCode = "68025",
                    CellNumber = "4022766993",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "OWNER",
                    ContactName = "MRS ELENA HERNANDEZ",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "722511",
                    NaicsDescription = "FULL-SERVICE RESTAURANT",
                    Status = "New Lead"
                },
                new LeadModel
                {
                    Id = 14,
                    BusinessName = "TACO SMACK",
                    Address = "1743 E MILITARY AVE",
                    City = "FREMONT",
                    County = "DODGE",
                    State = "NE",
                    ZipCode = "68025",
                    CellNumber = "4026570494",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "CO-OWNER",
                    ContactName = "MR CARLOS MENDEZ",
                    Website = "",
                    SicCode = "58120112",
                    SicDescription = "MEXICAN RESTAURANT",
                    NaicsCode = "722511",
                    NaicsDescription = "FULL-SERVICE RESTAURANT",
                    Status = "Follow-up"
                },
                new LeadModel
                {
                    Id = 15,
                    BusinessName = "TORTILLERIA ANITA",
                    Address = "207 S BELL ST",
                    City = "FREMONT",
                    County = "DODGE",
                    State = "NE",
                    ZipCode = "68025",
                    CellNumber = "4026204961",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "MANAGER",
                    ContactName = "MS ANITA LOPEZ",
                    Website = "",
                    SicCode = "58120112",
                    SicDescription = "BAKERIES",
                    NaicsCode = "18106",
                    NaicsDescription = "TORTILLA MANUFACTURING",
                    Status = "Contacted"
                },
                new LeadModel
                {
                    Id = 16,
                    BusinessName = "TACO JOHNS",
                    Address = "3310 E ELK LANE",
                    City = "FREMONT",
                    County = "DODGE",
                    State = "NE",
                    ZipCode = "68025",
                    CellNumber = "4026204385",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "INTERIM PRESIDENT",
                    ContactName = "MR GERARD LEWIS",
                    Website = "TACOJOHNS.COM",
                    SicCode = "58120112",
                    SicDescription = "MEXICAN RESTAURANT",
                    NaicsCode = "722511",
                    NaicsDescription = "FAST FOOD RESTAURANTS",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 17,
                    BusinessName = "MIA'S COCINITA",
                    Address = "305 S LOCUST ST",
                    City = "GRAND ISLAND",
                    County = "HALL",
                    State = "NE",
                    ZipCode = "68801",
                    CellNumber = "3086753413",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "OWNER",
                    ContactName = "MS MIA SANCHEZ",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "17116",
                    NaicsDescription = "RESTAURANTS",
                    Status = "New Lead"
                },
                new LeadModel
                {
                    Id = 18,
                    BusinessName = "PACO'S TACOS LLC",
                    Address = "109 W 8TH ST",
                    City = "GRAND ISLAND",
                    County = "HALL",
                    State = "NE",
                    ZipCode = "68801",
                    CellNumber = "3082271631",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "1 to 4",
                    Title = "FOUNDER",
                    ContactName = "MR PACO VILLA",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "17116",
                    NaicsDescription = "RESTAURANTS",
                    Status = "Follow-up"
                },
                new LeadModel
                {
                    Id = 19,
                    BusinessName = "BAMBOO CHINESE",
                    Address = "523 W 4TH ST",
                    City = "GRAND ISLAND",
                    County = "HALL",
                    State = "NE",
                    ZipCode = "68801",
                    CellNumber = "3086752543",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "MANAGING PARTNER",
                    ContactName = "MR DAVID CHEN",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "722511",
                    NaicsDescription = "FULL-SERVICE RESTAURANT",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 20,
                    BusinessName = "GARCIA/MICHEL RESTAURANT",
                    Address = "931 S OAK ST",
                    City = "GRAND ISLAND",
                    County = "HALL",
                    State = "NE",
                    ZipCode = "68801",
                    CellNumber = "3086750011",
                    AnnualSales = "Less than 500,000",
                    NumberOfEmployees = "5 to 9",
                    Title = "OWNER",
                    ContactName = "MR MICHEL GARCIA",
                    Website = "",
                    SicCode = "58120000",
                    SicDescription = "EATING PLACES",
                    NaicsCode = "17116",
                    NaicsDescription = "RESTAURANTS",
                    Status = "New Lead"
                }
            });
        }
    }
}
