using System;
using System.Collections.Generic;
using System.Linq;

namespace LeadManagement.Desktop.Models
{
    /// <summary>
    /// Represents a call disposition option with its color styling matching the Google Sheets palette.
    /// </summary>
    public class CallDispositionItem
    {
        public string Name { get; set; } = string.Empty;
        public string BackgroundHex { get; set; } = "#F1F5F9";
        public string ForegroundHex { get; set; } = "#0F172A";
        public string BorderHex { get; set; } = "#CBD5E1";

        public override string ToString() => Name;
    }

    /// <summary>
    /// Master list and lookup helper for all 18 call dispositions with exact color coding.
    /// </summary>
    public static class CallDispositionHelper
    {
        public static readonly List<CallDispositionItem> Dispositions = new()
        {
            new CallDispositionItem { Name = "Not in Service",             BackgroundHex = "#FECDD3", ForegroundHex = "#9F1239", BorderHex = "#FDA4AF" },
            new CallDispositionItem { Name = "Not interested",             BackgroundHex = "#B91C1C", ForegroundHex = "#FFFFFF", BorderHex = "#991B1B" },
            new CallDispositionItem { Name = "Speak to DM",                 BackgroundHex = "#BBF7D0", ForegroundHex = "#14532D", BorderHex = "#86EFAC" },
            new CallDispositionItem { Name = "Speak to GK",                 BackgroundHex = "#FDE68A", ForegroundHex = "#78350F", BorderHex = "#FCD34D" },
            new CallDispositionItem { Name = "Wrong number",               BackgroundHex = "#FECDD3", ForegroundHex = "#9F1239", BorderHex = "#FDA4AF" },
            new CallDispositionItem { Name = "DNC",                        BackgroundHex = "#991B1B", ForegroundHex = "#FFFFFF", BorderHex = "#7F1D1D" },
            new CallDispositionItem { Name = "Disconnected",               BackgroundHex = "#FECDD3", ForegroundHex = "#9F1239", BorderHex = "#FDA4AF" },
            new CallDispositionItem { Name = "Unable to connect/Complete", BackgroundHex = "#78350F", ForegroundHex = "#FFFFFF", BorderHex = "#5C2607" },
            new CallDispositionItem { Name = "Business sold/closed",       BackgroundHex = "#991B1B", ForegroundHex = "#FFFFFF", BorderHex = "#7F1D1D" },
            new CallDispositionItem { Name = "Retired",                    BackgroundHex = "#991B1B", ForegroundHex = "#FFFFFF", BorderHex = "#7F1D1D" },
            new CallDispositionItem { Name = "Language Barrier",           BackgroundHex = "#334155", ForegroundHex = "#FFFFFF", BorderHex = "#1E293B" },
            new CallDispositionItem { Name = "Appointment Set",            BackgroundHex = "#047857", ForegroundHex = "#FFFFFF", BorderHex = "#065F46" },
            new CallDispositionItem { Name = "Pending",                    BackgroundHex = "#1D4ED8", ForegroundHex = "#FFFFFF", BorderHex = "#1E40AF" },
            new CallDispositionItem { Name = "Callback",                   BackgroundHex = "#BAE6FD", ForegroundHex = "#0369A1", BorderHex = "#7DD3FC" },
            new CallDispositionItem { Name = "Voicemail",                  BackgroundHex = "#6B21A8", ForegroundHex = "#FFFFFF", BorderHex = "#581C87" },
            new CallDispositionItem { Name = "No Answer",                  BackgroundHex = "#DDD6FE", ForegroundHex = "#581C87", BorderHex = "#C4B5FD" },
            new CallDispositionItem { Name = "Not Qualified",              BackgroundHex = "#991B1B", ForegroundHex = "#FFFFFF", BorderHex = "#7F1D1D" },
            new CallDispositionItem { Name = "DEAD",                       BackgroundHex = "#1E293B", ForegroundHex = "#FFFFFF", BorderHex = "#0F172A" }
        };

        private static readonly CallDispositionItem DefaultItem = new()
        {
            Name = "",
            BackgroundHex = "#F1F5F9",
            ForegroundHex = "#64748B",
            BorderHex = "#E2E8F0"
        };

        public static CallDispositionItem GetDisposition(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return DefaultItem;
            var found = Dispositions.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return found ?? new CallDispositionItem
            {
                Name = name,
                BackgroundHex = "#F1F5F9",
                ForegroundHex = "#475569",
                BorderHex = "#CBD5E1"
            };
        }
    }
}

