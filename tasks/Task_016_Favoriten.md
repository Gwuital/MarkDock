# Arbeitsvorlage – Task 016

## Task-ID

`Task_016_Favoriten`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
/read docs/08_Funktionen.md
```

**Hintergrund:** `08_Funktionen.md` Abschnitt 8 beschreibt Favoriten als
eigene Funktion: Sternsymbol ★, Toggle-Verhalten, Speicherung in SQLite,
"Favorit bleibt nach Neustart erhalten".

**Wichtige Design-Überlegung, die den Umfang dieses Tasks erklärt:** Der
Import läuft bei **jedem** App-Start neu und schreibt alle gefundenen
Bookmarks per `INSERT OR REPLACE` in die Datenbank. `REPLACE` ersetzt die
komplette Zeile – würde also bei jedem Neustart den Favoriten-Status
zurücksetzen, sobald ein Bookmark erneut importiert wird. Deshalb wird die
Upsert-Logik an **drei Stellen** (Start-Import, Demo-Fallback,
HTML/JSON-Import-Button) von `INSERT OR REPLACE` auf ein echtes `INSERT ...
ON CONFLICT DO UPDATE` umgestellt, das nur `Url`/`Title` aktualisiert und
`IsFavorite` bei bereits vorhandenen Zeilen unangetastet lässt. Das ist
notwendig, damit Favoriten überhaupt funktionieren – kein Scope-Creep,
sondern Voraussetzung für dieses eine Feature.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

`MainWindow.xaml` – aktueller Stand:

```xml
<Window x:Class="MarkDock.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MarkDock" Height="450" Width="600"
        ContentRendered="MainWindow_Loaded">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>   <!-- Search -->
            <RowDefinition Height="Auto"/>   <!-- Import Button -->
            <RowDefinition Height="*"/>      <!-- List -->
            <RowDefinition Height="Auto"/>   <!-- Status -->
        </Grid.RowDefinitions>

        <TextBox x:Name="SearchBox"
                 Grid.Row="0"
                 Margin="0,0,0,10"
                 Height="30"
                 VerticalContentAlignment="Center"
                 FontSize="14"
                 TextChanged="SearchBox_TextChanged"/>

        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,10">
            <Button x:Name="ImportHtmlButton"
                    Content="HTML importieren..."
                    Margin="0,0,10,0"
                    Height="30"
                    Click="ImportHtmlButton_Click"/>

            <Button x:Name="ExportButton"
                    Content="Exportieren..."
                    Height="30"
                    Click="ExportButton_Click"/>
        </StackPanel>

        <ListView x:Name="BookmarksListView"
                  Grid.Row="2"
                  MouseDoubleClick="BookmarksListView_MouseDoubleClick">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Titel" DisplayMemberBinding="{Binding Title}" Width="250"/>
                    <GridViewColumn Header="URL"   DisplayMemberBinding="{Binding Url}"  Width="300"/>
                </GridView>
            </ListView.View>
        </ListView>

        <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Left" Margin="0,5,0,5">
            <TextBlock x:Name="StatusText" Text="0 Bookmarks"/>
        </StackPanel>
    </Grid>
