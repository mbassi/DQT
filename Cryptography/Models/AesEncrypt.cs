namespace DQT.Security.Cryptography.Models
{
    public class AesEncrypt
    {
        public string EncryptedData { get; private set; }
        public string Key { get; private set; }
        public string IV { get; private set; }
        public AesEncrypt()
        {
            EncryptedData = string.Empty;
            Key = string.Empty;
            IV = string.Empty;
        }
        public AesEncrypt(string encryptedData, string key, string iv)
        {
            EncryptedData = encryptedData;
            Key = key;
            IV = iv;
        }
    }
}
