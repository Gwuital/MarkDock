# Arbeitsvorlage – Task 020

## Task-ID

`Task_020_Ordnerpfad_Chromium`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Erster von drei Schritten Richtung Ordnerstruktur-Ansicht.
Dieser Task erfasst nur den Ordnerpfad für **Chrome/Edge und die anderen
Chromium-Browser** (Opera, Brave, Vivaldi, SRWare Iron, Comodo Dragon – alle
nutzen denselben Parser). Firefox folgt als eigener Task 021, die
eigentliche Baum-UI als Task 022.

**Wichtig – bitte NICHT ganze Methoden neu erfinden, nur exakt wie unten
beschrieben chirurgisch ergänzen.**

**Aktueller Code, der angepasst wird** (Ausschnitte, alles andere in
`MainWindow.xaml.cs` bleibt unangetastet):

**A) `Bookmark`-Modell:**
```csharp
public class Bookmark
{
    public string Title { get; set; } = "";
    public string Url   { get; set; } = "";
    public bool IsFavorite { get; set; } = false;
    public string Browser { get; set; } = "";
}
```

**B) `ImportFromBrowser()` — nur der Aufruf von `CollectBookmarksRecursive`
relevant:**
```csharp
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
```

**C) `CollectBookmarksRecursive()` — komplett:**
```csharp
private void CollectBookmarksRecursive(JsonElement node, List<Bookmark> list)
{
    if (node.ValueKind != JsonValueKind.Object)
        return;

    if (!node.TryGetProperty("type", out JsonElement typeProp))
        return;

    string type = typeProp.GetString();
    if (type == "url")
    {
        // Bookmark-Eintrag
        string title = node.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : "";
        string url   = node.TryGetProperty("url",  out JsonElement urlEl ) ? urlEl.GetString()  : "";
        if (!string.IsNullOrWhiteSpace(url))
            list.Add(new Bookmark { Title = title, Url = url });
    }
    else if (type == "folder")
    {
        // Ordner → rekursiv durch Kinder gehen
        if (node.TryGetProperty("children", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in children.EnumerateArray())
                CollectBookmarksRecursive(child, list);
        }
    }
}
```

**D) Die drei Upsert-Stellen** (Haupt-Import und Demo-Fallback in
`AttemptImportAndLoad()`, sowie in `ImportHtmlButton_Click()`) haben
aktuell dieses Muster (Browser-Backfill aus Task 018 bereits enthalten –
**bitte exakt so übernehmen, NICHT auf die alte Fassung ohne
`Browser = CASE WHEN...` zurückfallen**):
```csharp
using var insertCmd = new SqliteCommand(
    @"INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite, Browser)
      VALUES (@urlKey, @url, @title, 0, @browser)
      ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title, Browser = CASE WHEN Bookmarks.Browser = '' THEN excluded.Browser ELSE Bookmarks.Browser END;",
    connection);
insertCmd.Parameters.AddWithValue("@urlKey", urlKey);
insertCmd.Parameters.AddWithValue("@url", bm.Url);
insertCmd.Parameters.AddWithValue("@title", bm.Title);
insertCmd.Parameters.AddWithValue("@browser", bm.Browser);
insertCmd.ExecuteNonQuery();
```

Und die SELECT-Stellen (zweimal, Start-Load und Reload nach HTML/JSON-
Import):
```csharp
using (var selectCmd = new SqliteCommand("SELECT Url, Title, IsFavorite, Browser FROM Bookmarks;", connection))
using (SqliteDataReader reader = selectCmd.ExecuteReader())
{
    while (reader.Read())
    {
        _allBookmarks.Add(new Bookmark
        {
            Url = reader.GetString(0),
            Title = reader.GetString(1),
            IsFavorite = reader.GetInt32(2) == 1,
            Browser = reader.GetString(3)
        });
    }
}
```

**E) `MainWindow.xaml` — die `GridView`:**
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
    <GridViewColumn Header="Quelle" DisplayMemberBinding="{Binding Browser}" Width="100"/>
    <GridViewColumn Header="Titel" DisplayMemberBinding="{Binding Title}" Width="250"/>
    <GridViewColumn Header="URL"   DisplayMemberBinding="{Binding Url}"  Width="300"/>
