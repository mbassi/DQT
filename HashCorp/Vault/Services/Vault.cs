using Microsoft.Extensions.Options;
using DQT.HTTP;
using Microsoft.Extensions.Logging;
using DQT.HashCorp.Vault.Models;
using DQT.Security.Cryptography.Services;
using DQT.Enums;
using System.Reflection;
using System;
using System.Net;

namespace DQT.HashCorp.Vault.Services
{
    public class Vault : IVault
    {
        private VaultSettings _setting;
        private readonly SecureHttpClient _httpClient;
        private readonly ILogger<SecureHttpClient> _loggerHTTP;
        private readonly ILogger<Vault> _logger;
        public string AuthToken { get; private set; } = string.Empty;
        private readonly IAesEncryption _aesEncryption;
        public bool IsSucessfull { get; set; } = false;
        private HttpStatusCode StatusCode { get; set; }
        public int MaxAttempts { get; set; } = 3;

        public int WaitingTimeInSeconds { get; set; } = 3000;
        private int Attempts { get; set; } = 0;

        public Vault(VaultSettings settings, IAesEncryption aesEncryption, ILogger<SecureHttpClient> loggerHTTP, ILogger<Vault> logger)
        {
            _setting = settings;
            _httpClient = new SecureHttpClient(loggerHTTP);
            _aesEncryption = aesEncryption;
            _loggerHTTP = loggerHTTP;
            _logger = logger;
            AuthToken = string.Empty;
        }
        
        public Vault(IOptions<VaultSettings> settings, IAesEncryption aesEncryption, ILogger<SecureHttpClient> loggerHTTP, ILogger<Vault> logger)
        {
            _setting = settings.Value;
            _httpClient = new SecureHttpClient(loggerHTTP);
            _aesEncryption = aesEncryption;
            _loggerHTTP = loggerHTTP;
            _logger = logger;
            AuthToken = string.Empty;
        }
        public async Task<TResponse> CreateAsync<TRequest,TResponse>(string keyName, TRequest request)
        {

            try
            {

                do
                {
                    var responseLogin = true;
                    if (string.IsNullOrEmpty(AuthToken))
                    {
                        responseLogin = await LoginAsync();
                    }
                    if (responseLogin)
                    {
                        var response = await PostDataAsync<TRequest, TResponse>("v1/secret",keyName, request);
                        if (IsSucessfull)
                        {
                            return response;
                        }
                        else if (_httpClient.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            AuthToken = String.Empty;

                        }
                        if (!IsSucessfull)
                        {
                            _logger.LogError($"Nao foi possivel criar a chave {keyName}.");
                        }
                    }
                    else
                    {
                        Thread.Sleep(3000);
                        Attempts++;
                    }
                } while (Attempts < MaxAttempts);


            }
            catch (Exception ex)
            {
                IsSucessfull = false;
                _logger.LogError(ex,$"Erro inesperado ao recuperar o valor da chave {keyName}.Tipo:{nameof(ex)} causa:" + ex.Message);
            }
            return default;

        }
        private string GetUrl(string path, string keyName)
        {
            
            return _setting.Host + ":" + _setting.Port + "/" + path + (!string.IsNullOrEmpty(keyName) ? "/" + keyName : "");
        }
        
