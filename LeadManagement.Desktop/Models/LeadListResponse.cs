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
