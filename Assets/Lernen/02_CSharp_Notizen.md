# C#-Notizen für Python-Umsteiger

Nur die Konstrukte, die in diesem Projekt wirklich vorkommen — mit dem
Python-Gegenstück daneben. Kein vollständiger Kurs.

---

## Typen stehen vorne

```csharp
float tempo = 25.4f;      // Kommazahl, das f muss dran
int runden = 3;
byte b = 0x11;            // 0 bis 255, ein Byte
bool fertig = false;
string name = "TUO";
```

```python
tempo = 25.4
runden = 3
```

C# prüft die Typen beim Kompilieren. Das nervt am Anfang, fängt aber genau die
Fehler ab, die in Python erst zur Laufzeit auffallen.

Wenn der Typ offensichtlich ist, geht auch `var`:

```csharp
var paket = new byte[] { 0x11, 0x00 };   // Compiler weiss: byte[]
```

**Falle:** `5 / 2` ergibt in C# `2`, nicht `2.5` — zwei `int` ergeben ein `int`.
Man braucht `5f / 2f` oder `5.0 / 2.0`. In Python 3 macht `/` immer Kommazahlen.

---

## Klassen und Methoden

```csharp
public class BleTrainer : MonoBehaviour
{
    public float maxGradePercent = 10f;      // Feld

    public void SetGrade(float grade)        // Methode
    {
        _wantedGrade = grade;
    }
}
```

```python
class BleTrainer:
    def __init__(self):
        self.max_grade_percent = 10.0

    def set_grade(self, grade):
        self._wanted_grade = grade
```

Unterschiede:

- **Kein `self`.** Innerhalb der Klasse schreibt man einfach den Feldnamen.
- **`public` / `private`** steuert, wer drankommt. Ohne Angabe ist es `private`.
- **Geschweifte Klammern** statt Einrückung. Die Einrückung ist trotzdem
  Pflicht — für Menschen.
- **Semikolon** am Ende jeder Anweisung.

Der Doppelpunkt bei `: MonoBehaviour` heißt „erbt von". In Python wären das die
Klammern: `class BleTrainer(MonoBehaviour)`.

---

## Properties — Felder mit Rechnung dahinter

```csharp
public float TotalMassKg => riderMassKg + bikeMassKg;
```

Von außen sieht das aus wie ein Feld:

```csharp
float m = trainer.TotalMassKg;    // keine Klammern!
```

Wird aber bei jedem Zugriff neu berechnet. In Python:

```python
@property
def total_mass_kg(self):
    return self.rider_mass_kg + self.bike_mass_kg
```

Das `=>` heißt „ist definiert als". Bei einzeiligen Methoden geht es auch:

```csharp
public void Connect() => StartCoroutine(ConnectRoutine());
```

Es gibt auch die Variante mit privatem Setter:

```csharp
public bool IsReady { get; private set; }
```

Von außen lesbar, aber nur die Klasse selbst darf schreiben.

---

## `static` — gehört zur Klasse, nicht zum Objekt

```csharp
public static class RidePhysics
{
    public const float G = 9.80665f;

    public static float AeroPowerW(float cdA, float rho, float v)
        => 0.5f * rho * cdA * v * v * v;
}
```

Aufruf ohne Objekt:

```csharp
float p = RidePhysics.AeroPowerW(0.32f, 1.225f, 6.94f);
```

`static class` heißt: davon kann man gar kein Objekt erzeugen. Das entspricht in
Python einem Modul mit lauter Funktionen — `import ridephysics` und dann
`ridephysics.aero_power_w(...)`.

`const` ist eine Konstante, die sich nie ändert.

---

## `struct` statt `class`

```csharp
public struct TrainerData
{
    public float SpeedKmh;
    public float CadenceRpm;
    public float PowerW;
}
```

Ein `struct` wird **kopiert**, wenn man es weitergibt. Eine `class` wird als
**Referenz** weitergegeben — wie alles in Python.

```csharp
var a = new TrainerData { SpeedKmh = 20f };
var b = a;              // Kopie!
b.SpeedKmh = 30f;
// a.SpeedKmh ist immer noch 20
```

Für kleine Wertepakete nimmt man `struct`, weil dabei kein Müll für den Garbage
Collector entsteht — in einem Spiel, das 60-mal pro Sekunde läuft, macht das
einen Unterschied.

---

## Interfaces — ein Vertrag

```csharp
public interface ITrainer
{
    event Action<TrainerData> OnData;
    bool IsReady { get; }
    void SetGrade(float gradePercent);
}
```

Ein Interface sagt nur, *was* es geben muss, nicht *wie*. `BleTrainer` und
`MockTrainer` implementieren beide `ITrainer`. Der Rest des Spiels kennt nur das
Interface und merkt nicht, ob echte Hardware dranhängt.

```csharp
ITrainer trainer = useMock
    ? gameObject.AddComponent<MockTrainer>()
    : gameObject.AddComponent<BleTrainer>();

trainer.SetGrade(5f);      // funktioniert bei beiden
```

In Python entspricht das einer abstrakten Basisklasse mit `@abstractmethod` —
nur dass C# beim Kompilieren erzwingt, dass wirklich alles implementiert ist.

Das `?:` oben ist eine Kurzform von if/else, in Python:
`x = a if bedingung else b`.

---

## Events — jemanden benachrichtigen

