using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.JSONConverter
{
    public class JsonDeserializationExample
    {
        // Best Practice: Create a DynamicWrapper method for flexible JSON deserialization
        public static dynamic DeserializeToDynamic(string jsonString)
        {
            try
            {
                // Use JObject.Parse for robust parsing
                JObject jsonObject = JObject.Parse(jsonString);

                // Create a dynamic wrapper that allows flexible access to JSON properties
                dynamic dynamicWrapper = new ExpandoObject();
                var dynamicDict = dynamicWrapper as IDictionary<string, object>;

                // Iterate through all properties and add them to the dynamic object
                foreach (var property in jsonObject.Properties())
                {
                    dynamicDict[property.Name] = ConvertJTokenToDynamic(property.Value);
                }

                return dynamicWrapper;
            }
            catch (JsonReaderException ex)
            {
                // Best Practice: Provide meaningful error handling
                Console.WriteLine($"JSON Parsing Error: {ex.Message}");
                throw;
            }
        }

        // Recursive method to handle nested JSON structures
        private static object ConvertJTokenToDynamic(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    dynamic nestedObject = new ExpandoObject();
                    var nestedDict = nestedObject as IDictionary<string, object>;

                    foreach (var property in token.Children<JProperty>())
                    {
                        nestedDict[property.Name] = ConvertJTokenToDynamic(property.Value);
                    }
                    return nestedObject;

                case JTokenType.Array:
                    return token.Select(ConvertJTokenToDynamic).ToList();

                case JTokenType.Integer:
                    return (int)token;

                case JTokenType.Float:
                    return (double)token;

                case JTokenType.String:
                    return (string)token;

                case JTokenType.Boolean:
                    return (bool)token;

                case JTokenType.Null:
                    return null;

                default:
                    return token.ToString();
            }
        }
    }
}
