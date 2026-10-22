using System;

namespace LeadManagement.Desktop.Models
{
    public class LeadModel
    {
        public int Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string State { get; set; } = "NE";
        public string ZipCode { get; set; } = string.Empty;
        public string CellNumber { get; set; } = string.Empty;
        public string Fax { get; set; } = string.Empty;
        public string AnnualSales { get; set; } = "Less than 500,000";
        public string NumberOfEmployees { get; set; } = "1 to 4";
        public string Title { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string SicCode { get; set; } = string.Empty;
        public string SicDescription { get; set; } = string.Empty;
        public string NaicsCode { get; set; } = string.Empty;
        public string NaicsDescription { get; set; } = string.Empty;
        public string Status { get; set; } = "New Lead"; // New Lead, Qualified, Follow-up, Contacted

        public string FullLocation => $"{City}, {State} {ZipCode}".Trim(',', ' ');

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
