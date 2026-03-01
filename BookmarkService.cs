using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VeloxBrowser
{
    public class Bookmark
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public static class BookmarkService
    {
        /// <summary>Returns the bookmarks file path for the current profile.</summary>
        private static string FilePath
        {
            get
            {
                var profile = ProfileService.GetCurrentProfile();
                string folder = ProfileService.GetProfileFolderPath(profile);
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "bookmarks.json");
            }
        }

        public static List<Bookmark> Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return new List<Bookmark>();

            return JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(path))
                   ?? new List<Bookmark>();
        }

        public static void Save(List<Bookmark> bookmarks)
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                JsonSerializer.Serialize(bookmarks, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
