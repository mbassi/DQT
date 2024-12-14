using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.HTTP
{
    public interface ISecureHttpClient
    {
        public void AddHeaders(string key, string value);
        public Task<T> GetAsync<T>(string url, string bearerToken = null);
        public Task<TResponse> PostAsync<TRequest, TResponse>(
            string url,
            TRequest requestBody,
            string bearerToken = null);
    }
}
