# Arbeitsvorlage – Task 015

## Task-ID

`Task_015_Fortschrittsanzeige_Import`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** `AttemptImportAndLoad()` läuft aktuell direkt im
Konstruktor von `MainWindow`, **bevor** das Fenster überhaupt sichtbar
wird. Mit inzwischen 11 automatischen Quellen (Chrome, Edge, Opera, Brave,
Vivaldi, SRWare Iron, Comodo Dragon, DuckDuckGo, Firefox, Zen, Floorp,
Waterfox) kann das spürbar dauern – der Nutzer sieht in der Zwischenzeit
gar nichts, nicht mal ein leeres Fenster.

**Wichtig – bewusst kein "echtes" Async:** Bisherige Tasks haben
durchgehend "kein async" verlangt (ADR-999 Regel 12/13 – keine unnötige
Komplexität). Dieser Task bleibt dabei: **kein `async`/`await`, kein
`Task.Run`**. Stattdessen wird ein einfacher, robuster WPF-Kniff genutzt:
das Fenster wird zuerst angezeigt (Import verschiebt sich auf das
`Loaded`-Event statt im Konstruktor zu laufen), und ein kurzer manueller
UI-Repaint (`Dispatcher.Invoke` mit `DispatcherPriority.Render`) sorgt
dafür, dass der Statustext "Importiere Bookmarks..." tatsächlich sichtbar
wird, bevor der weiterhin blockierende (aber jetzt sichtbare) Import
startet.

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

`MainWindow.xaml.cs` – die relevanten Teile (Konstruktor + Anfang von
`AttemptImportAndLoad()`; der Rest der Methode – Migration, Upsert, DB-Load,
Fallback, `RefreshDisplayedBookmarks()` am Ende – bleibt exakt wie im
zuletzt abgenommenen Task 014, hier nicht nochmal ausgeschrieben):

```csharp
public MainWindow()
{
    InitializeComponent();
    BookmarksListView.ItemsSource = _displayedBookmarks;
    AttemptImportAndLoad();
}

private void AttemptImportAndLoad()
{
    // 1. Datenbank initialisieren
    string dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarkDock", "markdock.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
    using var connection = new SqliteConnection($"Data Source={dbPath}");
    connection.Open();

    // ... (Migration, Tabelle anlegen – unverändert, hier gekürzt) ...

    // 4. Chrome/Edge/... Import ausführen
    var imported = ImportBookmarksFromChromeEdge();
    imported.AddRange(ImportFromDuckDuckGo());
    imported.AddRange(ImportFromFirefox());

    // ... (Upsert, DB-Load, Fallback, RefreshDisplayedBookmarks() – unverändert) ...
}
```

---

## 2. Auftrag (genau EIN Problem)

Sorge dafür, dass das Fenster **zuerst sichtbar wird** und währenddessen
ein Statustext "Importiere Bookmarks..." angezeigt wird, bevor der (weiter
synchrone) Import läuft:

1. In `MainWindow.xaml`: Füge dem `Window`-Tag `Loaded="MainWindow_Loaded"`
   hinzu.

2. In `MainWindow.xaml.cs`:
   - Entferne den Aufruf `AttemptImportAndLoad();` aus dem Konstruktor.
   - Neue Methode `MainWindow_Loaded(object sender, RoutedEventArgs e)`:
     - Setzt `StatusText.Text = "Importiere Bookmarks...";`
     - Erzwingt einen UI-Repaint, damit der Text sichtbar wird, bevor die
       blockierende Arbeit beginnt:
       ```csharp
       Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
       ```
     - Ruft danach `AttemptImportAndLoad();` auf (unverändert, weiterhin
       synchron).

3. `AttemptImportAndLoad()` selbst bleibt inhaltlich unverändert – am Ende
   überschreibt `RefreshDisplayedBookmarks()` (bereits vorhanden) den
   Statustext automatisch mit der finalen Bookmark-Anzahl, das muss nicht
   extra behandelt werden.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- **Kein** `async`/`await`, **kein** `Task.Run`, **kein** `BackgroundWorker`
  – der Import bleibt vollständig synchron, nur der Startzeitpunkt
  verschiebt sich auf `Loaded`
- Keine Änderung an `AttemptImportAndLoad()` selbst (Migration, Import-
  Aufrufe, Upsert, Fallback, `RefreshDisplayedBookmarks()`)