</Window>
```

`MainWindow.xaml.cs` – relevante Ausschnitte (Rest der Datei bleibt
unverändert – alle Importer-Methoden, Export-Methoden, Suche etc. sind
**nicht** betroffen):

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
    imported.AddRange(ImportFromFirefox());

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

```csharp
private void ImportHtmlButton_Click(object sender, RoutedEventArgs e)
{
    var imported = ImportFromHtmlOrJson();

    if (imported.Count == 0)
    {
        MessageBox.Show("Keine Bookmarks gefunden.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    string dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarkDock", "markdock.db");

    using var connection = new SqliteConnection($"Data Source={dbPath}");
    connection.Open();

    int countBefore;
    using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM Bookmarks;", connection))
    {
        countBefore = Convert.ToInt32(countCmd.ExecuteScalar());
    }

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

    int countAfter;
    using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM Bookmarks;", connection))
    {
        countAfter = Convert.ToInt32(countCmd.ExecuteScalar());
    }

    int newlyAdded = countAfter - countBefore;
    int alreadyExisted = imported.Count - newlyAdded;

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

    RefreshDisplayedBookmarks();

    MessageBox.Show($"{imported.Count} Bookmarks verarbeitet: {newlyAdded} neu hinzugefügt, {alreadyExisted} bereits vorhanden (aktualisiert).", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
}
```

```csharp
public class Bookmark
{
    public string Title { get; set; } = "";
    public string Url   { get; set; } = "";
}
```

---

## 2. Auftrag (genau EIN Problem)

Füge **Favoriten-Markierung** hinzu: Sternsymbol pro Bookmark, klickbar zum
Umschalten, persistiert in SQLite, übersteht Neustart und erneuten Import.

1. **Modell erweitern:** `Bookmark`-Klasse bekommt
   `public bool IsFavorite { get; set; } = false;`

2. **Schema-Erweiterung (nicht-destruktiv):** In `AttemptImportAndLoad()`,
   nach dem bestehenden `UrlKey`-Migrationscheck, zusätzlich prüfen, ob die
   Spalte `IsFavorite` existiert (gleiches Muster: `PRAGMA table_info`, nach
   `"IsFavorite"` suchen). Falls nicht vorhanden:
   ```sql
   ALTER TABLE Bookmarks ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;
   ```
   in try/catch (falls die Spalte doch schon existiert, Fehler ignorieren).
   **Bewusst kein Drop+Recreate wie beim `UrlKey`-Fall** – hier soll die
   bestehende Datenmenge (Favoriten eingeschlossen, sobald vorhanden)
   erhalten bleiben, `ALTER TABLE ADD COLUMN` reicht und ist nicht
   destruktiv.

3. **Upsert-Logik an allen drei Stellen** (Start-Import, Demo-Fallback in
   `AttemptImportAndLoad()`, sowie in `ImportHtmlButton_Click()`) von
   `INSERT OR REPLACE` auf folgendes Muster umstellen:
   ```sql
   INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite)
   VALUES (@urlKey, @url, @title, 0)
   ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title;
   ```
   Wichtig: `IsFavorite` wird bei einem Konflikt (Bookmark existiert schon)
   **nicht** überschrieben – nur bei einem echten Neueintrag wird es mit
   `0` initialisiert.

4. **SELECT-Abfragen erweitern** (Start-Load und Reload nach HTML/JSON-
   Import) von `SELECT Url, Title FROM Bookmarks;` auf
   `SELECT Url, Title, IsFavorite FROM Bookmarks;` und beim Befüllen der
   `Bookmark`-Objekte `IsFavorite = reader.GetInt32(2) == 1` mit übernehmen.

5. **UI:** Neue `GridViewColumn` in der `ListView` **vor** der "Titel"-
   Spalte, Header `"★"`, schmale feste Breite (z. B. `Width="30"`). Zelle
   zeigt `"★"` wenn `IsFavorite == true`, sonst `"☆"` (leerer Stern) – dazu
   einen einfachen `IValueConverter` (`BoolToStarConverter`) schreiben und
   als `Window.Resources` registrieren. Die Zelle selbst ist ein `Button`
   mit minimalem Styling (kein Rahmen nötig, `Content="{Binding IsFavorite,
   Converter={StaticResource BoolToStarConverter}}"`), `Click` löst
   `ToggleFavorite_Click(object sender, RoutedEventArgs e)` aus.

6. **Neuer Click-Handler** `ToggleFavorite_Click`:
   - Ermittelt das zugehörige `Bookmark`-Objekt über
     `((FrameworkElement)sender).DataContext as Bookmark`
   - Kehrt `IsFavorite` im Objekt um (`bm.IsFavorite = !bm.IsFavorite;`)
   - Schreibt den neuen Status per `UPDATE Bookmarks SET IsFavorite = @fav
     WHERE UrlKey = @urlKey;` in die DB (eigene, kurze `SqliteConnection`,
     `UrlKey` über `NormalizeUrl(bm.Url)` berechnen)
   - Ruft `RefreshDisplayedBookmarks()` auf, damit die Sternanzeige
     aktualisiert wird

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Kein Sortieren nach Favorit, kein separater "Nur Favoriten"-Filter – nur
  Anzeige + Umschalten, wie im PRD beschrieben
- Keine Änderung an den Importer-Methoden selbst (`ImportBookmarksFromChromeEdge`,
  `ImportFromDuckDuckGo`, `ImportFromFirefox`, HTML-/JSON-Parser) – die
  liefern weiterhin nur `{Title, Url}`, `IsFavorite` wird ausschließlich in
  der DB-Schicht gesetzt
- Keine Änderung an Export (CSV/JSON/HTML enthalten weiterhin nur Title/Url,
  `IsFavorite` wird nicht mit exportiert – das wäre ein eigener Task)
