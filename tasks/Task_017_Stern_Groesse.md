# Arbeitsvorlage – Task 017

## Task-ID

`Task_017_Stern_Groesse`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** In Task 016 wurde die Favoriten-Spalte mit Sternsymbol
(★/☆) eingebaut. Sven fand die Sterne zu klein.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

`MainWindow.xaml` – relevanter Ausschnitt (Rest der Datei bleibt komplett
unverändert):

```xml
<GridViewColumn Header="★" Width="30">
    <GridViewColumn.CellTemplate>
        <DataTemplate>
            <Button Content="{Binding IsFavorite, Converter={StaticResource BoolToStarConverter}}"
                    Click="ToggleFavorite_Click"
                    Background="Transparent"
                    BorderThickness="0"/>
        </DataTemplate>
    </GridViewColumn.CellTemplate>
</GridViewColumn>
```

---

## 2. Auftrag (genau EIN Problem)

Vergrößere ausschließlich die Sternsymbol-Darstellung in dieser einen
`GridViewColumn`:

1. Dem `Button` `FontSize="18"` hinzufügen (aktuell keine explizite
   `FontSize` gesetzt, erbt die Standardgröße – deutlich zu klein für ein
   Stern-Symbol).
2. Die `Width` der `GridViewColumn` von `30` auf `36` erhöhen, damit der
   größere Stern nicht abgeschnitten wird.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Nur diese eine `GridViewColumn` (Header `"★"`) ändern – Titel-/URL-Spalten,
  restliche XAML, `.cs`-Datei bleiben komplett unangetastet
- Keine Änderung an `ToggleFavorite_Click`, `BoolToStarConverter` oder
  sonstiger Logik
- Kein neues Control, keine neue Dependency

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt von `MainWindow.xaml`
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Ausschnitt
  reicht als Kontext, falls die volle Datei gebraucht wird: kurz nachfragen
  ist hier ok

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] Sternsymbol ist sichtbar größer als vorher
- [ ] Alles andere (Suche, Import, Export, Statusanzeige, Öffnen-per-Klick,
      Favoriten-Toggle-Funktion) funktioniert unverändert weiter

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – zwei Punkte:** `FontSize="18"` fehlte (Kern des Auftrags nicht
umgesetzt, nur `Width` wurde geändert), außerdem fehlte erneut `xmlns:local`
für `BoolToStarConverter` – Qwen hat die Datei offenbar aus einem älteren
Gedächtnisstand rekonstruiert, ohne die eigene frühere Korrektur (von
Claude direkt gefixt, nie an Qwen zurückgemeldet) zu kennen. Beides von
Claude direkt korrigiert, kein Grund für weitere Qwen-Runde.

Live-Test: Sterne sichtbar größer, alles andere unverändert funktionsfähig.

**Task 017 abgeschlossen.**

---

## Nächster Schritt

Ursprüngliche Priorisierung: Browser-Filter, Ordnerstruktur-Ansicht.
