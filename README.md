# OMSI Hof Manager

Ein kleines, quelloffenes Windows-Programm, um **Hofdateien (`.hof`) für OMSI 2** an einer Stelle zu sammeln
und sie **mit einem Tastendruck (F5)** auf beliebig viele ausgewählte Busse zu verteilen.

Modernes Windows-11-Design (Fluent, Mica, automatischer Hell-/Dunkelmodus).

## Download

**[➜ Neueste Version herunterladen](https://github.com/DevSoeren29/Omsi-Hof-Manager/releases/latest)**

Zip entpacken, `OmsiHofManager.exe` starten – keine Installation, kein .NET nötig (Windows 10/11, 64-Bit).

> Beim ersten Start kann Windows SmartScreen warnen, weil das Programm nicht kostenpflichtig signiert ist:
> „Weitere Informationen“ → „Trotzdem ausführen“.

## Funktionen

- **Hof-Bibliothek** – deine gesammelten Hofdateien an einem Ort
  - Hinzufügen per Dateiauswahl, ganzem Ordner (inkl. Unterordner) oder **Drag & Drop**
  - **Aus OMSI importieren**: sammelt alle Hofdateien aus deinen installierten Bussen ein
    (bei gleichem Namen wird die neueste Version genommen)
  - Anzeige des echten Hof-Namens aus dem `[name]`-Abschnitt, Suche, Auswahl per Häkchen
- **Busliste** – erkennt automatisch alle Fahrzeugordner unter `OMSI 2\Vehicles`, die `.bus`-Dateien enthalten
  - OMSI wird automatisch über Steam gefunden (auch in zusätzlichen Steam-Bibliotheken)
  - Badge zeigt, wie viele der ausgewählten Hofdateien einem Bus fehlen
  - „Unvollständige“ wählt alle Busse aus, denen etwas fehlt
- **Verteilen (F5)** – kopiert die ausgewählten Hofdateien in alle ausgewählten Busse
  - identische Dateien werden übersprungen
  - optional überschreiben, optional mit **automatischer Sicherung** der alten Versionen
- **Aus Bussen entfernen** – ausgewählte Hofdateien wieder aus den Bussen löschen (in den Papierkorb)
- Auswahl, Pfad und Optionen werden gespeichert
- Fehlen Schreibrechte (z. B. unter `C:\Program Files (x86)`), bietet das Programm einen Neustart als Administrator an

## Tastenkürzel

| Taste      | Aktion                          |
|------------|---------------------------------|
| **F5**     | Hofdateien verteilen            |
| Strg + O   | Hofdateien hinzufügen           |
| Strg + I   | Aus OMSI importieren            |
| Strg + R   | Busse neu einlesen              |
| Entf       | Markierte Hofdateien aus Bibliothek entfernen |

## Benutzung

1. Programm starten – der Vehicles-Ordner wird automatisch gefunden (sonst „Durchsuchen…“).
2. Hofdateien in die Bibliothek holen (Drag & Drop, „Hinzufügen“ oder „Aus OMSI importieren“).
3. Häkchen bei den gewünschten Hofdateien und Bussen setzen.
4. **F5** drücken. Fertig.

> Hinweis: Hofdateien gehören in OMSI 2 direkt in den Fahrzeugordner (neben die `.bus`-Datei) –
> genau dorthin kopiert das Programm sie.

## Datenablage

| Was                 | Wo                                                    |
|---------------------|-------------------------------------------------------|
| Bibliothek          | `%AppData%\OmsiHofManager\Hofdateien`                 |
| Sicherungen         | `%AppData%\OmsiHofManager\Backups\<Datum_Uhrzeit>\…`  |
| Einstellungen       | `%AppData%\OmsiHofManager\settings.json`              |

**Portabler Modus:** Lege eine leere Datei `portable.txt` neben die EXE – dann landet alles im Unterordner `Daten`.

## Bauen

Voraussetzung: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (Windows 10/11).

```powershell
# Entwickeln / Starten
dotnet run --project src\OmsiHofManager

# Eigenständige EXE (ohne installiertes .NET lauffähig) nach .\publish
.\build.ps1

# Kleinere EXE, benötigt die .NET 9 Desktop Runtime
.\build.ps1 -FrameworkDependent
```

Alternativ `OmsiHofManager.sln` in Visual Studio 2022 öffnen.

## Projektstruktur

```
src/OmsiHofManager/
├─ App.xaml(.cs)            Start, Fluent-Theme (ThemeMode="System")
├─ MainWindow.xaml(.cs)     Oberfläche, Drag & Drop, Tastenkürzel
├─ Models/                  HofFile, BusFolder
├─ ViewModels/              MainViewModel (Logik), RelayCommand, ObservableObject
├─ Services/                OmsiLocator (Steam/Busse finden), HofLibrary, Distributor,
│                           HofParser, RecycleBin, AppSettings, AppPaths
└─ Assets/app.ico           erzeugt mit tools/make-icon.ps1
```

## Lizenz

MIT – siehe [LICENSE](LICENSE). Nicht offiziell mit OMSI / MR Software / aerosoft verbunden.
