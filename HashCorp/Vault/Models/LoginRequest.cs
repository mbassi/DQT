using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.HashCorp.Vault.Models
{
    internal class LoginRequest
    {
        public string password{ get; set; }
        public LoginRequest() { 
            password = string.Empty;
        }
    }
}
