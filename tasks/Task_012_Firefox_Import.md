# Arbeitsvorlage – Task 012

## Task-ID

`Task_012_Firefox_Import`

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

**Hintergrund:** `05_Browser_Importer.md` Abschnitt 8 beschreibt Firefox als
"geplant" – bisher gibt's nur den Umweg über manuellen JSON-Backup-Export
(Task 007). Dieser Task baut den echten, automatischen Live-Import direkt
aus Firefox' eigener Datenbank, analog zu Chrome/Edge (Task 002) und
DuckDuckGo (Task 005).

**Firefox-Speicherort und -Format (laut Doku):**

```
%APPDATA%\Mozilla\Firefox\Profiles\<profil>.default\places.sqlite
```

SQLite-Datenbank mit den Tabellen `moz_bookmarks` und `moz_places`. Im
Gegensatz zu DuckDuckGo ist das Schema hier **dokumentiert und stabil**
(seit vielen Firefox-Versionen unverändert) – keine Heuristik nötig wie bei
DuckDuckGo, ein fester JOIN reicht:

```sql
SELECT moz_places.url, moz_bookmarks.title
FROM moz_bookmarks
JOIN moz_places ON moz_bookmarks.fk = moz_places.id
WHERE moz_bookmarks.type = 1
  AND moz_places.url LIKE 'http%';
```

(`moz_bookmarks.type = 1` = Lesezeichen, nicht Ordner/Trenner;
`moz_bookmarks.fk` verweist auf `moz_places.id`.)

