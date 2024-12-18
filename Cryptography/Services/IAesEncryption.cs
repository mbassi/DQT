using DQT.Security.Cryptography.Models;

namespace DQT.Security.Cryptography.Services
{
    public interface IAesEncryption
    {
        byte[] GenerateIV();
        byte[] GenerateKey();
        AesEncrypt Encrypt(string text);
        AesEncrypt Encrypt(string text, string key, string iv);
        AesEncrypt Encrypt(string text, byte[] key, byte[] iv);
        string Decrypt(string encryptedText, string key, string iv);
        string Decrypt(string encryptedText, byte[] key, byte[] iv);

        string Decrypt(byte[] encryptedBytes, byte[] key, byte[] iv);
    }
}
