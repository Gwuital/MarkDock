# Arbeitsvorlage – Task 005

## Task-ID

`Task_005_DuckDuckGo_Import`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
/read docs/05_Browser_Importer.md
```

**Wichtig:** `05_Browser_Importer.md` Abschnitt 7 beschreibt DuckDuckGo
ausdrücklich als **nicht standardisiert dokumentiert** – Speicherort und
Struktur sind nur vermutet, nicht garantiert. Deshalb gilt hier (ADR-999
Regel 8 „Keine Spekulation" + Regel 10 „Defensive Programmierung"): **rein
heuristisch vorgehen, bei jeder Unsicherheit einfach überspringen statt
raten.** Ein leeres Ergebnis ist ein akzeptables Ergebnis für diesen Task –
Chrome/Edge liefern ja bereits echte Daten, DuckDuckGo ist ein Bonus, kein
Muss.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

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

Ergänze einen **heuristischen DuckDuckGo-Import** als dritte Quelle neben
Chrome und Edge:

1. Neue Methode `ImportFromDuckDuckGo()`, die eine `List<Bookmark>` liefert
   (leere Liste, wenn nichts gefunden/erkannt wird – kein Crash, kein
   Exception nach außen).

2. **Package-Ordner suchen:**
   ```
   %LOCALAPPDATA%\Packages\
   ```
   Darin nach Ordnern suchen, deren Name mit `DuckDuckGo` beginnt
   (`Directory.GetDirectories(packagesPath, "DuckDuckGo*")`). Falls keiner
   gefunden wird: leere Liste zurückgeben, fertig.

3. Für jeden gefundenen Package-Ordner: prüfen, ob
   `LocalState\browser-v1.db` existiert. Falls nicht: überspringen.

4. Falls vorhanden: Datei in denselben Temp-Mechanismus wie bei Chrome/Edge
   kopieren (read-only-Prinzip), dann die Kopie mit `Microsoft.Data.Sqlite`
   öffnen (Paket ist bereits vorhanden, keine neue Dependency nötig).

5. **Heuristische Struktur-Erkennung** (weil das Schema nicht dokumentiert
   ist):
   - Alle Tabellennamen abfragen: `SELECT name FROM sqlite_master WHERE
     type='table';`
   - Für jede Tabelle die Spalten abfragen (`PRAGMA table_info(<tabelle>);`)
   - Eine Tabelle ist ein Kandidat, wenn sie mindestens eine Spalte hat,
     deren Name (case-insensitive) `"url"` enthält, UND mindestens eine
     Spalte, deren Name `"title"` oder `"name"` enthält
   - Für jede Kandidaten-Tabelle: `SELECT [urlSpalte], [titleSpalte] FROM
     [tabelle];` ausführen
   - Nur Zeilen übernehmen, deren Url-Wert nicht leer ist und mit `http`
     beginnt (einfacher Plausibilitätsfilter gegen Datenmüll)
   - Alles in try/catch – jede einzelne Tabelle, die Probleme macht, wird
     übersprungen, nicht die ganze Methode abgebrochen

6. Ergebnis von `ImportFromDuckDuckGo()` in `ImportBookmarksFromChromeEdge()`
   (oder eine neue übergeordnete Methode, die alle drei Quellen
   zusammenführt) mit einbeziehen – landet dann automatisch im bestehenden
   Upsert-in-SQLite-Ablauf wie Chrome/Edge auch.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Keine feste Schema-Annahme für DuckDuckGo – ausschließlich die
  heuristische Erkennung aus Abschnitt 2, Punkt 5
- Kein Crash bei fehlender/unbekannter/beschädigter DuckDuckGo-Datenbank –
  einfach 0 Ergebnisse zurückgeben
- Keine neue NuGet-Dependency (Microsoft.Data.Sqlite ist schon da)
- Keine Änderung an Chrome/Edge-Importlogik
- Keine Änderung an der URL-Normalisierung/Migration aus Task 004
- Keine UI-Änderungen
- Keine neuen Felder im `Bookmark`-Modell
- Weiterhin synchron, kein async

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  der vollständige aktuelle Stand

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash, unabhängig davon, ob DuckDuckGo installiert ist
      oder nicht
- [ ] Falls DuckDuckGo installiert ist und eine passende Tabelle gefunden
      wird: zusätzliche Bookmarks erscheinen in der Liste
- [ ] Falls DuckDuckGo nicht installiert ist oder die Struktur nicht erkannt
      wird: App verhält sich wie bisher (Chrome/Edge/Demo-Fallback), kein
      Fehler sichtbar
- [ ] Chrome/Edge-Import, Persistenz, Dedup, Suche und Öffnen-per-Klick
      funktionieren weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – Bug gefunden:** `columnReader.GetString("name")` kompiliert nicht
(derselbe Fehlertyp wie zuvor bei Task 004 – `GetString()` braucht int-Ordinal,
keinen Spaltennamen-String). Korrektur angefordert.

**Runde 2 – abgenommen.** Fix korrekt: `columnReader.GetString(columnReader.GetOrdinal("name"))`.

Code-Review: Package-Suche (`DuckDuckGo*`), `browser-v1.db`-Check, Temp-Kopie
vor dem Lesen (read-only eingehalten), Tabellen-/Spalten-Heuristik
(case-insensitive Suche nach "url"/"title"/"name"), Plausibilitätsfilter
(`http`-Prefix), mehrstufige try/catch-Absicherung (Tabelle, Paket,
Gesamtmethode) – bricht bei Problemen sauber ab statt zu crashen. Einbindung
über `imported.AddRange(ImportFromDuckDuckGo())` rein additiv, Chrome/Edge-
Logik unangetastet. Alle harten Grenzen eingehalten (keine feste
Schema-Annahme, keine neue Dependency, keine UI-Änderung, kein neues Modell-
Feld, synchron).

Minor/kein Blocker: Spalten-/Tabellennamen werden direkt in SQL interpoliert
statt parametrisiert – unkritisch, da die Namen aus der Schema-Metadaten-
Abfrage derselben lokalen Datei stammen, nicht aus Nutzereingabe.

Live-Test: App startet ohne Crash. Bei Sven wurden keine DuckDuckGo-Bookmarks
gefunden (kein Crash, leeres Ergebnis – laut Task-Definition ein akzeptabler
Ausgang, da DuckDuckGo entweder nicht installiert ist oder die Heuristik
nicht zur tatsächlichen Struktur passt). Chrome/Edge-Bookmarks weiterhin
unverändert vorhanden, da der neue Code rein additiv ist.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach Korrektur)
- [x] App startet ohne Crash, unabhängig von DuckDuckGo-Status
- [ ] DuckDuckGo-Bookmarks gefunden – nicht der Fall bei Sven, aber laut Task
      explizit optionaler Bonus, kein Muss
- [x] Kein Fehler sichtbar, wenn DuckDuckGo nicht erkannt wird
- [x] Chrome/Edge-Import, Persistenz, Dedup, Suche, Öffnen-per-Klick
      funktionieren weiterhin

**Task 005 abgeschlossen.**

---

## Nächster Schritt

Offene Kandidaten für Task 006:
- Export (CSV/JSON/HTML)
- Async Import (UI blockiert nicht bei vielen Bookmarks)

Noch nicht entschieden – bei nächster Session klären.
