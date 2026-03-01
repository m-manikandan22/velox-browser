using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace VeloxBrowser
{
    public class TabItemViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _title = "New Tab";
        private System.Windows.Media.Imaging.BitmapImage? _faviconImage;
        private bool _isLoading;

        public string Title
        {
            get => _title;
            set { _title = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Title))); }
        }

        public System.Windows.Media.Imaging.BitmapImage? FaviconImage
        {
            get => _faviconImage;
            set { _faviconImage = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FaviconImage))); }
        }
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsLoading))); }
        }
        public WebView2 WebView { get; set; } = null!;
        public bool IsIncognito { get; set; } = false;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    public class AppSettings
    {
        public string SearchEngineQueryUrl { get; set; } = "https://www.google.com/search?q=";
        public string SearchEngineName { get; set; } = "Google";
        public string WallpaperUrl { get; set; } = "";
        public string Theme { get; set; } = "Light";
    }

    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty TabWidthProperty =
            DependencyProperty.Register("TabWidth", typeof(double), typeof(MainWindow), new PropertyMetadata(240.0));

        public double TabWidth
        {
            get => (double)GetValue(TabWidthProperty);
            set => SetValue(TabWidthProperty, value);
        }

        private static readonly string UserDataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VeloxBrowser");
        
        private static readonly string SettingsFile = Path.Combine(UserDataFolder, "settings.json");

        private ObservableCollection<TabItemViewModel> _tabs = new ObservableCollection<TabItemViewModel>();
        private CoreWebView2Environment? _webViewEnvironment;
        private AppSettings _settings = new AppSettings();
        public ObservableCollection<DownloadRecord> _downloads = new ObservableCollection<DownloadRecord>();

        public MainWindow()
        {
            InitializeComponent();
            TabStrip.ItemsSource = _tabs;
            Loaded += MainWindow_Loaded;
            SizeChanged += MainWindow_SizeChanged;
            LoadSettings();
            ApplyTheme(_settings.Theme);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ProfileService.Initialize();
                HistoryService.Initialize();

                var profiles = ProfileService.GetProfiles();
                if (profiles.Count > 1)
                {
                    // Show full-screen profile picker — hide chrome until profile is chosen
                    TitleBar.Visibility = Visibility.Collapsed;
                    ToolbarRow.Height = new GridLength(0);
                    ProgressRow.Height = new GridLength(0);
                    StatusBarRow.Height = new GridLength(0);
                    
                    if (!string.IsNullOrWhiteSpace(_settings.WallpaperUrl))
                    {
                        try
                        {
                            var uri = new Uri(_settings.WallpaperUrl.StartsWith("http") ? _settings.WallpaperUrl : _settings.WallpaperUrl);
                            if (uri.Scheme == "file" || _settings.WallpaperUrl.StartsWith("http://velox.assets"))
                            {
                                string localPath = _settings.WallpaperUrl.StartsWith("http://velox.assets") 
                                    ? Path.Combine(UserDataFolder, Path.GetFileName(_settings.WallpaperUrl))
                                    : uri.LocalPath;
                                
                                if (File.Exists(localPath))
                                {
                                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(localPath));
                                    this.Background = new System.Windows.Media.ImageBrush(bitmap) { Stretch = System.Windows.Media.Stretch.UniformToFill };
                                }
                            }
                        } catch { } // Fallback to transparent via window background
                    }
                    
                    StartupProfilesList.ItemsSource = profiles;
                    StartupProfileSelectorOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    // Single profile — just load it
                    ProfileService.SetCurrentProfile(profiles.First());
                    try { HistoryService.Initialize(); } catch { } // Pre-create per-profile DB
                    UpdateProfileButton();
                    await AddNewTab("about:blank");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("init_trace.txt", $"[{DateTime.Now}] FATAL ENVIRONMENT INIT TRACE:\n{ex}\n");
                MessageBox.Show($"Failed to initialize browser engine:\n\n{ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null) _settings = s;
                }
            }
            catch { /* fallback to defaults */ }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(UserDataFolder);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(_settings));
            }
            catch { }
        }

        private void ApplyTheme(string theme)
        {
            var res = Application.Current.Resources;
            try
            {
                void SetColor(string key, string hex)
                {
                    try 
                    {
                        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                        res[key] = new System.Windows.Media.SolidColorBrush(color);
                    } 
                    catch { }
                }

                if (theme == "Dark")
                {
                    SetColor("BackgroundBrush", "#1F1F1F");
                    SetColor("SurfaceBrush",    "#2C2C2C");
                    SetColor("BorderBrush",     "#3A3A3A");
                    SetColor("TextPrimaryBrush",   "#FFFFFF");
                    SetColor("TextSecondaryBrush", "#A0A0A0");
                    SetColor("AccentBrush",     "#60A5FA");
                }
                else
                {
                    SetColor("BackgroundBrush", "#FFFFFF");
                    SetColor("SurfaceBrush",    "#F3F4F6");
                    SetColor("BorderBrush",     "#E5E7EB");
                    SetColor("TextPrimaryBrush",   "#111827");
                    SetColor("TextSecondaryBrush", "#6B7280");
                    SetColor("AccentBrush",     "#2563EB");
                }

                // Also update all open WebViews so Google Search + right-click menu respect the theme
                ApplyWebViewTheme(theme);
            }
            catch { }
        }

        /// <summary>Sets WebView2 PreferredColorScheme on all tabs to match the app theme.</summary>
        private void ApplyWebViewTheme(string theme)
        {
            var scheme = theme == "Dark"
                ? Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark
                : Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Light;
            try
            {
                foreach (var tab in _tabs)
                {
                    try
                    {
                        if (tab.WebView?.CoreWebView2?.Profile != null)
                            tab.WebView.CoreWebView2.Profile.PreferredColorScheme = scheme;
                    }
                    catch { }
                }
            }
            catch { }
        }


        private async Task AddNewTab(string url = "", bool focus = true)
        {
            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            BrowserContainer.Children.Add(webView);

            // Wait for the native HWND to be fully allocated in WPF visual tree
            await webView.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

            if (_webViewEnvironment == null)
            {
                string profileFolder = ProfileService.GetProfileFolderPath(ProfileService.GetCurrentProfile());
                _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, profileFolder);
            }

            await webView.EnsureCoreWebView2Async(_webViewEnvironment);

            try
            {
                var settings = webView.CoreWebView2.Settings;
                settings.AreDevToolsEnabled = true;
                settings.AreDefaultContextMenusEnabled = true;
                settings.IsStatusBarEnabled = false;
                settings.IsSwipeNavigationEnabled = false;
                settings.IsPasswordAutosaveEnabled = true;
                settings.IsGeneralAutofillEnabled = true;
                
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "velox.assets", 
                    UserDataFolder, 
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                // Apply current theme to this WebView immediately
                var scheme = _settings.Theme == "Dark"
                    ? Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark
                    : Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Light;
                webView.CoreWebView2.Profile.PreferredColorScheme = scheme;
            }
            catch { }

            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.NavigationStarting += (s, e) => CoreWebView2_NavigationStarting(webView, e);
            webView.CoreWebView2.NavigationCompleted += (s, e) => CoreWebView2_NavigationCompleted(webView, e);
            webView.CoreWebView2.SourceChanged += (s, e) => CoreWebView2_SourceChanged(webView, e);
            webView.CoreWebView2.DocumentTitleChanged += (s, e) => CoreWebView2_DocumentTitleChanged(webView, e);
            webView.CoreWebView2.WebMessageReceived += (s, ev) => CoreWebView2_WebMessageReceived(webView, ev);
            webView.CoreWebView2.FaviconChanged += (s, e) => CoreWebView2_FaviconChanged(webView);
            
            webView.CoreWebView2.DownloadStarting += (s, ev) => 
            {
                var download = ev.DownloadOperation;
                var record = new DownloadRecord(download);

                Dispatcher.Invoke(() => _downloads.Add(record));

                download.BytesReceivedChanged += (_, __) =>
                {
                    if (download.TotalBytesToReceive > 0)
                    {
                        int pct = (int)((download.BytesReceived * 100) / (long)download.TotalBytesToReceive);
                        Dispatcher.Invoke(() => record.PercentComplete = pct);
                    }
                };

                download.StateChanged += (_, __) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        record.State = download.State.ToString();
                        if (download.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Completed)
                        {
                            StatusText.Text = "Download completed.";
                        }
                    });
                };
            };

            var tab = new TabItemViewModel { WebView = webView, Title = "New Tab" };
            _tabs.Add(tab);

            if (focus)
            {
                TabStrip.SelectedItem = tab;
            }

            UpdateWebViewVisibility();
            // Recalculate tab widths now that a tab was added
            _ = Dispatcher.InvokeAsync(UpdateTabWidths, System.Windows.Threading.DispatcherPriority.Loaded);

            if (string.IsNullOrEmpty(url) || url == "about:blank")
            {
                LoadNewTabPage(webView);
                // Auto-focus address bar so user can immediately type
                if (focus)
                    _ = Dispatcher.InvokeAsync(() => { AddressBar.Focus(); AddressBar.SelectAll(); },
                        System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                webView.CoreWebView2.Navigate(url);
            }
        }

        private void UpdateWebViewVisibility()
        {
            foreach (var t in _tabs)
            {
                if (t.WebView != null)
                {
                    t.WebView.Visibility = (TabStrip.SelectedItem == t) 
                        ? Visibility.Visible 
                        : Visibility.Collapsed;
                }
            }
        }

        private void LoadNewTabPage(WebView2 webView)
        {
            string wallpaperUri = "";
            try
            {
                if (!string.IsNullOrWhiteSpace(_settings.WallpaperUrl))
                {
                    if (_settings.WallpaperUrl.StartsWith("file:///"))
                    {
                        string fileName = Path.GetFileName(new Uri(_settings.WallpaperUrl).LocalPath);
                        wallpaperUri = $"http://velox.assets/{fileName}";
                    }
                    else
                    {
                        wallpaperUri = _settings.WallpaperUrl;
                    }
                }
            }
            catch { wallpaperUri = ""; }

            // Background: wallpaper or solid dark colour (like Chrome's dark NTP)
            string bodyCss = string.IsNullOrWhiteSpace(wallpaperUri)
                ? "background: #202124;"
                : $"background: url('{wallpaperUri}') no-repeat center center / cover;";

            string html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>New Tab</title>
<style>
  * {{ margin:0; padding:0; box-sizing:border-box; }}
  body {{
    {bodyCss}
    display:flex; flex-direction:column;
    align-items:center; justify-content:center;
    height:100vh;
    font-family:'Segoe UI',sans-serif;
    overflow:hidden;
  }}
  .brand {{
    font-size:52px; font-weight:700; letter-spacing:2px;
    color:#fff;
    text-shadow: 0 2px 24px rgba(0,0,0,0.55);
    margin-bottom:28px;
    user-select:none;
  }}
  .search-wrap {{
    display:flex; align-items:center;
    background:rgba(255,255,255,0.18);
    backdrop-filter:blur(14px) saturate(1.4);
    -webkit-backdrop-filter:blur(14px) saturate(1.4);
    border:1.5px solid rgba(255,255,255,0.38);
    border-radius:32px;
    padding:0 20px;
    width:580px; max-width:90vw;
    height:52px;
    box-shadow:0 4px 30px rgba(0,0,0,0.22);
    transition:background 0.2s, box-shadow 0.2s;
  }}
  .search-wrap:focus-within {{
    background:rgba(255,255,255,0.28);
    box-shadow:0 6px 40px rgba(0,0,0,0.32);
  }}
  .search-input {{
    flex:1; background:transparent; border:none; outline:none;
    font-size:16px; color:#fff;
    caret-color:#fff;
  }}
  .search-input::placeholder {{ color:rgba(255,255,255,0.65); }}
  .customize-btn {{
    position:fixed; bottom:22px; right:22px;
    padding:7px 16px;
    background:rgba(255,255,255,0.15);
    backdrop-filter:blur(8px);
    border:1px solid rgba(255,255,255,0.28);
    border-radius:20px; cursor:pointer;
    color:#fff; font-family:'Segoe UI'; font-size:12px;
    transition:background 0.2s;
  }}
  .customize-btn:hover {{ background:rgba(255,255,255,0.28); }}
</style>
</head>
<body>
  <div class='brand'>Velox</div>
  <form onsubmit='event.preventDefault(); window.chrome.webview.postMessage(""search:"" + document.getElementById(""q"").value);'>
    <div class='search-wrap'>
      <input class='search-input' type='text' id='q'
             placeholder='Search anything'
             autofocus autocomplete='off'/>
    </div>
  </form>
  <button class='customize-btn' onclick='window.chrome.webview.postMessage(""customize_wallpaper"")'>&#x1F58C; Customize</button>
</body>
</html>";

            webView.NavigateToString(html);
            AddressBar.Text = "";
        }


        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            if (!string.IsNullOrWhiteSpace(e.Uri))
            {
                _ = AddNewTab(e.Uri);
            }
        }

        private void CoreWebView2_WebMessageReceived(WebView2 webView, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (webView == null) return;
            string message = e.TryGetWebMessageAsString();
            
            if (message.StartsWith("search:"))
            {
                string query = message.Substring(7);
                Navigate(_settings.SearchEngineQueryUrl + Uri.EscapeDataString(query));
            }
            else if (message == "customize_wallpaper")
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp",
                    Title = "Select New Tab Wallpaper"
                };
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        string fileName = "wallpaper" + Path.GetExtension(dialog.FileName);
                        string destFile = Path.Combine(UserDataFolder, fileName);
                        File.Copy(dialog.FileName, destFile, true);
                        _settings.WallpaperUrl = $"http://velox.assets/{fileName}";
                        SaveSettings();
                        LoadNewTabPage(webView);
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Failed to set wallpaper: {ex.Message}", "about:blank");
                    }
                }
            }
        }

        private void CoreWebView2_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs e)
        {
            var tab = _tabs.FirstOrDefault(t => t.WebView == sender);
            if (tab == null) return;

            tab.IsLoading = true;
            
            if (TabStrip.SelectedItem == tab)
            {
                ErrorOverlay.Visibility = Visibility.Collapsed;
                LoadingProgress.Visibility = Visibility.Visible;
                StatusText.Text = "Loading\u2026";
                RefreshButton.Content = "\uE711";
                RefreshButton.ToolTip = "Stop (Escape)";
                
                // Don't show raw HTML string as address
                if (e.Uri != null && e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase)) 
                    AddressBar.Text = string.Empty;
                else 
                    AddressBar.Text = e.Uri;
                
                UpdateNavButtons();
            }
        }

        private void CoreWebView2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            var tab = _tabs.FirstOrDefault(t => t.WebView == sender);
            if (tab == null) return;

            tab.IsLoading = false;
            
            if (e.IsSuccess)
            {
                try
                {
                    string title = sender.CoreWebView2.DocumentTitle;
                    string url = sender.CoreWebView2.Source;
                    
                    // Do not log blank tabs to history
                    if (!url.StartsWith("data:text/html") && !string.IsNullOrWhiteSpace(url))
                    {
                        HistoryService.Add(string.IsNullOrWhiteSpace(title) ? "Unknown Page" : title, url);
                    }

                    // Auto-name profile from Google account if still on generic name
                    if (url.Contains("accounts.google.com") || url.Contains("myaccount.google.com") ||
                        url.Contains("mail.google.com") || url.Contains("google.com/u/"))
                    {
                        TryAutoNameProfileFromPage(sender);
                    }
                }
                catch { } // Ignore history logging errors
            }

            if (TabStrip.SelectedItem == tab)
            {
                LoadingProgress.Visibility = Visibility.Collapsed;
                RefreshButton.Content = "\uE72C";
                RefreshButton.ToolTip = "Refresh (F5)";
                UpdateNavButtons();

                if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    StatusText.Text = "Error loading page.";
                    ShowError($"Page could not be loaded. (Error: {e.WebErrorStatus})", sender.CoreWebView2?.Source ?? "Unknown URL");
                }
                else
                {
                    StatusText.Text = "Done";
                }
            }
            
            TabStrip.Items.Refresh(); // Force title to update if needed
        }

        private void CoreWebView2_SourceChanged(WebView2 sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel currentTab && currentTab.WebView == sender)
            {
                if (!currentTab.IsLoading)
                {
                    string src = sender.CoreWebView2?.Source ?? string.Empty;
                    if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        AddressBar.Text = string.Empty;
                    else
                        AddressBar.Text = src;
                }
                UpdateNavButtons();
            }
        }

        private void CoreWebView2_DocumentTitleChanged(WebView2 sender, object e)
        {
            var tab = _tabs.FirstOrDefault(t => t.WebView == sender);
            if (tab != null)
            {
                string title = sender.CoreWebView2.DocumentTitle;
                string src = sender.CoreWebView2?.Source ?? string.Empty;
                // Keep "New Tab" for our custom new-tab page
                if (string.IsNullOrWhiteSpace(title) || src.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
                    tab.Title = tab.IsIncognito ? "🕵 New Incognito Tab" : "New Tab";
                else
                    tab.Title = title;
            }
        }

        private async void CoreWebView2_FaviconChanged(WebView2 sender)
        {
            var tab = _tabs.FirstOrDefault(t => t.WebView == sender);
            if (tab == null) return;
            try
            {
                // Download the favicon as PNG bytes and create a BitmapImage
                using var stream = await sender.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
                if (stream == null) return;

                var ms = new System.IO.MemoryStream();
                await stream.CopyToAsync(ms);
                if (ms.Length == 0) return;
                ms.Position = 0;

                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        tab.FaviconImage = bmp;
                    }
                    catch { }
                });
            }
            catch { }
        }

        /// <summary>
        /// Recalculates tab widths whenever the tab area resizes — gives Chrome-like proportional shrinking.
        /// </summary>
        private void TabArea_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTabWidths();

        internal void UpdateTabWidths()
        {
            if (TabStrip == null || _tabs.Count == 0) return;

            // Give AddTab button enough space plus margins
            double addBtnW = AddTabButton.ActualWidth > 0 ? AddTabButton.ActualWidth + 10 : 42;
            double avail = Math.Max(0, TabAreaGrid.ActualWidth - addBtnW);

            // Constrain the ScrollViewer so the AddTab button is forced adjacent
            TabScrollViewer.MaxWidth = avail;

            // Calculate the proportional width (Chrome: min 48, max 240)
            double w = Math.Min(240.0, Math.Max(48.0, avail / _tabs.Count));

            // DP binding updates all XAML ListBoxItems instantly
            TabWidth = w;
        }


        private async void TryAutoNameProfileFromPage(WebView2 sender)
        {
            try
            {
                // Two separate simple JS calls — more reliable than JSON.stringify
                // ExecuteScriptAsync returns values as JSON strings (with outer double-quotes for strings)

                string? nameRaw = await sender.CoreWebView2.ExecuteScriptAsync(
                    @"(function() {
                        try {
                            // The most reliable place for the display name is the Google Account profile button tooltip
                            var el = document.querySelector('a[aria-label*=""Google Account""]');
                            if (el) {
                                var lbl = el.getAttribute('aria-label') || '';
                                if (lbl.includes('Google Account:')) {
                                    // Aria label format is typically: ""Google Account: First Last\n(email@gmail.com)""
                                    var parts = lbl.replace('Google Account:', '').split('\n');
                                    if (parts.length > 0) {
                                        return parts[0].trim(); // This is the pure Display Name
                                    }
                                }
                            }
                            // Fallback to title
                            var title = document.title || '';
                            if (title.includes('Google Account:')) {
                                var parts = title.split('-');
                                if (parts.length > 1) {
                                    var acc = parts[1].trim();
                                    return acc.replace('Google Account:', '').trim();
                                }
                            }
                        } catch(e) {}
                        return null;
                    })();");

                string? emailRaw = await sender.CoreWebView2.ExecuteScriptAsync(
                    @"(function() {
                        try {
                            var el = document.querySelector('[data-email]');
                            if (el) return el.getAttribute('data-email');
                            
                            var lbl = document.querySelector('a[aria-label*=""Google Account:""]');
                            if (lbl) {
                                var text = lbl.getAttribute('aria-label') || '';
                                var lines = text.split('\n');
                                for (var i = 0; i < lines.length; i++) {
                                    if (lines[i].includes('@')) return lines[i].trim();
                                }
                            }
                        } catch(e) {}
                        return null;
                    })();");

                string? imgRaw = await sender.CoreWebView2.ExecuteScriptAsync(
                    @"(function() {
                        try {
                            // Target Google account avatar explicitly
                            var selectors = [
                                'img[alt*=""Profile picture""]', 
                                'img[alt*=""profile picture""]',
                                'img[src*=""googleusercontent.com/a/""]', // Direct pattern for profile pics
                                '.gb_Cb img', // Top right corner avatar
                                'img.gb_Ac' 
                            ];
                            for (var selector of selectors) {
                                var img = document.querySelector(selector);
                                if (img && img.src && (img.src.includes('googleusercontent.com') || img.src.includes('ggpht.com'))) {
                                    // Make sure it's not a tiny icon by removing the size parameter or returning as-is
                                    var src = img.src;
                                    return src;
                                }
                            }
                            
                            // Fallback generic search
                            var imgs = document.querySelectorAll('img');
                            for (var i = 0; i < imgs.length; i++) {
                                var src = imgs[i].src || '';
                                if ((src.includes('googleusercontent.com') || src.includes('ggpht.com'))
                                    && imgs[i].width > 32) {
                                    return src;
                                }
                            }
                        } catch(e) {}
                        return null;
                    })();");

                // ExecuteScriptAsync returns strings as JSON: ""value"" — strip outer quotes
                string? name = (nameRaw != null && nameRaw != "null" && nameRaw.StartsWith("\""))
                    ? System.Text.Json.JsonSerializer.Deserialize<string>(nameRaw)
                    : null;

                string? imgUrl = (imgRaw != null && imgRaw != "null" && imgRaw.StartsWith("\""))
                    ? System.Text.Json.JsonSerializer.Deserialize<string>(imgRaw)
                    : null;
                string? email = (emailRaw != null && emailRaw != "null" && emailRaw.StartsWith("\""))
                    ? System.Text.Json.JsonSerializer.Deserialize<string>(emailRaw)
                    : null;

                if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(imgUrl) || !string.IsNullOrWhiteSpace(email))
                {
                    if (!string.IsNullOrWhiteSpace(name)) name = name.Trim();
                    if (!string.IsNullOrWhiteSpace(email)) email = email.Trim();
                    
                    var current = ProfileService.GetCurrentProfile();
                    
                    // Only use email as name if no real display name was found AND the profile has a generic name
                    // Don't clobber the Name with an email address
                    string? nameToSave = !string.IsNullOrWhiteSpace(name) ? name : null;

                    // Save the data to the profile system
                    ProfileService.UpdateProfileData(current.Id, nameToSave, email, imgUrl);

                    Dispatcher.Invoke(() =>
                    {
                        // Update the profile button avatar
                        if (!string.IsNullOrWhiteSpace(imgUrl))
                        {
                            try
                            {
                                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = new Uri(imgUrl);
                                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                
                                ProfileImageBrush.ImageSource = bmp;
                                ProfileImageBorder.Visibility = System.Windows.Visibility.Visible;
                                ProfileInitialsBorder.Visibility = System.Windows.Visibility.Collapsed;
                            }
                            catch { }
                        }
                        else if (!string.IsNullOrWhiteSpace(nameToSave))
                        {
                            ProfileImageBorder.Visibility = System.Windows.Visibility.Collapsed;
                            ProfileInitialsBorder.Visibility = System.Windows.Visibility.Visible;
                            ProfileButtonText.Text = nameToSave[0].ToString().ToUpper();
                        }
                    });
                }

            }
            catch { }
        }

        private void TabStrip_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
            else if (sender is ListBox listBox)
            {
                var sv = GetScrollViewer(listBox);
                if (sv != null)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
            }
        }

        private ScrollViewer? GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer scrollViewer) return scrollViewer;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void TabStrip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateWebViewVisibility();

            if (TabStrip.SelectedItem is TabItemViewModel activeTab)
            {
                // Sync UI state
                string src = activeTab.WebView.CoreWebView2?.Source ?? string.Empty;
                AddressBar.Text = src.StartsWith("data:text/html") ? string.Empty : src;
                
                if (activeTab.IsLoading)
                {
                    LoadingProgress.Visibility = Visibility.Visible;
                    RefreshButton.Content = "\uE711";
                }
                else
                {
                    LoadingProgress.Visibility = Visibility.Collapsed;
                    RefreshButton.Content = "\uE72C";
                }
                UpdateNavButtons();
            }
        }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: TabItemViewModel tab })
            {
                _tabs.Remove(tab);
                if (tab.WebView.Parent == BrowserContainer)
                    BrowserContainer.Children.Remove(tab.WebView);

                tab.WebView.Dispose();

                // Clean up incognito temp folder if this was an incognito tab
                if (tab.IsIncognito && _incognitoTempFolders.TryGetValue(tab.WebView, out string? tempDir))
                {
                    _incognitoTempFolders.Remove(tab.WebView);
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                }

                // Recalculate widths so remaining tabs expand again
                Dispatcher.InvokeAsync(UpdateTabWidths, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private async void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            await AddNewTab();
        }

        private async Task AddNewIncognitoTab(string url = "")
        {
            // Create a per-session temp folder — gone on OS restart or when we clean up
            string tempFolder = Path.Combine(Path.GetTempPath(), "VeloxIncognito_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            var incogEnv = await CoreWebView2Environment.CreateAsync(null, tempFolder);

            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            BrowserContainer.Children.Add(webView);
            await webView.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            await webView.EnsureCoreWebView2Async(incogEnv);

            try
            {
                var settings = webView.CoreWebView2.Settings;
                settings.AreDevToolsEnabled = false;
                settings.AreDefaultContextMenusEnabled = true;
                settings.IsStatusBarEnabled = false;
                settings.IsPasswordAutosaveEnabled = false;    // Never save in incognito
                settings.IsGeneralAutofillEnabled = false;

                // Apply current theme
                var scheme = _settings.Theme == "Dark"
                    ? Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark
                    : Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Light;
                webView.CoreWebView2.Profile.PreferredColorScheme = scheme;
            }
            catch { }

            // Wire same event handlers (no history logging for incognito)
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.NavigationStarting += (s, e) => CoreWebView2_NavigationStarting(webView, e);
            webView.CoreWebView2.NavigationCompleted += (s, e) => CoreWebView2_NavigationCompleted(webView, e);
            webView.CoreWebView2.SourceChanged += (s, e) => CoreWebView2_SourceChanged(webView, e);
            webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
            {
                var tab = _tabs.FirstOrDefault(t => t.WebView == webView);
                if (tab != null)
                {
                    string title = webView.CoreWebView2.DocumentTitle;
                    tab.Title = "🕵 " + (string.IsNullOrWhiteSpace(title) ? "Incognito" : title);
                }
            };
            webView.CoreWebView2.WebMessageReceived += (s, ev) => CoreWebView2_WebMessageReceived(webView, ev);

            var tab = new TabItemViewModel { WebView = webView, Title = "🕵 New Incognito Tab", IsIncognito = true };
            _tabs.Add(tab);
            TabStrip.SelectedItem = tab;
            UpdateWebViewVisibility();
            _ = Dispatcher.InvokeAsync(UpdateTabWidths, System.Windows.Threading.DispatcherPriority.Loaded);

            if (string.IsNullOrEmpty(url) || url == "about:blank")
            {
                // Navigate to a blank page with incognito banner
                webView.CoreWebView2.NavigateToString(@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><style>
* { margin:0; padding:0; box-sizing:border-box; }
body { background:#1a1a2e; color:#e0e0e0; font-family:'Segoe UI',sans-serif; 
       display:flex; flex-direction:column; align-items:center; justify-content:center; height:100vh; }
.icon { font-size:64px; margin-bottom:16px; }
h1 { font-size:28px; font-weight:600; margin-bottom:8px; }
p { font-size:14px; color:#999; max-width:480px; text-align:center; line-height:1.6; }
</style></head>
<body>
  <div class='icon'>🕵</div>
  <h1>Incognito Mode</h1>
  <p>Browsing history, cookies, form data and site data won't be saved after you close this tab.<br><br>Your activity is visible to websites you visit and your network.</p>
</body></html>");
                AddressBar.Text = string.Empty;
                _ = Dispatcher.InvokeAsync(() => { AddressBar.Focus(); AddressBar.SelectAll(); },
                    System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                webView.CoreWebView2.Navigate(url);
            }

            // Store temp folder on the tab for cleanup
            _incognitoTempFolders.Add(webView, tempFolder);
        }

        private readonly Dictionary<WebView2, string> _incognitoTempFolders = new();

        // --- Tab Drag-to-Reorder ---
        private TabItemViewModel? _draggedTab;
        private System.Windows.Point _tabDragStart;
        private bool _tabDragging;

        private void TabStrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _tabDragStart = e.GetPosition(TabStrip);
            _tabDragging = false;
            // Find which tab was hit
            var hit = System.Windows.Media.VisualTreeHelper.HitTest(TabStrip, _tabDragStart)?.VisualHit;
            while (hit != null && !(hit is ListBoxItem))
                hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
            if (hit is ListBoxItem item && item.DataContext is TabItemViewModel tab)
                _draggedTab = tab;
            else
                _draggedTab = null;
        }

        private void TabStrip_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedTab == null || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(TabStrip);
            if (!_tabDragging)
            {
                if (Math.Abs(pos.X - _tabDragStart.X) > SystemParameters.MinimumHorizontalDragDistance)
                    _tabDragging = true;
                else
                    return;
            }
            DragDrop.DoDragDrop(TabStrip, _draggedTab, DragDropEffects.Move);
        }

        private void TabStrip_Drop(object sender, DragEventArgs e)
        {
            if (_draggedTab == null) return;

            // Find drop target tab
            var pos = e.GetPosition(TabStrip);
            var hit = System.Windows.Media.VisualTreeHelper.HitTest(TabStrip, pos)?.VisualHit;
            while (hit != null && !(hit is ListBoxItem))
                hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);

            if (hit is ListBoxItem targetItem && targetItem.DataContext is TabItemViewModel targetTab
                && !ReferenceEquals(targetTab, _draggedTab))
            {
                int from = _tabs.IndexOf(_draggedTab);
                int to   = _tabs.IndexOf(targetTab);
                if (from >= 0 && to >= 0)
                {
                    _tabs.Move(from, to);
                    TabStrip.SelectedItem = _draggedTab;
                }
            }
            _draggedTab = null;
            _tabDragging = false;
        }

        private void Navigate(string url)
        {
            var activeTab = TabStrip.SelectedItem as TabItemViewModel;
            if (activeTab == null) activeTab = _tabs.FirstOrDefault(t => t.WebView.Visibility == Visibility.Visible);
            if (activeTab == null) return;

            url = NormalizeUrl(url);
            if (string.IsNullOrWhiteSpace(url)) return;

            AddressBar.Text = url;
            try
            {
                activeTab.WebView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                ShowError($"The engine blocked navigation to this internal URI.\n\n{ex.Message}", url);
            }
        }

        private string NormalizeUrl(string input)
        {
            input = input?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(input)) return string.Empty;

            if (input.StartsWith("edge://", StringComparison.OrdinalIgnoreCase))
                return input;

            // Explicitly ensure proper Uri formatting structure
            if (Uri.IsWellFormedUriString(input, UriKind.Absolute))
            {
                return input;
            }

            if (!input.Contains(" ") && input.Contains("."))
            {
                string httpsUri = "https://" + input;
                if (Uri.IsWellFormedUriString(httpsUri, UriKind.Absolute))
                    return httpsUri;
            }

            // Treat everything else as a search engine query
            return _settings.SearchEngineQueryUrl + Uri.EscapeDataString(input);
        }

        private void ShowError(string message, string url)
        {
            ErrorMessage.Text = $"{message}\n\nURL: {url}";
            ErrorOverlay.Visibility = Visibility.Visible;
            LoadingProgress.Visibility = Visibility.Collapsed;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Collapsed;
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorOverlay.Visibility = Visibility.Collapsed;
            if (TabStrip.SelectedItem is TabItemViewModel activeTab)
            {
                activeTab.WebView.Visibility = Visibility.Visible;
                activeTab.WebView.CoreWebView2.Reload();
            }
        }

        private void UpdateNavButtons()
        {
            if (TabStrip.SelectedItem is TabItemViewModel activeTab && activeTab.WebView.CoreWebView2 != null)
            {
                BackButton.IsEnabled = activeTab.WebView.CoreWebView2.CanGoBack;
                ForwardButton.IsEnabled = activeTab.WebView.CoreWebView2.CanGoForward;
            }
            else
            {
                BackButton.IsEnabled = false;
                ForwardButton.IsEnabled = false;
            }
        }

        // --- Navigation Toolbar Click Handlers ---

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t && t.WebView.CoreWebView2.CanGoBack)
                t.WebView.CoreWebView2.GoBack();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t && t.WebView.CoreWebView2.CanGoForward)
                t.WebView.CoreWebView2.GoForward();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t)
            {
                if (t.IsLoading) t.WebView.CoreWebView2.Stop();
                else t.WebView.CoreWebView2.Reload();
            }
        }

        private async void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t)
                LoadNewTabPage(t.WebView);
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            Navigate(AddressBar.Text);
        }

        // Tracks whether the last text change was a user keystroke vs. our own autocomplete
        private bool _suppressAutoComplete = false;

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                // If there's a selected (autocompleted) suffix, accept the full text
                string text = AddressBar.Text;
                Navigate(text);
                if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Focus();
            }
            else if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                _suppressAutoComplete = true;
                if (AddressBar.SelectionLength > 0 && e.Key == Key.Back)
                {
                    // If there's an autocompleted tail, Backspace should just delete that tail,
                    // leaving the carat at the end of the originally typed word.
                    e.Handled = true;
                    int start = AddressBar.SelectionStart;
                    AddressBar.Text = AddressBar.Text.Substring(0, start);
                    AddressBar.CaretIndex = start;
                }
            }
            else if (e.Key == Key.Escape)
            {
                // On Escape: strip the autocomplete tail (just keep typed portion)
                if (AddressBar.SelectionLength > 0)
                {
                    int caretPos = AddressBar.SelectionStart;
                    _suppressAutoComplete = true;
                    AddressBar.Text = AddressBar.Text.Substring(0, caretPos);
                    AddressBar.CaretIndex = AddressBar.Text.Length;
                }
            }
        }

        private void AddressBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressAutoComplete)
            {
                _suppressAutoComplete = false;
                return;
            }

            try
            {
                string typed = AddressBar.Text;
                // Only autocomplete when caret is at the exact end of the string
                if (AddressBar.SelectionStart != typed.Length || string.IsNullOrEmpty(typed) || typed.Length < 2)
                    return;

                var history = HistoryService.GetHistory();
                if (history == null || history.Count == 0) return;

                // Find a URL that starts exactly with typed text
                var match = history
                    .Where(h => !string.IsNullOrEmpty(h.Url) && h.Url.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(h => h.VisitTime)
                    .Select(h => h.Url)
                    .FirstOrDefault();

                if (match == null)
                {
                    // Fallback: match against domain name without scheme
                    match = history
                        .Where(h => !string.IsNullOrEmpty(h.Url))
                        .OrderByDescending(h => h.VisitTime)
                        .Select(h => h.Url)
                        .FirstOrDefault(url =>
                        {
                            string cleanUrl = url
                                .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                                .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                                .Replace("www.", "", StringComparison.OrdinalIgnoreCase);
                            return cleanUrl.StartsWith(typed, StringComparison.OrdinalIgnoreCase);
                        });
                }

                if (!string.IsNullOrEmpty(match) && match.Length > typed.Length)
                {
                    int matchIndex = match.IndexOf(typed, StringComparison.OrdinalIgnoreCase);
                    if (matchIndex >= 0)
                    {
                        _suppressAutoComplete = true;
                        int selectionStart = matchIndex + typed.Length;
                        if (selectionStart >= 0 && selectionStart <= match.Length)
                        {
                            AddressBar.Text = match;
                            AddressBar.Select(selectionStart, match.Length - selectionStart);
                        }
                    }
                }
            }
            catch { /* Silently ignore any autocomplete errors */ }
        }

        // These stubs satisfy XAML event bindings (popup no longer used, but XAML still references them)
        private void SuggestionList_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
        private void SuggestionList_KeyDown(object sender, KeyEventArgs e) { }

        private void AddressBar_GotFocus(object sender, RoutedEventArgs e) => AddressBar.SelectAll();

        private void AddressBar_LostFocus(object sender, RoutedEventArgs e)
        {
            _suppressAutoComplete = true;
            if (string.IsNullOrWhiteSpace(AddressBar.Text) && TabStrip.SelectedItem is TabItemViewModel t)
            {
                string src = t.WebView.CoreWebView2?.Source ?? string.Empty;
                AddressBar.Text = src.StartsWith("data:text/html") ? string.Empty : src;
            }
        }


        private void MenuButton_Click(object sender, RoutedEventArgs e) => MenuButton.ContextMenu.IsOpen = true;

        private void NewIncognitoTab_Click(object sender, RoutedEventArgs e) => _ = AddNewIncognitoTab();

        // --- Menu Item Handlers ---

        private void Downloads_Click(object sender, RoutedEventArgs e)
        {
            DownloadsList.ItemsSource = _downloads;
            DownloadsOverlay.Visibility = Visibility.Visible;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Collapsed;
        }

        private void CloseDownloads_Click(object sender, RoutedEventArgs e)
        {
            DownloadsOverlay.Visibility = Visibility.Collapsed;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Visible;
        }

        private void DownloadPause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DownloadRecord record)
                record.Pause();
        }

        private void DownloadResume_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DownloadRecord record)
                record.Resume();
        }

        private void DownloadCancel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DownloadRecord record)
                record.Cancel();
        }

        private void ShowBookmarks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BookmarksList.ItemsSource = BookmarkService.Load();
                BookmarksOverlay.Visibility = Visibility.Visible;
                if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open bookmarks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseBookmarks_Click(object sender, RoutedEventArgs e)
        {
            BookmarksOverlay.Visibility = Visibility.Collapsed;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Visible;
        }

        private void BookmarksList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BookmarksList.SelectedItem is Bookmark b)
            {
                CloseBookmarks_Click(sender, e);
                Navigate(b.Url);
            }
        }

        private void ShowProfiles_Click(object sender, RoutedEventArgs e)
        {
            ProfilesList.ItemsSource = ProfileService.GetProfiles();
            ProfilesOverlay.Visibility = Visibility.Visible;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Collapsed;
        }

        private void CloseProfiles_Click(object sender, RoutedEventArgs e)
        {
            ProfilesOverlay.Visibility = Visibility.Collapsed;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Visible;
        }

        private void NewCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            string param = e.Parameter as string ?? "";
            if (param == "incognito")
                _ = AddNewIncognitoTab();
            else
                _ = AddNewTab();
        }

        private void CloseCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel tab)
            {
                _tabs.Remove(tab);
                if (tab.WebView.Parent == BrowserContainer)
                    BrowserContainer.Children.Remove(tab.WebView);
                tab.WebView.Dispose();
                if (tab.IsIncognito && _incognitoTempFolders.TryGetValue(tab.WebView, out string? tempDir))
                {
                    _incognitoTempFolders.Remove(tab.WebView);
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                }
                Dispatcher.InvokeAsync(UpdateTabWidths, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new RenameDialog("New Profile", "Add Profile", "Create");
            dlg.Owner = this;
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
            {
                var newProfile = ProfileService.CreateProfile(dlg.Result);
                ProfilesList.ItemsSource = null;
                ProfilesList.ItemsSource = ProfileService.GetProfiles();
                
                // New profiles start at Google Sign-In (Chrome behavior)
                SwitchProfile(newProfile, isNewProfile: true);
            }
        }

        private void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BrowserProfile profile)
            {
                var dlg = new RenameDialog(profile.Name);
                dlg.Owner = this;
                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
                {
                    ProfileService.RenameProfile(profile.Id, dlg.Result);
                    ProfilesList.ItemsSource = null;
                    ProfilesList.ItemsSource = ProfileService.GetProfiles();
                }
            }
        }

        private void RemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BrowserProfile profile)
            {
                var current = ProfileService.GetCurrentProfile();
                if (profile.Id == current.Id)
                {
                    // STEP: Switch to the default profile first before deleting the active one
                    var fallback = ProfileService.GetProfiles().FirstOrDefault(p => p.Id != profile.Id) 
                                   ?? ProfileService.CreateProfile("Default Profile", "Default");
                    SwitchProfile(fallback);
                }

                var result = MessageBox.Show($"Are you sure you want to permanently delete the profile '{profile.Name}'?\n\nThis will remove all its history, downloads, and browsing data.", 
                                             "Delete Profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        string profileDir = Path.Combine(UserDataFolder, profile.Id);
                        if (Directory.Exists(profileDir))
                        {
                            // If it doesn't clean up instantly due to locked handles, the OS will clean it up later or we can GC.
                            Directory.Delete(profileDir, true);
                        }
                    }
                    catch { } // ignore lock errors

                    ProfileService.DeleteProfile(profile.Id);
                    ProfilesList.ItemsSource = null;
                    ProfilesList.ItemsSource = ProfileService.GetProfiles();
                    
                    if (StartupProfileSelectorOverlay.Visibility == Visibility.Visible)
                    {
                        StartupProfilesList.ItemsSource = null;
                        StartupProfilesList.ItemsSource = ProfileService.GetProfiles();
                        if (ProfileService.GetProfiles().Count == 0)
                        {
                            StartupProfileSelectorOverlay.Visibility = Visibility.Collapsed;
                            _ = AddNewTab("about:blank");
                        }
                    }
                }
            }
        }

        private void ProfilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProfilesList.SelectedItem is BrowserProfile p)
            {
                SwitchProfile(p);
            }
        }

        private void SwitchProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BrowserProfile p)
            {
                SwitchProfile(p);
            }
        }

        private void StartupAddProfile_Click(object sender, RoutedEventArgs e)
        {
            // Collapse the startup overlay, show chrome, then open the Profiles overlay
            StartupProfileSelectorOverlay.Visibility = Visibility.Collapsed;
            ShowBrowserChrome();
            // We need at least one tab for the browser to function during profile creation
            if (_tabs.Count == 0)
            {
                // Pick the first/only profile silently to bootstrap the WebView
                var fallback = ProfileService.GetProfiles().FirstOrDefault();
                if (fallback != null)
                {
                    ProfileService.SetCurrentProfile(fallback);
                    _ = AddNewTab("about:blank").ContinueWith(_ =>
                        Dispatcher.Invoke(() => ShowProfiles_Click(this, new RoutedEventArgs())));
                    return;
                }
            }
            ShowProfiles_Click(sender, e);
        }

        private void StartupProfileSelected_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BrowserProfile profile)
            {
                ProfileService.SetCurrentProfile(profile);
                StartupProfileSelectorOverlay.Visibility = Visibility.Collapsed;
                ShowBrowserChrome();
                UpdateProfileButton();
                _ = AddNewTab();
            }
        }

        private void ShowBrowserChrome()
        {
            this.Background = System.Windows.Media.Brushes.Transparent;
            TitleBar.Visibility = Visibility.Visible;
            ToolbarRow.Height = new GridLength(52);
            ProgressRow.Height = new GridLength(3);
            StatusBarRow.Height = new GridLength(24);
        }

        private async void SwitchProfile(BrowserProfile profile, bool isNewProfile = false)
        {
            this.IsEnabled = false; // Disable UI during switch
            try
            {
                // Set the new profile FIRST so GetProfileFolderPath returns the right path
                ProfileService.SetCurrentProfile(profile);
                
                // Update the profile button immediately to reflect the new profile
                UpdateProfileButton();
                
                // Close the profiles overlay so the UI doesn't hang
                CloseProfiles_Click(this, new RoutedEventArgs());

                // Dispose all current tabs first (STEP 2 - Dispose All WebViews)
                foreach (var tab in _tabs)
                {
                    try { tab.WebView.Dispose(); } catch { }
                }

                // Clear Collections
                _tabs.Clear();
                BrowserContainer.Children.Clear();
                
                // Clear the static environment reference
                _webViewEnvironment = null;

                // Force recreate environment with new profile path
                string profileFolder = ProfileService.GetProfileFolderPath(ProfileService.GetCurrentProfile());
                _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, profileFolder);
                
                // New profiles start on Google Sign-In to bind account to profile
                string startUrl = isNewProfile ? "https://accounts.google.com/signin" : string.Empty;
                try { HistoryService.Initialize(); } catch { } // Pre-create per-profile history DB
                await AddNewTab(startUrl);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            foreach (ComboBoxItem item in SearchEngineCombo.Items)
            {
                if (item.Tag.ToString() == _settings.SearchEngineQueryUrl)
                    SearchEngineCombo.SelectedItem = item;
            }
            foreach (ComboBoxItem item in ThemeCombo.Items)
            {
                if (item.Tag.ToString() == _settings.Theme)
                    ThemeCombo.SelectedItem = item;
            }
            SettingsOverlay.Visibility = Visibility.Visible;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Collapsed;
        }

        /// <summary>Refreshes the profile avatar button from the saved profile data.</summary>
        private void UpdateProfileButton()
        {
            var profile = ProfileService.GetCurrentProfile();
            if (profile == null) return;

            // Try to load avatar image (from Google CDN URL saved in profile)
            if (!string.IsNullOrWhiteSpace(profile.AvatarUrl))
            {
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(profile.AvatarUrl);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 56; // 28px * 2 for retina
                    bmp.EndInit();

                    ProfileImageBrush.ImageSource = bmp;
                    ProfileImageBorder.Visibility = Visibility.Visible;
                    ProfileInitialsBorder.Visibility = Visibility.Collapsed;
                    return;
                }
                catch { /* fall through to initials */ }
            }

            // Initials fallback: use display Name (not email)
            ProfileImageBorder.Visibility = Visibility.Collapsed;
            ProfileInitialsBorder.Visibility = Visibility.Visible;
            string displayName = profile.Name;
            ProfileButtonText.Text = string.IsNullOrWhiteSpace(displayName)
                ? "P"
                : displayName.Trim()[0].ToString().ToUpper();
        }

        private void NewWindow_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new MainWindow();
            newWindow.Show();
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HistoryService.Initialize(); // Ensure per-profile DB table exists
                HistoryList.ItemsSource = HistoryService.GetHistory();
                HistoryOverlay.Visibility = Visibility.Visible;
                if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseHistory_Click(object sender, RoutedEventArgs e)
        {
            HistoryOverlay.Visibility = Visibility.Collapsed;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Visible;
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MessageBox.Show("Are you sure you want to clear all history?", "Clear History", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    HistoryService.ClearHistory();
                    HistoryList.ItemsSource = HistoryService.GetHistory();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not clear history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HistoryList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (HistoryList.SelectedItem is HistoryRecord record)
                {
                    CloseHistory_Click(sender, e);
                    Navigate(record.Url);
                }
            }
            catch { }
        }
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.ZoomFactor += 0.25;
        }
        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.ZoomFactor -= 0.25;
        }
        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.ZoomFactor = 1.0;
        }
        
        private void AddBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel tab && tab.WebView.CoreWebView2 != null)
            {
                string url = tab.WebView.CoreWebView2.Source;
                if (url.StartsWith("data:text/html") || string.IsNullOrWhiteSpace(url)) return;
                
                try
                {
                    var bookmarks = BookmarkService.Load();
                    bookmarks.Add(new Bookmark
                    {
                        Title = string.IsNullOrWhiteSpace(tab.Title) ? "Saved Page" : tab.Title,
                        Url = url
                    });
                    
                    BookmarkService.Save(bookmarks);
                    StatusText.Text = "Bookmarked.";
                }
                catch { StatusText.Text = "Failed to bookmark page."; }
            }
        }
        
        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t) await t.WebView.CoreWebView2.ExecuteScriptAsync("window.print();");
        }
        private void Find_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t && t.WebView.CoreWebView2 != null)
            {
                // WebView2 natively handles Ctrl+F to open its find bar when the WebView has focus
                t.WebView.Focus();
                // Simulate Ctrl+F inside the WebView via script fallback
                _ = t.WebView.CoreWebView2.ExecuteScriptAsync(
                    "window.find && window.find('');");
            }
        }
        private void DevTools_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.CoreWebView2.OpenDevToolsWindow();
        }
        private void Help_Click(object sender, RoutedEventArgs e) => Navigate("https://support.microsoft.com/en-us/microsoft-edge");

        private async void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            if (TabStrip.SelectedItem is TabItemViewModel t && t.WebView.CoreWebView2 != null)
            {
                try
                {
                    await t.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync();
                    StatusText.Text = "Cache cleared.";
                }
                catch { StatusText.Text = "Could not clear cache."; }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        // --- Settings Overlay Handlers ---
        
        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
            if (TabStrip.SelectedItem is TabItemViewModel t) t.WebView.Visibility = Visibility.Visible;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (SearchEngineCombo.SelectedItem is ComboBoxItem engineItem)
            {
                _settings.SearchEngineQueryUrl = engineItem.Tag.ToString() ?? "";
                _settings.SearchEngineName = engineItem.Content.ToString() ?? "";
            }
            if (ThemeCombo.SelectedItem is ComboBoxItem themeItem)
            {
                _settings.Theme = themeItem.Tag.ToString() ?? "Light";
                ApplyTheme(_settings.Theme);
            }
            
            SaveSettings();
            SettingsOverlay.Visibility = Visibility.Collapsed;
            StatusText.Text = "Settings saved.";
        }

        // --- Window Controls ---
        
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        
        private Rect _normalBounds;
        private bool _isCustomMaximized = false;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCustomMaximized)
            {
                // Save normal window size
                _normalBounds = new Rect(Left, Top, Width, Height);
                
                // Maximize taking Taskbar into account
                Left = SystemParameters.WorkArea.Left;
                Top = SystemParameters.WorkArea.Top;
                Width = SystemParameters.WorkArea.Width;
                Height = SystemParameters.WorkArea.Height;
                _isCustomMaximized = true;
            }
            else
            {
                // Restore
                Left = _normalBounds.Left;
                Top = _normalBounds.Top;
                Width = _normalBounds.Width;
                Height = _normalBounds.Height;
                _isCustomMaximized = false;
            }
            UpdateMaximizeIcon();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateMaximizeIcon();

        private void UpdateMaximizeIcon()
        {
            MaximizeButton.Content = _isCustomMaximized ? "\uE923" : "\uE922";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }
        
        private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }
        
        private void TitleBar_MouseMove(object sender, MouseEventArgs e) { }



        // --- Keyboard Shortcuts ---

        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;

            if (e.Key == Key.T && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                await AddNewTab();
            }
            else if (e.Key == Key.N && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                NewWindow_Click(sender, e);
            }
            else if (e.Key == Key.L && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                AddressBar.Focus();
                AddressBar.SelectAll();
            }
            else if (e.Key == Key.H && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                History_Click(sender, e);
            }
            else if (e.Key == Key.J && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                Downloads_Click(sender, e);
            }
            else if (e.Key == Key.B && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                ShowBookmarks_Click(sender, e);
            }
            else if (e.Key == Key.D && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                AddBookmark_Click(sender, e);
            }
            else if (e.Key == Key.P && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                Print_Click(sender, e);
            }
            else if (e.Key == Key.F && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                Find_Click(sender, e);
            }
            else if (e.Key == Key.I && mods == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                e.Handled = true;
                DevTools_Click(sender, e);
            }
            else if (e.Key == Key.Left && mods == ModifierKeys.Alt)
            {
                e.Handled = true;
                BackButton_Click(sender, e);
            }
            else if (e.Key == Key.Right && mods == ModifierKeys.Alt)
            {
                e.Handled = true;
                ForwardButton_Click(sender, e);
            }
            else if (e.Key == Key.F5)
            {
                e.Handled = true;
                RefreshButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                if (TabStrip.SelectedItem is TabItemViewModel t && t.IsLoading)
                {
                    e.Handled = true;
                    t.WebView.CoreWebView2.Stop();
                }
            }
            else if ((e.Key == Key.OemPlus || e.Key == Key.Add) && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                ZoomIn_Click(sender, e);
            }
            else if ((e.Key == Key.OemMinus || e.Key == Key.Subtract) && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                ZoomOut_Click(sender, e);
            }
            else if (e.Key == Key.D0 && mods == ModifierKeys.Control)
            {
                e.Handled = true;
                ZoomReset_Click(sender, e);
            }
        }

        // --- Cleanup ---

        protected override void OnClosed(EventArgs e)
        {
            foreach (var tab in _tabs)
            {
                tab.WebView.Dispose();
            }
            base.OnClosed(e);
        }
    }

    // --- Data Models ---
    public class DownloadRecord : INotifyPropertyChanged
    {
        private int _percent;
        private string _state = "";
        private Microsoft.Web.WebView2.Core.CoreWebView2DownloadOperation? _operation;

        public DownloadRecord(Microsoft.Web.WebView2.Core.CoreWebView2DownloadOperation operation)
        {
            _operation = operation;
            FileName = System.IO.Path.GetFileName(operation.ResultFilePath) ?? "Unknown";
            PercentComplete = 0;
            State = "Downloading";
        }

        public string FileName { get; set; } = "";
        
        public int PercentComplete 
        { 
            get => _percent; 
            set { _percent = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PercentComplete))); }
        }
        
        public string State 
        { 
            get => _state; 
            set 
            { 
                _state = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State))); 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanPause))); 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanResume))); 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanCancel))); 
            }
        }

        public bool CanPause => _operation != null && _operation.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.InProgress;
        public bool CanResume => _operation != null && _operation.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Interrupted;
        public bool CanCancel => _operation != null && (_operation.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.InProgress || _operation.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Interrupted);

        public void Pause() { try { _operation?.Pause(); } catch { } }
        public void Resume() { try { _operation?.Resume(); } catch { } }
        public void Cancel() { try { _operation?.Cancel(); } catch { } }
        
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
