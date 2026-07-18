# Arbeitsvorlage – Task 018

## Task-ID

`Task_018_Browser_Quelle_Speichern`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Für einen Browser-Filter (nächster Task) muss zuerst
gespeichert werden, aus welchem Browser jedes Bookmark stammt. Dieser Task
baut **nur die Datenebene**: Feld ergänzen, beim Import setzen, speichern,
als zusätzliche Spalte anzeigen. **Noch kein Filter-UI** – das kommt als
eigener, kleinerer Folge-Task, sobald diese Grundlage sauber steht.

**Wichtig – bitte NICHT ganze Methoden neu schreiben, sondern exakt die
unten markierten Stellen chirurgisch ergänzen.** Vorherige Tasks sind
mehrfach gescheitert, weil ganze Methoden umgeschrieben wurden und dabei
unabsichtlich bestehende Funktionalität verloren ging.

**Aktueller Code, der angepasst wird** (Ausschnitte aus
`MainWindow.xaml.cs`, alle anderen Methoden bleiben komplett unangetastet):

**A) `Bookmark`-Modell:**
```csharp
public class Bookmark
{
    public string Title { get; set; } = "";
    public string Url   { get; set; } = "";
    public bool IsFavorite { get; set; } = false;
}
```

**B) Demo-Bookmarks:**
```csharp
private static readonly Bookmark[] _demoBookmarks =
{
    new Bookmark { Title = "Google",   Url = "https://www.google.com" },
    new Bookmark { Title = "Bing",     Url = "https://www.bing.com" },
    new Bookmark { Title = "DuckDuckGo", Url = "https://duckduckgo.com" },
    new Bookmark { Title = "Stack Overflow", Url = "https://stackoverflow.com" },
    new Bookmark { Title = "GitHub",   Url = "https://github.com" }
};
```

**C) `ImportBookmarksFromChromeEdge()`:**
```csharp
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
            allBookmarks.AddRange(ImportFromBrowser(name, path, tempDir));
        }
        catch
        {
            // Fehler beim Import eines Browsers werden ignoriert
        }
    }

    Directory.Delete(tempDir, true);
    return allBookmarks;
}
```

**D) `ImportFromDuckDuckGo()`** — nur der Kopf und die Return-Stelle relevant,
der Rest (heuristische Tabellen-/Spaltensuche) bleibt exakt wie er ist:
```csharp
private List<Bookmark> ImportFromDuckDuckGo()
{
    var bookmarks = new List<Bookmark>();
    // ... (Package-Suche, SQLite-Zugriff, Tabellen-/Spaltensuche – unverändert) ...
    // am Ende: return bookmarks;
}
```

**E) `ImportFromFirefox()`:**
```csharp
private List<Bookmark> ImportFromFirefox()
{
    var allBookmarks = new List<Bookmark>();

    var geckoRoots = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zen", "Profiles"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Floorp", "Profiles"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Waterfox", "Profiles")
    };

    foreach (var root in geckoRoots)
    {
        try
        {
            if (Directory.Exists(root))
            {
                allBookmarks.AddRange(ImportBookmarksFromGeckoProfiles(root));
            }
        }
        catch
        {
            continue;
        }
    }

    return allBookmarks;
}
```

**F) `ImportFromHtmlOrJson()`** — nur Kopf/Ende relevant, der komplette
Regex-/JSON-Parsing-Innenteil bleibt exakt wie er ist:
```csharp
private List<Bookmark> ImportFromHtmlOrJson()
{
    var bookmarks = new List<Bookmark>();
    // ... (Dateidialog, HTML-Regex-Parsing ODER JSON-Parsing – unverändert) ...
    return bookmarks;
}
```

**G) `AttemptImportAndLoad()`** — Upsert-Statements (zweimal vorhanden:
Haupt-Import und Demo-Fallback) und die SELECT-Stelle:
```csharp
using var insertCmd = new SqliteCommand(
    @"INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite)
      VALUES (@urlKey, @url, @title, 0)
      ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title;",
    connection);
insertCmd.Parameters.AddWithValue("@urlKey", urlKey);
insertCmd.Parameters.AddWithValue("@url", bm.Url);
insertCmd.Parameters.AddWithValue("@title", bm.Title);
insertCmd.ExecuteNonQuery();
```
(kommt zweimal vor – einmal im Haupt-Import-Loop, einmal im Demo-Fallback-
Loop; beide identisch zu ändern)

```csharp
using (var selectCmd = new SqliteCommand("SELECT Url, Title, IsFavorite FROM Bookmarks;", connection))
using (SqliteDataReader reader = selectCmd.ExecuteReader())
{
    while (reader.Read())
    {
        _allBookmarks.Add(new Bookmark
        {
            Url = reader.GetString(0),
            Title = reader.GetString(1),
            IsFavorite = reader.GetInt32(2) == 1
        });
    }
}
```

**H) `ImportHtmlButton_Click()`** — dieselben zwei Stellen (Upsert + SELECT),
identisch aufgebaut wie in G.

