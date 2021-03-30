//===============================================================================
// Microsoft FastTrack for Azure
// Azure Batch Samples
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CopyStartupFiles
{
    class Program
    {
        private static readonly string AZURE_STORAGE_CONNECTION_STRING = "{your blob storage account connection string here}";
        private static readonly string AZURE_FILES_CONNECTION_STRING = "{your files storage account connection string here}";

        static void Main(string[] args)
        {
            string downloadDirectory = args[0];

            // Create a BlobServiceClient object which will be used to create a container client
            BlobServiceClient blobServiceClient = new BlobServiceClient(AZURE_STORAGE_CONNECTION_STRING);

            // Create a unique name for the container
            string containerName = "startupfiles";

            // Create the container and return a container client object
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            List<BlobItem> blobs = containerClient.GetBlobs().ToList();

            // Download the blob files in parallel - maximum of 10 at a time }
            Parallel.ForEach(blobs, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, blob =>
            {
                string downloadFilePath = Path.Combine(downloadDirectory, blob.Name);
                BlobClient blobClient = containerClient.GetBlobClient(blob.Name);
                BlobDownloadInfo download = blobClient.Download();

                using (FileStream downloadFileStream = File.OpenWrite(downloadFilePath))
                {
                    download.Content.CopyTo(downloadFileStream);
                    downloadFileStream.Close();
                }
            });

            string shareName = "startupfiles";

            ShareClient shareClient = new ShareClient(AZURE_FILES_CONNECTION_STRING, shareName);

            if (shareClient.Exists())
            {
                ShareDirectoryClient shareDirectoryClient = shareClient.GetRootDirectoryClient();

                List<ShareFileItem> items = shareDirectoryClient.GetFilesAndDirectories().ToList();
                foreach(ShareFileItem item in items)
                {
                    if (!item.IsDirectory)
                    {
                        string downloadFilePath = Path.Combine(downloadDirectory, item.Name);
                        ShareFileClient shareFileClient = shareDirectoryClient.GetFileClient(item.Name);
                        ShareFileDownloadInfo download = shareFileClient.Download();
                        using (FileStream downloadFileStream = File.OpenWrite(downloadFilePath))
                        {
                            download.Content.CopyTo(downloadFileStream);
                            downloadFileStream.Close();
                        }
                    }
                }
            }
        }
    }
}
