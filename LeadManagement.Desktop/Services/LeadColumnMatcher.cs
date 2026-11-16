using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LeadManagement.Desktop.Models;

namespace LeadManagement.Desktop.Services
{
    /// <summary>
    /// Intelligent header matching and data sanitization engine for Lead spreadsheet imports.
    /// Accurately matches columns based on aliases, prevents collisions, and self-corrects
    /// swapped codes/descriptions (e.g. NAICS code vs description, SIC code vs description).
    /// </summary>
    public static class LeadColumnMatcher
    {
        public class ColumnMapping
        {
            public int BusinessNameIndex { get; set; } = -1;
            public int ContactNameIndex { get; set; } = -1;
            public int TitleIndex { get; set; } = -1;
            public int AddressIndex { get; set; } = -1;
            public int CityIndex { get; set; } = -1;
            public int CountyIndex { get; set; } = -1;
            public int StateIndex { get; set; } = -1;
            public int ZipCodeIndex { get; set; } = -1;
            public int CellNumberIndex { get; set; } = -1;
            public int FaxIndex { get; set; } = -1;
            public int AnnualSalesIndex { get; set; } = -1;
            public int NumberOfEmployeesIndex { get; set; } = -1;
            public int WebsiteIndex { get; set; } = -1;
            public int EmailIndex { get; set; } = -1;
            public int SicCodeIndex { get; set; } = -1;
            public int SicDescriptionIndex { get; set; } = -1;
            public int NaicsCodeIndex { get; set; } = -1;
            public int NaicsDescriptionIndex { get; set; } = -1;
            public int StatusIndex { get; set; } = -1;

            // Extra CRM fields (for client leads)
            public int CallDateIndex { get; set; } = -1;
            public int CallDispositionIndex { get; set; } = -1;
            public int AssignedAgentIndex { get; set; } = -1;
            public int NotesIndex { get; set; } = -1;
            public int CallCountIndex { get; set; } = -1;
        }

