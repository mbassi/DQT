using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.D365
{
    public class Dynamics365Integration : IDynamics365Integration
    {
        private readonly ServiceClient _serviceClient;

        /// <summary>
        /// Initializes a new instance of the DynamicsConnectionService
        /// </summary>
        /// <param name="instanceUrl">The URL of your Dynamics 365 instance</param>
        /// <param name="clientId">The ClientId for authentication</param>
        /// <param name="clientSecret">The ClientSecret for authentication</param>
        /// <param name="tenantId">The Azure AD Tenant ID</param>
        public Dynamics365Integration(string instanceUrl, string clientId, string clientSecret, string tenantId)
        {
            // Construct the connection string
            string connectionString = $@"AuthType=ClientSecret;Url={instanceUrl};ClientId={clientId};ClientSecret={clientSecret};TenantId={tenantId}";

            // Create the service client
            _serviceClient = new ServiceClient(connectionString);
        }

        /// <summary>
        /// Verifies the connection to Dynamics 365
        /// </summary>
        /// <returns>True if connection is successful, otherwise false</returns>
        public async Task<bool> TestConnectionAsync()
        {
            var whoAmIRequest = new WhoAmIRequest();
            var whoAmIResponse = (WhoAmIResponse)await _serviceClient.ExecuteAsync(whoAmIRequest);
            Console.WriteLine($"Connected successfully. User ID: {whoAmIResponse.UserId}");
            return true;
        }

        /// <summary>
        /// Creates a new entity record
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        /// <param name="attributes">Attributes to set for the new record</param>
        /// <returns>The GUID of the newly created record</returns>
        public async Task<Guid> CreateRecordAsync( Entity attributes)
        {
            try
            {
                return await _serviceClient.CreateAsync(attributes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating record: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieves an entity record by ID
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        /// <param name="id">GUID of the record to retrieve</param>
        /// <param name="columnSet">Columns to retrieve (optional)</param>
        /// <returns>The retrieved entity</returns>
        public async Task<Entity> RetrieveRecordAsync(string entityLogicalName, Guid id, string[] columnSet = null)
        {
            try
            {
                return await _serviceClient.RetrieveAsync(entityLogicalName, id, new ColumnSet(columnSet));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving record: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing entity record
        /// </summary>
        /// <param name="entityToUpdate">The entity with updated attributes</param>
        public async Task UpdateRecordAsync(Entity entityToUpdate)
        {
            try
            {
                await _serviceClient.UpdateAsync(entityToUpdate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating record: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes an entity record
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        /// <param name="id">GUID of the record to delete</param>
        public async Task DeleteRecordAsync(string entityLogicalName, Guid id)
        {
            try
            {
                await _serviceClient.DeleteAsync(entityLogicalName, id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting record: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Disposes of the service client
        /// </summary>
        /// <summary>
        /// Retrieves multiple records based on a query
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        /// <param name="query">The query to filter records</param>
        /// <returns>A collection of entities matching the query</returns>
        public async Task<EntityCollection> RetrieveMultipleAsync( string query)
        {
            try
            {
                // Assuming FetchXml query, you can modify this to support different query types
                return await _serviceClient.RetrieveMultipleAsync(new FetchExpression(query));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving multiple records: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Implements IDisposable pattern to ensure proper resource cleanup
        /// </summary>
        public void Dispose()
        {
            _serviceClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
