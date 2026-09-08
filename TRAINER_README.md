# Steigungssimulator — Elite Tuo über BLE FTMS

Steuert die Magnetbremse eines Elite Tuo aus Unity. Steigung und Luftwiderstand
werden aus physikalisch sinnvollen Größen berechnet, Speed, Trittfrequenz und
Leistung kommen live vom Trainer zurück.

Getestet mit Unity 6.3 LTS (6000.3.23f1) unter Windows 11.

## Öffnen

Dieser Ordner **ist** das Unity-Projekt. Nichts kopieren, nichts einbinden:

> Unity Hub → `Add` → `Add project from disk` → diesen Ordner wählen → öffnen

Der erste Start dauert ein paar Minuten, weil Unity `Library/` neu aufbaut. Das
ist Cache und gehört nicht ins Repo (steht in `.gitignore`).

Der Ordner ist gleichzeitig das Git-Repo. Einfach hier `git init` bzw. den Inhalt
ins bestehende Repo legen.

**Zu den `.meta`-Dateien:** Die gehören ins Repo. Darin stehen die GUIDs, über die
die Szene ihre Scripts findet, und beim DLL-Meta die Einstellung „Editor-Plugin
aktiviert". Fehlen sie, zeigt Unity „Missing Script". Für die Dateien unter
`Assets/Scripts/` und `Assets/Lernen/` sind noch keine dabei — die erzeugt Unity
beim ersten Import selbst. Danach mitcommitten.

## Lernmaterial

`Assets/Lernen/` erklärt, wie die Berechnung funktioniert — ohne Bluetooth-Details:

| Datei | Inhalt |
|---|---|
| `01_Wie_der_Widerstand_berechnet_wird.md` | Die drei Widerstände, warum Luft quadratisch wächst, Zahlenbeispiele |
| `02_CSharp_Notizen.md` | Die C#-Konstrukte aus diesem Projekt, jeweils mit Python-Gegenstück |
| `PhysikDemo.cs` | Zum Ausprobieren: rechnet im Editor Schritt für Schritt vor |

`PhysikDemo` braucht weder Trainer noch Play-Modus: leeres GameObject anlegen,
`Add Component` → `Physik Demo`, Werte einstellen, dann im Kontextmenü der
Komponente (drei Punkte ⋮) einen der vier Punkte wählen. Das Ergebnis erscheint
in der Konsole.

## Benutzen

1. Szene `Assets/BLE_FTMS_IndoorBike/test.unity` öffnen
2. Play drücken
3. Im Game-Fenster auf **Connect** klicken
4. Warten bis in der Konsole `[BLE] bereit` steht
5. Slider bewegen

Der `BleTrainer` wird zur Laufzeit von `FTMS_UI.connect()` erzeugt — er liegt
nicht in der Szene und taucht erst im Play-Modus im Inspector auf.

Entwickeln ohne Hardware: am GameObject `FTMS_UI` das Häkchen
**`Use Mock Trainer`** setzen.

---

# Die Physik

## Wer rechnet was

Das ist der wichtigste Punkt zum Verständnis:

| | rechnet | wie oft |
|---|---|---|
| **Unity** | Steigung aus der Landschaft, Kalibrierung, Koeffizienten | 1× pro Sekunde gesendet |
| **Trainer** | Bremskraft aus Koeffizienten × gemessener Geschwindigkeit | intern, laufend |

Wir schicken dem Tuo mit Opcode `0x11` nur **Koeffizienten** — Steigung, Crr,
Cw. Er misst selbst, wie schnell das Hinterrad läuft, und stellt die Bremse
kontinuierlich nach. Der quadratische Luftwiderstand entsteht also **im Trainer**
und ist nicht an unser 1-Hz-Sendelimit gebunden. Deshalb reagiert er sofort,
wenn du schneller trittst.

`RidePhysics.cs` enthält dieselben Formeln nochmal in C#. Nicht um den Trainer zu
ersetzen, sondern um vorherzusagen, wieviel Watt eine Situation kosten wird —
für die UI, fürs Kalibrieren, und um zu prüfen ob der Trainer plausibel bremst.

