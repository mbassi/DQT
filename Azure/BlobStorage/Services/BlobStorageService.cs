using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DQT.Azure.BlobStorage.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DQT.Azure.BlobStorage.Services
{
    public class BlobStorageService:IBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly BlobStorageOptions _options;
        private readonly ILogger<BlobStorageService> _logger;
        private readonly HashSet<string> _allowedExtensions;
        private readonly HashSet<string> _allowedContentTypes;
        public BlobStorageService(
            BlobStorageOptions options,
            ILogger<BlobStorageService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize security settings
            _allowedExtensions = new HashSet<string>(
                options.AllowedFileExtensions ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase
            );
            _allowedContentTypes = new HashSet<string>(
                options.AllowedContentTypes ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase
            );

            // Initialize blob client with appropriate authentication
            if (options.RequiresManagedIdentity)
            {
                var credential = new DefaultAzureCredential();
                var serviceClient = new BlobServiceClient(
                    options.ConnectionString
                );
                _containerClient = serviceClient.GetBlobContainerClient(options.ContainerName);
            }
            else
            {
                _containerClient = new BlobContainerClient(
                    options.ConnectionString,
                    options.ContainerName
                );
            }
        }
        public async Task<IEnumerable<string>> ListBlobsAsync(string prefix)
        {
            try
            {
                var blobs = new List<string>();
                await foreach (var blob in _containerClient.GetBlobsAsync(prefix: prefix))
                {
                    blobs.Add(blob.Name);
                }
                return blobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing blobs with prefix: {Prefix}", prefix);
                throw;
            }
        }

        public async Task<byte[]> GetBlobBinaryContentAsync(string blobName)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(SanitizeBlobName(blobName));

                if (!await blobClient.ExistsAsync())
                {
                    throw new KeyNotFoundException($"Blob '{blobName}' not found.");
                }

                using var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading blob: {BlobName}", blobName);
                throw;
            }
        }

        public async Task<string> GetBlobTextContentAsync(string blobName)
        {
            var content = await GetBlobBinaryContentAsync(blobName);
            return System.Text.Encoding.UTF8.GetString(content);
        }

        public async Task DeleteBlobAsync(string blobName)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(SanitizeBlobName(blobName));

                if (!await blobClient.ExistsAsync())
                {
                    throw new KeyNotFoundException($"Blob '{blobName}' not found.");
                }

                await blobClient.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob: {BlobName}", blobName);
                throw;
            }
        }
        public async Task<List<string>> ExtractZipBlobAsync(
            string rootFolder,
            string zipBlobName,
            byte[] zipContent,
            IDictionary<string, string> metadata = null,
            IProgress<long> progress = null
            
        )
        {
            List<string> blobs = new List<string>();
            try
            {
                
                

                using (var zipStream = new MemoryStream(zipContent))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {

                        // Validate extracted file names

                        var blobName = rootFolder + "\\" + entry.FullName;
                        if (blobName.EndsWith("/") || blobName.EndsWith("\\")) continue;
                        using (var entryStream = entry.Open())
                        using (var memoryStream = new MemoryStream())
                        {
                            await entryStream.CopyToAsync(memoryStream);
                            await SaveFileAsync(
                                memoryStream.ToArray(),
                                blobName,
                                null,
                                metadata,
                                progress

                            );
                        }
                        blobs.Add(blobName);
                    }
                }

                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting ZIP blob: {BlobName}", zipBlobName);
                throw;
            }
            return blobs;
        }
        public async Task<Dictionary<string, byte[]>> ExtractZipBlobAsync(string zipBlobName)
        {
            try
            {
                byte[] zipContent = await GetBlobBinaryContentAsync(zipBlobName);
                var extractedFiles = new Dictionary<string, byte[]>();

                using (var zipStream = new MemoryStream(zipContent))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // Validate extracted file names
                        if (!IsValidFileName(entry.FullName))
                        {
                            _logger.LogWarning("Skipping potentially malicious file name: {FileName}", entry.FullName);
                            continue;
                        }

                        using (var entryStream = entry.Open())
                        using (var memoryStream = new MemoryStream())
                        {
                            await entryStream.CopyToAsync(memoryStream);
                            extractedFiles[entry.FullName] = memoryStream.ToArray();
                        }
                    }
                }

                return extractedFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting ZIP blob: {BlobName}", zipBlobName);
                throw;
            }
        }
        private string GetMimeMapping(string extension)
        {
            // Common MIME types mapping
            var mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {".txt", "text/plain"},
                {".pdf", "application/pdf"},
                {".doc", "application/msword"},
                {".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
                {".xls", "application/vnd.ms-excel"},
                {".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
                {".png", "image/png"},
                {".jpg", "image/jpeg"},
                {".jpeg", "image/jpeg"},
                {".gif", "image/gif"},
                {".csv", "text/csv"},
                {".xml", "application/xml"},
                {".json", "application/json"},
                {".zip", "application/x-zip-compressed"},
                {".zip.done", "application/x-zip-compressed"},
                {".mp3", "audio/mpeg"},
                {".mp4", "video/mp4"},
                {".webp", "image/webp"},
                {".svg", "image/svg+xml"},
                {".ico", "image/x-icon"},
                {".html", "text/html"},
                {".htm", "text/html"},
                {".css", "text/css"},
                {".js", "application/javascript"}
            };

            return mimeTypes.TryGetValue(extension, out var mimeType)
                ? mimeType
                : "application/octet-stream";  // Default binary file type
        }
        public async Task<BlobUploadResult> SaveFileAsync(
            byte[] fileContent,
            string blobName,
            string contentType = null,
            IDictionary<string, string> metadata = null,
            IProgress<long> progress = null
        )
        {
            try
            {
                if (fileContent == null || fileContent.Length == 0)
                {
                    throw new ArgumentException("File content cannot be null or empty");
                }

                if (fileContent.Length > _options.MaxFileSizeBytes)
                {
                    throw new InvalidOperationException($"File size exceeds maximum allowed size of {_options.MaxFileSizeBytes} bytes");
                }
                if (string.IsNullOrEmpty(contentType))
                {
                    var extension = Path.GetExtension(blobName);
                    if (!string.IsNullOrEmpty(extension))
                    {
                        contentType = GetMimeMapping(extension);
                    }
                }
                // Validate content type
                if (!string.IsNullOrEmpty(contentType) && _allowedContentTypes.Count > 0 && !_allowedContentTypes.Contains(contentType))
                {
                    throw new InvalidOperationException($"Content type '{contentType}' is not allowed");
                }
                
                blobName = SanitizeBlobName(blobName);
                var blobClient = _containerClient.GetBlobClient(blobName);

                var result = new BlobUploadResult
                {
                    Name = blobName,
                    Url = blobClient.Uri.ToString(),
                    SizeBytes = fileContent.Length,
                    Metadata = metadata ?? new Dictionary<string, string>()
                };

                // Calculate MD5 hash and prepare upload options
                using (var md5 = MD5.Create())
                {
                    result.ContentMD5 = Convert.ToBase64String(md5.ComputeHash(fileContent));

                    var options = new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = contentType,
                            ContentHash = Convert.FromBase64String(result.ContentMD5)
                        },
                        Metadata = result.Metadata,
                        ProgressHandler = new Progress<long>(bytesTransferred =>
                        {
                            progress?.Report(bytesTransferred);
                        }),
                        // Add overwrite condition
                        Conditions = new BlobRequestConditions { }
                    };

                    // Encrypt content if enabled
                    
                    using (var memoryStream = new MemoryStream(fileContent))
                    {
                        await blobClient.UploadAsync(memoryStream, options);
                    }
                    
                }

                _logger.LogInformation(
                    "Successfully uploaded blob {BlobName}",
                    blobName
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading blob: {BlobName}", blobName);
                throw;
            }
        }

        private static string SanitizeBlobName(string blobName)
        {
            var sanitized = blobName.Replace("\\", "/");
            // Remove any potentially dangerous characters
            //sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9\-_./]", "");

            // Ensure no directory traversal
            sanitized = sanitized.Replace("..", "");

            // Ensure valid blob name
            return sanitized.TrimStart('/');
        }

        private static bool IsValidFileName(string fileName)
        {
            // Check for directory traversal attempts
            if (fileName.Contains("..") || fileName.Contains("~"))
                return false;

            // Check for invalid characters
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            return true;
        }
    }
}
