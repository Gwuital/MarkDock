# Arbeitsvorlage – Template (MarkDock)

Dieses Template ist die Vorlage für jedes einzelne Task-Paket, das an
Qwen (oder GPT) als Implementierer geht. Rollen siehe `docs/0B_Governance_Arbeitsregeln.md`:

- **Product Owner (Sven):** entscheidet Scope & Freigabe
- **Claude (dieses Dokument):** schreibt Task-Pakete, reviewt Ergebnisse
- **Qwen / GPT:** implementiert exakt das Paket, nichts darüber hinaus

> Eine Kopie dieser Datei pro Task anlegen: `Task_XXX_<kurzer-name>.md`

---

## Task-ID

`Task_XXX`

## Status

`Offen` / `In Arbeit` / `Zur Review` / `Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Bevor irgendetwas implementiert wird, müssen folgende Dateien **explizit per `/read`
geladen** werden (Qwen/GPT halluziniert Inhalte, wenn Dateien nicht vorab geladen sind –
bekanntes Muster aus Hexabyte_AI und Projekt K):

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
/read docs/<weitere relevante Docs für diesen Task>
```

## 2. Auftrag (genau EIN Problem – ADR-999 Regel 2)

<Präzise, eng umrissene Aufgabenbeschreibung. Kein "und mach auch noch...".>

## 3. Harte Grenzen – was NICHT angefasst werden darf

- Keine Architekturentscheidungen treffen, die nicht in den Docs stehen
- Keine neuen Dependencies ohne Begründung (ADR-999 Regel 12)
- Keine Refactorings an funktionierendem Code (ADR-999 Regel 4)
- Kein Scope über diesen Task hinaus

## 4. Erwarteter Output

- vollständiger, kompilierbarer Code (kein Pseudocode, kein "man könnte")
- vollständige Datei- bzw. Projektstruktur, falls neu
- keine theoretischen Erklärungen im Output – nur die Lösung
- **sofort schreiben, nicht nachfragen:** alle für den Task nötigen Dateiinhalte
  (auch bestehender Code, der geändert werden soll) werden im Task-Paket
  mitgeliefert. Also direkt die vollständige(n) Datei(en) ausgeben – nicht erst
  nach dem aktuellen Stand fragen, das kostet nur eine Runde.

## 5. Definition of Done (ADR-999 Regel 15)

- [ ] implementiert
- [ ] kompiliert ohne Fehler
- [ ] Anwendung startet ohne Crash
- [ ] bestehende Funktionen weiterhin funktionsfähig
- [ ] entspricht den referenzierten Docs

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

`<offen bis Rückgabe>`

---

**Faustregel:** Wenn ein Task nicht in 1–2 Sätzen zusammenfassbar ist, ist er zu groß
und muss aufgeteilt werden (ADR-999 Regel 2 + 5).
