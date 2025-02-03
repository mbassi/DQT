using Microsoft.Xrm.Sdk;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Collections.Concurrent;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Reflection;
using Microsoft.Crm.Sdk.Messages;
using System.ServiceModel;
using Microsoft.Extensions.Logging;
using System.Net;
using DQT.Enums;

namespace DQT.D365
{
    public class Dynamics365Integration : IDynamics365Integration
    {

        private readonly ServiceClient _serviceClient;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<OrganizationRequest> _pendingRequests;
        private readonly int _maxBatchSize;
        private readonly ILogger<Dynamics365Integration> _logger;
        private readonly int _maxRetries;
        private readonly TimeSpan _timeout;
        private bool _disposed;

        public Dynamics365Integration(
    string instanceUrl,
    string clientId,
    string clientSecret,
    string tenantId,
    ILogger<Dynamics365Integration> logger,
    int maxBatchSize = 1000,
    int maxRetries = 3,
    int timeoutMinutes = 20)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("Initializing Dynamics365Integration service");

            try
            {
                // Configure connection string with timeout and retry settings
                string connectionString = $@"
                        AuthType=ClientSecret;
                        Url={instanceUrl};
                        ClientId={clientId};
                        ClientSecret={clientSecret};
                        TenantId={tenantId};
                        RequestTimeout={timeoutMinutes * 60};
                        ConnectTimeout={timeoutMinutes * 60};
                        RetryCount={maxRetries};
                        RetryPauseTime=5";

                _semaphore = new SemaphoreSlim(1, 1);
                _pendingRequests = new ConcurrentQueue<OrganizationRequest>();
                _serviceClient = new ServiceClient(connectionString.Replace("\r\n", "").Replace("\n", ""));
                _maxBatchSize = maxBatchSize;
                _maxRetries = maxRetries;
                _timeout = TimeSpan.FromMinutes(timeoutMinutes);
                ConfigureServicePointManager();
                _logger.LogInformation("Dynamics365Integration service initialized successfully with maxBatchSize: {MaxBatchSize}, maxRetries: {MaxRetries}, timeout: {Timeout} minutes",
                    maxBatchSize, maxRetries, timeoutMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Dynamics365Integration service");
                throw;
            }
        }

        private void ConfigureServicePointManager()
        {
            ServicePointManager.DefaultConnectionLimit = 65000;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.EnableDnsRoundRobin = true;
        }
        public async Task<bool> TestConnectionAsync()
        {
            _logger.LogInformation("Testing connection");
            return await ExecuteWithRetryAsync(async () =>
            {
                var whoAmIRequest = new WhoAmIRequest();
                var response = (WhoAmIResponse)await _serviceClient.ExecuteAsync(whoAmIRequest);
                _logger.LogInformation("Connected successfully. User ID: {UserId}", response.UserId);
                return true;
            }, "TestConnection");
        }

        public async Task<Guid> CreateRecordAsync(Entity attributes)
        {
            ArgumentNullException.ThrowIfNull(attributes);
            _logger.LogInformation("Creating {EntityType} record", attributes.LogicalName);

            return await ExecuteWithRetryAsync(async () =>
            {
                var result = await _serviceClient.CreateAsync(attributes);
                _logger.LogInformation("Created record with ID: {RecordId}", result);
                return result;
            }, "CreateRecord");
        }

        public async Task<Entity> RetrieveRecordAsync(string entityLogicalName, Guid id, string[] columnSet = null)
        {
            _logger.LogInformation("Retrieving {EntityType} record {RecordId}", entityLogicalName, id);

            return await ExecuteWithRetryAsync(async () =>
            {
                var result = await _serviceClient.RetrieveAsync(entityLogicalName, id, new ColumnSet(columnSet));
                _logger.LogInformation("Retrieved record successfully");
                return result;
            }, "RetrieveRecord");
        }

        public async Task UpdateRecordAsync(Entity entityToUpdate)
        {
            ArgumentNullException.ThrowIfNull(entityToUpdate);
            _logger.LogInformation("Updating {EntityType} record {RecordId}", entityToUpdate.LogicalName, entityToUpdate.Id);

            await ExecuteWithRetryAsync(async () =>
            {
                await _serviceClient.UpdateAsync(entityToUpdate);
                _logger.LogInformation("Updated record successfully");
                return true;
            }, "UpdateRecord");
        }

        public async Task DeleteRecordAsync(string entityLogicalName, Guid id)
        {
            _logger.LogInformation("Deleting {EntityType} record {RecordId}", entityLogicalName, id);

            await ExecuteWithRetryAsync(async () =>
            {
                await _serviceClient.DeleteAsync(entityLogicalName, id);
                _logger.LogInformation("Deleted record successfully");
                return true;
            }, "DeleteRecord");
        }


