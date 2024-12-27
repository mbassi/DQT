using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Utilities
{
    public class ArrayOperations<T> : IArrayOperations<T>
    {
        public T[] AppendRange(T[] sourceArray, IEnumerable<T> itemsToAppend)
        {
            // Check for null arguments
            if (sourceArray == null)
                throw new ArgumentNullException(nameof(sourceArray));
            if (itemsToAppend == null)
                throw new ArgumentNullException(nameof(itemsToAppend));

            // Convert itemsToAppend to array for length calculation
            var itemsArray = itemsToAppend.ToArray();

            // Create new array with combined length
            var result = new T[sourceArray.Length + itemsArray.Length];

            // Copy original array
            Array.Copy(sourceArray, 0, result, 0, sourceArray.Length);

            // Copy new items
            Array.Copy(itemsArray, 0, result, sourceArray.Length, itemsArray.Length);

            return result;
        }
    }
}
