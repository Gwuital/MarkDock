# Arbeitsvorlage – Task 006

## Task-ID

`Task_006_HTML_Bookmark_Import`

## Status

`Offen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** DuckDuckGo verschlüsselt seine lokale Bookmark-Datenbank
(`browser-v1.db` ist keine lesbare SQLite-Datei, sondern verschlüsselt). Ein
automatischer Import wie bei Chrome/Edge ist daher nicht möglich, ohne
Sicherheitsmechanismen der App zu umgehen – das machen wir bewusst nicht.
Stattdessen: DuckDuckGo (und praktisch jeder andere Browser) kann Bookmarks
selbst als HTML-Datei exportieren (Netscape Bookmark File Format, offener
Standard). Dieser Task baut den **Import einer solchen HTML-Datei**, manuell
vom Nutzer ausgewählt.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

`MainWindow.xaml` – aktueller Stand:

```xml
<Window x:Class="MarkDock.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MarkDock" Height="450" Width="600">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>   <!-- Search -->
            <RowDefinition Height="*"/>      <!-- List -->
        </Grid.RowDefinitions>

        <!-- Search TextBox -->
        <TextBox x:Name="SearchBox"
                 Grid.Row="0"
                 Margin="0,0,0,10"
                 Height="30"
                 VerticalContentAlignment="Center"
                 FontSize="14"
                 TextChanged="SearchBox_TextChanged"/>

        <!-- ListView of bookmarks -->
        <ListView x:Name="BookmarksListView"
                  Grid.Row="1"
                  MouseDoubleClick="BookmarksListView_MouseDoubleClick">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Titel" DisplayMemberBinding="{Binding Title}" Width="250"/>
                    <GridViewColumn Header="URL"   DisplayMemberBinding="{Binding Url}"  Width="300"/>
                </GridView>
            </ListView.View>
        </ListView>

    </Grid>
</Window>
```

`MainWindow.xaml.cs` – aktueller Stand:

```csharp
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

namespace MarkDock
{
    public partial class MainWindow : Window
    {
        // Demo-Bookmarks (fallback)
        private static readonly Bookmark[] _demoBookmarks =
        {
            new Bookmark { Title = "Google",   Url = "https://www.google.com" },
            new Bookmark { Title = "Bing",     Url = "https://www.bing.com" },
            new Bookmark { Title = "DuckDuckGo", Url = "https://duckduckgo.com" },
            new Bookmark { Title = "Stack Overflow", Url = "https://stackoverflow.com" },
            new Bookmark { Title = "GitHub",   Url = "https://github.com" }
        };

        private readonly List<Bookmark> _allBookmarks = new();
        private readonly ObservableCollection<Bookmark> _displayedBookmarks = new();

        public MainWindow()
        {
            InitializeComponent();
            BookmarksListView.ItemsSource = _displayedBookmarks;
            AttemptImportAndLoad();
        }

        private void AttemptImportAndLoad()
        {
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkDock", "markdock.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

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

            using (var cmd = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS Bookmarks (UrlKey TEXT PRIMARY KEY, Url TEXT NOT NULL, Title TEXT NOT NULL);",
                connection))
            {
                cmd.ExecuteNonQuery();
            }

            var imported = ImportBookmarksFromChromeEdge();
            imported.AddRange(ImportFromDuckDuckGo());

            foreach (var bm in imported)
            {
                string urlKey = NormalizeUrl(bm.Url);
                using var insertCmd = new SqliteCommand(
                    "INSERT OR REPLACE INTO Bookmarks (UrlKey, Url, Title) VALUES (@urlKey, @url, @title);",
                    connection);
                insertCmd.Parameters.AddWithValue("@urlKey", urlKey);
                insertCmd.Parameters.AddWithValue("@url", bm.Url);
                insertCmd.Parameters.AddWithValue("@title", bm.Title);
                insertCmd.ExecuteNonQuery();
            }

            _allBookmarks.Clear();
            using (var selectCmd = new SqliteCommand("SELECT Url, Title FROM Bookmarks;", connection))
            using (SqliteDataReader reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    _allBookmarks.Add(new Bookmark
                    {
                        Url = reader.GetString(0),
                        Title = reader.GetString(1)
                    });
                }
            }

