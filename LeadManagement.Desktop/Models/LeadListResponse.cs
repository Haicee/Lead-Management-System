using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    public class LeadListData
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("batch_id")]
        public int? BatchId { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("leads")]
        public List<LeadModel> Leads { get; set; } = new();
    }

    /// <summary>
    /// Response data shape returned by GET /clients/files/{id}/leads.
    /// </summary>
    public class ClientFileLeadsData
    {
        [JsonPropertyName("file_id")]
        public int FileId { get; set; }

        [JsonPropertyName("client_id")]
        public int ClientId { get; set; }

        [JsonPropertyName("client_name")]
        public string ClientName { get; set; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("file_size")]
        public string FileSize { get; set; } = string.Empty;

        [JsonPropertyName("batch_id")]
        public int? BatchId { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("leads")]
        public List<LeadModel> Leads { get; set; } = new();
    }

    /// <summary>
    /// Response data shape returned by GET /clients/{id}/leads.
    /// </summary>
    public class ClientLeadsData
    {
        [JsonPropertyName("client_id")]
        public int ClientId { get; set; }

        [JsonPropertyName("client_name")]
        public string ClientName { get; set; } = string.Empty;

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("leads")]
        public List<LeadModel> Leads { get; set; } = new();
    }

    public class LeadQueryResultData
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("filtered_total")]
        public int FilteredTotal { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("leads")]
        public List<LeadModel> Leads { get; set; } = new();
    }

    public class LeadQueryRequest
    {
        [JsonPropertyName("search")]
        public string? Search { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 1000;

        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;

        [JsonPropertyName("sort_by")]
        public string? SortBy { get; set; }

        [JsonPropertyName("sort_direction")]
        public string? SortDirection { get; set; }

        [JsonPropertyName("batch_id")]
        public int? BatchId { get; set; }

        [JsonPropertyName("cities")]
        public List<string>? Cities { get; set; }

        [JsonPropertyName("counties")]
        public List<string>? Counties { get; set; }

        [JsonPropertyName("states")]
        public List<string>? States { get; set; }

        [JsonPropertyName("statuses")]
        public List<string>? Statuses { get; set; }

        [JsonPropertyName("industries")]
        public List<string>? Industries { get; set; }

        [JsonPropertyName("annual_sales")]
        public List<string>? AnnualSales { get; set; }

        [JsonPropertyName("naics_codes")]
        public List<string>? NaicsCodes { get; set; }

        [JsonPropertyName("naics_descriptions")]
        public List<string>? NaicsDescriptions { get; set; }
    }

    public class LeadFiltersData
    {
        [JsonPropertyName("cities")]
        public List<string> Cities { get; set; } = new();

        [JsonPropertyName("counties")]
        public List<string> Counties { get; set; } = new();

        [JsonPropertyName("states")]
        public List<string> States { get; set; } = new();

        [JsonPropertyName("statuses")]
        public List<string> Statuses { get; set; } = new();

        [JsonPropertyName("industries")]
        public List<string> Industries { get; set; } = new();

        [JsonPropertyName("annual_sales")]
        public List<string> AnnualSales { get; set; } = new();

        [JsonPropertyName("naics_codes")]
        public List<string> NaicsCodes { get; set; } = new();

        [JsonPropertyName("naics_descriptions")]
        public List<string> NaicsDescriptions { get; set; } = new();
    }
}
