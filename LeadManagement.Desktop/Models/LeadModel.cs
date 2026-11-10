using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LeadManagement.Desktop.ViewModels;

namespace LeadManagement.Desktop.Models
{
    public class LeadModel : BaseViewModel
    {
        private int _id;
        private int? _batchId;
        private string _batchName = string.Empty;
        private int? _clientId;
        private string _callDate = string.Empty;
        private string _callDisposition = string.Empty;
        private int? _assignedAgentId;
        private string _assignedAgent = string.Empty;
        private string _notes = string.Empty;
        private int _callCount = 1;
        private string _businessName = string.Empty;
        private string _address = string.Empty;
        private string _city = string.Empty;
        private string _county = string.Empty;
        private string _state = "NE";
        private string _zipCode = string.Empty;
        private string _cellNumber = string.Empty;
        private string _email = string.Empty;
        private string _fax = string.Empty;
        private string _annualSales = "Less than 500,000";
        private string _numberOfEmployees = "1 to 4";
        private string _title = string.Empty;
        private string _contactName = string.Empty;
        private string _website = string.Empty;
        private string _sicCode = string.Empty;
        private string _sicDescription = string.Empty;
        private string _naicsCode = string.Empty;
        private string _naicsDescription = string.Empty;
        private string _status = "New Lead";
        private IEnumerable<string>? _availableAgentOptions;

        /// <summary>
        /// Callback fired when any editable field (Date, Disposition, Agent, Notes, Call Count) is modified.
        /// Used by the ViewModel to auto-sync changes to the backend in real time.
        /// </summary>
        [JsonIgnore]
        public Action<LeadModel, string>? OnFieldChanged { get; set; }

        private void NotifyChange(string fieldName)
        {
            OnFieldChanged?.Invoke(this, fieldName);
        }

        [JsonPropertyName("id")]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [JsonPropertyName("batch_id")]
        public int? BatchId
        {
            get => _batchId;
            set => SetProperty(ref _batchId, value);
        }

        [JsonPropertyName("batch_name")]
        public string BatchName
        {
            get => _batchName;
            set => SetProperty(ref _batchName, value);
        }

        [JsonPropertyName("client_id")]
        public int? ClientId
        {
            get => _clientId;
            set => SetProperty(ref _clientId, value);
        }

        [JsonPropertyName("call_date")]
        public string CallDate
        {
            get => _callDate;
            set
            {
                if (SetProperty(ref _callDate, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(SelectedDate));
                }
            }
        }

