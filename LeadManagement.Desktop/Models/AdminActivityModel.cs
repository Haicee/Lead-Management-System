using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    public class AdminActivityModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("admin_id")]
        public int? AdminId { get; set; }

        [JsonPropertyName("admin_name")]
        public string AdminName { get; set; } = "System Administrator";

        [JsonPropertyName("action_type")]
        public string ActionType { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("item_count")]
        public int? ItemCount { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("date_formatted")]
        public string DateFormatted { get; set; } = string.Empty;

        [JsonPropertyName("time_ago")]
        public string TimeAgo { get; set; } = string.Empty;

        // UI Presentation Helpers
        public bool HasCount => ItemCount.HasValue && ItemCount.Value > 0;

        public string FormattedCount => ItemCount.HasValue ? $"{ItemCount.Value:N0} records" : string.Empty;

        public string ActionBadgeText => ActionType.ToUpperInvariant() switch
        {
            "IMPORT" => "IMPORT",
            "EXPORT" => "EXPORT",
            "DELETE_LEADS" => "DELETED LEADS",
            "CLIENT_MANAGE" => "CLIENT",
            "USER_MANAGE" => "USER ACCOUNT",
            _ => "ACTIVITY"
        };

        public string BadgeBackground => ActionType.ToUpperInvariant() switch
        {
            "IMPORT" => "#EFF6FF",         // Light Blue
            "EXPORT" => "#F3E8FF",         // Light Purple
            "DELETE_LEADS" => "#FEF2F2",   // Light Red / Rose
            "CLIENT_MANAGE" => "#ECFDF5",  // Light Emerald
            "USER_MANAGE" => "#FEF3C7",    // Light Amber
            _ => "#F1F5F9"                 // Slate Gray
        };

        public string BadgeForeground => ActionType.ToUpperInvariant() switch
        {
            "IMPORT" => "#1D4ED8",         // Blue
            "EXPORT" => "#7C3AED",         // Purple
            "DELETE_LEADS" => "#DC2626",   // Red
            "CLIENT_MANAGE" => "#059669",  // Emerald
            "USER_MANAGE" => "#D97706",    // Amber
            _ => "#475569"                 // Slate
        };

        public string ActionIcon => ActionType.ToUpperInvariant() switch
        {
            "IMPORT" => "📥",
            "EXPORT" => "📤",
            "DELETE_LEADS" => "🗑️",
            "CLIENT_MANAGE" => "📁",
            "USER_MANAGE" => "👤",
            _ => "⚡"
        };
    }
}