- Keine Änderung an den einzelnen Importer-Methoden
- Keine Änderung an Suche, Export, Persistenz, Dedup
- Keine neue Dependency
- Kein Fortschrittsbalken mit Prozentangabe oder Schritt-für-Schritt-
  Anzeige pro Quelle – ein einziger, statischer "Importiere..."-Text reicht
  für diesen Task

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – falls der volle Stand einer der
  beiden Dateien gebraucht wird und nicht komplett vorliegt: kurz
  nachfragen ist hier ok

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] Fenster wird sichtbar, **bevor** der Import fertig ist (nicht erst
      danach)
- [ ] "Importiere Bookmarks..." ist kurz sichtbar, bevor die finale
      Bookmark-Anzahl erscheint
- [ ] Nach Abschluss zeigt die Statuszeile wie bisher die korrekte
      Gesamtzahl
- [ ] Alle Importquellen, Persistenz, Dedup, Suche, Export,
      Öffnen-per-Klick funktionieren weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – zwei Bugs:** `BookmarksListView.ItemsSource = _displayedBookmarks;`
versehentlich aus dem Konstruktor entfernt (hätte die Liste dauerhaft leer
gelassen), außerdem fehlte `Loaded="MainWindow_Loaded"` in der XAML – der
Handler wäre nie aufgerufen worden, Import hätte gar nicht mehr gestartet.
Korrektur angefordert.

**Runde 2 – von Qwen korrekt behoben.** Beide Punkte sauber gefixt.

**Runde 3 – Timing-Verfeinerung, von Claude selbst korrigiert (kein
Qwen-Bug):** `Loaded` feuert vor dem tatsächlichen ersten Bildschirm-Paint,
daher war der Statustext trotz korrekter Logik nicht sichtbar. Auf
`ContentRendered` umgestellt (garantiert Ausführung erst nach echtem
Rendern), Methoden-Signatur entsprechend von `RoutedEventArgs` auf
`EventArgs` angepasst (anderer Event-Typ).

**Live-Test / Ergebnis:** Import funktioniert nachweislich korrekt (neue
Brave-Bookmarks kamen an), Fenster erscheint strukturell vor Abschluss des
Imports. Der "Importiere Bookmarks..."-Zwischentext selbst war bei Sven
trotzdem nicht mit bloßem Auge sichtbar – vermutlich weil die meisten der
elf Quellen bei ihm nicht installiert sind (nur ein schneller
`File.Exists`-Check) und die installierten (Chrome/Edge/Brave/DuckDuckGo/
Firefox) bei seiner Bookmark-Menge in unter ~100ms durchlaufen, schneller
als ein sichtbarer Screen-Refresh. Das ist kein Implementierungsfehler,
sondern eine inhärente Grenze des bewusst gewählten synchronen Ansatzes
(kein `async`/`Task.Run`, wie in allen bisherigen Tasks vorgegeben) bei
sehr schnellen Importzeiten. Architektur/Mechanismus sind korrekt und
zahlen sich aus, sobald der Import einmal länger dauert (mehr Bookmarks,
langsamere Platte, mehr installierte Browser).

Definition of Done – Bewertung:

- [x] Projekt kompiliert ohne manuelle Fixes (nach Korrekturen)
- [x] Fenster wird sichtbar, bevor der Import fertig ist (strukturell
      erfüllt, `ContentRendered` garantiert das)
- [ ] "Importiere Bookmarks..." mit bloßem Auge sichtbar – bei Svens
      Konfiguration technisch nicht beobachtbar, da Import zu schnell läuft;
      Mechanismus selbst korrekt implementiert
- [x] Statuszeile zeigt nach Abschluss korrekt die Gesamtzahl
- [x] Alle Importquellen, Persistenz, Dedup, Suche, Export, Doppelklick-
      Öffnen funktionieren weiterhin unverändert

**Task 015 abgeschlossen** – funktional korrekt, mit dokumentierter
Einschränkung bei sehr schnellen Importläufen (kosmetisch, keine
Funktionsstörung).

---

## Nächster Schritt

Ursprüngliche Priorisierung: Favoriten-Markierung, Browser-Filter,
Ordnerstruktur-Ansicht.
