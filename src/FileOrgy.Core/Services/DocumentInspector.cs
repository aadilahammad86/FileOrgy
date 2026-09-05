using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using FileOrgy.Core.Utils;
using UglyToad.PdfPig;

namespace FileOrgy.Core.Services
{
    public class DocumentInspectionResult
    {
        public string TextContent { get; set; } = string.Empty;
        public DateTime? DocumentDate { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static class DocumentInspector
    {
        private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Inspects supported files to extract text content and internal document date.
        /// </summary>
        public static DocumentInspectionResult Inspect(string filePath, int maxChars = 200_000)
        {
            var result = new DocumentInspectionResult();
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File does not exist";
                return result;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                switch (ext)
                {
                    case ".pdf":
                        InspectPdf(filePath, result, maxChars);
                        break;
                    case ".docx":
                        InspectDocx(filePath, result, maxChars);
                        break;
                    case ".xlsx":
                        InspectXlsx(filePath, result, maxChars);
                        break;
                    case ".pptx":
                        InspectPptx(filePath, result, maxChars);
                        break;
                    case ".jpg":
                    case ".jpeg":
                    case ".tiff":
                    case ".tif":
                        InspectImage(filePath, result);
                        break;
                    case ".txt":
                    case ".csv":
                    case ".tsv":
                    case ".log":
                    case ".md":
                    case ".json":
                    case ".xml":
                    case ".html":
                    case ".htm":
                    case ".ini":
                    case ".yaml":
                    case ".yml":
                    case ".sql":
                    case ".cs":
                    case ".py":
                    case ".js":
                    case ".ts":
                    case ".rtf":
                        InspectPlainText(filePath, result, maxChars);
                        break;
                    default:
                        // Unknown extension, attempt small plain text read if file is small
                        var fi = new FileInfo(filePath);
                        if (fi.Length < 100_000)
                        {
                            InspectPlainText(filePath, result, maxChars);
                        }
                        break;
                }

                // If document date wasn't found in metadata, search extracted text
                if (!result.DocumentDate.HasValue && !string.IsNullOrWhiteSpace(result.TextContent))
                {
                    result.DocumentDate = DateParserHelper.ExtractDateFromText(result.TextContent);
                }

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private static void InspectPdf(string filePath, DocumentInspectionResult result, int maxChars)
        {
            using var document = PdfDocument.Open(filePath);
            var sb = new StringBuilder();

            // Extract metadata dates
            if (document.Information != null)
            {
                result.Title = document.Information.Title;
                result.Author = document.Information.Author;

                if (!string.IsNullOrWhiteSpace(document.Information.CreationDate))
                {
                    result.DocumentDate = ParsePdfDate(document.Information.CreationDate);
                }
                if (!result.DocumentDate.HasValue && !string.IsNullOrWhiteSpace(document.Information.ModifiedDate))
                {
                    result.DocumentDate = ParsePdfDate(document.Information.ModifiedDate);
                }
            }

            // Extract text from pages
            int pageCount = Math.Min(document.NumberOfPages, 20); // First 20 pages max for performance
            for (int i = 1; i <= pageCount; i++)
            {
                var page = document.GetPage(i);
                var words = page.GetWords();
                var text = words != null && words.Any()
                    ? string.Join(" ", words.Select(w => w.Text))
                    : page.Text;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                    if (sb.Length >= maxChars) break;
                }
            }

            result.TextContent = TruncateContent(sb.ToString(), maxChars);
        }

        private static void InspectDocx(string filePath, DocumentInspectionResult result, int maxChars)
        {
            using var zip = ZipFile.OpenRead(filePath);

            // 1. Read metadata
            ReadOfficeCoreProps(zip, result);

            // 2. Read word/document.xml
            var docEntry = zip.GetEntry("word/document.xml");
            if (docEntry != null)
            {
                using var stream = docEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string xml = reader.ReadToEnd();
                result.TextContent = ExtractTextFromXml(xml, maxChars);
            }
        }

        private static void InspectXlsx(string filePath, DocumentInspectionResult result, int maxChars)
        {
            using var zip = ZipFile.OpenRead(filePath);
            ReadOfficeCoreProps(zip, result);

            var sb = new StringBuilder();

            // Read shared strings
            var sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
            if (sharedEntry != null)
            {
                using var stream = sharedEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string xml = reader.ReadToEnd();
                sb.AppendLine(ExtractTextFromXml(xml, maxChars));
            }

            result.TextContent = TruncateContent(sb.ToString(), maxChars);
        }

        private static void InspectPptx(string filePath, DocumentInspectionResult result, int maxChars)
        {
            using var zip = ZipFile.OpenRead(filePath);
            ReadOfficeCoreProps(zip, result);

            var sb = new StringBuilder();
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) &&
                    entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string xml = reader.ReadToEnd();
                    sb.AppendLine(ExtractTextFromXml(xml, 10_000));
                    if (sb.Length >= maxChars) break;
                }
            }

