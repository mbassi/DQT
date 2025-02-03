using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Security.KeyVault.Keys;
using Microsoft.Extensions.Logging;
using DQT.Enums;
using Azure;
using DQT.Security.Cryptography.Services;
using DQT.Security.Cryptography.Models;
using System.Text;

namespace DQT.Azure.KeyVault.Services
{
    /// <summary>
    /// Service for interacting with Azure Key Vault with enhanced security practices
    /// </summary>
    public class KeyVaultService : IKeyVaultService ,IDisposable
    {
        private readonly Uri _keyVaultUri;
        private readonly string _clientId;
        private readonly ILogger<KeyVaultService> _logger; 
        private readonly string _clientSecret;
        private readonly string _tenantId;
        private readonly bool _encrypt = true;
        private byte[] _encryptionKey;
        private byte[] _encryptionIV;
        private readonly IAesEncryption encryptServuce = new AesEncryption();
        public string KeySecretName { get; set; } = "DQTContent";
        public string IVSecretName { get; set; } = "DQTValue";
        private SecretClient _secretClient { get; set; }

        
        private KeyClient _keyClient { get; set; }
        
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the AzureKeyVaultService
        /// </summary>
        /// <param name="keyVaultUri">The URI of the Azure Key Vault</param>
        /// <param name="clientId">The Azure AD application (client) ID</param>
        /// <param name="clientSecret">The Azure AD application client secret</param>
        /// <param name="logger">Optional logger for tracking operations</param>
        public KeyVaultService(
            string tenantId,
            string keyVaultUri,
            string clientId,
            string clientSecret,
            ILogger<KeyVaultService> logger,
            bool encrypt = true)
        {
            _keyVaultUri = new Uri(keyVaultUri);
            _clientId = clientId;
            _clientSecret = clientSecret;
            _tenantId = tenantId;
            _logger = logger;
            _encrypt = encrypt;
            ValidateInputParameters(keyVaultUri, clientId, clientSecret);
            Authenticate();
            
            
        }
        /// <summary>
        /// Initializes a new instance of the AzureKeyVaultService
        /// </summary>
        /// <param name="keyVaultUri">The URI of the Azure Key Vault</param>
        /// <param name="clientId">The Azure AD application (client) ID</param>
        /// <param name="clientSecret">The Azure AD application client secret</param>
        /// <param name="encryptionKey">The Encryption Key</param>
        /// /// <param name="encryptionIV">The Encryption IV</param>
        /// <param name="logger">Optional logger for tracking operations</param>
        public KeyVaultService(
            string tenantId,
            string keyVaultUri,
            string clientId,
            string clientSecret,
            string encryptionKey,
            string encryptionIV,
            ILogger<KeyVaultService> logger)
        {
            _keyVaultUri = new Uri(keyVaultUri);
            _clientId = clientId;
            _clientSecret = clientSecret;
            _tenantId = tenantId;
            _logger = logger;
            _encrypt = false;
            _encrypt = true;
            _encryptionKey = Convert.FromBase64String(encryptionKey);
            _encryptionIV = Convert.FromBase64String(encryptionIV);
            ValidateInputParameters(keyVaultUri, clientId, clientSecret);
            Authenticate();


        }

        /// <summary>
        /// Validates input parameters to prevent null or empty values
        /// </summary>
        private void ValidateInputParameters(string keyVaultUri, string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(keyVaultUri))
                throw new ArgumentException("Key Vault URI cannot be null or empty", nameof(keyVaultUri));

            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

            if (string.IsNullOrWhiteSpace(clientSecret))
                throw new ArgumentException("Client Secret cannot be null or empty", nameof(clientSecret));
        }

