# Elite Tuo - FTMS Minimalanbindung

## Einbinden
1. Projekt in Unity 6.3 LTS (6000.3.23f1) oeffnen.
2. Leeres GameObject in der Szene anlegen.
3. `Trainer` als Component draufziehen, `TrainerUI` dazu (Verbinden-Knopf + Steigungsfeld).
4. Play druecken, auf "Verbinden" klicken.

## Benutzen
```csharp
trainer.Connect();                  // sucht "TUO", verbindet, startet die Sendeschleife
trainer.Speed;                      // km/h
trainer.Cadence;                    // rpm
trainer.Power;                      // Watt
trainer.SetResistance(0.05f);       // Steigung, dimensionslos. 0.10 = Maximum des Tuo
```
`SetResistance` merkt den Wert nur - gesendet wird im 1-Hz-Takt der Sendeschleife.

## Die vier Fallen
1. **Max. 1 Write pro Sekunde** auf den Control Point `0x2AD9`. Schneller, und die BLE-Verbindung bricht ab.
2. **Bit 0 der Flags in `0x2AD2` ist invertiert**: `0` heisst "Momentangeschwindigkeit vorhanden".
3. **Total Distance ist uint24** (3 Bytes). Wer 2 oder 4 ueberspringt, liest ab da Muell.
4. **`SendData(data, block: false)` gibt immer `false` zurueck.** Kein Fehlerindikator - echte Fehler kommen ueber `GetError()`, und `"Ok"` bedeutet dort *kein* Fehler.

## Die DLL ist gepatcht
`BleWinrtDll.dll` schrieb fest einkompiliert mit `WriteWithoutResponse`. Das geht als ATT Write
Command raus, wird nie bestaetigt, und der Trainer verwirft es still - ohne jede Fehlermeldung.
Byte-Patch bei Datei-Offset `0x127B2`: `01` -> `00` (`mov r8d, 1` -> `mov r8d, 0`).

**Die DLL hier nicht durch eine frische aus dem Internet ersetzen.** Pruefen: Byte `0x127B2` muss `0x00` sein.
Nach einem DLL-Tausch muss Unity komplett neu starten - native DLLs werden erst beim Beenden des Editors freigegeben.
