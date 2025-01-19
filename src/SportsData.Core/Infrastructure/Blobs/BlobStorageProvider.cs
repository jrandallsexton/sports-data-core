using Azure.Storage.Blobs;

using System.IO;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Blobs
{
    public interface IProvideBlobStorage
    {
        Task<string> UploadImageAsync(Stream stream, string containerName, string filename);
    }

    public class BlobStorageProvider : IProvideBlobStorage
    {
        public async Task<string> UploadImageAsync(Stream stream, string containerName, string filename)
        {
            // TODO: Bring this into Azure AppConfig (CommonConfig?)
            const string connectionString = "FROM_AZ_APP_CONFIG";

            var tmp = new BlobContainerClient(connectionString, containerName.ToLower());

            await tmp.CreateIfNotExistsAsync();

            var blobClient = new BlobClient(connectionString,
                containerName.ToLower(), filename);
            
            // TODO: Perhaps bring that overwrite parameter into the method params?
            await blobClient.UploadAsync(stream, true);

            // TODO: Return the url where it is located
            return $"https://sportsdatastoragedev.blob.core.windows.net/{containerName.ToLower()}/{filename}";
        }
    }
}
