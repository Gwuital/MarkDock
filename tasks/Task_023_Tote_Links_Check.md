# Arbeitsvorlage – Task 023

## Task-ID

`Task_023_Tote_Links_Check`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Wichtige Ausnahme von der bisherigen Projektlinie:** Alle bisherigen
Tasks liefen bewusst synchron. Dieser Task ist die **einzige** Ausnahme:
Da hier echte Netzwerkanfragen an potenziell hunderte URLs gestellt werden,
würde eine synchrone Umsetzung die App für sehr lange Zeit komplett
einfrieren. Deshalb wird hier `async`/`await` mit `HttpClient` verwendet –
das ist eine bewusste, begründete Abweichung, keine Regel-Verletzung.

**Aktueller Code, der angepasst wird** (Ausschnitte):

**A) `Bookmark`-Modell:**
```csharp
public class Bookmark
{
    public string Title { get; set; } = "";
    public string Url   { get; set; } = "";
    public bool IsFavorite { get; set; } = false;
    public string Browser { get; set; } = "";
    public string Folder { get; set; } = "";
}
```

**B) `MainWindow.xaml` — die Button-Zeile:**
```xml
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
    <ComboBox x:Name="BrowserFilterComboBox"
              Width="150"
              Margin="10,0,0,0"
              Height="30"
              SelectionChanged="BrowserFilterComboBox_SelectionChanged"/>
</StackPanel>
```

**C) `MainWindow.xaml` — die `GridView`-Spalten** (Rest von `ListView`
inkl. `GroupStyle` bleibt exakt wie er ist, hier nur die Spalten-Liste):
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
    <GridViewColumn Header="Ordner" DisplayMemberBinding="{Binding Folder}" Width="150"/>
    <GridViewColumn Header="Titel" DisplayMemberBinding="{Binding Title}" Width="250"/>
    <GridViewColumn Header="URL"   DisplayMemberBinding="{Binding Url}"  Width="300"/>
