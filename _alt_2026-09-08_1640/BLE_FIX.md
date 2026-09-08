# Warum der Widerstand nicht reagiert hat

Kurzfassung für alle, die später in den Code schauen — und fürs Projekt-Journal.

## Symptom

Verbindung zum Elite Tuo stand, Notifications auf `0x2AD2` lieferten live Speed,
Cadence und Power. Im Log erschien bei jedem Slider-Zug brav `[BLE] TX 11 00 00 …`.
Am Trainer passierte nichts. Vom Control Point `0x2AD9` kam nie eine Indication
zurück, und `GetError()` der DLL meldete keinen Fehler.

## Ursache

`BleWinrtDll.dll` (Fork von `github.com/adabru/BleWinrtDll`) schreibt in
`SendDataAsync` fest einkompiliert:

```cpp
auto status = co_await characteristic.WriteValueAsync(
    buffer, GattWriteOption::WriteWithoutResponse);
```

Der FTMS Control Point `0x2AD9` hat aber nur die Properties **Write** und
**Indicate** — kein Write Without Response.

Entscheidend ist, was daraus auf BLE-Ebene wird: `WriteWithoutResponse` geht als
**ATT Write Command** raus. Ein Write Command wird per Definition **nicht
bestätigt**, und Fehler werden nicht zurückgemeldet. Windows meldet deshalb
„Success", sobald das Paket rausgeschoben ist. Der Tuo empfängt es auf einem
Handle, das nur *Write Request* erlaubt, und verwirft es stillschweigend.

Das erklärt alle drei Beobachtungen gleichzeitig: kein Fehler in Windows, keine
Antwort vom Trainer, keine Widerstandsänderung.

Die README des DLL-Projekts nennt genau das als Stolperstein:
*„If sending data to the device does not work, try replacing WriteWithoutResponse
with WriteWithResponse."*

## Fix

Ein Byte im kompilierten Binary:

```
Datei-Offset       0x127B2
Virtuelle Adresse  0x1800133B2
Vorher   41 B8 01 00 00 00   =  mov r8d, 1   (WriteWithoutResponse)
Nachher  41 B8 00 00 00 00   =  mov r8d, 0   (WriteWithResponse)
```

`r8d` ist das dritte Argument von `WriteValueAsync` (x64-Aufrufkonvention:
`rcx` = Characteristic, `rdx` = Buffer, `r8` = GattWriteOption, `r9` = out).

Reproduzierbar mit `patch_dll.ps1`. Das Skript prüft vorher, ob an der Stelle
wirklich `mov r8d, imm32` steht, legt ein Backup an und ist idempotent.

### Warum das keine Nebenwirkungen hat

Über die `.pdata`-Exception-Tabelle lässt sich der Aufrufbaum rekonstruieren:

```
0x1800133B2  liegt in Funktion 0x13030–0x1364C   (Coroutine-Body SendDataAsync)
  ← aufgerufen nur von 0x1300C
     ← in Funktion 0x12F90–0x13025              (Coroutine-Rampe)
        ← aufgerufen nur von 0x48DE
           ← in Funktion 0x4880–0x490E          (Worker hinter dem Export SendData)
```

Die Stelle ist ausschliesslich über `SendData` erreichbar. Scan, Connect und die
Subscriptions laufen über eigene Funktionen (`StartDeviceScan` 0x2640,
`ScanServices` 0x32A0, `SubscribeCharacteristic_*` 0x4500 / 0x4620) und sind nicht
betroffen. Die PE-Checksumme ist in der Original-DLL bereits `0`, wird von Windows
für normale DLLs also nicht geprüft; eine Signatur gibt es nicht.

Sauberere Alternative: DLL aus dem Quelltext neu bauen (Visual Studio, Workload
„Desktopentwicklung mit C++", C++/WinRT) mit `WriteWithResponse` statt des Patches.
Funktional identisch, aber ~2 Stunden Setup.

## Falsche Fährten unterwegs

- **`[BLE] Ok` als roter Fehler.** Die DLL initialisiert ihren Fehlerpuffer mit dem
  String `"Ok"`. Unser Code loggte alles Nicht-Leere als `LogError`. Ist jetzt in
  `BleTrainer.IstFehler()` rausgefiltert.
- **Verdacht, der Patch zerstöre die Verbindung.** Der erste Testlauf schlug fehl,
  war aber ein hängengebliebener Windows-Bluetooth-Zustand. Nach Bluetooth aus/ein
  und Unity-Neustart funktionierte derselbe Patch.
- **Verdacht auf falsche Subscription.** Ausgeschlossen: die beiden Coroutinen
  unterscheiden sich nur in einem Wert — `SubscribeCharacteristic_Read` schreibt
  `1` (Notify), `SubscribeCharacteristic_Write` schreibt `2` (Indicate) ins CCCD.
  Für `0x2AD9` ist Indicate korrekt.

## Was man wissen muss, wenn man am Code weiterarbeitet

- **Nach jedem DLL-Tausch Unity komplett beenden.** Native DLLs werden beim ersten
  Play geladen und erst beim Schliessen des Editors freigegeben.
- **Maximal 1 Write pro Sekunde** auf den Control Point, sonst bricht die
  BLE-Verbindung ab. `BleTrainer.SendLoop()` drosselt darauf.
- **`SendData(data, block: false)` gibt immer `false` zurück.** Der Rückgabewert
  wird nur im blockierenden Modus gesetzt und taugt nicht als Fehlerindikator.
- **Pflichtreihenfolge:** `0x00` Request Control → `0x07` Start → erst dann
  Steuerbefehle. Ohne das antwortet der Trainer mit Result Code `0x05`
  (Control Not Permitted).
- **Flags-Bitmaske in `0x2AD2`: Bit 0 ist invertiert** (0 = Momentangeschwindigkeit
  vorhanden). Total Distance ist `uint24`, also 3 Bytes.
- **Der Tuo kann maximal 10 % Steigung simulieren**, Leistungsgenauigkeit ±5 %.

## Diagnose ohne Unity

`tuo_test.py` (braucht `pip install bleak`) verbindet sich direkt mit dem Tuo,
liest die Fitness Machine Features aus `0x2ACC`, fährt die Startsequenz und
zeigt die Control-Point-Antworten im Klartext. Nützlich, wenn unklar ist, ob ein
Problem am Unity-Code oder am Trainer liegt — BLE in einer Konsole zu debuggen ist
deutlich schneller als im Editor.
