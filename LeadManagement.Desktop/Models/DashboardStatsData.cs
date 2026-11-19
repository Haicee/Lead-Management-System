using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    public class DashboardStatsData
    {
        [JsonPropertyName("total_leads")]
        public long TotalLeads { get; set; }

        [JsonPropertyName("masterlist_leads")]
        public long MasterlistLeads { get; set; }

        [JsonPropertyName("client_leads")]
        public long ClientLeads { get; set; }

        [JsonPropertyName("total_clients")]
        public int TotalClients { get; set; }

        [JsonPropertyName("total_users")]
        public int TotalUsers { get; set; }

        [JsonPropertyName("activities")]
        public List<AdminActivityModel> Activities { get; set; } = new();
    }
}
