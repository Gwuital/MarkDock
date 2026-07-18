# Arbeitsvorlage – Task 013

## Task-ID

`Task_013_Weitere_Chromium_Browser`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Opera, Brave, Vivaldi, SRWare Iron und Comodo Dragon sind
alle Chromium-basiert und nutzen **exakt dasselbe** Bookmark-JSON-Format wie
Chrome/Edge. Die bestehende Methode `ImportFromBrowser(browserName,
sourcePath, tempDir)` ist bereits generisch gebaut (Task 002) – es müssen
nur weitere Pfade ergänzt werden, kein neuer Parser nötig.

**Hinweis zur Pfad-Genauigkeit:** Die folgenden Pfade sind Standard-
Installationsorte, aber nicht zu 100% garantiert bei jeder Version/jedem
Installer. Das ist unkritisch – falls ein Pfad bei einem Nutzer nicht
stimmt, greift automatisch die bestehende Fehlerbehandlung
(`File.Exists`-Check in `ImportFromBrowser`): der Browser wird einfach
übersprungen, kein Crash, kein Fehler sichtbar (ADR-999 Regel 9/10).

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

```csharp
private List<Bookmark> ImportBookmarksFromChromeEdge()
{
    var result = new List<Bookmark>();
    string tempDir = Path.Combine(Path.GetTempPath(), "MarkDock_Import");
    Directory.CreateDirectory(tempDir);

    // Chrome
    result.AddRange(ImportFromBrowser(
        "Chrome",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                      @"Google\Chrome\User Data\Default\Bookmarks"),
        tempDir));

    // Edge
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
            CollectBookmarksRecursive(rootProp.Value, bookmarks);
        }
    }
    catch
    {
        // Fehler beim Kopieren oder Parsen → Browser einfach überspringen
    }

    return bookmarks;
}
```

`ImportFromBrowser` selbst und `CollectBookmarksRecursive` bleiben
**unverändert** – nur `ImportBookmarksFromChromeEdge` wird erweitert.

---

## 2. Auftrag (genau EIN Problem)

Erweitere `ImportBookmarksFromChromeEdge()` um fünf weitere Chromium-Browser.
Statt fünf weitere fast identische `ImportFromBrowser(...)`-Aufrufe
hintereinanderzuschreiben: eine kleine Liste von `(Name, Pfad)`-Paaren
anlegen und per Schleife durchgehen (Chrome/Edge dürfen gerne mit in diese
Liste, das vereinfacht den Code, ohne das Verhalten zu ändern):

```
Chrome:        %LOCALAPPDATA%\Google\Chrome\User Data\Default\Bookmarks
Edge:          %LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Bookmarks
Opera:         %APPDATA%\Opera Software\Opera Stable\Bookmarks
Brave:         %LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data\Default\Bookmarks
Vivaldi:       %LOCALAPPDATA%\Vivaldi\User Data\Default\Bookmarks
SRWare Iron:   %LOCALAPPDATA%\Chromium\User Data\Default\Bookmarks
Comodo Dragon: %LOCALAPPDATA%\Comodo\Dragon\User Data\Default\Bookmarks
```

(Opera nutzt bewusst `%APPDATA%` – Roaming, nicht Local, wie bei Firefox in
Task 012. Alle anderen nutzen `%LOCALAPPDATA%`.)

Jeder Eintrag wird wie bisher über `ImportFromBrowser(name, pfad, tempDir)`
verarbeitet, Ergebnisse werden zusammengeführt und zurückgegeben – exakt
dasselbe Verhalten wie bisher, nur mit mehr Quellen.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- `ImportFromBrowser` und `CollectBookmarksRecursive` bleiben unverändert –
  kein neuer Parser, keine neue Logik, nur mehr Aufrufe/Pfade
- Keine Änderung an DuckDuckGo- oder Firefox-Import
- Keine Änderung an Persistenz, Suche, Export, Statusanzeige
- Keine neue Dependency
- Kein Arc, kein Tor Browser, kein Mullvad Browser (bewusst nicht Teil
  dieses Tasks – andere Installationslogik bzw. explizit nicht gewünscht)
- Weiterhin synchron, kein async

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `ImportBookmarksFromChromeEdge()`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  ausreichend Kontext

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash, unabhängig davon welche der Browser
      installiert sind
- [ ] Falls einer der neuen Browser installiert ist und Bookmarks hat:
      diese erscheinen automatisch nach dem Start
- [ ] Chrome/Edge-Import funktioniert weiterhin unverändert (Regressionstest)
- [ ] Nicht gefundene Browser führen zu keinem sichtbaren Fehler
- [ ] DuckDuckGo/Firefox-Import, Persistenz, Dedup, Suche, Export,
      Statusanzeige funktionieren weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – Bug gefunden:** `ImportBookmarksFromChromeEdge()` wurde als
`static` deklariert, ruft aber die weiterhin Instanzmethode
`ImportFromBrowser(...)` auf – Compile-Fehler. Korrektur angefordert.

**Runde 2 – abgenommen.** Fix korrekt: `static` entfernt.

Code-Review: Alle 7 Browser-Pfade korrekt (Opera bewusst über
`ApplicationData`/Roaming wie bei Firefox, restliche 6 über
`LocalApplicationData`), Schleife statt Wiederholung sauber umgesetzt,
zusätzliches try/catch pro Browser als Sicherheitsnetz (über die bereits
vorhandene Absicherung in `ImportFromBrowser` hinaus). `ImportFromBrowser`
und `CollectBookmarksRecursive` unverändert. Kleine, unaufgeforderte aber
unproblematische Verbesserung: Temp-Ordner wird nach dem Lauf aufgeräumt
(vorher blieb er liegen).

Live-Test: App kompiliert und startet. Brave-Bookmarks erfolgreich
automatisch importiert. Opera/Vivaldi/SRWare Iron/Comodo Dragon nicht bei
Sven installiert, daher nicht live prüfbar – laufen aber über dieselbe,
nun bestätigte Schleifen-Logik wie Brave, daher hohe Zuversicht per
Code-Review.

Definition of Done erfüllt (soweit prüfbar):

- [x] Projekt kompiliert ohne manuelle Fixes (nach Korrektur)
- [x] App startet ohne Crash
- [x] Brave-Bookmarks erscheinen automatisch – exemplarisch für alle neuen
      Quellen bewiesen
- [x] Chrome/Edge-Import weiterhin funktionsfähig (Regressionstest)
- [x] Nicht installierte Browser führen zu keinem Fehler
- [x] DuckDuckGo/Firefox-Import, Persistenz, Dedup, Suche, Export,
      Statusanzeige weiterhin funktionsfähig

**Task 013 abgeschlossen.**

---

## Stand MarkDock gesamt

Automatischer Import aus: Chrome, Edge, Opera, Brave, Vivaldi, SRWare Iron,
Comodo Dragon (alle Chromium-JSON), DuckDuckGo (heuristisch, meist leer
wegen Verschlüsselung), Firefox (places.sqlite). Dazu manueller HTML-/
JSON-Import (beide Richtungen), SQLite-Persistenz, URL-Dedup, Suche,
Export (CSV/JSON/HTML), Statusanzeige. Alle 13 Tasks abgenommen.

## Nächster Schritt

Offen: Zen/Floorp/Waterfox (Firefox-basiert, Task 014 wäre analog zu
Task 013 nur mit `places.sqlite`-Pfaden), danach die ursprüngliche Liste
(Fortschrittsanzeige, Favoriten, Browser-Filter, Ordneransicht).
