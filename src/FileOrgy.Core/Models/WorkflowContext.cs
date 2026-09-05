using System;
using System.Collections.Generic;
using System.IO;

namespace FileOrgy.Core.Models
{
    public class WorkflowContext
    {
        public string OriginalFilePath { get; }
        public string CurrentFilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        public string FileName => Path.GetFileName(CurrentFilePath);
        public string BaseName => Path.GetFileNameWithoutExtension(CurrentFilePath);
        public string Extension => Path.GetExtension(CurrentFilePath);
        public string DirectoryPath => Path.GetDirectoryName(CurrentFilePath) ?? string.Empty;

        public string? ExtractedContent { get; set; }
        public DateTime? ExtractedDate { get; set; }

        public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ExtractedFiles { get; } = new();

        public bool IsAborted { get; set; }
        public string? AbortReason { get; set; }

        public WorkflowContext(string filePath, IDictionary<string, string>? globalVariables = null)
        {
            OriginalFilePath = filePath;
            CurrentFilePath = filePath;

            if (File.Exists(filePath))
            {
                var fi = new FileInfo(filePath);
                FileSizeBytes = fi.Length;
                CreatedDate = fi.CreationTime;
                ModifiedDate = fi.LastWriteTime;
            }

            if (globalVariables != null)
            {
                foreach (var kvp in globalVariables)
                {
                    Variables[kvp.Key] = kvp.Value;
                }
            }
        }

        public void SetVariable(string key, string value)
        {
            Variables[key] = value;
        }

        public string? GetVariable(string key)
        {
            return Variables.TryGetValue(key, out var val) ? val : null;
        }
    }
}
