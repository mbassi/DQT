using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.SFTP
{
    public interface ISecureSftpClient
    {
        public void Connect();
        public void Disconnect();
        public void Dispose();
        public byte[] GetBinaryFileContent(string remoteFilePath);
        public string GetTextFileContent(string remoteFilePath);
        public List<string> ListFiles(string remotePath);
        public List<string> ListFolders(string remotePath);
        public void RenameFile(string oldPath, string newPath);
    }
}
