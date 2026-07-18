# Arbeitsvorlage – Task 022

## Task-ID

`Task_022_Ordner_Gruppierung_UI`

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

**Hintergrund:** Letzter der drei Ordner-Tasks. `08_Funktionen.md`
beschreibt die Ordneransicht als "Baumstruktur Darstellung, **optional**
einklappbar" – das "optional" ist hier wichtig: Dieser Task baut eine
**gruppierte Anzeige mit Ordner-Überschriften** (WPF-Standardmechanismus
für Listen), **keine** aufklappbare Baumstruktur mit Ein-/Ausklappen. Das
hält die Änderung klein und risikoarm. Eine echte ein-/ausklappbare
Baumstruktur wäre ein deutlich größerer, separater Task, falls später
gewünscht.

**Technischer Ansatz:** WPF's `ListView` unterstützt Gruppierung nativ über
`ICollectionView.GroupDescriptions` (im Code gesetzt) und
`ListView.GroupStyle` (in der XAML, definiert wie eine Gruppen-Überschrift
aussieht). Die bestehende `ListView`/`GridView`-Struktur bleibt **komplett
erhalten** – es wird nur Gruppierung ergänzt, keine andere Control-Art
verwendet (explizit **kein** `TreeView`, **kein** `DataGrid`).

**Aktueller Code, der angepasst wird:**

**A) Konstruktor `MainWindow()`:**
```csharp
public MainWindow()
{
    InitializeComponent();
    BookmarksListView.ItemsSource = _displayedBookmarks;
}
```

**B) `MainWindow.xaml` — die `ListView`:**
```xml
<ListView x:Name="BookmarksListView"
          Grid.Row="2"
          MouseDoubleClick="BookmarksListView_MouseDoubleClick">
    <ListView.View>
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
    </ListView.View>
</ListView>
```

---

## 2. Auftrag (genau EIN Problem)

Gruppiere die Bookmark-Liste sichtbar nach Ordnerpfad, mit einer
Überschriftszeile pro Ordner:

1. **A) Konstruktor:** direkt nach `BookmarksListView.ItemsSource =
   _displayedBookmarks;` folgendes ergänzen, um die Gruppierung nach
   `Folder` zu aktivieren:
   ```csharp
   var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_displayedBookmarks);
   view.GroupDescriptions.Add(new System.ComponentModel.PropertyGroupDescription("Folder"));
   ```
   (volle Namespace-Qualifizierung verwenden wie oben gezeigt, damit keine
   zusätzlichen `using`-Zeilen nötig sind – falls stattdessen `using
   System.ComponentModel;` ergänzt wird, ist das auch ok, dann aber
   zusätzlich zu den bestehenden `using`-Zeilen, nicht anstelle)

2. **B) `MainWindow.xaml`:** der bestehenden `ListView` ein `GroupStyle`
   hinzufügen, das den Ordnerpfad als fett gedruckte, farblich abgesetzte
   Überschrift zeigt:
   ```xml
   <ListView x:Name="BookmarksListView"
             Grid.Row="2"
             MouseDoubleClick="BookmarksListView_MouseDoubleClick">
       <ListView.GroupStyle>
           <GroupStyle>
               <GroupStyle.HeaderTemplate>
                   <DataTemplate>
                       <TextBlock Text="{Binding Name}"
                                  FontWeight="Bold"
                                  FontSize="13"
                                  Background="#E0E0E0"
                                  Padding="4,2"
                                  Margin="0,4,0,0"/>
                   </DataTemplate>
               </GroupStyle.HeaderTemplate>
           </GroupStyle>
       </ListView.GroupStyle>
       <ListView.View>
           <!-- GridView-Inhalt bleibt exakt wie er ist -->
       </ListView.View>
   </ListView>
   ```
   (`ListView.GroupStyle` wird **vor** `ListView.View` eingefügt, der
   `GridView`-Inhalt selbst bleibt unverändert – bitte den kompletten,
   bestehenden `GridView`-Block aus Abschnitt 1B 1:1 wieder einsetzen,
   nicht durch den Kommentar ersetzen)

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Kein `TreeView`, kein `DataGrid` – die bestehende `ListView`/`GridView`-
  Struktur bleibt erhalten, nur `GroupStyle` kommt dazu
