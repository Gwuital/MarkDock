# Arbeitsvorlage – Task 010

## Task-ID

`Task_010_Export`

## Status

`Offen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
/read docs/01_Produktanforderungen_PRD.md
```

**Hintergrund:** `01_Produktanforderungen_PRD.md` Abschnitt 3.6 listet Export
(CSV, JSON, HTML) als **MUST HAVE** für Version 1 – das fehlt bisher
komplett. Import (Task 006/007) ist bereits da, Export nicht.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

`MainWindow.xaml` – aktueller Stand:

```xml
<Window x:Class="MarkDock.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MarkDock" Height="450" Width="600">
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
        <Button x:Name="ImportHtmlButton"
                Grid.Row="1"
                Content="HTML importieren..."
                Margin="0,0,0,10"
                Height="30"
                Click="ImportHtmlButton_Click"/>
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

`MainWindow.xaml.cs` – identisch zum abgenommenen Task 009 (Chrome/Edge/
DuckDuckGo-Start-Import, SQLite-Persistenz mit `UrlKey`/`Url`/`Title`,
`NormalizeUrl()`, HTML-/Firefox-JSON-Import über `ImportFromHtmlOrJson()`
mit Neu/Vorhanden-Zählung, Statusanzeige in `RefreshDisplayedBookmarks()`).
Der volle Dateiinhalt liegt unter `src\MarkDock\MainWindow.xaml.cs` im
Projekt – für diesen Task wird nur eine neue Methode + ein neuer
Button-Handler ergänzt, der Rest bleibt unangetastet.

---

## 2. Auftrag (genau EIN Problem)

Füge einen **Export-Button** hinzu, der die aktuell angezeigten Bookmarks
(`_displayedBookmarks` – respektiert damit automatisch einen aktiven
Suchfilter, oder bei leerem Suchfeld alle) in eine Datei exportiert:

1. **UI-Ergänzung:** Ein Button `"Exportieren..."` neben dem
   Import-Button (z. B. in derselben Zeile per `StackPanel Orientation="Horizontal"`,
   oder als eigene neue `RowDefinition Height="Auto"` direkt darunter –
   Qwen darf hier die einfachere Variante wählen).

2. **Click-Handler** `ExportButton_Click`:
   - Öffnet `Microsoft.Win32.SaveFileDialog` mit Filter
     `"CSV-Datei (*.csv)|*.csv|JSON-Datei (*.json)|*.json|HTML-Datei
     (*.html)|*.html"`
   - Anhand der gewählten Dateiendung (`Path.GetExtension`) das passende
     Format schreiben (siehe Punkt 3)
   - Nach erfolgreichem Export: `MessageBox.Show` mit Anzahl exportierter
     Bookmarks

3. **Format-Schreiblogik** (jeweils `_displayedBookmarks`, nicht
   `_allBookmarks`):
   - **CSV:** erste Zeile `Title,Url`, danach eine Zeile pro Bookmark.
     Werte in Anführungszeichen setzen und enthaltene `"` verdoppeln
     (Standard-CSV-Escaping), falls Title oder Url ein Komma oder
     Anführungszeichen enthält.
   - **JSON:** Liste von `{ "title": "...", "url": "..." }`-Objekten via
     `System.Text.Json.JsonSerializer.Serialize` (bereits im Projekt
     verwendet, keine neue Dependency). `WriteIndented = true` für
     Lesbarkeit.
   - **HTML:** einfaches Netscape-Bookmark-Format, kompatibel zum eigenen
     Import aus Task 006/007:
     ```html
     <!DOCTYPE NETSCAPE-Bookmark-file-1>
     <META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">
     <TITLE>Bookmarks</TITLE>
     <H1>Bookmarks</H1>
     <DL><p>
         <DT><A HREF="URL">TITEL</A>
         ...
     </DL><p>
     ```
     (Titel-Werte mit `System.Net.WebUtility.HtmlEncode` kodieren, damit
     Sonderzeichen wie `&` korrekt escaped werden – Gegenstück zum
     `HtmlDecode` beim Import.)

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Export bezieht sich auf `_displayedBookmarks` (aktuell sichtbare/gefilterte
  Liste), nicht auf einen separaten Auswahlmechanismus
- Keine Änderung an Import-Logik, Persistenz, Suche, Statusanzeige
- Keine neue Dependency (`System.Text.Json` und `Microsoft.Win32` sind
  schon vorhanden)
- Kein Fortschrittsbalken, keine Zwischenschritte – einfacher, synchroner
  Schreibvorgang reicht (Datenmenge ist klein genug)
- Bookmark-Modell bleibt `{ Title, Url }`, keine neuen Felder

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – falls der volle aktuelle Stand
  einer der beiden Dateien gebraucht wird und nicht komplett vorliegt: kurz
  nachfragen ist hier ok

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet, Export-Button sichtbar
- [ ] Export als CSV erzeugt eine gültige, in Excel/LibreOffice öffenbare
      Datei
- [ ] Export als JSON erzeugt gültiges, lesbares JSON
- [ ] Export als HTML lässt sich anschließend über den eigenen
      Import-Button (Task 006) wieder erfolgreich importieren
      (Round-Trip-Test)
- [ ] Suchfilter wirkt sich korrekt auf den Export aus (nur gefilterte
      Treffer landen in der Datei, wenn eine Suche aktiv ist)
- [ ] Alles andere (Import, Persistenz, Suche, Statusanzeige,
      Öffnen-per-Klick) funktioniert unverändert weiter

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

`<offen bis Rückgabe>`
