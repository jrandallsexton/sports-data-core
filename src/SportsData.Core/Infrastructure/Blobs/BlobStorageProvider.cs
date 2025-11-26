using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SportsData.Core.Config;

using System;
using System.IO;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Blobs
{
    public interface IProvideBlobStorage
    {
        Task<Uri> UploadImageAsync(Stream stream, string containerName, string filename);
        Task<string> GetFileContentsAsync(string containerName, string filename);
    }

    public class BlobStorageProvider : IProvideBlobStorage
    {
        private readonly IOptions<CommonConfig> _config;
        private readonly ILogger<BlobStorageProvider> _logger;

        public BlobStorageProvider(IOptions<CommonConfig> config, ILogger<BlobStorageProvider> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<Uri> UploadImageAsync(Stream stream, string containerName, string filename)
        {
            // Normalize names
            containerName = containerName.ToLower();

            // Stronger client options + retries (affects all operations on this client)
            var clientOptions = new BlobClientOptions
            {
                Retry =
            {
                Mode = RetryMode.Exponential,
                MaxRetries = 5,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(15)
            }
            };

            // Single container client, reuse its pipeline for the blob client
            var container = new BlobContainerClient(
                _config.Value.AzureBlobStorageConnectionString,
                containerName,
                clientOptions);

            await container.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);

            var blob = container.GetBlobClient(filename);

            // If you know content type, set it here
            var httpHeaders = new BlobHttpHeaders
            {
                ContentType = "image/png" // or derive dynamically
            };

            // Ensure stream is positioned at start
            if (stream.CanSeek) stream.Position = 0;

            // Serialize upload to avoid parallel chunking over flaky transports
            var transfer = new StorageTransferOptions
            {
                // 4 MB chunks are fine; tune up/down if needed
                InitialTransferSize = 4 * 1024 * 1024,
                MaximumTransferSize = 4 * 1024 * 1024,
                MaximumConcurrency = 1
            };

            // SDK-level overwrite + headers + transfer tuning
            await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = httpHeaders,
                    TransferOptions = transfer
                });

            return blob.Uri;
        }

        public async Task<string> GetFileContentsAsync(string containerName, string filename)
        {
            _logger.LogInformation("GetFileContentsAsync called - containerName: '{ContainerName}', filename: '{Filename}'", containerName, filename);
            
            // Normalize names to match your naming conventions
            //containerName = $"{_config.Value.AzureBlobStorageContainerPrefix.ToLower()}-{containerName.ToLower()}";

            var clientOptions = new BlobClientOptions
            {
                Retry = {
                    Mode = RetryMode.Exponential,
                    MaxRetries = 5,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(15)
                }
            };

            var connectionString = _config.Value.AzureBlobStorageConnectionString;
            _logger.LogInformation("Using connection string starting with: '{ConnectionStringPrefix}'", 
                connectionString?.Substring(0, Math.Min(50, connectionString?.Length ?? 0)));

            var containerClient = new BlobContainerClient(
                connectionString,
                containerName,
                clientOptions);

            _logger.LogInformation("BlobContainerClient created - Container URI: '{ContainerUri}'", containerClient.Uri);

            var blobClient = containerClient.GetBlobClient(filename);
            
            _logger.LogInformation("BlobClient created - Blob URI: '{BlobUri}'", blobClient.Uri);

            try
            {
                var response = await blobClient.DownloadAsync();
                _logger.LogInformation("Blob downloaded successfully - ContentLength: {ContentLength}", response.Value.ContentLength);
                
                using var reader = new StreamReader(response.Value.Content);
                var content = await reader.ReadToEndAsync();
                
                _logger.LogInformation("Blob content read successfully - Length: {ContentLength} characters", content.Length);
                
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download blob - Container: '{ContainerName}', Filename: '{Filename}', BlobUri: '{BlobUri}'", 
                    containerName, filename, blobClient.Uri);
                throw;
            }
        }
    }
}