        /// <summary>
        /// Normalizes raw header strings for flexible comparison.
        /// </summary>
        public static string NormalizeHeader(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var text = raw.Trim().ToLowerInvariant();
            text = text.Replace("_", " ").Replace("-", " ").Replace(".", " ").Replace("/", " ").Replace("\\", " ");
            text = Regex.Replace(text, @"[^\w\s]", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }

        /// <summary>
        /// Analyzes a list of normalized column headers and identifies the best column index for each lead field.
        /// Guaranteed: No cross-field contamination (e.g. Business Name will never steal Contact Name).
        /// </summary>
        public static ColumnMapping MapColumns(IReadOnlyList<string> headers)
        {
            var mapping = new ColumnMapping();
            int count = headers.Count;
            if (count == 0) return mapping;

            var normalized = headers.Select(NormalizeHeader).ToList();
            var claimed = new HashSet<int>();

            int MatchBest(string[] exactAliases, string[] looseAliases, string[] excludeKeywords)
            {
                // Pass 1: Exact alias matches among unclaimed columns
                for (int i = 0; i < count; i++)
                {
                    if (claimed.Contains(i)) continue;
                    var h = normalized[i];
                    if (string.IsNullOrEmpty(h)) continue;

                    // If header contains any forbidden keyword, skip
                    if (excludeKeywords.Any(ex => h.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    foreach (var exact in exactAliases)
                    {
                        if (string.Equals(h, exact, StringComparison.OrdinalIgnoreCase))
                        {
                            claimed.Add(i);
                            return i;
                        }
                    }
                }

                // Pass 2: Loose substring matches among unclaimed columns
                for (int i = 0; i < count; i++)
                {
                    if (claimed.Contains(i)) continue;
                    var h = normalized[i];
                    if (string.IsNullOrEmpty(h)) continue;

                    if (excludeKeywords.Any(ex => h.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    foreach (var loose in looseAliases)
                    {
                        if (h.Contains(loose, StringComparison.OrdinalIgnoreCase))
                        {
                            claimed.Add(i);
                            return i;
                        }
                    }
                }

                return -1;
            }

            // 1. High-precision Business Name (must not match contact/person)
            mapping.BusinessNameIndex = MatchBest(
                new[] { "business name", "company name", "business", "company", "businessname", "companyname", "account name", "organization", "firm name", "legal name", "client business", "biz name" },
                new[] { "business name", "company name", "account name", "firm name", "business", "company" },
                new[] { "contact", "person", "owner", "individual", "rep", "agent", "title", "sic", "naics" }
            );

            // 2. High-precision Contact Name (must not match business/company)
            mapping.ContactNameIndex = MatchBest(
                new[] { "contact name", "contact person", "contact_name", "contactname", "full name", "fullname", "person name", "person", "contact full name", "customer name", "owner name", "lead contact", "contact", "name" },
                new[] { "contact name", "contact person", "full name", "person name", "contact" },
                new[] { "business", "company", "account", "firm", "sic", "naics", "title", "address", "city" }
            );

            // Fallback: If header is literally "name" and unclaimed, use as Contact Name
            if (mapping.ContactNameIndex < 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!claimed.Contains(i) && normalized[i] == "name")
                    {
                        claimed.Add(i);
                        mapping.ContactNameIndex = i;
                        break;
                    }
                }
            }

            // 3. SIC Code vs Description
            mapping.SicCodeIndex = MatchBest(
                new[] { "sic code", "siccode", "primary sic code", "primary sic", "sic" },
                new[] { "sic code", "siccode" },
                new[] { "description", "desc", "title", "name", "industry", "heading" }
            );

            mapping.SicDescriptionIndex = MatchBest(
                new[] { "sic description", "sic desc", "sic description heading", "sic title", "sic name", "sic industry", "line of business", "industry" },
                new[] { "sic description", "sic desc", "sic industry", "industry" },
                new[] { "code", "num", "id" }
            );

            // If SIC code was still not found but there's a column with just "sic", claim it as code
            if (mapping.SicCodeIndex < 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!claimed.Contains(i) && normalized[i] == "sic")
                    {
                        claimed.Add(i);
                        mapping.SicCodeIndex = i;
                        break;
                    }
                }
            }

            // 4. NAICS Code vs Description
            mapping.NaicsCodeIndex = MatchBest(
                new[] { "naics code", "naicscode", "primary naics code", "primary naics", "naics" },
                new[] { "naics code", "naicscode" },
                new[] { "description", "desc", "title", "name", "industry" }
            );

            mapping.NaicsDescriptionIndex = MatchBest(
                new[] { "naics description", "naics desc", "naics title", "naics name", "naics industry", "naics industry description" },
                new[] { "naics description", "naics desc", "naics industry" },
                new[] { "code", "num", "id" }
            );

            // If NAICS code was still not found but there's a column with just "naics", claim it as code
            if (mapping.NaicsCodeIndex < 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!claimed.Contains(i) && normalized[i] == "naics")
                    {
                        claimed.Add(i);
                        mapping.NaicsCodeIndex = i;
                        break;
                    }
                }
            }

            // 5. Title / Position
            mapping.TitleIndex = MatchBest(
                new[] { "title", "job title", "job_title", "jobtitle", "position", "contact title", "owner title", "role", "designation" },
                new[] { "job title", "position", "title" },
                new[] { "sic", "naics", "book", "page" }
            );

            // 6. Cell Number / Phone
            mapping.CellNumberIndex = MatchBest(
                new[] { "cell number", "cell phone", "phone number", "phone no", "phone_no", "phoneno", "phone", "cell", "telephone", "mobile", "tel", "primary phone" },
                new[] { "phone", "cell", "mobile", "telephone" },
                new[] { "fax" }
            );

            // 7. Fax
            mapping.FaxIndex = MatchBest(
                new[] { "fax", "fax number", "fax_no", "facsimile", "faxphone" },
                new[] { "fax" },
                Array.Empty<string>()
            );

            // 8. Address
            mapping.AddressIndex = MatchBest(
                new[] { "address", "street address", "street", "address 1", "address1", "billing address", "physical address", "location address" },
                new[] { "address", "street" },
                new[] { "email", "web", "ip", "state", "city", "county" }
            );

            // 9. City
            mapping.CityIndex = MatchBest(
                new[] { "city", "town", "municipality", "locality" },
                new[] { "city", "town" },
                Array.Empty<string>()
            );

            // 10. County
            mapping.CountyIndex = MatchBest(
                new[] { "county", "parish", "borough" },
                new[] { "county" },
                new[] { "country" }
            );

            // 11. State
            mapping.StateIndex = MatchBest(
                new[] { "state", "province", "st", "region" },
                new[] { "state" },
                new[] { "status" }
            );

