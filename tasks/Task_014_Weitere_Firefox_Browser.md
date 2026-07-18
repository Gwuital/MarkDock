# Arbeitsvorlage – Task 014

## Task-ID

`Task_014_Weitere_Firefox_Browser`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Zen Browser, Floorp und Waterfox sind alle Firefox-Forks
(Gecko-Engine) und nutzen **exakt dieselbe** `places.sqlite`-Struktur wie
Firefox selbst, inklusive derselben Profile-Ordner-Konvention – nur unter
einem anderen Hersteller-Ordnernamen. Genau wie bei Task 013 (Chromium-
Klone) muss kein neuer Parser gebaut werden, nur weitere Profil-Wurzel-
Pfade ergänzt werden.

**Hinweis zur Pfad-Genauigkeit:** Die folgenden Pfade sind die üblichen
Installationsorte, aber (besonders bei den kleineren Projekten Zen/Floorp/
Waterfox) nicht zu 100% garantiert bei jeder Version. Unkritisch – falscher
Pfad heißt einfach "keine Profile gefunden", kein Fehler, kein Crash
(gleiches Verhalten wie bei nicht installierten Browsern).

```
Firefox:  %APPDATA%\Mozilla\Firefox\Profiles
Zen:      %APPDATA%\zen\Profiles
Floorp:   %APPDATA%\Floorp\Profiles
Waterfox: %APPDATA%\Waterfox\Profiles
```

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

```csharp
/// <summary>
/// Importiert Bookmarks aus Firefox (places.sqlite, alle Profile).
/// </summary>
private static List<Bookmark> ImportFromFirefox()
{
    var bookmarks = new List<Bookmark>();

    try
    {
        var profilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles");

        if (!Directory.Exists(profilesPath))
            return bookmarks;

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
                    "SELECT moz_places.url, moz_bookmarks.title FROM moz_bookmarks JOIN moz_places ON moz_bookmarks.fk = moz_places.id WHERE moz_bookmarks.type = 1 AND moz_places.url LIKE 'http%';",
                    connection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var url = reader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        var title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        bookmarks.Add(new Bookmark { Title = title, Url = url });
                    }
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
    }
    catch
    {
        // Bei schwerwiegenden Fehlern: leere Liste zurückgeben
        return new List<Bookmark>();
    }

    return bookmarks;
}
```

Wird aufgerufen aus `AttemptImportAndLoad()` via
`imported.AddRange(ImportFromFirefox());` – **dieser Aufruf bleibt
unverändert**, nur was intern in `ImportFromFirefox()` passiert, wird
erweitert.

---

## 2. Auftrag (genau EIN Problem)

Erweitere `ImportFromFirefox()` so, dass sie **mehrere** Gecko-basierte
Profil-Wurzeln durchsucht statt nur Firefox selbst:

1. Extrahiere die bestehende "einen Profile-Ordner durchsuchen"-Logik
   (alles innerhalb der äußeren `try`, ab `if (!Directory.Exists(profilesPath))`
   bis zum Ende der `foreach`-Schleife über `profileDir`) in eine neue
   private Hilfsmethode, z. B.
   `ImportBookmarksFromGeckoProfiles(string profilesPath)`, die eine
   `List<Bookmark>` zurückgibt.

2. `ImportFromFirefox()` selbst wird zur Schleife über die vier
   Profil-Wurzeln (Firefox, Zen, Floorp, Waterfox aus Abschnitt 1), ruft für
   jede `ImportBookmarksFromGeckoProfiles(...)` auf und führt die Ergebnisse
   zusammen – analog zum Chromium-Muster aus Task 013.

3. Der äußere try/catch-Schutz ("Bei schwerwiegenden Fehlern: leere Liste")
   bleibt sinngemäß erhalten – pro Profil-Wurzel oder als Gesamtabsicherung,
   Qwen darf hier die sauberere Variante wählen, solange ein Fehler bei
   einer Wurzel (z. B. Zen nicht installiert) die anderen nicht verhindert.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Das SQL selbst, die `NULL`-Titel-Behandlung und die Temp-Kopie-Logik
  bleiben exakt wie sie sind – nur der äußere Aufruf-Mechanismus wird
  erweitert