## Die drei Widerstände

```
LUFT       F = 0.5 · rho · CdA · v²        quadratisch mit v
           P = 0.5 · rho · CdA · v³        kubisch mit v

ROLLEN     F = Crr · m · g                 konstant
           P = Crr · m · g · v             linear mit v

STEIGUNG   F = m · g · (Steigung% / 100)   konstant
           P = m · g · (Steigung% / 100) · v
```

Der Luftwiderstand ist der Grund, warum schnell fahren so unverhältnismäßig
anstrengend wird. Bei CdA 0.32 und Meereshöhe:

| Tempo | Leistung nur gegen Luft |
|---|---|
| 10 km/h | 4 W |
| 20 km/h | 34 W |
| 30 km/h | 113 W |
| 40 km/h | 269 W |
| 50 km/h | 525 W |

Doppeltes Tempo, achtfache Leistung. Bis etwa 20 km/h dominiert der
Rollwiderstand, darüber übernimmt die Luft.

## Die Einstellungen im Inspector

**Fahrer + Rad**

| Feld | Standard | Bedeutung |
|---|---|---|
| `riderMassKg` | 70 | Körpergewicht |
| `bikeMassKg` | 9 | Radgewicht |

Die Masse geht in Rollwiderstand und Steigung ein, nicht in den Luftwiderstand.

**Luftwiderstand**

| Feld | Standard | Bedeutung |
|---|---|---|
| `cdA` | 0.32 | Stirnfläche × Beiwert in m². Bestimmt den Luftwiderstand fast allein. |
| `airDensity` | 1.225 | Luftdichte kg/m³. 1.225 = Meereshöhe, 1.167 = ca. 500 m |
| `windCoefficientScale` | 1.0 | Korrekturfaktor, siehe unten |

CdA-Richtwerte: 0.22 Zeitfahren · 0.28 Unterlenker · **0.32 Oberlenker** ·
0.40 Gravel aufrecht · 0.55 Citybike.

**Rollwiderstand**

| Feld | Standard | Bedeutung |
|---|---|---|
| `crr` | 0.006 | 0.004 Rennrad Asphalt · 0.006 Gravel · 0.012 MTB Schotter |

## `windCoefficientScale` — warum es das gibt

Die FTMS-Spezifikation nennt für den Wind Resistance Coefficient nur die Einheit
kg/m, **nicht die Formel**. Wir rechnen nach Lehrbuch:

```
Cw = 0.5 · rho · CdA        bei CdA 0.32  ->  Cw = 0.196 kg/m  ->  Byte 0x14
```

Manche Trainer-Firmware interpretiert denselben Wert aber als `rho · CdA`, also
doppelt so groß. Das Referenzprojekt sendete `0x33` = 0.51 kg/m — das entspräche
CdA 0.83 m², also einem aufrecht stehenden Menschen im Gegenwind.

Falls sich der Luftwiderstand am echten Gerät zu schwach anfühlt: `windCoefficientScale`
hochdrehen (2.0 verdoppelt). Zu stark: runter. Das ist reine Empirie — es gibt
keinen Weg, die Konvention der Firmware auszulesen.

## Steigungs-Kalibrierung

**Das Problem:** Die Magnetbremse hat einen mechanischen Anschlag nach unten. Bei
0 % Steigung steht sie schon fast dort — deshalb fühlen sich −2 %, −5 % und −10 %
identisch an.

**Die Lösung:** Die Steigung aus dem Spiel wird auf einen Bereich abgebildet, der
komplett über diesem Anschlag liegt. Drei Stützpunkte, dazwischen linear:

| Feld | Standard | wann |
|---|---|---|
| `simGradeAtDownhill` | 0 % | Spiel auf maximalem Gefälle |
| `simGradeAtFlat` | 1.5 % | Spiel in der Ebene ← **der Grundwiderstand** |
| `simGradeAtUphill` | 8 % | Spiel auf maximaler Steigung |
| `maxGradePercent` | 10 % | Steigungsbereich, den das Spiel liefert |

