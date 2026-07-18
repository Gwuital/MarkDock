# Arbeitsvorlage – Task 001

## Task-ID

`Task_001_MVP_Bootstrap`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**, in dieser Reihenfolge:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
/read docs/A2_Vorgabe.md
/read docs/0C_Claude_Qwen_Projektinitialisierung.md
```

Diese vier Dokumente sind für diesen Task bindend. Die volle Architektur aus
`A1_Startprompt.md` (Clean Architecture, 5 Projekte, DI, Multi-Browser-Import)
ist für diesen Task **ausdrücklich nicht relevant** – das kommt erst in späteren
Tasks (Ausbaustufen laut `docs/10_Roadmap.md`, Phase 2+).

## 2. Auftrag (genau EIN Problem)

Baue eine **kompilierbare, lauffähige** Windows-Desktop-Anwendung (C# / .NET 8 / WPF)
namens **MarkDock**, die den in `A2_Vorgabe.md` beschriebenen Single-Run-Build
umsetzt:

- 1 WPF-Fenster: Suchfeld oben, Liste darunter
- Beim Start werden **Demo-Bookmarks** geladen (kein echter Browser-Import in
  diesem Task – das ist Task 002+)
- Suche filtert die Liste live, case-insensitive
- Klick/Doppelklick auf einen Eintrag öffnet die URL im Standardbrowser
  (`Process.Start`)
- Datenmodell minimal: `Bookmark { Title, Url }`

Ziel ist **ein Mal bauen → starten → funktioniert**, nicht Perfektion.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Kein Chrome-/Edge-/DuckDuckGo-Import (kommt in eigenem Task)
- Kein SQLite (In-Memory-Liste reicht für diesen Task)
- Keine Multi-Projekt-Solution (1 WPF-Projekt genügt)
- Keine Clean-Architecture-Aufteilung (UI/Core/Infrastructure/Importer/Database)
  – das ist erst ab dem Task, der explizit auf `A1_Startprompt.md` verweist
- Kein Favicon-Handling
- Bei jeder Unsicherheit: einfachste Lösung wählen (ADR-999 Regel 3 + 9)

## 4. Erwarteter Output

- vollständige Projektstruktur (1 `.sln` + 1 WPF-Projekt)
- vollständiger Code aller Dateien
- kurze Anleitung: „öffnen → builden → starten" (keine weiteren Erklärungen)

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] App startet ohne Crash
- [ ] Demo-Bookmarks sind sichtbar
- [ ] Suche funktioniert (live, case-insensitive)
- [ ] Klick öffnet Link im Standardbrowser

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – nicht abgenommen.** Zwei Blocker gefunden:

1. **Compile-Fehler:** `MainWindow.xaml` setzt `PlaceholderText` auf der `TextBox`.
   Diese Property existiert in WPF nicht (nur UWP/WinUI) → Build schlägt fehl.
2. **Kein sichtbares Fenster:** `App.xaml` hat kein `StartupUri` gesetzt, und
   `App.xaml.cs` zeigt auch sonst nirgends das `MainWindow` an. App würde zwar
   crashfrei starten, aber ohne UI – DoD-Punkt "Demo-Bookmarks sind sichtbar"
   nicht erfüllbar.

Rest (Suche live/case-insensitive, Doppelklick öffnet URL via `Process.Start`,
Datenmodell `Bookmark{Title,Url}`, In-Memory-Liste, keine Scope-Überschreitung)
ist sauber und korrekt umgesetzt.

→ Korrekturauftrag an Qwen/GPT nachgeschickt (Fix Runde 1).

---

**Runde 2 – abgenommen.** Beide Blocker korrekt behoben:

1. `PlaceholderText` aus `MainWindow.xaml` entfernt – TextBox jetzt gültiges WPF.
2. `StartupUri="MainWindow.xaml"` in `App.xaml` gesetzt – Fenster erscheint beim Start.

Keine Kollateraländerungen am restlichen Code (Code-behind, Bookmark-Modell,
Suchlogik, Doppelklick-Handler unverändert). Definition of Done vollständig
erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes
- [x] App startet ohne Crash
- [x] Demo-Bookmarks sind sichtbar
- [x] Suche funktioniert (live, case-insensitive)
- [x] Klick öffnet Link im Standardbrowser

**Task 001 abgeschlossen.** Nächster Schritt: Task 002 (Browser-Import
Chrome/Edge, siehe `docs/05_Browser_Importer.md` – read-only, Bookmarks-JSON
Parsen), sobald gewünscht.
