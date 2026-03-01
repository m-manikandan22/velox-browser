using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace VeloxBrowser
{
    public class BrowserProfile : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string _name = "Default Profile";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string FolderName { get; set; } = "Default";

        private string? _email;
        public string? Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        private bool _isCurrent;
        public bool IsCurrent
        {
            get => _isCurrent;
            set { _isCurrent = value; OnPropertyChanged(); }
        }

        private string? _avatarUrl;
        public string? AvatarUrl
        {
            get => _avatarUrl;
            set { _avatarUrl = value; OnPropertyChanged(); }
        }

        public DateTime Created { get; set; } = DateTime.Now;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class ProfileService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VeloxBrowser");
            
        private static readonly string ProfilesFilePath = Path.Combine(AppDataFolder, "profiles.json");
        public static readonly string ProfilesDirectory = Path.Combine(AppDataFolder, "Profiles");

        private static List<BrowserProfile> _profiles = new List<BrowserProfile>();
        private static BrowserProfile? _currentProfile;

        public static void Initialize()
        {
            Directory.CreateDirectory(ProfilesDirectory);
            LoadProfiles();

            if (!_profiles.Any())
            {
                CreateProfile("Default Profile", "Default");
            }
            else
            {
                SetCurrentProfile(_profiles.First());
            }
        }

        private static void LoadProfiles()
        {
            try
            {
                if (File.Exists(ProfilesFilePath))
                {
                    var json = File.ReadAllText(ProfilesFilePath);
                    _profiles = JsonSerializer.Deserialize<List<BrowserProfile>>(json) ?? new List<BrowserProfile>();
                }
            }
            catch { _profiles = new List<BrowserProfile>(); }
        }

        private static void SaveProfiles()
        {
            try
            {
                var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProfilesFilePath, json);
            }
            catch { }
        }

        public static List<BrowserProfile> GetProfiles()
        {
            return _profiles;
        }

        public static BrowserProfile GetCurrentProfile()
        {
            if (_currentProfile == null) Initialize();
            return _currentProfile!;
        }

        public static void SetCurrentProfile(BrowserProfile profile)
        {
            if (_profiles.Any(p => p.Id == profile.Id))
            {
                _currentProfile = profile;
                foreach (var p in _profiles)
                {
                    p.IsCurrent = (p.Id == profile.Id);
                }
            }
        }

        public static BrowserProfile CreateProfile(string name, string folderName = "")
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                var invalidChars = Path.GetInvalidFileNameChars();
                folderName = new string(name.Where(ch => !invalidChars.Contains(ch)).ToArray()).Trim();
                if (string.IsNullOrWhiteSpace(folderName)) folderName = "Profile_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            var newProfile = new BrowserProfile
            {
                Name = name,
                FolderName = folderName
            };

            _profiles.Add(newProfile);
            SaveProfiles();
            
            // Create the directory immediately
            Directory.CreateDirectory(GetProfileFolderPath(newProfile));

            if (_currentProfile == null)
            {
                _currentProfile = newProfile;
            }

            return newProfile;
        }

        public static void UpdateProfileData(string id, string? name, string? email, string? avatarUrl)
        {
            var p = _profiles.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                if (!string.IsNullOrWhiteSpace(name)) p.Name = name;
                if (!string.IsNullOrWhiteSpace(email)) p.Email = email;
                if (!string.IsNullOrWhiteSpace(avatarUrl)) p.AvatarUrl = avatarUrl;
                SaveProfiles();
            }
        }

        public static void RenameProfile(string id, string newName)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == id);
            if (profile != null && !string.IsNullOrWhiteSpace(newName))
            {
                profile.Name = newName.Trim();
                SaveProfiles();
            }
        }

        public static void DeleteProfile(string id)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == id);
            if (profile != null)
            {
                _profiles.Remove(profile);
                SaveProfiles();
            }
        }

        public static string GetProfileFolderPath(BrowserProfile profile)
        {
            return Path.Combine(ProfilesDirectory, profile.FolderName);
        }
    }
}