- Keine Änderung an Chrome/Edge/Opera/Brave/Vivaldi/SRWare Iron/Comodo
  Dragon/DuckDuckGo-Import
- Keine Änderung an `AttemptImportAndLoad()` – der Aufruf
  `imported.AddRange(ImportFromFirefox());` bleibt exakt so
- Keine Änderung an Persistenz, Suche, Export, Statusanzeige
- Keine neue Dependency
- Weiterhin synchron, kein async

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `ImportFromFirefox()` und der neuen
  Hilfsmethode `ImportBookmarksFromGeckoProfiles(...)`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  ausreichend Kontext

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash, unabhängig davon welche der vier Browser
      installiert sind
- [ ] Firefox-Import funktioniert weiterhin unverändert (Regressionstest,
      Task 012)
- [ ] Falls Zen/Floorp/Waterfox installiert ist und Bookmarks hat: diese
      erscheinen automatisch nach dem Start
- [ ] Nicht gefundene Profil-Wurzeln führen zu keinem sichtbaren Fehler
- [ ] Chrome/Edge/weitere Chromium-Browser, DuckDuckGo, Persistenz, Dedup,
      Suche, Export, Statusanzeige funktionieren weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – zwei Punkte:** Zen-Pfad-Abweichung (`"ZenBrowser"` statt
vorgegebenem `"zen"`) und ein echter Logikfehler – der äußere try/catch
umschloss die gesamte Schleife über alle vier Profil-Wurzeln, wodurch ein
Fehler bei einer Wurzel (z. B. Zen) auch bereits gesammelte Ergebnisse
anderer Wurzeln (z. B. Firefox) verworfen hätte. Korrektur angefordert.

**Runde 2 – abgenommen.** Beide Punkte korrekt behoben: Zen-Pfad auf
`"zen"` korrigiert, try/catch in die Schleife hineinverschoben (pro
Profil-Wurzel abgesichert). `ImportBookmarksFromGeckoProfiles` (SQL,
NULL-Handling, Temp-Kopie-Logik) unverändert übernommen aus Task 012.
`AttemptImportAndLoad()` nicht angefasst.

Live-Test: App kompiliert und startet, Firefox-Import weiterhin
funktionsfähig (Regressionstest bestanden). Zen/Floorp/Waterfox nicht bei
Sven installiert, daher nicht live prüfbar – laufen aber über dieselbe,
bereits bewährte Firefox-Logik.

Definition of Done erfüllt (soweit prüfbar):

- [x] Projekt kompiliert ohne manuelle Fixes (nach Korrektur)
- [x] App startet ohne Crash
- [x] Firefox-Import weiterhin funktionsfähig (Regressionstest)
- [x] Fehlende Profil-Wurzeln führen zu keinem Fehler
- [x] Restliche Importe, Persistenz, Dedup, Suche, Export, Statusanzeige
      weiterhin funktionsfähig

**Task 014 abgeschlossen.**

---

## Stand MarkDock gesamt

Automatischer Import aus: Chrome, Edge, Opera, Brave, Vivaldi, SRWare Iron,
Comodo Dragon (Chromium-JSON), DuckDuckGo (heuristisch), Firefox, Zen,
Floorp, Waterfox (Gecko/places.sqlite). Dazu manueller HTML-/JSON-Import,
SQLite-Persistenz, URL-Dedup, Suche, Export (CSV/JSON/HTML), Statusanzeige.
Alle 14 Tasks abgenommen. Damit sind praktisch alle relevanten Desktop-
Browser aus Svens Liste automatisch abgedeckt (außer Arc, Tor, Mullvad –
bewusst ausgeklammert, Safari existiert nicht für Windows).

## Nächster Schritt

Ursprüngliche Priorisierung: Fortschrittsanzeige beim Import, Favoriten-
Markierung, Browser-Filter, Ordnerstruktur-Ansicht.