        /// <summary>
        /// Creates a ClientSecretCredential for authentication
        /// </summary>
        private ClientSecretCredential CreateClientSecretCredential(string clientId, string clientSecret)
        {
            try
            {
                // Use DefaultAzureCredential for more robust authentication
                return new ClientSecretCredential(
                    _tenantId,
                    clientId,
                    clientSecret
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create Azure credentials");
                throw new AuthenticationException("Unable to create Azure credentials", ex);
            }
        }
        private async Task GetEncryptedKeyIV()
        {

            if (_encryptionKey == null)
            {

                try
                {
                    KeyVaultSecret secretEncryptionKey = await _secretClient.GetSecretAsync(KeySecretName);
                    _encryptionKey = Convert.FromBase64String(secretEncryptionKey.Value);
                }
                catch (Exception ex)
                {

                    _logger.LogError(ex, ex.Message);
                    _encryptionKey = encryptServuce.GenerateKey();
                    await _secretClient.SetSecretAsync(KeySecretName, Convert.ToBase64String(_encryptionKey));


                }
            }
            if (_encryptionIV == null)
            {
                try
                {
                    KeyVaultSecret secretEncryptionIV = await _secretClient.GetSecretAsync(IVSecretName);
                    _encryptionIV = Convert.FromBase64String(secretEncryptionIV.Value);

                }
                catch
                {
                    _encryptionIV = encryptServuce.GenerateIV();
                    await _secretClient.SetSecretAsync(IVSecretName, Convert.ToBase64String(_encryptionIV));
                }
            }

        }
        private async Task<string> GetEncryptedDataAsync(string value)
        {
            string returnValue = value;
            if (_encrypt == true)
            {
                await GetEncryptedKeyIV();
                if (_encryptionKey == null)
                {
                    throw new InvalidOperationException("Unable to get Key");
                }
                if (_encryptionKey == null)
                {
                    throw new InvalidOperationException("Unable to get IV");
                }
                var model = encryptServuce.Encrypt(value,_encryptionKey, _encryptionIV);
                returnValue = model.EncryptedData;
            }
            return returnValue;
        }
        /// <summary>
        /// Authenticates to Azure Key Vault
        /// </summary>
        /// <returns>A boolean indicating successful authentication</returns>
        public void Authenticate()
        {
            try
            {
                // Use ClientSecretCredential for service principal authentication
                // Validate input parameters


                // Create credential using client secret
                var clientSecretCredential = CreateClientSecretCredential(_clientId, _clientSecret);

                // Initialize Key Vault clients
                _secretClient = new SecretClient(_keyVaultUri, clientSecretCredential);
                _keyClient = new KeyClient(_keyVaultUri, clientSecretCredential);

                // Perform a simple operation to verify authentication
                AsyncPageable<SecretProperties> props = _secretClient.GetPropertiesOfSecretsAsync();
                _logger.LogInformation("Successfully authenticated to Azure Key Vault");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication to Azure Key Vault failed");
                throw new AuthenticationException("Unable to Authenticate", ex);
            }
            
        }

        /// <summary>
        /// Creates or updates a secret in the Key Vault with enhanced security
        /// </summary>
        /// <param name="secretName">Name of the secret</param>
        /// <param name="secretValue">Value of the secret</param>
        public async Task CreateOrUpdateSecretAsync(string secretName, string secretValue)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(secretName))
                throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));

            if (string.IsNullOrWhiteSpace(secretValue))
                throw new ArgumentException("Secret value cannot be null or empty", nameof(secretValue));

            try
            {
                // Ensure authentication before creating/updating secret
                if (_secretClient == null)
                {
                    throw new InvalidOperationException("Authentication failed");
                }
                secretValue = await GetEncryptedDataAsync(secretValue);

                await _secretClient.SetSecretAsync(secretName, secretValue);

                _logger.LogInformation($"Successfully created/updated secret: {secretName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating/updating secret: {secretName}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a secret from Azure Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret to retrieve</param>
        /// <returns>The secret value</returns>
        public async Task<string> GetSecretAsync(string secretName)
        {
            if (_logger == null)
            {
                Console.WriteLine("Warning: Logger is null in KeyVaultService");
            }

            _logger?.LogDebug("Starting GetSecretAsync for secret: {SecretName}", secretName);

            try
            {
                if (_secretClient == null)
                {
                    _logger?.LogError("SecretClient is null. Authentication may have failed.");
                    throw new InvalidOperationException("SecretClient is not initialized. Please ensure Authenticate() was called successfully.");
                }

                _logger?.LogDebug("About to call GetSecretAsync on _secretClient");
                Console.WriteLine($"Attempting to retrieve secret: {secretName}"); // Direct console output for debugging

                KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);

                if (secret == null)
                {
                    _logger?.LogError("Retrieved secret is null");
                    Console.WriteLine("Retrieved secret is null"); // Direct console output for debugging
                    throw new KeyNotFoundException($"Secret '{secretName}' not found in Key Vault");
                }

                Console.WriteLine($"Successfully retrieved secret: {secretName}"); // Direct console output for debugging
                _logger?.LogInformation("Successfully retrieved secret: {SecretName}", secretName);

                string value = secret.Value;

                if (_encrypt)
                {
                    _logger?.LogDebug("Starting decryption process");
                    await GetEncryptedKeyIV();
                    if (_encryptionKey == null || _encryptionIV == null)
                    {
                        _logger?.LogError("Encryption keys are not properly initialized");
                        throw new InvalidOperationException("Encryption keys are not properly initialized");
                    }
                    value = encryptServuce.Decrypt(value, _encryptionKey, _encryptionIV);
                    _logger?.LogDebug("Decryption completed successfully");
                }

                return value;
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"Azure Key Vault error: {ex.Message}"); // Direct console output for debugging
                _logger?.LogError(ex, "Azure Key Vault error retrieving secret '{SecretName}'. Status: {Status}", secretName, ex.Status);
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General error: {ex.Message}"); // Direct console output for debugging
                _logger?.LogError(ex, "Error retrieving secret: {SecretName}", secretName);
                throw;
            }
        }

        /// <summary>
        /// Sets or updates a secret in Azure Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret</param>
        /// <param name="secretValue">Value of the secret</param>
        public async Task SetSecretAsync(string secretName, string secretValue)
        {
            try
            {
                // Validate secret value
                if (string.IsNullOrWhiteSpace(secretValue))
                    throw new ArgumentException("Secret value cannot be null or empty", nameof(secretValue));

                await _secretClient.SetSecretAsync(secretName, secretValue);
                _logger?.LogInformation($"Successfully set/updated secret: {secretName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error setting secret: {secretName}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a secret from Azure Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret to delete</param>
        public async Task DeleteSecretAsync(string secretName)
        {
            try
            {
                DeleteSecretOperation operation = await _secretClient.StartDeleteSecretAsync(secretName);

                // Wait for the delete operation to complete
                await operation.WaitForCompletionAsync();

                _logger?.LogInformation($"Successfully deleted secret: {secretName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error deleting secret: {secretName}");
                throw;
            }
        }

        /// <summary>
        /// Lists all secrets in the Key Vault
        /// </summary>
        /// <returns>Enumerable of secret names</returns>
        public async Task<IEnumerable<string>> ListSecretsAsync()
        {
            try
            {
                var secrets = new List<string>();
                await foreach (SecretProperties secretProperties in _secretClient.GetPropertiesOfSecretsAsync())
                {
                    secrets.Add(secretProperties.Name);
                }
                return secrets;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error listing secrets");
                throw;
            }
        }

        /// <summary>
        /// Creates a new cryptographic key in Azure Key Vault
        /// </summary>
        /// <param name="keyName">Name of the key to create</param>
        /// <param name="keyType">Type of key to create</param>
        public async Task CreateKeyAsync(string keyName, KeyType keyType )
        {
            try
            {
                await _keyClient.CreateKeyAsync(keyName, keyType);
                _logger?.LogInformation($"Successfully created key: {keyName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creating key: {keyName}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a key from Azure Key Vault
        /// </summary>
        /// <param name="keyName">Name of the key to delete</param>
        public async Task DeleteKeyAsync(string keyName)
        {
            try
            {
                DeleteKeyOperation operation = await _keyClient.StartDeleteKeyAsync(keyName);

                // Wait for the delete operation to complete
                await operation.WaitForCompletionAsync();

                _logger?.LogInformation($"Successfully deleted key: {keyName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error deleting key: {keyName}");
                throw;
            }
        }

        /// <summary>
        /// Lists all keys in the Key Vault
        /// </summary>
        /// <returns>Enumerable of key names</returns>
        public async Task<IEnumerable<string>> ListKeysAsync()
        {
            try
            {
                var keys = new List<string>();
                await foreach (KeyProperties keyProperties in _keyClient.GetPropertiesOfKeysAsync())
                {
                    keys.Add(keyProperties.Name);
                }
                return keys;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error listing keys");
                throw;
            }
        }

        /// <summary>
        /// Implements secure disposal of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        /// <param name="disposing">Whether the method is called from Dispose()</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    _logger?.LogInformation("AzureKeyVaultService is being disposed.");
                }

                // Clean up unmanaged resources if any

                _disposed = true;
            }
        }

        /// <summary>
        /// Destructor to ensure resources are cleaned up
        /// </summary>
        ~KeyVaultService()
        {
            Dispose(false);
        }
    }
}