**I) `MainWindow.xaml`** — die `GridView`:
```xml
<GridView>
    <GridViewColumn Header="★" Width="36">
        <GridViewColumn.CellTemplate>
            <DataTemplate>
                <Button Content="{Binding IsFavorite, Converter={StaticResource BoolToStarConverter}}"
                        Click="ToggleFavorite_Click"
                        Background="Transparent"
                        BorderThickness="0"
                        FontSize="18"/>
            </DataTemplate>
        </GridViewColumn.CellTemplate>
    </GridViewColumn>
    <GridViewColumn Header="Titel" DisplayMemberBinding="{Binding Title}" Width="250"/>
    <GridViewColumn Header="URL"   DisplayMemberBinding="{Binding Url}"  Width="300"/>
</GridView>
```

---

## 2. Auftrag (genau EIN Problem)

Speichere pro Bookmark, aus welchem Browser es stammt, und zeige es als
Spalte an:

1. **A) Bookmark-Modell:** eine Zeile ergänzen:
   `public string Browser { get; set; } = "";`

2. **B) Demo-Bookmarks:** jedem der 5 Einträge `Browser = "Demo"` ergänzen
   (z. B. `new Bookmark { Title = "Google", Url = "...", Browser = "Demo" }`).

3. **C) `ImportBookmarksFromChromeEdge()`:** In der `foreach`-Schleife,
   statt direkt `allBookmarks.AddRange(...)`, das Ergebnis zuerst in eine
   Variable nehmen, jedem Bookmark `Browser = name` setzen, dann erst
   hinzufügen:
   ```csharp
   var result = ImportFromBrowser(name, path, tempDir);
   foreach (var bm in result)
       bm.Browser = name;
   allBookmarks.AddRange(result);
   ```

4. **D) `ImportFromDuckDuckGo()`:** unmittelbar vor dem finalen
   `return bookmarks;` (ganz am Ende der Methode, nach der gesamten
   Heuristik) einfügen:
   ```csharp
   foreach (var bm in bookmarks)
       bm.Browser = "DuckDuckGo";
   ```

5. **E) `ImportFromFirefox()`:** `geckoRoots` von einem reinen `string[]`
   auf ein Array von `(string Name, string Path)`-Tupeln umstellen:
   ```csharp
   var geckoRoots = new[]
   {
       ("Firefox", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles")),
       ("Zen", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zen", "Profiles")),
       ("Floorp", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Floorp", "Profiles")),
       ("Waterfox", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Waterfox", "Profiles"))
   };
   ```
   Die `foreach`-Schleife entsprechend anpassen (`foreach (var (name, root) in geckoRoots)`),
   und beim Hinzufügen jedem gefundenen Bookmark `Browser = name` setzen
   (gleiches Muster wie bei C).

6. **F) `ImportFromHtmlOrJson()`:** unmittelbar vor dem finalen
   `return bookmarks;` einfügen:
   ```csharp
   foreach (var bm in bookmarks)
       bm.Browser = "Manuell";
   ```

7. **G) und H) (beide Stellen, insgesamt 4x Upsert + 2x SELECT):**
   - Upsert-SQL erweitern:
     ```sql
     INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite, Browser)
     VALUES (@urlKey, @url, @title, 0, @browser)
     ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title;
     ```
     (Browser wird bei Konflikt **nicht** überschrieben, genau wie
     `IsFavorite` – nur beim allerersten Insert gesetzt) und
     `insertCmd.Parameters.AddWithValue("@browser", bm.Browser);` ergänzen.
   - SELECT erweitern zu
     `SELECT Url, Title, IsFavorite, Browser FROM Bookmarks;` und beim
     Befüllen `Browser = reader.GetString(3)` ergänzen.
   - **Datenbank-Migration:** In `AttemptImportAndLoad()`, direkt nach dem
     bestehenden `IsFavorite`-Migrationscheck (Abschnitt "2b"), einen
     dritten, analogen Check für die Spalte `"Browser"` einfügen: falls
     nicht vorhanden, `ALTER TABLE Bookmarks ADD COLUMN Browser TEXT NOT
     NULL DEFAULT '';` in try/catch ausführen (gleiches Muster wie beim
     `IsFavorite`-Check).
   - `CREATE TABLE IF NOT EXISTS` (für komplett neue Datenbanken) um
     `Browser TEXT NOT NULL DEFAULT ''` ergänzen.

8. **I) `MainWindow.xaml`:** neue `GridViewColumn Header="Quelle"
   DisplayMemberBinding="{Binding Browser}" Width="100"` zwischen der
   Stern-Spalte und der Titel-Spalte einfügen.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Keine Filter-Logik, keine Dropdown/Sidebar-UI – nur Speichern + Anzeigen
  als Spalte, Filtern ist ein eigener Folge-Task
