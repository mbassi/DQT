using DQT.Azure.BlobStorage.Models;
namespace DQT.Azure.BlobStorage.Services
{
    public interface IBlobStorageService
    {
        Task<IEnumerable<string>> ListBlobsAsync(string prefix);
        Task<byte[]> GetBlobBinaryContentAsync(string blobName);
        Task<string> GetBlobTextContentAsync(string blobName);
        Task DeleteBlobAsync(string blobName);
        Task<Dictionary<string, byte[]>> ExtractZipBlobAsync(string zipBlobName);

        Task<List<string>> ExtractZipBlobAsync(
            string rootFolder,
            string zipBlobName,
            byte[] zipContent,
            IDictionary<string, string> metadata = null,
            IProgress<long> progress = null

        );
        Task<BlobUploadResult> SaveFileAsync(
            byte[] fileContent,
            string blobName,
            string contentType = null,
            IDictionary<string, string> metadata = null,
            IProgress<long> progress = null
        );
    }
}
