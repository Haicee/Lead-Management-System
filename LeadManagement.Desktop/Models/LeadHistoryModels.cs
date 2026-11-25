using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    public class LeadAssignedClientInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("list_name")]
        public string? ListName { get; set; }

        [JsonPropertyName("assigned_at")]
        public string AssignedAt { get; set; } = string.Empty;

        [JsonIgnore]
        public string DisplayLabel => string.IsNullOrWhiteSpace(ListName) ? FullName : $"{FullName} ({ListName})";
    }

    public class LeadCallHistoryEntry
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("lead_id")]
        public int LeadId { get; set; }

        [JsonPropertyName("agent_id")]
        public int? AgentId { get; set; }

        [JsonPropertyName("agent_name")]
        public string AgentName { get; set; } = string.Empty;

        [JsonPropertyName("client_names")]
        public string ClientNames { get; set; } = string.Empty;

        [JsonPropertyName("call_disposition")]
        public string CallDisposition { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        [JsonPropertyName("call_number")]
        public int CallNumber { get; set; }

        [JsonPropertyName("called_at")]
        public string CalledAt { get; set; } = string.Empty;

        [JsonIgnore]
        public string FormattedCalledAt
        {
            get
            {
                if (DateTime.TryParse(CalledAt, out var dt))
                    return dt.ToString("MMM dd, yyyy h:mm tt");
                return CalledAt;
            }
        }

        [JsonIgnore]
        public bool HasDisposition => !string.IsNullOrWhiteSpace(CallDisposition);

        [JsonIgnore]
        public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

        [JsonIgnore]
        public CallDispositionItem DispositionInfo => CallDispositionHelper.GetDisposition(CallDisposition);

        [JsonIgnore]
        public string DisplayNotes => string.IsNullOrWhiteSpace(Notes) ? "---" : Notes;
    }

    public class LeadHistoryData
    {
        [JsonPropertyName("lead_id")]
        public int LeadId { get; set; }

        [JsonPropertyName("assigned_clients")]
        public List<LeadAssignedClientInfo> AssignedClients { get; set; } = new();

        [JsonPropertyName("history")]
        public List<LeadCallHistoryEntry> History { get; set; } = new();
    }
}