- Keine Änderung an der heuristischen DuckDuckGo-Logik selbst, am HTML-/
  JSON-Parsing selbst, an `ImportFromBrowser`/`CollectBookmarksRecursive`/
  `ImportBookmarksFromGeckoProfiles` – nur die jeweils außen herum liegenden
  Stellen wie oben beschrieben
- `IsFavorite`-Logik, Suche, Export, Statusanzeige unverändert
- Keine neue Dependency
- Weiterhin synchron, kein async

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen**

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash mit bestehender DB (nicht-destruktive
      Migration für die neue `Browser`-Spalte)
- [ ] Neue Spalte "Quelle" zeigt den korrekten Browsernamen pro Bookmark
- [ ] Manuell importierte Bookmarks (HTML/JSON) zeigen "Manuell"
- [ ] Demo-Bookmarks zeigen "Demo"
- [ ] Browser-Zuordnung bleibt nach Neustart erhalten
- [ ] Favoriten, Suche, Export, Statusanzeige, Öffnen-per-Klick funktionieren
      weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Sehr viele Korrekturrunden – zweitgrößter Task nach Favoriten:**

Mehrfach kam trotz explizitem Auftrag nur der unveränderte Altstand von
`AttemptImportAndLoad()` zurück (Ursache vermutlich: Sven hatte nur
Teilausschnitte des Auftrags statt des kompletten Textes geschickt).
Kontrollfrage (ADR-999 Regel 5) bestand, Session war grundsätzlich gesund.
Nach vollständigem Abschicken kam die Migration/Upsert/SELECT-Erweiterung
(Punkt G) korrekt.

**Kritischste Runde:** Bei Punkten C (`ImportBookmarksFromChromeEdge`) und E
(`ImportFromFirefox`) hat Qwen die Methoden nicht ergänzt, sondern komplett
neu erfunden – dabei gingen **5 von 7 Chromium-Browsern verloren** (nur
noch Chrome/Edge) und **Firefox rief die falsche Importer-Methode auf**
(`ImportFromBrowser`, die JSON erwartet, statt `ImportBookmarksFromGeckoProfiles`
für SQLite) – zusätzlich wurde in beiden Fällen `null` statt einem echten
Temp-Verzeichnis übergeben, was jeden Import-Versuch stillschweigend zum
Scheitern gebracht hätte (Exception intern abgefangen → leere Liste, kein
sichtbarer Fehler). Das hätte den kompletten automatischen Import lahmgelegt.
Claude hat beide Methoden direkt selbst korrigiert (bekannter Originalcode
aus dem Task-Kontext, rein mechanische Ergänzung der Browser-Markierung),
keine weitere Qwen-Runde nötig.

**Nachtrag nach erstem Live-Test:** "Quelle"-Spalte blieb bei den 758
bestehenden Bookmarks leer, weil das Upsert-Pattern (Browser nur beim
Erst-Insert setzen, wie bei IsFavorite) bei bereits vorhandenen Zeilen nur
Url/Title aktualisiert, nicht Browser. Von Claude direkt gefixt: Browser wird
beim Konflikt jetzt einmalig nachgefüllt, *nur* wenn es noch leer ist
(`CASE WHEN Bookmarks.Browser = '' THEN excluded.Browser ELSE Bookmarks.Browser END`)
– sticky-Verhalten für die Zukunft bleibt erhalten.

**Live-Test (Sven):** Kompiliert, startet, Bookmark-Anzahl blieb stabil
(kein Datenverlust trotz der zwischenzeitlich kaputten Importer-Versuche,
da diese vor dem Schreiben korrigiert wurden). "Quelle"-Spalte füllt sich
schrittweise (aktiv gescannte Browser sofort, historische HTML-/JSON-
Importe erst nach erneutem manuellem Re-Import) – als "Manuell" bei einem
erneuten HTML-Import bestätigt. Akzeptables, dokumentiertes Verhalten laut
Sven ("ist manuell aber egal").

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach mehreren Korrekturen)
- [x] App startet ohne Crash mit bestehender DB (nicht-destruktive
      Migration)
- [x] Neue Spalte "Quelle" zeigt korrekten Browsernamen (für neu/erneut
      gescannte Einträge; historische Fülllung schrittweise beim nächsten
      Kontakt mit der jeweiligen Quelle)
- [x] Manuell importierte Bookmarks zeigen "Manuell" (bestätigt)
- [x] Demo-Bookmarks zeigen "Demo" (Code korrekt, nicht separat live
      getestet, da DB nicht leer)
- [x] Browser-Zuordnung bleibt nach Neustart erhalten (in DB persistiert)
- [x] Favoriten, Suche, Export, Statusanzeige, Öffnen-per-Klick weiterhin
      funktionsfähig (keine dieser Funktionen wurde berührt)

**Task 018 abgeschlossen.**

---

## Nächster Schritt

Task 019: eigentliches Browser-Filter-UI (Dropdown/Sidebar), das auf der
jetzt gespeicherten `Browser`-Spalte aufbaut. Danach: Ordnerstruktur-Ansicht.
