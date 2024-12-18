using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.HTTP
{
    public interface ISecureHttpClient
    {
        public string GetToken(string issuer, string audience, int expirationInMinutes, string secretKey);
        public void AddHeaders(string key, string value);
        public Task<T> GetAsync<T>(string url, string bearerToken = null);
        public Task<TResponse> PostAsync<TRequest, TResponse>(
            string url,
            TRequest requestBody,
            string bearerToken = null);
    }
}
