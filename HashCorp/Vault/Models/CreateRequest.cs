using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.HashCorp.Vault.Models
{
    public class CreateRequest<T>
    {
        public T Data { get; set; }
        public CreateRequest(T data)
        {
            Data = data;
        }
    }
}