            result.TextContent = TruncateContent(sb.ToString(), maxChars);
        }

        private static void ReadOfficeCoreProps(ZipArchive zip, DocumentInspectionResult result)
        {
            var coreEntry = zip.GetEntry("docProps/core.xml");
            if (coreEntry == null) return;

            try
            {
                using var stream = coreEntry.Open();
                var doc = new XmlDocument();
                doc.Load(stream);

                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("cp", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
                nsmgr.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
                nsmgr.AddNamespace("dcterms", "http://purl.org/dc/terms/");

                result.Title = doc.SelectSingleNode("//dc:title", nsmgr)?.InnerText;
                result.Author = doc.SelectSingleNode("//dc:creator", nsmgr)?.InnerText;

                var createdNode = doc.SelectSingleNode("//dcterms:created", nsmgr);
                if (createdNode != null && DateTime.TryParse(createdNode.InnerText, out var created))
                {
                    result.DocumentDate = created;
                    return;
                }

                var modNode = doc.SelectSingleNode("//dcterms:modified", nsmgr);
                if (modNode != null && DateTime.TryParse(modNode.InnerText, out var mod))
                {
                    result.DocumentDate = mod;
                }
            }
            catch
            {
                // Silently ignore corrupt metadata
            }
        }

        private static void InspectPlainText(string filePath, DocumentInspectionResult result, int maxChars)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);

            char[] buffer = new char[Math.Min(maxChars, 65536)];
            int read = reader.Read(buffer, 0, buffer.Length);
            result.TextContent = new string(buffer, 0, read);
        }

        private static void InspectImage(string filePath, DocumentInspectionResult result)
        {
            try
            {
                // Extract EXIF DateTimeOriginal / DateTime from JPEG
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var exifDate = TryExtractJpegExifDate(fs);
                if (exifDate.HasValue)
                {
                    result.DocumentDate = exifDate.Value;
                }
            }
            catch
            {
                // Ignore EXIF parsing failures
            }
        }

        private static DateTime? TryExtractJpegExifDate(Stream stream)
        {
            // Simple fast binary scan for EXIF date format: "YYYY:MM:DD HH:MM:SS" (Tag 0x9003 / 0x0132)
            byte[] buffer = new byte[Math.Min(65536, stream.Length)];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            var dateRegex = new Regex(@"\b(19\d\d|20\d\d):([01]\d):([0-3]\d)\s([0-2]\d):([0-5]\d):([0-5]\d)\b");
            string ascii = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            var match = dateRegex.Match(ascii);

            if (match.Success)
            {
                string formatted = $"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value} {match.Groups[4].Value}:{match.Groups[5].Value}:{match.Groups[6].Value}";
                if (DateTime.TryParse(formatted, out var dt))
                {
                    return dt;
                }
            }

            return null;
        }

        private static DateTime? ParsePdfDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // PDF dates often look like: D:20260905143000Z or D:20260905143000+04'00'
            string cleaned = raw.StartsWith("D:", StringComparison.OrdinalIgnoreCase) ? raw[2..] : raw;
            if (cleaned.Length >= 8)
            {
                string y = cleaned[..4];
                string m = cleaned.Substring(4, 2);
                string d = cleaned.Substring(6, 2);
                if (int.TryParse(y, out int year) && int.TryParse(m, out int month) && int.TryParse(d, out int day))
                {
                    try
                    {
                        return new DateTime(year, month, day);
                    }
                    catch { }
                }
            }

            if (DateTime.TryParse(cleaned, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string ExtractTextFromXml(string xml, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(xml)) return string.Empty;

            // Strip XML tags and clean whitespace
            string text = TagRegex.Replace(xml, " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = WhitespaceRegex.Replace(text, " ").Trim();
            return TruncateContent(text, maxChars);
        }

        private static string TruncateContent(string str, int maxChars)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Length > maxChars ? str[..maxChars] : str;
        }
    }
}
