# Arbeitsvorlage – Task 021

## Task-ID

`Task_021_Ordnerpfad_Firefox`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Zweiter von drei Schritten Richtung Ordnerstruktur-Ansicht
(Task 020 hat das für Chromium-Browser bereits erledigt). Firefox' Schema
ist anders: `moz_bookmarks` hat eine Baumstruktur über die Spalte `parent`
(verweist auf die `id` eines anderen Eintrags in derselben Tabelle – auch
Ordner sind Zeilen in `moz_bookmarks`, mit `type = 2`; `type = 1` sind
echte Bookmarks, `type = 3` sind Trenner).

Bisher wird nur `url` und `title` der Bookmark-Zeilen gelesen, die
Ordnerstruktur wird ignoriert. Dieser Task lädt **alle** Zeilen (Bookmarks
UND Ordner), baut daraus eine Id→(Parent,Titel,Typ)-Landkarte im Speicher,
und läuft für jedes Bookmark die `parent`-Kette hoch, um den vollen
Ordnerpfad zu rekonstruieren.

**Wichtig – bitte NICHT die ganze Datei neu schreiben, nur exakt diese eine
Methode ändern.**

**Aktueller Code, der angepasst wird** — `ImportBookmarksFromGeckoProfiles`,
komplett:

```csharp
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

    return bookmarks;
}
```

`Bookmark`-Modell hat bereits `Folder` (aus Task 020):
```csharp
public string Folder { get; set; } = "";
```

---

## 2. Auftrag (genau EIN Problem)

Baue `ImportBookmarksFromGeckoProfiles` so um, dass sie den Ordnerpfad pro
Bookmark ermittelt:

1. **SQL-Abfrage ändern:** statt nur Bookmarks (`type = 1`) zu laden, jetzt
   **alle** Zeilen laden (Bookmarks UND Ordner), inklusive `id`, `parent`,
   `type`, `title`, und `url` (per LEFT JOIN, damit auch Ordner-Zeilen ohne
   `url` mitkommen):
   ```csharp
   using var cmd = new SqliteCommand(
       "SELECT moz_bookmarks.id, moz_bookmarks.parent, moz_bookmarks.type, moz_bookmarks.title, moz_places.url " +
       "FROM moz_bookmarks LEFT JOIN moz_places ON moz_bookmarks.fk = moz_places.id;",
       connection);
   ```

2. **Alle Zeilen zuerst in eine Landkarte einlesen** (id → Parent, Typ,
   Titel), und Bookmark-Kandidaten (type == 1, url vorhanden) separat
   merken:
   ```csharp
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
   ```

3. **Für jedes Bookmark die `parent`-Kette hochlaufen**, um den Ordnerpfad
   zu bauen (nur Knoten mit `Type == 2`, also echte Ordner, werden
   berücksichtigt; leere Titel werden übersprungen; die Kette wird
   umgekehrt zusammengesetzt, da man von unten nach oben läuft):
   ```csharp
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
   ```
   (Der `safetyCounter` verhindert eine Endlosschleife, falls die
   `parent`-Kette durch defekte Daten zirkulär wäre – defensiv, wie an
   anderen Stellen im Projekt üblich.)

4. Der Rest der Methode (Temp-Kopie, `try`/`catch` um das Profil, Directory-
   Aufräumen) bleibt exakt wie er ist.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Nur `ImportBookmarksFromGeckoProfiles` ändern – `ImportFromFirefox`
  (die äußere Schleife über Firefox/Zen/Floorp/Waterfox) bleibt unangetastet
- Keine Änderung an Chromium-Import (Task 020), DuckDuckGo, HTML/JSON-Import
- Keine Änderung an Migration, Upsert, SELECT, XAML – die `Folder`-Spalte
  existiert bereits aus Task 020 und wird einfach mitbefüllt
- Keine neue Dependency
- Weiterhin synchron, kein async

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `ImportBookmarksFromGeckoProfiles`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen**

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash
- [ ] Firefox-Bookmarks zeigen jetzt einen Ordnerpfad in der "Ordner"-Spalte
      (z. B. "Lesezeichen-Symbolleiste/Arbeit")
- [ ] Bookmarks direkt in der Symbolleiste (kein Unterordner) zeigen nur den
      obersten Ordnernamen, keine leere Zelle
- [ ] Chromium-Ordnerpfade (Task 020), Browser-Filter (Task 019), Favoriten,
      Suche, Export, Statusanzeige, Öffnen-per-Klick funktionieren weiterhin
      unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – nur Fragment:** Erste Rückgabe enthielt nur den inneren
Landkarten-Teil, komplette äußere Struktur (Profil-Schleife, try/catch,
Temp-Kopie) fehlte, und der Parameter `profilesPath` war aus der Signatur
verschwunden – hätte den Aufruf aus `ImportFromFirefox()` gebrochen.
Vollständige Methode nachgefordert.

**Runde 2 – kritischer Bug:** SQL-Abfrage hatte den `LEFT JOIN` mit
`moz_places` verloren (`SELECT id, parent, type, title, url FROM
moz_bookmarks ...` – `moz_bookmarks` hat gar keine `url`-Spalte). Hätte bei
jedem Firefox-Profil eine SQL-Exception ausgelöst, vom bestehenden
try/catch abgefangen – Ergebnis: 0 Firefox-Bookmarks, komplett unbemerkt.
Korrektur angefordert.

**Runde 3 – abgenommen.** `LEFT JOIN` korrekt ergänzt, sonst nichts
verändert. Landkarten-Aufbau (Id → Parent/Type/Title), Elternketten-Logik
mit `safetyCounter` gegen Zirkelbezüge, Null-Behandlung für `parent`/`title`/
`url` – alles korrekt. Kleine Abweichung: Temp-Handling nutzt eine einzelne
Datei statt eines Unterordners – funktional gleichwertig, kein Blocker.

Live-Test (Sven, Screenshot): Firefox zeigt jetzt echte Ordnerpfade
("menu", "menu/Mozilla Firefox", "toolbar") – verschachtelte Pfade korrekt
zusammengesetzt. Die Namen sind Firefox' interne technische Bezeichner
(nicht die übersetzten Anzeigenamen wie bei Edge/"Favoritenleiste"), aber
inhaltlich korrekt und funktional ausreichend für die geplante Baum-UI.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach zwei Korrekturen)
- [x] App startet ohne Crash
- [x] Firefox-Bookmarks zeigen Ordnerpfad in der "Ordner"-Spalte
- [x] Bookmarks in der obersten Ebene zeigen den Ordnernamen, keine leere
      Zelle (bestätigt: "menu", "toolbar")
- [x] Chromium-Ordnerpfade, Browser-Filter, Favoriten, Suche, Export,
      Statusanzeige, Öffnen-per-Klick weiterhin funktionsfähig

**Task 021 abgeschlossen.**

---

## Nächster Schritt

Task 022: die eigentliche Baum-UI (aufklappbare Ordner-Gruppen statt
flacher Liste), aufbauend auf den jetzt gespeicherten `Folder`-Pfaden aus
Task 020+021.