        public Entity PrepareEntity<TParameter>(
            string entityLogicalName,
            Guid id,
            TParameter obj,
            Dictionary<string, string> mapping,
            D365Action action = D365Action.Create)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(mapping);

            _logger.LogInformation("Preparing entity of type {EntityType} for {Action}", entityLogicalName, action);

            Entity entity = new Entity(entityLogicalName);
            if (id != Guid.Empty)
            {
                entity.Id = id;
            }

            if (action == D365Action.Delete)
            {
                _logger.LogInformation("Delete action specified, returning basic entity reference");
                return entity;
            }

            try
            {
                Type type = obj.GetType();
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in properties)
                {
                    try
                    {
                        // Skip if property is not in mapping
                        if (!mapping.ContainsKey(property.Name))
                        {
                            continue;
                        }

                        object value = property.GetValue(obj);

                        if (value != null)
                        {

                            bool skip = value is EntityReference entityReference && entityReference.Id == Guid.Empty;
                            if (skip) continue;

                            entity[mapping[property.Name]] = value;
                            _logger.LogTrace("Mapped {PropertyName} to {FieldName} with value {Value}",
                                property.Name, mapping[property.Name], value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error mapping property {PropertyName}", property.Name);
                        throw;
                    }
                }

                _logger.LogInformation("Entity preparation completed. Mapped {Count} properties",
                    entity.Attributes.Count);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing entity of type {EntityType}", entityLogicalName);
                throw;
            }
        }
        public async Task<EntityCollection> RetrieveMultipleAsync(string query)
        {
            ArgumentNullException.ThrowIfNull(query);
            _logger.LogInformation("Retrieving multiple records");

            return await ExecuteWithRetryAsync(async () =>
            {
                var result = await _serviceClient.RetrieveMultipleAsync(new FetchExpression(query));
                _logger.LogInformation("Retrieved {Count} records", result.Entities.Count);
                return result;
            }, "RetrieveMultiple");
        }

        public async Task AddToQueue<TParameter>(
            string entityLogicalName,
            Guid id,
            TParameter obj,
            Dictionary<string, string> mapping,
            D365Action action = D365Action.Create)
        {
            ThrowIfDisposed();
            _logger.LogInformation("Adding {Action} operation to queue", action);

            try
            {
                Entity entity = PrepareEntity(entityLogicalName, id, obj, mapping, action);
                await _semaphore.WaitAsync();

                try
                {
                    OrganizationRequest request = action switch
                    {
                        D365Action.Create => new CreateRequest { Target = entity },
                        D365Action.Update => new UpdateRequest { Target = entity },
                        D365Action.Delete => new DeleteRequest { Target = entity.ToEntityReference() },
                        _ => throw new ArgumentException($"Unsupported action: {action}")
                    };

                    _pendingRequests.Enqueue(request);
                    _logger.LogInformation("Added request to queue. Queue size: {QueueSize}", _pendingRequests.Count);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to queue");
                throw;
            }
        }

        public async Task<BulkOperationResult> CommitChangesAsync()
        {
            ThrowIfDisposed();
            var startTime = DateTime.Now;
            _logger.LogInformation("Starting bulk operation with {Count} requests", _pendingRequests.Count);
            await _semaphore.WaitAsync();
            try
            {
                var result = new BulkOperationResult();
                var totalRequests = _pendingRequests.Count;
                var batches = new List<List<OrganizationRequest>>();
                var optimizedBatchSize = Math.Min(_maxBatchSize, 200);
                while (_pendingRequests.Count > 0)
                {
                    var batch = await CreateNextBatchAsync(optimizedBatchSize);
                    if (batch.Any())
                    {
                        batches.Add(batch);
                    }
                }

                var parallelResult = new ConcurrentBag<(List<(OrganizationRequest, Exception)> failures, List<OrganizationRequest> successes, int count)>();

                await Parallel.ForEachAsync(batches, async (batch, cancellationToken) =>
                {
                    var failures = new List<(OrganizationRequest, Exception)>();
                    var successes = new List<OrganizationRequest>();

                    try
                    {
                        var batchResponse = await ExecuteBatchWithTimeoutAsync(batch);
                        ProcessBatchResponse(batchResponse, batch, failures, successes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch");
                    }

                    parallelResult.Add((failures, successes, batch.Count));
                });

                result.Failures = parallelResult.SelectMany(r => r.failures).ToList().AsReadOnly();
                result.Successes = parallelResult.SelectMany(r => r.successes).ToList().AsReadOnly();
                var processedRequests = parallelResult.Sum(r => r.count);

                ReportProgress(startTime, processedRequests, totalRequests, result.SuccessCount, result.Failures.Count);

                LogOperationSummary(startTime, result);
                return result;
            }
            finally
            {
                _pendingRequests.Clear();
                _semaphore.Release();
            }
        }
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
        {
            int attempts = 0;
            while (true)
            {
                try
                {
                    using var cts = new CancellationTokenSource(_timeout);
                    return await operation().WaitAsync(cts.Token);
                }
                catch (Exception ex) when (attempts < _maxRetries && IsTransientException(ex))
                {
                    attempts++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts)); // Exponential backoff
                    _logger.LogWarning("Attempt {Attempt} of {MaxRetries} failed for {Operation}. Retrying in {Delay} seconds. Error: {Error}",
                        attempts, _maxRetries, operationName, delay.TotalSeconds, ex.Message);
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Operation {Operation} failed after {Attempts} attempts", operationName, attempts + 1);
                    throw;
                }
            }
        }

