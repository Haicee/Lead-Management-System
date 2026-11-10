using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using LeadManagement.Desktop.ViewModels;

namespace LeadManagement.Desktop.Models
{
    /// <summary>
    /// Represents a client record from the `clients` table.
    /// Inherits from BaseViewModel to support property change notifications in WPF.
    /// </summary>
    public class ClientModel : BaseViewModel
    {
        private int _id;
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private string _createdAt = string.Empty;
        private string _updatedAt = string.Empty;
        private List<ClientFileModel> _files = new();
        private List<UserModel> _assignedAgents = new();
        private bool _isExpanded;
        private bool _isAssignDropdownOpen;
        private ObservableCollection<object> _agentSelectionItems = new();

        [JsonPropertyName("id")]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [JsonPropertyName("first_name")]
        public string FirstName
        {
            get => _firstName;
            set
            {
                if (SetProperty(ref _firstName, value))
                {
                    OnPropertyChanged(nameof(FullName));
                }
            }
        }

        [JsonPropertyName("last_name")]
        public string LastName
        {
            get => _lastName;
            set
            {
                if (SetProperty(ref _lastName, value))
                {
                    OnPropertyChanged(nameof(FullName));
                }
            }
        }

        [JsonPropertyName("created_at")]
        public string CreatedAt
        {
            get => _createdAt;
            set
            {
                if (SetProperty(ref _createdAt, value))
                {
                    OnPropertyChanged(nameof(FormattedCreatedDate));
                }
            }
        }

        [JsonPropertyName("updated_at")]
        public string UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
        }

        [JsonPropertyName("files")]
        public List<ClientFileModel> Files
        {
            get => _files;
            set
            {
                if (SetProperty(ref _files, value))
                {
                    OnPropertyChanged(nameof(FileCount));
                    OnPropertyChanged(nameof(TotalLeads));
                }
            }
        }

        [JsonPropertyName("assigned_agents")]
        public List<UserModel> AssignedAgents
        {
            get => _assignedAgents;
            set => SetProperty(ref _assignedAgents, value);
        }

        // Computed helpers
        [JsonIgnore]
        public string FullName
        {
            get => $"{FirstName} {LastName}".Trim();
            set { }
        }

        [JsonIgnore]
        public int FileCount
        {
            get => Files?.Count ?? 0;
            set { }
        }

        [JsonIgnore]
        public int TotalLeads
        {
            get => Files?.Sum(f => f.TotalLeads) ?? 0;
            set { }
        }

        [JsonIgnore]
        public string FormattedCreatedDate
        {
            get
            {
                if (DateTime.TryParse(CreatedAt, out var dt))
                    return dt.ToString("MMM dd, yyyy");
                return CreatedAt;
            }
            set { }
        }

        // --- UI state (not persisted) ---

        [JsonIgnore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        [JsonIgnore]
        public bool IsAssignDropdownOpen
        {
            get => _isAssignDropdownOpen;
            set => SetProperty(ref _isAssignDropdownOpen, value);
        }

        /// <summary>
        /// Bindable list of agents with IsSelected flags, populated by ManageClientsViewModel.
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<object> AgentSelectionItems
        {
            get => _agentSelectionItems;
            set => SetProperty(ref _agentSelectionItems, value);
        }

        // --- Client Leads Table & Real-Time Sync State ---

        private ObservableCollection<LeadModel> _leads = new();
        private ObservableCollection<LeadModel> _filteredLeads = new();
        private string _leadSearchText = string.Empty;
        private bool _isLoadingLeads;
        private ObservableCollection<string> _assignedAgentNames = new();

        [JsonIgnore]
        public ObservableCollection<LeadModel> Leads
        {
            get => _leads;
            set
            {
                if (SetProperty(ref _leads, value))
                {
                    FilterLeads();
                    OnPropertyChanged(nameof(HasLeads));
                }
            }
        }

        [JsonIgnore]
        public ObservableCollection<LeadModel> FilteredLeads
        {
            get => _filteredLeads;
            set => SetProperty(ref _filteredLeads, value);
        }

        [JsonIgnore]
        public string LeadSearchText
        {
            get => _leadSearchText;
            set
            {
                if (SetProperty(ref _leadSearchText, value))
                {
                    FilterLeads();
                }
            }
        }

        [JsonIgnore]
        public bool IsLoadingLeads
        {
            get => _isLoadingLeads;
            set => SetProperty(ref _isLoadingLeads, value);
        }

        [JsonIgnore]
        public bool HasLeads => (Leads != null && Leads.Count > 0) || TotalLeads > 0;

        [JsonIgnore]
        public ObservableCollection<string> AssignedAgentNames
        {
            get => _assignedAgentNames;
            set => SetProperty(ref _assignedAgentNames, value);
        }

        public void FilterLeads()
        {
            if (Leads == null || Leads.Count == 0)
            {
                FilteredLeads = new ObservableCollection<LeadModel>();
                return;
            }

            var q = LeadSearchText?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(q))
            {
                FilteredLeads = new ObservableCollection<LeadModel>(Leads);
            }
            else
            {
                var filtered = System.Linq.Enumerable.Where(Leads, l =>
                    (l.BusinessName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.ContactName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.City?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.State?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.CellNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.CallDisposition?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.AssignedAgent?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (l.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
                FilteredLeads = new ObservableCollection<LeadModel>(filtered);
            }
        }
    }
}
