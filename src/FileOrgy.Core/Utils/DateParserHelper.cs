using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FileOrgy.Core.Utils
{
    public static class DateParserHelper
    {
        // Targeted invoice / document date regex with prefix keywords
        private static readonly Regex PrefixedDateRegex = new(
            @"(?:invoice\s*date|bill\s*date|statement\s*date|issue\s*date|order\s*date|date\s*of\s*issue|dated|date)[:\s]+([A-Za-z0-9\s,\.\-\/]{6,25})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ISO format: YYYY-MM-DD, YYYY/MM/DD, YYYY.MM.DD
        private static readonly Regex IsoDateRegex = new(
            @"\b(19\d\d|20\d\d)[-/.](0[1-9]|1[0-2])[-/.](0[1-9]|[12]\d|3[01])(?!\d)",
            RegexOptions.Compiled);

        // Standard 01/15/2026 or 15/01/2026
        private static readonly Regex NumericDateRegex = new(
            @"\b(0[1-9]|[12]\d|3[01])[-/.](0[1-9]|1[0-2])[-/.](19\d\d|20\d\d)\b|\b(0[1-9]|1[0-2])[-/.](0[1-9]|[12]\d|3[01])[-/.](19\d\d|20\d\d)\b",
            RegexOptions.Compiled);

        // Textual Month DD, YYYY (e.g. September 5, 2026 or Sep 05 2026)
        private static readonly Regex TextualDateRegex1 = new(
            @"\b(January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\s+(\d{1,2})(?:st|nd|rd|th)?,?\s+(19\d\d|20\d\d)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Textual DD Month YYYY (e.g. 5 September 2026 or 05-Sep-2026)
        private static readonly Regex TextualDateRegex2 = new(
            @"\b(\d{1,2})(?:st|nd|rd|th)?[\s\.\-]+(January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)[\s\.\-]+(19\d\d|20\d\d)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] CustomFormats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
            "MM/dd/yyyy", "dd/MM/yyyy", "MM-dd-yyyy", "dd-MM-yyyy",
            "yyyyMMdd", "dd MMM yyyy", "dd MMMM yyyy",
            "MMM dd, yyyy", "MMMM dd, yyyy", "MMM dd yyyy", "MMMM dd yyyy",
            "d MMM yyyy", "d MMMM yyyy", "MMM d, yyyy", "MMMM d, yyyy"
        };

        /// <summary>
        /// Attempts to extract the most relevant document date from text content.
        /// Prioritizes dates following keywords like "Invoice Date:", "Date:", then ISO dates, then textual dates.
        /// </summary>
        public static DateTime? ExtractDateFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // 1. Try prefixed date
            var prefixMatch = PrefixedDateRegex.Match(text);
            if (prefixMatch.Success && prefixMatch.Groups.Count > 1)
            {
                string raw = prefixMatch.Groups[1].Value.Trim().TrimEnd('.', ',');
                if (TryParseDateCandidate(raw, out var parsed))
                {
                    return parsed;
                }
                var innerIso = IsoDateRegex.Match(raw);
                if (innerIso.Success && DateTime.TryParse(innerIso.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var innerDate))
                {
                    if (IsValidDocumentDate(innerDate)) return innerDate;
                }
            }

            // 2. Try ISO 8601 date (e.g. 2026-09-05)
            var isoMatch = IsoDateRegex.Match(text);
            if (isoMatch.Success)
            {
                if (DateTime.TryParse(isoMatch.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
                {
                    if (IsValidDocumentDate(isoDate)) return isoDate;
                }
            }

            // 3. Try textual patterns (e.g. September 5, 2026)
            var textMatch1 = TextualDateRegex1.Match(text);
            if (textMatch1.Success)
            {
                string cleaned = CleanOrdinalSuffixes(textMatch1.Value);
                if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var textDate))
                {
                    if (IsValidDocumentDate(textDate)) return textDate;
                }
            }

            // 4. Try textual patterns (e.g. 5 September 2026)
            var textMatch2 = TextualDateRegex2.Match(text);
            if (textMatch2.Success)
            {
                string cleaned = CleanOrdinalSuffixes(textMatch2.Value);
                if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var textDate))
                {
                    if (IsValidDocumentDate(textDate)) return textDate;
                }
            }

            // 5. Try numeric date
            var numMatch = NumericDateRegex.Match(text);
            if (numMatch.Success)
            {
                if (TryParseDateCandidate(numMatch.Value, out var numDate))
                {
                    return numDate;
                }
            }

            return null;
        }

        private static bool TryParseDateCandidate(string candidate, out DateTime result)
        {
            candidate = CleanOrdinalSuffixes(candidate).Trim();

            if (DateTime.TryParseExact(candidate, CustomFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                if (IsValidDocumentDate(result)) return true;
            }

            if (DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                if (IsValidDocumentDate(result)) return true;
            }

            if (DateTime.TryParse(candidate, CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
            {
                if (IsValidDocumentDate(result)) return true;
            }

            result = default;
            return false;
        }

        private static string CleanOrdinalSuffixes(string str)
        {
            return Regex.Replace(str, @"(?<=\d)(st|nd|rd|th)", "", RegexOptions.IgnoreCase);
        }

        private static bool IsValidDocumentDate(DateTime date)
        {
            // Valid if between 1980 and 30 years into the future
            return date.Year >= 1980 && date.Year <= DateTime.UtcNow.Year + 30;
        }
    }
}
