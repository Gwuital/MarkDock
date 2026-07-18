# Arbeitsvorlage – Task 009

## Task-ID

`Task_009_Statusleiste_Bookmark_Anzahl`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Aktuell zeigt der Import nur einmalig eine MessageBox mit
der Anzahl neuer/vorhandener Bookmarks. Sven möchte zusätzlich **dauerhaft
sichtbar** unten links im Fenster sehen, wie viele Bookmarks insgesamt
gespeichert sind – nicht nur direkt nach einem Import, sondern immer.

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
    </Grid>
</Window>
```

`MainWindow.xaml.cs` – die relevante Methode `RefreshDisplayedBookmarks()`
(Rest der Datei unverändert vom letzten abgenommenen Task):

```csharp
private void RefreshDisplayedBookmarks()
{
    _displayedBookmarks.Clear();
    foreach (var bm in _allBookmarks)
        _displayedBookmarks.Add(bm);
}
```

Diese Methode wird bereits an beiden relevanten Stellen aufgerufen: einmal
am Ende von `AttemptImportAndLoad()` (Start-Import) und einmal am Ende von
`ImportHtmlButton_Click()` (manueller Import). Das ist der zentrale Punkt,
an dem sich `_allBookmarks` zuletzt geändert hat.

---

## 2. Auftrag (genau EIN Problem)

Füge eine **dauerhaft sichtbare Statusanzeige unten links** hinzu, die die
Gesamtzahl der gespeicherten Bookmarks zeigt:

1. In `MainWindow.xaml`: neue `RowDefinition Height="Auto"` unten in der
   Grid hinzufügen (nach der ListView-Zeile), darin ein
   `TextBlock x:Name="StatusText"` links ausgerichtet
   (`HorizontalAlignment="Left"`), z. B. mit Text `"0 Bookmarks"` als
   Platzhalter im XAML (wird beim Start sofort überschrieben).

2. In `MainWindow.xaml.cs`: `RefreshDisplayedBookmarks()` um eine Zeile
   ergänzen, die `StatusText.Text` auf `$"{_allBookmarks.Count} Bookmarks"`
   setzt. Da diese Methode bereits nach jedem relevanten Update aufgerufen
   wird (Start-Import und manueller Import), reicht diese eine Stelle – die
   Anzeige aktualisiert sich dadurch automatisch überall.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Nur `MainWindow.xaml` (neue Zeile mit TextBlock) und
  `RefreshDisplayedBookmarks()` in `MainWindow.xaml.cs` ändern – keine
  andere Methode anfassen
- Die Statusanzeige zeigt die Gesamtzahl aller gespeicherten Bookmarks
  (`_allBookmarks.Count`), **nicht** die Anzahl der aktuell gefilterten
  Suchergebnisse (`_displayedBookmarks.Count`) – auch während der Suche
  bleibt die Gesamtzahl stehen
- Keine Änderung an der MessageBox aus Task 008 – die bleibt zusätzlich wie
  sie ist
- Kein neues Control außer dem einen `TextBlock`
- Keine neue Dependency

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – falls der volle Stand von
  `MainWindow.xaml.cs` gebraucht wird und nicht komplett vorliegt: kurz
  nachfragen ist hier ok

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet, unten links steht sofort die korrekte Gesamtzahl
- [ ] Nach manuellem Import (Task 006–008) aktualisiert sich die Zahl
      automatisch
- [ ] Während der Suche bleibt die Zahl unverändert (zeigt weiterhin die
      Gesamtzahl, nicht die Trefferzahl)
- [ ] Alles andere (Suche, Öffnen, Import-Button, Persistenz) funktioniert
      unverändert weiter

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – kritischer Regelverstoß:** Statt nur eine Zeile zu ergänzen wurde
die komplette `MainWindow.xaml` neu geschrieben – SearchBox, Import-Button
und MouseDoubleClick-Verdrahtung dabei gelöscht (Verstoss gegen ADR-999
Regel 4, hätte außerdem den Build gebrochen, da `SearchBox_TextChanged` im
Code-Behind auf das gelöschte Control verweist). Klare Korrektur mit exakter
Ausgangs-XAML nachgeschickt.

**Runde 2 – abgenommen.** Fix korrekt: alle drei bestehenden Elemente
(SearchBox, ImportHtmlButton, ListView mit MouseDoubleClick) unverändert,
nur eine vierte `RowDefinition` plus `StatusText`-TextBlock ergänzt.
`RefreshDisplayedBookmarks()` in der .cs korrekt um `StatusText.Text =
$"{_allBookmarks.Count} Bookmarks"` erweitert – zeigt Gesamtzahl, nicht
Suchtreffer, da `_allBookmarks.Count` verwendet wird statt
`_displayedBookmarks.Count`.

Live-Test: App zeigt sofort korrekte Gesamtzahl (679 Bookmarks) unten links.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes
- [x] App startet, Gesamtzahl sofort korrekt sichtbar
- [x] Aktualisiert sich automatisch nach Import (gleiche Methode wie Start)
- [x] SearchBox, Import-Button, Doppelklick-Öffnen weiterhin vorhanden und
      funktionsfähig

**Task 009 abgeschlossen.**

---

## Stand MarkDock gesamt

MVP + Chrome/Edge/DuckDuckGo-Import + SQLite-Persistenz + URL-Normalisierung/
Dedup + HTML-/Firefox-JSON-Import + Neu/Vorhanden-Zählung + Statusanzeige.
Alle 9 Tasks abgenommen. Nächste Schritte offen – bei Bedarf neuen Task
definieren.