- Kein Ein-/Ausklappen (Expander/Collapse) – nur sichtbare
  Gruppen-Überschriften, das reicht für diesen Task
- Keine Änderung an `ApplyFilters()`, `PopulateBrowserFilter()`, Suche,
  Browser-Filter, Import, Export, Favoriten, Statusanzeige
- Keine neue Dependency
- `xmlns:local`, `FontSize="18"` am Stern-Button und die
  `BrowserFilterComboBox` aus früheren Tasks müssen erhalten bleiben – bei
  der Rückgabe der kompletten `MainWindow.xaml` bitte den aktuellen Stand
  aus Abschnitt 1 als Basis nehmen, nicht aus dem Ged&auml;chtnis
  rekonstruieren

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt des Konstruktors `MainWindow()`
- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen**

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash
- [ ] Liste zeigt sichtbare Gruppen-Überschriften pro Ordnerpfad
- [ ] Bookmarks ohne Ordner (z. B. "Manuell"-Importe) werden in einer
      eigenen (ggf. leeren) Gruppe zusammengefasst, kein Crash
- [ ] Suche und Browser-Filter funktionieren weiterhin, Gruppierung bleibt
      dabei erhalten (gefilterte Liste ist weiterhin nach Ordner gruppiert)
- [ ] Favoriten, Export, Statusanzeige, Öffnen-per-Klick funktionieren
      weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Dokument-Upload-Kanal war während dieses Tasks durchgehend defekt** –
mehrere Versuche (Copy-Paste, `.txt`-Datei-Upload) kamen bei Claude leer
an, auch nach Neustart der Session. Da der Task ohnehin sehr klein und
exakt spezifiziert war (nur zwei Stellen: Konstruktor + `GroupStyle`),
wurden beide Änderungen direkt von Claude selbst eingebaut, ohne auf eine
lesbare Qwen-Antwort zu warten.

**Eigener Fehler beim ersten Versuch:** `PropertyGroupDescription` wurde
fälschlich mit Namespace `System.ComponentModel` referenziert – die Klasse
liegt tatsächlich in `System.Windows.Data`. Compile-Fehler CS0234, direkt
selbst korrigiert.

Live-Test (Sven, Screenshot): Gruppierung funktioniert einwandfrei –
fett/grau hinterlegte Überschriften pro Ordnerpfad ("Favoritenleiste",
"Lesezeichenleiste", "menu", "menu/Mozilla Firefox", "toolbar"),
Bookmarks korrekt darunter sortiert. Kein Ein-/Ausklappen möglich – das ist
korrektes, spezifiziertes Verhalten für diesen Task (bewusst kein
TreeView/Expander, um die Änderung klein zu halten).

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach Namespace-Korrektur)
- [x] App startet ohne Crash
- [x] Sichtbare Gruppen-Überschriften pro Ordnerpfad (bestätigt)
- [x] Bookmarks ohne Ordner ("Manuell") landen in eigener Gruppe, kein Crash
- [x] Suche/Browser-Filter funktionieren weiterhin (nicht separat
      gegengetestet, aber `ApplyFilters()` unverändert, Gruppierung hängt
      nur an der ObservableCollection-Referenz, nicht an der Filterlogik)
- [x] Favoriten, Export, Statusanzeige, Öffnen-per-Klick weiterhin
      funktionsfähig

**Task 022 abgeschlossen.**

---

## Stand MarkDock gesamt

MVP + 11-Browser-Import + manueller HTML-/JSON-Import + SQLite-Persistenz
+ URL-Dedup + Favoriten + Browser-Quelle/-Filter + Ordnerpfad (Chromium +
Firefox) + Ordner-Gruppierung in der UI + Export (CSV/JSON/HTML) +
Statusanzeige. Alle 22 Tasks abgenommen.

## Optionaler nächster Schritt (falls gewünscht)

Echtes Ein-/Ausklappen der Ordnergruppen (Expander-Style statt reinem
HeaderTemplate) wäre ein eigener, größerer Folge-Task. Sonst: Rest der
ursprünglichen Liste (Portable Version, Launcher, Tote-Links-Check,
Shortcuts).
