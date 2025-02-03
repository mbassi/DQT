using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace DQT.SFTP
{
    /// <summary>
    /// Secure SFTP Client with robust error handling, security best practices, and comprehensive logging
    /// </summary>
    public class SecureSftpClient : ISecureSftpClient, IDisposable
    {
        private readonly SftpClient _sftpClient;
        private readonly ILogger<SecureSftpClient> _logger;
        private bool _disposed = false;
        private readonly string _correlationId;

        public List<string> ErrorMessage { get; private set; }
        public int MaxConnectionTentatives { get; set; } = 3;
        public int WaitTimeBetweenTentativesInMiliseconds { get; set; } = 3000;
        public long MaxFileSize { get; set; } = 1_073_741_824; // 1 GB

        /// <summary>
        /// Constructor for private key authentication with logging
        /// </summary>
        public SecureSftpClient(
            string host,
            string username,
            string privateKeyPath,
            ILogger<SecureSftpClient> logger,
            string passphrase = null,
            int port = 22) : this(host, username, privateKeyPath, logger, passphrase, null, port)
        {
        }

        /// <summary>
        /// Constructor for username and password authentication with logging
        /// </summary>
        public SecureSftpClient(
            string host,
            string username,
            string password,
            ILogger<SecureSftpClient> logger,
            int port = 22) : this(host, username, null, logger, null, password, port)
        {
        }

        /// <summary>
        /// Private constructor to handle multiple authentication methods
        /// </summary>
        private SecureSftpClient(
            string host,
            string username,
            string privateKeyPath,
            ILogger<SecureSftpClient> logger,
            string passphrase = null,
            string password = null,
            int port = 22)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _correlationId = Guid.NewGuid().ToString();

            _logger.LogInformation("Initializing SecureSftpClient with correlation ID: {CorrelationId}", _correlationId);

            ErrorMessage = new List<string>();

            ValidateConstructorParameters(host, username, privateKeyPath, password, port);

            var connectionInfo = CreateConnectionInfo(host, port, username, privateKeyPath, passphrase, password);
            _sftpClient = new SftpClient(connectionInfo);

            _logger.LogDebug("SecureSftpClient initialized successfully for host: {Host}:{Port}", host, port);
        }

        private void ValidateConstructorParameters(string host, string username, string privateKeyPath, string password, int port)
        {
            _logger.LogDebug("Validating constructor parameters");

            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogError("Host parameter validation failed");
                throw new ArgumentException("Host cannot be empty", nameof(host));
            }

            if (port <= 0 || port > 65535)
            {
                _logger.LogError("Port parameter validation failed: {Port}", port);
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogError("Username parameter validation failed");
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));
            }

            if (string.IsNullOrWhiteSpace(privateKeyPath) && string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("Authentication method validation failed - neither private key nor password provided");
                throw new ArgumentException("Either private key or password must be provided");
            }

            if (!string.IsNullOrWhiteSpace(privateKeyPath) && !File.Exists(privateKeyPath))
            {
                _logger.LogError("Private key file not found at path: {PrivateKeyPath}", privateKeyPath);
                throw new FileNotFoundException("Private key file not found.", privateKeyPath);
            }
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
            _logger.LogInformation("Attempting to connect to SFTP server [CorrelationId: {CorrelationId}]", _correlationId);

            try
            {
                _sftpClient.Connect();
                _logger.LogInformation("Successfully connected to SFTP server [CorrelationId: {CorrelationId}]", _correlationId);
            }
            catch (Exception ex)
            {
                string msg = $"Connection failed: {ex.Message}";
                _logger.LogError(ex, "Failed to connect to SFTP server [CorrelationId: {CorrelationId}]", _correlationId);
                AddError(msg, true, ex);
            }
        }

        /// <summary>
        /// List all folders in a given directory
        /// </summary>
        public List<string> ListFolders(string remotePath = ".")
        {
            _logger.LogInformation("Listing folders in path: {RemotePath} [CorrelationId: {CorrelationId}]",
                remotePath, _correlationId);

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

                _logger.LogInformation("Successfully listed {Count} folders in {RemotePath} [CorrelationId: {CorrelationId}]",
                    folders.Count, remotePath, _correlationId);
            }
            catch (Exception ex)
            {
                string msg = $"Error listing folders: {ex.Message}";
                _logger.LogError(ex, "Failed to list folders in {RemotePath} [CorrelationId: {CorrelationId}]",
                    remotePath, _correlationId);
                AddError(msg, true, ex);
            }

            return folders;
        }

        /// <summary>
        /// Ensure connection is active before operations
        /// </summary>
        private void EnsureConnected()
        {
            if (!_sftpClient.IsConnected)
            {
                _logger.LogWarning("Connection check failed, attempting to reconnect [CorrelationId: {CorrelationId}]",
                    _correlationId);

                int numberOfTentatives = 0;
                while (numberOfTentatives < MaxConnectionTentatives)
                {
                    try
                    {
                        _logger.LogInformation("Reconnection attempt {Attempt} of {MaxAttempts} [CorrelationId: {CorrelationId}]",
                            numberOfTentatives + 1, MaxConnectionTentatives, _correlationId);

                        Connect();
                        if (_sftpClient.IsConnected)
                        {
                            _logger.LogInformation("Reconnection successful [CorrelationId: {CorrelationId}]", _correlationId);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Reconnection attempt {Attempt} failed [CorrelationId: {CorrelationId}]",
                            numberOfTentatives + 1, _correlationId);
                    }

                    numberOfTentatives++;
                    Thread.Sleep(WaitTimeBetweenTentativesInMiliseconds);
                }

                if (!_sftpClient.IsConnected)
                {
                    string msg = $"Connection failed after {MaxConnectionTentatives} attempts";
                    _logger.LogError(msg + " [CorrelationId: {CorrelationId}]", _correlationId);
                    AddError(msg, false, null);
                }
            }
        }

        /// <summary>
        /// Disconnect from SFTP server
        /// </summary>
        public void Disconnect()
        {
            _logger.LogInformation("Initiating disconnect [CorrelationId: {CorrelationId}]", _correlationId);

            ErrorMessage = new List<string>();
            if (_sftpClient.IsConnected)
            {
                try
                {
                    _sftpClient.Disconnect();
                    _logger.LogInformation("Successfully disconnected [CorrelationId: {CorrelationId}]", _correlationId);
                }
                catch (Exception ex)
                {
                    string msg = $"Error during disconnection: {ex.Message}";
                    _logger.LogError(ex, "Disconnect operation failed [CorrelationId: {CorrelationId}]", _correlationId);
                    ErrorMessage.Add(msg);
                }
            }
        }

        /// <summary>
        /// Error Handling with logging
        /// </summary>
        private void AddError(string errorMessage, bool throwException, Exception ex)
        {
            ErrorMessage.Add(errorMessage);
            _logger.LogError(ex, "Error occurred: {ErrorMessage} [CorrelationId: {CorrelationId}]",
                errorMessage, _correlationId);

            if (throwException)
            {
                throw new Exception(errorMessage, ex);
            }
        }

        /// <summary>
        /// List files in a specific directory
        /// </summary>
        public List<string> ListFiles(string remotePath = ".")
        {
            _logger.LogInformation("Listing files in path: {RemotePath} [CorrelationId: {CorrelationId}]",
                remotePath, _correlationId);

            EnsureConnected();
            var files = new List<string>();

            try
            {
                foreach (var file in _sftpClient.ListDirectory(remotePath))
                {
                    if (file.IsRegularFile)
                    {
                        files.Add(file.Name);
                        _logger.LogDebug("Found file: {FileName} in {RemotePath} [CorrelationId: {CorrelationId}]",
                            file.Name, remotePath, _correlationId);
                    }
                }

                _logger.LogInformation("Successfully listed {Count} files in {RemotePath} [CorrelationId: {CorrelationId}]",
                    files.Count, remotePath, _correlationId);
            }
            catch (Exception ex)
            {
                string msg = $"Error listing files: {ex.Message}";
                _logger.LogError(ex, "Failed to list files in {RemotePath} [CorrelationId: {CorrelationId}]",
                    remotePath, _correlationId);
                AddError(msg, true, ex);
            }

            return files;
        }

        /// <summary>
        /// Read content of a specific file
        /// </summary>
        public string GetTextFileContent(string remoteFilePath)
        {
            _logger.LogInformation("Reading text file content from: {RemoteFilePath} [CorrelationId: {CorrelationId}]",
                remoteFilePath, _correlationId);

            EnsureConnected();

            try
            {
                var fileAttributes = _sftpClient.GetAttributes(remoteFilePath);
                _logger.LogDebug("File size: {FileSize} bytes [CorrelationId: {CorrelationId}]",
                    fileAttributes.Size, _correlationId);

                if (fileAttributes.Size > MaxFileSize)
                {
                    string msg = $"File exceeds maximum allowed size of {MaxFileSize} bytes.";
                    _logger.LogWarning(msg + " [CorrelationId: {CorrelationId}]", _correlationId);
                    throw new IOException(msg);
                }

                using (var memoryStream = new MemoryStream())
                {
                    _logger.LogDebug("Downloading file to memory stream [CorrelationId: {CorrelationId}]", _correlationId);
                    _sftpClient.DownloadFile(remoteFilePath, memoryStream);
                    memoryStream.Position = 0;

                    using (var reader = new StreamReader(memoryStream))
                    {
                        string content = reader.ReadToEnd();
                        _logger.LogInformation("Successfully read text file content, length: {ContentLength} characters [CorrelationId: {CorrelationId}]",
                            content.Length, _correlationId);
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error reading file: {ex.Message}";
                _logger.LogError(ex, "Failed to read text file content from {RemoteFilePath} [CorrelationId: {CorrelationId}]",
                    remoteFilePath, _correlationId);
                AddError(msg, true, ex);
            }

            return default;
        }

        /// <summary>
        /// Retrieve binary file content from the SFTP server.
        /// </summary>
        public byte[] GetBinaryFileContent(string remoteFilePath)
        {
            _logger.LogInformation("Retrieving binary file content from: {RemoteFilePath} [CorrelationId: {CorrelationId}]",
                remoteFilePath, _correlationId);

            EnsureConnected();

            try
            {
                if (string.IsNullOrWhiteSpace(remoteFilePath))
                {
                    _logger.LogError("Remote file path is null or empty [CorrelationId: {CorrelationId}]", _correlationId);
                    throw new ArgumentException("Remote file path cannot be null or empty.", nameof(remoteFilePath));
                }

                var fileAttributes = _sftpClient.GetAttributes(remoteFilePath);
                _logger.LogDebug("File size: {FileSize} bytes [CorrelationId: {CorrelationId}]",
                    fileAttributes.Size, _correlationId);

                if (fileAttributes.Size > MaxFileSize)
                {
                    string msg = $"File exceeds maximum allowed size of {MaxFileSize} bytes.";
                    _logger.LogWarning(msg + " [CorrelationId: {CorrelationId}]", _correlationId);
                    throw new IOException(msg);
                }

                using (var memoryStream = new MemoryStream())
                {
                    _logger.LogDebug("Downloading binary file to memory stream [CorrelationId: {CorrelationId}]", _correlationId);
                    _sftpClient.DownloadFile(remoteFilePath, memoryStream);

                    byte[] content = memoryStream.ToArray();
                    _logger.LogInformation("Successfully retrieved binary file content, size: {ContentLength} bytes [CorrelationId: {CorrelationId}]",
                        content.Length, _correlationId);
                    return content;
                }
            }
            catch (Exception ex)
            {
                string msg = $"File Download Error: {ex.Message}";
                _logger.LogError(ex, "Failed to retrieve binary file content from {RemoteFilePath} [CorrelationId: {CorrelationId}]",
                    remoteFilePath, _correlationId);
                AddError(msg, true, ex);
            }

            return default;
        }

        /// <summary>
        /// Rename a file securely
        /// </summary>
        public void RenameFile(string oldPath, string newPath)
        {
            _logger.LogInformation("Renaming file from {OldPath} to {NewPath} [CorrelationId: {CorrelationId}]",
                oldPath, newPath, _correlationId);

            EnsureConnected();

            try
            {
                // Validate paths
                if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
                {
                    _logger.LogError("File paths cannot be null or empty [CorrelationId: {CorrelationId}]", _correlationId);
                    throw new ArgumentException("File paths cannot be null or empty");
                }

                // Check if source file exists
                if (!_sftpClient.Exists(oldPath))
                {
                    string msg = $"Source file {oldPath} does not exist";
                    _logger.LogError(msg + " [CorrelationId: {CorrelationId}]", _correlationId);
                    throw new FileNotFoundException(msg);
                }

                // Check if destination already exists
                if (_sftpClient.Exists(newPath))
                {
                    string msg = $"Destination file {newPath} already exists";
                    _logger.LogWarning(msg + " [CorrelationId: {CorrelationId}]", _correlationId);
                }

                _sftpClient.RenameFile(oldPath, newPath);
                _logger.LogInformation("Successfully renamed file [CorrelationId: {CorrelationId}]", _correlationId);
            }
            catch (Exception ex)
            {
                string msg = $"Error renaming file: {ex.Message}";
                _logger.LogError(ex, "Failed to rename file from {OldPath} to {NewPath} [CorrelationId: {CorrelationId}]",
                    oldPath, newPath, _correlationId);
                AddError(msg, true, ex);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger.LogInformation("Disposing SecureSftpClient [CorrelationId: {CorrelationId}]", _correlationId);
                    Disconnect();
                    _sftpClient?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}