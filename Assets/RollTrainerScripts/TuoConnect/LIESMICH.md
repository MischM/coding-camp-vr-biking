# TuoConnect

Alles, was zum Verbinden mit dem Elite Tuo noetig ist. Sonst nichts.

## Einbauen

Diesen ganzen Ordner ins Unity-Projekt nach `Assets\` ziehen.

**Vorher**: falls `BleApi.cs` oder `BleWinrtDll.dll` schon irgendwo im Projekt
liegen, die alten Kopien loeschen. Zwei Kopien derselben Klasse geben einen
Kompilierfehler, zwei Kopien der DLL geben Ladefehler.

Danach: **Play druecken.** Mehr nicht. Kein GameObject, kein Knopf, keine Szene.
`Trainer` legt sich beim Start selbst an, verbindet, und versucht es alle 5
Sekunden erneut, falls der Trainer noch schlaeft.

## Benutzen

```csharp
var t = FindFirstObjectByType<Trainer>();

t.Speed;                    // km/h
t.Cadence;                  // rpm
t.Power;                    // Watt
t.IsReady;                  // true, sobald die Verbindung steht
t.Status;                   // Klartext fuer eine Anzeige
t.SetResistance(0.05f);     // Steigung, dimensionslos. 0.10 = Maximum des Tuo
```

`SetResistance` merkt den Wert nur - gesendet wird im 1-Hz-Takt der Sendeschleife.

## Inhalt

| Datei | wofuer |
|---|---|
| `BleWinrtDll.dll` | native BLE-Anbindung an Windows. **GEPATCHT** - nicht ersetzen |
| `BleWinrtDll.dll.meta` | Plugin-Einstellungen. Ohne die laedt Unity die DLL nicht |
| `BleApi.cs` | P/Invoke-Deklarationen zur DLL |
| `Trainer.cs` | Verbindung, Sendeschleife, Auswertung der Messwerte |
| `TrainerUI.cs` | Debug-Anzeige (IMGUI). Kann raus, sobald du eigene UI hast |
| `Widerstand.cs` | Minimalbeispiel: Regler im Inspector -> `SetResistance()` |

## Widerstand ausprobieren

`Widerstand.cs` ist das einzige Skript hier, das ein GameObject braucht - der
Regler muss ja irgendwo im Inspector auftauchen:

1. Rechtsklick in der Hierarchy -> Create Empty
2. Add Component -> `Widerstand`
3. Play, dann am Regler `steigung` ziehen (-0.1 bis 0.1)

Sobald du deine eigene Logik hast (Strecke gibt die Steigung vor), kannst du das
Skript loeschen und `SetResistance()` selbst aufrufen.

## Nicht kaputtmachen

1. **Max. 1 Write pro Sekunde** auf den Control Point, sonst bricht BLE ab.
2. **Bit 0 der Flags in `0x2AD2` ist invertiert**: `0` heisst "Speed vorhanden".
3. **Total Distance ist uint24** (3 Bytes), nicht 2 oder 4.
4. **`SendData(..., block: false)` gibt immer `false` zurueck** - kein Fehlerindikator.
   Echte Fehler kommen ueber `GetError()`, wo `"Ok"` *kein* Fehler bedeutet.
5. **`ScanCharacteristics` vor dem Subscriben ist Pflicht** - erst dieser Scan fuellt
   in der DLL die Handles. Ohne ihn laufen die Subscriptions lautlos ins Leere.
6. **DLL nicht durch eine frische aus dem Internet ersetzen.** Die Originalversion
   schreibt mit `WriteWithoutResponse`, was der Tuo stillschweigend verwirft.
   Pruefen: Byte an Datei-Offset `0x127B2` muss `0x00` sein.
7. **Nach einem DLL-Tausch Unity komplett neu starten** - native DLLs werden erst
   beim Beenden des Editors freigegeben.

## Wenn nichts geht

- `DllNotFoundException` -> die DLL oder ihre `.meta` fehlt im Projekt.
- `Geraet nicht gefunden` -> Trainer schlaeft (Kurbel drehen), oder eine andere App
  haelt ihn (nur eine Verbindung gleichzeitig), oder Windows haengt: Unity beenden,
  Bluetooth aus/ein, neu starten.
- Gar keine `[BLE]`-Zeile in der Console -> Unity hat nicht kompiliert. Nach roten
  Fehlern schauen; bei einem Compile-Fehler laeuft die zuletzt gebaute Fassung weiter.
