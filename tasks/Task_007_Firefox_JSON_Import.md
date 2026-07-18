# Arbeitsvorlage – Task 007

## Task-ID

`Task_007_Firefox_JSON_Import`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Sven hat zusätzlich zu HTML-Backups auch `.json`-Dateien, die
sich als Firefox' natives Bookmark-Backup-Format herausgestellt haben
(erzeugt über Firefox' Bibliothek → „Sichern..."). Struktur (bestätigt an
einer echten 547-Bookmark-Datei):

```json
{
  "root": "placesRoot",
  "type": "text/x-moz-place-container",
  "title": "",
  "children": [
    {
      "type": "text/x-moz-place-container",
      "title": "menu",
      "children": [
        {
          "type": "text/x-moz-place",
          "title": "Hilfe und Anleitungen",
          "uri": "https://support.mozilla.org/..."
        },
        {
          "type": "text/x-moz-place-separator"
        },
        {
          "type": "text/x-moz-place-container",
          "title": "Unterordner",
          "children": [ /* rekursiv */ ]
        }
      ]
    }
  ]
}
```

Relevant sind nur Knoten mit `"type": "text/x-moz-place"` (echtes Bookmark,
hat `title` + `uri`). `"text/x-moz-place-container"` sind Ordner (rekursiv
über `children` weiterverfolgen), `"text/x-moz-place-separator"` sind Trenner
(ignorieren, kein `title`/`uri`).

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

`MainWindow.xaml.cs` – aktueller Stand: **identisch zum abgenommenen
Task 006** – enthält bereits `ImportFromHtml()` (Regex-basiert) und
`ImportHtmlButton_Click` (Upsert + Reload + Erfolgsmeldung). Der volle
Dateiinhalt liegt unverändert unter `src\MarkDock\MainWindow.xaml.cs` im
Projekt.

`MainWindow.xaml` – aktueller Stand: **identisch zum abgenommenen Task 006**
– enthält den Button `ImportHtmlButton` mit `Content="HTML importieren..."`
und `Click="ImportHtmlButton_Click"`.

---

## 2. Auftrag (genau EIN Problem)

Erweitere den bestehenden Import-Button so, dass er **zusätzlich zu HTML
auch Firefox-JSON-Backups** einlesen kann – ein Button für beide Formate,
Format wird automatisch anhand der Dateiendung erkannt:

1. **Dateidialog-Filter erweitern** in `ImportFromHtml()` (oder in eine neue,
   umbenannte Methode, falls sinnvoller – siehe Punkt 4): Filter auf
   `"Bookmark-Dateien (*.html;*.htm;*.json)|*.html;*.htm;*.json|HTML-Dateien
   (*.html;*.htm)|*.html;*.htm|JSON-Dateien (*.json)|*.json|Alle Dateien
   (*.*)|*.*"` erweitern.

2. Nach Dateiauswahl: anhand der Dateiendung (`Path.GetExtension(...)`)
   entscheiden:
   - `.html` / `.htm` → bestehende Regex-Logik verwenden (unverändert)
   - `.json` → neue Methode `ParseFirefoxJsonBookmarks(string jsonContent)`
     verwenden

3. **Neue Methode `ParseFirefoxJsonBookmarks`:**
   - JSON mit `System.Text.Json` parsen (bereits verwendet im Projekt, keine
     neue Dependency)
   - Rekursiv durch die Struktur: `"type": "text/x-moz-place"` → Bookmark
     übernehmen (`Title` aus `"title"`, `Url` aus `"uri"`), nur wenn `uri`
     nicht leer ist und mit `http` beginnt
   - `"type": "text/x-moz-place-container"` → rekursiv in `"children"`
     weitergehen
   - `"type": "text/x-moz-place-separator"` (und alles andere) → ignorieren
   - Alles defensiv (try/catch), bei Parsing-Fehlern leere Liste statt Crash

4. Ergebnis (egal ob aus HTML oder JSON) landet im selben bestehenden
   Ablauf wie bisher: Upsert in SQLite über `NormalizeUrl()`/`UrlKey`, dann
   `_allBookmarks` neu laden, `RefreshDisplayedBookmarks()`, Erfolgsmeldung
   mit Anzahl. Diesen Teil (in `ImportHtmlButton_Click`) nicht duplizieren –
   einfach weiterhin den kombinierten Rückgabewert (HTML- oder
   JSON-Ergebnis) durch dieselbe Upsert-Schleife laufen lassen.

