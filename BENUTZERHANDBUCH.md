# 20 Benutzerhandbuch – MarkDock

Vollständige Anleitung für alle Funktionen von MarkDock.

---

## 1. Erster Start

Beim ersten Start durchsucht MarkDock automatisch alle unterstützten
Browser auf deinem Rechner nach Bookmarks und importiert sie. Das
passiert im Hintergrund — das Fenster erscheint sofort, kurz mit dem
Hinweis "Importiere Bookmarks...", danach ist die Liste gefüllt.

Werden **keine** Bookmarks in irgendeinem Browser gefunden, lädt MarkDock
stattdessen ein paar Demo-Bookmarks, damit die App nicht leer aussieht.

Bei jedem weiteren Start läuft derselbe Import erneut — neue Bookmarks aus
deinen Browsern kommen automatisch dazu, nichts geht dabei verloren
(bereits gesetzte Favoriten, Ordner-Zuordnungen usw. bleiben erhalten).

---

## 2. Die Bookmark-Liste

Jede Zeile zeigt:

| Spalte | Bedeutung |
|---|---|
| (Icon) | Favicon der Website (automatisch geladen) |
| ★ | Favorit — anklicken zum Umschalten |
| Quelle | Aus welchem Browser das Bookmark stammt |
| Ordner | Ordnerpfad aus dem Browser (oder selbst vergeben) |
| Status | "✓ OK" / "✗ Tot" nach einer Link-Prüfung, sonst leer |
| Titel | Bookmark-Titel |
| URL | Die eigentliche Adresse |

**Doppelklick** auf eine Zeile öffnet den Link im Standardbrowser.

### Gruppierung

Die Liste ist nach Ordner gruppiert, mit fett hervorgehobenen
Überschriften. Reihenfolge:

1. **★ Favoriten (ohne Ordner)** — favorisierte Bookmarks, die keinem
   Ordner zugewiesen sind, stehen immer ganz oben
2. **Selbst erstellte Ordner** — Ordner, die du per "In Ordner
   verschieben..." oder "Ordner umbenennen..." selbst benannt hast
3. **Importierte Ordner** — Ordnerstruktur aus deinen Browsern

Innerhalb jedes Ordners stehen favorisierte Bookmarks ebenfalls oben.

---

## 3. Suche und Filter

### Suchfeld

Oben links. Durchsucht Titel **und** URL, live während des Tippens. Ein
"✕" erscheint rechts im Feld, sobald etwas eingetragen ist — Klick leert
das Feld.

### Browser-Filter (Dropdown "Alle")

Schränkt die Liste auf einen bestimmten Browser ein. Zeigt nur Browser an,
die tatsächlich Bookmarks geliefert haben.

### Status-Filter

Klick auf die **Spaltenüberschrift "Status"** schaltet durch:
Alle → Nur tote Links → Nur OK-Links → wieder Alle. Die aktuelle Auswahl
wird kurz in der Statuszeile unten links angezeigt.

### Sortierung

- Klick auf **"Titel"**: sortiert alphabetisch innerhalb jeder
  Ordner-Gruppe (nochmal klicken = umgekehrt)
- Klick auf **"Ordner"**: schaltet die Ordner-Reihenfolge auf rein
  alphabetisch um (überschreibt bis zum nächsten Neustart die
  "eigene Ordner zuerst"-Regel)

Alle Filter/Sortierungen lassen sich beliebig kombinieren.

---

## 4. Favoriten

Klick auf den Stern (★/☆) links neben einem Bookmark schaltet den
Favoriten-Status um. Favorisierte Bookmarks:

- stehen automatisch oben in ihrer Ordner-Gruppe
- Bookmarks **ohne** Ordner-Zuordnung bekommen zusätzlich eine eigene
  Gruppe "★ Favoriten (ohne Ordner)" ganz oben in der gesamten Liste
- der Status übersteht Neustarts und erneute Importe

---

## 5. Ordner

