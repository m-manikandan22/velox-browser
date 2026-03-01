# Velox Browser 🚀

Velox Browser is a modern, lightweight, and fast web browser built for Windows using WPF and Microsoft's WebView2 engine. It focuses on a clean user interface, robust profile management, and a seamless browsing experience.

![Velox Browser Logo](velox-logo.png)

## ✨ Core Features

### 👤 Profile Management
- **Chrome-style Profiles:** Truly isolated environments for each profile.
- **Account Integration:** New profiles start directly at Google Sign-In to seamlessly link your account.
- **Per-Profile Data:** History, bookmarks, and sessions are kept completely separate per profile.
- **Profile Customization:** Profiles automatically pull your Google avatar directly from the logged-in account, falling back to initials if an avatar isn't available. You can easily rename or delete profiles.

### 🕵️‍♂️ Browsing Modes
- **Standard Tabs:** Fast and secure browsing powered by Edge/Chromium (WebView2).
- **Incognito Mode:** Launch isolated incognito tabs that leave no trace. History, passwords, and autofill are strictly disabled, and all temporary data is destroyed immediately when the tab closes.

### 📑 Advanced Tab Management
- **Fluid Reordering:** Click and drag tabs to visually reorder them.
- **Context Menus:** Right-click tabs to perform quick actions like "Close Other Tabs".

### 🎨 Customization & Theming
- **Dynamic Theming:** Switch instantly between Light and Dark mode. *The browser's UI and even web content (via `prefers-color-scheme`) adapt in real-time.*
- **Custom New Tab Page:** Features a sleek default design with a blurred search bar and the ability to customize your own background wallpaper.

### 🛠 Built-in Tools
- **History Manager:** Browse your past visits.
- **Bookmark Manager:** Save and organize your favorite sites.
- **Downloads Manager:** Track active and completed file downloads.

### ⌨️ Keyboard Shortcuts
Power users can navigate quickly using standard browser shortcuts:
- `Ctrl + T`: New Tab
- `Ctrl + Shift + N`: New Incognito Tab (via Menu)
- `Ctrl + W`: Close Current Tab
- `Ctrl + H`: Open History
- `Ctrl + J`: Open Downloads
- `Ctrl + D`: Bookmark Current Page
- `Ctrl + L`: Focus Address Bar
- `Ctrl + + / -`: Zoom In / Out
- `Ctrl + 0`: Reset Zoom
- `F5`: Reload page

---

## 🏗 Technical Architecture

Velox Browser is built on a modern Windows desktop stack:

- **UI Framework:** WPF (Windows Presentation Foundation) with XAML.
- **Browser Engine:** `Microsoft.Web.WebView2` (Chromium-based).
- **Database:** `Microsoft.Data.Sqlite` for fast, local history storage.
- **Target Framework:** .NET 10.0 (Windows).

### Data Storage Architecture
User data is stored in your local AppData folder (`%LOCALAPPDATA%\VeloxBrowser`):
- `Profiles/`: Contains individual profile folders.
  - `[ProfileName]/WebView2Data/`: The isolated Chromium cache and session data.
  - `[ProfileName]/history.db`: The profile's SQLite history database.
  - `[ProfileName]/bookmarks.json`: The profile's saved bookmarks.
- `settings.json`: Global browser settings (Theme, Default Search Engine, etc.).

---

## 🚀 Building from Source

### Prerequisites
- Windows 10 or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### How to Build (Self-Contained EXE)
You can easily build a standalone `.exe` that will work on *any* Windows PC without requiring the user to install the .NET runtime.

1. Open File Explorer and navigate to the project directory.
2. Double-click the included `build.bat` file.
3. The build process will take a few minutes (it downloads the Windows runtime).
4. Upon success, look inside the generated `publish_standalone` folder.
5. You can now share `VeloxBrowser.exe` with anyone!

*(Note: Ensure `velox-logo.png` and `velox-logo.ico` are in the project root before building for the icon to be embedded correctly).*
