using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Azure.KeyVault.Services
{
    /// <summary>
    /// Interface defining operations for Azure Key Vault management
    /// </summary>
    public interface IKeyVaultService
    {

        
        /// <summary>
        /// Authenticates to Azure Key Vault
        /// </summary>
        /// <returns>A boolean indicating successful authentication</returns>
        void Authenticate();

        /// <summary>
        /// Retrieves a secret from Azure Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret to retrieve</param>
        /// <returns>The secret value</returns>
        Task<string> GetSecretAsync(string secretName);

        /// <summary>
        /// Lists all secrets in the Key Vault
        /// </summary>
        /// <returns>A collection of secret names</returns>
        Task<IEnumerable<string>> ListSecretsAsync();

        /// <summary>
        /// Creates or updates a secret in the Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret</param>
        /// <param name="secretValue">Value of the secret</param>
        /// <returns>A task representing the operation</returns>
        Task CreateOrUpdateSecretAsync(string secretName, string secretValue);

        /// <summary>
        /// Deletes a secret from the Key Vault
        /// </summary>
        /// <param name="secretName">Name of the secret to delete</param>
        /// <returns>A task representing the operation</returns>
        Task DeleteSecretAsync(string secretName);
    }
}