        private bool IsTransientException(Exception ex)
        {
            return ex is TimeoutException
                || ex is FaultException<OrganizationServiceFault> fault && IsTransientFault(fault)
                || ex is TaskCanceledException
                || ex is OperationCanceledException;
        }

        private bool IsTransientFault(FaultException<OrganizationServiceFault> fault)
        {
            // Common Dynamics 365 transient error codes
            int[] transientErrorCodes = new[]
            {
                -2147204784, // Deadlock
                -2147204720, // Connection issues
                -2147203082, // SQL timeout
                -2147204773, // Throttling
                -2147204778  // Network connectivity
            };
            return transientErrorCodes.Contains(fault.Detail.ErrorCode);
        }

        private async Task<List<OrganizationRequest>> CreateNextBatchAsync(int batchSize)
        {
            _logger.LogInformation("Creating next batch. Current queue size: {QueueSize}", _pendingRequests.Count);
            var batch = new List<OrganizationRequest>();

            while (batch.Count < batchSize && _pendingRequests.TryDequeue(out var request))
            {
                batch.Add(request);
            }

            _logger.LogInformation("Created batch of {Count} requests", batch.Count);
            return batch;
        }

        private async Task<ExecuteMultipleResponse> ExecuteBatchWithTimeoutAsync(List<OrganizationRequest> batch)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var executeMultipleRequest = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = true,
                        ReturnResponses = true
                    },
                };
                var requests = new OrganizationRequestCollection();

                foreach (var request in batch)
                {
                    requests.Add(request);
                }
                executeMultipleRequest.Requests = requests;
                var response = (ExecuteMultipleResponse)await _serviceClient.ExecuteAsync(executeMultipleRequest);

                _logger.LogInformation("Batch execution completed. Response count: {Count}", response.Responses.Count);
                return response;
            }, "BatchExecution");
        }

        private void ProcessBatchResponse(
            ExecuteMultipleResponse response,
            List<OrganizationRequest> batch,
            List<(OrganizationRequest, Exception)> failures,
            List<OrganizationRequest> successes)
        {
            for (int i = 0; i < response.Responses.Count; i++)
            {
                var responseItem = response.Responses[i];
                if (responseItem.Fault != null)
                {
                    _logger.LogWarning("Fault in batch item {Index}. Error: {Error}", i, responseItem.Fault.Message);
                    failures.Add((batch[i], new FaultException<OrganizationServiceFault>(responseItem.Fault)));

                }
                else
                {
                    successes.Add(batch[i]);
                }
            }
        }

        private void ReportProgress(
            DateTime startTime,
            int processedRequests,
            int totalRequests,
            int successCount,
            int failureCount
            )
        {
            var progressPercentage = (int)((float)processedRequests / totalRequests * 100);
            var elapsedTime = DateTime.Now - startTime;

            if (processedRequests > 0)
            {
                var estimatedTimeRemaining = TimeSpan.FromTicks(
                    (long)((elapsedTime.Ticks * ((float)(totalRequests - processedRequests) / processedRequests))));
            }
        }

        private void LogOperationSummary(DateTime startTime, BulkOperationResult result)
        {
            var totalTime = DateTime.Now - startTime;
            _logger.LogInformation(
                "Bulk operation completed in {TotalTime:hh\\:mm\\:ss} | Success: {SuccessCount} | Failures: {FailureCount}",
                totalTime,
                result.SuccessCount,
                result.FailureCount);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Dynamics365Integration));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.LogInformation("Disposing Dynamics365Integration service");
                _serviceClient?.Dispose();
                _semaphore?.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}