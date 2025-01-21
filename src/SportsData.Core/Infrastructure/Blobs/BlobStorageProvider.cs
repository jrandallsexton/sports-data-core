using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Options;

using SportsData.Core.Config;

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
        private readonly IOptions<CommonConfig> _config;

        public BlobStorageProvider(IOptions<CommonConfig> config)
        {
            _config = config;
        }

        public async Task<string> UploadImageAsync(Stream stream, string containerName, string filename)
        {
            containerName = $"{_config.Value.AzureBlobStorageContainerPrefix}-{containerName.ToLower()}";

            var tmp = new BlobContainerClient(_config.Value.AzureBlobStorageConnectionString, containerName);

            await tmp.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            
            // TODO: Use BlobContainerClient only?
            var blobClient = new BlobClient(
                _config.Value.AzureBlobStorageConnectionString,
                containerName, filename);
            
            // TODO: Perhaps bring that overwrite parameter into the method params?
            await blobClient.UploadAsync(stream, true);

            // TODO: Return the url where it is located
            return $"{_config.Value.AzureBlobStorageUrl}/{containerName}/{filename}";
        }
    }
}