        private async Task<TResponse>PostDataAsync<TRequest,TResponse>(string path, string keyName, TRequest request, bool addAuthHeader = false)
        {
            
            try
            {
                
                if (addAuthHeader) _httpClient.AddHeaders("X-Vault-Token", AuthToken);
                string url = GetUrl(path,keyName);
                
                var response = await _httpClient.PostAsync<TRequest, TResponse>(url, request);
                IsSucessfull = _httpClient.IsSuccessfull;
                StatusCode = _httpClient.StatusCode;
                if (!IsSucessfull)
                {
                    _logger.LogError($"Erro ao fazer requisicao url: {url} Status Code: {StatusCode}");
                }
                else
                {
                    _logger.LogInformation($"Requisicao HTTP bem sucedida url: {url}");
                }
                return response;
            }
            catch (Exception ex)
            {
                
                _logger.LogError(ex,$"Erro inesperado ao fazer requisicao.Tipo:{nameof(ex)} causa:" + ex.Message);
                IsSucessfull = false;
            }
            return default;
        }
        private async Task<TResponse> GetDataAsync<TResponse>(string path, string keyName, bool addAuthHeader = false)
        {

            try
            {

                string url = GetUrl(path,keyName);
                
                var response = await _httpClient.GetAsync<TResponse>(url);
                IsSucessfull = _httpClient.IsSuccessfull;
                StatusCode = _httpClient.StatusCode;
                if (!IsSucessfull)
                {
                    _logger.LogError($"Erro ao fazer requisicao url: {url} Status Code: {StatusCode}");
                }
                else
                {
                    _logger.LogInformation($"Requisicao HTTP bem sucedida url: {url}");
                }
                return response;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex,$"Erro inesperado ao fazer requisicao.Tipo:{nameof(ex)} causa:" + ex.Message);
                IsSucessfull = false;
            }
            return default;
        }
        private async Task<bool> LoginAsync()
        {
            bool loginReturn = false; 
            try
            {
                string userName = _aesEncryption.Decrypt(_setting.Data1, _setting.Data3, _setting.Data4);
                string password = _aesEncryption.Decrypt(_setting.Data2, _setting.Data5, _setting.Data6);
                LoginRequest model = new LoginRequest
                {
                    password = password,
                };

                var response = await PostDataAsync<LoginRequest, LoginResponse>("v1/auth/userpass/login/" + userName,string.Empty, model);
                if (response == null)
                {
                    _logger.LogWarning("Erro no Login nao retornou dados");
                    return false;
                }
                var errorMessage = response.GetErrors();
                if (_httpClient.IsSuccessfull == true)
                {
                    AuthToken = response.auth.client_token;
                    _httpClient.AddHeaders("X-Vault-Token", AuthToken);
                    if (!string.IsNullOrEmpty(errorMessage)&& !string.IsNullOrWhiteSpace(AuthToken))
                    {
                        _logger.LogWarning("Erro no Login mas retornou o token, podemos proceguir causa: " + errorMessage);
                    }
                    else if (string.IsNullOrWhiteSpace(errorMessage) && string.IsNullOrWhiteSpace(AuthToken))
                    {
                        _logger.LogError("Erro desconhecido no Login não retornou o token, não podemos proceguir");
                    }
                    else if (!string.IsNullOrWhiteSpace(errorMessage) && string.IsNullOrWhiteSpace(AuthToken))
                    {
                        _logger.LogError("Erro inexperadono Login não retornou o token, não podemos proceguir causa: " + errorMessage);

                    }
                    else if (string.IsNullOrWhiteSpace(errorMessage) && !string.IsNullOrWhiteSpace(AuthToken))
                    {
                        _logger.LogInformation($"Usuario: {userName} logou com sucesso");
                        loginReturn = true;
                    }
                }
                else
                {
                    _logger.LogError($"Login Failed: Status Code {_httpClient.StatusCode} causa: {errorMessage}" );
                }

            }
            catch (Exception ex)
            {
                AuthToken = string.Empty;
                loginReturn = false;
                _logger.LogError(ex,$"Erro inesperado ao fazer o Login.Tipo:{nameof(ex)} causa:" + ex.Message);
            }

            return loginReturn;
        }

        
        public async Task<TResponse> GetValueAsync<TResponse>(string keyName)
        {
            try
            {
                
                do
                {
                    var responseLogin = true;
                    if (string.IsNullOrEmpty(AuthToken))
                    {
                        responseLogin = await LoginAsync();
                    }
                    if (responseLogin)
                    {
                        var response = await GetDataAsync<TResponse>("v1/secret", keyName, true);
                        if (IsSucessfull)
                        {
                            return response;
                        }
                        else if (_httpClient.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            AuthToken = String.Empty;
                                
                        }
                        if (!IsSucessfull)
                        {
                            _logger.LogError($"Nao foi possivel recuperar o dado da chave {keyName}.");
                            Thread.Sleep(3000);
                            Attempts++;
                        }
                    }
                    else
                    {
                        Thread.Sleep(3000);
                        Attempts++;
                    }
                } while (Attempts < MaxAttempts);
                

            }
            catch (Exception ex) 
            {
                IsSucessfull = false;
                _logger.LogError(ex, $"Erro inesperado ao recuperar o valor da chave {keyName}.Tipo:{nameof(ex) } causa:" + ex.Message);
            }   
            return default;
        }
    }
}
