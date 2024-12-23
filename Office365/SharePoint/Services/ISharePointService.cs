using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Office365.SharePoint.Services
{
    public interface ISharePointService : IDisposable
    {
       
        Task ConnectAsync();
        Task CreateDirectoryAsync(string folderName, string parentFolderPath = "");
        Task UploadFileAsync(byte[] fileContent, string fileName, string destinationFolder);
        
    }
}
