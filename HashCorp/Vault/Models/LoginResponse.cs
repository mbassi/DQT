using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.HashCorp.Vault.Models
{
    public class LoginResponse:VaultResponse
    {
        public Auth auth { get; set; } = new Auth();
        
    }
    public class Auth
    {
        public string client_token { get; set; } = string.Empty;
    }
}
