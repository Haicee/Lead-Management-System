using System;
using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    /// <summary>
    /// Represents an assigned lead list (e.g., List 01, List 02) for a client from `client_lists`.
    /// </summary>
    public class ClientListModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("client_id")]
        public int ClientId { get; set; }

        [JsonPropertyName("list_name")]
        public string ListName { get; set; } = string.Empty;

        [JsonPropertyName("total_leads")]
        public int TotalLeads { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonIgnore]
        public string ClientName { get; set; } = string.Empty;

        [JsonIgnore]
        public string FormattedDate
        {
            get
            {
                if (DateTime.TryParse(CreatedAt, out var dt))
                    return dt.ToString("MMM dd, yyyy HH:mm");
                return CreatedAt;
            }
        }

        [JsonIgnore]
        public string FormattedLeadCount => $"{TotalLeads:N0} leads";
    }
}
