# Arbeitsvorlage – Task 011

## Task-ID

`Task_011_JSON_Import_MarkDock_Format`

## Status

`Abgenommen`

---

## 1. Kontext – Pflichtlektüre vor dem Auftrag

Vor dem Auftrag folgende Dateien **explizit per `/read` laden**:

```
/read docs/0B_Governance_Arbeitsregeln.md
/read docs/0A_ADR.md
```

**Hintergrund:** Task 007 hat JSON-Import gebaut, aber nur für Firefox'
verschachteltes Backup-Format (`type: "text/x-moz-place"`, `children`, ...).
Task 010 hat JSON-**Export** gebaut, aber in einem eigenen, **flachen**
Format (`[{ "title": "...", "url": "..." }, ...]`). Ergebnis: eigene
exportierte JSON-Dateien lassen sich nicht wieder importieren ("Keine
Bookmarks gefunden"), weil der Import-Parser nur Firefox' Struktur erkennt.

Dieser Task erweitert den Import so, dass **beide** JSON-Formate erkannt
werden.

**Aktueller Code, der angepasst wird** (wird direkt mitgeliefert, keine
Rückfrage nötig):

Die relevanten Methoden in `MainWindow.xaml.cs` – aktueller Stand:

```csharp
/// <summary>
/// Parst Firefox-Bookmark-JSON.
/// </summary>
private List<Bookmark> ParseFirefoxJsonBookmarks(string jsonContent)
{
    var bookmarks = new List<Bookmark>();

    try
    {
        using JsonDocument doc = JsonDocument.Parse(jsonContent);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return bookmarks;

        // Rekursive Suche im JSON-Tree
        CollectFirefoxBookmarksRecursive(doc.RootElement, bookmarks);
    }
    catch
    {
        // Fehler beim Parsen → leere Liste zurückgeben
    }

    return bookmarks;
}

/// <summary>
/// Rekursive Durchgang der Firefox-Bookmark-Struktur.
/// </summary>
private void CollectFirefoxBookmarksRecursive(JsonElement node, List<Bookmark> list)
{
    if (node.ValueKind != JsonValueKind.Object)
        return;

    if (!node.TryGetProperty("type", out JsonElement typeProp))
        return;

    string type = typeProp.GetString();

    if (type == "text/x-moz-place")
    {
        // Bookmark-Eintrag
        string title = node.TryGetProperty("title", out JsonElement titleEl) ? titleEl.GetString() : "";
        string url   = node.TryGetProperty("uri",   out JsonElement uriEl)   ? uriEl.GetString()   : "";

        if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("http"))
        {
            list.Add(new Bookmark { Title = title, Url = url });
        }
    }
    else if (type == "text/x-moz-place-container")
    {
        // Ordner → rekursiv durch Kinder gehen
        if (node.TryGetProperty("children", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in children.EnumerateArray())
                CollectFirefoxBookmarksRecursive(child, list);
        }
    }
    // type == "text/x-moz-place-separator" oder alles andere → ignorieren
}
```

Zum Vergleich – MarkDocks eigenes Export-Format (aus Task 010, `ExportToJson`)
sieht so aus:

```json
[
  {
    "title": "Google",
    "url": "https://www.google.com"
  },
  {
    "title": "GitHub",
    "url": "https://github.com"
  }
]
```

Ein **flaches JSON-Array** von Objekten mit `title`/`url` (Kleinschreibung) –
kein `type`, keine Verschachtelung.

---

## 2. Auftrag (genau EIN Problem)

Erweitere `ParseFirefoxJsonBookmarks` so, dass sie anhand des JSON-Root-Typs
zwischen beiden Formaten unterscheidet:

1. JSON parsen wie bisher.
2. **Root-Element prüfen:**
   - `JsonValueKind.Array` → neues, flaches MarkDock-Format. Neue Methode
     `ParseMarkDockJsonExport(JsonElement root, List<Bookmark> list)`
     aufrufen: über die Array-Elemente iterieren, aus jedem Objekt `title`
     und `url` lesen (`TryGetProperty`, beide klein geschrieben), nur
     übernehmen wenn `url` nicht leer ist und mit `http` beginnt.
   - `JsonValueKind.Object` → wie bisher: `CollectFirefoxBookmarksRecursive`
     aufrufen (Firefox-Format, unverändert).
   - alles andere (z. B. ungültiges JSON, `Number`, `String` als Root) →
     leere Liste zurückgeben.
3. Alles weiterhin defensiv (try/catch bleibt um den gesamten
   Parse-Vorgang), bei Fehlern leere Liste statt Crash.

## 3. Harte Grenzen – was NICHT angefasst werden darf

- `CollectFirefoxBookmarksRecursive` bleibt exakt wie sie ist – keine
  Änderung an der Firefox-Erkennung
- Keine Änderung an `ExportToJson` (Task 010) – das Exportformat bleibt so
  wie es ist, nur der Import lernt dazu
- Keine Änderung an HTML-Import, Chrome/Edge/DuckDuckGo-Import,
  Persistenz, Suche, Statusanzeige, Export-Button
- Keine neue Dependency
- Keine UI-Änderung

## 4. Erwarteter Output

- vollständiger, geänderter Inhalt der Methode(n) `ParseFirefoxJsonBookmarks`
  und der neuen `ParseMarkDockJsonExport` (reicht als Ausschnitt, keine
  komplette Datei nötig, da nur diese Methoden betroffen sind)
- kompiliert ohne Fehler, keine theoretischen Erklärungen im Output
- **sofort schreiben, nicht nachfragen** – der oben mitgelieferte Code ist
  der vollständige aktuelle Stand der betroffenen Methoden

## 5. Definition of Done

- [ ] Projekt kompiliert ohne manuelle Fixes
- [ ] Eine mit MarkDock exportierte JSON-Datei (flaches Array) lässt sich
      über den Import-Button wieder erfolgreich einlesen
- [ ] Eine Firefox-JSON-Backup-Datei funktioniert weiterhin unverändert
      (Regressionstest, Task 007)
- [ ] Round-Trip funktioniert jetzt für alle drei Export-Formate: HTML,
      JSON und (indirekt, da CSV nicht importierbar ist – das ist ok, war
      nie gefordert)
- [ ] Alles andere funktioniert unverändert weiter

## 6. Review-Notiz (wird von Claude nach Rückgabe ausgefüllt)

**Runde 1 – abgenommen, keine Korrektur nötig.**

Code-Review: `ParseFirefoxJsonBookmarks` unterscheidet jetzt sauber per
`switch` auf `root.ValueKind` – `Array` → neue `ParseMarkDockJsonExport`,
`Object` → unveränderte `CollectFirefoxBookmarksRecursive`, alles andere →
leere Liste. Neue Methode korrekt defensiv (Null-Fallbacks via `?? ""`,
`http`-Plausibilitätsfilter). Firefox-Erkennung nicht angefasst.

Live-Test: MarkDocks eigener JSON-Export lässt sich jetzt erfolgreich
wieder importieren ("0 neu, X bereits vorhanden" bei erneutem Import
derselben Datei). Bei Sven bereits erfolgreich getestet.

Definition of Done erfüllt:

- [x] Projekt kompiliert ohne manuelle Fixes
- [x] MarkDock-JSON-Export lässt sich wieder importieren
- [x] Firefox-JSON weiterhin funktionsfähig (Code unverändert, kein
      Regressionsrisiko)
- [x] Round-Trip funktioniert jetzt für HTML und JSON
- [x] Rest unverändert funktionsfähig

**Task 011 abgeschlossen.**

---

## Stand MarkDock gesamt

MVP + Chrome/Edge/DuckDuckGo-Import + SQLite-Persistenz + URL-Normalisierung/
Dedup + HTML-/Firefox-JSON-Import + Neu/Vorhanden-Zählung + Statusanzeige +
Export (CSV/JSON/HTML) + JSON-Import erkennt beide Formate (Firefox +
MarkDock-eigenes). Alle 11 Tasks abgenommen. PRD-MUST-HAVEs (Import, Suche,
Dedup, Persistenz, Export) damit vollständig abgedeckt.