</GridView>
```

---

## 2. Auftrag (genau EIN Problem)

Erfasse den Ordnerpfad beim Chromium-Import (Chrome, Edge, Opera, Brave,
Vivaldi, SRWare Iron, Comodo Dragon) und zeige ihn als Spalte an:

1. **A) Bookmark-Modell:** eine Zeile ergänzen:
   `public string Folder { get; set; } = "";`

2. **C) `CollectBookmarksRecursive()`:** um einen Parameter `string
   folderPath` erweitern, der beim Rekursieren durch Ordner mitgeführt und
   erweitert wird, und beim `"url"`-Typ ins neue Bookmark übernommen wird:
   ```csharp
   private void CollectBookmarksRecursive(JsonElement node, List<Bookmark> list, string folderPath)
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
               list.Add(new Bookmark { Title = title, Url = url, Folder = folderPath });
       }
       else if (type == "folder")
       {
           string folderName = node.TryGetProperty("name", out JsonElement folderNameEl) ? folderNameEl.GetString() : "";
           string childPath = string.IsNullOrEmpty(folderPath) ? folderName : $"{folderPath}/{folderName}";

           if (node.TryGetProperty("children", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
           {
               foreach (JsonElement child in children.EnumerateArray())
                   CollectBookmarksRecursive(child, list, childPath);
           }
       }
   }
   ```

3. **B) `ImportFromBrowser()`:** den Aufruf anpassen, um mit leerem
   Startpfad zu beginnen:
   ```csharp
   CollectBookmarksRecursive(rootProp.Value, bookmarks, "");
   ```
   (statt `CollectBookmarksRecursive(rootProp.Value, bookmarks);`)

4. **D) Drei Upsert-Stellen:** `Folder` mit demselben Backfill-Muster wie
   `Browser` ergänzen (nur beim Erst-Insert setzen bzw. nachfüllen, wenn
   noch leer):
   ```csharp
   using var insertCmd = new SqliteCommand(
       @"INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite, Browser, Folder)
         VALUES (@urlKey, @url, @title, 0, @browser, @folder)
         ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title, Browser = CASE WHEN Bookmarks.Browser = '' THEN excluded.Browser ELSE Bookmarks.Browser END, Folder = CASE WHEN Bookmarks.Folder = '' THEN excluded.Folder ELSE Bookmarks.Folder END;",
       connection);
   insertCmd.Parameters.AddWithValue("@urlKey", urlKey);
   insertCmd.Parameters.AddWithValue("@url", bm.Url);
   insertCmd.Parameters.AddWithValue("@title", bm.Title);
   insertCmd.Parameters.AddWithValue("@browser", bm.Browser);
   insertCmd.Parameters.AddWithValue("@folder", bm.Folder);
   insertCmd.ExecuteNonQuery();
   ```

5. **D) Zwei SELECT-Stellen:** erweitern zu
   `SELECT Url, Title, IsFavorite, Browser, Folder FROM Bookmarks;` und
   beim Befüllen `Folder = reader.GetString(4)` ergänzen.

6. **D) Migration:** In `AttemptImportAndLoad()`, nach dem bestehenden
   `Browser`-Migrationscheck (Abschnitt "2c"), einen vierten, analogen
   Check für die Spalte `"Folder"` einfügen: falls nicht vorhanden,
   `ALTER TABLE Bookmarks ADD COLUMN Folder TEXT NOT NULL DEFAULT '';` in
   try/catch (gleiches Muster wie bei `Browser`/`IsFavorite`).
   `CREATE TABLE IF NOT EXISTS` um `Folder TEXT NOT NULL DEFAULT ''`
   ergänzen.

7. **E) `MainWindow.xaml`:** neue `GridViewColumn Header="Ordner"
   DisplayMemberBinding="{Binding Folder}" Width="150"` zwischen der
   "Quelle"- und der "Titel"-Spalte einfügen.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Keine Änderung an DuckDuckGo-, Firefox- oder HTML/JSON-Import – die
  bekommen keinen Ordnerpfad in diesem Task (Firefox folgt als Task 021)
- Kein Baum-UI, keine aufklappbaren Gruppen – nur Speichern + Anzeigen als
  flache Textspalte, wie bei "Quelle" in Task 018
- `ImportBookmarksFromChromeEdge()` selbst (die äußere Browser-Schleife)
  bleibt unangetastet – nur `CollectBookmarksRecursive` und der eine Aufruf
  in `ImportFromBrowser` ändern sich
- Browser-Backfill-Logik (`Browser = CASE WHEN...`) aus Task 018 bleibt
  erhalten, nicht auf die alte Fassung zurückfallen
- Keine neue Dependency
- Weiterhin synchron, kein async

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `CollectBookmarksRecursive` und dem
  Aufruf in `ImportFromBrowser`
- vollständiger, geänderter Inhalt der drei Upsert-Stellen und zwei
  SELECT-Stellen (Migration, `CREATE TABLE`, Upsert, SELECT)
- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen**

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash mit bestehender DB (nicht-destruktive
      Migration für die neue `Folder`-Spalte)
- [ ] Neue Spalte "Ordner" zeigt korrekte, verschachtelte Pfade
      (z. B. "Bookmark bar/Arbeit/Projekt X") für Chromium-Bookmarks
- [ ] Bookmarks direkt in der Bookmark-Leiste (keine Unterordner) zeigen
      nur den obersten Ordnernamen, keine leere Zelle
- [ ] Browser-Filter (Task 019) funktioniert weiterhin unverändert
- [ ] Favoriten, Suche, Export, Statusanzeige, Öffnen-per-Klick
      funktionieren weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – XAML wieder mit bekanntem Regressionsmuster:** `xmlns:local`,
`FontSize="18"` am Stern-Button und die komplette `BrowserFilterComboBox`
aus Task 019 fehlten in Qwens Rückgabe (rekonstruiert aus altem
Gedächtnisstand). Nicht übernommen – stattdessen die neue "Ordner"-Spalte
direkt in die bereits korrekte XAML eingefügt.

Die `.cs`-Änderungen waren diesmal vollständig korrekt: `CollectBookmarksRecursive`
mit `folderPath`-Parameter exakt wie spezifiziert, `AttemptImportAndLoad()`
und `ImportHtmlButton_Click()` inklusive Migration/`CREATE TABLE`/Upsert/
SELECT korrekt um `Folder` erweitert – und wichtig: der `PopulateBrowserFilter()`-
Aufruf aus Task 019 wurde diesmal **nicht** verloren (im Gegensatz zu
früheren Runden). Trotzdem wurden die Upsert/SELECT-Teile nicht blind
übernommen, sondern gezielt in die bereits korrekte Version eingefügt, um
kein Risiko einzugehen.

Live-Test (Sven, Screenshot): Ordner-Spalte zeigt korrekt lokalisierte
Namen – "Favoritenleiste" (Edge), "Lesezeichenleiste" (Brave). Firefox und
"Manuell"-Einträge bleiben leer – exakt erwartetes Verhalten, da Task 020
bewusst nur Chromium-Browser abdeckt.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach XAML-Korrektur)
- [x] App startet ohne Crash mit bestehender DB (nicht-destruktive Migration)
- [x] Spalte "Ordner" zeigt korrekte, teils verschachtelte Pfade für
      Chromium-Bookmarks
- [x] Bookmarks direkt in der Leiste zeigen den obersten Ordnernamen, keine
      leere Zelle (bestätigt: "Favoritenleiste"/"Lesezeichenleiste")
- [x] Browser-Filter (Task 019) funktioniert weiterhin
- [x] Favoriten, Suche, Export, Statusanzeige, Öffnen-per-Klick weiterhin
      funktionsfähig

**Task 020 abgeschlossen.**

---

## Nächster Schritt

Task 021: Ordnerpfad für Firefox/Gecko-Import (analoges Vorgehen, `moz_bookmarks`
hat eine Baumstruktur über die `parent`-Spalte). Danach Task 022: eigentliche
Baum-UI.
