using Microsoft.Xrm.Sdk;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Collections.Concurrent;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Reflection;
using Microsoft.Crm.Sdk.Messages;
using System.ServiceModel;
using DQT.Enum;
using Microsoft.Extensions.Logging;

namespace DQT.D365
{
    public class Dynamics365Integration : IDynamics365Integration
    {
        private readonly ServiceClient _serviceClient;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<OrganizationRequest> _pendingRequests;
        private readonly int _maxBatchSize;
        private readonly ILogger<Dynamics365Integration> _logger;
        private bool _disposed;

        public Dynamics365Integration(
            string instanceUrl,
            string clientId,
            string clientSecret,
            string tenantId,
            ILogger<Dynamics365Integration> logger,
            int maxBatchSize = 1000)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("Initializing Dynamics365Integration service");

            try
            {
                string connectionString = $@"AuthType=ClientSecret;Url={instanceUrl};ClientId={clientId};ClientSecret={clientSecret};TenantId={tenantId}";
                _semaphore = new SemaphoreSlim(1, 1);
                _pendingRequests = new ConcurrentQueue<OrganizationRequest>();
                _serviceClient = new ServiceClient(connectionString);
                _maxBatchSize = maxBatchSize;

                _logger.LogInformation("Dynamics365Integration service initialized successfully with maxBatchSize: {MaxBatchSize}", maxBatchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Dynamics365Integration service");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            _logger.LogInformation("Testing connection to Dynamics 365");
            try
            {
                var whoAmIRequest = new WhoAmIRequest();
                var whoAmIResponse = (WhoAmIResponse)await _serviceClient.ExecuteAsync(whoAmIRequest);
                _logger.LogInformation("Connected successfully. User ID: {UserId}", whoAmIResponse.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Dynamics 365");
                throw;
            }
        }

        public async Task<Guid> CreateRecordAsync(Entity attributes)
        {
            try
            {
                _logger.LogInformation("Creating new {EntityType} record", attributes.LogicalName);
                var result = await _serviceClient.CreateAsync(attributes);
                _logger.LogInformation("Successfully created {EntityType} record with ID: {RecordId}",
                    attributes.LogicalName, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {EntityType} record", attributes.LogicalName);
                throw;
            }
        }

        public async Task<Entity> RetrieveRecordAsync(string entityLogicalName, Guid id, string[] columnSet = null)
        {
            try
            {
                _logger.LogInformation("Retrieving {EntityType} record with ID: {RecordId}. Columns: {Columns}",
                    entityLogicalName, id, columnSet != null ? string.Join(", ", columnSet) : "All");

                var result = await _serviceClient.RetrieveAsync(entityLogicalName, id, new ColumnSet(columnSet));

                _logger.LogInformation("Successfully retrieved {EntityType} record with ID: {RecordId}",
                    entityLogicalName, id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving {EntityType} record with ID: {RecordId}",
                    entityLogicalName, id);
                throw;
            }
        }

        public async Task UpdateRecordAsync(Entity entityToUpdate)
        {
            try
            {
                _logger.LogInformation("Updating {EntityType} record with ID: {RecordId}. Modified attributes: {Attributes}",
                    entityToUpdate.LogicalName,
                    entityToUpdate.Id,
                    string.Join(", ", entityToUpdate.Attributes.Keys));

                await _serviceClient.UpdateAsync(entityToUpdate);

                _logger.LogInformation("Successfully updated {EntityType} record with ID: {RecordId}",
                    entityToUpdate.LogicalName, entityToUpdate.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating {EntityType} record with ID: {RecordId}",
                    entityToUpdate.LogicalName, entityToUpdate.Id);
                throw;
            }
        }
        public async Task DeleteRecordAsync(string entityLogicalName, Guid id)
        {
            try
            {
                _logger.LogInformation("Deleting {EntityType} record with ID: {RecordId}",
                    entityLogicalName, id);

                await _serviceClient.DeleteAsync(entityLogicalName, id);

                _logger.LogInformation("Successfully deleted {EntityType} record with ID: {RecordId}",
                    entityLogicalName, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {EntityType} record with ID: {RecordId}",
                    entityLogicalName, id);
                throw;
            }
        }

        public async Task<EntityCollection> RetrieveMultipleAsync(string query)
        {
            try
            {
                _logger.LogInformation("Retrieving multiple records using query: {Query}", query);

                var result = await _serviceClient.RetrieveMultipleAsync(new FetchExpression(query));

                _logger.LogInformation("Successfully retrieved {Count} records", result.Entities.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving multiple records with query: {Query}", query);
                throw;
            }
        }

        public async Task AddToQueue<TParameter>(string entityLogicalName, Guid id, TParameter obj,
            Dictionary<string, string> mapping, D365Action action = D365Action.Create)
        {
            ThrowIfDisposed();

            _logger.LogInformation("Adding {Action} operation to queue for {EntityType} {RecordId}",
                action, entityLogicalName, id != Guid.Empty ? id.ToString() : "new");

            try
            {
                Entity entity = PrepareEntity(entityLogicalName, id, obj, mapping, action);

                await _semaphore.WaitAsync();
                try
                {
                    switch (action)
                    {
                        case D365Action.Create:
                            _pendingRequests.Enqueue(new CreateRequest { Target = entity });
                            _logger.LogDebug("Added Create request to queue for {EntityType}", entityLogicalName);
                            break;
                        case D365Action.Update:
                            _pendingRequests.Enqueue(new UpdateRequest { Target = entity });
                            _logger.LogDebug("Added Update request to queue for {EntityType} {RecordId}",
                                entityLogicalName, id);
                            break;
                        case D365Action.Delete:
                            _pendingRequests.Enqueue(new DeleteRequest { Target = entity.ToEntityReference() });
                            _logger.LogDebug("Added Delete request to queue for {EntityType} {RecordId}",
                                entityLogicalName, id);
                            break;
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                _logger.LogInformation("Successfully added {Action} operation to queue. Queue size: {QueueSize}",
                    action, _pendingRequests.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding {Action} operation to queue for {EntityType} {RecordId}",
                    action, entityLogicalName, id);
                throw;
            }
        }

        public async Task<BulkOperationResult> CommitChangesAsync(IProgress<int> progress = null)
        {
            ThrowIfDisposed();
            _logger.LogInformation("Starting bulk operation with {Count} pending requests", _pendingRequests.Count);

            await _semaphore.WaitAsync();
            try
            {
                var result = new BulkOperationResult
                {
                    Success = true,
                    SuccessCount = 0,
                    FailureCount = 0
                };

                var failures = new List<(OrganizationRequest, Exception)>();
                var totalRequests = _pendingRequests.Count;
                var processedRequests = 0;

                while (_pendingRequests.Count > 0)
                {
                    var batch = await CreateNextBatchAsync();
                    if (!batch.Any()) break;

                    _logger.LogInformation("Processing batch of {Count} requests", batch.Count);

                    try
                    {
                        var batchResponse = await ExecuteBatchAsync(batch);
                        ProcessBatchResponse(batchResponse, batch, failures);

                        processedRequests += batch.Count;
                        result.SuccessCount += batch.Count - failures.Count;

                        var progressPercentage = (int)((float)processedRequests / totalRequests * 100);
                        progress?.Report(progressPercentage);

                        _logger.LogInformation("Batch processed. Progress: {Progress}%. Success: {SuccessCount}, Failures: {FailureCount}",
                            progressPercentage, batch.Count - failures.Count, failures.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch of {Count} requests", batch.Count);
                        foreach (var request in batch)
                        {
                            failures.Add((request, ex));
                        }
                    }
                }

                result.Failures = failures.AsReadOnly();
                result.FailureCount = failures.Count;
                result.Success = failures.Count == 0;

                _logger.LogInformation("Bulk operation completed. Total Success: {SuccessCount}, Total Failures: {FailureCount}",
                    result.SuccessCount, result.FailureCount);

                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<List<OrganizationRequest>> CreateNextBatchAsync()
        {
            _logger.LogDebug("Creating next batch. Current queue size: {QueueSize}", _pendingRequests.Count);
            var batch = new List<OrganizationRequest>();

            while (batch.Count < _maxBatchSize && _pendingRequests.TryDequeue(out var request))
            {
                batch.Add(request);
            }

            _logger.LogDebug("Created batch of {Count} requests", batch.Count);
            return batch;
        }

        private async Task<ExecuteMultipleResponse> ExecuteBatchAsync(List<OrganizationRequest> batch)
        {
            _logger.LogDebug("Executing batch of {Count} requests", batch.Count);

            var executeMultipleRequest = new ExecuteMultipleRequest
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                }
            };

            var requests = new OrganizationRequestCollection();
            foreach (var request in batch)
            {
                requests.Add(request);
            }

            executeMultipleRequest.Requests = requests;

            try
            {
                var response = (ExecuteMultipleResponse)await _serviceClient.ExecuteAsync(executeMultipleRequest);
                _logger.LogDebug("Batch execution completed. Response count: {Count}", response.Responses.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing batch of {Count} requests", batch.Count);
                throw;
            }
        }

        private void ProcessBatchResponse(ExecuteMultipleResponse response, List<OrganizationRequest> batch,
            List<(OrganizationRequest, Exception)> failures)
        {
            _logger.LogDebug("Processing batch response with {Count} responses", response.Responses.Count);

            for (int i = 0; i < response.Responses.Count; i++)
            {
                var responseItem = response.Responses[i];
                if (responseItem.Fault != null)
                {
                    _logger.LogWarning("Fault detected in batch response item {Index}. Error: {Error}",
                        i, responseItem.Fault.Message);
                    failures.Add((batch[i], new FaultException<OrganizationServiceFault>(responseItem.Fault)));
                }
            }

            _logger.LogDebug("Batch response processing completed. Failures: {FailureCount}", failures.Count);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                _logger.LogError("Attempted to use disposed Dynamics365Integration service");
                throw new ObjectDisposedException(nameof(Dynamics365Integration));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.LogInformation("Disposing Dynamics365Integration service");
                _serviceClient?.Dispose();
                _semaphore.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
        public Entity PrepareEntity<TParameter>(string entityLogicalName, Guid id, TParameter obj,
                Dictionary<string, string> mapping, D365Action action = D365Action.Create)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(mapping);

            _logger.LogDebug("Preparing entity of type {EntityType} for {Action} operation", entityLogicalName, action);

            Entity entity = new Entity(entityLogicalName);
            if (id != Guid.Empty)
            {
                entity.Id = id;
            }

            if (action == D365Action.Delete)
            {
                _logger.LogDebug("Delete action specified, returning basic entity reference");
                return entity;
            }

            try
            {
                Type type = obj.GetType();
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                _logger.LogDebug("Processing {Count} properties for entity", properties.Length);

                foreach (PropertyInfo property in properties)
                {
                    try
                    {
                        object value = property.GetValue(obj);
                        if (value == null || !mapping.ContainsKey(property.Name))
                        {
                            _logger.LogTrace("Skipping property {PropertyName} - null value or not in mapping", property.Name);
                            continue;
                        }

                        entity[mapping[property.Name]] = value;
                        _logger.LogTrace("Mapped property {PropertyName} to {FieldName} with value {Value}",
                            property.Name, mapping[property.Name], value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error mapping property {PropertyName}", property.Name);
                        throw;
                    }
                }

                _logger.LogDebug("Entity preparation completed. Mapped {Count} properties",
                    entity.Attributes.Count);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing entity of type {EntityType}", entityLogicalName);
                throw;
            }
        }
    }

}
