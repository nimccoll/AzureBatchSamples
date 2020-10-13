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
using System;
using System.Collections.Generic;

namespace CreatePoolWithStartupTask
{
    class Program
    {
        // Batch account credentials
        private const string BatchAccountName = "{your batch account name here}";
        private const string BatchAccountKey = "{your batch account key here}";
        private const string BatchAccountUrl = "{your batch account URL here}";

        // Batch resource settings
        private const string PoolId = "DemoPool";
        private const int PoolNodeCount = 1;
        private const string PoolVMSize = "standard_a1";

        static void Main(string[] args)
        {

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
                        new ApplicationPackageReference { ApplicationId = "CopyStartupFiles", Version = "1"}
                    };

                    // Add a startup task to initialize the node
                    StartTask startTask = new StartTask()
                    {
                        WaitForSuccess = true,
                        CommandLine = "cmd /c %AZ_BATCH_APP_PACKAGE_COPYSTARTUPFILES%\\CopyStartupFiles.exe \"%AZ_BATCH_NODE_SHARED_DIR%\""
                    };

                    pool.StartTask = startTask;

                    // Set the maximum number of parallel tasks per node
                    pool.MaxTasksPerComputeNode = 4;
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
            }
        }
    }
}
