# Wichtig – Portable MarkDock.exe

## Unterschied zwischen den beiden .exe-Dateien

Nach `dotnet publish` liegen zwei verschiedene `MarkDock.exe` herum – nur
eine davon ist wirklich portabel:

| | `win-x64\MarkDock.exe` | `win-x64\publish\MarkDock.exe` |
|---|---|---|
| Enthält .NET-Laufzeit + WPF? | Ja, aber als ~150 einzelne lose DLL-Dateien daneben | Ja, alles in **einer** Datei gebündelt |
| Kann man die eine Datei einfach kopieren? | Nein – ohne die DLLs daneben startet sie nicht | Ja – genau das ist der Sinn |
| Wofür gedacht? | Build-Zwischenergebnis, entsteht automatisch beim Kompilieren | Das eigentliche Endprodukt zum Weitergeben |

Kurz: **immer die Datei aus dem `publish`-Unterordner nehmen.** Die andere
ist quasi ein Nebenprodukt des Bauens, kein fertiges Programm zum
Verteilen.

Die fertige, wirklich **portable** MarkDock.exe (Einzeldatei, alles
eingebaut, läuft ohne .NET-Installation) liegt nach dem Publish-Befehl
hier:

```
MarkDock\src\MarkDock\bin\Release\net8.0-windows\win-x64\publish\MarkDock.exe
```

**Wichtig: der `publish`-Unterordner ist Pflicht.** Der Ordner eine Ebene
darüber (`win-x64\` ohne `publish`) enthält zwar auch eine `MarkDock.exe`,
aber daneben liegen noch ~150 lose DLL-Dateien — das ist nur ein
Build-Zwischenergebnis, keine echte portable Einzeldatei. Nur die `.exe`
aus dem `publish`-Ordner lässt sich problemlos allein kopieren (USB-Stick,
anderer Rechner usw.).

## Desktop-Verknüpfung erstellen

1. Im Explorer zu `...\win-x64\publish\` navigieren
2. Rechtsklick auf `MarkDock.exe` → **"Senden an" → "Desktop (Verknüpfung
   erstellen)"**

## Neu bauen (nach Code-Änderungen)

```
cd C:\AI_Projekte\Projekt_J\MarkDock\src\MarkDock
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
