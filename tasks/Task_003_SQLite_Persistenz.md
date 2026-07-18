# Arbeitsvorlage – Task 003

## Task-ID

`Task_003_SQLite_Persistenz`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
/read docs/06_Datenbank.md
```

**Wichtig:** `06_Datenbank.md` beschreibt das volle Ziel-Schema (mehrere
Tabellen, GUID-Ids, UrlHash, Folder, Browser-Feld usw.). Dieser Task setzt
davon **bewusst nur einen kleinen, minimalen Ausschnitt** um (siehe Abschnitt 2
und die harten Grenzen in Abschnitt 3) – der Rest kommt in späteren Tasks,
wenn das Projekt weiter wächst. Nicht das volle Schema aus dem Dokument bauen.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

`MarkDock.csproj` – aktueller Stand:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

`MainWindow.xaml.cs` – aktueller Stand:

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
            var imported = ImportBookmarksFromChromeEdge();

            if (imported.Count > 0)
                _allBookmarks.AddRange(imported);
            else
                LoadDemoBookmarks();

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

Ersetze die reine In-Memory-Haltung der Bookmarks durch eine **minimale
SQLite-Persistenz**, sodass Bookmarks auch nach einem Neustart erhalten
bleiben:

1. NuGet-Paket **`Microsoft.Data.Sqlite`** zum Projekt hinzufügen
   (`<PackageReference Include="Microsoft.Data.Sqlite" Version="8.*" />`
   im `.csproj`).
2. Beim Start: Datenbankdatei unter
   `%LOCALAPPDATA%\MarkDock\markdock.db` öffnen bzw. anlegen (Ordner
   `MarkDock` erstellen, falls nicht vorhanden).
3. Minimale Tabelle anlegen, falls nicht vorhanden:
   ```sql
   CREATE TABLE IF NOT EXISTS Bookmarks (
       Url   TEXT PRIMARY KEY,
       Title TEXT NOT NULL
   );
   ```
4. Ablauf beim Start:
   - Chrome/Edge-Import wie bisher ausführen (Code bleibt unverändert)
   - jedes importierte Bookmark per **Upsert** in SQLite schreiben (Url ist
     Primary Key – existiert die Url schon, wird nur `Title` aktualisiert,
     sonst neu eingefügt: `INSERT ... ON CONFLICT(Url) DO UPDATE SET Title=...`)
   - danach **alle** Bookmarks aus SQLite lesen und in `_allBookmarks` laden
     (SQLite ist jetzt die Quelle für die Anzeige, nicht mehr direkt die
     Import-Liste)
   - **Fallback:** wenn die Tabelle nach dem Upsert immer noch leer ist
     (kein Import gefunden UND vorher noch nichts in der DB), Demo-Bookmarks
     laden **und ebenfalls in SQLite speichern**, damit sie ab dem nächsten
     Start auch aus der DB kommen
5. Suche filtert weiterhin nur über die bereits geladene `_allBookmarks`-Liste
   im Speicher – kein DB-Query pro Tastenanschlag.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- **Kein** Umstieg auf das volle Datenmodell aus `docs/04_Datenmodell.md` –
  nur die zwei Spalten `Url`, `Title`. Kein GUID-`Id`, kein `Folder`, kein
  `Browser`-Feld, kein `CreatedAt`/`ModifiedAt`, kein `UrlHash`
- Keine URL-Normalisierung (trim/lowercase) – rohe URL als Primary Key
  verwenden, Normalisierung ist ein eigener späterer Task
- Keine `Browsers`- oder `ImportRuns`-Tabellen – nur die eine
  `Bookmarks`-Tabelle
- Keine Migrations-Logik – `CREATE TABLE IF NOT EXISTS` reicht
- Keine asynchronen DB-Zugriffe – synchron ist für diese Datenmenge ok
- Kein DuckDuckGo-Import (eigener Task)
- Suche- und Klick-zum-Öffnen-Logik unverändert lassen – nur die
  Datenquelle (`_allBookmarks`-Befüllung) ändert sich

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- vollständiger, geänderter Inhalt von `MarkDock.csproj` (neue
  `PackageReference`-Zeile)
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  der vollständige aktuelle Stand beider Dateien

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes (NuGet-Paket wird beim Build
      automatisch geladen)
- [ ] App startet ohne Crash
- [ ] Nach dem ersten Start existiert
      `%LOCALAPPDATA%\MarkDock\markdock.db`
- [ ] Bookmarks bleiben nach einem Neustart erhalten (auch wenn zwischen den
      Starts kein neuer Import stattfindet)
- [ ] Keine Duplikate bei mehrfachem Start (Url als Primary Key, Upsert statt
      Insert)
- [ ] Suche und Öffnen-per-Klick funktionieren weiterhin

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – abgenommen.**

Code-Review: NuGet-Paket korrekt, DB-Pfad korrekt (`%LOCALAPPDATA%\MarkDock\markdock.db`),
minimale Tabelle wie verlangt (`Url` PK, `Title`), `INSERT OR REPLACE` als valide
Upsert-Variante akzeptiert (funktional gleichwertig zum vorgeschlagenen
`ON CONFLICT DO UPDATE`). Anzeige lädt jetzt aus der DB statt direkt aus dem
Import – echte Persistenz. Fallback (Demo-Daten) wird ebenfalls gespeichert.
Alle harten Grenzen eingehalten (kein reicheres Modell, keine Normalisierung,
keine weiteren Tabellen, synchron, kein DuckDuckGo, Suche/Klick unverändert).

Minor/kein Blocker: `LoadDemoBookmarks()` ist jetzt toter Code (Fallback-Logik
wurde inline in `AttemptImportAndLoad` gebaut statt die Methode zu nutzen) –
kosmetisch, für spätere Politur vormerken.

Live-Test (2 Screenshots):
- Build läuft mit nur harmlosen Nullable-Warnings (CS8600/8601/8604), keine Fehler
- `%LOCALAPPDATA%\MarkDock\markdock.db` existiert nach erstem Start
- **Netter Nebeneffekt:** `Url` als Primary Key hat Chrome/Edge-Duplikate
  automatisch entfernt – 11 importierte Einträge wurden zu 9 eindeutigen
  Bookmarks in der DB (Dubletten-Erkennung „gratis“ mitgeliefert, obwohl nicht
  Teil des Tasks)
- **Neustart-Test bestanden:** nach Schließen + `dotnet run` erscheinen exakt
  dieselben 9 Bookmarks sofort wieder – Persistenz funktioniert nachweislich

Definition of Done vollständig erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nur Nullable-Warnings)
- [x] App startet ohne Crash
- [x] `%LOCALAPPDATA%\MarkDock\markdock.db` existiert nach erstem Start
- [x] Bookmarks bleiben nach Neustart erhalten
- [x] Keine Duplikate bei mehrfachem Start
- [x] Suche und Öffnen-per-Klick funktionieren weiterhin

**Task 003 abgeschlossen.**

---

## Nächster Schritt

Offene Kandidaten für Task 004 (siehe `docs/10_Roadmap.md`):
- DuckDuckGo-Import (dritter Browser, heuristisch)
- URL-Normalisierung (trim/lowercase vor Hash/PK-Vergleich)
- Export (CSV/JSON/HTML)
- Async Import (UI-Blocking vermeiden bei großen Bookmark-Mengen)

Noch nicht entschieden – bei nächster Session klaren.
