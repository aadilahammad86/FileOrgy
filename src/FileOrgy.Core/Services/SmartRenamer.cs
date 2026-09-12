using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using FileOrgy.Core.Models;
using FileOrgy.Core.Utils;

namespace FileOrgy.Core.Services
{
    public static class SmartRenamer
    {
        private static readonly Regex TokenRegex = new(@"\{([^\}]+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Evaluates a template string replacing dynamic tokens with values from the workflow context.
        /// </summary>
        public static string EvaluateTemplate(string template, WorkflowContext context, int counter = 1)
        {
            if (string.IsNullOrWhiteSpace(template)) return context.FileName;

            string result = TokenRegex.Replace(template, match =>
            {
                string tokenContent = match.Groups[1].Value.Trim();
                return ResolveToken(tokenContent, context, counter);
            });

            return result;
        }

        /// <summary>
        /// Applies case transformation to a string.
        /// </summary>
        public static string ApplyCaseTransform(string input, CaseTransform transform)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return transform switch
            {
                CaseTransform.LowerCase => input.ToLowerInvariant(),
                CaseTransform.UpperCase => input.ToUpperInvariant(),
                CaseTransform.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLowerInvariant()),
                CaseTransform.SnakeCase => ToSnakeCase(input),
                CaseTransform.KebabCase => ToKebabCase(input),
                _ => input
            };
        }

        private static string ResolveToken(string token, WorkflowContext context, int counter)
        {
            // 1. Nested modifiers like upper:basename or lower:doc_date:yyyy
            if (token.StartsWith("upper:", StringComparison.OrdinalIgnoreCase))
            {
                string inner = token[6..];
                return ResolveToken(inner, context, counter).ToUpperInvariant();
            }
            if (token.StartsWith("lower:", StringComparison.OrdinalIgnoreCase))
            {
                string inner = token[6..];
                return ResolveToken(inner, context, counter).ToLowerInvariant();
            }
            if (token.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            {
                string inner = token[6..];
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(ResolveToken(inner, context, counter).ToLowerInvariant());
            }
            if (token.StartsWith("snake:", StringComparison.OrdinalIgnoreCase))
            {
                string inner = token[6..];
                return ToSnakeCase(ResolveToken(inner, context, counter));
            }
            if (token.StartsWith("kebab:", StringComparison.OrdinalIgnoreCase))
            {
                string inner = token[6..];
                return ToKebabCase(ResolveToken(inner, context, counter));
            }

            // 2. Custom variable: var:VariableName
            if (token.StartsWith("var:", StringComparison.OrdinalIgnoreCase))
            {
                string varName = token[4..].Trim();
                return context.GetVariable(varName) ?? string.Empty;
            }

            // 3. Regex capture: regex:groupName or match:1
            if (token.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            {
                string groupName = token[6..].Trim();
                return context.GetVariable("regex_" + groupName) ?? context.GetVariable(groupName) ?? string.Empty;
            }
            if (token.StartsWith("match:", StringComparison.OrdinalIgnoreCase))
            {
                string groupName = token[6..].Trim();
                return context.GetVariable("match_" + groupName) ?? context.GetVariable(groupName) ?? string.Empty;
            }

            // 4. Counter: counter or counter:001
            if (token.StartsWith("counter", StringComparison.OrdinalIgnoreCase))
            {
                int colonIdx = token.IndexOf(':');
                if (colonIdx > 0 && colonIdx < token.Length - 1)
                {
                    string format = token[(colonIdx + 1)..].Trim();
                    if (format.Length > 0 && format.All(char.IsDigit))
                    {
                        return counter.ToString(new string('0', format.Length), CultureInfo.InvariantCulture);
                    }
                    try
                    {
                        return counter.ToString(format, CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        return counter.ToString(CultureInfo.InvariantCulture);
                    }
                }
                return counter.ToString(CultureInfo.InvariantCulture);
            }

            // 5. Date tokens with optional format
            // {date:yyyy-MM-dd}, {created:yyyy-MM-dd}, {modified:yyyy-MM-dd}, {doc_date:yyyy-MM-dd}
            if (token.StartsWith("date", StringComparison.OrdinalIgnoreCase))
            {
                return FormatDate(token, DateTime.Now);
            }
            if (token.StartsWith("created", StringComparison.OrdinalIgnoreCase))
            {
                return FormatDate(token, context.CreatedDate);
            }
            if (token.StartsWith("modified", StringComparison.OrdinalIgnoreCase))
            {
                return FormatDate(token, context.ModifiedDate);
            }
            if (token.StartsWith("doc_date", StringComparison.OrdinalIgnoreCase))
            {
                DateTime dt = context.ExtractedDate ?? context.CreatedDate;
                return FormatDate(token, dt);
            }

            // 6. Base file properties
            if (token.Equals("filename", StringComparison.OrdinalIgnoreCase))
            {
                return context.FileName;
            }
            if (token.Equals("basename", StringComparison.OrdinalIgnoreCase))
            {
                return context.BaseName;
            }
            if (token.Equals("ext", StringComparison.OrdinalIgnoreCase))
            {
                return context.Extension.TrimStart('.');
            }
            if (token.Equals("dotext", StringComparison.OrdinalIgnoreCase))
            {
                return context.Extension;
            }
            if (token.Equals("filesize", StringComparison.OrdinalIgnoreCase))
            {
                return PathHelper.FormatFileSize(context.FileSizeBytes);
            }
            if (token.Equals("filesize_bytes", StringComparison.OrdinalIgnoreCase))
            {
                return context.FileSizeBytes.ToString(CultureInfo.InvariantCulture);
            }
            if (token.Equals("parent", StringComparison.OrdinalIgnoreCase))
            {
                string dir = context.DirectoryPath;
                return string.IsNullOrEmpty(dir) ? string.Empty : Path.GetFileName(dir);
            }
            if (token.Equals("directory", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("dir", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("folder", StringComparison.OrdinalIgnoreCase))
            {
                return context.DirectoryPath;
            }

            // 7. Check if token directly matches a stored variable
            var directVar = context.GetVariable(token);
            if (directVar != null)
            {
                return directVar;
            }

            return "{" + token + "}";
        }

        private static string FormatDate(string token, DateTime date)
        {
            int colonIdx = token.IndexOf(':');
            if (colonIdx > 0 && colonIdx < token.Length - 1)
            {
                string format = token[(colonIdx + 1)..].Trim();
                try
                {
                    return date.ToString(format, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
            }

            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string ToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            string s = Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2");
            s = Regex.Replace(s, @"[\s\.\-]+", "_");
            return s.ToLowerInvariant();
        }

        private static string ToKebabCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            string s = Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1-$2");
            s = Regex.Replace(s, @"[\s\._]+", "-");
            return s.ToLowerInvariant();
        }
    }
}
