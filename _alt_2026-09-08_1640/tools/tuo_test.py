"""
Tuo-Diagnose ausserhalb von Unity.

Zweck: in 2 Minuten klaeren, ob der Trainer FTMS-Steuerbefehle ueberhaupt
annimmt - unabhaengig von Unity und von BleWinrtDll.

Installation (einmalig, PowerShell):
    pip install bleak

Start:
    python tuo_test.py

Wichtig: myETraining / Zwift schliessen, sonst ist der Tuo belegt.
"""

import asyncio
import struct
from bleak import BleakScanner, BleakClient

FTMS      = "00001826-0000-1000-8000-00805f9b34fb"
FEATURE   = "00002acc-0000-1000-8000-00805f9b34fb"  # Fitness Machine Feature (read)
BIKE_DATA = "00002ad2-0000-1000-8000-00805f9b34fb"  # Indoor Bike Data (notify)
RES_RANGE = "00002ad6-0000-1000-8000-00805f9b34fb"  # Supported Resistance Level Range (read)
POW_RANGE = "00002ad8-0000-1000-8000-00805f9b34fb"  # Supported Power Range (read)
CTRL_PT   = "00002ad9-0000-1000-8000-00805f9b34fb"  # Control Point (write + indicate)
STATUS    = "00002ada-0000-1000-8000-00805f9b34fb"  # Fitness Machine Status (notify)

RESULT = {
    0x01: "Success",
    0x02: "Op Code not supported",
    0x03: "Invalid Parameter",
    0x04: "Operation Failed",
    0x05: "Control Not Permitted (Request Control fehlt)",
}

# Bit -> Name, zweite 4 Bytes von 0x2ACC (Target Setting Features)
TARGET_BITS = {
    0: "Speed Target",
    1: "Inclination Target",
    2: "Resistance Target        <- fuer Opcode 0x04",
    3: "Power Target",
    4: "Heart Rate Target",
    13: "Indoor Bike Simulation  <- fuer Opcode 0x11",
    15: "Spin Down Control",
}

antwort = asyncio.Queue()


def on_control_point(_sender, data: bytearray):
    """Antwort ist immer: 0x80, angefragter Opcode, Result Code."""
    if len(data) >= 3 and data[0] == 0x80:
        txt = f"0x{data[1]:02X} -> {RESULT.get(data[2], hex(data[2]))}"
    else:
        txt = data.hex(" ")
    antwort.put_nowait(txt)


letzte_daten = {}


def on_bike_data(_sender, data: bytearray):
    """Nur grob geparst - reicht, um zu sehen, ob sich Leistung aendert."""
    if len(data) < 2:
        return
    flags = struct.unpack_from("<H", data, 0)[0]
    i = 2
    d = {}
    if (flags & 0x0001) == 0:                       # Bit 0 ist INVERTIERT
        d["speed"] = struct.unpack_from("<H", data, i)[0] / 100.0; i += 2
    if flags & 0x0002: i += 2
    if flags & 0x0004:
        d["cadence"] = struct.unpack_from("<H", data, i)[0] / 2.0; i += 2
    if flags & 0x0008: i += 2
    if flags & 0x0010: i += 3                       # Total Distance = uint24
    if flags & 0x0020:
        d["resistance"] = struct.unpack_from("<h", data, i)[0]; i += 2
    if flags & 0x0040:
        d["power"] = struct.unpack_from("<h", data, i)[0]; i += 2
    letzte_daten.update(d)


async def sende(client, payload: bytes, was: str):
    """Schreibt MIT Response auf den Control Point und wartet auf die Indication."""
    while not antwort.empty():
        antwort.get_nowait()
    print(f"\n--> {was}:  {payload.hex(' ')}")
    try:
        await client.write_gatt_char(CTRL_PT, payload, response=True)
    except Exception as e:
        print(f"    SCHREIBFEHLER: {e}")
        return None
    try:
        r = await asyncio.wait_for(antwort.get(), timeout=3.0)
        print(f"<-- {r}")
        return r
    except asyncio.TimeoutError:
        print("<-- KEINE ANTWORT (Timeout 3s)")
        return None


