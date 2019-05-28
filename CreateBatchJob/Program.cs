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
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;

namespace CreateBatchJob
{
    class Program
    {
        // Batch account credentials
        private const string BatchAccountName = "{your batch account name here}";
        private const string BatchAccountKey = "{your batch account key here}";
        private const string BatchAccountUrl = "{your batch account URL here}";

        // Storage account credentials
        private const string StorageAccountName = "{your storage account name here}";
        private const string StorageAccountKey = "{your storage account key here}";

        // Batch resource settings
        private const string PoolId = "DotNetQuickstartPool";
        private const string JobId = "DotNetQuickstartJob";
        private const int PoolNodeCount = 1;
        private const string PoolVMSize = "standard_a1";

        static void Main(string[] args)
        {
            string storageConnectionString =
                $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey}";

            // Retrieve the storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            // Create the blob client
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            const string inputContainerName = "batchinput";
            List<string> inputFilePaths = new List<string>
                {
                    "taskdata0.txt",
                    "taskdata1.txt",
                    "taskdata2.txt"
                };

            // Upload the input files to blob storage
            List<ResourceFile> inputFiles = new List<ResourceFile>();

            foreach (string filePath in inputFilePaths)
            {
                inputFiles.Add(UploadFileToContainer(blobClient, inputContainerName, filePath));
            }

            // Get a SAS Url for the output container
            const string outputContainerName = "batchoutput";
            string outputContainerSasUrl = GetOutputContainerSasUrl(blobClient, outputContainerName);

            // Create the virtual machine image reference
            ImageReference imageReference = new ImageReference(
                publisher: "MicrosoftWindowsServer",
                offer: "WindowsServer",
                sku: "2016-datacenter-smalldisk",
                version: "latest");

            // Create the virtual machine configuration for the pool
            VirtualMachineConfiguration virtualMachineConfiguration = new VirtualMachineConfiguration(
                imageReference: imageReference,
                nodeAgentSkuId: "batch.node.windows amd64");

            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(BatchAccountUrl, BatchAccountName, BatchAccountKey);

            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                Console.WriteLine("Creating pool [{0}]...", PoolId);
                try
                {
                    CloudPool pool = batchClient.PoolOperations.CreatePool(
                        poolId: PoolId,
                        targetDedicatedComputeNodes: PoolNodeCount,
                        virtualMachineSize: PoolVMSize,
                        virtualMachineConfiguration: virtualMachineConfiguration);

                    // Specify the application packages for the tasks
                    pool.ApplicationPackageReferences = new List<ApplicationPackageReference>
                    {
                        new ApplicationPackageReference { ApplicationId = "ReadWriteFile", Version = "1"}
                    };

                    pool.Commit();
                }
                catch (BatchException be)
                {
                    // Accept the specific error code PoolExists as that is expected if the pool already exists
                    if (be.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.PoolExists)
                    {
                        Console.WriteLine("The pool {0} already existed when we tried to create it", PoolId);
                    }
                    else
                    {
                        throw; // Any other exception is unexpected
                    }
                }

                Console.WriteLine("Creating job [{0}]...", JobId);
                try
                {
                    CloudJob job = batchClient.JobOperations.CreateJob();
                    job.Id = JobId;
                    job.PoolInformation = new PoolInformation { PoolId = PoolId };

                    job.Commit();
                }
                catch (BatchException be)
                {
                    // Accept the specific error code JobExists as that is expected if the job already exists
                    if (be.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.JobExists)
                    {
                        Console.WriteLine("The job {0} already existed when we tried to create it", JobId);
                    }
                    else
                    {
                        throw; // Any other exception is unexpected
                    }
                }

                // Create a collection to hold the tasks that we'll be adding to the job
                Console.WriteLine("Adding {0} tasks to job [{1}]...", inputFiles.Count, JobId);

                List<CloudTask> tasks = new List<CloudTask>();

