using System;
using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    public class UserModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = "user"; // "admin" or "user"

        [JsonPropertyName("is_active")]
        public int IsActive { get; set; } = 1;

        [JsonPropertyName("last_login_at")]
        public string? LastLoginAt { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonIgnore]
        public bool IsAdmin => Role?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;

        [JsonIgnore]
        public string RoleDisplay => IsAdmin ? "Administrator" : "Standard User (Agent)";

        [JsonIgnore]
        public string StatusDisplay => IsActive == 1 ? "Active" : "Inactive";

        [JsonIgnore]
        public string FormattedLastLogin
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LastLoginAt)) return "Never";
                if (DateTime.TryParse(LastLoginAt, out var dt))
                {
                    return dt.ToString("MMM dd, yyyy h:mm tt");
                }
                return LastLoginAt;
            }
        }

        [JsonIgnore]
        public string FormattedCreatedDate
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CreatedAt)) return "—";
                if (DateTime.TryParse(CreatedAt, out var dt))
                {
                    return dt.ToString("MMM dd, yyyy");
                }
                return CreatedAt;
            }
        }
    }
}

