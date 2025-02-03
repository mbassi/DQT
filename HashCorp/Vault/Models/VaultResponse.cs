using DQT.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.HashCorp.Vault.Models
{
    public class VaultResponse
    {
        public List<string> errors = new List<string>();
        public string GetErrors()
        {
            var errorMessage = string.Empty;
            bool hasErrors = errors == null || errors.Count <= 0;
            if (hasErrors) errorMessage = Utility.Join(errors, ", ");
            return errorMessage;
        }
    }
}