**Wichtige Besonderheit:** Firefox kann mehrere Profile haben
(`%APPDATA%\Mozilla\Firefox\Profiles\` enthält einen Ordner pro Profil, z. B.
`abc12345.default-release`). Es reicht, **alle** Profil-Ordner zu
durchsuchen und deren `places.sqlite` (falls vorhanden) zu importieren –
analog zum DuckDuckGo-Task, der auch alle `DuckDuckGo*`-Package-Ordner
durchsucht.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

Die relevante Methode `AttemptImportAndLoad()` und die Struktur der
bestehenden Importer-Methoden (`ImportBookmarksFromChromeEdge`,
`ImportFromDuckDuckGo`) in `MainWindow.xaml.cs` – aktueller Stand (Ausschnitt,
Rest der Datei unverändert vom letzten abgenommenen Task 011):

```csharp
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

    // 3. Neue Tabelle erstellen, falls nicht vorhanden
    using (var cmd = new SqliteCommand(
        "CREATE TABLE IF NOT EXISTS Bookmarks (UrlKey TEXT PRIMARY KEY, Url TEXT NOT NULL, Title TEXT NOT NULL);",
        connection))
    {
        cmd.ExecuteNonQuery();
    }

    // 4. Chrome/Edge/DuckDuckGo Import ausführen
    var imported = ImportBookmarksFromChromeEdge();
    imported.AddRange(ImportFromDuckDuckGo());

    // 5. Importierte Bookmarks in DB upserten
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

    // 6. Alle Bookmarks aus DB laden
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

    // 7. Fallback: Demo-Daten, falls DB leer ist
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
```

Analoge Struktur der bestehenden `ImportFromDuckDuckGo()` (zum Nachbau-Muster
für Multi-Profil-Suche + Temp-Kopie + SQLite-Zugriff – **nicht** verändern,
nur als Vorlage):

```csharp
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
                // ... (heuristische Tabellen-/Spaltensuche, hier nicht relevant)
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
```

---

## 2. Auftrag (genau EIN Problem)

Ergänze einen **echten Firefox-Import** als vierte automatische Quelle
(neben Chrome, Edge, DuckDuckGo):

1. Neue Methode `ImportFromFirefox()`, die eine `List<Bookmark>` liefert
   (leere Liste bei Fehler/Fehlen, kein Crash).

2. **Profil-Ordner suchen:**
   ```
   %APPDATA%\Mozilla\Firefox\Profiles\
   ```
   Alle Unterordner durchsuchen (`Directory.GetDirectories(...)`, kein
   Namensmuster nötig, da Profile beliebig heißen können – einfach alle
   Ordner prüfen). Falls der `Profiles`-Ordner selbst nicht existiert: leere
   Liste, fertig.

3. Für jeden Profil-Ordner: prüfen, ob `places.sqlite` existiert. Falls
   nicht: überspringen.

4. Falls vorhanden: Datei in denselben Temp-Mechanismus wie bei
   Chrome/Edge/DuckDuckGo kopieren (read-only-Prinzip), dann die Kopie mit
   `Microsoft.Data.Sqlite` öffnen.

5. **Festes SQL** (kein Heuristik nötig, Schema ist dokumentiert):
   ```sql
   SELECT moz_places.url, moz_bookmarks.title
   FROM moz_bookmarks
   JOIN moz_places ON moz_bookmarks.fk = moz_places.id
   WHERE moz_bookmarks.type = 1
     AND moz_places.url LIKE 'http%';
   ```
   Für jede Ergebniszeile: `Bookmark { Title = title, Url = url }`
   übernehmen (nur wenn `url` nicht leer ist – `title` kann in Firefox
   gelegentlich `NULL` sein, dann leeren String verwenden).

6. Alles in try/catch – ein defektes/gesperrtes Profil wird übersprungen,
   nicht die ganze Methode abgebrochen.

7. Ergebnis von `ImportFromFirefox()` in `AttemptImportAndLoad()` mit
   einbeziehen (`imported.AddRange(ImportFromFirefox());` direkt nach der
   bestehenden Zeile für DuckDuckGo) – landet dann automatisch im
   bestehenden Upsert-Ablauf.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Kein Ordner/Struktur-Import (`moz_bookmarks` hat eine Baumstruktur über
  `parent`-Spalte – für diesen Task ignorieren, nur flache Liste wie beim
  restlichen Projekt)
- Keine Änderung an Chrome/Edge/DuckDuckGo-Importlogik oder deren
  Reihenfolge in `AttemptImportAndLoad()`
- Keine Änderung an URL-Normalisierung, Persistenz, Suche, Statusanzeige,
  Export
- Keine neue NuGet-Dependency (`Microsoft.Data.Sqlite` ist schon da)
- Keine neuen Felder im `Bookmark`-Modell
- Weiterhin synchron, kein async
- Firefox-Datei muss (wie bei allen anderen Quellen) **nur gelesen** werden,
  niemals verändert – ausschließlich über die kopierte Temp-Datei zugreifen

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt der Methode `AttemptImportAndLoad()`
  (nur die eine neue Zeile für den Aufruf) und der neuen Methode
  `ImportFromFirefox()`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  ausreichend Kontext

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash, unabhängig davon ob Firefox installiert ist
- [ ] Falls Firefox mit Bookmarks installiert ist: diese erscheinen nach dem
      Start automatisch in der Liste (keine manuelle Aktion nötig)
- [ ] Falls Firefox nicht installiert ist: kein Fehler sichtbar, restliche
      Quellen funktionieren normal
- [ ] Keine Veränderung der Original-`places.sqlite` (nur read-only über
      Temp-Kopie)
- [ ] Chrome/Edge/DuckDuckGo-Import, Persistenz, Dedup, Suche, Export,
      Statusanzeige funktionieren weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Sessionprobleme vor der eigentlichen Umsetzung:** Mehrere "Unknown tool"-
Fehler in Hexabyte durch einen Browser-Absturz, der den System-Prompt/das
Profil zurücksetzte (Qwen antwortete zwischenzeitlich in JavaScript statt
C#). Nach Profil-Reparatur und einem C#-Kontrolltest lief es wieder normal.
Zusätzliche Verwirrung: der Kontext-Teil (Abschnitt 1, Referenzcode) wurde
versehentlich mehrfach als eigener Auftrag geschickt statt Abschnitt 2+3 –
geklärt, danach korrekt abgeschickt.

**Runde 1 – Code korrekt, aber unvollständig geliefert** (erst zwei
Hilfsmethoden, dann nach Nachfrage die vollständige `ImportFromFirefox()`
Methode separat). Code-Review der finalen Methode: `%APPDATA%\Mozilla\
Firefox\Profiles` korrekt (bewusst Roaming statt Local, anders als bei
Chrome/Edge/DuckDuckGo), alle Profile durchsucht, Read-only-Prinzip
eingehalten (Temp-Kopie, danach sogar aufgeräumt), exaktes SQL wie
vorgegeben, `NULL`-Titel korrekt behandelt (`IsDBNull`), zweistufiges
try/catch. Einbindung in `AttemptImportAndLoad()` war im Auftrag exakt
vorgegeben, daher direkt selbst ergänzt statt erneut anzufragen.

**Runde 2 – Compile-Fehler beim Zusammenbau, aber selbst verursacht:**
Beim Einfügen der neuen Methode ist mir (Claude) versehentlich die
öffnende `#region UI-Logic`-Markierung verlorengegangen →
`CS1028: Unerwartete Präprozessordirektive` durch eine verwaiste
`#endregion`. Kein Qwen-Fehler, direkt selbst korrigiert (eine Zeile).

Live-Test: App kompiliert und startet. Bookmark-Anzahl stieg von 679 auf
758 – Firefox-Bookmarks wurden erfolgreich automatisch importiert.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach Korrektur des
      Region-Fehlers)
- [x] App startet ohne Crash
- [x] Firefox-Bookmarks erscheinen automatisch (679 → 758)
- [x] Keine Veränderung der Original-`places.sqlite` (nur Temp-Kopie)
- [x] Chrome/Edge/DuckDuckGo, Persistenz, Dedup, Suche, Export,
      Statusanzeige weiterhin funktionsfähig

**Task 012 abgeschlossen.**

---

## Stand MarkDock gesamt

MVP + Chrome/Edge/DuckDuckGo/Firefox-Import (alle automatisch beim Start)
+ SQLite-Persistenz + URL-Normalisierung/Dedup + HTML-/JSON-Import (beide
Richtungen, beide Formate) + Neu/Vorhanden-Zählung + Statusanzeige +
Export (CSV/JSON/HTML). Alle 12 Tasks abgenommen. Alle vier ursprünglich
geplanten Browser (Chrome, Edge, DuckDuckGo, Firefox) sind jetzt abgedeckt.

## Nächster Schritt (siehe Priorisierung von Sven)

1. Fortschrittsanzeige beim Import
2. Favoriten-Markierung
3. Browser-Filter
4. Ordnerstruktur-Ansicht
5. Rest (Portable Version, Launcher, Tote-Links-Check, Shortcuts)
