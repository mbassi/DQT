using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Security.KeyVault.Keys;
using Microsoft.Extensions.Logging;
using DQT.Utilities;
using Azure;
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
            ILogger<KeyVaultService> logger)
        {

            ValidateInputParameters(keyVaultUri, clientId, clientSecret);
            AuthenticateAsync();
            _keyVaultUri = new Uri(keyVaultUri);
            _clientId = clientId;
            _clientSecret = clientSecret;   
            _tenantId = tenantId;
            _logger = logger;
            
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
        /// <summary>
        /// Authenticates to Azure Key Vault
        /// </summary>
        /// <returns>A boolean indicating successful authentication</returns>
        public void AuthenticateAsync()
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

                

                // Create or update the secret
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
            try
            {
                KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
                _logger?.LogInformation($"Successfully retrieved secret: {secretName}");
                return secret.Value;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error retrieving secret: {secretName}");
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
