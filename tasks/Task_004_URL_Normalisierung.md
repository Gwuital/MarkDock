# Arbeitsvorlage – Task 004

## Task-ID

`Task_004_URL_Normalisierung`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
/read docs/04_Datenmodell.md
/read docs/06_Datenbank.md
```

Relevant sind hier vor allem die Abschnitte zur **URL-Normalisierung**
(`04_Datenmodell.md` Abschnitt 9, `06_Datenbank.md` Abschnitt 10): URL wird
normalisiert durch **Trim, Lowercase, Entfernen des trailing Slash** – mehr
nicht (keine Query-Parameter-Bereinigung, kein http/https-Angleich, kein
www-Stripping).

**Wichtiger Hinweis zur Migration:** Auf dem Zielrechner existiert bereits
eine `markdock.db` mit dem alten Schema aus Task 003
(`Bookmarks(Url TEXT PRIMARY KEY, Title TEXT NOT NULL)`). Für diesen Task ist
ein **einfacher, harter Schema-Wechsel** ausreichend und ausdrücklich erlaubt
(siehe ADR-999 Regel 9 – Fallback ist erlaubt): wird beim Start die alte
Tabellenstruktur erkannt (Spalte `UrlKey` fehlt), wird die Tabelle **gelöscht
und mit neuem Schema neu angelegt**. Das ist unkritisch, weil alle Bookmarks
beim nächsten Start automatisch erneut aus Chrome/Edge importiert werden
(bzw. der Demo-Fallback greift). Keine komplexere Migrations-Logik nötig.

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

            using (var cmd = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS Bookmarks (Url TEXT PRIMARY KEY, Title TEXT NOT NULL);",
                connection))
            {
                cmd.ExecuteNonQuery();
            }

            var imported = ImportBookmarksFromChromeEdge();

            foreach (var bm in imported)
            {
                using var insertCmd = new SqliteCommand(
                    "INSERT OR REPLACE INTO Bookmarks (Url, Title) VALUES (@url, @title);",
                    connection);
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
                    using var insertCmd = new SqliteCommand(
                        "INSERT OR REPLACE INTO Bookmarks (Url, Title) VALUES (@url, @title);",
                        connection);
                    insertCmd.Parameters.AddWithValue("@url", bm.Url);
                    insertCmd.Parameters.AddWithValue("@title", bm.Title);
                    insertCmd.ExecuteNonQuery();
                }

                _allBookmarks.AddRange(_demoBookmarks);
            }

            RefreshDisplayedBookmarks();
        }

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

Führe eine **URL-Normalisierung** für die Dubletten-Erkennung ein, damit z. B.
`https://Example.com/` und `https://example.com` als dasselbe Bookmark erkannt
werden (aktuell werden sie fälschlich als zwei verschiedene gespeichert, weil
die rohe URL als Primary Key dient):

1. Neue private Hilfsmethode `NormalizeUrl(string url)`:
   - `Trim()`
   - `ToLowerInvariant()`
   - trailing `/` entfernen (falls vorhanden)
   - nichts weiter (keine Query-Parameter, kein http/https-Angleich, kein
     www-Stripping)

2. Schema-Änderung der `Bookmarks`-Tabelle:
   ```sql
   CREATE TABLE IF NOT EXISTS Bookmarks (
       UrlKey TEXT PRIMARY KEY,
       Url    TEXT NOT NULL,
       Title  TEXT NOT NULL
   );
   ```
   `UrlKey` ist die normalisierte URL (Dedup-Schlüssel), `Url` bleibt die
   **Original-URL** (für Anzeige und zum Öffnen im Browser – wichtig, weil
   viele echte URLs case-sensitive Pfade oder Query-Parameter haben und durch
   Lowercase kaputtgehen könnten).

3. **Migration (einmalig):** Beim Start prüfen, ob die Tabelle `Bookmarks`
   bereits existiert, aber die Spalte `UrlKey` fehlt (altes Schema aus
   Task 003, `PRAGMA table_info(Bookmarks)` abfragen). Falls ja: Tabelle
   löschen (`DROP TABLE Bookmarks;`) und mit neuem Schema neu anlegen. Alte
   Daten gehen dabei verloren – das ist hier ausdrücklich in Ordnung, da beim
   nächsten Start automatisch neu aus Chrome/Edge importiert wird (bzw. der
   Demo-Fallback greift).

