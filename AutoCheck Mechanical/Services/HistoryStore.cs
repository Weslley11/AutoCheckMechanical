using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using AutoCheckMechanical.Models;

namespace AutoCheckMechanical.Services
{
    public static class HistoryStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoCheckMechanical",
            "historico.json");

        public static List<BatchFileResult> Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new List<BatchFileResult>();

                using (FileStream stream = File.OpenRead(FilePath))
                {
                    DataContractJsonSerializer serializer =
                        new DataContractJsonSerializer(typeof(List<BatchFileResult>));

                    return serializer.ReadObject(stream) as List<BatchFileResult>
                        ?? new List<BatchFileResult>();
                }
            }
            catch (Exception)
            {
                return new List<BatchFileResult>();
            }
        }

        public static void Save(List<BatchFileResult> results)
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = File.Create(FilePath))
                {
                    DataContractJsonSerializer serializer =
                        new DataContractJsonSerializer(typeof(List<BatchFileResult>));

                    serializer.WriteObject(stream, results);
                }
            }
            catch (Exception)
            {
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception)
            {
            }
        }
    }
}