            // 12. Zip Code
            mapping.ZipCodeIndex = MatchBest(
                new[] { "zip code", "zip_code", "zip", "zipcode", "postal code", "postal", "postcode", "pincode" },
                new[] { "zip", "postal" },
                Array.Empty<string>()
            );

            // 13. Annual Sales
            mapping.AnnualSalesIndex = MatchBest(
                new[] { "annual sales", "annual revenue", "sales", "revenue", "sales volume", "annualsales", "turnover" },
                new[] { "annual sales", "revenue", "sales" },
                Array.Empty<string>()
            );

            // 14. Number of Employees
            mapping.NumberOfEmployeesIndex = MatchBest(
                new[] { "number of employee", "number of employees", "no of employees", "no employee", "no_employee", "no employees", "employees", "employee count", "num employees", "headcount", "employee" },
                new[] { "employee", "headcount" },
                Array.Empty<string>()
            );

            // 15. Website
            mapping.WebsiteIndex = MatchBest(
                new[] { "website", "web", "url", "site", "domain", "web address", "company website", "web page", "homepage" },
                new[] { "website", "web", "url", "domain" },
                new[] { "email" }
            );

            // 16. Email
            mapping.EmailIndex = MatchBest(
                new[] { "email", "e mail", "email address", "contact email" },
                new[] { "email", "e mail" },
                Array.Empty<string>()
            );

            // 17. Status
            mapping.StatusIndex = MatchBest(
                new[] { "status", "lead status", "pipeline status" },
                new[] { "status" },
                new[] { "state" }
            );

            // 18. CRM / Client fields
            mapping.CallDateIndex = MatchBest(
                new[] { "date", "call date", "appointment date", "lead date" },
                new[] { "date" },
                Array.Empty<string>()
            );

            mapping.CallDispositionIndex = MatchBest(
                new[] { "call disposition", "call composition", "disposition", "composition", "call status", "dispo" },
                new[] { "disposition", "composition" },
                Array.Empty<string>()
            );

            mapping.AssignedAgentIndex = MatchBest(
                new[] { "agent assigned", "assigned agent", "agent name", "agent" },
                new[] { "agent" },
                Array.Empty<string>()
            );

            mapping.NotesIndex = MatchBest(
                new[] { "notes", "note", "call notes", "comments", "remarks" },
                new[] { "notes", "note", "comment" },
                Array.Empty<string>()
            );

            mapping.CallCountIndex = MatchBest(
                new[] { "of cal", "# of cal", "# of call", "# of calls", "call count", "number of call", "number of calls", "calls" },
                new[] { "call count", "calls" },
                Array.Empty<string>()
            );

            return mapping;
        }

        private static bool LooksLikeNumericCode(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var trimmed = s.Trim();
            int digits = trimmed.Count(char.IsDigit);
            int letters = trimmed.Count(char.IsLetter);
            return digits >= 2 && letters == 0;
        }

        private static bool LooksLikeTextDescription(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            return s.Trim().Any(char.IsLetter);
        }