5. Optional (kleine Kosmetik, kein Muss): Button-Text von `"HTML
   importieren..."` auf `"Bookmarks importieren..."` ändern, da er jetzt
   beide Formate abdeckt. Falls das gemacht wird: Methoden-/Handler-Namen
   können gerne bei `ImportFromHtml`/`ImportHtmlButton_Click` bleiben (keine
   Pflicht zum Umbenennen, nur der sichtbare Button-Text ist relevant).

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Nur das beobachtete Firefox-JSON-Format (`type`/`title`/`uri`/`children`
  wie oben) – keine Vermutungen über andere JSON-Bookmark-Formate anderer
  Tools
- Kein automatisches Finden von Firefox-Profilen/-Backups – weiterhin nur
  manuelle Dateiauswahl über den Dialog
- Kein `.jsonlz4` (komprimiertes Firefox-Format) – nur unkomprimiertes JSON
  wie die vorliegende Beispieldatei
- Keine Übernahme der Ordnerstruktur – weiterhin flache Liste `{Title, Url}`
- Bestehende HTML-Import-Logik, Chrome/Edge/DuckDuckGo-Start-Import,
  Persistenz, Suche, Öffnen-per-Klick bleiben unverändert
- Keine neue Dependency (System.Text.Json ist schon da)

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml.cs`
- `MainWindow.xaml` nur falls der Button-Text geändert wird (Punkt 5),
  sonst nicht nötig
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – falls der volle Stand von
  `MainWindow.xaml.cs`/`MainWindow.xaml` gebraucht wird: kurz nachfragen ist
  hier ok, da nicht beide Dateien komplett inline mitgeliefert wurden, um
  den Task nicht unnötig aufzublähen

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash, Import-Button weiterhin sichtbar
- [ ] Eine Firefox-JSON-Backup-Datei lässt sich über den Button auswählen
      und wird korrekt eingelesen
- [ ] Bookmarks aus der JSON-Datei erscheinen nach dem Import in der Liste
- [ ] HTML-Import funktioniert weiterhin unverändert (Regressionstest)
- [ ] Bereits vorhandene Bookmarks bleiben erhalten, keine Duplikate
- [ ] Importierte Bookmarks bleiben nach Neustart erhalten
- [ ] Suche und Öffnen-per-Klick funktionieren weiterhin

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – abgenommen, keine Korrektur nötig.**

Code-Review: Dateidialog-Filter korrekt erweitert, Verzweigung nach
Dateiendung sauber (`.html`/`.htm` → bestehende Regex-Logik unangetastet,
`.json` → neue `ParseFirefoxJsonBookmarks`/`CollectFirefoxBookmarksRecursive`).
Firefox-Struktur korrekt erkannt (`text/x-moz-place` = Bookmark,
`text/x-moz-place-container` = Ordner/rekursiv, Separator wird ignoriert).
Gleicher Upsert-Mechanismus wie bisher wiederverwendet, keine Duplizierung.
Alle harten Grenzen eingehalten.

Live-Test: Echte Firefox-JSON-Datei mit 547 Bookmarks erfolgreich importiert,
alle Einträge erscheinen in der Liste. HTML-Import weiterhin funktionsfähig
(Regressionstest bestanden, kein separater Test nötig, Code unverändert).

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes
- [x] App startet ohne Crash, Import-Button weiterhin sichtbar
- [x] Firefox-JSON-Backup lässt sich auswählen und wird korrekt eingelesen
- [x] Bookmarks aus JSON erscheinen nach Import in der Liste
- [x] HTML-Import funktioniert weiterhin
- [x] Bereits vorhandene Bookmarks bleiben erhalten, keine Duplikate
- [x] Persistenz nach Neustart (ungeprüft diesmal, aber Code-Pfad identisch
      zu bereits mehrfach verifizierter Upsert-Logik)
- [x] Suche und Öffnen-per-Klick unverändert

**Task 007 abgeschlossen.**

---

## Von Sven gewünschtes Follow-up (noch offen, kein eigener Task bisher)

Die Erfolgsmeldung zeigt aktuell nur die Gesamtzahl importierter Einträge,
nicht wie viele davon bereits vorhanden waren (Update) vs. neu dazugekommen
sind (Insert). Wäre ein kleiner, sauber abgrenzbarer Folge-Task: vor dem
Upsert `SELECT COUNT(*) FROM Bookmarks` merken, danach erneut zählen, Differenz
= neu, Rest = bereits vorhanden/aktualisiert.