def zeige_features(raw: bytes):
    if len(raw) < 8:
        print("  unerwartete Laenge:", raw.hex(" "))
        return
    machine, target = struct.unpack("<II", raw[:8])
    print(f"  roh: {raw.hex(' ')}")
    print(f"  Machine Features = 0x{machine:08X}")
    print(f"  Target  Features = 0x{target:08X}")
    for bit, name in TARGET_BITS.items():
        ja = "JA " if target & (1 << bit) else "NEIN"
        print(f"    Bit {bit:2d}  {ja}  {name}")


async def main():
    print("Suche 'TUO' ...")
    dev = await BleakScanner.find_device_by_name("TUO", timeout=20.0)
    if dev is None:
        print("Nicht gefunden. Trainer eingesteckt? Andere App verbunden?")
        return
    print(f"Gefunden: {dev.name}  {dev.address}")

    async with BleakClient(dev) as client:
        print("Verbunden.\n")

        # ---- 1. Was kann der Trainer laut eigener Auskunft? -------------
        print("=== Fitness Machine Feature (0x2ACC) ===")
        try:
            zeige_features(await client.read_gatt_char(FEATURE))
        except Exception as e:
            print("  nicht lesbar:", e)

        for uuid, name, fmt in [
            (RES_RANGE, "Supported Resistance Level Range (0x2AD6)", "<hhH"),
            (POW_RANGE, "Supported Power Range (0x2AD8)", "<hhH"),
        ]:
            print(f"\n=== {name} ===")
            try:
                raw = await client.read_gatt_char(uuid)
                print("  roh:", raw.hex(" "))
                if len(raw) >= 6:
                    mn, mx, inc = struct.unpack(fmt, raw[:6])
                    print(f"  min={mn}  max={mx}  increment={inc}")
            except Exception as e:
                print("  nicht lesbar:", e)

        # ---- 2. Notifications an ----------------------------------------
        await client.start_notify(CTRL_PT, on_control_point)
        await client.start_notify(BIKE_DATA, on_bike_data)
        try:
            await client.start_notify(STATUS, lambda _s, d: print(f"[Status] {d.hex(' ')}"))
        except Exception:
            pass
        await asyncio.sleep(0.5)

        # ---- 3. Pflichtsequenz ------------------------------------------
        print("\n=== Steuerung uebernehmen ===")
        await sende(client, bytes([0x00]), "0x00 Request Control")
        await asyncio.sleep(1.0)
        await sende(client, bytes([0x07]), "0x07 Start/Resume")
        await asyncio.sleep(1.0)

        # ---- 4. Der eigentliche Test ------------------------------------
        print("\n=== JETZT TRETEN und auf Widerstand achten ===")
        for grade in (0.0, 5.0, 10.0, 0.0):
            g = int(round(grade * 100))                    # Einheit 0.01 %
            pkt = struct.pack("<BhhBB", 0x11, 0, g, 0x32, 0x33)
            await sende(client, pkt, f"0x11 Simulation, Steigung {grade:+.1f}%")
            for _ in range(6):
                await asyncio.sleep(1.0)
                print(f"    {letzte_daten}")

        # ---- 5. Gegentest: direkter Widerstand ---------------------------
        print("\n=== Gegentest 0x04 SetTargetResistanceLevel ===")
        print("Die FTMS-Spec ist hier uneindeutig (uint8 mit 0.1er-Aufloesung")
        print("vs. glatte 0-100). Deshalb beide Varianten probieren.")
        for payload, beschr in [
            (bytes([0x04, 50]),                "uint8 = 50"),
            (bytes([0x04, 100]),               "uint8 = 100"),
            (struct.pack("<Bh", 0x04, 500),    "sint16 = 500"),
        ]:
            await sende(client, payload, f"0x04 {beschr}")
            for _ in range(4):
                await asyncio.sleep(1.0)
                print(f"    {letzte_daten}")

        await sende(client, bytes([0x08]), "0x08 Stop")
        print("\nFertig.")


if __name__ == "__main__":
    asyncio.run(main())
