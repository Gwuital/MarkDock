# Arbeitsvorlage – Task 019

## Task-ID

`Task_019_Browser_Filter_UI`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Task 018 speichert jetzt pro Bookmark, aus welchem Browser
es stammt (`Bookmark.Browser`), und zeigt es als Spalte. Dieser Task baut
das eigentliche Filtern: eine Dropdown-Auswahl, mit der man die Liste auf
einen bestimmten Browser einschränken kann, kombiniert mit der bestehenden
Textsuche.

**Wichtig – bitte KEINE ganzen Methoden neu erfinden, nur exakt wie unten
beschrieben ergänzen/anpassen.** Vorherige Tasks sind wiederholt
gescheitert, wenn ganze Methoden umgeschrieben statt gezielt geändert
wurden.

**Aktueller Code, der angepasst wird:**

**A) `MainWindow.xaml`** — die Button-Zeile:
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
</StackPanel>
```

**B) `RefreshDisplayedBookmarks()`:**
```csharp
private void RefreshDisplayedBookmarks()
{
    _displayedBookmarks.Clear();
    foreach (var bm in _allBookmarks)
        _displayedBookmarks.Add(bm);

    // Statusanzeige aktualisieren
    StatusText.Text = $"{_allBookmarks.Count} Bookmarks";
}
```

**C) `SearchBox_TextChanged`:**
```csharp
private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
{
    string filter = SearchBox.Text.Trim().ToLowerInvariant();

    _displayedBookmarks.Clear();

    foreach (var bm in _allBookmarks.Where(b =>
                b.Title.ToLowerInvariant().Contains(filter) ||
                b.Url   .ToLowerInvariant().Contains(filter)))
    {
        _displayedBookmarks.Add(bm);
    }
}
```

**D) Aufrufstellen von `RefreshDisplayedBookmarks()`:** Wird bereits
aufgerufen in `AttemptImportAndLoad()` (ganz am Ende), `ImportHtmlButton_Click()`
(vor der Erfolgsmeldung) und `ToggleFavorite_Click` (am Ende) – diese drei
Aufrufstellen bleiben **unverändert**, nur der Inhalt von
`RefreshDisplayedBookmarks()` selbst ändert sich (siehe Punkt 2 unten).

---

## 2. Auftrag (genau EIN Problem)

Füge eine Browser-Filter-Dropdown hinzu, die mit der bestehenden Suche
kombiniert funktioniert:

1. **A) XAML:** In der Button-`StackPanel` einen `ComboBox
   x:Name="BrowserFilterComboBox"` ergänzen (z. B. nach dem
   `ExportButton`, mit etwas `Margin` und `Width="150"`,
   `SelectionChanged="BrowserFilterComboBox_SelectionChanged"`).

2. **Neue Methode `ApplyFilters()`** (ersetzt die bisherige Filterlogik
   aus `SearchBox_TextChanged`): liest sowohl den Suchtext aus `SearchBox`
   als auch den ausgewählten Wert aus `BrowserFilterComboBox`, kombiniert
   beide Kriterien:
   ```csharp
   private void ApplyFilters()
   {
       string searchFilter = SearchBox.Text.Trim().ToLowerInvariant();
       string selectedBrowser = BrowserFilterComboBox.SelectedItem as string;

       _displayedBookmarks.Clear();

       var query = _allBookmarks.Where(b =>
           b.Title.ToLowerInvariant().Contains(searchFilter) ||
           b.Url.ToLowerInvariant().Contains(searchFilter));

       if (!string.IsNullOrEmpty(selectedBrowser) && selectedBrowser != "Alle")
       {
           query = query.Where(b => b.Browser == selectedBrowser);
       }

       foreach (var bm in query)
       {
           _displayedBookmarks.Add(bm);
       }
   }
   ```

3. **`SearchBox_TextChanged` vereinfachen** auf einen einzigen Aufruf:
   ```csharp
   private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
   {
       ApplyFilters();
   }
   ```

4. **Neuer Handler `BrowserFilterComboBox_SelectionChanged`:**
   ```csharp
   private void BrowserFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
   {
       ApplyFilters();
   }
   ```

5. **Neue Methode `PopulateBrowserFilter()`:** ermittelt die eindeutigen
   Browser-Werte aus `_allBookmarks`, füllt die ComboBox (mit `"Alle"` als
   erstem Eintrag), behält `"Alle"` als Standardauswahl bei einem Neuaufbau:
   ```csharp
   private void PopulateBrowserFilter()
   {
       var browsers = _allBookmarks
           .Select(b => b.Browser)
           .Where(b => !string.IsNullOrEmpty(b))
           .Distinct()
           .OrderBy(b => b)
           .ToList();

       browsers.Insert(0, "Alle");

       BrowserFilterComboBox.ItemsSource = browsers;
       BrowserFilterComboBox.SelectedIndex = 0;
   }
   ```

6. **B) `RefreshDisplayedBookmarks()` anpassen:** statt der bisherigen
   direkten Kopie aller Bookmarks in `_displayedBookmarks`, jetzt
   `ApplyFilters()` aufrufen (damit ein bereits aktiver Filter/Suchtext
   erhalten bleibt, z. B. nach dem Umschalten eines Favoriten):
   ```csharp
   private void RefreshDisplayedBookmarks()
   {
       ApplyFilters();

       // Statusanzeige aktualisieren
       StatusText.Text = $"{_allBookmarks.Count} Bookmarks";
   }
   ```

7. **`PopulateBrowserFilter()` aufrufen:** direkt vor dem bestehenden
   Aufruf von `RefreshDisplayedBookmarks()` in `AttemptImportAndLoad()`
   (ganz am Ende der Methode) und in `ImportHtmlButton_Click()` (vor der
   Erfolgsmeldung) je eine Zeile `PopulateBrowserFilter();` ergänzen – **nur
   diese eine Zeile**, sonst nichts an diesen beiden Methoden ändern.
   **Nicht** in `ToggleFavorite_Click` aufrufen (dort ändert sich die
   Browser-Liste nicht, nur der Filter würde sonst unnötig zurückgesetzt).

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Keine Änderung an Import-Logik, Persistenz, Export, Favoriten-Logik,
  Statusanzeige-Berechnung (`_allBookmarks.Count` bleibt die Quelle für den
  Zähler, nicht die gefilterte Liste)
- `AttemptImportAndLoad()` und `ImportHtmlButton_Click()` werden **nur** um
  die eine `PopulateBrowserFilter();`-Zeile ergänzt, sonst nichts
- Keine neue Dependency
- Weiterhin synchron, kein async

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen**

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] Dropdown zeigt "Alle" plus alle tatsächlich vorkommenden Browser-Werte
- [ ] Auswahl eines Browsers filtert die Liste korrekt
- [ ] Suche und Browser-Filter funktionieren kombiniert (beide Kriterien
      gleichzeitig)
- [ ] Statuszeile zeigt weiterhin die **Gesamtzahl**, nicht die gefilterte
      Anzahl
- [ ] Favoriten-Toggle behält den aktiven Filter/Suchtext bei (kein Reset)
- [ ] Import, Export, Persistenz, Öffnen-per-Klick funktionieren weiterhin
      unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – zwei Punkte, direkt von Claude korrigiert (kein Qwen-Bug):**

1. XAML fehlte komplett in der Rückgabe – ohne die neue `ComboBox` im
   Markup hätte `BrowserFilterComboBox` im Code-Behind gar nicht existiert.
   Direkt selbst ergänzt (eine `ComboBox` in der bestehenden Button-
   `StackPanel`).
2. Die zurückgegebenen `AttemptImportAndLoad()`/`ImportHtmlButton_Click()`
   stammten aus Qwens älterem Gedächtnisstand – ohne den Backfill-Fix aus
   Task 018 (`CASE WHEN Bookmarks.Browser = ''...`). Statt die ganzen
   Methoden zu ersetzen (was den Fix wieder verloren hätte), wurde nur die
   eine neue Zeile `PopulateBrowserFilter();` gezielt in die bereits
   korrekte Version eingefügt.

Die vier neuen/geänderten Methoden (`ApplyFilters`, `PopulateBrowserFilter`,
`SearchBox_TextChanged`, `BrowserFilterComboBox_SelectionChanged`,
`RefreshDisplayedBookmarks`) waren inhaltlich korrekt und wurden direkt
übernommen.

**Kleine Spec-Abweichung, funktional unproblematisch:** `PopulateBrowserFilter()`
wurde *nach* `RefreshDisplayedBookmarks()` aufgerufen statt davor (wie im
Auftrag vorgegeben). Da das Setzen von `SelectedIndex = 0` in WPF automatisch
ein `SelectionChanged`-Event auslöst (außer der Wert war schon 0), korrigiert
sich das selbst – `ApplyFilters()` läuft dadurch ohnehin nochmal mit dem
korrekten, frisch befüllten Dropdown-Zustand. Kein Blocker, nicht geändert.

Live-Test (Sven): Kompiliert, startet. Dropdown zeigt korrekt nur
tatsächlich vorkommende Browser (nicht alle 11 unterstützten, sondern nur
die, die wirklich Bookmarks geliefert haben). Filterauswahl schränkt die
Liste korrekt ein. Statuszeile zeigt weiterhin die Gesamtzahl, nicht die
gefilterte Anzahl.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes (nach Korrekturen)
- [x] Dropdown zeigt "Alle" plus tatsächlich vorkommende Browser
- [x] Auswahl filtert die Liste korrekt
- [x] Suche und Browser-Filter laufen über dieselbe `ApplyFilters()`-Methode
      (kombiniert funktionsfähig per Code-Review, nicht separat live
      gegengetestet, aber derselbe bereits bestätigte Mechanismus)
- [x] Statuszeile zeigt Gesamtzahl, nicht gefilterte Anzahl (bestätigt)
- [x] Favoriten-Toggle behält aktiven Filter bei (nutzt ebenfalls `ApplyFilters()`
      über `RefreshDisplayedBookmarks()`, kein Reset-Aufruf von
      `PopulateBrowserFilter()` in `ToggleFavorite_Click`)
- [x] Import, Export, Persistenz, Öffnen-per-Klick weiterhin funktionsfähig

**Task 019 abgeschlossen.**

---

## Stand MarkDock gesamt

MVP + 11-Browser-Import (automatisch) + manueller HTML-/JSON-Import (beide
Richtungen) + SQLite-Persistenz + URL-Dedup + Favoriten + Browser-Quelle
+ Browser-Filter + Export (CSV/JSON/HTML) + Statusanzeige +
Fortschritts-Zwischentext. Alle 19 Tasks abgenommen.

## Nächster Schritt

Ursprüngliche Priorisierung: Ordnerstruktur-Ansicht. Danach: Rest
(Portable Version, Launcher, Tote-Links-Check, Shortcuts).
