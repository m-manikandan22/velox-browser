using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace VeloxBrowser
{
    public static class HistoryService
    {
        /// <summary>Returns the history DB path for the current profile.</summary>
        private static string DbPath
        {
            get
            {
                var profile = ProfileService.GetCurrentProfile();
                string folder = ProfileService.GetProfileFolderPath(profile);
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "history.db");
            }
        }

        public static void Initialize()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            @"
            CREATE TABLE IF NOT EXISTS History (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT,
                Url TEXT,
                VisitTime TEXT
            );
            ";
            cmd.ExecuteNonQuery();
        }

        public static void Add(string title, string url)
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            @"
            INSERT INTO History (Title, Url, VisitTime)
            VALUES ($title, $url, $time);
            ";
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$url", url);
            cmd.Parameters.AddWithValue("$time", DateTime.Now.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static System.Collections.Generic.List<HistoryRecord> GetHistory()
        {
            var results = new System.Collections.Generic.List<HistoryRecord>();
            if (!File.Exists(DbPath)) return results;

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Title, Url, VisitTime FROM History ORDER BY Id DESC LIMIT 500";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new HistoryRecord
                {
                    Title = reader.GetString(0),
                    Url = reader.GetString(1),
                    VisitTime = DateTime.Parse(reader.GetString(2))
                });
            }
            return results;
        }

        public static void ClearHistory()
        {
            if (!File.Exists(DbPath)) return;
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM History";
            cmd.ExecuteNonQuery();
        }
    }

    public class HistoryRecord
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime VisitTime { get; set; }
    }
}
