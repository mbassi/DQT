using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Azure.BlobStorage.Models
{
    // <summary>
    /// Configuration options for blob storage
    /// </summary>
    public class BlobStorageOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;

        public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB default
        public string[] AllowedFileExtensions { get; set; } 
        public string[] AllowedContentTypes { get; set; }
        public bool RequiresManagedIdentity { get; set; } = false;
    }
}
