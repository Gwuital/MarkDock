# Arbeitsvorlage – Task 002

## Task-ID

`Task_002_Browser_Import_Chrome_Edge`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**, in dieser Reihenfolge:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
/read docs/05_Browser_Importer.md
```

`05_Browser_Importer.md` beschreibt Speicherorte, JSON-Struktur und Fehlerbehandlung
für Chrome/Edge – das ist die verbindliche Grundlage für diesen Task.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine Rückfrage nötig):

`MainWindow.xaml.cs` – aktueller Stand:

```csharp
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MarkDock
{
    public partial class MainWindow : Window
    {
        // Alle Demo-Bookmarks (unveränderlich)
        private readonly Bookmark[] _allBookmarks = new[]
        {
            new Bookmark { Title = "Google",   Url = "https://www.google.com" },
            new Bookmark { Title = "Bing",     Url = "https://www.bing.com" },
            new Bookmark { Title = "DuckDuckGo", Url = "https://duckduckgo.com" },
            new Bookmark { Title = "Stack Overflow", Url = "https://stackoverflow.com" },
            new Bookmark { Title = "GitHub",   Url = "https://github.com" }
        };

        // Die aktuell gefilterte Liste (verknüpft mit ListView)
        private readonly ObservableCollection<Bookmark> _displayedBookmarks = new();

        public MainWindow()
        {
            InitializeComponent();
            BookmarksListView.ItemsSource = _displayedBookmarks;
            LoadDemoBookmarks();
        }

        private void LoadDemoBookmarks()
        {
            _displayedBookmarks.Clear();
            foreach (var b in _allBookmarks)
                _displayedBookmarks.Add(b);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.Trim().ToLowerInvariant();
            _displayedBookmarks.Clear();

            foreach (var bm in _allBookmarks.Where(b =>
                         b.Title.ToLowerInvariant().Contains(filter) ||
                         b.Url .ToLowerInvariant().Contains(filter)))
            {
                _displayedBookmarks.Add(bm);
            }
        }

        private void BookmarksListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
                MessageBox.Show($"URL konnte nicht geöffnet werden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Bookmark
    {
        public string Title { get; set; } = "";
        public string Url   { get; set; } = "";
    }
}
```

Hinweis: `_allBookmarks` ist aktuell ein festes `readonly` Array mit Demo-Daten.
Für den Import muss diese Quelle austauschbar werden (z. B. `List<Bookmark>`
statt `readonly Array`), damit echte Bookmarks statt der Demo-Daten geladen
werden können – Demo-Daten bleiben aber als Fallback im Code erhalten.

---

## 2. Auftrag (genau EIN Problem)

Erweitere die bestehende MarkDock-App um **echten Bookmark-Import aus Chrome
und Edge** (Chromium-JSON-Format), anstelle der reinen Demo-Daten:

1. Beim Start: für Chrome und Edge jeweils prüfen, ob die Bookmarks-Datei
   existiert:
   - Chrome: `%LOCALAPPDATA%\Google\Chrome\User Data\Default\Bookmarks`
   - Edge: `%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Bookmarks`
2. Falls vorhanden: Datei in einen Temp-Ordner kopieren (read-only-Prinzip,
   vermeidet Sperrprobleme), dann **nur die Kopie** als JSON parsen.
3. JSON rekursiv durch `"roots"` (`bookmark_bar`, `other`, `synced`) durchgehen.
   Jeder Knoten vom Typ `"url"` wird als `Bookmark { Title = name, Url = url }`
   übernommen. Ordner (Typ `"folder"`) werden rekursiv weiterverfolgt.
4. Alle gefundenen Bookmarks aus Chrome + Edge zusammen in die bestehende
   Anzeige-Liste laden – ersetzt die Demo-Daten als Quelle.
5. **Fallback:** Wenn nach dem Import beider Browser insgesamt **0 Bookmarks**
   gefunden wurden (kein Browser installiert, Datei fehlt, Parsing schlägt
   fehl), automatisch die bisherigen Demo-Bookmarks laden – wie bisher in
   `LoadDemoBookmarks()`.
6. Import läuft **synchron** beim Start (asynchroner Import ist ein späterer
   Task, hier nicht nötig).

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Kein DuckDuckGo-Import (eigener späterer Task, anderes Format)
- Keine Dubletten-Erkennung/Merge zwischen Chrome und Edge – doppelte URLs
  dürfen doppelt in der Liste erscheinen, das ist ein separater Task
- Keine SQLite-Speicherung – weiterhin In-Memory
- Keine Erweiterung des `Bookmark`-Modells (bleibt exakt `{ Title, Url }`) –
  keine neuen Felder wie Browser-Quelle, Ordner, Datum
- Kein Logging-System – bei Fehlern (Datei fehlt, JSON kaputt) den jeweiligen
  Browser einfach überspringen (try/catch, kein Crash, kein Log-File)
- Keine UI-Änderungen (kein neuer Button, keine Statusanzeige) – Import läuft
  automatisch und unsichtbar im Hintergrund beim Start
- Suche und Klick-zum-Öffnen-Logik aus Task 001 bleiben unverändert

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs` (inkl. neuer
  Import-Logik)
- falls eine eigene Importer-Klasse/-Datei sinnvoll ist: vollständigen Inhalt
  dieser neuen Datei(en) mit ausgeben
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist der
  vollständige aktuelle Stand, es muss nichts weiter angefragt werden

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash
- [ ] Falls Chrome und/oder Edge installiert sind: echte Bookmarks werden
      angezeigt (nicht die Demo-Daten)
- [ ] Falls kein Browser gefunden wird: Demo-Bookmarks erscheinen wie bisher
      als Fallback
- [ ] Suche und Öffnen-per-Klick funktionieren weiterhin wie in Task 001
- [ ] Keine Veränderung der Original-Browserdateien (nur read-only Zugriff
      über kopierte Temp-Datei)

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – Code-Review bestanden, Live-Test teilweise bestätigt.**

Statischer Review: `_allBookmarks` korrekt auf `List<Bookmark>` umgestellt,
Chrome/Edge-Pfade korrekt, Datei wird vor dem Lesen kopiert (read-only
eingehalten), Rekursion durch `roots` → Ordner → `url`-Knoten korrekt,
Fallback auf Demo-Daten bei 0 Treffern sauber verdrahtet. Bookmark-Modell
unverändert, kein Dedup, kein SQLite, kein DuckDuckGo, keine UI-Änderung –
alle Scope-Grenzen eingehalten.

Live-Test (Screenshot): App startet, **echte Chrome/Edge-Bookmarks werden
angezeigt** (nicht mehr die Demo-Daten) – Import funktioniert nachweislich.

Build-Output zeigt 4x `warning CS8600`/`CS8601` (Nullable-Warnungen bei
`JsonElement.GetString()` → `string`). Keine Fehler, kein Blocker – rein
kosmetisch, da `Nullable` im csproj aktiviert ist. Für spätere Politur
vormerken, kein eigener Task nötig.

Offen: Bestätigung, dass Suche und Doppelklick-öffnen weiterhin funktionieren
(Code dafür unverändert, aber noch nicht erneut live geprüft).

**Runde 2 – abgenommen.** Suche und Doppelklick-öffnen von Sven bestätigt,
funktionieren weiterhin wie in Task 001. Definition of Done vollständig erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nur harmlose Nullable-Warnings)
- [x] App startet ohne Crash
- [x] Chrome/Edge installiert → echte Bookmarks werden angezeigt
- [x] Fallback auf Demo-Bookmarks bleibt im Code (nicht negativ getestet, aber
      Logik unverändert aus Task 001 korrekt übernommen)
- [x] Suche und Öffnen-per-Klick funktionieren weiterhin
- [x] Keine Veränderung der Original-Browserdateien (nur Kopie in Temp gelesen)

**Task 002 abgeschlossen.**

---

## Nächster Schritt (für nächste Session)

**Task 003 – Vorschlag:** DuckDuckGo-Import (SQLite-Datei, heuristisch,
siehe `docs/05_Browser_Importer.md` Abschnitt 7) ODER Umstieg von In-Memory
auf SQLite-Speicherung (`docs/06_Datenbank.md`) – noch nicht entschieden,
welches zuerst drankommt. Bei Sessionstart hier anfangen.

Token-Limit erreicht (~90%), Pause auf Wunsch von Sven. Aktueller Code liegt
vollständig und lauffähig unter `src\MarkDock\`.
