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
using Microsoft.Azure.Cosmos.Table;

namespace ProcessMessage
{
    class Program
    {
        private static readonly string TABLE_STORAGE_CONNECTIONSTRING = "{your storage account connection string here}";
        private static readonly string TABLE_NAME = "Customers";

        static void Main(string[] args)
        {
            string customerId = args[0];
            string firstName = args[1];
            string lastName = args[2];
            string state = args[3];

            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(TABLE_STORAGE_CONNECTIONSTRING);
            CloudTableClient cloudTableClient = cloudStorageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable customersTable = cloudTableClient.GetTableReference(TABLE_NAME);
            bool exists = customersTable.Exists();

            if (exists)
            {
                CustomerEntity customerEntity = new CustomerEntity()
                {
                    CustomerId = customerId,
                    FirstName = firstName,
                    LastName = lastName,
                    State = state
                };
                TableOperation createOperation = TableOperation.Insert(customerEntity);
                TableResult result = customersTable.Execute(createOperation);
            }
        }
    }
}