                // Create each of the tasks to process one of the input files. 
                for (int i = 0; i < inputFiles.Count; i++)
                {
                    string taskId = String.Format("Task{0}", i);
                    string inputFilename = inputFiles[i].FilePath;
                    string outputFileName = string.Format("out{0}", inputFilename);
                    string taskCommandLine = string.Format("cmd /c %AZ_BATCH_APP_PACKAGE_READWRITEFILE%\\ReadWriteFile.exe {0} {1}", inputFilename, outputFileName);

                    CloudTask task = new CloudTask(taskId, taskCommandLine);

                    // Set the resource files and output files for the task
                    task.ResourceFiles = new List<ResourceFile> { inputFiles[i] };
                    task.OutputFiles = new List<OutputFile>
                    {
                        new OutputFile(
                            filePattern: outputFileName,
                            destination: new OutputFileDestination(new OutputFileBlobContainerDestination(containerUrl: outputContainerSasUrl, path: outputFileName)),
                            uploadOptions: new OutputFileUploadOptions(OutputFileUploadCondition.TaskCompletion))
                    };
                    tasks.Add(task);
                }

                // Add all tasks to the job.
                batchClient.JobOperations.AddTask(JobId, tasks);

                // Monitor task success/failure, specifying a maximum amount of time to wait for the tasks to complete.
                TimeSpan timeout = TimeSpan.FromMinutes(30);
                Console.WriteLine("Monitoring all tasks for 'Completed' state, timeout in {0}...", timeout);

                IEnumerable<CloudTask> addedTasks = batchClient.JobOperations.ListTasks(JobId);

                batchClient.Utilities.CreateTaskStateMonitor().WaitAll(addedTasks, TaskState.Completed, timeout);

                Console.WriteLine("All tasks reached state Completed.");

                // Print task output
                Console.WriteLine();
                Console.WriteLine("Printing task output...");

                IEnumerable<CloudTask> completedtasks = batchClient.JobOperations.ListTasks(JobId);

                foreach (CloudTask task in completedtasks)
                {
                    string nodeId = String.Format(task.ComputeNodeInformation.ComputeNodeId);
                    Console.WriteLine("Task: {0}", task.Id);
                    Console.WriteLine("Node: {0}", nodeId);
                    Console.WriteLine("Standard out:");
                    Console.WriteLine(task.GetNodeFile(Constants.StandardOutFileName).ReadAsString());
                }

                // Clean up Batch resources (if the user so chooses)
                Console.WriteLine();
                Console.Write("Delete job? [yes] no: ");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.JobOperations.DeleteJob(JobId);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.PoolOperations.DeletePool(PoolId);
                }
            }
        }

        private static string GetOutputContainerSasUrl(CloudBlobClient blobClient, string outputContainerName)
        {
            CloudBlobContainer container = blobClient.GetContainerReference(outputContainerName);
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(2),
                Permissions = SharedAccessBlobPermissions.Create | SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write
            };
            string sasContainerToken = container.GetSharedAccessSignature(sasConstraints);
            return string.Format("{0}{1}", container.Uri, sasContainerToken);
        }

        private static ResourceFile UploadFileToContainer(CloudBlobClient blobClient, string containerName, string filePath)
        {
            Console.WriteLine("Uploading file {0} to container [{1}]...", filePath, containerName);

            string blobName = Path.GetFileName(filePath);

            filePath = Path.Combine(Environment.CurrentDirectory, filePath);

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blobData = container.GetBlockBlobReference(blobName);
            blobData.UploadFromFileAsync(filePath).Wait();

            // Set the expiry time and permissions for the blob shared access signature. 
            // In this case, no start time is specified, so the shared access signature 
            // becomes valid immediately
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(2),
                Permissions = SharedAccessBlobPermissions.Read
            };

            // Construct the SAS URL for blob
            string sasBlobToken = blobData.GetSharedAccessSignature(sasConstraints);
            string blobSasUri = String.Format("{0}{1}", blobData.Uri, sasBlobToken);

            return ResourceFile.FromUrl(blobSasUri, blobName);
        }
    }
}
