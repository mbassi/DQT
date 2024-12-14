using DQT.Security.Cryptography.Models;
using System.Security.Cryptography;

namespace DQT.Security.Cryptography.Services
{
    public class AesEncryption : IAesEncryption
    {
        public AesEncrypt Encrypt(string text)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(text);
                        }
                    }

                    byte[] encryptedData = ms.ToArray();
                    return new AesEncrypt(Convert.ToBase64String(encryptedData), Convert.ToBase64String(aes.Key), Convert.ToBase64String(aes.IV));
                }
            }
        }
        public string Decrypt(string? encryptedText, string? key, string? iv)
        {
            if (string.IsNullOrWhiteSpace(encryptedText) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(iv))
            {
                throw new ArgumentNullException();
            }
            byte[] encryptedData = Convert.FromBase64String(encryptedText);
            byte[] keyBytes = Convert.FromBase64String(key);
            byte[] ivBytes = Convert.FromBase64String(iv);

            using (Aes aes = Aes.Create())
            {
                ICryptoTransform decryptor = aes.CreateDecryptor(keyBytes, ivBytes);
                using (MemoryStream ms = new MemoryStream(encryptedData))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
