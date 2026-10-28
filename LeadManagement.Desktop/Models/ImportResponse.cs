using System;
using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    /// <summary>
    /// Response payload from the POST /leads/import endpoint.
    /// </summary>
    public class ImportResultData
    {
        [JsonPropertyName("batch_id")]
        public int BatchId { get; set; }

        [JsonPropertyName("batch_name")]
        public string BatchName { get; set; } = string.Empty;

        [JsonPropertyName("inserted_count")]
        public int InsertedCount { get; set; }

        [JsonPropertyName("total_leads")]
        public int TotalLeads { get; set; }
    }

    /// <summary>
    /// Represents an import batch in the audit trail.
    /// </summary>
    public class ImportBatchModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("batch_name")]
        public string BatchName { get; set; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("total_leads")]
        public int TotalLeads { get; set; }

        [JsonPropertyName("initial_status")]
        public string InitialStatus { get; set; } = "New Lead";

        [JsonPropertyName("imported_by")]
        public int? ImportedBy { get; set; }

        [JsonPropertyName("imported_by_username")]
        public string? ImportedByUsername { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonIgnore]
        public string FormattedDate
        {
            get
            {
                if (DateTime.TryParse(CreatedAt, out var dt))
                {
                    return dt.ToString("MMM dd, yyyy  h:mm tt");
                }
                return CreatedAt;
            }
        }

        [JsonIgnore]
        public string ImportedByDisplay =>
            string.IsNullOrWhiteSpace(ImportedByUsername) ? "System Administrator" : ImportedByUsername;

        [JsonIgnore]
        public string FormattedTotalLeads => $"{TotalLeads:N0} Leads";
    }
}