</GridView>
```

**D) `AttemptImportAndLoad()` — nur die Migrationsblöcke, `CREATE TABLE`,
Upsert und SELECT sind relevant** (Import-Aufrufe und alles andere bleibt
unangetastet). Aktuelles Muster für die Migrationschecks (zuletzt: Folder,
Abschnitt "2d"):
```csharp
// 2d. Zusätzliche, nicht-destruktive Migration: Folder-Spalte ergänzen, falls sie fehlt
try
{
    using (var infoCmd4 = new SqliteCommand("PRAGMA table_info(Bookmarks);", connection))
    using (SqliteDataReader reader4 = infoCmd4.ExecuteReader())
    {
        bool hasFolder = false;
        while (reader4.Read())
        {
            if (reader4.GetString(reader4.GetOrdinal("name")) == "Folder")
            {
                hasFolder = true;
                break;
            }
        }
        if (!hasFolder)
        {
            using (var alterCmd = new SqliteCommand("ALTER TABLE Bookmarks ADD COLUMN Folder TEXT NOT NULL DEFAULT '';", connection))
            {
                alterCmd.ExecuteNonQuery();
            }
        }
    }
}
catch
{
    // Tabelle existiert noch nicht (Erstinstallation) → kein Migration nötig, CREATE TABLE übernimmt das gleich
}
```

`CREATE TABLE`:
```sql
CREATE TABLE IF NOT EXISTS Bookmarks (UrlKey TEXT PRIMARY KEY, Url TEXT NOT NULL, Title TEXT NOT NULL, IsFavorite INTEGER NOT NULL DEFAULT 0, Browser TEXT NOT NULL DEFAULT '', Folder TEXT NOT NULL DEFAULT '');
```

Upsert (kommt dreimal vor: Haupt-Import, Demo-Fallback, `ImportHtmlButton_Click`):
```sql
INSERT INTO Bookmarks (UrlKey, Url, Title, IsFavorite, Browser, Folder)
VALUES (@urlKey, @url, @title, 0, @browser, @folder)
ON CONFLICT(UrlKey) DO UPDATE SET Url = excluded.Url, Title = excluded.Title, Browser = CASE WHEN Bookmarks.Browser = '' THEN excluded.Browser ELSE Bookmarks.Browser END, Folder = CASE WHEN Bookmarks.Folder = '' THEN excluded.Folder ELSE Bookmarks.Folder END;
```

SELECT (kommt zweimal vor: Start-Load, Reload nach HTML/JSON-Import):
```sql
SELECT Url, Title, IsFavorite, Browser, Folder FROM Bookmarks;
```

---

## 2. Auftrag (genau EIN Problem)

Füge einen **"Links prüfen"-Button** hinzu, der die aktuell sichtbaren
(gefilterten) Bookmarks per HTTP-Anfrage auf Erreichbarkeit prüft:

1. **A) Bookmark-Modell:** eine Zeile ergänzen:
   `public bool? IsDead { get; set; } = null;`
   (nullable – `null` = noch nicht geprüft, `true` = tot, `false` = ok)

2. **B) `MainWindow.xaml`:** einen weiteren Button in der Button-`StackPanel`
   ergänzen, nach der `BrowserFilterComboBox`:
   ```xml
   <Button x:Name="CheckLinksButton"
           Content="Links prüfen"
           Margin="10,0,0,0"
           Height="30"
           Click="CheckLinksButton_Click"/>
   ```

3. **C) `MainWindow.xaml`:** neue `GridViewColumn Header="Status"` **vor**
   der Titel-Spalte, mit `CellTemplate`, die den Status als Text anzeigt
   (`IValueConverter` nötig, siehe Punkt 5):
   ```xml
   <GridViewColumn Header="Status" Width="70">
       <GridViewColumn.CellTemplate>
           <DataTemplate>
               <TextBlock Text="{Binding IsDead, Converter={StaticResource DeadLinkStatusConverter}}"/>
           </DataTemplate>
       </GridViewColumn.CellTemplate>
   </GridViewColumn>
   ```
   Den Converter im `Window.Resources`-Block registrieren (analog zu
   `BoolToStarConverter`, mit `xmlns:local`-Präfix):
   ```xml
   <local:DeadLinkStatusConverter x:Key="DeadLinkStatusConverter"/>
   ```

4. **D) Datenbank:** neue Spalte `IsDead` als `INTEGER` (**ohne** `NOT NULL`,
   damit `NULL` = "nicht geprüft" möglich bleibt):
   - Migrationscheck (Abschnitt "2e", analog zum Folder-Muster): falls
     Spalte fehlt, `ALTER TABLE Bookmarks ADD COLUMN IsDead INTEGER;`
   - `CREATE TABLE` um `IsDead INTEGER` ergänzen (ohne `NOT NULL DEFAULT`)
   - Die drei Upsert-Stellen und zwei SELECT-Stellen müssen **nicht**
     erweitert werden (der Status wird nur separat per `UPDATE`
     geschrieben, siehe Punkt 6) – hier reicht die Migration + `CREATE
     TABLE`-Erweiterung

5. **Neuer `IValueConverter`** `DeadLinkStatusConverter`:
   ```csharp
   public class DeadLinkStatusConverter : IValueConverter
   {
       public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
       {
           if (value is bool isDead)
               return isDead ? "✗ Tot" : "✓ OK";
           return "";
       }

       public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
       {
           throw new NotImplementedException();
       }
   }
   ```

6. **Neuer Click-Handler `CheckLinksButton_Click`** (async, wichtige
   Ausnahme siehe oben):
   ```csharp
   private async void CheckLinksButton_Click(object sender, RoutedEventArgs e)
   {
       var toCheck = _displayedBookmarks.ToList();
       if (toCheck.Count == 0)
           return;

       string dbPath = Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
           "MarkDock", "markdock.db");

       using var httpClient = new System.Net.Http.HttpClient();
       httpClient.Timeout = TimeSpan.FromSeconds(5);

       int checkedCount = 0;
       foreach (var bm in toCheck)
       {
           checkedCount++;
           StatusText.Text = $"Prüfe Link {checkedCount}/{toCheck.Count}...";

           bool isDead;
           try
           {
               var response = await httpClient.GetAsync(bm.Url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
               isDead = !response.IsSuccessStatusCode;
           }
           catch
           {
               isDead = true;
           }

           bm.IsDead = isDead;

           using var connection = new SqliteConnection($"Data Source={dbPath}");
           connection.Open();
           using var cmd = new SqliteCommand(
               "UPDATE Bookmarks SET IsDead = @isDead WHERE UrlKey = @urlKey;",
               connection);
           cmd.Parameters.AddWithValue("@isDead", isDead ? 1 : 0);
           cmd.Parameters.AddWithValue("@urlKey", NormalizeUrl(bm.Url));
           cmd.ExecuteNonQuery();
       }

       RefreshDisplayedBookmarks();
       MessageBox.Show($"{toCheck.Count} Links geprüft.", "Links prüfen", MessageBoxButton.OK, MessageBoxImage.Information);
   }
   ```
   (Prüft bewusst nur `_displayedBookmarks` – also die aktuell
   sichtbare/gefilterte Liste. Damit kann Sven per Suche/Browser-Filter
   vorher eingrenzen, statt immer alle 900+ Bookmarks auf einmal zu
   prüfen.)

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Nur dieser eine Button + Handler + Spalte + Converter + Modell-Feld +
  Migration – keine Änderung an Import-Logik, Export, Suche, Browser-Filter,
  Ordner-Gruppierung, Favoriten
- Kein automatisches Löschen toter Links – nur Anzeige des Status
- Keine Parallelisierung/Nebenläufigkeit (kein `SemaphoreSlim`, kein
  `Task.WhenAll`) – sequentiell reicht für diesen Task, auch wenn's
  dadurch bei großen Listen dauert (Sven kann über Suche/Filter eingrenzen)
- Keine neue NuGet-Dependency (`HttpClient` ist Teil von .NET, keine
  Installation nötig)
- **Dies ist die einzige Stelle im Projekt mit `async`/`await`** – nicht an
  anderer Stelle async einführen

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- vollständiger, geänderter Inhalt der betroffenen Teile von
  `MainWindow.xaml.cs` (Bookmark-Modell, neuer Converter, neuer Handler,
  Migrationscheck + `CREATE TABLE` in `AttemptImportAndLoad`)
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen**

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash mit bestehender DB (nicht-destruktive
      Migration für `IsDead`)
- [ ] Button "Links prüfen" sichtbar
- [ ] Klick prüft die aktuell sichtbare Liste, zeigt Fortschritt in der
      Statuszeile, danach Ergebnis-Meldung
- [ ] Spalte "Status" zeigt "✓ OK" / "✗ Tot" nach der Prüfung, leer davor
- [ ] UI bleibt während der Prüfung bedienbar (kein kompletter Freeze wie
      bei den übrigen, synchronen Importen)
- [ ] Suche/Filter vor der Prüfung schränkt die zu prüfende Menge korrekt
      ein
- [ ] Import, Export, Persistenz, Favoriten, Ordner-Gruppierung,
      Öffnen-per-Klick funktionieren weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Dokument-Upload-Kanal weiterhin unzuverlässig, aber diesmal differenziert:**
Qwens Antworten als reiner Text kamen für Bookmark-Modell (A), Converter
und den Click-Handler (Punkt 6) problemlos durch – nur die beiden
XAML-Teile (B, C) wurden vom Client offenbar automatisch in Datei-Anhänge
konvertiert (vermutlich wegen der Spitzklammern/XML-Struktur), die dann
leer bei Claude ankamen. Da B, C und D (Migration) exakt wie in der
Task-Spezifikation vorgegeben, wurden diese drei Teile direkt von Claude
selbst eingebaut, ohne weitere Qwen-Runde.

Alle Qwen-gelieferten Teile (Bookmark-Modell, `DeadLinkStatusConverter`,
`CheckLinksButton_Click`) waren beim ersten Versuch bereits vollständig
korrekt – keine Bugs, keine Korrekturrunden nötig.

**Bekannte, bewusste Einschränkung (dokumentiert, kein Bug):** Die beiden
SELECT-Abfragen laden `IsDead` nicht mit – Status bleibt nur innerhalb der
laufenden Session sichtbar, wird aber korrekt in der DB gespeichert.
Setzt nach Neustart/Re-Import zurück auf leer, obwohl die Werte weiterhin
in der DB liegen. War bewusste Vereinfachung, um den Task klein zu halten.

Live-Test (Sven): Kompiliert, startet. Button sichtbar, Klick läuft
asynchron – App bleibt währenddessen bedienbar (bestätigt, kein Freeze).
Status-Spalte zeigt nach Prüfung korrekt "OK"/"Tot", nicht alle Links tot
(plausibel, echte HTTP-Prüfung funktioniert differenziert).

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes
- [x] App startet ohne Crash mit bestehender DB (nicht-destruktive Migration)
- [x] Button "Links prüfen" sichtbar
- [x] Klick prüft sichtbare Liste, zeigt Fortschritt, Ergebnis-Meldung
- [x] Status-Spalte zeigt "✓ OK"/"✗ Tot" nach Prüfung
- [x] UI bleibt während der Prüfung bedienbar (bestätigt)
- [ ] Suche/Filter vor der Prüfung nicht separat gegengetestet, aber nutzt
      dieselbe `_displayedBookmarks`-Quelle wie Export/Statusanzeige
- [x] Import, Export, Persistenz, Favoriten, Ordner-Gruppierung,
      Öffnen-per-Klick weiterhin funktionsfähig (nicht angefasst)

**Task 023 abgeschlossen.**

---

## Stand MarkDock gesamt

MVP + 11-Browser-Import + manueller HTML-/JSON-Import + SQLite-Persistenz
+ URL-Dedup + Favoriten + Browser-Quelle/-Filter + Ordnerpfad + Ordner-
Gruppierung + Tote-Links-Check (async, einzige Ausnahme im Projekt) +
Export (CSV/JSON/HTML) + Statusanzeige. Alle 23 Tasks abgenommen.

## Von Sven gewünschte Folge-Tasks (zurückgestellt, Tokens erschöpft)

1. **Tote Links wirklich löschen können** – z. B. Button "Tote Links
   entfernen", der alle aktuell als `IsDead = true` markierten Bookmarks
   aus der DB löscht (mit Sicherheitsabfrage vorher)
2. **`IsDead` über Neustart hinweg sichtbar machen** – SELECT-Abfragen um
   `IsDead` erweitern (siehe Einschränkung oben)
