using DQT.Office365.SharePoint.Modeles;
using Microsoft.SharePoint.Client;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Office365.SharePoint.Services
{
    public class SharePointService: ISharePointService
    {
        private ClientContext _context;
        private readonly SharePointConfig _config;
        private bool _disposed;

        public SharePointService(SharePointConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task ConnectAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SharePointService));

            try
            {
                // Convert the password to SecureString
               
                // Create ClientContext with credentials
                _context = new ClientContext(_config.SiteUrl);
                _context.Credentials = new NetworkCredential(_config.UserName, _config.Password);
                await _context.ExecuteQueryAsync();
                // Test the connection
                
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to connect to SharePoint", ex);
            }
        }

        public async Task CreateDirectoryAsync(string folderName, string parentFolderPath = "")
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SharePointService));

            if (_context == null)
                throw new InvalidOperationException("Not connected to SharePoint");

            try
            {
                Web web = _context.Web;
                Folder rootFolder = web.RootFolder;

                // Get or create the parent folder path
                Folder targetFolder = rootFolder;
                if (!string.IsNullOrEmpty(parentFolderPath))
                {
                    string[] folderPaths = parentFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string folder in folderPaths)
                    {
                        if (!FolderExists(targetFolder, folder))
                        {
                            // Create folder if it doesn't exist
                            targetFolder = targetFolder.Folders.Add(folder);
                        }
                        else
                        {
                            targetFolder = targetFolder.Folders.GetByUrl(folder);
                        }
                        _context.Load(targetFolder);
                        await _context.ExecuteQueryAsync();
                    }
                }

                // Create the new folder
                if (!FolderExists(targetFolder, folderName))
                {
                    targetFolder.Folders.Add(folderName);
                    await _context.ExecuteQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create directory: {folderName}", ex);
            }
        }
        private bool FolderExists(Folder parentFolder, string folderName)
        {
            try
            {
                var folder = parentFolder.Folders.GetByUrl(folderName);
                _context.Load(folder);
                _context.ExecuteQuery();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task UploadFileAsync(byte[] fileContent, string fileName, string destinationFolder)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SharePointService));

            if (_context == null)
                throw new InvalidOperationException("Not connected to SharePoint");

            if (fileContent == null || fileContent.Length == 0)
                throw new ArgumentException("File content cannot be null or empty", nameof(fileContent));

            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

            try
            {
                Web web = _context.Web;
                Folder rootFolder = web.RootFolder;

                // Get the target folder
                Folder targetFolder = rootFolder;
                if (!string.IsNullOrEmpty(destinationFolder))
                {
                    string[] folderPaths = destinationFolder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string folder in folderPaths)
                    {
                        targetFolder = targetFolder.Folders.GetByUrl(folder);
                        await _context.ExecuteQueryAsync();
                    }
                }

                // Upload the file
                using (var memoryStream = new MemoryStream(fileContent))
                {
                    var fileCreationInfo = new FileCreationInformation
                    {
                        ContentStream = memoryStream,
                        Url = fileName,
                        Overwrite = true
                    };

                    Microsoft.SharePoint.Client.File uploadedFile =
                        targetFolder.Files.Add(fileCreationInfo);

                    _context.Load(uploadedFile);
                    await _context.ExecuteQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to upload file: {fileName}", ex);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
