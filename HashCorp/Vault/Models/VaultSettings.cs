using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.HashCorp.Vault.Models
{
    public class VaultSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 0;
        public string Data1 { get; set; } = string.Empty;

        public string Data2 { get; set; } = string.Empty;

        public string Data3 { get; set; } = string.Empty;

        public string Data4 { get; set; } = string.Empty;
        public string Data5 { get; set; } = string.Empty;
        public string Data6 { get; set; } = string.Empty;
        
    }
}
