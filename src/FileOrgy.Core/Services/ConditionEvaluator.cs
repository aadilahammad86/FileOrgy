using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileOrgy.Core.Models;

namespace FileOrgy.Core.Services
{
    public static class ConditionEvaluator
    {
        public static bool EvaluateRule(
            Rule rule,
            WorkflowContext context,
            IReadOnlyDictionary<string, List<string>> keywordLists,
            IReadOnlyDictionary<string, string> globalVariables)
        {
            if (rule == null || !rule.Enabled) return false;
            if (rule.Conditions == null || rule.Conditions.Count == 0) return true; // Rule without conditions matches all files

            if (rule.MatchLogic == ConditionMatchLogic.All)
            {
                return rule.Conditions.All(c => EvaluateCondition(c, context, keywordLists, globalVariables));
            }
            else // Any
            {
                return rule.Conditions.Any(c => EvaluateCondition(c, context, keywordLists, globalVariables));
            }
        }

        public static bool EvaluateCondition(
            RuleCondition condition,
            WorkflowContext context,
            IReadOnlyDictionary<string, List<string>> keywordLists,
            IReadOnlyDictionary<string, string> globalVariables)
        {
            var comparison = condition.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            switch (condition.Target)
            {
                case RuleTarget.FileName:
                    return EvaluateString(context.FileName, condition, comparison, keywordLists);

                case RuleTarget.BaseName:
                    return EvaluateString(context.BaseName, condition, comparison, keywordLists);

                case RuleTarget.Extension:
                    string ext = context.Extension.TrimStart('.');
                    string val = condition.Value.TrimStart('.');
                    return EvaluateExtension(ext, val, condition, comparison, keywordLists);

                case RuleTarget.FullPath:
                    return EvaluateString(context.CurrentFilePath, condition, comparison, keywordLists);

                case RuleTarget.FileSizeBytes:
                    return EvaluateNumeric(context.FileSizeBytes, condition);

                case RuleTarget.FileCreatedDate:
                    return EvaluateDate(context.CreatedDate, condition);

                case RuleTarget.FileModifiedDate:
                    return EvaluateDate(context.ModifiedDate, condition);

                case RuleTarget.ExtractedContent:
                    EnsureContentExtracted(context);
                    return EvaluateString(context.ExtractedContent ?? string.Empty, condition, comparison, keywordLists);

                case RuleTarget.ExtractedDate:
                    EnsureContentExtracted(context);
                    if (!context.ExtractedDate.HasValue) return false;
                    return EvaluateDate(context.ExtractedDate.Value, condition);

                case RuleTarget.CustomVariable:
                    string varKey = condition.TargetVariable ?? string.Empty;
                    string varVal = context.GetVariable(varKey) ??
                                    (globalVariables.TryGetValue(varKey, out var gv) ? gv : string.Empty);
                    return EvaluateString(varVal, condition, comparison, keywordLists);

                default:
                    return false;
            }
        }

        private static void EnsureContentExtracted(WorkflowContext context)
        {
            if (context.ExtractedContent == null && File.Exists(context.CurrentFilePath))
            {
                var inspection = DocumentInspector.Inspect(context.CurrentFilePath);
                context.ExtractedContent = inspection.TextContent;
                if (inspection.DocumentDate.HasValue && !context.ExtractedDate.HasValue)
                {
                    context.ExtractedDate = inspection.DocumentDate;
                }
            }
        }

