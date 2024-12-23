using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace DQT.SFTP
{
    /// <summary>
    /// Secure SFTP Client with robust error handling and security best practices
    /// </summary>
    public class SecureSftpClient : ISecureSftpClient, IDisposable
    {
        private readonly SftpClient _sftpClient;
        private bool _disposed = false;

        public List<string> ErrorMessage { get; private set; }
        public int MaxConnectionTentatives { get; set; } = 3;
        public int WaitTimeBetweenTentativesInMiliseconds { get; set; } = 3000;
        public long MaxFileSize { get; set; } = 1_073_741_824; // 1 GB
        /// <summary>
        /// Constructor using connection details with enhanced security
        /// </summary>
        /// <param name="host">SFTP Server hostname</param>
        /// <param name="port">SFTP Server port (default 22)</param>
        /// <param name="username">SFTP Username</param>
        /// <param name="privateKeyPath">Path to private key file</param>
        /// <param name="passphrase">Optional passphrase for private key</param>
        /// <summary>
        /// Constructor for private key authentication
        /// </summary>
        /// <param name="host">SFTP Server hostname</param>
        /// <param name="username">SFTP Username</param>
        /// <param name="privateKeyPath">Path to private key file</param>
        /// <param name="passphrase">Optional passphrase for private key</param>
        /// <param name="port">SFTP Server port (default 22)</param>
        public SecureSftpClient(
            string host,
            string username,
            string privateKeyPath,
            string passphrase = null,
            int port = 22) : this(host, username, privateKeyPath, passphrase, null, port)
        {
        }

        /// <summary>
        /// Constructor for username and password authentication
        /// </summary>
        /// <param name="host">SFTP Server hostname</param>
        /// <param name="username">SFTP Username</param>
        /// <param name="password">SFTP Password</param>
        /// <param name="port">SFTP Server port (default 22)</param>
        public SecureSftpClient(
            string host,
            string username,
            string password,
            int port = 22) : this(host,username,null, null, password,port)
        {
        }

        /// <summary>
        /// Private constructor to handle multiple authentication methods
        /// </summary>
        private SecureSftpClient(
            string host,
            string username,
            string privateKeyPath = null,
            string passphrase = null,
            string password = null,
            int port = 22)
        {
            ErrorMessage = new List<string>();
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be empty", nameof(host));
            
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));

            if (string.IsNullOrWhiteSpace(privateKeyPath) && string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Either private key or password must be provided");

            if (! string.IsNullOrWhiteSpace(privateKeyPath) && !File.Exists(privateKeyPath))
                throw new FileNotFoundException("Private key file not found.", privateKeyPath);

            var connectionInfo = CreateConnectionInfo(host, port, username, privateKeyPath, passphrase, password);

            _sftpClient = new SftpClient(connectionInfo);
        }

        /// <summary>
        /// Create connection info with flexible authentication methods
        /// </summary>
        private ConnectionInfo CreateConnectionInfo(
            string host,
            int port,
            string username,
            string privateKeyPath,
            string passphrase,
            string password)
        {
            var authMethods = new List<AuthenticationMethod>();

            if (!string.IsNullOrWhiteSpace(privateKeyPath))
            {
                // Validate private key file exists
                if (!File.Exists(privateKeyPath))
                    throw new FileNotFoundException("Private key file not found", privateKeyPath);

                // Use private key authentication method
                var privateKeyFile = new PrivateKeyFile(privateKeyPath, passphrase ?? string.Empty);
                authMethods.Add(new PrivateKeyAuthenticationMethod(username, privateKeyFile));
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                authMethods.Add(new PasswordAuthenticationMethod(username, password));
            }

            // Create and return connection info
            return new ConnectionInfo(
                host,
                port,
                username,
                authMethods.ToArray()
            );
        }

        /// <summary>
        /// Establish secure connection to SFTP server
        /// </summary>
        public void Connect()
        {
            try
            {
                
                
                _sftpClient.Connect();

            }
            catch (Exception ex)
            {
                string msg = $"Connection failed: {ex.Message}";
                AddError(msg, true, ex);
                
            }
        }

        /// <summary>
        /// List all folders in a given directory
        /// </summary>
        /// <param name="remotePath">Remote directory path to list</param>
        /// <returns>List of folder names</returns>
        public List<string> ListFolders(string remotePath = ".")
        {
            EnsureConnected();
            var folders = new List<string>();
            try
            {
                
                foreach (var file in _sftpClient.ListDirectory(remotePath))
                {
                    if (file.IsDirectory && file.Name != "." && file.Name != "..")
                    {
                        folders.Add(file.Name);
                    }
                }
                
            }
            catch (Exception ex)
            {
                string msg = $"Error listing folders: {ex.Message}";
                AddError(msg, true, ex);
                
            }
            return folders;
        }

        /// <summary>
        /// List files in a specific directory
        /// </summary>
        /// <param name="remotePath">Remote directory path to list files</param>
        /// <returns>List of file names</returns>
        public List<string> ListFiles(string remotePath = ".")
        {
            EnsureConnected();
            var files = new List<string>();

            try
            {
                
                foreach (var file in _sftpClient.ListDirectory(remotePath))
                {
                    if (file.IsRegularFile)
                    {
                        files.Add(file.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error listing files:  {ex.Message}";
                AddError(msg, true, ex);
            }
            return files;
        }

        /// <summary>
        /// Read content of a specific file
        /// </summary>
        /// <param name="remoteFilePath">Full path to remote file</param>
        /// <returns>File content as string</returns>
        public string GetTextFileContent(string remoteFilePath)
        {
            EnsureConnected();

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    _sftpClient.DownloadFile(remoteFilePath, memoryStream);
                    memoryStream.Position = 0;
                    using (var reader = new StreamReader(memoryStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error reading file:   {ex.Message}";
                AddError(msg, true, ex);
                
            }
            return default;
        }    
        /// <summary>
        /// Retrieve binary file content from the SFTP server.
        /// Implements best practices for file download and security.
        /// </summary>
        /// <param name="remoteFilePath">Full path to the remote file</param>
        /// <returns>Byte array containing the file content</returns>
        public byte[] GetBinaryFileContent(string remoteFilePath)
        {
            // Validate connection and input
            EnsureConnected();


            try
            {
                if (string.IsNullOrWhiteSpace(remoteFilePath))
                    throw new ArgumentException("Remote file path cannot be null or empty.", nameof(remoteFilePath));
                // Check file existence and permissions
                var fileAttributes = _sftpClient.GetAttributes(remoteFilePath);

                // Optional: Add file size limit to prevent massive downloads
                
                if (fileAttributes.Size > MaxFileSize)
                {
                    throw new IOException($"File exceeds maximum allowed size of {MaxFileSize} bytes.");
                }

                // Download file content
                using (var memoryStream = new MemoryStream())
                {
                    _sftpClient.DownloadFile(remoteFilePath, memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                string msg = $"File Download Error:   {ex.Message}";
                AddError(msg, true, ex);
            }
            return default;
        }


        /// <summary>
        /// Rename a file securely
        /// </summary>
        /// <param name="oldPath">Current file path</param>
        /// <param name="newPath">New file path</param>
        public void RenameFile(string oldPath, string newPath)
        {
            EnsureConnected();

            try
            {
                _sftpClient.RenameFile(oldPath, newPath);
            }
            catch (Exception ex)
            {
                string msg = $"Error renaming file: {ex.Message}";
                AddError(msg, true, ex);

            }
        }

        /// <summary>
        /// Ensure connection is active before operations
        /// </summary>
        private void EnsureConnected()
        {
            
            if (!_sftpClient.IsConnected)
            {
                int numberOfTentatives = 0;
                while (numberOfTentatives < MaxConnectionTentatives)
                {
                    try
                    {
                        Connect();
                        if (_sftpClient.IsConnected)
                        {
                            break;
                        }

                    }
                    catch { 
                    
                    }
                    numberOfTentatives++;
                    Thread.Sleep(WaitTimeBetweenTentativesInMiliseconds);
                }
                if (!_sftpClient.IsConnected)
                {
                    string msg = "Connection failed for {MaxConnectionTentatives} times";
                    AddError(msg, false,null);
                    
                }
            }
        }

        /// <summary>
        /// Disconnect from SFTP server
        /// </summary>
        public void Disconnect()
        {
            ErrorMessage = new List<string>();
            if (_sftpClient.IsConnected)
            {
                try
                {
                    _sftpClient.Disconnect();
                }
                catch (Exception ex)
                {
                    string msg = $"Error during disconnection: {ex.Message}";
                    ErrorMessage.Add(msg);
                    
                }
            }
        }

        /// <summary>
        /// Implement IDisposable for proper resource management
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    Disconnect();
                    _sftpClient?.Dispose();
                }

                _disposed = true;
            }
        }
        /// <summary>
        /// Error Handling
        /// </summary>
        /// <param name="errorMeesse">Error Message</param>
        /// <param name="throwException">Suposed to throw an exception?</param>
        /// <param name="ex">The Exception</param>
        private void AddError(string errorMeesse, bool throwException, Exception ex)
        {
            ErrorMessage.Add(errorMeesse);
            if (throwException)
            {
                throw new Exception(errorMeesse, ex);
            }
        }
    }
}
