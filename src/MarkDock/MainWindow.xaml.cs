using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Net;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MarkDock
{
    public partial class MainWindow : Window
    {
        // Demo-Bookmarks (fallback)
        private static readonly Bookmark[] _demoBookmarks =
        {
            new Bookmark { Title = "Google",   Url = "https://www.google.com", Browser = "Demo" },
            new Bookmark { Title = "Bing",     Url = "https://www.bing.com", Browser = "Demo" },
            new Bookmark { Title = "DuckDuckGo", Url = "https://duckduckgo.com", Browser = "Demo" },
            new Bookmark { Title = "Stack Overflow", Url = "https://stackoverflow.com", Browser = "Demo" },
            new Bookmark { Title = "GitHub",   Url = "https://github.com", Browser = "Demo" }
        };

        // Alle aktuellen Bookmarks (Source for filtering / display)
        private readonly List<Bookmark> _allBookmarks = new();

        // Die aktuell gefilterte Liste (verknüpft mit ListView)
        private readonly ObservableCollection<Bookmark> _displayedBookmarks = new();

        // 0 = alle anzeigen, 1 = nur tote, 2 = nur OK
        private int _statusFilterState = 0;

        // Merkt sich, ob der letzte Rechtsklick tatsächlich auf einer Bookmark-Zeile war
        private bool _rightClickOnItem = false;

        // Namen der Ordner, die der Nutzer selbst erstellt/umbenannt hat
        private readonly HashSet<string> _customFolderNames = new();

        // Referenz auf die CollectionView für Sortierung per Spaltenklick
        private System.ComponentModel.ICollectionView _bookmarksView;
        private bool _folderSortAscending = true;
        private bool _titleSortAscending = true;

        // Aktueller Theme-Zustand (hell/dunkel)
        private bool _isDarkTheme = false;

        public MainWindow()
        {
            InitializeComponent();
            BookmarksListView.ItemsSource = _displayedBookmarks;

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_displayedBookmarks);
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("EffectiveGroup"));
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription("FolderSortKey", System.ComponentModel.ListSortDirection.Ascending));
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription("IsFavorite", System.ComponentModel.ListSortDirection.Descending));
            _bookmarksView = view;
        }

        #region Schnellstart-Launcher (globaler Hotkey Strg+Leertaste)

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint VK_SPACE = 0x20;
        private const int WM_HOTKEY = 0x0312;

        private uint _currentHotkeyModifiers = MOD_CONTROL;
        private uint _currentHotkeyVk = VK_SPACE;

        private Window _launcherWindow;
        private TextBox _launcherSearchBox;
        private ListBox _launcherResultsList;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var (savedMod, savedVk) = LoadHotkeySetting();
                _currentHotkeyModifiers = savedMod;
                _currentHotkeyVk = savedVk;

                var helper = new WindowInteropHelper(this);
                RegisterHotKey(helper.Handle, HOTKEY_ID, _currentHotkeyModifiers, _currentHotkeyVk);
                HwndSource source = HwndSource.FromHwnd(helper.Handle);
                source?.AddHook(HwndHook);
            }
            catch
            {
                // Globaler Hotkey konnte nicht registriert werden (z. B. bereits belegt) – kein Crash,
                // der Launcher bleibt einfach nur über den Button erreichbar
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
            }
            catch
            {
                // Kein Problem, wenn das Abmelden fehlschlägt – Prozess beendet sich ohnehin
            }
            base.OnClosed(e);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ShowQuickLauncher();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void ShowQuickLauncher()
        {
            if (_launcherWindow != null)
            {
                _launcherWindow.Show();
                _launcherWindow.Activate();
                _launcherSearchBox.Text = "";
                _launcherSearchBox.Focus();
                UpdateLauncherResults();
                return;
            }

            _launcherWindow = new Window
            {
                Width = 500,
                Height = 400,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                ShowInTaskbar = false,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var dockPanel = new DockPanel { Margin = new Thickness(10) };

            _launcherSearchBox = new TextBox
            {
                Height = 36,
                FontSize = 16,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_launcherSearchBox, Dock.Top);

            _launcherResultsList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 10, 0, 0),
                DisplayMemberPath = "Title"
            };

            dockPanel.Children.Add(_launcherSearchBox);
            dockPanel.Children.Add(_launcherResultsList);
            _launcherWindow.Content = dockPanel;

            _launcherSearchBox.TextChanged += (s, args) => UpdateLauncherResults();
            _launcherSearchBox.PreviewKeyDown += LauncherSearchBox_PreviewKeyDown;
            _launcherResultsList.MouseDoubleClick += (s, args) => OpenSelectedLauncherResult();
            _launcherWindow.Deactivated += (s, args) => _launcherWindow.Hide();

            _launcherWindow.Show();
            _launcherSearchBox.Focus();
            UpdateLauncherResults();
        }

        private void UpdateLauncherResults()
        {
            string filter = _launcherSearchBox.Text.Trim().ToLowerInvariant();

            var results = string.IsNullOrEmpty(filter)
                ? _allBookmarks.Take(8).ToList()
                : _allBookmarks.Where(b =>
                    b.Title.ToLowerInvariant().Contains(filter) ||
                    b.Url.ToLowerInvariant().Contains(filter)).Take(8).ToList();

            _launcherResultsList.ItemsSource = results;
            if (results.Count > 0)
                _launcherResultsList.SelectedIndex = 0;
        }

        private void LauncherSearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                _launcherWindow.Hide();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Enter)
            {
                OpenSelectedLauncherResult();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Down)
            {
                if (_launcherResultsList.SelectedIndex < _launcherResultsList.Items.Count - 1)
                    _launcherResultsList.SelectedIndex++;
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Up)
            {
                if (_launcherResultsList.SelectedIndex > 0)
                    _launcherResultsList.SelectedIndex--;
                e.Handled = true;
            }
        }

        private void OpenSelectedLauncherResult()
        {
            if (_launcherResultsList.SelectedItem is Bookmark bm)
            {
                OpenUrlInBrowser(bm.Url);
                _launcherWindow.Hide();
            }
        }

        private void LauncherButton_Click(object sender, RoutedEventArgs e)
        {
            ShowQuickLauncher();
        }

        private (uint modifiers, uint vk) LoadHotkeySetting()
        {
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using (var cmd = new SqliteCommand("CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT);", connection))
                cmd.ExecuteNonQuery();

            uint mod = MOD_CONTROL;
            uint vk = VK_SPACE;

            using (var cmd = new SqliteCommand("SELECT Value FROM Settings WHERE Key = 'HotkeyModifiers';", connection))
            {
                var result = cmd.ExecuteScalar();
                if (result != null && uint.TryParse(result.ToString(), out uint parsedMod))
                    mod = parsedMod;
            }
            using (var cmd = new SqliteCommand("SELECT Value FROM Settings WHERE Key = 'HotkeyVk';", connection))
            {
                var result = cmd.ExecuteScalar();
                if (result != null && uint.TryParse(result.ToString(), out uint parsedVk))
                    vk = parsedVk;
            }

            return (mod, vk);
        }

        private void SaveHotkeySetting(uint modifiers, uint vk)
        {
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using (var cmd = new SqliteCommand("CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT);", connection))
                cmd.ExecuteNonQuery();

            using (var cmd = new SqliteCommand("INSERT INTO Settings (Key, Value) VALUES ('HotkeyModifiers', @val) ON CONFLICT(Key) DO UPDATE SET Value = @val;", connection))
            {
                cmd.Parameters.AddWithValue("@val", modifiers.ToString());
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new SqliteCommand("INSERT INTO Settings (Key, Value) VALUES ('HotkeyVk', @val) ON CONFLICT(Key) DO UPDATE SET Value = @val;", connection))
            {
                cmd.Parameters.AddWithValue("@val", vk.ToString());
                cmd.ExecuteNonQuery();
            }
        }

        private bool IsModifierKey(System.Windows.Input.Key key)
        {
            return key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                   key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                   key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                   key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin;
        }

        private string FormatHotkeyDisplay(uint modifiers, System.Windows.Input.Key key)
        {
            var parts = new List<string>();
            if ((modifiers & MOD_CONTROL) != 0) parts.Add("Strg");
            if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & MOD_SHIFT) != 0) parts.Add("Umschalt");
            if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }

        private void ChangeHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            var currentKey = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)_currentHotkeyVk);
            string currentDisplay = FormatHotkeyDisplay(_currentHotkeyModifiers, currentKey);

            var dialog = new Window
            {
                Title = "Tastenkombination ändern",
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(15) };
            var label = new TextBlock
            {
                Text = "Neue Tastenkombination im Feld drücken (mind. eine Zusatztaste wie Strg/Alt/Umschalt/Win):",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var captureBox = new TextBox
            {
                Text = currentDisplay,
                IsReadOnly = true,
                Height = 32,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            uint newMod = 0;
            uint newVk = 0;
            string newDisplay = "";

            captureBox.PreviewKeyDown += (s, args) =>
            {
                args.Handled = true;
                var key = args.Key == System.Windows.Input.Key.System ? args.SystemKey : args.Key;
                if (IsModifierKey(key))
                    return;

                uint modifiers = 0;
                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
                    modifiers |= MOD_CONTROL;
                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
                    modifiers |= MOD_ALT;
                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                    modifiers |= MOD_SHIFT;
                if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows))
                    modifiers |= MOD_WIN;

                if (modifiers == 0)
                    return;

                newMod = modifiers;
                newVk = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
                newDisplay = FormatHotkeyDisplay(modifiers, key);
                captureBox.Text = newDisplay;
            };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            var saveButton = new Button { Content = "Speichern", Width = 90, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Abbrechen", Width = 90 };

            bool? dialogResult = null;
            saveButton.Click += (s, args) => { dialogResult = true; dialog.Close(); };
            cancelButton.Click += (s, args) => { dialogResult = false; dialog.Close(); };

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(label);
            stack.Children.Add(captureBox);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;

            dialog.ShowDialog();

            if (dialogResult == true && newVk != 0)
            {
                var helper = new WindowInteropHelper(this);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
                bool success = RegisterHotKey(helper.Handle, HOTKEY_ID, newMod, newVk);

                if (success)
                {
                    _currentHotkeyModifiers = newMod;
                    _currentHotkeyVk = newVk;
                    SaveHotkeySetting(newMod, newVk);
                    MessageBox.Show($"Neue Tastenkombination gespeichert: {newDisplay}", "Hotkey geändert", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Fehlgeschlagen (z. B. schon von einem anderen Programm belegt) – alte Kombination wiederherstellen
                    RegisterHotKey(helper.Handle, HOTKEY_ID, _currentHotkeyModifiers, _currentHotkeyVk);
                    MessageBox.Show("Diese Tastenkombination konnte nicht registriert werden (vermutlich bereits von einem anderen Programm belegt). Die bisherige Kombination bleibt aktiv.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        #endregion

        private void MainWindow_Loaded(object sender, EventArgs e)
        {
            StatusText.Text = "Importiere Bookmarks...";

            // Erzwingt einen UI-Repaint, damit der Text sichtbar wird, bevor die blockierende Arbeit beginnt
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            AttemptImportAndLoad();
        }

        /// <summary>
        /// Beim Start: Browser-Bookmarks importieren, sonst Demo-Daten laden.
        /// </summary>
        private void AttemptImportAndLoad()
        {
            // 1. Datenbank initialisieren
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            // 2. Migration prüfen und ggf. alte Tabelle löschen
            bool needsMigration = false;
            try
            {
                using (var infoCmd = new SqliteCommand("PRAGMA table_info(Bookmarks);", connection))
                using (SqliteDataReader reader = infoCmd.ExecuteReader())
                {
                    bool hasUrlKey = false;
                    while (reader.Read())
                    {
                        if (reader.GetString(reader.GetOrdinal("name")) == "UrlKey")
                        {
                            hasUrlKey = true;
                            break;
                        }
                    }
                    needsMigration = !hasUrlKey;
                }
            }
            catch
            {
                // Tabelle existiert nicht → kein Migration nötig
            }

            if (needsMigration)
            {
                using (var dropCmd = new SqliteCommand("DROP TABLE IF EXISTS Bookmarks;", connection))
                {
                    dropCmd.ExecuteNonQuery();
                }
            }

            // 2b. Zusätzliche, nicht-destruktive Migration: IsFavorite-Spalte ergänzen, falls sie fehlt
            try
            {
                using (var infoCmd2 = new SqliteCommand("PRAGMA table_info(Bookmarks);", connection))
                using (SqliteDataReader reader2 = infoCmd2.ExecuteReader())
                {
                    bool hasIsFavorite = false;
                    while (reader2.Read())
                    {
                        if (reader2.GetString(reader2.GetOrdinal("name")) == "IsFavorite")
                        {
                            hasIsFavorite = true;
                            break;
                        }
                    }
                    if (!hasIsFavorite)
                    {
                        using (var alterCmd = new SqliteCommand("ALTER TABLE Bookmarks ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;", connection))
                        {
                            alterCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {
                // Tabelle existiert noch nicht (Erstinstallation) → kein Migration nötig, CREATE TABLE übernimmt das gleich
            }

            // 2c. Zusätzliche, nicht-destruktive Migration: Browser-Spalte ergänzen, falls sie fehlt
            try
            {
                using (var infoCmd3 = new SqliteCommand("PRAGMA table_info(Bookmarks);", connection))
                using (SqliteDataReader reader3 = infoCmd3.ExecuteReader())
                {
                    bool hasBrowser = false;
                    while (reader3.Read())
                    {
                        if (reader3.GetString(reader3.GetOrdinal("name")) == "Browser")
                        {
                            hasBrowser = true;
                            break;
                        }
                    }
                    if (!hasBrowser)
                    {
                        using (var alterCmd = new SqliteCommand("ALTER TABLE Bookmarks ADD COLUMN Browser TEXT NOT NULL DEFAULT '';", connection))
                        {
                            alterCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {
                // Tabelle existiert noch nicht (Erstinstallation) → kein Migration nötig, CREATE TABLE übernimmt das gleich
            }

            // 2d. Zusätzliche, nicht-destruktive Migration: Folder-Spalte ergänzen, falls sie fehlt
            try
            {
                using (var infoCmd4 = new SqliteCommand("PRAGMA table_info(Bookmarks);", connection))
                using (SqliteDataReader reader4 = infoCmd4.ExecuteReader())
                {
                    bool hasFolder = false;
                    while (reader4.Read())
                    {
                        if (reader4.GetString(reader4.GetOrdinal("name")) == "Folder")
                        {
                            hasFolder = true;
                            break;
                        }
                    }
                    if (!hasFolder)
                    {
                        using (var alterCmd = new SqliteCommand("ALTER TABLE Bookmarks ADD COLUMN Folder TEXT NOT NULL DEFAULT '';", connection))
                        {
                            alterCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {
                // Tabelle existiert noch nicht (Erstinstallation) → kein Migration nötig, CREATE TABLE übernimmt das gleich
            }

            // 2e. Zusätzliche, nicht-destruktive Migration: IsDead-Spalte ergänzen, falls sie fehlt
            try
            {
                using (var infoCmd5 = new SqliteCommand("PRAGMA table_info(Bookmarks);", connection))
                using (SqliteDataReader reader5 = infoCmd5.ExecuteReader())
                {
                    bool hasIsDead = false;
                    while (reader5.Read())
                    {
                        if (reader5.GetString(reader5.GetOrdinal("name")) == "IsDead")
                        {
                            hasIsDead = true;
                            break;
                        }
                    }
                    if (!hasIsDead)
                    {
                        using (var alterCmd = new SqliteCommand("ALTER TABLE Bookmarks ADD COLUMN IsDead INTEGER;", connection))
                        {
                            alterCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch
            {
                // Tabelle existiert noch nicht (Erstinstallation) → kein Migration nötig, CREATE TABLE übernimmt das gleich
            }

            // 2f. Neue Tabelle für selbst erstellte Ordnernamen
            using (var cmd2 = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS CustomFolders (Name TEXT PRIMARY KEY);",
                connection))
            {
                cmd2.ExecuteNonQuery();
            }

            // 3. Neue Tabelle erstellen, falls nicht vorhanden
            using (var cmd = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS Bookmarks (UrlKey TEXT PRIMARY KEY, Url TEXT NOT NULL, Title TEXT NOT NULL, IsFavorite INTEGER NOT NULL DEFAULT 0, Browser TEXT NOT NULL DEFAULT '', Folder TEXT NOT NULL DEFAULT '', IsDead INTEGER);",
                connection))
            {
                cmd.ExecuteNonQuery();
            }

            // 4. Chrome/Edge/DuckDuckGo Import ausführen
            var imported = ImportBookmarksFromChromeEdge();
            imported.AddRange(ImportFromDuckDuckGo());
            imported.AddRange(ImportFromFirefox());

            // 5. Importierte Bookmarks in DB upserten (in einer Transaktion – deutlich
            // schneller als hunderte Einzel-Inserts, da SQLite sonst nach jeder Zeile
            // einzeln auf die Festplatte synchronisiert)
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var bm in imported)
                {
                    string urlKey = NormalizeUrl(bm.Url);
                    using var insertCmd = new SqliteCommand(
                        @"INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite, Browser, Folder)
                          VALUES (@urlKey, @url, @title, 0, @browser, @folder)
                          ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title, Browser = CASE WHEN Bookmarks.Browser = '' THEN excluded.Browser ELSE Bookmarks.Browser END, Folder = CASE WHEN Bookmarks.Folder = '' THEN excluded.Folder ELSE Bookmarks.Folder END;",
                        connection, transaction);
                    insertCmd.Parameters.AddWithValue("@urlKey", urlKey);
                    insertCmd.Parameters.AddWithValue("@url", bm.Url);
                    insertCmd.Parameters.AddWithValue("@title", bm.Title);
                    insertCmd.Parameters.AddWithValue("@browser", bm.Browser);
                    insertCmd.Parameters.AddWithValue("@folder", bm.Folder);
                    insertCmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 6. Alle Bookmarks aus DB laden
            _allBookmarks.Clear();
            using (var selectCmd = new SqliteCommand("SELECT Url, Title, IsFavorite, Browser, Folder, IsDead FROM Bookmarks;", connection))
            using (SqliteDataReader reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    _allBookmarks.Add(new Bookmark
                    {
                        Url = reader.GetString(0),
                        Title = reader.GetString(1),
                        IsFavorite = reader.GetInt32(2) == 1,
                        Browser = reader.GetString(3),
                        Folder = reader.GetString(4),
                        IsDead = reader.IsDBNull(5) ? (bool?)null : reader.GetInt32(5) == 1
                    });
                }
            }

            // 7. Fallback: Demo-Daten, falls DB leer ist
            if (_allBookmarks.Count == 0)
            {
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var bm in _demoBookmarks)
                    {
                        string urlKey = NormalizeUrl(bm.Url);
                        using var insertCmd = new SqliteCommand(
                            @"INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite, Browser, Folder)
                              VALUES (@urlKey, @url, @title, 0, @browser, @folder)
                              ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title, Browser = CASE WHEN Bookmarks.Browser = '' THEN excluded.Browser ELSE Bookmarks.Browser END, Folder = CASE WHEN Bookmarks.Folder = '' THEN excluded.Folder ELSE Bookmarks.Folder END;",
                            connection, transaction);
                        insertCmd.Parameters.AddWithValue("@urlKey", urlKey);
                        insertCmd.Parameters.AddWithValue("@url", bm.Url);
                        insertCmd.Parameters.AddWithValue("@title", bm.Title);
                        insertCmd.Parameters.AddWithValue("@browser", bm.Browser);
                        insertCmd.Parameters.AddWithValue("@folder", bm.Folder);
                        insertCmd.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }

                // Demo-Daten auch in Speicher laden
                _allBookmarks.AddRange(_demoBookmarks);
            }

            LoadCustomFolderNames();
            UpdateCustomFolderFlags();

            RefreshDisplayedBookmarks();
            PopulateBrowserFilter();
        }

        #region Hilfsmethoden

        /// <summary>
        /// Lädt die Namen der selbst erstellten Ordner aus der DB in den Speicher.
        /// </summary>
        private void LoadCustomFolderNames()
        {
            _customFolderNames.Clear();

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var cmd = new SqliteCommand("SELECT Name FROM CustomFolders;", connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _customFolderNames.Add(reader.GetString(0));
            }
        }

        /// <summary>
        /// Markiert einen Ordnernamen als selbst erstellt, damit er in der Anzeige oben einsortiert wird.
        /// </summary>
        private void MarkFolderAsCustom(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return;

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var cmd = new SqliteCommand("INSERT OR IGNORE INTO CustomFolders (Name) VALUES (@name);", connection);
            cmd.Parameters.AddWithValue("@name", folderName);
            cmd.ExecuteNonQuery();

            _customFolderNames.Add(folderName);
        }

        /// <summary>
        /// Setzt bei allen Bookmarks das IsCustomFolder-Flag passend zu den bekannten Custom-Ordnernamen.
        /// </summary>
        private void UpdateCustomFolderFlags()
        {
            foreach (var bm in _allBookmarks)
            {
                bm.IsCustomFolder = _customFolderNames.Contains(bm.Folder);
            }
        }

        /// <summary>
        /// Normalisiert eine URL für die Deduplikationslogik.
        /// </summary>
        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            // Trim
            url = url.Trim();

            // Lowercase
            url = url.ToLowerInvariant();

            // Trailing Slash entfernen
            if (url.EndsWith('/'))
                url = url.Substring(0, url.Length - 1);

            return url;
        }

        #endregion

        #region Demo-Loading

        /// <summary>
        /// Lädt die statischen Demo-Bookmarks in den Quell-Array.
        /// </summary>
        private void LoadDemoBookmarks()
        {
            _allBookmarks.Clear();
            foreach (var b in _demoBookmarks)
                _allBookmarks.Add(b);
        }

        #endregion

        #region Bookmark Import

        /// <summary>
        /// Importiert Bookmarks aus Chrome und Edge (Chromium-JSON).
        /// </summary>
        /// <returns>Liste aller gefundenen Bookmarks.</returns>
        private List<Bookmark> ImportBookmarksFromChromeEdge()
        {
            var browsers = new[]
            {
                ("Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Bookmarks")),
                ("Edge", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data", "Default", "Bookmarks")),
                ("Opera", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software", "Opera Stable", "Bookmarks")),
                ("Brave", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "User Data", "Default", "Bookmarks")),
                ("Vivaldi", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vivaldi", "User Data", "Default", "Bookmarks")),
                ("SRWare Iron", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Chromium", "User Data", "Default", "Bookmarks")),
                ("Comodo Dragon", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Comodo", "Dragon", "User Data", "Default", "Bookmarks"))
            };

            var tempDir = Path.Combine(Path.GetTempPath(), $"bookmark_import_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            var allBookmarks = new List<Bookmark>();
            foreach (var (name, path) in browsers)
            {
                try
                {
                    var result = ImportFromBrowser(name, path, tempDir);
                    foreach (var bm in result)
                        bm.Browser = name;
                    allBookmarks.AddRange(result);
                }
                catch
                {
                    // Fehler beim Import eines Browsers werden ignoriert
                }
            }

            Directory.Delete(tempDir, true);
            return allBookmarks;
        }

        /// <summary>
        /// Importiert die Bookmark-Datei eines Browsers, falls vorhanden.
        /// </summary>
        private List<Bookmark> ImportFromBrowser(string browserName, string sourcePath, string tempDir)
        {
            var bookmarks = new List<Bookmark>();

            if (!File.Exists(sourcePath))
                return bookmarks; // Browser nicht installiert oder Datei fehlt

            try
            {
                string tempFile = Path.Combine(tempDir, $"{browserName}_Bookmarks.json");
                File.Copy(sourcePath, tempFile, true);

                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(tempFile));
                if (!doc.RootElement.TryGetProperty("roots", out JsonElement roots))
                    return bookmarks; // kein gültiges Format

                foreach (JsonProperty rootProp in roots.EnumerateObject())
                {
                    CollectBookmarksRecursive(rootProp.Value, bookmarks, "");
                }
            }
            catch
            {
                // Fehler beim Kopieren oder Parsen → Browser einfach überspringen
            }

            return bookmarks;
        }

        /// <summary>
        /// Rekursive Durchgang der Bookmark-Struktur.
        /// </summary>
        private void CollectBookmarksRecursive(JsonElement node, List<Bookmark> list, string folderPath)
        {
            if (node.ValueKind != JsonValueKind.Object)
                return;

            if (!node.TryGetProperty("type", out JsonElement typeProp))
                return;

            string type = typeProp.GetString();
            if (type == "url")
            {
                // Bookmark-Eintrag
                string title = node.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : "";
                string url   = node.TryGetProperty("url",  out JsonElement urlEl ) ? urlEl.GetString()  : "";
                if (!string.IsNullOrWhiteSpace(url))
                    list.Add(new Bookmark { Title = title, Url = url, Folder = folderPath });
            }
            else if (type == "folder")
            {
                // Ordner → rekursiv durch Kinder gehen, Pfad mitführen
                string folderName = node.TryGetProperty("name", out JsonElement folderNameEl) ? folderNameEl.GetString() : "";
                string childPath = string.IsNullOrEmpty(folderPath) ? folderName : $"{folderPath}/{folderName}";

                if (node.TryGetProperty("children", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement child in children.EnumerateArray())
                        CollectBookmarksRecursive(child, list, childPath);
                }
            }
        }

        /// <summary>
        /// Importiert Bookmarks aus DuckDuckGo.
        /// </summary>
        private List<Bookmark> ImportFromDuckDuckGo()
        {
            var bookmarks = new List<Bookmark>();
            string packagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (!Directory.Exists(packagesPath))
                return bookmarks;

            try
            {
                string[] duckDuckGoPackages = Directory.GetDirectories(packagesPath, "DuckDuckGo*");
                if (duckDuckGoPackages.Length == 0)
                    return bookmarks;

                string tempDir = Path.Combine(Path.GetTempPath(), "MarkDock_Import");
                Directory.CreateDirectory(tempDir);

                foreach (string packagePath in duckDuckGoPackages)
                {
                    string dbPath = Path.Combine(packagePath, "LocalState", "browser-v1.db");
                    if (!File.Exists(dbPath))
                        continue;

                    string tempDbPath = Path.Combine(tempDir, $"DuckDuckGo_{Path.GetRandomFileName()}.db");
                    File.Copy(dbPath, tempDbPath, true);

                    try
                    {
                        using var connection = new SqliteConnection($"Data Source={tempDbPath}");
                        connection.Open();

                        using (var tableCmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table';", connection))
                        using (SqliteDataReader tableReader = tableCmd.ExecuteReader())
                        {
                            while (tableReader.Read())
                            {
                                string tableName = tableReader.GetString(0);
                                if (string.IsNullOrWhiteSpace(tableName))
                                    continue;

                                using (var columnCmd = new SqliteCommand($"PRAGMA table_info({tableName});", connection))
                                using (SqliteDataReader columnReader = columnCmd.ExecuteReader())
                                {
                                    List<string> columns = new();
                                    string urlColumn = null;
                                    string titleColumn = null;

                                    while (columnReader.Read())
                                    {
                                        string columnName = columnReader.GetString(columnReader.GetOrdinal("name"));
                                        if (!string.IsNullOrWhiteSpace(columnName))
                                        {
                                            columns.Add(columnName);
                                            if (columnName.Equals("url", StringComparison.OrdinalIgnoreCase))
                                                urlColumn = columnName;
                                            else if (columnName.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                                                     columnName.Equals("name", StringComparison.OrdinalIgnoreCase))
                                                titleColumn = columnName;
                                        }
                                    }

                                    if (!string.IsNullOrEmpty(urlColumn) && !string.IsNullOrEmpty(titleColumn))
                                    {
                                        try
                                        {
                                            using (var selectCmd = new SqliteCommand(
                                                $"SELECT {urlColumn}, {titleColumn} FROM {tableName};", connection))
                                            using (SqliteDataReader selectReader = selectCmd.ExecuteReader())
                                            {
                                                while (selectReader.Read())
                                                {
                                                    string url = selectReader.GetString(0);
                                                    string title = selectReader.GetString(1);

                                                    if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("http"))
                                                    {
                                                        bookmarks.Add(new Bookmark { Title = title, Url = url });
                                                    }
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                return bookmarks;
            }

            foreach (var bm in bookmarks)
                bm.Browser = "DuckDuckGo";

            return bookmarks;
        }

        /// <summary>
        /// Importiert Bookmarks aus einer HTML- oder JSON-Datei.
        /// </summary>
        private List<Bookmark> ImportFromHtmlOrJson()
        {
            var bookmarks = new List<Bookmark>();
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Bookmark-Dateien (*.html;*.htm;*.json)|*.html;*.htm;*.json|HTML-Dateien (*.html;*.htm)|*.html;*.htm|JSON-Dateien (*.json)|*.json|Alle Dateien (*.*)|*.*"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string fileName = openFileDialog.FileName;
                    string extension = Path.GetExtension(fileName).ToLowerInvariant();

                    string content = File.ReadAllText(fileName);

                    if (extension == ".html" || extension == ".htm")
                    {
                        // HTML-Import mit Regex
                        Regex regex = new Regex(@"<A[^>]*HREF=""([^""]+)""[^>]*>([^<]*)</A>", RegexOptions.IgnoreCase);

                        foreach (Match match in regex.Matches(content))
                        {
                            string url = match.Groups[1].Value;
                            string title = match.Groups[2].Value;

                            // URL mit http beginnt
                            if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("http"))
                            {
                                // Titel dekodieren
                                title = WebUtility.HtmlDecode(title);
                                bookmarks.Add(new Bookmark { Title = title, Url = url });
                            }
                        }
                    }
                    else if (extension == ".json")
                    {
                        // Firefox-JSON-Import
                        bookmarks = ParseFirefoxJsonBookmarks(content);
                    }
                }
            }
            catch
            {
                // Fehler beim Import → leere Liste zurückgeben
            }

            foreach (var bm in bookmarks)
                bm.Browser = "Manuell";

            return bookmarks;
        }

        /// <summary>
        /// Parst Firefox-Bookmark-JSON oder MarkDocks eigenes flaches Export-Format.
        /// </summary>
        private List<Bookmark> ParseFirefoxJsonBookmarks(string jsonContent)
        {
            var bookmarks = new List<Bookmark>();

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                JsonElement root = doc.RootElement;

                switch (root.ValueKind)
                {
                    case JsonValueKind.Array:
                        // Flaches MarkDock-Export-Format
                        ParseMarkDockJsonExport(root, bookmarks);
                        break;
                    case JsonValueKind.Object:
                        // Klassisches Firefox-Format
                        CollectFirefoxBookmarksRecursive(root, bookmarks);
                        break;
                    default:
                        // Nicht unterstützter Root-Typ → leere Liste
                        break;
                }
            }
            catch
            {
                // Fehler beim Parsen → leere Liste zurückgeben
            }

            return bookmarks;
        }

        /// <summary>
        /// Verarbeitet MarkDocks eigenes flaches JSON-Export-Format.
        /// Erwartete Struktur: [ { "title":"...", "url":"..." }, ... ]
        /// </summary>
        private void ParseMarkDockJsonExport(JsonElement root, List<Bookmark> list)
        {
            if (root.ValueKind != JsonValueKind.Array)
                return;

            foreach (var element in root.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                string title = element.TryGetProperty("title", out JsonElement t)
                    ? t.GetString() ?? ""
                    : "";
                string url = element.TryGetProperty("url", out JsonElement u)
                    ? u.GetString() ?? ""
                    : "";

                if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("http"))
                {
                    list.Add(new Bookmark { Title = title, Url = url });
                }
            }
        }

        /// <summary>
        /// Rekursive Durchgang der Firefox-Bookmark-Struktur.
        /// </summary>
        private void CollectFirefoxBookmarksRecursive(JsonElement node, List<Bookmark> list)
        {
            if (node.ValueKind != JsonValueKind.Object)
                return;

            if (!node.TryGetProperty("type", out JsonElement typeProp))
                return;

            string type = typeProp.GetString();

            if (type == "text/x-moz-place")
            {
                // Bookmark-Eintrag
                string title = node.TryGetProperty("title", out JsonElement titleEl) ? titleEl.GetString() : "";
                string url   = node.TryGetProperty("uri",   out JsonElement uriEl)   ? uriEl.GetString()   : "";

                if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("http"))
                {
                    list.Add(new Bookmark { Title = title, Url = url });
                }
            }
            else if (type == "text/x-moz-place-container")
            {
                // Ordner → rekursiv durch Kinder gehen
                if (node.TryGetProperty("children", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement child in children.EnumerateArray())
                        CollectFirefoxBookmarksRecursive(child, list);
                }
            }
            // type == "text/x-moz-place-separator" oder alles andere → ignorieren
        }

        /// <summary>
        /// Importiert Bookmarks aus einer Gecko-basierten Profil-Wurzel (Firefox, Zen, Floorp, Waterfox).
        /// </summary>
        private List<Bookmark> ImportBookmarksFromGeckoProfiles(string profilesPath)
        {
            var bookmarks = new List<Bookmark>();

            foreach (var profileDir in Directory.GetDirectories(profilesPath))
            {
                try
                {
                    if (!File.Exists(Path.Combine(profileDir, "places.sqlite")))
                        continue;

                    var tempPath = Path.Combine(Path.GetTempPath(), $"firefox_import_{Guid.NewGuid()}");
                    Directory.CreateDirectory(tempPath);
                    var tempDbPath = Path.Combine(tempPath, "places.sqlite");
                    File.Copy(Path.Combine(profileDir, "places.sqlite"), tempDbPath, true);

                    using var connection = new SqliteConnection($"Data Source={tempDbPath}");
                    connection.Open();

                    using var cmd = new SqliteCommand(
                        "SELECT moz_bookmarks.id, moz_bookmarks.parent, moz_bookmarks.type, moz_bookmarks.title, moz_places.url " +
                        "FROM moz_bookmarks LEFT JOIN moz_places ON moz_bookmarks.fk = moz_places.id " +
                        "WHERE moz_bookmarks.type IN (1, 2);",
                        connection);

                    var nodeMap = new Dictionary<long, (long? Parent, int Type, string Title)>();
                    var bookmarkRows = new List<(long Id, string Url)>();

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        long id = reader.GetInt64(0);
                        long? parent = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
                        int type = reader.GetInt32(2);
                        string title = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        string url = reader.IsDBNull(4) ? null : reader.GetString(4);

                        nodeMap[id] = (parent, type, title);

                        if (type == 1 && !string.IsNullOrWhiteSpace(url) && url.StartsWith("http"))
                        {
                            bookmarkRows.Add((id, url));
                        }
                    }

                    foreach (var (id, url) in bookmarkRows)
                    {
                        string title = nodeMap.TryGetValue(id, out var self) ? self.Title : "";

                        var folderNames = new List<string>();
                        long? currentParent = nodeMap.TryGetValue(id, out var selfNode) ? selfNode.Parent : null;

                        int safetyCounter = 0;
                        while (currentParent.HasValue && nodeMap.TryGetValue(currentParent.Value, out var parentNode) && safetyCounter < 50)
                        {
                            if (parentNode.Type == 2 && !string.IsNullOrWhiteSpace(parentNode.Title))
                            {
                                folderNames.Insert(0, parentNode.Title);
                            }
                            currentParent = parentNode.Parent;
                            safetyCounter++;
                        }

                        string folderPath = string.Join("/", folderNames);

                        bookmarks.Add(new Bookmark { Title = title, Url = url, Folder = folderPath });
                    }

                    connection.Close();
                    Directory.Delete(tempPath, true);
                }
                catch
                {
                    // Defektes oder gesperrtes Profil wird übersprungen
                    continue;
                }
            }

            return bookmarks;
        }

        /// <summary>
        /// Importiert Bookmarks aus Firefox und Firefox-Forks (Zen, Floorp, Waterfox).
        /// </summary>
        private List<Bookmark> ImportFromFirefox()
        {
            var allBookmarks = new List<Bookmark>();

            var geckoRoots = new[]
            {
                ("Firefox", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles")),
                ("Zen", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zen", "Profiles")),
                ("Floorp", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Floorp", "Profiles")),
                ("Waterfox", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Waterfox", "Profiles"))
            };

            foreach (var (name, root) in geckoRoots)
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        var result = ImportBookmarksFromGeckoProfiles(root);
                        foreach (var bm in result)
                            bm.Browser = name;
                        allBookmarks.AddRange(result);
                    }
                }
                catch
                {
                    // Fehler bei dieser Profil-Wurzel überspringen, andere Wurzeln nicht beeinträchtigen
                    continue;
                }
            }

            return allBookmarks;
        }

        #endregion

        #region UI-Logic

        private void ApplyFilters()
        {
            string searchFilter = SearchBox.Text.Trim().ToLowerInvariant();
            string selectedBrowser = BrowserFilterComboBox.SelectedItem as string;

            _displayedBookmarks.Clear();

            var query = _allBookmarks.Where(b =>
                b.Title.ToLowerInvariant().Contains(searchFilter) ||
                b.Url.ToLowerInvariant().Contains(searchFilter));

            if (!string.IsNullOrEmpty(selectedBrowser) && selectedBrowser != "Alle")
            {
                query = query.Where(b => b.Browser == selectedBrowser);
            }

            if (_statusFilterState == 1)
            {
                query = query.Where(b => b.IsDead == true);
            }
            else if (_statusFilterState == 2)
            {
                query = query.Where(b => b.IsDead == false);
            }

            foreach (var bm in query)
            {
                _displayedBookmarks.Add(bm);
            }
        }

        private void StatusColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            _statusFilterState = (_statusFilterState + 1) % 3;
            string label = _statusFilterState switch
            {
                1 => "Filter: nur tote Links",
                2 => "Filter: nur OK-Links",
                _ => "Filter: alle Links"
            };
            StatusText.Text = label;
            ApplyFilters();
        }

        private void FolderColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            _folderSortAscending = !_folderSortAscending;

            // Primäre Sortierung (Gruppen-Reihenfolge) auf rein alphabetisch umstellen
            if (_bookmarksView.SortDescriptions.Count > 0)
                _bookmarksView.SortDescriptions.RemoveAt(0);
            _bookmarksView.SortDescriptions.Insert(0, new System.ComponentModel.SortDescription(
                "Folder", _folderSortAscending ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending));
        }

        private void TitleColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            _titleSortAscending = !_titleSortAscending;

            // Tertiäre Sortierung (innerhalb jeder Ordner-Gruppe, nach Favoriten) nach Titel setzen
            while (_bookmarksView.SortDescriptions.Count > 2)
                _bookmarksView.SortDescriptions.RemoveAt(2);
            _bookmarksView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                "Title", _titleSortAscending ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending));
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isDarkTheme = !_isDarkTheme;
                ApplyTheme(_isDarkTheme);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Fehler beim Theme-Wechsel: {ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyTheme(bool dark)
        {
            void SetBrush(string key, string hex)
            {
                Resources[key] = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            }

            if (dark)
            {
                SetBrush("WindowBackgroundBrush", "#1E1E1E");
                SetBrush("WindowForegroundBrush", "#DCDCDC");
                SetBrush("ControlBackgroundBrush", "#2D2D2D");
                SetBrush("ControlBorderBrush", "#3F3F3F");
                SetBrush("GroupHeaderBackgroundBrush", "#333333");
                SetBrush("GroupHeaderForegroundBrush", "#DCDCDC");
                ThemeToggleButton.Content = "☀";
            }
            else
            {
                SetBrush("WindowBackgroundBrush", "#FAFAFA");
                SetBrush("WindowForegroundBrush", "#202020");
                SetBrush("ControlBackgroundBrush", "#FFFFFF");
                SetBrush("ControlBorderBrush", "#CCCCCC");
                SetBrush("GroupHeaderBackgroundBrush", "#E0E0E0");
                SetBrush("GroupHeaderForegroundBrush", "#202020");
                ThemeToggleButton.Content = "🌙";
            }
        }

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "MarkDock-Datenbank (*.db)|*.db",
                FileName = $"markdock_backup_{DateTime.Now:yyyy-MM-dd}.db"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    string dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MarkDock", "markdock.db");
                    File.Copy(dbPath, saveDialog.FileName, true);
                    MessageBox.Show("Backup erfolgreich erstellt.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Backup fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_allBookmarks.Count == 0)
            {
                MessageBox.Show("Es sind keine Bookmarks vorhanden.", "Alles löschen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = "Alle Bookmarks unwiderruflich löschen",
                Width = 420,
                Height = 230,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(15) };
            var warningLabel = new TextBlock
            {
                Text = $"Das löscht ALLE {_allBookmarks.Count} Bookmarks unwiderruflich. " +
                       "Erstelle vorher ein Backup, falls du dir nicht sicher bist.\n\n" +
                       "Tippe zur Bestätigung genau \"LÖSCHEN\" in das Feld:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var confirmBox = new TextBox { Height = 30, FontSize = 14 };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            var deleteButton = new Button { Content = "Endgültig löschen", Width = 130, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)), Foreground = Brushes.White };
            var cancelButton = new Button { Content = "Abbrechen", Width = 90 };

            bool? dialogResult = null;
            deleteButton.Click += (s, args) =>
            {
                if (confirmBox.Text.Trim() == "LÖSCHEN")
                {
                    dialogResult = true;
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("Bitte exakt \"LÖSCHEN\" eintippen, um fortzufahren.", "Bestätigung fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            cancelButton.Click += (s, args) => { dialogResult = false; dialog.Close(); };

            buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(warningLabel);
            stack.Children.Add(confirmBox);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;

            dialog.ShowDialog();

            if (dialogResult != true)
                return;

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using (var cmd = new SqliteCommand("DELETE FROM Bookmarks;", connection))
            {
                cmd.ExecuteNonQuery();
            }

            _allBookmarks.Clear();
            RefreshDisplayedBookmarks();
            PopulateBrowserFilter();

            MessageBox.Show("Alle Bookmarks wurden gelöscht.", "Alles löschen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "MarkDock-Datenbank (*.db)|*.db"
            };

            if (openDialog.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "Die aktuellen Bookmarks werden durch das Backup ersetzt. Fortfahren?",
                    "Backup wiederherstellen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                try
                {
                    string dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MarkDock", "markdock.db");
                    File.Copy(openDialog.FileName, dbPath, true);
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                    // Bookmarks aus der wiederhergestellten DB neu laden (ohne erneuten Browser-Import)
                    _allBookmarks.Clear();
                    using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                    {
                        connection.Open();
                        using var cmd = new SqliteCommand("SELECT Url, Title, IsFavorite, Browser, Folder, IsDead FROM Bookmarks;", connection);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            _allBookmarks.Add(new Bookmark
                            {
                                Url = reader.GetString(0),
                                Title = reader.GetString(1),
                                IsFavorite = reader.GetInt32(2) == 1,
                                Browser = reader.GetString(3),
                                Folder = reader.GetString(4),
                                IsDead = reader.IsDBNull(5) ? (bool?)null : reader.GetInt32(5) == 1
                            });
                        }
                    }

                    LoadCustomFolderNames();
                    UpdateCustomFolderFlags();
                    RefreshDisplayedBookmarks();
                    PopulateBrowserFilter();

                    MessageBox.Show("Backup wiederhergestellt.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Wiederherstellung fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PopulateBrowserFilter()
        {
            var browsers = _allBookmarks
                .Select(b => b.Browser)
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct()
                .OrderBy(b => b)
                .ToList();

            browsers.Insert(0, "Alle");
            BrowserFilterComboBox.ItemsSource = browsers;
            BrowserFilterComboBox.SelectedIndex = 0;
        }

        private void RefreshDisplayedBookmarks()
        {
            ApplyFilters();

            // Statusanzeige aktualisieren
            StatusText.Text = $"{_allBookmarks.Count} Bookmarks";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
        }

        private void BrowserFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void BookmarksListView_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Sorgt dafür, dass Rechtsklick das Bookmark unter dem Mauszeiger auch tatsächlich auswählt
            _rightClickOnItem = false;
            var element = e.OriginalSource as DependencyObject;
            while (element != null && element is not ListViewItem)
            {
                if (element is System.Windows.Controls.GridViewColumnHeader)
                    return; // Klick war auf dem Spaltenkopf – _rightClickOnItem bleibt false
                element = VisualTreeHelper.GetParent(element);
            }
            if (element is ListViewItem item)
            {
                _rightClickOnItem = true;
                // Standard-Verhalten: Rechtsklick auf ein noch nicht ausgewähltes Element
                // ersetzt die Auswahl; ist das Element bereits Teil einer Mehrfachauswahl,
                // bleibt diese für Sammel-Aktionen erhalten
                if (!item.IsSelected)
                {
                    BookmarksListView.SelectedItems.Clear();
                    item.IsSelected = true;
                }
            }
        }

        private void BookmarksListView_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            // Nur blockieren, wenn der Rechtsklick wirklich auf einem Spaltenkopf war –
            // Bookmark-Zeilen und Ordner-Überschriften dürfen ihr eigenes Menü ganz normal zeigen
            var element = e.OriginalSource as DependencyObject;
            while (element != null)
            {
                if (element is System.Windows.Controls.GridViewColumnHeader)
                {
                    e.Handled = true;
                    return;
                }
                if (element is ListViewItem || element is System.Windows.Controls.GroupItem)
                {
                    return;
                }
                element = VisualTreeHelper.GetParent(element);
            }
        }

        private void BookmarksListView_MouseDoubleClick(object sender,
                                                       System.Windows.Input.MouseButtonEventArgs e)
        {
            // Prüfen, ob der Doppelklick wirklich auf einer Bookmark-Zeile war,
            // nicht auf einer Spaltenkopfzeile o. ä.
            var element = e.OriginalSource as DependencyObject;
            while (element != null && element is not ListViewItem)
            {
                if (element is System.Windows.Controls.GridViewColumnHeader)
                    return;
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is ListViewItem && BookmarksListView.SelectedItem is Bookmark selectedBookmark)
                OpenUrlInBrowser(selectedBookmark.Url);
        }

        private void OpenUrlInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"URL konnte nicht geöffnet werden: {ex.Message}", "Fehler",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportHtmlButton_Click(object sender, RoutedEventArgs e)
        {
            var imported = ImportFromHtmlOrJson();

            if (imported.Count == 0)
            {
                MessageBox.Show("Keine Bookmarks gefunden.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Datenbank-Verbindung
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            // Vor der Upsert-Schleife aktuelle Zeilenanzahl ermitteln
            int countBefore;
            using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM Bookmarks;", connection))
            {
                countBefore = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            // Importierte Bookmarks in DB upserten (in einer Transaktion)
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var bm in imported)
                {
                    string urlKey = NormalizeUrl(bm.Url);
                    using var insertCmd = new SqliteCommand(
                        @"INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite, Browser, Folder)
                          VALUES (@urlKey, @url, @title, 0, @browser, @folder)
                          ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title, Browser = CASE WHEN Bookmarks.Browser = '' THEN excluded.Browser ELSE Bookmarks.Browser END, Folder = CASE WHEN Bookmarks.Folder = '' THEN excluded.Folder ELSE Bookmarks.Folder END;",
                        connection, transaction);
                    insertCmd.Parameters.AddWithValue("@urlKey", urlKey);
                    insertCmd.Parameters.AddWithValue("@url", bm.Url);
                    insertCmd.Parameters.AddWithValue("@title", bm.Title);
                    insertCmd.Parameters.AddWithValue("@browser", bm.Browser);
                    insertCmd.Parameters.AddWithValue("@folder", bm.Folder);
                    insertCmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // Nach der Upsert-Schleife Zeilenanzahl erneut ermitteln
            int countAfter;
            using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM Bookmarks;", connection))
            {
                countAfter = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            // Berechnen: Neu hinzugefügte vs. bereits vorhandene Bookmarks
            int newlyAdded = countAfter - countBefore;
            int alreadyExisted = imported.Count - newlyAdded;

            // _allBookmarks aus der DB neu laden
            _allBookmarks.Clear();
            using (var selectCmd = new SqliteCommand("SELECT Url, Title, IsFavorite, Browser, Folder, IsDead FROM Bookmarks;", connection))
            using (SqliteDataReader reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    _allBookmarks.Add(new Bookmark
                    {
                        Url = reader.GetString(0),
                        Title = reader.GetString(1),
                        IsFavorite = reader.GetInt32(2) == 1,
                        Browser = reader.GetString(3),
                        Folder = reader.GetString(4),
                        IsDead = reader.IsDBNull(5) ? (bool?)null : reader.GetInt32(5) == 1
                    });
                }
            }

            // UI aktualisieren
            UpdateCustomFolderFlags();
            RefreshDisplayedBookmarks();
            PopulateBrowserFilter();

            // Erfolgsmeldung
            MessageBox.Show($"{imported.Count} Bookmarks verarbeitet: {newlyAdded} neu hinzugefügt, {alreadyExisted} bereits vorhanden (aktualisiert).", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "CSV-Datei (*.csv)|*.csv|JSON-Datei (*.json)|*.json|HTML-Datei (*.html)|*.html"
            };

            if (saveDialog.ShowDialog() == true)
            {
                string fileName = saveDialog.FileName;
                string extension = Path.GetExtension(fileName).ToLowerInvariant();

                try
                {
                    if (extension == ".csv")
                    {
                        ExportToCsv(fileName);
                    }
                    else if (extension == ".json")
                    {
                        ExportToJson(fileName);
                    }
                    else if (extension == ".html")
                    {
                        ExportToHtml(fileName);
                    }

                    MessageBox.Show($"{_displayedBookmarks.Count} Bookmarks exportiert.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Export fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportToCsv(string fileName)
        {
            using var writer = new StreamWriter(fileName);

            // Header
            writer.WriteLine("Title,Url");

            foreach (var bm in _displayedBookmarks)
            {
                string title = EscapeCsvValue(bm.Title);
                string url = EscapeCsvValue(bm.Url);
                writer.WriteLine($"{title},{url}");
            }
        }

        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Wenn der Wert Komma, Anführungszeichen oder Zeilenumbruch enthält, muss er in Anführungszeichen gesetzt werden
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                // Alle Anführungszeichen verdoppeln
                string escaped = value.Replace("\"", "\"\"");
                return $"\"{escaped}\"";
            }

            return value;
        }

        private void ExportToJson(string fileName)
        {
            var exportList = _displayedBookmarks.Select(bm => new { title = bm.Title, url = bm.Url }).ToList();
            string json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(fileName, json);
        }

        private void ExportToHtml(string fileName)
        {
            using var writer = new StreamWriter(fileName);

            writer.WriteLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
            writer.WriteLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
            writer.WriteLine("<TITLE>Bookmarks</TITLE>");
            writer.WriteLine("<H1>Bookmarks</H1>");
            writer.WriteLine("<DL><p>");

            foreach (var bm in _displayedBookmarks)
            {
                string encodedTitle = WebUtility.HtmlEncode(bm.Title);
                writer.WriteLine($"  <DT><A HREF=\"{bm.Url}\">{encodedTitle}</A>");
            }

            writer.WriteLine("</DL><p>");
        }

        private async void CheckLinksButton_Click(object sender, RoutedEventArgs e)
        {
            var toCheck = _displayedBookmarks.ToList();
            if (!toCheck.Any()) return;

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");

            using var httpClient = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            int checkedCount = 0;
            foreach (var bm in toCheck)
            {
                checkedCount++;
                StatusText.Text = $"Prüfe Link {checkedCount}/{toCheck.Count}...";

                bool isDead;
                try
                {
                    var response = await httpClient.GetAsync(
                        bm.Url,
                        System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                    int statusCode = (int)response.StatusCode;
                    // Nur eindeutige "nicht mehr vorhanden"-Codes als tot werten –
                    // andere Fehler (403/429/500 etc.) sind oft Bot-Sperren, Login-Wände
                    // oder temporäre Probleme, kein Beweis dafür, dass die Seite weg ist
                    isDead = statusCode == 404 || statusCode == 410;
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    // Timeout – könnte auch nur eine langsame Seite sein, nicht als tot werten
                    isDead = false;
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    // Verbindung komplett fehlgeschlagen (DNS, Server nicht erreichbar) – starkes Indiz
                    isDead = true;
                }
                catch
                {
                    isDead = false;
                }

                bm.IsDead = isDead;

                using var cmd = new SqliteCommand(
                    "UPDATE Bookmarks SET IsDead = @isDead WHERE UrlKey = @urlKey;",
                    connection);
                cmd.Parameters.AddWithValue("@isDead", isDead ? 1 : 0);
                cmd.Parameters.AddWithValue("@urlKey", NormalizeUrl(bm.Url));
                cmd.ExecuteNonQuery();
            }

            RefreshDisplayedBookmarks();
            MessageBox.Show($"{toCheck.Count} Links geprüft.", "Links prüfen",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteBookmarkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = BookmarksListView.SelectedItems.Cast<Bookmark>().ToList();
            if (selected.Count == 0)
                return;

            string message = selected.Count == 1
                ? $"\"{selected[0].Title}\" wirklich löschen?"
                : $"{selected.Count} Bookmarks wirklich löschen?";

            var result = MessageBox.Show(
                message,
                "Bookmark löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            foreach (var bm in selected)
            {
                using var cmd = new SqliteCommand(
                    "DELETE FROM Bookmarks WHERE UrlKey = @urlKey;",
                    connection);
                cmd.Parameters.AddWithValue("@urlKey", NormalizeUrl(bm.Url));
                cmd.ExecuteNonQuery();

                _allBookmarks.Remove(bm);
            }

            RefreshDisplayedBookmarks();
        }

        private void DeleteDeadLinksButton_Click(object sender, RoutedEventArgs e)
        {
            var deadBookmarks = _allBookmarks.Where(b => b.IsDead == true).ToList();

            if (deadBookmarks.Count == 0)
            {
                MessageBox.Show("Keine toten Links gefunden (oder noch nicht geprüft).", "Tote Links entfernen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = $"{deadBookmarks.Count} tote(n) Link(s) löschen?",
                Width = 550,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var outerPanel = new DockPanel { Margin = new Thickness(10) };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            var deleteButton = new Button { Content = "Löschen", Width = 90, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Abbrechen", Width = 90 };

            var infoLabel = new TextBlock { Text = "Folgende Bookmarks werden gelöscht:", Margin = new Thickness(0, 0, 0, 5) };
            DockPanel.SetDock(infoLabel, Dock.Top);

            var listBox = new ListBox
            {
                ItemsSource = deadBookmarks.Select(b => $"{b.Title}  —  {b.Url}").ToList()
            };

            bool? dialogResult = null;
            deleteButton.Click += (s, args) => { dialogResult = true; dialog.Close(); };
            cancelButton.Click += (s, args) => { dialogResult = false; dialog.Close(); };

            buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(cancelButton);
            outerPanel.Children.Add(infoLabel);
            outerPanel.Children.Add(buttonPanel);
            outerPanel.Children.Add(listBox);
            dialog.Content = outerPanel;

            dialog.ShowDialog();

            if (dialogResult != true)
                return;

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            foreach (var bm in deadBookmarks)
            {
                using var cmd = new SqliteCommand(
                    "DELETE FROM Bookmarks WHERE UrlKey = @urlKey;",
                    connection);
                cmd.Parameters.AddWithValue("@urlKey", NormalizeUrl(bm.Url));
                cmd.ExecuteNonQuery();

                _allBookmarks.Remove(bm);
            }

            RefreshDisplayedBookmarks();
            MessageBox.Show($"{deadBookmarks.Count} tote Links entfernt.", "Tote Links entfernen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RenameFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.Parent is not ContextMenu contextMenu)
                return;
            if (contextMenu.DataContext is not System.Windows.Data.CollectionViewGroup group)
                return;

            string oldFolder = group.Name as string ?? "";

            var dialog = new Window
            {
                Title = "Ordner umbenennen",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(10) };
            var label = new TextBlock { Text = $"Neuer Name für Ordner \"{oldFolder}\":", Margin = new Thickness(0, 0, 0, 10) };
            var textBox = new TextBox { Text = oldFolder, Height = 30 };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var okButton = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Abbrechen", Width = 70 };

            bool? dialogResult = null;
            okButton.Click += (s, args) => { dialogResult = true; dialog.Close(); };
            cancelButton.Click += (s, args) => { dialogResult = false; dialog.Close(); };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(label);
            stack.Children.Add(textBox);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;

            dialog.ShowDialog();

            if (dialogResult == true)
            {
                string newFolder = textBox.Text?.Trim() ?? "";
                if (newFolder == oldFolder)
                    return;

                string dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MarkDock", "markdock.db");
                using var connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();
                using var cmd = new SqliteCommand(
                    "UPDATE Bookmarks SET Folder = @newFolder WHERE Folder = @oldFolder;",
                    connection);
                cmd.Parameters.AddWithValue("@newFolder", newFolder);
                cmd.Parameters.AddWithValue("@oldFolder", oldFolder);
                cmd.ExecuteNonQuery();

                foreach (var bm in _allBookmarks.Where(b => b.Folder == oldFolder))
                {
                    bm.Folder = newFolder;
                }

                if (!string.IsNullOrEmpty(newFolder))
                {
                    MarkFolderAsCustom(newFolder);
                }

                UpdateCustomFolderFlags();
                RefreshDisplayedBookmarks();
            }
        }

        private void DeleteFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.Parent is not ContextMenu contextMenu)
                return;
            if (contextMenu.DataContext is not System.Windows.Data.CollectionViewGroup group)
                return;

            string folder = group.Name as string ?? "";

            var result = MessageBox.Show(
                $"Ordner \"{folder}\" auflösen? Die Bookmarks bleiben erhalten, verlieren aber ihre Ordnerzuordnung.",
                "Ordner auflösen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var cmd = new SqliteCommand(
                "UPDATE Bookmarks SET Folder = '' WHERE Folder = @folder;",
                connection);
            cmd.Parameters.AddWithValue("@folder", folder);
            cmd.ExecuteNonQuery();

            foreach (var bm in _allBookmarks.Where(b => b.Folder == folder))
            {
                bm.Folder = "";
            }

            RefreshDisplayedBookmarks();
        }

        private void MarkAsOkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetBookmarkDeadStatus(false);
        }

        private void MarkAsDeadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetBookmarkDeadStatus(true);
        }

        private void SetBookmarkDeadStatus(bool isDead)
        {
            var selected = BookmarksListView.SelectedItems.Cast<Bookmark>().ToList();
            if (selected.Count == 0)
                return;

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            foreach (var bm in selected)
            {
                bm.IsDead = isDead;

                using var cmd = new SqliteCommand(
                    "UPDATE Bookmarks SET IsDead = @isDead WHERE UrlKey = @urlKey;",
                    connection);
                cmd.Parameters.AddWithValue("@isDead", isDead ? 1 : 0);
                cmd.Parameters.AddWithValue("@urlKey", NormalizeUrl(bm.Url));
                cmd.ExecuteNonQuery();
            }

            RefreshDisplayedBookmarks();
        }

        private void EditBookmarkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = BookmarksListView.SelectedItems.Cast<Bookmark>().ToList();
            if (selected.Count == 0)
                return;

            if (selected.Count > 1)
            {
                MessageBox.Show("Bearbeiten funktioniert nur mit einem einzelnen ausgewählten Bookmark.", "Bearbeiten", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var bm = selected[0];

            var dialog = new Window
            {
                Title = "Bookmark bearbeiten",
                Width = 450,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(10) };
            var titleLabel = new TextBlock { Text = "Titel:", Margin = new Thickness(0, 0, 0, 2) };
            var titleBox = new TextBox { Text = bm.Title, Height = 30, Margin = new Thickness(0, 0, 0, 10) };
            var urlLabel = new TextBlock { Text = "URL:", Margin = new Thickness(0, 0, 0, 2) };
            var urlBox = new TextBox { Text = bm.Url, Height = 30, Margin = new Thickness(0, 0, 0, 10) };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Abbrechen", Width = 70 };

            bool? dialogResult = null;
            okButton.Click += (s, args) => { dialogResult = true; dialog.Close(); };
            cancelButton.Click += (s, args) => { dialogResult = false; dialog.Close(); };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(titleLabel);
            stack.Children.Add(titleBox);
            stack.Children.Add(urlLabel);
            stack.Children.Add(urlBox);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;

            dialog.ShowDialog();

            if (dialogResult != true)
                return;

            string newTitle = titleBox.Text?.Trim() ?? "";
            string newUrl = urlBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(newUrl))
            {
                MessageBox.Show("Die URL darf nicht leer sein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string oldUrlKey = NormalizeUrl(bm.Url);
            string newUrlKey = NormalizeUrl(newUrl);

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            if (newUrlKey != oldUrlKey)
            {
                // Prüfen, ob die neue URL bereits als anderes Bookmark existiert
                using var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM Bookmarks WHERE UrlKey = @newKey;", connection);
                checkCmd.Parameters.AddWithValue("@newKey", newUrlKey);
                long existingCount = (long)checkCmd.ExecuteScalar();
                if (existingCount > 0)
                {
                    MessageBox.Show("Diese URL existiert bereits als anderes Bookmark.", "Bearbeiten nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            using var cmd = new SqliteCommand(
                "UPDATE Bookmarks SET UrlKey = @newKey, Url = @newUrl, Title = @newTitle WHERE UrlKey = @oldKey;",
                connection);
            cmd.Parameters.AddWithValue("@newKey", newUrlKey);
            cmd.Parameters.AddWithValue("@newUrl", newUrl);
            cmd.Parameters.AddWithValue("@newTitle", newTitle);
            cmd.Parameters.AddWithValue("@oldKey", oldUrlKey);
            cmd.ExecuteNonQuery();

            bm.Title = newTitle;
            bm.Url = newUrl;

            RefreshDisplayedBookmarks();
        }

        private void MoveToFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = BookmarksListView.SelectedItems.Cast<Bookmark>().ToList();
            if (selected.Count == 0)
                return;

            var existingFolders = _allBookmarks
                .Select(b => b.Folder)
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            var dialog = new Window
            {
                Title = "In Ordner verschieben",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(10) };
            string labelText = selected.Count == 1
                ? $"Ordner für \"{selected[0].Title}\":"
                : $"Ordner für {selected.Count} Bookmarks:";
            var label = new TextBlock { Text = labelText, Margin = new Thickness(0, 0, 0, 10) };
            var comboBox = new ComboBox
            {
                ItemsSource = existingFolders,
                IsEditable = true,
                Text = selected.Count == 1 ? selected[0].Folder : "",
                Height = 30
            };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var okButton = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Abbrechen", Width = 70 };

            bool? dialogResult = null;
            okButton.Click += (s, args) => { dialogResult = true; dialog.Close(); };
            cancelButton.Click += (s, args) => { dialogResult = false; dialog.Close(); };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(label);
            stack.Children.Add(comboBox);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;

            dialog.ShowDialog();

            if (dialogResult == true)
            {
                string newFolder = comboBox.Text?.Trim() ?? "";
                bool isNewFolder = !existingFolders.Contains(newFolder);

                string dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MarkDock", "markdock.db");
                using var connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();

                foreach (var bm in selected)
                {
                    bm.Folder = newFolder;

                    using var cmd = new SqliteCommand(
                        "UPDATE Bookmarks SET Folder = @folder WHERE UrlKey = @urlKey;",
                        connection);
                    cmd.Parameters.AddWithValue("@folder", newFolder);
                    cmd.Parameters.AddWithValue("@urlKey", NormalizeUrl(bm.Url));
                    cmd.ExecuteNonQuery();
                }

                if (isNewFolder && !string.IsNullOrEmpty(newFolder))
                {
                    MarkFolderAsCustom(newFolder);
                }

                UpdateCustomFolderFlags();
                RefreshDisplayedBookmarks();
            }
        }

        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is Bookmark bm)
            {
                bm.IsFavorite = !bm.IsFavorite;

                string dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MarkDock", "markdock.db");

                using var connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();

                using var cmd = new SqliteCommand(
                    "UPDATE Bookmarks SET IsFavorite = @isFavorite WHERE UrlKey = @urlKey;",
                    connection);
                cmd.Parameters.AddWithValue("@isFavorite", bm.IsFavorite ? 1 : 0);
                cmd.Parameters.AddWithValue("@urlKey", NormalizeUrl(bm.Url));
                cmd.ExecuteNonQuery();

                RefreshDisplayedBookmarks();
            }
        }

        #endregion
    }

    public class Bookmark
    {
        public string Title { get; set; } = "";
        public string Url   { get; set; } = "";
        public bool IsFavorite { get; set; } = false;
        public string Browser { get; set; } = "";
        public string Folder { get; set; } = "";
        public bool? IsDead { get; set; } = null;
        public bool IsCustomFolder { get; set; } = false;

        // Sortierschlüssel: Favoriten ohne Ordner (ganz oben) < selbst erstellte Ordner (0_) < importierte (1_)
        public string FolderSortKey
        {
            get
            {
                if (IsFavorite && string.IsNullOrEmpty(Folder))
                    return "";
                return (IsCustomFolder ? "0_" : "1_") + Folder;
            }
        }

        // Gruppierungsschlüssel: Favoriten ohne Ordner bekommen eine eigene Gruppe oben,
        // alle anderen werden ganz normal nach Ordner gruppiert
        public string EffectiveGroup => (IsFavorite && string.IsNullOrEmpty(Folder)) ? "★ Favoriten (ohne Ordner)" : Folder;

        // URL zu einem kleinen Favicon über einen öffentlichen Dienst
        public string FaviconUrl
        {
            get
            {
                try
                {
                    var uri = new Uri(Url);
                    return $"https://www.google.com/s2/favicons?sz=16&domain={uri.Host}";
                }
                catch
                {
                    return "";
                }
            }
        }
    }

    public class BoolToStarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? "★" : "☆";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DeadLinkStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDead)
                return isDead ? "✗ Tot" : "✓ OK";
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