Was das bei 79 kg konkret bedeutet:

```
Ebene bei 25 km/h        Luft 66 W + Rollen 32 W + Steigung 81 W  =  178 W
maximal bergab, 25 km/h  Luft 66 W + Rollen 32 W + Steigung  0 W  =   98 W
maximal bergauf, 15 km/h Luft 14 W + Rollen 19 W + Steigung 258 W =  292 W
```

Die Ebene kostet also rund 180 W — ein angenehmes Grundtempo. Bergab werden
80 W weniger verlangt, das ist deutlich spürbar. Bergauf wird es hart, ohne dass
die Bremse in die Sättigung läuft.

`simGradeAtUphill` steht bewusst auf 8 statt 10: Reserve nach oben, und ihr baut
sowieso keine extremen Anstiege.

## Kalibrieren am Gerät

Zwei Kontextmenü-Einträge am `BleTrainer` (drei Punkte ⋮ am Komponenten-Header):

**`Physik: Leistungstabelle ins Log`** — rechnet ohne Trainer aus, was die
aktuellen Einstellungen bei 10 bis 40 km/h kosten würden, aufgeschlüsselt nach
Luft, Rollen und Steigung. Zeigt auch, welche Bytes tatsächlich gesendet werden.

**`Kalibrierung: Stufen durchfahren`** — fährt fünf Steigungsstufen à 12 s durch
und protokolliert gemessene gegen erwartete Leistung. **Dabei gleichmäßig
weitertreten**, sonst sind die Werte wertlos.

```
[Kalibrierung] Spiel  -10.0 %  ->  gesendet   0.0 %  |  24.8 km/h  |  gemessen   97 W  |  erwartet   98 W
[Kalibrierung] Spiel   -5.0 %  ->  gesendet   0.8 %  |  24.9 km/h  |  gemessen  138 W  |  erwartet  140 W
...
```

**So liest du das Ergebnis:**

- Fällt die gemessene Leistung nach unten hin **nicht weiter**, steht die Bremse
  am Anschlag → `simGradeAtFlat` erhöhen.
- Ist die Ebene zu anstrengend → `simGradeAtFlat` senken.
- Weicht gemessen stark von erwartet ab → `windCoefficientScale` justieren.

Werte, die du im Play-Modus änderst, werden beim Stoppen zurückgesetzt. Guten
Wert merken und danach nochmal setzen.

## Werte im Code verwenden

`BleTrainer` stellt alles als Property bereit, live:

```csharp
var t = FindObjectOfType<BleTrainer>();

t.GameGradePercent    // Steigung aus dem Spiel, in %
t.SimGradePercent     // was nach der Kalibrierung wirklich gesendet wird
t.TotalMassKg         // Fahrer + Rad
t.WindCoefficient     // Cw in kg/m, wie gesendet

t.SpeedMs             // aktuelle Geschwindigkeit in m/s
t.AeroForceN          // Luftwiderstand JETZT, in Newton (quadratisch mit v)
t.AeroPowerW          // Leistungsanteil Luft
t.RollingPowerW       // Leistungsanteil Rollen
t.ClimbPowerW         // Leistungsanteil Steigung, negativ bergab
t.ExpectedPowerW      // Summe = was der Fahrer treten müsste

t.Latest.PowerW       // was der Trainer tatsächlich misst
t.Latest.SpeedKmh
t.Latest.CadenceRpm
```

`RidePhysics` geht auch ohne Trainer, rein statisch:

```csharp
float v = RidePhysics.KmhToMs(32f);
float luft = RidePhysics.AeroPowerW(0.32f, RidePhysics.RhoSeaLevel, v);
float alles = RidePhysics.TotalPowerW(v, gradePercent: 4f, totalMassKg: 79f,
                                      crr: 0.006f, cdA: 0.32f,
                                      airDensity: RidePhysics.RhoSeaLevel);
```

