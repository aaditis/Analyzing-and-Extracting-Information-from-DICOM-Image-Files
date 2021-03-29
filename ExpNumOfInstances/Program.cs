using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using DicomIO;
using System.IO;
using System.Data;

namespace ExpNumOfInstances
{
    class Program
    {
        static int Main(string[] args)
        {
            string patientDatabasename = "";
            
            try
            {
                patientDatabasename = args[0];
            }
            catch
            {
                Console.WriteLine("Please enter a valid parameter: Patient Database path");
                // Wait for the user to respond before closing.
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                return 0;
            }

            int parameterCount = args.Length;
            // Return an error if proper arguments are not entered
            if (parameterCount != 1)
            {
                Console.WriteLine("Please enter valid number of parameters.");
                // Wait for the user to respond before closing.
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                return 0;
            } 

            if (!File.Exists(patientDatabasename))
            {
                Console.WriteLine("Database name entered are incorrect.");
                // Wait for the user to respond before closing.
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                return 0;
            } 

            // Establish connection with the database
            string pat_connectionString = "Data Source = " + patientDatabasename + "; Version = 3; Synchronous = off; Cache Size = 16000; Default Timeout = 120";
            SQLiteConnection pat_con = new SQLiteConnection(pat_connectionString);

            // establish connection to the database
            try
            {
                pat_con.Open();
            }
            catch
            {
                Console.WriteLine("Could not establish connection to Patient database");
                // Wait for the user to respond before closing.
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                return 0;
            }

            // Retrieve Patient Indices from Patient Database and store in a Data Table
            StringBuilder commandBuilder = new StringBuilder();
            commandBuilder.Append("SELECT PatientIndex from Patients");

            DataTable patientIndexTable = new DataTable();
            try
            {
                // Query the database and fill in the dataset with the returned information.
                using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(commandBuilder.ToString(), pat_con))
                {
                    adapter.Fill(patientIndexTable);
                }
            }
            catch
            {
                patientIndexTable.Dispose();
                patientIndexTable = null;
                return 0;
            }

            // Convert Data Table into list of integers
            List<int> patIndex_list = new List<int>();
            DataRow[] currentRows = patientIndexTable.Select(null, null, DataViewRowState.CurrentRows);
            foreach (DataRow row in currentRows)
            {
                foreach (DataColumn column in patientIndexTable.Columns)
                {
                    var patInd = Convert.ToInt32(row[column]);
                    patIndex_list.Add(patInd);
                }
            }