### Bookmark in einen Ordner verschieben

Rechtsklick auf ein (oder mehrere ausgewählte) Bookmark(s) →
**"In Ordner verschieben..."**. Im Dialog entweder einen bestehenden
Ordner aus der Liste wählen oder einen komplett neuen Namen eintippen.

Neu erstellte Ordner werden automatisch als "eigene" markiert und
erscheinen fortan oben in der Sortierung (vor den aus Browsern
importierten Ordnern).

### Ordner umbenennen / auflösen

Rechtsklick auf die **Ordner-Überschrift** selbst (die fett gedruckte
graue Zeile, nicht auf ein einzelnes Bookmark):

- **"Ordner umbenennen..."** — ändert den Namen für alle Bookmarks in
  diesem Ordner auf einmal
- **"Ordner auflösen..."** — entfernt die Ordner-Zuordnung von allen
  Bookmarks darin (Bookmarks bleiben erhalten, landen nur wieder
  "ohne Ordner")

Auf die Spezial-Gruppe "★ Favoriten (ohne Ordner)" haben diese beiden
Optionen keine Wirkung (das ist kein echter Ordner).

---

## 6. Mehrfachauswahl

Strg+Klick oder Umschalt+Klick wählt mehrere Bookmarks gleichzeitig aus.
Rechtsklick auf eine bereits markierte Zeile behält die gesamte Auswahl
bei (Windows-Explorer-Verhalten). Folgende Aktionen wirken dann auf
**alle** ausgewählten Bookmarks:

- Löschen
- In Ordner verschieben
- Als OK / Als tot markieren

**"Bearbeiten..."** funktioniert bewusst nur mit genau einem ausgewählten
Bookmark (Titel/URL sind pro Bookmark eindeutig).

---

## 7. Bookmark bearbeiten und löschen

**Bearbeiten:** Rechtsklick → "Bearbeiten..." öffnet einen Dialog mit
Titel- und URL-Feld. Falls die neue URL bereits bei einem anderen
Bookmark existiert, erscheint eine Warnung statt eines stillen
Datenverlusts.

**Löschen:** Rechtsklick → "Löschen" (mit Sicherheitsabfrage, zeigt bei
Mehrfachauswahl die Anzahl).

---

## 8. Tote Links prüfen und entfernen

**"Links prüfen"** prüft alle aktuell **sichtbaren** (gefilterten)
Bookmarks per echter Netzwerkanfrage. Läuft asynchron im Hintergrund —
die App bleibt währenddessen bedienbar, ein Fortschritt wird in der
Statuszeile angezeigt.

Bewusst konservativ: Nur eindeutige "nicht mehr vorhanden"-Antworten
(HTTP 404/410) und komplette Verbindungsfehler gelten als "tot".
Blockierungen durch Bot-Schutz, Login-Wände oder temporäre
Serverprobleme (403, 429, 500 usw.) zählen **nicht** als tot, um falsche
Löschungen zu vermeiden.

Ergebnis erscheint in der Status-Spalte ("✓ OK" / "✗ Tot") und übersteht
einen Neustart.

**Manuell korrigieren:** Falls die automatische Prüfung mal danebenliegt,
Rechtsklick → "Als OK markieren" / "Als tot markieren" setzt den Status
von Hand.

**"Tote Links entfernen"** zeigt eine Liste aller aktuell als tot
markierten Bookmarks zur Kontrolle, bevor sie auf Bestätigung endgültig
gelöscht werden.

---

## 9. Import und Export

### Manueller Import

**"HTML importieren..."** öffnet einen Dateidialog für `.html`/`.htm`-
oder `.json`-Dateien (unterstützt sowohl klassische Browser-Exporte als
auch Firefox-JSON-Backups und MarkDocks eigenes Export-Format). Die
Meldung danach zeigt, wie viele Bookmarks neu hinzugekommen sind und wie
viele schon vorhanden waren.

### Export

