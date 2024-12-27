using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Utilities
{
    public interface IArrayOperations<T>
    {
        T[] AppendRange(T[] sourceArray, IEnumerable<T> itemsToAppend);
    }

}
