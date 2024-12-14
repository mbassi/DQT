using DQT.HashCorp.Vault.Models;

namespace DQT.HashCorp.Vault.Services
{
    public interface IVault
    {
        Task<TResponse> CreateAsync<TRequest, TResponse>(string key,TRequest request);
        Task<T> GetValueAsync<T>(string name);
        
    }
}
