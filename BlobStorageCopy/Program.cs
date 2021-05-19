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
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlobStorageCopy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("*** Welcome to the Blob Container Copy Utility ***");
            Console.Write("Enter the connection string of the source storage account: ");
            string sourceStorageConnectionString = Console.ReadLine();
            Console.Write("Enter the name of the source container: ");
            string sourceContainer = Console.ReadLine();
            Console.Write("Enter the connection string of the destination storage account: ");
            string destStorageConnectionString = Console.ReadLine();
            Console.Write("Enter the name of the destination container: ");
            string destContainer = Console.ReadLine();

            if (string.IsNullOrEmpty(sourceStorageConnectionString)
                || string.IsNullOrEmpty(sourceContainer)
                || string.IsNullOrEmpty(destStorageConnectionString)
                || string.IsNullOrEmpty(destContainer))
            {
                Console.WriteLine("You must provide values for the source connection string, source container, destination connection string and destination container");
            }
            else
            {
                BlobServiceClient sourceBlobServiceClient = new BlobServiceClient(sourceStorageConnectionString);
                BlobContainerClient sourceContainerClient = sourceBlobServiceClient.GetBlobContainerClient(sourceContainer);

                BlobServiceClient destBlobServiceClient = new BlobServiceClient(destStorageConnectionString);
                BlobContainerClient destContainerClient = destBlobServiceClient.GetBlobContainerClient(destContainer);

                if (sourceContainerClient.Exists())
                {
                    if (!destContainerClient.Exists())
                    {
                        destContainerClient.Create(PublicAccessType.None);
                    }

                    List<BlobItem> blobs = sourceContainerClient.GetBlobs().ToList();
                    Console.WriteLine($"Copying {blobs.Count} files from source to destination.");
                    foreach (BlobItem blob in blobs)
                    {
                        BlobClient sourceBlob = sourceContainerClient.GetBlobClient(blob.Name);
                        if (sourceBlob.Exists())
                        {
                            Console.WriteLine($"{blob.Name} copy started...");
                            BlobDownloadInfo download = sourceBlob.Download();
                            BlobClient destBlob = destContainerClient.GetBlobClient(blob.Name);
                            destBlob.Upload(download.Content);
                            Console.WriteLine($"{blob.Name} copied successfully.");
                        }
                    }

                }
                else
                {
                    Console.WriteLine($"Source blob container {sourceContainer} does not exist.");
                }
                Console.WriteLine("*** Copy completed ***");
            }
            Console.WriteLine("*** Press any key to exit ***");
            Console.Read();
        }
    }
}