        /// <summary>
        /// Populates a LeadModel from parsed row parts using the column mapping,
        /// and applies intelligent self-healing / validation for swapped codes and descriptions.
        /// </summary>
        public static LeadModel BuildLead(
            string[] parts,
            ColumnMapping mapping,
            int rowId,
            string defaultStatus = "New Lead",
            string batchName = "")
        {
            string Get(int index, string def = "")
            {
                if (index >= 0 && index < parts.Length)
                {
                    var val = parts[index].Trim();
                    return string.IsNullOrEmpty(val) ? def : val;
                }
                return def;
            }

            var bName = Get(mapping.BusinessNameIndex);
            if (string.IsNullOrWhiteSpace(bName))
            {
                bName = $"Unnamed Business #{rowId}";
            }

            var cName = Get(mapping.ContactNameIndex);
            var title = Get(mapping.TitleIndex);
            var sicCode = Get(mapping.SicCodeIndex);
            var sicDesc = Get(mapping.SicDescriptionIndex);
            var naicsCode = Get(mapping.NaicsCodeIndex);
            var naicsDesc = Get(mapping.NaicsDescriptionIndex);

            // ─── SELF-HEALING 1: NAICS Code vs Description Swapped ───
            // If the NAICS Code field contains words/letters (e.g. "INSURANCE AGENCIES") 
            // and NAICS Description contains digits (e.g. "524210"): SWAP THEM!
            if (LooksLikeTextDescription(naicsCode) && LooksLikeNumericCode(naicsDesc))
            {
                var temp = naicsCode;
                naicsCode = naicsDesc;
                naicsDesc = temp;
            }
            else if (!LooksLikeNumericCode(naicsCode) && LooksLikeNumericCode(naicsDesc))
            {
                // naicsCode is empty or text, but naicsDesc has the code
                if (string.IsNullOrEmpty(naicsCode))
                {
                    naicsCode = naicsDesc;
                    naicsDesc = string.Empty;
                }
            }

            // If NAICS Code and NAICS Description are identical digits (e.g. both "561790")
            if (naicsCode.Equals(naicsDesc, StringComparison.OrdinalIgnoreCase) && LooksLikeNumericCode(naicsCode))
            {
                // NAICS Description should be empty or found in another cell rather than duplicating the code
                naicsDesc = string.Empty;
            }

            // ─── SELF-HEALING 2: SIC Code vs Description Swapped ───
            // If the SIC Code field contains text (e.g. "CLEANING SERVICES") 
            // and SIC Description contains numeric digits (e.g. "76991700"): SWAP THEM!
            if (LooksLikeTextDescription(sicCode) && LooksLikeNumericCode(sicDesc))
            {
                var temp = sicCode;
                sicCode = sicDesc;
                sicDesc = temp;
            }
            else if (LooksLikeTextDescription(sicCode) && string.IsNullOrEmpty(sicDesc))
            {
                // SIC Code has description text, and description is empty -> move it to description
                sicDesc = sicCode;
                sicCode = string.Empty;
            }
            else if (sicCode.Equals(sicDesc, StringComparison.OrdinalIgnoreCase) && LooksLikeTextDescription(sicCode))
            {
                // Both contain description text (e.g. "CLEANING SERVICES")
                // Keep description, search row for actual numeric SIC code if available
                string foundCode = string.Empty;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i == mapping.CellNumberIndex || i == mapping.ZipCodeIndex || i == mapping.FaxIndex)
                        continue;
                    var p = parts[i].Trim();
                    if (LooksLikeNumericCode(p) && p.Length >= 4 && p.Length <= 8 && p != naicsCode)
                    {
                        foundCode = p;
                        break;
                    }
                }
                sicCode = foundCode;
            }

            // ─── SELF-HEALING 3: Contact Name vs Business Name ───
            // Contact Name must not be header artifacts or duplicate of business name if business name was copied
            if (!string.IsNullOrWhiteSpace(cName))
            {
                var clean = cName.Trim();
                if (clean.Equals("contact name", StringComparison.OrdinalIgnoreCase) ||
                    clean.Equals("contact", StringComparison.OrdinalIgnoreCase) ||
                    clean.Equals("contact_name", StringComparison.OrdinalIgnoreCase) ||
                    clean.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    cName = string.Empty;
                }
                else if (clean.Equals(bName, StringComparison.OrdinalIgnoreCase))
                {
                    // If contact name is literally identical to business name, check if there's an actual contact column
                    // or if it was duplicated by a bad prior import
                    cName = string.Empty;
                }
            }

            var statusVal = Get(mapping.StatusIndex);
            if (string.IsNullOrWhiteSpace(statusVal))
            {
                statusVal = defaultStatus;
            }

            return new LeadModel
            {
                Id                = rowId,
                BatchName         = batchName,
                BusinessName      = bName,
                ContactName       = cName,
                Title             = title,
                Address           = Get(mapping.AddressIndex),
                City              = Get(mapping.CityIndex),
                County            = Get(mapping.CountyIndex),
                State             = Get(mapping.StateIndex, "NE"),
                ZipCode           = Get(mapping.ZipCodeIndex),
                CellNumber        = Get(mapping.CellNumberIndex),
                Fax               = Get(mapping.FaxIndex),
                AnnualSales       = Get(mapping.AnnualSalesIndex, "Less than 500,000"),
                NumberOfEmployees = Get(mapping.NumberOfEmployeesIndex, "1 to 4"),
                Website           = Get(mapping.WebsiteIndex),
                Email             = Get(mapping.EmailIndex),
                SicCode           = sicCode,
                SicDescription    = sicDesc,
                NaicsCode         = naicsCode,
                NaicsDescription  = naicsDesc,
                Status            = statusVal
            };
        }
    }
}
