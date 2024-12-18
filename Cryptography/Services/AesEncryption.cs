using DQT.Security.Cryptography.Models;
using System.Security.Cryptography;
using System.Text;

namespace DQT.Security.Cryptography.Services
{
    public class AesEncryption : IAesEncryption
    {
        public int KeySize { get; set; } = 256;
        public AesEncrypt Encrypt(string text, string key, string iv)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] ivBytes = Encoding.UTF8.GetBytes(iv);
            return Encrypt(text);
        }
        public AesEncrypt Encrypt(string text, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.Key = key;
                aes.IV = iv;

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
        public AesEncrypt Encrypt(string text)
        {
            
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;


                aes.GenerateKey();
                byte[] key = aes.Key;
                aes.GenerateIV();
                byte[] iv = aes.IV;
                return Encrypt(text, key, iv);
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
            return Decrypt(encryptedData, keyBytes, ivBytes);


        }

        public string Decrypt(string? encryptedText, byte[] key, byte[] iv)
        {
            if (string.IsNullOrWhiteSpace(encryptedText) || key == null  || iv == null)
            {
                throw new ArgumentNullException();
            }
            byte[] encryptedData = Convert.FromBase64String(encryptedText);
            return Decrypt(encryptedData, key, iv);


        }
        public string Decrypt(byte[] encryptedBytes, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                ICryptoTransform decryptor = aes.CreateDecryptor(key, iv);
                using (MemoryStream ms = new MemoryStream(encryptedBytes))
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
        public byte[] GenerateIV()
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;

                aes.GenerateIV();
                byte[] iv = aes.IV;
                return iv;
            }
        }

        public byte[] GenerateKey()
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.GenerateKey();
                byte[] key = aes.Key;
                return key;
            }

        }
    }
}