        private static bool EvaluateString(
            string text,
            RuleCondition condition,
            StringComparison comparison,
            IReadOnlyDictionary<string, List<string>> keywordLists)
        {
            string condValue = condition.Value ?? string.Empty;

            switch (condition.Operator)
            {
                case ConditionOperator.Equals:
                    return string.Equals(text, condValue, comparison);

                case ConditionOperator.NotEquals:
                    return !string.Equals(text, condValue, comparison);

                case ConditionOperator.Contains:
                    return text.Contains(condValue, comparison);

                case ConditionOperator.NotContains:
                    return !text.Contains(condValue, comparison);

                case ConditionOperator.StartsWith:
                    return text.StartsWith(condValue, comparison);

                case ConditionOperator.EndsWith:
                    return text.EndsWith(condValue, comparison);

                case ConditionOperator.MatchesRegex:
                    try
                    {
                        var regexOptions = condition.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        return Regex.IsMatch(text, condValue, regexOptions);
                    }
                    catch
                    {
                        return false;
                    }

                case ConditionOperator.MatchesWildcard:
                    return MatchesWildcardPattern(text, condValue, !condition.CaseSensitive);

                case ConditionOperator.InKeywordList:
                    string inList = !string.IsNullOrEmpty(condition.KeywordListName) ? condition.KeywordListName : condValue;
                    if (!string.IsNullOrEmpty(inList) &&
                        keywordLists.TryGetValue(inList, out var keywords))
                    {
                        return keywords.Any(kw => !string.IsNullOrWhiteSpace(kw) && text.Contains(kw.Trim(), comparison));
                    }
                    return false;

                case ConditionOperator.NotInKeywordList:
                    string notInList = !string.IsNullOrEmpty(condition.KeywordListName) ? condition.KeywordListName : condValue;
                    if (!string.IsNullOrEmpty(notInList) &&
                        keywordLists.TryGetValue(notInList, out var kwList))
                    {
                        return !kwList.Any(kw => !string.IsNullOrWhiteSpace(kw) && text.Contains(kw.Trim(), comparison));
                    }
                    return true;

                case ConditionOperator.IsEmptyFile:
                    return string.IsNullOrWhiteSpace(text);

                default:
                    return false;
            }
        }

        private static bool EvaluateExtension(
            string ext,
            string targetExt,
            RuleCondition condition,
            StringComparison comparison,
            IReadOnlyDictionary<string, List<string>> keywordLists)
        {
            string listName = !string.IsNullOrEmpty(condition.KeywordListName) ? condition.KeywordListName : condition.Value;

            if (condition.Operator == ConditionOperator.InKeywordList &&
                !string.IsNullOrEmpty(listName) &&
                keywordLists.TryGetValue(listName, out var keywords))
            {
                return keywords.Any(kw => string.Equals(ext, kw.Trim().TrimStart('.'), comparison));
            }

            if (condition.Operator == ConditionOperator.NotInKeywordList &&
                !string.IsNullOrEmpty(listName) &&
                keywordLists.TryGetValue(listName, out var kwList))
            {
                return !kwList.Any(kw => string.Equals(ext, kw.Trim().TrimStart('.'), comparison));
            }

            if (condition.Operator == ConditionOperator.Equals)
            {
                return string.Equals(ext, targetExt, comparison);
            }

            if (condition.Operator == ConditionOperator.NotEquals)
            {
                return !string.Equals(ext, targetExt, comparison);
            }

            // Fall back to standard string evaluation
            return EvaluateString(ext, condition, comparison, keywordLists);
        }

        private static bool EvaluateNumeric(long actualBytes, RuleCondition condition)
        {
            if (condition.Operator == ConditionOperator.IsEmptyFile)
            {
                return actualBytes == 0;
            }

            if (!long.TryParse(condition.Value, out long targetValue))
            {
                return false;
            }

            return condition.Operator switch
            {
                ConditionOperator.Equals => actualBytes == targetValue,
                ConditionOperator.NotEquals => actualBytes != targetValue,
                ConditionOperator.GreaterThan => actualBytes > targetValue,
                ConditionOperator.LessThan => actualBytes < targetValue,
                _ => false
            };
        }

        private static bool EvaluateDate(DateTime date, RuleCondition condition)
        {
            var now = DateTime.Now;

            if (condition.Operator == ConditionOperator.OlderThanDays)
            {
                if (double.TryParse(condition.Value, out double days))
                {
                    return (now - date).TotalDays >= days;
                }
                return false;
            }

            if (condition.Operator == ConditionOperator.NewerThanDays)
            {
                if (double.TryParse(condition.Value, out double days))
                {
                    return (now - date).TotalDays <= days;
                }
                return false;
            }

            if (DateTime.TryParse(condition.Value, out var targetDate))
            {
                return condition.Operator switch
                {
                    ConditionOperator.Equals => date.Date == targetDate.Date,
                    ConditionOperator.NotEquals => date.Date != targetDate.Date,
                    ConditionOperator.GreaterThan => date > targetDate,
                    ConditionOperator.LessThan => date < targetDate,
                    _ => false
                };
            }

            return false;
        }

        private static bool MatchesWildcardPattern(string text, string pattern, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            string regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            return Regex.IsMatch(text, regexPattern, options);
        }
    }
}