        /// <summary>
        /// Two-way bindable DateTime? for DatePicker UI with calendar popup.
        /// </summary>
        [JsonIgnore]
        public DateTime? SelectedDate
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CallDate)) return null;
                if (DateTime.TryParse(CallDate, out var dt)) return dt;
                return null;
            }
            set
            {
                var newDateStr = value?.ToString("MM/dd/yy") ?? string.Empty;
                if (CallDate != newDateStr)
                {
                    CallDate = newDateStr;
                    NotifyChange("call_date");
                }
            }
        }

        [JsonPropertyName("call_disposition")]
        public string CallDisposition
        {
            get => _callDisposition;
            set
            {
                if (SetProperty(ref _callDisposition, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(DispositionInfo));
                    NotifyChange("call_disposition");
                }
            }
        }

        /// <summary>
        /// Returns color styling information matching the selected call disposition.
        /// </summary>
        [JsonIgnore]
        public CallDispositionItem DispositionInfo => CallDispositionHelper.GetDisposition(CallDisposition);

        [JsonPropertyName("assigned_agent_id")]
        public int? AssignedAgentId
        {
            get => _assignedAgentId;
            set => SetProperty(ref _assignedAgentId, value);
        }

        [JsonPropertyName("assigned_agent")]
        public string AssignedAgent
        {
            get => _assignedAgent;
            set
            {
                if (SetProperty(ref _assignedAgent, value ?? string.Empty))
                {
                    NotifyChange("assigned_agent");
                }
            }
        }

        [JsonPropertyName("notes")]
        public string Notes
        {
            get => _notes;
            set
            {
                if (SetProperty(ref _notes, value ?? string.Empty))
                {
                    NotifyChange("notes");
                }
            }
        }

        [JsonPropertyName("call_count")]
        public int CallCount
        {
            get => _callCount;
            set
            {
                if (SetProperty(ref _callCount, value))
                {
                    NotifyChange("call_count");
                }
            }
        }

        /// <summary>
        /// Dynamically supplied list of assigned agent names for this lead's client.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string>? AvailableAgentOptions
        {
            get => _availableAgentOptions;
            set => SetProperty(ref _availableAgentOptions, value);
        }

        [JsonPropertyName("business_name")]
        public string BusinessName
        {
            get => _businessName;
            set => SetProperty(ref _businessName, value);
        }

        [JsonPropertyName("address")]
        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        [JsonPropertyName("city")]
        public string City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }

        [JsonPropertyName("county")]
        public string County
        {
            get => _county;
            set => SetProperty(ref _county, value);
        }

        [JsonPropertyName("state")]
        public string State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        [JsonPropertyName("zip_code")]
        public string ZipCode
        {
            get => _zipCode;
            set => SetProperty(ref _zipCode, value);
        }

        [JsonPropertyName("cell_number")]
        public string CellNumber
        {
            get => _cellNumber;
            set
            {
                if (SetProperty(ref _cellNumber, value))
                {
                    OnPropertyChanged(nameof(FormattedPhone));
                }
            }
        }

        [JsonPropertyName("email")]
        public string Email
        {
            get => _email;
            set
            {
                if (SetProperty(ref _email, value ?? string.Empty))
                {
                    NotifyChange("email");
                }
            }
        }

        [JsonPropertyName("fax")]
        public string Fax
        {
            get => _fax;
            set => SetProperty(ref _fax, value);
        }

        [JsonPropertyName("annual_sales")]
        public string AnnualSales
        {
            get => _annualSales;
            set => SetProperty(ref _annualSales, value);
        }

        [JsonPropertyName("number_of_employees")]
        public string NumberOfEmployees
        {
            get => _numberOfEmployees;
            set => SetProperty(ref _numberOfEmployees, value);
        }

        [JsonPropertyName("title")]
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        [JsonPropertyName("contact_name")]
        public string ContactName
        {
            get => _contactName;
            set => SetProperty(ref _contactName, value);
        }

        [JsonPropertyName("website")]
        public string Website
        {
            get => _website;
            set => SetProperty(ref _website, value);
        }

        [JsonPropertyName("sic_code")]
        public string SicCode
        {
            get => _sicCode;
            set => SetProperty(ref _sicCode, value);
        }

        [JsonPropertyName("sic_description")]
        public string SicDescription
        {
            get => _sicDescription;
            set => SetProperty(ref _sicDescription, value);
        }

        [JsonPropertyName("naics_code")]
        public string NaicsCode
        {
            get => _naicsCode;
            set => SetProperty(ref _naicsCode, value);
        }

        [JsonPropertyName("naics_description")]
        public string NaicsDescription
        {
            get => _naicsDescription;
            set => SetProperty(ref _naicsDescription, value);
        }

        [JsonPropertyName("status")]
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        [JsonIgnore]
        public string FullLocation => $"{City}, {State} {ZipCode}".Trim(',', ' ');

        [JsonIgnore]
        public string FormattedPhone
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CellNumber)) return "—";
                var digits = System.Text.RegularExpressions.Regex.Replace(CellNumber, @"[^\d]", "");
                if (digits.Length == 10)
                {
                    return $"({digits[..3]}) {digits.Substring(3, 3)}-{digits.Substring(6, 4)}";
                }
                return CellNumber;
            }
        }
    }
}
