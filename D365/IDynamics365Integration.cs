using DQT.Enum;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.D365
{
    /*    internal interface 
        {
        }*/

    public interface IDynamics365Integration : IDisposable
    {
        /// <summary>
        /// Tests the connection to Dynamics 365
        /// </summary>
        /// <returns>True if connection is successful, otherwise false</returns>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// Creates a new entity record
        /// </summary>
        /// <param name="attributes">Attributes to set for the new record</param>
        /// <returns>The GUID of the newly created record</returns>
        Task<Guid> CreateRecordAsync(Entity attributes);

        /// <summary>
        /// Retrieves an entity record by ID
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        /// <param name="id">GUID of the record to retrieve</param>
        /// <param name="columnSet">Columns to retrieve (optional)</param>
        /// <returns>The retrieved entity</returns>
        Task<Entity> RetrieveRecordAsync(string entityLogicalName, Guid id, string[] columnSet = null);

        /// <summary>
        /// Updates an existing entity record
        /// </summary>
        /// <param name="entityToUpdate">The entity with updated attributes</param>
        Task UpdateRecordAsync(Entity entityToUpdate);

        /// <summary>
        /// Deletes an entity record
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        /// <param name="id">GUID of the record to delete</param>
        Task DeleteRecordAsync(string entityLogicalName, Guid id);

        /// <summary>
        /// Retrieves multiple records based on a query
        /// </summary>
        /// <param name="query">The query to filter records</param>
        /// <returns>A collection of entities matching the query</returns>
        Task<EntityCollection> RetrieveMultipleAsync(string query);
        Entity PrepareEntity<TParameter>(string entityLogicalName, Guid id, TParameter obj,
                Dictionary<string, string> mapping, D365Action action );
        Task AddToQueue<TParameter>(string entityLogicalName, Guid id, TParameter obj, Dictionary<string,string> mapping,D365Action action);
        Task<BulkOperationResult> CommitChangesAsync(IProgress<int> progress);
    }

}
