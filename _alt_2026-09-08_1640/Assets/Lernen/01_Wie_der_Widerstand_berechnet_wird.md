# Wie der Widerstand berechnet wird

Das Nützliche zum Verstehen. Ohne Bluetooth-Details.

Zum Ausprobieren: `PhysikDemo.cs` auf ein leeres GameObject ziehen, Werte
einstellen, im Kontextmenü (drei Punkte ⋮) einen der vier Punkte wählen. Die
Rechnung erscheint Schritt für Schritt in der Konsole.

---

## Die Grundidee

Wer Fahrrad fährt, arbeitet gegen drei Dinge:

1. **Die Luft**, durch die man sich schiebt
2. **Den Reifen**, der sich beim Abrollen verformt
3. **Die Schwerkraft**, wenn es bergauf geht

Jedes davon ist eine **Kraft** in Newton. Um vorwärts zu kommen, muss man
dagegen drücken. Wie anstrengend das ist, hängt zusätzlich davon ab, wie
schnell man fährt — das ist die **Leistung** in Watt:

```
Leistung = Kraft × Geschwindigkeit
P [W]    = F [N]  × v [m/s]
```

Das ist der Schlüssel zu allem, was folgt.

---

## 1. Luftwiderstand

```
F = 0.5 · rho · CdA · v²
```

| | |
|---|---|
| `rho` | Luftdichte in kg/m³. Auf Meereshöhe bei 15 °C: 1.225 |
| `CdA` | Stirnfläche × Widerstandsbeiwert, in m² |
| `v` | Geschwindigkeit in m/s |

**Warum quadratisch?** Zwei Effekte multiplizieren sich. Fährst du doppelt so
schnell, triffst du in derselben Zeit auf doppelt so viel Luft — *und* du musst
jedes Luftteilchen doppelt so stark beschleunigen. 2 × 2 = 4.

Die Leistung ist dann `P = F · v`, also nochmal ein `v` mehr:

```
P = 0.5 · rho · CdA · v³
```

**Kubisch.** Doppeltes Tempo, achtfache Leistung. Das ist der wichtigste Satz
in diesem Dokument.

| Tempo | Leistung nur gegen Luft (CdA 0.32) |
|---|---|
| 10 km/h | 4 W |
| 20 km/h | 34 W |
| 30 km/h | 113 W |
| 40 km/h | 269 W |
| 50 km/h | 525 W |

Deshalb ist ein Weltklasse-Sprinter nicht doppelt so schnell wie ein
Hobbyfahrer, obwohl er die dreifache Leistung tritt.

### CdA

`CdA` steckt zwei Dinge in eine Zahl: wie groß du von vorne bist (`A`), und wie
windschlüpfrig deine Form ist (`Cd`). Man kann beide nicht getrennt messen, also
behandelt man sie als eine Größe.

| CdA | Position |
|---|---|
| 0.22 | Zeitfahrposition, Aerohelm |
| 0.28 | Rennrad, Unterlenker |
| **0.32** | Rennrad, Oberlenker ← unser Standard |
| 0.40 | Gravel, aufrecht |
| 0.55 | Citybike, ganz aufrecht |

Auffällig: Das Rad selbst spielt kaum eine Rolle. Fast alles ist der Körper. Sich
in den Unterlenker zu legen bringt mehr als jedes Aero-Laufrad.

---

## 2. Rollwiderstand

```
F = Crr · m · g
```

| | |
|---|---|
| `Crr` | Rollwiderstandsbeiwert, dimensionslos |
| `m` | Masse in kg (Fahrer + Rad) |
| `g` | 9.81 m/s² |

Der Reifen wird an der Aufstandsfläche platt gedrückt und federt danach wieder
auf. Dabei geht Energie als Wärme verloren. Wie viel, hängt davon ab, wie stark
er gedrückt wird — also am Gewicht.

**Die Kraft ist konstant.** Sie hängt nicht von der Geschwindigkeit ab. Die
Leistung schon, weil `P = F · v`:

```
P = Crr · m · g · v          linear mit v
```

| Crr | Situation |
|---|---|
| 0.004 | Rennrad, guter Reifen, Asphalt |
| **0.006** | Gravel auf Asphalt ← unser Standard |
| 0.012 | MTB auf Schotter |

Bei 79 kg und 25 km/h sind das rund 32 W.

---

## 3. Steigung

```
F = m · g · sin(alpha)
```

Bergauf hebst du dein Gewicht gegen die Schwerkraft. Nur der Teil der
Schwerkraft, der entlang der Straße zeigt, bremst dich — daher der Sinus.

