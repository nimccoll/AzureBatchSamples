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
using System.IO;

namespace ReadWriteFile
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputFileName = string.Empty;
            string outputFileName = string.Empty;
            string inputText = string.Empty;


            if (args.Length == 2)
            {
                inputFileName = args[0];
                outputFileName = args[1];
            }

            if (File.Exists(inputFileName))
            {
                inputText = File.ReadAllText(inputFileName);
            }

            File.WriteAllText(outputFileName, inputText);
        }
    }
}
