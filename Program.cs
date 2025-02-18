// Order-ORM - A DICOM Worklist to ORM Converter
// Copyright (c) 2025 [Your Company Name]
//
// This software is licensed under the Medical Software Academic and Restricted Use License.
// See the LICENSE.md file for details. Source available for academic use only; commercial use
// and use by competitors or clients requires written permission from Flux Inc.
// Contact: [sales@fluxinc.co]

using Dicom;
using Dicom.Network;
using System;
using System.Collections.Generic;
using System.Configuration; // For App.config
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace OrderORM
{
    class Program
    {
        private static string _connectionString = "Data Source=sent_orders.db";

        static void Main(string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Usage: Order-ORM <spsStartDate> <modality> <stationName>");
                    return;
                }

                string spsStartDate = args[0]; // Format: YYYYMMDD
                string modality = args[1];
                string stationName = args[2];

                // Since Main can't be async in .NET Framework, wrap in Task.Run
                Task.Run(async () =>
                {
                    await InitializeDatabase();
                    var worklistResults = await QueryWorklist(spsStartDate, modality, stationName);

                    foreach (var result in worklistResults)
                    {
                        if (!await WasOrderSentRecently(result))
                        {
                            await SendOrmMessage(result);
                            await RecordSentOrder(result);
                        }
                    }

                    Console.WriteLine($"Processed {worklistResults.Count} worklist items");
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task InitializeDatabase()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS SentOrders (
                        AccessionNumber TEXT PRIMARY KEY,
                        SendDateTime TEXT
                    )";
                await command.ExecuteNonQueryAsync();
            }
        }

        private static async Task<List<DicomDataset>> QueryWorklist(string spsStartDate, string modality, string stationName)
        {
            var results = new List<DicomDataset>();
            var client = new DicomClient();

            var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Worklist)
            {
                Dataset = new DicomDataset
                {
                    { DicomTag.ScheduledProcedureStepStartDate, spsStartDate },
                    { DicomTag.Modality, modality },
                    { DicomTag.ScheduledStationAETitle, stationName },
                    { DicomTag.AccessionNumber, "" },
                    { DicomTag.PatientID, "" },
                    { DicomTag.PatientName, "" },
                    { DicomTag.ScheduledProcedureStepID, "" }
                }
            };

            request.OnResponseReceived = (req, response) =>
            {
                if (response.Dataset != null)
                {
                    results.Add(response.Dataset);
                }
            };

            client.AddRequest(request);
            await Task.Run(() => client.Send(
                ConfigurationManager.AppSettings["WorklistHost"],
                int.Parse(ConfigurationManager.AppSettings["WorklistPort"]),
                false,
                ConfigurationManager.AppSettings["WorklistCallingAE"],
                ConfigurationManager.AppSettings["WorklistCalledAE"]
            ));

            return results;
        }

        private static async Task<bool> WasOrderSentRecently(DicomDataset dataset)
        {
            var accessionNumber = dataset.GetString(DicomTag.AccessionNumber);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT SendDateTime
                    FROM SentOrders
                    WHERE AccessionNumber = @accession
                    AND SendDateTime >= @dateLimit";
                command.Parameters.AddWithValue("@accession", accessionNumber);
                command.Parameters.AddWithValue("@dateLimit", DateTime.UtcNow.AddDays(-7).ToString("o"));

                var result = await command.ExecuteScalarAsync();
                return result != null;
            }
        }

        private static async Task SendOrmMessage(DicomDataset worklistItem)
        {
            var client = new DicomClient();
            var orm = CreateOrmDataset(worklistItem);

            var request = new DicomCStoreRequest(orm);
            client.AddRequest(request);
            await Task.Run(() => client.Send(
                ConfigurationManager.AppSettings["DestinationHost"],
                int.Parse(ConfigurationManager.AppSettings["DestinationPort"]),
                false,
                ConfigurationManager.AppSettings["DestinationCallingAE"],
                ConfigurationManager.AppSettings["DestinationCalledAE"]
            ));
        }

        private static DicomDataset CreateOrmDataset(DicomDataset worklistItem)
        {
            var orm = new DicomDataset
            {
                { DicomTag.SOPClassUID, DicomUID.GeneralPurposeScheduledProcedureStepSOPClass },
                { DicomTag.SOPInstanceUID, DicomUID.Generate() },
                { DicomTag.MessageID, "1" },
                { DicomTag.OrderPlacerIdentifierSequence, new DicomSequence() },
                { DicomTag.OrderFillerIdentifierSequence, new DicomSequence() }
            };

            orm.AddOrUpdate(DicomTag.AccessionNumber, worklistItem.GetString(DicomTag.AccessionNumber));
            orm.AddOrUpdate(DicomTag.PatientID, worklistItem.GetString(DicomTag.PatientID));
            orm.AddOrUpdate(DicomTag.PatientName, worklistItem.GetString(DicomTag.PatientName));

            return orm;
        }

        private static async Task RecordSentOrder(DicomDataset dataset)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO SentOrders (AccessionNumber, SendDateTime)
                    VALUES (@accession, @datetime)";
                command.Parameters.AddWithValue("@accession", dataset.GetString(DicomTag.AccessionNumber));
                command.Parameters.AddWithValue("@datetime", DateTime.UtcNow.ToString("o"));

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
