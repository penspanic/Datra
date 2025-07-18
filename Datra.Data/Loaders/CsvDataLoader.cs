using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Datra.Data.Interfaces;

namespace Datra.Data.Loaders
{
    /// <summary>
    /// Loader for loading/saving data in CSV format
    /// </summary>
    public class CsvDataLoader : IDataLoader
    {
        private readonly CsvConfiguration _config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLower(),
            HeaderValidated = null,
            MissingFieldFound = null
        };
        
        public T LoadSingle<T>(string text) where T : class, new()
        {
            // CSV is table format, so for single data read only the first row
            using var reader = new StringReader(text);
            using var csv = new CsvReader(reader, _config);
            
            var records = csv.GetRecords<T>().ToList();
            if (records.Count == 0)
            {
                throw new InvalidOperationException("CSV data is empty.");
            }
            
            return records.First();
        }
        
        public Dictionary<TKey, T> LoadTable<TKey, T>(string text) 
            where T : class, ITableData<TKey>, new()
        {
            using var reader = new StringReader(text);
            using var csv = new CsvReader(reader, _config);
            
            var items = csv.GetRecords<T>().ToList();
            return items.ToDictionary(item => item.Id);
        }
        
        public string SaveSingle<T>(T data) where T : class
        {
            using var writer = new StringWriter();
            using var csv = new CsvWriter(writer, _config);
            
            csv.WriteRecords(new[] { data });
            return writer.ToString();
        }
        
        public string SaveTable<TKey, T>(Dictionary<TKey, T> table) 
            where T : class, ITableData<TKey>
        {
            using var writer = new StringWriter();
            using var csv = new CsvWriter(writer, _config);
            
            csv.WriteRecords(table.Values);
            return writer.ToString();
        }
    }
}