# Arbeitsvorlage – Task 024

## Task-ID

`Task_024_Ordner_Verschieben`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** "Ordner" sind in MarkDock kein eigenes Datenobjekt,
sondern einfach der Text im `Folder`-Feld jedes Bookmarks (Task 020/021).
Ein Bookmark "in einen Ordner verschieben" heißt technisch: das
`Folder`-Feld auf einen neuen Wert setzen. Dieser Task baut ein
Rechtsklick-Kontextmenü mit einem einfachen Auswahl-Dialog (kein Drag &
Drop – bewusst der risikoärmere, einfachere Weg).

**Wichtig – keine neue `.xaml`-Datei anlegen.** Der Auswahl-Dialog wird
komplett per C#-Code als eigenes `Window`-Objekt zusammengebaut (kein
separates Dialog-XAML-File), das hält den Dateiumfang klein und vermeidet
Risiko.

**Aktueller Code, der angepasst wird:**

**A) `MainWindow.xaml` — die `ListView`:**
```xml
<ListView x:Name="BookmarksListView"
          Grid.Row="2"
          MouseDoubleClick="BookmarksListView_MouseDoubleClick">
    <ListView.GroupStyle>
        <!-- ... unverändert ... -->
    </ListView.GroupStyle>
    <ListView.View>
        <!-- ... unverändert (GridView mit allen Spalten) ... -->
    </ListView.View>
</ListView>
```

**B) `Bookmark`-Modell** (zur Referenz, unverändert):
```csharp
public class Bookmark
{
    public string Title { get; set; } = "";
    public string Url   { get; set; } = "";
    public bool IsFavorite { get; set; } = false;
    public string Browser { get; set; } = "";
    public string Folder { get; set; } = "";
    public bool? IsDead { get; set; } = null;
}
```

---

## 2. Auftrag (genau EIN Problem)

Füge ein Rechtsklick-Kontextmenü zur `ListView` hinzu, mit dem sich ein
Bookmark in einen (bestehenden oder neuen) Ordner verschieben lässt:

1. **A) `MainWindow.xaml`:** der `ListView` ein `ContextMenu` hinzufügen
   (vor `ListView.GroupStyle`, oder danach – Reihenfolge der
   `ListView.XXX`-Attached-Properties ist in WPF nicht wichtig):
   ```xml
   <ListView.ContextMenu>
       <ContextMenu>
           <MenuItem Header="In Ordner verschieben..." Click="MoveToFolderMenuItem_Click"/>
       </ContextMenu>
   </ListView.ContextMenu>
   ```