**"Exportieren..."** speichert die aktuell **sichtbare** (gefilterte)
Liste als CSV, JSON oder HTML — je nach gewählter Dateiendung. Praktisch:
erst per Suche/Filter eingrenzen, dann gezielt nur diesen Ausschnitt
exportieren.

---

## 10. Backup und Wiederherstellung

**"Backup erstellen"** kopiert die komplette Datenbank-Datei an einen
gewählten Ort — im Gegensatz zum normalen Export ist das ein
vollständiges 1:1-Abbild aller Daten (inklusive Favoriten, Ordner,
Status usw.), keine gefilterte Teilmenge.

**"Backup wiederherstellen"** ersetzt die aktuelle Datenbank durch eine
gewählte Backup-Datei (mit Sicherheitsabfrage, da das die aktuellen Daten
überschreibt).

---

## 11. Hell-/Dunkel-Design

Der runde blaue Button (🌙/☀) schaltet zwischen einem hellen und einem
augenschonenden dunklen Farbschema um. Die Wahl gilt nur für die laufende
Sitzung (kein Neustart-Speicher, startet immer hell).

---

## 12. Schnellstart-Launcher

Der schnellste Weg, ein Bookmark zu öffnen, ohne das Hauptfenster
überhaupt zu benutzen:

1. Tastenkombination drücken (standardmäßig **Strg+Leertaste**, siehe
   unten zum Ändern) — oder Klick auf den **"🚀 Launcher"**-Button
2. Ein kleines, schwebendes Suchfenster erscheint
3. Tippen — sofort erscheinen bis zu 8 passende Treffer (durchsucht Titel
   und URL)
4. **Pfeiltasten** zum Navigieren, **Enter** öffnet den ausgewählten Link
   direkt im Browser, Popup schließt sich automatisch
5. **Escape** schließt ohne zu öffnen
6. Klick woanders hin schließt das Popup ebenfalls

### Tastenkombination ändern

Klick auf den **⚙-Button** neben "🚀 Launcher", dann im Dialog einfach die
gewünschte neue Kombination drücken (mindestens eine Zusatztaste wie
Strg/Alt/Umschalt/Win ist Pflicht, sonst würden normale Tasten
systemweit blockiert). "Speichern" — die neue Kombination ist sofort
aktiv und übersteht auch einen Neustart.

Falls die gewählte Kombination bereits von einem anderen Programm belegt
ist, erscheint eine Fehlermeldung und die alte Kombination bleibt aktiv.

**Hinweis:** Die Standard-Kombination Strg+Leertaste wird von manchen
Programmen (z. B. Entwicklungsumgebungen) für Autovervollständigung
genutzt. Falls dir auffällt, dass sowas woanders nicht mehr funktioniert,
solange MarkDock läuft, ändere die Kombination über den ⚙-Button auf
etwas wie Strg+Alt+B.

---

## 13. Datenspeicherort

Alle Daten liegen in einer einzigen SQLite-Datei:

```
%LOCALAPPDATA%\MarkDock\markdock.db
```

Kein Cloud-Zugriff, keine Übertragung an Dritte. Backup/Wiederherstellung
direkt über die App-Funktionen (Abschnitt 10) statt manuellem
Datei-Kopieren empfohlen, funktioniert aber auch so, solange die App
dabei geschlossen ist.

---

## 14. Unterstützte Browser (automatischer Import)

| Browser | Format |
|---|---|
| Chrome, Edge, Opera, Brave, Vivaldi, SRWare Iron, Comodo Dragon | Chromium-JSON |
| Firefox, Zen, Floorp, Waterfox | Gecko/`places.sqlite` |
| DuckDuckGo | Heuristisch (oft verschlüsselt, dann 0 Ergebnisse — kein Fehler) |

Alle Browser werden bei jedem Start automatisch neu gescannt, unabhängig
davon, welche gerade installiert sind — nicht gefundene Browser werden
einfach übersprungen, kein Fehler sichtbar.