4. Beim Upsert: `UrlKey = NormalizeUrl(bm.Url)` berechnen, `INSERT OR REPLACE
   INTO Bookmarks (UrlKey, Url, Title) VALUES (@urlKey, @url, @title)`.

5. Beim Laden aus der DB: weiterhin `Url` und `Title` in die
   `Bookmark`-Objekte laden (wie bisher) – `UrlKey` wird nur intern für die
   Dedup-Logik gebraucht, nicht im `Bookmark`-Modell gespeichert.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Keine weitergehende Normalisierung als Trim/Lowercase/Trailing-Slash (siehe
  oben) – kein Entfernen von `www.`, kein Angleich von `http`/`https`, keine
  Query-Parameter-Bereinigung
- Keine Erweiterung des `Bookmark`-Modells (bleibt `{ Title, Url }`) –
  `UrlKey` existiert nur in der Datenbank, nicht im C#-Modell
- Keine komplexe Migrations-Logik – Drop+Recreate bei Schema-Mismatch reicht
- Kein DuckDuckGo-Import, kein Export, kein Async-Import (eigene Tasks)
- Suche- und Klick-zum-Öffnen-Logik unverändert lassen

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  der vollständige aktuelle Stand

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash, auch wenn die alte `markdock.db` mit dem
      Task-003-Schema noch vorhanden ist (Migration greift automatisch)
- [ ] Zwei Bookmarks, die sich nur durch Groß-/Kleinschreibung oder trailing
      Slash unterscheiden, werden nur noch einmal gespeichert
- [ ] Angezeigte/geöffnete URL bleibt die Original-Schreibweise (nicht die
      normalisierte Version)
- [ ] Bookmarks bleiben nach Neustart weiterhin erhalten
- [ ] Suche und Öffnen-per-Klick funktionieren weiterhin

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – Bug gefunden:** `reader.GetString("name")` beim Migrations-Check
kompiliert nicht (SqliteDataReader.GetString nimmt nur int-Ordinal, keinen
Spaltennamen). Korrektur angefordert.

**Runde 2 – abgenommen.** Fix korrekt: `reader.GetString(reader.GetOrdinal("name"))`.

Code-Review: Schema-Erweiterung korrekt (`UrlKey` als PK, `Url`/`Title`
bleiben für Anzeige/Öffnen unangetastet – wichtig wegen case-sensitiver
Pfade). `NormalizeUrl()` exakt nach Vorgabe (Trim/Lowercase/Trailing-Slash,
nichts mehr). Migrations-Logik (PRAGMA table_info → Drop+Recreate bei
fehlendem UrlKey) deckt sowohl "alte DB von Task 003" als auch "komplett neue
DB" korrekt ab. Upsert nutzt jetzt `UrlKey` als Konfliktschlüssel statt roher
`Url`. Alle harten Grenzen eingehalten.

Live-Test: App startet trotz vorhandener alter `markdock.db` (Task-003-Schema)
ohne Crash, Migration greift automatisch, Bookmarks sind nach dem Umbau
wieder da. Exakter Dedup-Fall (zwei URLs, die sich nur durch Groß/
Kleinschreibung oder trailing Slash unterscheiden) wurde nicht live mit einem
konstruierten Testfall durchgespielt, aber Code-Logik im Review eindeutig
korrekt verifiziert.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach Korrektur)
- [x] App startet ohne Crash, alte DB migriert automatisch
- [x] Dedup-Logik korrekt implementiert (Code-Review, nicht live demonstriert)
- [x] Angezeigte/geöffnete URL bleibt Original-Schreibweise
- [x] Bookmarks bleiben nach Neustart erhalten
- [x] Suche und Öffnen-per-Klick funktionieren weiterhin

**Task 004 abgeschlossen.**

---

## Nächster Schritt

Offene Kandidaten für Task 005:
- DuckDuckGo-Import (dritter Browser, heuristisch)
- Export (CSV/JSON/HTML)
- Async Import (UI blockiert nicht bei vielen Bookmarks)

Noch nicht entschieden – bei nächster Session klaren.