            if (_allBookmarks.Count == 0)
            {
                foreach (var bm in _demoBookmarks)
                {
                    string urlKey = NormalizeUrl(bm.Url);
                    using var insertCmd = new SqliteCommand(
                        "INSERT OR REPLACE INTO Bookmarks (UrlKey, Url, Title) VALUES (@urlKey, @url, @title);",
                        connection);
                    insertCmd.Parameters.AddWithValue("@urlKey", urlKey);
                    insertCmd.Parameters.AddWithValue("@url", bm.Url);
                    insertCmd.Parameters.AddWithValue("@title", bm.Title);
                    insertCmd.ExecuteNonQuery();
                }

                _allBookmarks.AddRange(_demoBookmarks);
            }

            RefreshDisplayedBookmarks();
        }

        #region Hilfsmethoden

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            url = url.Trim();
            url = url.ToLowerInvariant();

            if (url.EndsWith('/'))
                url = url.Substring(0, url.Length - 1);

            return url;
        }

        #endregion

        #region Demo-Loading

        private void LoadDemoBookmarks()
        {
            _allBookmarks.Clear();
            foreach (var b in _demoBookmarks)
                _allBookmarks.Add(b);
        }

        #endregion

        #region Bookmark Import

        private List<Bookmark> ImportBookmarksFromChromeEdge()
        {
            var result = new List<Bookmark>();
            string tempDir = Path.Combine(Path.GetTempPath(), "MarkDock_Import");
            Directory.CreateDirectory(tempDir);

            result.AddRange(ImportFromBrowser(
                "Chrome",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                              @"Google\Chrome\User Data\Default\Bookmarks"),
                tempDir));

            result.AddRange(ImportFromBrowser(
                "Edge",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                              @"Microsoft\Edge\User Data\Default\Bookmarks"),
                tempDir));

            return result;
        }

        private List<Bookmark> ImportFromBrowser(string browserName, string sourcePath, string tempDir)
        {
            var bookmarks = new List<Bookmark>();

            if (!File.Exists(sourcePath))
                return bookmarks;

            try
            {
                string tempFile = Path.Combine(tempDir, $"{browserName}_Bookmarks.json");
                File.Copy(sourcePath, tempFile, true);

                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(tempFile));
                if (!doc.RootElement.TryGetProperty("roots", out JsonElement roots))
                    return bookmarks;

                foreach (JsonProperty rootProp in roots.EnumerateObject())
                {
                    CollectBookmarksRecursive(rootProp.Value, bookmarks);
                }
            }
            catch
            {
                // Fehler beim Kopieren oder Parsen → Browser einfach überspringen
            }

            return bookmarks;
        }

        private void CollectBookmarksRecursive(JsonElement node, List<Bookmark> list)
        {
            if (node.ValueKind != JsonValueKind.Object)
                return;

            if (!node.TryGetProperty("type", out JsonElement typeProp))
                return;

            string type = typeProp.GetString();
            if (type == "url")
            {
                string title = node.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : "";
                string url   = node.TryGetProperty("url",  out JsonElement urlEl ) ? urlEl.GetString()  : "";
                if (!string.IsNullOrWhiteSpace(url))
                    list.Add(new Bookmark { Title = title, Url = url });
            }
            else if (type == "folder")
            {
                if (node.TryGetProperty("children", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement child in children.EnumerateArray())
                        CollectBookmarksRecursive(child, list);
                }
            }
        }

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

            return bookmarks;
        }

        #endregion

        #region UI-Logic

        private void RefreshDisplayedBookmarks()
        {
            _displayedBookmarks.Clear();
            foreach (var bm in _allBookmarks)
                _displayedBookmarks.Add(bm);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.Trim().ToLowerInvariant();

            _displayedBookmarks.Clear();

            foreach (var bm in _allBookmarks.Where(b =>
                        b.Title.ToLowerInvariant().Contains(filter) ||
                        b.Url   .ToLowerInvariant().Contains(filter)))
            {
                _displayedBookmarks.Add(bm);
            }
        }

        private void BookmarksListView_MouseDoubleClick(object sender,
                                                       System.Windows.Input.MouseButtonEventArgs e)
        {
            if (BookmarksListView.SelectedItem is Bookmark selectedBookmark)
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

        #endregion
    }

    public class Bookmark
    {
        public string Title { get; set; } = "";
        public string Url   { get; set; } = "";
    }
}
```

---

## 2. Auftrag (genau EIN Problem)

Füge einen **manuellen HTML-Bookmark-Import** hinzu:

1. **UI-Ergänzung** (einzige erlaubte UI-Änderung in diesem Task): Ein Button
   `"HTML importieren..."` oberhalb oder neben dem Suchfeld in
   `MainWindow.xaml`.

2. **Click-Handler** `ImportHtmlButton_Click`:
   - Öffnet einen Standard-Windows-Dateidialog
     (`Microsoft.Win32.OpenFileDialog`, Teil von .NET, keine neue
     Dependency) mit Filter `"HTML-Dateien (*.html;*.htm)|*.html;*.htm|Alle
     Dateien (*.*)|*.*"`
   - Falls eine Datei ausgewählt wurde: Inhalt einlesen und Bookmarks
     extrahieren (siehe Punkt 3)