---

# Die Landschaft anbinden

Zwei Komponenten schliessen den Kreis zwischen Spielwelt und Bremse. Beide
funktionieren mit **jeder** Art von Landschaft — Unity Terrain, Mesh, Spline —
weil sie nur die tatsächliche Bewegung auswerten.

```
   treten  →  Trainer misst Tempo  →  RiderMover fährt vorwärts
                                            ↓
                                      Landschaft wird steiler
                                            ↓
                                      RouteGrade misst die Steigung
                                            ↓
                                      BleTrainer stellt die Bremse
```

## `RiderMover` — fährt nach gemessenem Tempo

Auf das Fahrer-Objekt (Kapsel, Kamera, Modell — egal):

| Feld | Bedeutung |
|---|---|
| `trainerSource` | GameObject mit `BleTrainer` oder `MockTrainer` |
| `speedMultiplier` | 1 = 1 km/h am Trainer sind 1 km/h im Spiel |
| `testSpeedKmh` | Ohne Trainer: damit fahren, um die Landschaft zu testen |
| `turnSpeedDegPerSec` | Lenken mit A/D bzw. Pfeiltasten |
| `stickToGround` | Raycast nach unten hält den Fahrer auf dem Boden |
| `groundMask` | In welchen Layern euer Boden liegt |

Liefert `SpeedKmh`, `DistanceMeters` und `ElevationGainMeters` — letzteres
praktisch für eine Anzeige „so viele Höhenmeter geschafft".

## `RouteGrade` — misst die Steigung

Auf dasselbe oder ein eigenes Logik-Objekt:

| Feld | Bedeutung |
|---|---|
| `rider` | Das Objekt, das sich bewegt |
| `trainerSource` | GameObject mit `BleTrainer` oder `MockTrainer` |
| `sampleDistanceMeters` | Über welche Strecke gemittelt wird (4 m) |
| `smoothingSeconds` | Zusätzliche Glättung gegen Rucken (0.6 s) |
| `applyToTrainer` | Aus, wenn ihr die Steigung erst nur anzeigen wollt |

Die Rechnung ist simpel:

```
Steigung % = Höhendifferenz / horizontale Strecke × 100
```

Gemessen wird erst, wenn `sampleDistanceMeters` zusammengekommen sind — sonst
teilt man durch fast null und bekommt Unsinn. Die Glättung tastet sich dann
schrittweise an den Rohwert heran, statt zu springen.

`SetGrade()` darf beliebig oft aufgerufen werden, der `BleTrainer` drosselt
intern selbst auf 1 Hz.

## Aufbau in der Szene

```
Fahrer                    ← RiderMover, RouteGrade
   └─ Main Camera
FTMS_UI                   ← FTMS_UI (erzeugt BleTrainer beim Connect)
Terrain / Landschaft
```

`rider` und `trainerSource` beider Komponenten auf die passenden Objekte ziehen.

## Ohne Trainer testen

1. Am `FTMS_UI` das Häkchen `Use Mock Trainer` setzen
2. Am `RiderMover` `testSpeedKmh` auf z.B. 25 stellen
3. Play, `Connect` klicken, mit A/D lenken

Dann fährt der Fahrer durch die Landschaft und `RouteGrade.GradePercent` zeigt
im Inspector live die Steigung — ganz ohne Hardware.

## Für die Landschaft

- Steigungen bis etwa **8 %** bauen. Darüber macht der Trainer keinen
  Unterschied mehr, weil `simGradeAtUphill` gedeckelt ist.
- **Gefälle lohnt bis etwa −10 %.** Steiler bringt nichts, die Bremse ist offen.
- Für Hügel in einer Landschaft reichen **3–5 %** völlig — das entspricht schon
  einer ordentlichen Steigung.
- Bei Terrain: keine harten Kanten. `sampleDistanceMeters` glättet zwar, aber
  eine Stufe erzeugt trotzdem einen Ausschlag.

