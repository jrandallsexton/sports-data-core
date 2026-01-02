using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Options;

using SportsData.Core.Config;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Blobs
{
    public interface IProvideBlobStorage
    {
        /// <summary>
/// Uploads an image stream to the specified blob container and returns the blob's URI.
/// </summary>
/// <param name="stream">Stream containing the image data. If the stream supports seeking, its position will be reset to 0 before upload.</param>
/// <param name="containerName">Name of the target blob container (normalized to lowercase).</param>
/// <param name="filename">Name to assign to the uploaded blob within the container.</param>
/// <returns>The URI of the uploaded blob.</returns>
Task<Uri> UploadImageAsync(Stream stream, string containerName, string filename);
        /// <summary>
/// Retrieves the contents of the specified blob and returns them as a string.
/// </summary>
/// <param name="containerName">The name of the container containing the blob.</param>
/// <param name="filename">The name of the blob/file to read.</param>
/// <param name="cancellationToken">Token to cancel the download and read operations.</param>
/// <returns>The blob's contents as a string.</returns>
Task<string> GetFileContentsAsync(string containerName, string filename, CancellationToken cancellationToken = default);
    }

    public class BlobStorageProvider : IProvideBlobStorage
    {
        private readonly IOptions<CommonConfig> _config;

        public BlobStorageProvider(IOptions<CommonConfig> config)
        {
            _config = config;
        }

        /// <summary>
        /// Uploads an image stream to the specified blob container and returns the blob's URI.
        /// </summary>
        /// <param name="stream">Stream containing the image data to upload. If the stream supports seeking, its position will be reset to the start.</param>
        /// <param name="containerName">Name of the target blob container; the name will be normalized to lowercase.</param>
        /// <param name="filename">The blob name (filename) to create or overwrite in the container.</param>
        /// <returns>The <see cref="Uri"/> of the uploaded blob.</returns>
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

        /// <summary>
        /// Retrieves the contents of a blob from the specified container and returns it as a string.
        /// </summary>
        /// <param name="containerName">The name of the blob container to read from.</param>
        /// <param name="filename">The name (path) of the blob within the container.</param>
        /// <param name="cancellationToken">A token to cancel the download and read operations.</param>
        /// <returns>The full contents of the blob as a string.</returns>
        public async Task<string> GetFileContentsAsync(string containerName, string filename, CancellationToken cancellationToken = default)
        {
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

            var containerClient = new BlobContainerClient(
                _config.Value.AzureBlobStorageConnectionString,
                containerName,
                clientOptions);

            var blobClient = containerClient.GetBlobClient(filename);

            var response = await blobClient.DownloadAsync(cancellationToken);
            using var reader = new StreamReader(response.Value.Content);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}