- Keine neue Dependency
- Weiterhin synchron, kein async

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – falls der volle Stand einer der
  beiden Dateien gebraucht wird und nicht komplett vorliegt: kurz
  nachfragen ist hier ok

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash, auch mit der bestehenden `markdock.db` (nicht-
      destruktive Migration greift automatisch)
- [ ] Sternsymbol sichtbar pro Bookmark, Klick schaltet um
- [ ] Favorit bleibt nach Neustart der App erhalten
- [ ] Favorit bleibt **auch nach erneutem automatischem Import** erhalten
      (nicht durch `INSERT OR REPLACE` zurückgesetzt)
- [ ] Bestehende Bookmarks (aus vorheriger Nutzung) bleiben nach der
      Migration vollständig erhalten – kein Datenverlust
- [ ] Suche, Export, Öffnen-per-Klick, Statusanzeige funktionieren
      weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Mehrere Korrekturrunden nötig, größter Task bisher:**

**Runde 1:** `AttemptImportAndLoad()` komplett neu geschrieben statt ergänzt
(erfundene `_connectionString`, `CalculateUrlKey`, `LoadDemoBookmarks()` als
List-Rückgabe obwohl void, `UrlKey`-Migration destruktiv verändert,
`_allBookmarks` nirgends mehr befüllt). Sehr strikter Korrektur-Prompt mit
exaktem Ausgangscode und nur 4 erlaubten Änderungen nachgeschickt.

**Runde 2:** Fix korrekt, aber unvollständig – IsFavorite-Spalten-Migration
fehlte komplett (hätte bei Svens bestehender 758-Bookmark-DB sofort mit
"no such column" abgestürzt). Gezielte Ergänzung nachgefordert, korrekt
geliefert.

**Runde 3 (ImportHtmlButton_Click/XAML/ToggleFavorite_Click):** Erneut
komplett neu erfunden – eigener Dateidialog nur für HTML (Task 007 JSON-
Import verloren), `countBefore`/`countAfter`-Logik aus Task 008 verloren,
`_allBookmarks` nie neu geladen, ListView komplett durch DataGrid ersetzt
(hätte SearchBox/Buttons/StatusText weggerissen). Erneut sehr strikter
Prompt mit komplettem Ist-Zustand aller betroffenen Teile (A–D) und exakt
definierten erlaubten Änderungen.

**Runde 4:** Inhaltlich korrekt (ListView blieb erhalten, Upsert/Select
korrekt erweitert, Bookmark-Modell korrekt), aber zwei technische Lücken:
fehlendes `xmlns:local` für den `BoolToStarConverter` in der XAML, fehlende
`using System.Windows.Data;`/`using System.Globalization;` für den neuen
Converter. Beides von Claude direkt korrigiert (reine Housekeeping-Fixes,
kein Grund für weitere Qwen-Runde). `ToggleFavorite_Click` fehlte in der
Rückgabe, wurde aus einem früheren, bereits vollständig spezifizierten
Claude-Vorschlag übernommen.

Gesamte Datei anschließend einmal komplett neu geschrieben (str_replace
schlug wiederholt an vermuteten Zeilenumbruch-Unterschieden fehl), um
Konsistenz sicherzustellen.

**Live-Test (Sven):** App kompiliert und startet mit bestehender 758-
Bookmark-Datenbank (nicht-destruktive Migration funktioniert). Sterne
sichtbar, Klick füllt/leert sie korrekt, Zustand übersteht Neustart – der
kritische Fall aus der DoD (Favorit übersteht erneuten Import) damit
nachweislich erfüllt. Kleinere kosmetische Anmerkung von Sven: Sternsymbole
wirken etwas klein – für späteren Politur-Task vorgemerkt, kein Blocker.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach mehreren Korrekturen)
- [x] App startet ohne Crash mit bestehender DB (nicht-destruktive
      Migration)
- [x] Sternsymbol sichtbar, Klick schaltet um
- [x] Favorit bleibt nach Neustart erhalten
- [x] Favorit bleibt auch nach erneutem automatischem Import erhalten
      (Upsert-Umstellung an allen drei Stellen funktioniert)
- [x] Bestehende Bookmarks vollständig erhalten, kein Datenverlust
- [x] Suche, Export, Öffnen-per-Klick, Statusanzeige weiterhin
      funktionsfähig

**Task 016 abgeschlossen.**

---

## Vorgemerkt für später (kosmetisch)

Stern-Icon-Größe in der Favoriten-Spalte etwas klein – bei Gelegenheit
vergrößern (z. B. größere FontSize auf dem Button-Content).

## Nächster Schritt

Ursprüngliche Priorisierung: Browser-Filter, Ordnerstruktur-Ansicht.
