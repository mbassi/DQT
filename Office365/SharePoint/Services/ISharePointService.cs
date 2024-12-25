

namespace DQT.Office365.SharePoint.Services
{
    public interface ISharePointService : IDisposable
    {

        Task ConnectAsync(string siteUrl, string libraryName, string username, string password, string clientId, string clientSecret, string tenantId);
        Task CreateFolderAsync(string folderName, string parentFolderPath = "");
        Task UploadFileAsync(byte[] fileContent, string fileName, string folderPath = "");


    }
}
