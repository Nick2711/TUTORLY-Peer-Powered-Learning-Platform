using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Tutorly.Server.Helpers;

namespace Tutorly.Server.Handler
{
    public class AzureBlobStoreHandler
    {
        #region ctor
        public AzureBlobStoreHandler(AzureBlobStoreOptions options) :
          this(options.ConnectionString, options.BlobContainerName)
        {
            Console.WriteLine($"DEBUG: AzureBlobStoreHandler - Constructor called with options");
            Console.WriteLine($"DEBUG: AzureBlobStoreHandler - ConnectionString: {(!string.IsNullOrEmpty(options.ConnectionString) ? "SET" : "EMPTY")}");
            Console.WriteLine($"DEBUG: AzureBlobStoreHandler - BlobContainerName: {options.BlobContainerName}");
        }
        public AzureBlobStoreHandler(string connectionString, string blobContainerName)
        {
            this.connectionString = connectionString;
            this.blobContainerName = blobContainerName;

            Console.WriteLine($"DEBUG: AzureBlobStoreHandler - Constructor called with parameters");
            Console.WriteLine($"DEBUG: AzureBlobStoreHandler - ConnectionString: {(!string.IsNullOrEmpty(connectionString) ? "SET" : "EMPTY")}");
            Console.WriteLine($"DEBUG: AzureBlobStoreHandler - BlobContainerName: {blobContainerName}");
        }

        #endregion

        public async Task<Uri> PutAsync(string sourceFilePath, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            try
            {
                string remoteFilename = Path.GetFileName(sourceFilePath);

                BlobClient blobClient = new BlobClient(connectionString, blobContainerName, remoteFilename);

                await using FileStream stream = File.OpenRead(sourceFilePath);

                await blobClient.UploadAsync(stream, overwrite, cancellationToken);

                return blobClient.Uri;
            }
            catch (Exception exc)
            {
                throw new Exception($"Error Putting file {sourceFilePath} in BLOB container {blobContainerName}", exc);
            }
        }
        public async Task<Uri> PutAsync(Stream sourceStream, string remoteFilename, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (sourceStream.CanSeek)
                    sourceStream.Seek(0, SeekOrigin.Begin);

                BlobClient blobClient = new BlobClient(connectionString, blobContainerName, remoteFilename);

                await blobClient.UploadAsync(sourceStream, overwrite, cancellationToken);

                return blobClient.Uri;
            }
            catch (Exception exc)
            {
                throw new Exception($"Error PUTting file to {remoteFilename} in BLOB container {blobContainerName}", exc);
            }
        }
        public async Task<Stream> OpenForWriteAsync(string remoteFilename, bool overwrite, CancellationToken cancellationToken = default)
        {
            try
            {
                BlobClient blobClient = new BlobClient(connectionString, blobContainerName, remoteFilename);

                return await blobClient.OpenWriteAsync(overwrite, cancellationToken: cancellationToken);
            }
            catch (Exception exc)
            {
                throw new Exception($"Error opening write stream to file {remoteFilename} in BLOB container {blobContainerName}", exc);
            }
        }

        public async Task DownloadAsync(string remoteFilename, string localFilePath, CancellationToken cancellationToken = default)
        {
            try
            {
                BlobClient blobClient = new BlobClient(connectionString, blobContainerName, remoteFilename);

                await using FileStream stream = File.OpenWrite(localFilePath);

                BlobDownloadInfo downloadInfo = await blobClient.DownloadAsync(cancellationToken: cancellationToken);

                await downloadInfo.Content.CopyToAsync(stream, cancellationToken);
            }
            catch (Exception exc)
            {
                throw new Exception($"Error downloading remote file {remoteFilename} from BLOB container {blobContainerName} to local file {localFilePath}", exc);
            }
        }

        public async Task<Stream> GetAsStreamAsync(string remoteFilename, CancellationToken cancellationToken = default)
        {
            try
            {
                BlobClient blobClient = new BlobClient(connectionString, blobContainerName, remoteFilename);

                BlobDownloadInfo downloadInfo = await blobClient.DownloadAsync(cancellationToken: cancellationToken);

                return downloadInfo.Content;
            }
            catch (Exception exc)
            {
                throw new Exception($"Error getting download stream for remote file {remoteFilename} from BLOB container {blobContainerName}", exc);
            }
        }

        public async Task DeleteAsync(string remoteFilename, CancellationToken cancellationToken = default)
        {
            try
            {
                BlobClient blobClient = new BlobClient(connectionString, blobContainerName, remoteFilename);

                await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
            }
            catch (Exception exc)
            {
                throw new Exception($"Error deleting remote file {remoteFilename} from BLOB container {blobContainerName}", exc);
            }
        }

        public async Task<bool> ContainsAsync(string remoteFilename, CancellationToken cancellationToken = default)
        {
            try
            {
                BlobClient blobClient = new BlobClient(connectionString, blobContainerName, remoteFilename);

                Response<bool> exists = await blobClient.ExistsAsync(cancellationToken);
                if (exists.HasValue == false)
                    throw new InvalidOperationException("Response has no value");

                return exists.Value;
            }
            catch (Exception exc)
            {
                throw new Exception($"Error checking if remote file {remoteFilename} exists in BLOB container {blobContainerName}", exc);
            }
        }

        public async Task<Uri> GetUriAsync(string remoteFilename)
        {
            try
            {
                BlobClient blobClient = new BlobClient(connectionString, blobContainerName, remoteFilename);

                return await Task.FromResult(blobClient.Uri);
            }
            catch (Exception exc)
            {
                throw new Exception($"Error getting URI for remote file {remoteFilename} in BLOB container {blobContainerName}", exc);
            }
        }

        public async Task<IList<string>> GetAllAsync(int maxEntries = int.MaxValue)
        {
            try
            {
                int entryCount = 0;

                BlobContainerClient blobContainerClient = new BlobContainerClient(connectionString, blobContainerName);

                AsyncPageable<BlobItem> pageable = blobContainerClient.GetBlobsAsync();

                IAsyncEnumerator<BlobItem> enumerator = pageable.GetAsyncEnumerator();

                List<string> result = new List<string>();

                while (await enumerator.MoveNextAsync())
                {
                    BlobItem blobItem = enumerator.Current;

                    result.Add(blobItem.Name);
                    entryCount++;
                    if (entryCount == maxEntries)
                        break;
                }

                return result;
            }
            catch (Exception exc)
            {
                throw new Exception($"Error getting BLOBs in BLOB container {blobContainerName}", exc);
            }
        }


        #region Fields
        private readonly string connectionString;
        private readonly string blobContainerName;
        #endregion
    }
}
