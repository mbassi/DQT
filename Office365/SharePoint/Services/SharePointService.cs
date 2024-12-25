using Microsoft.SharePoint.Client;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Xrm.Sdk;
using Microsoft.Extensions.Logging;
using System.Net;

namespace DQT.Office365.SharePoint.Services
{
    public class SharePointService : ISharePointService
    {
        private readonly ILogger<SharePointService> _logger;
        private readonly HttpClient _httpClient;
        private ClientContext _context;
        private bool _disposed;
        private string _accessToken;
        private string _graphAccessToken;
        private string _siteUrl;
        private string _libraryName;
        private string _clientId;
        private string _clientSecret;
        private string _tenantId;
        private string _username;
        private string _password;
        private string _siteId;
        private string _libraryId;

        public SharePointService(ILogger<SharePointService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();
        }

        public async Task ConnectAsync(string siteUrl, string libraryName, string username, string password, string clientId, string clientSecret, string tenantId)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SharePointService));

            try
            {
                // Store credentials
                _siteUrl = siteUrl;
                _libraryName = libraryName;
                _clientId = clientId;
                _clientSecret = clientSecret;
                _tenantId = tenantId;
                _username = username;
                _password = password;

                // Get tokens
                var uri = new Uri(siteUrl);
                string resource = $"{uri.Scheme}://{uri.Host}";
                _accessToken = await GetAccessTokenAsync(resource);
                _graphAccessToken = await GetGraphTokenAsync();

                // Initialize context
                _context = new ClientContext(siteUrl);
                _context.ExecutingWebRequest += (sender, e) =>
                {
                    e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + _accessToken;
                };

                // Initialize IDs only if needed
                if (string.IsNullOrEmpty(_siteId))
                {
                    await InitializeSiteIdAsync();
                }

                if (string.IsNullOrEmpty(_libraryId))
                {
                    await InitializeLibraryIdAsync(_libraryName);
                }

                _logger.LogInformation("Successfully connected and initialized site {SiteUrl} with library {LibraryName}",
                    siteUrl, libraryName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SharePoint site {SiteUrl} with library {LibraryName}",
                    siteUrl, libraryName);
                throw;
            }
        }

        private async Task<string> GetAccessTokenAsync(string resource)
        {
            var tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/token";

            var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("username", _username),
            new KeyValuePair<string, string>("password", _password),
            new KeyValuePair<string, string>("resource", resource)
        });

            var response = await _httpClient.PostAsync(tokenEndpoint, content);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Token request failed: {Response}", result);
                throw new Exception($"Failed to get access token: {result}");
            }

            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(result);
            return tokenResponse.GetProperty("access_token").GetString();
        }

        private async Task<string> GetGraphTokenAsync()
        {
            var tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";

            var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("username", _username),
            new KeyValuePair<string, string>("password", _password),
            new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default")
        });

            var response = await _httpClient.PostAsync(tokenEndpoint, content);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Graph token request failed: {Response}", result);
                throw new Exception($"Failed to get Graph token: {result}");
            }

            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(result);
            return tokenResponse.GetProperty("access_token").GetString();
        }

        private async Task InitializeSiteIdAsync()
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);

            var siteUrl = $"https://graph.microsoft.com/v1.0/sites/{new Uri(_siteUrl).Host}:/sites/{_siteUrl.Split("/sites/")[1]}";
            var response = await httpClient.GetAsync(siteUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get site ID. Status: {response.StatusCode}, Response: {content}");
            }

            var siteData = JsonSerializer.Deserialize<JsonElement>(content);
            _siteId = siteData.GetProperty("id").GetString();
            _logger.LogInformation("Successfully initialized site ID: {SiteId}", _siteId);
        }

        private async Task InitializeLibraryIdAsync(string libraryName)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);

            var listsUrl = $"https://graph.microsoft.com/v1.0/sites/{_siteId}/lists";
            var response = await httpClient.GetAsync(listsUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get lists. Status: {response.StatusCode}, Response: {content}");
            }

            var listsData = JsonSerializer.Deserialize<JsonElement>(content);
            var lists = listsData.GetProperty("value").EnumerateArray();

            foreach (var list in lists)
            {
                var displayName = list.GetProperty("displayName").GetString();
                if (displayName.Equals(libraryName, StringComparison.OrdinalIgnoreCase))
                {
                    _libraryId = list.GetProperty("id").GetString();
                    _logger.LogInformation("Successfully initialized library ID for '{LibraryName}': {LibraryId}",
                        libraryName, _libraryId);
                    return;
                }
            }

            throw new Exception($"Library '{libraryName}' not found");
        }

        public async Task CreateFolderAsync(string folderName, string parentFolderPath = "")
        {
            try
            {
                await EnsureValidConnectionAsync();

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);

                var folderPath = !string.IsNullOrEmpty(parentFolderPath)
                    ? $"{parentFolderPath}/{folderName}".TrimStart('/')
                    : folderName;

                var createFolderUrl = $"https://graph.microsoft.com/v1.0/sites/{_siteId}/lists/{_libraryId}/drive/root:/{folderPath}";

                var folderContent = new Dictionary<string, object>
                {
                    ["name"] = folderName,
                    ["folder"] = new { },
                    ["microsoft.graph.conflictBehavior"] = "replace"
                };

                var jsonContent = JsonSerializer.Serialize(folderContent);
                var requestContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var createResponse = await httpClient.PutAsync(createFolderUrl, requestContent);
                var createResult = await createResponse.Content.ReadAsStringAsync();

                if (!createResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to create folder. Status: {createResponse.StatusCode}, Response: {createResult}");
                }

                _logger.LogInformation("Successfully created folder '{FolderName}'", folderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating folder '{FolderName}'", folderName);
                throw;
            }
        }

        public async Task UploadFileAsync(byte[] fileContent, string fileName, string folderPath = "")
        {
            try
            {
                await EnsureValidConnectionAsync();

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _graphAccessToken);

                var filePath = !string.IsNullOrEmpty(folderPath)
                    ? $"{folderPath}/{fileName}".TrimStart('/')
                    : fileName;

                var uploadUrl = $"https://graph.microsoft.com/v1.0/sites/{_siteId}/lists/{_libraryId}/drive/root:/{filePath}:/content";

                using (var content = new ByteArrayContent(fileContent))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    var uploadResponse = await httpClient.PutAsync(uploadUrl, content);
                    var uploadResult = await uploadResponse.Content.ReadAsStringAsync();

                    if (!uploadResponse.IsSuccessStatusCode)
                    {
                        throw new Exception($"Failed to upload file. Status: {uploadResponse.StatusCode}, Response: {uploadResult}");
                    }
                }

                _logger.LogInformation("Successfully uploaded file '{FileName}' to path '{FolderPath}'",
                    fileName, folderPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file '{FileName}'", fileName);
                throw;
            }
        }

        private async Task EnsureValidConnectionAsync()
        {
            if (_context == null || string.IsNullOrEmpty(_siteId) || string.IsNullOrEmpty(_libraryId))
            {
                await ConnectAsync(_siteUrl, _libraryName, _username, _password, _clientId, _clientSecret, _tenantId);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _context?.Dispose();
                _httpClient?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    } 
}
