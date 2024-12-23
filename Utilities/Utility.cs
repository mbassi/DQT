using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Utilities
{
    public static class Utility
    {
        public static string Join<T>(IEnumerable<T> list, string separator)
        {
            return string.Join(separator, list);
        }
        public static string Concat(string[] strings)
        {
            string result = string.Empty;
            for (int i = 0; i < strings.Length-1; i++)
            {
                result += Concat(strings[i], strings[i + 1]);
            }
            return result;
        }
        /// <summary>
        /// Concatenates two strings together with input validation
        /// </summary>
        /// <param name="firstString">The first string to concatenate</param>
        /// <param name="secondString">The second string to concatenate</param>
        /// <returns>The concatenated string result</returns>
        /// <exception cref="ArgumentNullException">Thrown when either input string is null</exception>
        public static string Concat(string firstString, string secondString)
        {
            // Validate inputs to ensure they're not null
            // Using C# 7.0+ null coalescing throw operator for concise null checks
            _ = firstString ?? throw new ArgumentNullException(nameof(firstString));
            _ = secondString ?? throw new ArgumentNullException(nameof(secondString));

            // Use StringBuilder for better performance when working with strings
            // especially if this method might be extended to handle more strings in the future
            StringBuilder resultBuilder = new StringBuilder(firstString.Length + secondString.Length);

            // Append both strings to the StringBuilder
            resultBuilder.Append(firstString);
            resultBuilder.Append(secondString);

            // Return the final concatenated result
            return resultBuilder.ToString();
        }
        public static T GetValueOrDefault<T>(dynamic obj, string propertyName, out bool exists, T defaultValue = default)
        {
            exists = false;
            try
            {
                // Check if property exists and is of the correct type
                var value = ((IDictionary<string, object>)obj)[propertyName];
                exists = value != null;
                return value is T typedValue ? typedValue : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
        public static string Substring(string input, int startPosition, int? length = null)
        {
            // First, validate that our input string isn't null
            // We use the null coalescing throw operator for a concise null check
            _ = input ?? throw new ArgumentNullException(nameof(input),
                "Input string cannot be null");

            // Validate the start position is within bounds
            // We check if it's negative or beyond the string length
            if (startPosition < 0 || startPosition > input.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startPosition),
                    $"Start position must be between 0 and {input.Length}");
            }

            // If no length is specified, calculate the remaining length from the start position
            int actualLength = length ?? (input.Length - startPosition);

            // Validate the length parameter
            if (length.HasValue && length.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length),
                    "Length cannot be negative");
            }

            // Calculate the maximum possible length from the start position
            int maxPossibleLength = input.Length - startPosition;

            // Ensure we don't try to read beyond the string's end
            int safeLength = Math.Min(actualLength, maxPossibleLength);

            // Use StringBuilder for the extraction to optimize memory usage
            // Pre-allocate the exact size we need
            StringBuilder resultBuilder = new StringBuilder(safeLength);

            // Extract the characters one by one, which allows us to add additional processing if needed
            for (int i = 0; i < safeLength; i++)
            {
                resultBuilder.Append(input[startPosition + i]);
            }

            return resultBuilder.ToString();
        }
        public static string Trim(string input)
        {
            string result = input;
            if (!string.IsNullOrEmpty(input))
            {
                result = input.Trim();
            }
            return result;
        }
    }
}
