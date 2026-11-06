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
        private string? _assignedAt;
        private bool _isExpanded;
        private bool _isAssignDropdownOpen;
        private ObservableCollection<object> _agentSelectionItems = new();

        [JsonPropertyName("id")]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [JsonPropertyName("assigned_at")]
        public string? AssignedAt
        {
            get => _assignedAt;
            set
            {
                if (SetProperty(ref _assignedAt, value))
                {
                    OnPropertyChanged(nameof(FormattedAssignedDate));
                }
            }
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

        [JsonIgnore]
        public string FormattedAssignedDate
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AssignedAt)) return FormattedCreatedDate;
                if (DateTime.TryParse(AssignedAt, out var dt))
                    return dt.ToString("MMM dd, yyyy");
                return AssignedAt;
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
    }
}
