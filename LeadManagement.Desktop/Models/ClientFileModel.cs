using System;
using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    /// <summary>
    /// Represents an uploaded file record from the `client_files` table.
    /// </summary>
    public class ClientFileModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("client_id")]
        public int ClientId { get; set; }

        [JsonPropertyName("client_name")]
        public string ClientName { get; set; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("file_size")]
        public string FileSize { get; set; } = string.Empty;

        [JsonPropertyName("total_leads")]
        public int TotalLeads { get; set; }

        [JsonPropertyName("batch_id")]
        public int? BatchId { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        // Computed helpers
        [JsonIgnore]
        public string FormattedDate
        {
            get
            {
                if (DateTime.TryParse(CreatedAt, out var dt))
                    return dt.ToString("MMM dd, yyyy HH:mm");
                return CreatedAt;
            }
            set { }
        }

        [JsonIgnore]
        public string FormattedLeadCount
        {
            get => $"{TotalLeads:N0} leads";
            set { }
        }
    }
}