Steigungen gibt man aber in Prozent an, nicht in Grad. 8 % heißt: auf 100 m
Strecke geht es 8 m hoch. Das ist der **Tangens**, nicht der Sinus. Bei kleinen
Winkeln sind beide fast gleich:

| Steigung | sin | tan | Fehler |
|---|---|---|---|
| 5 % | 0.0499 | 0.0500 | 0.1 % |
| 10 % | 0.0995 | 0.1000 | 0.5 % |
| 15 % | 0.1483 | 0.1500 | 1.1 % |

Bis 15 % liegt der Fehler unter 1 %. Deshalb rechnen wir einfach:

```
F = m · g · (Steigung% / 100)
P = m · g · (Steigung% / 100) · v
```

Bei negativer Steigung wird das negativ — die Schwerkraft hilft dann mit.

**Faustregel:** Bei 79 kg und 25 km/h kostet jedes Prozent Steigung rund 54 W.

---

## Alles zusammen

Bei 79 kg, CdA 0.32, Crr 0.006, 25 km/h:

| Steigung | Luft | Rollen | Steigung | gesamt |
|---|---|---|---|---|
| −4 % | 66 W | 32 W | −215 W | −117 W (rollt von allein) |
| 0 % | 66 W | 32 W | 0 W | 98 W |
| 3 % | 66 W | 32 W | 161 W | 259 W |
| 8 % | 66 W | 32 W | 430 W | 528 W |

Zwei Dinge fallen auf:

- **In der Ebene dominiert bei diesem Tempo die Luft** (66 von 98 W).
- **Am Berg dominiert die Steigung völlig.** Deshalb fährt man bergauf
  langsam — und weil man langsam fährt, spielt die Luft dort kaum eine Rolle.
  Das ist der Grund, warum Aerodynamik am Berg fast egal ist.

---

## Was davon macht der Trainer?

Wir schicken dem Tuo nur die **Koeffizienten**: Steigung, `Crr` und `Cw`. Er
misst selbst laufend die Geschwindigkeit und stellt die Bremse nach.

`Cw` ist der FTMS-Name für den konstanten Teil der Luftformel:

```
F = 0.5 · rho · CdA · v²  =  Cw · v²        mit  Cw = 0.5 · rho · CdA
```

Bei CdA 0.32 und rho 1.225 also `Cw = 0.196 kg/m`.

Der ganze quadratische Luftwiderstand entsteht damit **im Trainer**, aus einer
einzigen Zahl. Wir müssen nichts nachsenden, wenn du schneller wirst.

`RidePhysics.cs` rechnet dieselben Formeln in C# nach — nicht um den Trainer zu
ersetzen, sondern um vorherzusagen was etwas kosten wird, und um zu prüfen ob
der Trainer plausibel bremst.

---

## Warum die Steigung kalibriert werden muss

Der Tuo ist ein Wheel-on-Trainer mit Magnetbremse. Die hat einen mechanischen
Anschlag: unter einen gewissen Grundwiderstand kommt sie nicht.

Bei 0 % Steigung steht sie da schon fast. Sendet man −5 %, passiert nichts mehr
— sie kann nicht weiter aufmachen. Alles Negative fühlt sich gleich an.

**Lösung:** Wir bilden die Spielsteigung auf einen Bereich ab, der komplett über
dem Anschlag liegt.

```
Spiel  -10 %  →  gesendet  0 %      Bremse maximal offen
Spiel    0 %  →  gesendet  1.5 %    Grundwiderstand, ca. 80 W bei 25 km/h
Spiel  +10 %  →  gesendet  8 %
```

Der Preis: Die Ebene kostet etwas mehr als in Wirklichkeit. Der Gewinn: Gefälle
ist spürbar leichter. Für ein Spiel ist das der bessere Tausch — die Werte
stehen als `simGradeAtDownhill`, `simGradeAtFlat` und `simGradeAtUphill` im
Inspector.

---

## Was ihr für die Landschaft wissen solltet

- Baut Steigungen bis etwa **8 %**. Darüber macht der Trainer keinen
  Unterschied mehr, weil `simGradeAtUphill` gedeckelt ist.
- **Gefälle lohnt sich nur bis etwa −10 %.** Steiler bringt nichts, die Bremse
  ist schon offen.
- Ein Anstieg über **1200 m mit 84 Höhenmetern** ergibt 7 % — das ist schon
  ordentlich steil. Für Hügel in einer Landschaft reichen 3–5 % völlig.
- Rechnung dafür steht in `TrainerController.AverageGrade()`.