---

# Dateien

| Datei | Zweck |
|---|---|
| `Assets/Scripts/RidePhysics.cs` | Formeln für Luft-, Roll- und Steigungswiderstand, FTMS-Kodierung |
| `Assets/Scripts/ITrainer.cs` | Interface + `TrainerData`-Struct |
| `Assets/Scripts/BleTrainer.cs` | Echte Implementierung: Scan, Connect, FTMS-Protokoll, Kalibrierung |
| `Assets/Scripts/MockTrainer.cs` | Fake-Trainer zum Entwickeln ohne Hardware |
| `Assets/Scripts/RiderMover.cs` | Bewegt den Fahrer mit dem gemessenen Tempo durch die Welt |
| `Assets/Scripts/RouteGrade.cs` | Misst die Steigung der Landschaft, schickt sie an den Trainer |
| `Assets/Scripts/TrainerController.cs` | Bindeglied zur Test-UI, `AverageGrade()` |
| `Assets/Lernen/` | Lernmaterial: Physik, C#-Notizen, `PhysikDemo.cs` |
| `Assets/BLE_FTMS_IndoorBike/BleApi.cs` | P/Invoke-Deklarationen zur nativen DLL |
| `Assets/BLE_FTMS_IndoorBike/BleWinrtDll.dll` | **gepatcht**, siehe `BLE_FIX.md` |
| `Assets/BLE_FTMS_IndoorBike/FTMS_UI.cs` | Test-Oberfläche, in `test.unity` verdrahtet |
| `tools/patch_dll.ps1` | Erzeugt die gepatchte DLL reproduzierbar aus dem Original |
| `tools/BleWinrtDll.dll.original` | Unveränderte DLL als Referenz |
| `tools/tuo_test.py` | BLE-Diagnose ohne Unity (`pip install bleak`) |
| `BLE_FIX.md` | Warum die DLL gepatcht werden musste |

# Stolperfallen

- **Die DLL im Repo ist gepatcht.** Ohne den Patch verbindet sich alles normal,
  aber der Widerstand reagiert nicht. Details in `BLE_FIX.md`.
- **Nach jedem DLL-Tausch Unity komplett beenden.** Native DLLs werden beim
  ersten Play geladen und erst beim Schließen des Editors freigegeben.
- **Maximal 1 Write pro Sekunde** auf den Control Point, sonst bricht die
  BLE-Verbindung ab. `SendLoop()` drosselt darauf, der Slider darf beliebig
  schnell feuern.
- **Wenn das Verbinden hakt:** Bluetooth in Windows aus/ein, 10 s warten.
- **Nur eine BLE-Verbindung gleichzeitig.** myETraining auf dem Handy schließen.
- **Der Tuo simuliert maximal 10 % Steigung**, Leistungsgenauigkeit ±5 %.

# Protokoll-Notizen (BLE FTMS)

- Service `0x1826`, Control Point `0x2AD9` (Write + Indicate), Indoor Bike Data
  `0x2AD2` (Notify)
- Pflichtreihenfolge: `0x00` Request Control → `0x07` Start → dann Steuerbefehle.
  Sonst antwortet der Trainer mit Result Code `0x05` (Control Not Permitted).
- Opcode `0x11` SetIndoorBikeSimulationParameters, 7 Bytes, little-endian:
  `0x11` | Wind sint16 (0.001 m/s) | Steigung sint16 (0.01 %) | Crr uint8 (0.0001) |
  Cw uint8 (0.01 kg/m)
- Antwort ist immer `0x80`, angefragter Opcode, Result Code. `0x01` = Success.
- In `0x2AD2` ist **Bit 0 der Flags invertiert** (0 = Momentangeschwindigkeit
  vorhanden). Total Distance ist `uint24`, also 3 Bytes.

Basis war `github.com/frakw/BLE_FTMS_IndoorBike` (DLL: Fork von
`github.com/adabru/BleWinrtDll`), ursprünglich für einen Xpedo Apx Pro.
