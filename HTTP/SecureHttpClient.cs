using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DQT.HTTP
{
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net;

public class SecureHttpClient:ISecureHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SecureHttpClient> _logger;
        private Dictionary<string,string> Headers = new Dictionary<string,string>();
        public bool IsSuccessfull { get; private set; } = false;
        public bool IsTimedOut { get; private set; } = false;
        public HttpStatusCode StatusCode{ get; private set; }
        // Configuration options for HTTP client
        private static readonly HttpClientHandler _clientHandler = new HttpClientHandler
        {
            // Enforce SSL/TLS certificate validation
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
            {
                // In production, implement more robust certificate validation
                // This is a placeholder - replace with proper validation logic
                return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
            },

            // Prevent potential SSRF attacks by limiting redirects
            MaxAutomaticRedirections = 3,
            

            // Use system proxy settings securely
            UseProxy = true,
            Proxy = WebRequest.GetSystemWebProxy()
        };

        public SecureHttpClient(ILogger<SecureHttpClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Create HttpClient with predefined handler
            _httpClient = new HttpClient(_clientHandler)
            {
                // Set a reasonable timeout to prevent hung requests
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Configure default headers
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }
        public void AddHeaders(string key, string value)
        {
            if (Headers == null ) Headers = new Dictionary<string,string>();
            if (Headers.ContainsKey(key)) Headers[key] = value;
            else Headers.Add(key, value);
            
        }
        private void AddHttpHeaders() 
        {
            if (Headers == null) return;
            foreach (var heder in Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(heder.Key, heder.Value);
            }
        }
        /// <summary>
        /// Performs a secure GET request with optional bearer token authentication
        /// </summary>
        /// <typeparam name="T">Type to deserialize the response to</typeparam>
        /// <param name="url">Target URL for the request</param>
        /// <param name="bearerToken">Optional authentication token</param>
        /// <returns>Deserialized response object</returns>
        public async Task<T> GetAsync<T>(string url, string bearerToken = null)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            try
            {
                // Add optional bearer token
                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrWhiteSpace(bearerToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", bearerToken);
                }
                AddHttpHeaders();
                // Perform the HTTP GET request
                using var response = await _httpClient.GetAsync(url);

                // Ensure successful status code
                StatusCode = response.StatusCode;
                IsSuccessfull = response.IsSuccessStatusCode;
                var responseBody = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
            }
            catch (HttpRequestException ex)
            {
                
                // Log the specific HTTP request error
                _logger.LogError(ex, "HTTP request failed for URL: {Url}", url);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                // Handle timeout scenarios
                _logger.LogError(ex, "Request to {Url} timed out", url);
                IsTimedOut = true;
            }
            catch (Exception ex)
            {
                // Catch and log any unexpected errors
                _logger.LogError(ex, "Unexpected error during HTTP request to {Url}", url);
                throw;
            }
            finally
            {
                // Reset authorization header to prevent leaking across requests
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
            return default;
        }

        /// <summary>
        /// Performs a secure POST request with optional bearer token authentication
        /// </summary>
        /// <typeparam name="TRequest">Type of request body</typeparam>
        /// <typeparam name="TResponse">Type to deserialize the response to</typeparam>
        /// <param name="url">Target URL for the request</param>
        /// <param name="requestBody">Object to serialize as request body</param>
        /// <param name="bearerToken">Optional authentication token</param>
        /// <returns>Deserialized response object</returns>
        public async Task<TResponse> PostAsync<TRequest, TResponse>(
            string url,
            TRequest requestBody,
            string bearerToken = null)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrWhiteSpace(bearerToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", bearerToken);
                }
                AddHttpHeaders();
                // Serialize request body
                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var httpContent = new StringContent(
                    jsonRequest,
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                
                // Perform the HTTP POST request
                using var response = await _httpClient.PostAsync(url, httpContent);

                // Ensure successful status code
                StatusCode = response.StatusCode;
                IsSuccessfull = response.IsSuccessStatusCode;
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("BODY: " + responseBody);
                if (!string.IsNullOrEmpty(responseBody)) {
                    return JsonSerializer.Deserialize<TResponse>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }



            }
            catch (HttpRequestException ex)
            {
                // Log the specific HTTP request error
                _logger.LogError(ex, $"HTTP POST request failed for URL: {url} causa {ex.Message}" );
                throw;
            }
            catch (TaskCanceledException ex)
            {
                // Handle timeout scenarios
                _logger.LogError(ex, $"POST request to {url} timed out", url);
                
            }
            catch (Exception ex)
            {
                // Catch and log any unexpected errors
                _logger.LogError(ex, $"Unexpected error during HTTP POST request to {url} causa: {ex.Message}");
                throw;
            }
            finally
            {
                // Reset authorization header to prevent leaking across requests
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
            return default;
        }
    }

    
    
    
}