3. **HTML-Parsing** (bewusst einfach, kein HTML-Parser-Paket nötig – siehe
   harte Grenzen): Bookmark-Export-Dateien folgen einem sehr einheitlichen
   Muster (`<A HREF="url">Titel</A>`). Ein Regex reicht:
   ```
   <A[^>]*HREF="([^"]+)"[^>]*>([^<]*)</A>
   ```
   (case-insensitive). Gruppe 1 = URL, Gruppe 2 = Titel. Titel mit
   `System.Net.WebUtility.HtmlDecode()` dekodieren (wandelt z. B. `&amp;`
   zurück in `&`). Nur Treffer übernehmen, deren URL mit `http` beginnt.

4. Gefundene Bookmarks über denselben Upsert-Mechanismus wie
   Chrome/Edge/DuckDuckGo in die SQLite-Tabelle schreiben (`UrlKey` via
   `NormalizeUrl()`, `Url`, `Title`).

5. Danach `_allBookmarks` aus der DB neu laden und
   `RefreshDisplayedBookmarks()` aufrufen – gleicher Ablauf wie beim
   Start-Import.

6. Kurzes Feedback nach dem Import: einfaches `MessageBox.Show($"{count}
   Bookmarks importiert.")` reicht (gleiches Muster wie die bestehende
   Fehler-MessageBox beim Öffnen-Fehler).

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Kein automatisches Finden/Ausführen des Browser-eigenen Exports – der
  Nutzer exportiert manuell und wählt die Datei im Dialog aus
- Keine externe HTML-Parser-Bibliothek (kein HtmlAgilityPack o. ä.) – der
  Regex-Ansatz reicht für die einheitliche Struktur von Bookmark-Exporten
- Keine Übernahme der Ordnerstruktur aus der HTML-Datei – nur flache Liste
  `{Title, Url}`, konsistent mit dem bestehenden Modell
- Keine Änderung am automatischen Start-Import (Chrome/Edge/DuckDuckGo) –
  der HTML-Import ist ein zusätzlicher, manuell ausgelöster Weg
- Kein neues Feld im `Bookmark`-Modell
- Nur der eine Button + Click-Handler als UI-Änderung – kein Fortschrittsbalken,
  keine Statusleiste, kein zusätzliches Fenster

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  der vollständige aktuelle Stand beider Dateien

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash, Button "HTML importieren..." ist sichtbar
- [ ] Export aus DuckDuckGo (oder einem anderen Browser) als HTML-Datei lässt
      sich über den Button auswählen
- [ ] Bookmarks aus der HTML-Datei erscheinen nach dem Import in der Liste
- [ ] Bereits vorhandene Bookmarks (aus Chrome/Edge) bleiben erhalten, keine
      Duplikate durch den HTML-Import
- [ ] Bookmarks aus dem HTML-Import bleiben nach einem Neustart erhalten
      (landen in derselben SQLite-Tabelle)
- [ ] Suche und Öffnen-per-Klick funktionieren weiterhin für alle Bookmarks

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

`<offen bis Rückgabe>`
