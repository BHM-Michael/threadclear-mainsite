using System;
using System.Text.Json;
using System.Collections.Generic;

namespace ThreadClear.Functions.Helpers
{
    /// <summary>
    /// Helper methods for cleaning and parsing JSON responses from AI services
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// Cleans AI-generated JSON responses by removing markdown code blocks and extra text
        /// </summary>
        public static string CleanJsonResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "{}";

            response = response.Trim();

            // Remove all variations of markdown code blocks
            // Handle ```json, ```JSON, ``` with newlines, etc.
            response = System.Text.RegularExpressions.Regex.Replace(
                response,
                @"^```(?:json|JSON)?\s*\n?",
                "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            response = System.Text.RegularExpressions.Regex.Replace(
                response,
                @"\n?```\s*$",
                "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            // Remove single backticks at start/end
            response = response.Trim('`', ' ', '\n', '\r', '\t');

            response = response.Trim();

            // Extract just the JSON object/array
            int jsonStart = response.IndexOf('{');
            int arrayStart = response.IndexOf('[');

            // Use whichever comes first
            int start = -1;
            if (jsonStart >= 0 && arrayStart >= 0)
                start = Math.Min(jsonStart, arrayStart);
            else if (jsonStart >= 0)
                start = jsonStart;
            else if (arrayStart >= 0)
                start = arrayStart;

            if (start >= 0)
            {
                char endChar = response[start] == '{' ? '}' : ']';
                int end = response.LastIndexOf(endChar);
                if (end > start)
                {
                    response = response.Substring(start, end - start + 1);
                }
            }

            return response.Trim();
        }

        /// <summary>
        /// Safely parses JSON with automatic cleaning
        /// </summary>
        public static JsonDocument ParseClean(string response)
        {
            var cleaned = CleanJsonResponse(response);
            return JsonDocument.Parse(JsonHelper.CleanJsonResponse(cleaned));
        }

        /// <summary>
        /// Tries to get a string property, returns default if not found
        /// </summary>
        public static string GetStringSafe(this JsonElement element, string propertyName, string defaultValue = "")
        {
            return element.TryGetProperty(propertyName, out var prop)
                ? prop.GetString() ?? defaultValue
                : defaultValue;
        }

        /// <summary>
        /// Tries to get an int property, returns default if not found
        /// </summary>
        public static int GetInt32Safe(this JsonElement element, string propertyName, int defaultValue = 0)
        {
            return element.TryGetProperty(propertyName, out var prop)
                ? prop.GetInt32()
                : defaultValue;
        }

        /// <summary>
        /// Tries to get a DateTime property, returns default if not found or invalid
        /// </summary>
        public static DateTime GetDateTimeSafe(this JsonElement element, string propertyName, DateTime? defaultValue = null)
        {
            if (element.TryGetProperty(propertyName, out var prop) &&
                DateTime.TryParse(prop.GetString(), out var dateTime))
            {
                return dateTime;
            }
            return defaultValue ?? DateTime.UtcNow;
        }

        public static List<string> ParseStringArray(this JsonElement element, string propertyName)
        {
            var list = new List<string>();
            if (element.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrEmpty(value))
                        list.Add(value);
                }
            }
            return list;
        }
    }
}