```csharp
public event Action<TrainerData> OnData;      // deklarieren

OnData?.Invoke(neueDaten);                    // ausloesen
```

Anmelden von außen:

```csharp
trainer.OnData += HandleData;      // anmelden
trainer.OnData -= HandleData;      // abmelden
```

`Action<TrainerData>` heißt: eine Funktion, die ein `TrainerData` bekommt und
nichts zurückgibt. In Python wäre das eine Liste von Callbacks, die man
durchgeht.

Das `?.` ist wichtig: **falls niemand angemeldet ist**, ist `OnData` gleich
`null` und `Invoke` würde krachen. `?.` überspringt den Aufruf dann einfach. Das
gibt es in Python nicht.

**Immer abmelden**, wenn das Objekt verschwindet — sonst hält das Event eine
Referenz und das Objekt wird nie aufgeräumt. Deshalb steht in
`TrainerController.OnDestroy()` das `-=`.

---

## Coroutinen — warten, ohne alles zu blockieren

Das größte Unity-spezifische Konzept.

```csharp
private IEnumerator ConnectRoutine()
{
    Debug.Log("Request Control");
    WriteControlPoint(new byte[] { 0x00 });

    yield return new WaitForSeconds(1.0f);    // <- hier pausiert es

    Debug.Log("Start");
    WriteControlPoint(new byte[] { 0x07 });
}

// Starten:
StartCoroutine(ConnectRoutine());
```

Bei `yield return` gibt die Methode die Kontrolle zurück ans Spiel. Das Bild
läuft weiter, Eingaben funktionieren. Eine Sekunde später macht sie genau dort
weiter.

Ein `Thread.Sleep(1000)` würde dagegen das **ganze Spiel** einfrieren.

Am nächsten kommt in Python `async def` mit `await asyncio.sleep(1)`. Die
Rückgabe `IEnumerator` sieht seltsam aus — Unity nutzt dafür Generatoren, und
`yield return` ist technisch dasselbe wie Pythons `yield`.

Weitere Varianten:

```csharp
yield return null;                          // ein Bild warten
yield return new WaitForSeconds(0.5f);      // Zeit warten
yield return AndereRoutine();               // auf andere Coroutine warten
```

---

## `[Attribute]` in eckigen Klammern

```csharp
[Header("Luftwiderstand")]
[Tooltip("CdA in m². 0.32 = Rennrad Oberlenker.")]
[Range(0.15f, 0.70f)]
public float cdA = 0.32f;
```

Attribute sind Zusatzinfos für andere Werkzeuge. Diese drei sagen dem Unity-
Inspector: mach eine Überschrift, zeig einen Tooltip, und mach einen Schieber
statt eines Eingabefelds.

```csharp
[ContextMenu("Physik: Leistungstabelle ins Log")]
public void LogPowerTable() { ... }
```

Das erzeugt einen Eintrag im Kontextmenü der Komponente (drei Punkte ⋮ im
Inspector). Sehr praktisch zum Testen — man braucht keinen Button in der UI.

In Python entsprechen Attribute am ehesten Dekoratoren.

---

## String-Interpolation

```csharp
Debug.Log($"Tempo {kmh:F1} km/h, Leistung {watt:F0} W");
```

Das `$` vorne erlaubt `{...}` mitten im Text — wie Pythons f-String. Nach dem
Doppelpunkt steht das Format:

| | |
|---|---|
| `F1` | eine Nachkommastelle |
| `F0` | keine, gerundet |
| `X2` | Hexadezimal, zwei Stellen |
| `,5:F0` | rechtsbündig auf 5 Zeichen |

```python
print(f"Tempo {kmh:.1f} km/h")
```

---

## Bits und Bytes

Kommt beim Bluetooth-Protokoll vor:

```csharp
byte low  = (byte)(wert & 0xFF);          // untere 8 Bit
byte high = (byte)((wert >> 8) & 0xFF);   // 8 Bit nach rechts, dann untere 8
```

| | |
|---|---|
| `&` | UND — Bits ausmaskieren |
| `\|` | ODER — Bits setzen |
| `>>` | nach rechts schieben |
| `<<` | nach links schieben |
| `0xFF` | hexadezimal 255, also 8 gesetzte Bits |

Funktioniert in Python genauso. Das `(byte)` davor ist ein **Cast**: „behandle
das jetzt als byte". C# verlangt das explizit, wenn dabei etwas verloren gehen
könnte.

Der Grund für die Zerlegung: eine Zahl über 255 passt nicht in ein Byte. Sie
wird auf mehrere verteilt, niederwertiges zuerst — das heißt **little-endian**.

---

## Was in diesem Projekt wo steht

| Datei | Konzepte zum Anschauen |
|---|---|
| `RidePhysics.cs` | `static class`, `const`, Ausdrucks-Methoden mit `=>` |
| `ITrainer.cs` | `interface`, `struct`, `event` |
| `MockTrainer.cs` | Interface implementieren, `Update()` |
| `TrainerController.cs` | Events an- und abmelden, `as`-Cast |
| `BleTrainer.cs` | Coroutinen, Attribute, Bit-Operationen, P/Invoke |
| `PhysikDemo.cs` | `StringBuilder`, `ContextMenu`, Schleifen |

Zum Reinlesen ist `RidePhysics.cs` der beste Startpunkt — reine Rechnerei, keine
Unity-Magie.
