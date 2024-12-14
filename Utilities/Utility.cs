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
    }
}
