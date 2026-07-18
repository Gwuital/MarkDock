# MarkDock

*[English version below](#english)*

Ein lokales, datenschutzfreundliches Bookmark-Verwaltungstool für Windows.
Kein Cloud-Zwang, keine Konten, keine Werbung — deine Bookmarks bleiben auf
deinem Rechner.

## Wie dieses Projekt entstanden ist

MarkDock ist komplett im Zusammenspiel zweier KI-Modelle entstanden, mit
mir als Product Owner und Tester:

- **[Claude](https://claude.ai) (Anthropic)** — Architektur, Code-Review,
  Aufgaben-Planung, Fehleranalyse
- **Qwen3-Coder 30B** — lokal laufendes Modell (RTX 4090), übernimmt die
  eigentliche Code-Umsetzung nach Vorgabe von Claude

Der Ablauf: Claude entwirft eine präzise Aufgabenbeschreibung, Qwen3-Coder
setzt sie um, Claude prüft das Ergebnis auf Bugs und Regressionen, ich
teste jede Änderung live in der laufenden App. Alle Entwicklungsschritte
sind nachvollziehbar in [`tasks/`](tasks/) dokumentiert — inklusive
gefundener Bugs und Korrekturrunden. Kein Vibe-Coding auf Vertrauen: jede
Zeile wurde geprüft, bevor sie committet wurde.

## Was MarkDock kann

- **Automatischer Import** aus 11 Browsern (Chrome, Edge, Opera, Brave,
  Vivaldi, SRWare Iron, Comodo Dragon, DuckDuckGo, Firefox, Zen, Floorp,
  Waterfox) — läuft bei jedem Start automatisch im Hintergrund
- **Manueller Import** aus HTML- oder JSON-Bookmark-Dateien
- **Export** als CSV, JSON oder HTML
- **Volltextsuche** über Titel und URL, live während des Tippens
- **Filter** nach Browser-Quelle und Link-Status (OK/Tot)
- **Favoriten** mit Sternsymbol, erscheinen automatisch oben in der Liste
- **Ordner** (aus dem Browser übernommen oder selbst angelegt), inklusive
  Umbenennen, Auflösen und Verschieben von Bookmarks per Rechtsklick
- **Tote-Links-Prüfung** mit anschließendem Massen-Löschen
- **Mehrfachauswahl** für Sammel-Aktionen (Löschen, Verschieben, Markieren)
- **Bookmark manuell bearbeiten** (Titel/URL korrigieren)
- **Backup & Wiederherstellung** der kompletten Datenbank
- **Hell-/Dunkel-Design**, augenschonend
- **Schnellstart-Launcher**: global per Tastenkombination (standardmäßig
  Strg+Leertaste, frei änderbar) ein Suchfenster öffnen, tippen, Enter,
  fertig — ohne das Hauptfenster überhaupt zu öffnen

## Technik

- **.NET 8** / **WPF** (Windows-Desktop-Anwendung)
- **SQLite** (`Microsoft.Data.Sqlite`) für lokale Speicherung, keine
  externe Datenbank nötig
- Keine Cloud-Anbindung, keine Telemetrie

## Starten

```
cd src\MarkDock
dotnet run
```

Voraussetzung: .NET 8 SDK installiert.

## Portable Version selbst bauen

Die fertige `.exe` gibt's bereits fix und fertig unter
[Releases](../../releases) zum Download — für die meisten reicht das.

Wer stattdessen selbst eine portable Einzeldatei bauen möchte (läuft ohne
.NET-Installation auf jedem Windows-Rechner):

```
cd src\MarkDock
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Die fertige Datei liegt danach unter:

```
src\MarkDock\bin\Release\net8.0-windows\win-x64\publish\MarkDock.exe
```

**Wichtig:** Es entsteht dabei zusätzlich eine zweite `MarkDock.exe` eine
Ebene höher (`win-x64\` ohne `publish`) — das ist nur ein
Build-Zwischenergebnis mit ~150 losen DLL-Dateien daneben, keine echte
portable Einzeldatei. Immer die Version aus dem `publish`-Unterordner
verwenden.

## Wo liegen meine Daten?

```
%LOCALAPPDATA%\MarkDock\markdock.db
```

Eine einzelne SQLite-Datei. Backup/Wiederherstellung direkt über den
"Backup erstellen"/"Backup wiederherstellen"-Button in der App möglich.

## Ausführliche Anleitung

Siehe [`BENUTZERHANDBUCH.md`](BENUTZERHANDBUCH.md) für eine
vollständige Beschreibung aller Funktionen.

## Projektstruktur

```
MarkDock/
├── docs/               ← Dokumentation (Anforderungen, Architektur)
├── src/MarkDock/        ← Quellcode (WPF-Anwendung)
├── tasks/               ← Entwicklungs-Task-Protokolle (Claude/Qwen-Workflow)
├── LICENSE               ← MIT-Lizenz
└── README.md             ← diese Datei
```

## Lizenz

MIT — siehe [`LICENSE`](LICENSE). Frei nutzbar, veränderbar und
weitergebbar, auch kommerziell, solange der Copyright-Hinweis erhalten
bleibt.

---

<a id="english"></a>

# MarkDock (English)

A local, privacy-friendly bookmark manager for Windows. No cloud
requirement, no accounts, no ads — your bookmarks stay on your machine.

## How this project was built

MarkDock was built entirely through the collaboration of two AI models,
with me acting as product owner and tester:

- **[Claude](https://claude.ai) (Anthropic)** — architecture, code review,
  task planning, bug analysis
- **Qwen3-Coder 30B** — running locally (RTX 4090), handles the actual
  code implementation based on Claude's specifications

The workflow: Claude drafts a precise task specification, Qwen3-Coder
implements it, Claude reviews the result for bugs and regressions, I test
every change live in the running app. The full development history is
documented in [`tasks/`](tasks/) — including bugs found and correction
rounds. No blind vibe-coding: every line was reviewed before being
committed.

## What MarkDock can do

- **Automatic import** from 11 browsers (Chrome, Edge, Opera, Brave,
  Vivaldi, SRWare Iron, Comodo Dragon, DuckDuckGo, Firefox, Zen, Floorp,
  Waterfox) — runs automatically in the background on every launch
- **Manual import** from HTML or JSON bookmark files
- **Export** as CSV, JSON, or HTML
- **Full-text search** across title and URL, live while typing
- **Filters** by browser source and link status (OK/Dead)
- **Favorites** with a star icon, automatically shown at the top of the
  list
- **Folders** (inherited from browsers or created manually), including
  renaming, dissolving, and moving bookmarks via right-click
- **Dead link checking** with subsequent bulk deletion
- **Multi-select** for batch actions (delete, move, mark)
- **Manual bookmark editing** (fix title/URL)
- **Backup & restore** of the full database
- **Light/dark theme**, easy on the eyes
- **Quick launcher**: open a search popup globally via a hotkey (default
  Ctrl+Space, freely configurable), type, hit Enter, done — without ever
  opening the main window

## Tech stack

- **.NET 8** / **WPF** (Windows desktop application)
- **SQLite** (`Microsoft.Data.Sqlite`) for local storage, no external
  database required
- No cloud connectivity, no telemetry

## Running it

```
cd src\MarkDock
dotnet run
```

Requires the .NET 8 SDK.

## Building the portable version yourself

A ready-to-use `.exe` is already available for download under
[Releases](../../releases) — that's enough for most people.

If you'd rather build your own portable single-file executable (runs on
any Windows machine without a .NET installation):

```
cd src\MarkDock
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The finished file ends up here:

```
src\MarkDock\bin\Release\net8.0-windows\win-x64\publish\MarkDock.exe
```

**Note:** this also produces a second `MarkDock.exe` one level up
(`win-x64\` without `publish`) — that's just an intermediate build
artifact with ~150 loose DLL files next to it, not a real portable
single file. Always use the one from the `publish` subfolder.

## Where is my data stored?

```
%LOCALAPPDATA%\MarkDock\markdock.db
```

A single SQLite file. Backup/restore available directly via the
"Backup erstellen"/"Backup wiederherstellen" buttons in the app.

## Full user guide

See [`BENUTZERHANDBUCH.md`](BENUTZERHANDBUCH.md) (German) for a complete
description of all features.

## Project structure

```
MarkDock/
├── docs/               ← Documentation (requirements, architecture)
├── src/MarkDock/        ← Source code (WPF application)
├── tasks/               ← Development task logs (Claude/Qwen workflow)
├── LICENSE               ← MIT license
└── README.md             ← this file
```

## License

MIT — see [`LICENSE`](LICENSE). Free to use, modify, and redistribute,
including commercially, as long as the copyright notice is retained.
