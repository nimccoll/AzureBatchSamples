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
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QueueProcessingJobManager
{
    class Program
    {
        // Batch account credentials
        private const string BatchAccountName = "{your batch account name here}";
        private const string BatchAccountKey = "{your batch account key here}";
        private const string BatchAccountUrl = "{your batch account URL here}";

        // Storage queue credentials
        private const string StorageAccountConnectionString = "{your storage account connection string here}";
        private const string QueueName = "azurebatchqueue";
        private const string TABLE_NAME = "QueueProcessingControl";

        static void Main(string[] args)
        {
            string jobId = args[0];
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(BatchAccountUrl, BatchAccountName, BatchAccountKey);

            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                Console.WriteLine("Retrieving job [{0}]...", jobId);
                CloudJob job = null;
                try
                {
                    job = batchClient.JobOperations.GetJob(jobId);
                }
                catch (BatchException be)
                {
                    // Were we unable to find the job?
                    if (be.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.JobNotFound)
                    {
                        Console.WriteLine("The job {0} was not found when we attempted to retrieve it", jobId);
                    }
                    else
                    {
                        throw; // Any other exception is unexpected
                    }
                }

                // Retrieve messages from Azure Queue and create a job task to process each message
                if (job != null)
                {
                    Console.WriteLine("Retrieving messages from queue {0}", QueueName);
                    QueueClient queueClient = new QueueClient(StorageAccountConnectionString, QueueName);
                    
                    if (queueClient.Exists())
                    {
                        do
                        {
                            List<CloudTask> tasks = new List<CloudTask>();
                            QueueMessage[] messages = queueClient.ReceiveMessages();
                            if (messages != null && messages.Length > 0)
                            {
                                Console.WriteLine("Messages found. Creating job tasks...");
                                foreach (QueueMessage message in messages)
                                {
                                    byte[] decodedMessage = Convert.FromBase64String(message.MessageText);
                                    dynamic customer = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(decodedMessage));
                                    string taskId = String.Format("Task{0}", Guid.NewGuid());
                                    string taskCommandLine = string.Format("cmd /c %AZ_BATCH_APP_PACKAGE_PROCESSMESSAGE%\\ProcessMessage.exe {0} {1} {2} {3}", customer.customerId, customer.firstName, customer.lastName, customer.state);

                                    CloudTask task = new CloudTask(taskId, taskCommandLine);
                                    tasks.Add(task);
                                    queueClient.DeleteMessage(message.MessageId, message.PopReceipt);
                                }
                                Console.WriteLine("Starting job tasks...");
                                batchClient.JobOperations.AddTask(job.Id, tasks);
                            }

                            // Has a job stop been requested?
                            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(StorageAccountConnectionString);
                            CloudTableClient cloudTableClient = cloudStorageAccount.CreateCloudTableClient(new TableClientConfiguration());
                            CloudTable controlTable = cloudTableClient.GetTableReference(TABLE_NAME);
                            bool exists = controlTable.Exists();

                            if (exists)
                            {
                                List<DynamicTableEntity> results = controlTable.ExecuteQuery(new TableQuery()).ToList();
                                if (results.Count > 0)
                                {
                                    Console.WriteLine("*** Job stop has been requested. Shutting down... ***");
                                    break;
                                }
                            }

                        } while (true);

                        // Make sure all tasks have finished before terminating the job
                        bool tasksRunning = false;
                        do
                        {
                            tasksRunning = false;
                            List<CloudTask> tasks = batchClient.JobOperations.ListTasks(jobId).ToList();
                            foreach (CloudTask task in tasks)
                            {
                                if (task.DisplayName != "Queue Processing Job Manager"
                                    && task.State != TaskState.Completed)
                                {
                                    Console.WriteLine("*** Tasks are still running ***");
                                    tasksRunning = true;
                                    break;
                                }
                            }
                        } while (tasksRunning);

                        // Terminate the job when all tasks have been completed
                        Console.WriteLine("*** All Tasks Completed - Terminating Job {0}", jobId);
                        batchClient.JobOperations.TerminateJob(jobId);
                    }
                }
            }
        }
    }
}
