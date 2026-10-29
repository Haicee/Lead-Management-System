using System;
using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    public class LeadModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("batch_id")]
        public int? BatchId { get; set; }

        [JsonPropertyName("batch_name")]
        public string BatchName { get; set; } = string.Empty;

        [JsonPropertyName("business_name")]
        public string BusinessName { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("county")]
        public string County { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = "NE";

        [JsonPropertyName("zip_code")]
        public string ZipCode { get; set; } = string.Empty;

        [JsonPropertyName("cell_number")]
        public string CellNumber { get; set; } = string.Empty;

        [JsonPropertyName("fax")]
        public string Fax { get; set; } = string.Empty;

        [JsonPropertyName("annual_sales")]
        public string AnnualSales { get; set; } = "Less than 500,000";

        [JsonPropertyName("number_of_employees")]
        public string NumberOfEmployees { get; set; } = "1 to 4";

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("contact_name")]
        public string ContactName { get; set; } = string.Empty;

        [JsonPropertyName("website")]
        public string Website { get; set; } = string.Empty;

        [JsonPropertyName("sic_code")]
        public string SicCode { get; set; } = string.Empty;

        [JsonPropertyName("sic_description")]
        public string SicDescription { get; set; } = string.Empty;

        [JsonPropertyName("naics_code")]
        public string NaicsCode { get; set; } = string.Empty;

        [JsonPropertyName("naics_description")]
        public string NaicsDescription { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "New Lead"; // New Lead, Qualified, Follow-up, Contacted

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