            string path1 = patientDatabasename.Remove(patientDatabasename.Length - 5);
            int patIndex_len = patIndex_list.Count;
            for (int i = 0; i < patIndex_len; i++)
            {
                List<int> executions = new List<int>();
                // Obtain the path for PatientInfo database from the path of Patient Database and patientIndex
                string patientInfoDatabasename = path1 + "\\" + patIndex_list[i] + "\\PatientInfo.cbsd";
                string directory = path1 + "\\" + patIndex_list[i];
                // Establish connection with the database
                string patInfo_connectionString = "Data Source = " + patientInfoDatabasename + "; Version = 3; Synchronous = off; Cache Size = 16000; Default Timeout = 120";
                SQLiteConnection patInfo_con = new SQLiteConnection(patInfo_connectionString);

                // Establish connection to the PatientInfo database
                try
                {
                    patInfo_con.Open();
                }
                catch
                {
                    Console.WriteLine("Could not establish connection to PatientInfo database");
                    // Wait for the user to respond before closing.
                    Console.Write("Press any key to continue...");
                    Console.ReadKey();
                    return 0;
                }

                // Check if field ExpNumInstances is part of Series table. If yes, continue 
                // Otherwise - Alter the table and add the field to the Series table.
                SQLiteCommand cmd;
                try
                {
                    DataTable ColsTable = patInfo_con.GetSchema("Columns");
                    bool expNumInstancesFieldExists = ColsTable.Select("COLUMN_NAME = 'ExpNumInstances' AND TABLE_NAME = 'Series'").Length != 0;
                    int rc;
                    if (!expNumInstancesFieldExists)
                    {
                        commandBuilder = new StringBuilder();
                        commandBuilder.Append("ALTER TABLE Series ");
                        commandBuilder.Append("ADD COLUMN ExpNumInstances TEXT");
                        cmd = new SQLiteCommand(commandBuilder.ToString(), patInfo_con);
                        rc = cmd.ExecuteNonQuery();
                    }
                    commandBuilder = new StringBuilder();
                    commandBuilder.Append("UPDATE Series ");
                    commandBuilder.Append("SET ExpNumInstances = NULL ");
                    cmd = new SQLiteCommand(commandBuilder.ToString(), patInfo_con);
                    rc = cmd.ExecuteNonQuery();
                }
                catch
                {
                    Console.WriteLine("Failed to add field 'ExpNumInstances' to Series table.");
                    // Wait for the user to respond before closing.
                    Console.Write("Press any key to continue...");
                    Console.ReadKey();
                    return 0;
                }

                try
                {
                    Console.WriteLine("Starting to update the database for Patient Index: " + patIndex_list[i]);
                    // Loop through each Patient Index and store all DICOM file paths in an array
                    string[] filepaths = Directory.GetFiles(directory);
                    // Remove the PatientInfo.cbsd file from the directory
                    filepaths = filepaths.Where(w => !w.Contains("PatientInfo")).ToArray();
                    int file_len = filepaths.Length;
                    for (int j = 0; j < file_len; j++)
                    {
                        DicomHeaderValues localHeaderData = new DicomHeaderValues();
                        DicomIO.DicomReader.ReadDicomHeader(filepaths[j], out localHeaderData);
                        int? expNumInstances = GetDicomInt((ushort?)localHeaderData.ExpNumInstances);
                        string filename_full = GetDicomString(localHeaderData.FileName.ToString());
                        var tokens = filename_full.Split('\\');
                        int token_len = tokens.Length;
                        string filename = tokens[token_len - 2] + "\\" + tokens[token_len - 1];

                        commandBuilder = new StringBuilder();
                        commandBuilder.Append("UPDATE Series ");
                        commandBuilder.Append(string.Format("SET ExpNumInstances = '{0}' ", expNumInstances));
                        commandBuilder.Append("WHERE SeriesIndex IN ( ");
                        commandBuilder.Append("SELECT Series.SeriesIndex ");
                        commandBuilder.Append("FROM Series LEFT JOIN Instances ");
                        commandBuilder.Append("ON Series.SeriesIndex = Instances.SeriesIndex ");
                        commandBuilder.Append(string.Format("WHERE FileName = '{0}' ", filename));
                        commandBuilder.Append("GROUP BY Series.SeriesIndex)");
                        cmd = new SQLiteCommand(commandBuilder.ToString(), patInfo_con);
                        int rc = cmd.ExecuteNonQuery();
                        executions.Add(rc);
                    }
                    commandBuilder = new StringBuilder();
                    commandBuilder.Append("UPDATE Series ");
                    commandBuilder.Append("SET ExpNumInstances = CASE WHEN ExpNumInstances != NumInstances THEN ");
                    commandBuilder.Append("'*' || ExpNumInstances ");
                    commandBuilder.Append("ELSE ExpNumInstances END ");
                    commandBuilder.Append("WHERE ExpNumInstances != -1");
                    cmd = new SQLiteCommand(commandBuilder.ToString(), patInfo_con);
                    int rc2 = cmd.ExecuteNonQuery();

                    bool allNonNegative = executions.All(x => x >= 0);
                    if (allNonNegative)
                        Console.WriteLine("ExpNumInstances successfully updated in database for Patient Index: " + patIndex_list[i]);
                    Console.WriteLine("");
                }
                catch
                {
                    Console.WriteLine("Could not add tag values to Series table in PatientInfo Database.");
                    // Wait for the user to respond before closing.
                    Console.Write("Press any key to continue...");
                    Console.ReadKey();
                    return 0;
                }

            }

            // Wait for the user to respond before closing.
            Console.Write("Press any key to continue...");
            Console.ReadKey();
            return 0;
        }

        // Gets the integer representation of the specified DICOM tag unsigned short.
        private static int? GetDicomInt(ushort? tagValue)
        {
            if (tagValue != null && tagValue != 0)
            {
                return tagValue;
            }

            return -1;
        }

        private static string GetDicomString(string tagValue)
        {
            if (tagValue != null)
            {
                return tagValue;
            }

            return string.Empty;
        }
    }
}