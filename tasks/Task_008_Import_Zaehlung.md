# Arbeitsvorlage – Task 008

## Task-ID

`Task_008_Import_Zaehlung_Neu_Vorhanden`

## Status

`Offen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Der Import-Button (`ImportHtmlButton_Click`) zeigt aktuell
nur die Gesamtzahl importierter Einträge an (`"{imported.Count} Bookmarks
importiert."`). Das sagt nicht aus, wie viele davon **wirklich neu** waren
und wie viele **schon vorher in der DB** existierten und nur aktualisiert
wurden (Upsert kann beides sein). Sven möchte diese Unterscheidung sehen.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

Nur die Methode `ImportHtmlButton_Click` in `MainWindow.xaml.cs` ist
betroffen – aktueller Stand:

```csharp
private void ImportHtmlButton_Click(object sender, RoutedEventArgs e)
{
    var imported = ImportFromHtmlOrJson();

    if (imported.Count == 0)
    {
        MessageBox.Show("Keine Bookmarks gefunden.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    // Datenbank-Verbindung
    string dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarkDock", "markdock.db");

    using var connection = new SqliteConnection($"Data Source={dbPath}");
    connection.Open();

    // Importierte Bookmarks in DB upserten
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

    // _allBookmarks aus der DB neu laden
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

    // UI aktualisieren
    RefreshDisplayedBookmarks();

    // Erfolgsmeldung
    MessageBox.Show($"{imported.Count} Bookmarks importiert.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
}
```

**Alle anderen Methoden der Klasse bleiben exakt so wie sie sind** – dieser
Task betrifft ausschließlich diese eine Methode.

---

## 2. Auftrag (genau EIN Problem)

Erweitere `ImportHtmlButton_Click` so, dass die Erfolgsmeldung zwischen
**neu hinzugefügten** und **bereits vorhandenen (aktualisierten)** Bookmarks
unterscheidet:

1. **Vor** der Upsert-Schleife: aktuelle Zeilenanzahl der `Bookmarks`-Tabelle
   ermitteln: `SELECT COUNT(*) FROM Bookmarks;`
2. Upsert-Schleife wie bisher unverändert durchlaufen lassen
3. **Nach** der Upsert-Schleife: Zeilenanzahl erneut ermitteln
4. Berechnen:
   - `neu = countNachher - countVorher`
   - `bereitsVorhanden = imported.Count - neu`
5. Erfolgsmeldung anpassen, z. B.:
   ```
   "{imported.Count} Bookmarks verarbeitet: {neu} neu hinzugefügt, {bereitsVorhanden} bereits vorhanden (aktualisiert)."
   ```
   (Formulierung darf leicht abweichen, Kernaussage muss aber neu vs.
   bereits vorhanden klar trennen.)

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Nur `ImportHtmlButton_Click` ändern – keine andere Methode anfassen
- Keine Änderung an der Upsert-Logik selbst (`INSERT OR REPLACE` bleibt wie
  es ist) – nur die Zählung drumherum ergänzen
- Keine Änderung am Start-Import (`AttemptImportAndLoad`) – die Zählung ist
  nur für den manuellen HTML/JSON-Import relevant
- Keine neue Dependency
- Keine UI-Änderung außer dem Text der bestehenden MessageBox

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  der vollständige aktuelle Stand der betroffenen Methode; falls der Rest
  der Datei für den vollständigen Output gebraucht wird und nicht vorliegt,
  kurz nachfragen ist hier ok

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] Import von komplett neuen Bookmarks zeigt korrekt "X neu hinzugefügt,
      0 bereits vorhanden"
- [ ] Erneuter Import derselben Datei zeigt korrekt "0 neu hinzugefügt, X
      bereits vorhanden"
- [ ] Mischfall (teils neu, teils vorhanden) zeigt beide Zahlen korrekt
- [ ] Alles andere (Suche, Öffnen, Persistenz, Start-Import) funktioniert
      unverändert weiter

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

`<offen bis Rückgabe>`
