using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Extentions.Dynamics
{
    public class FlexibleObject : DynamicObject
    {
        // Internal dictionary to store dynamic properties
        private Dictionary<string, object> _properties = new Dictionary<string, object>();

        // Override TryGetMember to enable dynamic property getting
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return _properties.TryGetValue(binder.Name, out result);
        }

        // Override TrySetMember to enable dynamic property setting
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _properties[binder.Name] = value;
            return true;
        }

        // Method to get all dynamic properties
        public IEnumerable<string> GetDynamicPropertyNames() => _properties.Keys;
    }

    // Example 2: Expando Object for Dynamic Property Management
    public class DynamicPropertiesshowcase
    {
        public static void DemonstrateExpandoObject()
        {
            // Using ExpandoObject for runtime property addition
            dynamic person = new ExpandoObject();

            // Adding properties dynamically
            person.Name = "John Doe";
            person.Age = 30;
            person.Address = new
            {
                Street = "123 Dynamic Lane",
                City = "Propertyville"
            };

            // Adding a method dynamically
            person.Introduce = (Func<string>)(() =>
                $"Hi, I'm {person.Name} and I'm {person.Age} years old.");

            Console.WriteLine(person.Name);  // Output: John Doe
            Console.WriteLine(person.Introduce());  // Output: Hi, I'm John Doe and I'm 30 years old
        }

        // Example 3: Safe Dynamic Property Handling
        public static void SafeDynamicPropertyAccess()
        {
            dynamic safeObject = new ExpandoObject();
            var dict = safeObject as IDictionary<string, object>;

            // Safely add properties
            dict["Key1"] = "Value1";
            dict["Key2"] = 42;

            // Safe property access
            if (dict.TryGetValue("Key1", out object value))
            {
                Console.WriteLine($"Key1 value: {value}");
            }

            // Avoid runtime errors
            try
            {
                // This might throw an exception if the property doesn't exist
                Console.WriteLine(safeObject.NonExistentProperty);
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                Console.WriteLine($"Property access error: {ex.Message}");
            }
        }
    }

    // Best Practice: Wrapper for Type-Safe Dynamic Properties
    public class DynamicPropertyWrapper
    {
        private Dictionary<string, object> _properties = new Dictionary<string, object>();

        // Type-safe getter with default value
        public T GetProperty<T>(string key, T defaultValue = default)
        {
            return _properties.TryGetValue(key, out object value)
                ? (value is T typedValue ? typedValue : defaultValue)
                : defaultValue;
        }

        // Type-safe setter with validation
        public void SetProperty<T>(string key, T value)
        {
            // Add custom validation logic if needed
            if (value != null)
            {
                _properties[key] = value;
            }
        }

        // Check if property exists
        public bool HasProperty(string key) => _properties.ContainsKey(key);
    }

}