2. **Neue Methode `MoveToFolderMenuItem_Click`** in `MainWindow.xaml.cs`:
   - Ermittelt das aktuell ausgewählte Bookmark über
     `BookmarksListView.SelectedItem as Bookmark` (falls `null`, sofort
     `return`)
   - Baut ein kleines `Window` **komplett per Code** (kein XAML-File) mit:
     - einer `TextBlock`-Beschriftung ("Ordner für \"{Titel}\":")
     - einer **editierbaren** `ComboBox` (`IsEditable = true`), deren
       `ItemsSource` die eindeutigen, bereits vorhandenen `Folder`-Werte
       aus `_allBookmarks` sind (leere Werte rausgefiltert, alphabetisch
       sortiert), und deren `Text` mit dem aktuellen `Folder`-Wert des
       Bookmarks vorbefüllt ist
     - einem "OK"- und einem "Abbrechen"-Button
   - Bei OK: `bm.Folder` auf den (getrimmten) Text der ComboBox setzen,
     per `UPDATE Bookmarks SET Folder = @folder WHERE UrlKey = @urlKey;`
     in der DB speichern, danach `RefreshDisplayedBookmarks()` aufrufen
     (das sorgt automatisch dafür, dass das Bookmark in der Gruppierung
     aus Task 022 in die neue Gruppe wandert, weil die Anzeige-Liste
     komplett neu aufgebaut wird)
   - Bei Abbrechen: nichts tun

   Vollständiges Beispiel als Orientierung:
   ```csharp
   private void MoveToFolderMenuItem_Click(object sender, RoutedEventArgs e)
   {
       if (BookmarksListView.SelectedItem is not Bookmark bm)
           return;

       var existingFolders = _allBookmarks
           .Select(b => b.Folder)
           .Where(f => !string.IsNullOrEmpty(f))
           .Distinct()
           .OrderBy(f => f)
           .ToList();

       var dialog = new Window
       {
           Title = "In Ordner verschieben",
           Width = 350,
           Height = 150,
           WindowStartupLocation = WindowStartupLocation.CenterOwner,
           Owner = this,
           ResizeMode = ResizeMode.NoResize
       };

       var stack = new StackPanel { Margin = new Thickness(10) };
       var label = new TextBlock { Text = $"Ordner für \"{bm.Title}\":", Margin = new Thickness(0, 0, 0, 10) };
       var comboBox = new ComboBox { ItemsSource = existingFolders, IsEditable = true, Text = bm.Folder, Height = 30 };

       var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
       var okButton = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 10, 0) };
       var cancelButton = new Button { Content = "Abbrechen", Width = 70 };

       bool? dialogResult = null;
       okButton.Click += (s, args) => { dialogResult = true; dialog.Close(); };
       cancelButton.Click += (s, args) => { dialogResult = false; dialog.Close(); };

       buttonPanel.Children.Add(okButton);
       buttonPanel.Children.Add(cancelButton);
       stack.Children.Add(label);
       stack.Children.Add(comboBox);
       stack.Children.Add(buttonPanel);
       dialog.Content = stack;

       dialog.ShowDialog();

       if (dialogResult == true)
       {
           string newFolder = comboBox.Text?.Trim() ?? "";
           bm.Folder = newFolder;

           string dbPath = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "MarkDock", "markdock.db");
           using var connection = new SqliteConnection($"Data Source={dbPath}");
           connection.Open();
           using var cmd = new SqliteCommand(
               "UPDATE Bookmarks SET Folder = @folder WHERE UrlKey = @urlKey;",
               connection);
           cmd.Parameters.AddWithValue("@folder", newFolder);
           cmd.Parameters.AddWithValue("@urlKey", NormalizeUrl(bm.Url));
           cmd.ExecuteNonQuery();

           RefreshDisplayedBookmarks();
       }
   }
   ```

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Kein Drag & Drop – nur der Rechtsklick-Dialog-Weg
- Keine neue `.xaml`-Datei – der Dialog wird komplett per C#-Code gebaut
- Keine Änderung an Import, Export, Suche, Browser-Filter, Favoriten,
  Tote-Links-Check, Ordner-Gruppierung selbst (nur ein neuer Weg, `Folder`
  zu ändern)
- Keine neue Dependency
- Weiterhin synchron (dieser Task braucht kein `async`)

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml` (nur die
  `ListView` mit ergänztem `ContextMenu`, Rest unverändert)
- vollständiger Inhalt der neuen Methode `MoveToFolderMenuItem_Click`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen**

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] Rechtsklick auf ein Bookmark zeigt "In Ordner verschieben..."
- [ ] Dialog zeigt bestehende Ordner zur Auswahl, erlaubt aber auch
      komplett neue Ordnernamen per Freitext
- [ ] Nach OK: Bookmark erscheint in der Liste unter der neuen
      Ordner-Gruppe
- [ ] Änderung übersteht einen Neustart der App
- [ ] Abbrechen ändert nichts
- [ ] Import, Export, Suche, Browser-Filter, Favoriten, Tote-Links-Check,
      Statusanzeige funktionieren weiterhin unverändert

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Direkt von Claude umgesetzt, ohne Qwen** – Sven hatte parallel zu viele
andere Projekte offen und bat darum, den Task diesmal selbst fertigzustellen.
Da der komplette Code bereits in der Task-Spezifikation exakt vorformuliert
war (inklusive vollständigem Beispiel-Handler), konnte er direkt 1:1
ubernommen werden – ContextMenu in der XAML ergänzt, `MoveToFolderMenuItem_Click`
in der `.cs`-Datei ergänzt.

Live-Test (Sven): funktioniert. Rechtsklick → "In Ordner verschieben..." →
Dialog mit editierbarer ComboBox (bestehende Ordner + Freitext) → nach OK
landet das Bookmark in der neuen Gruppe.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes
- [x] Rechtsklick zeigt "In Ordner verschieben..."
- [x] Dialog zeigt bestehende Ordner + erlaubt Freitext für neue
- [x] Nach OK: Bookmark erscheint in neuer Ordner-Gruppe (bestätigt)
- [x] Änderung persistiert in DB (übersteht Neustart, da direktes UPDATE)
- [x] Rest (Import, Export, Suche, Filter, Favoriten, Tote-Links-Check)
      unverändert, da nur additiv gebaut

**Task 024 abgeschlossen.**

---

## Nächster Schritt

Sven möchte zusätzlich Ordner **umbenennen** und **löschen** können –
wird als Task 025 vorbereitet.
