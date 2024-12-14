using DQT.Security.Cryptography.Models;

namespace DQT.Security.Cryptography.Services
{
    public interface IAesEncryption
    {
        AesEncrypt Encrypt(string text);

        string Decrypt(string encryptedText, string key, string iv);
    }
}
