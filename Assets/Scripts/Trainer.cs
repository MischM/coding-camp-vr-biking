using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Elite Tuo ueber BLE FTMS. Auf ein leeres GameObject ziehen und Connect() aufrufen.
/// Liefert Speed / Cadence / Power und nimmt mit SetResistance() eine Steigung entgegen.
/// Braucht BleApi.cs und die GEPATCHTE BleWinrtDll.dll im Projekt.
/// </summary>
public class Trainer : MonoBehaviour
{
    public string deviceName = "TUO";

    const string Service  = "{00001826-0000-1000-8000-00805f9b34fb}";
    const string BikeData = "{00002ad2-0000-1000-8000-00805f9b34fb}";  // Notify
    const string Control  = "{00002ad9-0000-1000-8000-00805f9b34fb}";  // Write + Indicate

    // Feste FTMS-Koeffizienten. Spaeter frei ersetzbar.
    const byte  Crr      = 0x3C;    // Einheit 0.0001  -> 0.006
    const byte  Cw       = 0x14;    // Einheit 0.01    -> 0.20 kg/m
    const float MaxGrade = 0.10f;   // dimensionslos, 0.10 = 10 % (mehr kann der Tuo nicht)

    // ---- die vier Dinge ----
    public float Speed   { get; private set; }   // km/h
    public float Cadence { get; private set; }   // rpm
    public float Power   { get; private set; }   // Watt
    public void SetResistance(float grade) => _wanted = Mathf.Clamp(grade, -MaxGrade, MaxGrade);

    public bool   IsReady { get; private set; }
    public string Status  { get; private set; } = "nicht verbunden";

    string _dev = "", _svc = "";
    bool   _subscribed, _hasData, _hasControl, _busy;
    float  _wanted, _sent = float.NaN;
    string _lastError;

    // =========================================================================
    // Startet sich beim Druecken von Play von selbst - egal welche Szene geladen
    // ist und ob irgendwo ein GameObject dafuer existiert. Kein Setup noetig.
    // Wer es lieber von Hand steuert: diese Methode loeschen und Connect() selbst
    // aufrufen.
    // =========================================================================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        var t = FindFirstObjectByType<Trainer>();
        if (t == null)
        {
            var go = new GameObject("Trainer (auto)");
            DontDestroyOnLoad(go);
            t = go.AddComponent<Trainer>();
            go.AddComponent<TrainerUI>();
        }
        Debug.Log("[BLE] AutoStart - verbinde mit " + t.deviceName);
        t.Connect();
    }

    /// <summary>Auch per Rechtsklick auf die Trainer-Component im Inspector aufrufbar.</summary>
    [ContextMenu("Verbinden")]
    public void Connect()
    {
        // Zweiter Aufruf waehrend ein Versuch laeuft wuerde per Quit() den eigenen
        // Scan abschiessen. Genau so entsteht ein "Geraet nicht gefunden".
        if (_busy) { Debug.LogWarning("[BLE] Verbindungsversuch laeuft bereits."); return; }
        Debug.Log("[BLE] Connect() aufgerufen.");
        StartCoroutine(ConnectRoutine());
    }

    IEnumerator ConnectRoutine()
    {
        _busy = true;
        IsReady = false;
        Status = "suche " + deviceName;
        BleApi.Quit();                              // schliesst alle Handles ...
        yield return new WaitForSeconds(1.5f);      // ... Windows braucht danach Zeit

        yield return Scan();
        if (_dev == "") { Status = "Geraet nicht gefunden"; Debug.LogError("[BLE] " + Status); _busy = false; yield break; }

        // Windows baut die Verbindung erst beim Zugriff auf -> mehrere Anlaeufe
        for (int i = 1; i <= 4 && _svc == ""; i++)
        {
            Status = "Service-Suche " + i + "/4";
            Debug.Log("[BLE] " + Status);
            yield return ScanServices();
            if (_svc == "") yield return new WaitForSeconds(1.5f);
        }
        if (_svc == "") { Status = "FTMS-Service nicht gefunden"; Debug.LogError("[BLE] " + Status); _busy = false; yield break; }

        // PFLICHT, auch wenn man das Ergebnis nicht braeuchte: erst dieser Scan
        // fuellt in der DLL die Handles der Characteristics. Ohne ihn laufen die
        // beiden Subscribe-Aufrufe unten ins Leere - ohne Fehlermeldung.
        yield return ScanCharacteristics();
        if (!_hasData || !_hasControl)
        {
            Status = "Characteristic fehlt";
            Debug.LogError($"[BLE] {Status}. Data={_hasData} Control={_hasControl}");
            _busy = false;
            yield break;
        }

        // _Read abonniert Notify, _Write abonniert Indicate. 0x2AD9 kann nur Indicate.
        BleApi.SubscribeCharacteristic_Read(_dev, _svc, BikeData, false);
        BleApi.SubscribeCharacteristic_Write(_dev, _svc, Control, false);
        _subscribed = true;
        yield return new WaitForSeconds(0.5f);      // CCCD-Writes durchlassen

        // Pflichtreihenfolge mit Pausen, sonst antwortet der Tuo mit 0x05.
        Debug.Log("[BLE] -> 0x00 Request Control");
        Write(new byte[] { 0x00 });
        yield return new WaitForSeconds(1f);

        Debug.Log("[BLE] -> 0x07 Start/Resume");
        Write(new byte[] { 0x07 });
        yield return new WaitForSeconds(1f);

        IsReady = true;
        _busy = false;
        Status = "verbunden";
        Debug.Log("[BLE] bereit.");
        StartCoroutine(SendLoop());
    }

    /// <summary>Hoechstens 1 Write pro Sekunde - schneller und die Verbindung bricht ab.</summary>
    IEnumerator SendLoop()
    {
        var wait = new WaitForSeconds(1f);
        while (IsReady)
        {
            if (float.IsNaN(_sent) || Mathf.Abs(_wanted - _sent) > 0.0005f)
            {
                // Opcode 0x11, 7 Bytes, little-endian. Steigung in Einheiten von 0.01 %,
                // darum dimensionslos * 10000. Wind bleibt 0.
                short g = (short)Mathf.RoundToInt(_wanted * 10000f);
                Write(new byte[] { 0x11, 0, 0, (byte)(g & 0xFF), (byte)((g >> 8) & 0xFF), Crr, Cw });
                _sent = _wanted;
            }
            yield return wait;
        }
    }

    void Write(byte[] p)
    {
        if (_dev == "") return;
        var d = new BleApi.BLEData
        {
            buf = new byte[512], size = (short)p.Length,
            deviceId = _dev, serviceUuid = _svc, characteristicUuid = Control
        };
        Array.Copy(p, d.buf, p.Length);
        BleApi.SendData(in d, false);   // Rueckgabe ist hier IMMER false - kein Fehlerindikator
        Debug.Log("[BLE] TX " + BitConverter.ToString(p).Replace("-", " "));
    }

    void Update()
    {
        if (!_subscribed) return;

        while (BleApi.PollData(out BleApi.BLEData r, false))
        {
            if (r.size >= 3 && r.buf[0] == 0x80) ControlResponse(r);   // 0x80 = Antwort
            else ParseBikeData(r);
        }

        var e = new BleApi.ErrorMessage();
        BleApi.GetError(out e);
        string m = e.msg?.Trim().Trim('\0') ?? "";
        if (m.Length == 0 || m == "Ok") return;      // "Ok" ist KEIN Fehler
        if (m != _lastError) { Debug.LogError("[BLE] " + m); _lastError = m; }
    }

    /// <summary>Antwort ist immer 3 Bytes: 0x80, angefragter Opcode, Result Code. 0x01 = Success.</summary>
    void ControlResponse(BleApi.BLEData r)
    {
        string bedeutung = r.buf[2] switch
        {
            0x01 => "Success",
            0x02 => "Op Code not supported",
            0x03 => "Invalid Parameter",
            0x04 => "Operation Failed",
            0x05 => "Control Not Permitted (Request Control fehlt)",
            _    => "unbekannt"
        };
        string t = $"RX 0x{r.buf[1]:X2} -> {bedeutung}";
        if (r.buf[2] == 0x01) Debug.Log("[BLE] " + t); else Debug.LogWarning("[BLE] " + t);
    }

    /// <summary>Indoor Bike Data 0x2AD2. Feldreihenfolge fest, die Flags sagen nur was drin ist.</summary>
    void ParseBikeData(BleApi.BLEData r)
    {
        if (r.size < 2) return;
        int i = 0;
        ushort f = BitConverter.ToUInt16(r.buf, i); i += 2;

        if ((f & 0x0001) == 0) { Speed   = BitConverter.ToUInt16(r.buf, i) / 100f; i += 2; } // Bit 0 INVERTIERT
        if ((f & 0x0002) != 0) i += 2;                                                       // Average Speed
        if ((f & 0x0004) != 0) { Cadence = BitConverter.ToUInt16(r.buf, i) / 2f;   i += 2; }
        if ((f & 0x0008) != 0) i += 2;                                                       // Average Cadence
        if ((f & 0x0010) != 0) i += 3;                                                       // Total Distance = uint24
        if ((f & 0x0020) != 0) i += 2;                                                       // Resistance Level
        if ((f & 0x0040) != 0) Power = BitConverter.ToInt16(r.buf, i);
    }

    IEnumerator Scan()
    {
        _dev = "";
        var names = new Dictionary<string, string>();
        var connectable = new Dictionary<string, bool>();
        string kandidat = "";          // richtiger Name gesehen, aber noch nicht bestaetigt
        float first = -1f;

        BleApi.StartDeviceScan();
        var d = new BleApi.DeviceUpdate();
        BleApi.ScanStatus s;
        do
        {
            s = BleApi.PollDevice(ref d, false);
            if (s == BleApi.ScanStatus.AVAILABLE)
            {
                // Name und isConnectable kommen in getrennten Paketen -> beides sammeln.
                if (d.nameUpdated) names[d.id] = d.name;
                if (d.isConnectableUpdated) connectable[d.id] = d.isConnectable;

                if (names.TryGetValue(d.id, out string n) && !string.IsNullOrEmpty(n)
                    && n.Trim().StartsWith(deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    kandidat = d.id;
                    if (first < 0f) first = Time.time;
                    connectable.TryGetValue(d.id, out bool c);
                    if (c)
                    {
                        _dev = d.id;
                        Debug.Log($"[BLE] Geraet gefunden: '{n}' connectable=True");
                        yield break;   // Scan NICHT stoppen - der DLL ist das lieber
                    }
                }
            }

            // Notbremse ausserhalb des AVAILABLE-Zweigs: die DLL initialisiert
            // isConnectableUpdated nicht, der Wert kann also nie kommen. Sendet der
            // Trainer dann keine weiteren Pakete mehr, wuerde drinnen nie wieder
            // geprueft und der Scan liefe ins Leere.
            if (kandidat != "" && Time.time - first > 4f)
            {
                _dev = kandidat;
                Debug.LogWarning("[BLE] Geraet gefunden (ohne connectable-Bestaetigung).");
                yield break;
            }
            yield return null;
        } while (s == BleApi.ScanStatus.AVAILABLE || s == BleApi.ScanStatus.PROCESSING);

        // Scan zu Ende: lieber den Kandidaten versuchen als aufgeben.
        if (kandidat != "")
        {
            _dev = kandidat;
            Debug.LogWarning("[BLE] Scan beendet - versuche den Kandidaten trotzdem.");
            yield break;
        }
        foreach (var kv in names) Debug.LogWarning($"[BLE] gesehen: '{kv.Value}'");
    }

    IEnumerator ScanServices()
    {
        _svc = "";
        BleApi.ScanServices(_dev);
        BleApi.ScanStatus s;
        do
        {
            s = BleApi.PollService(out BleApi.Service v, false);
            if (s == BleApi.ScanStatus.AVAILABLE)
            {
                Debug.Log("[BLE]   Service: " + v.uuid);
                if (Eq(v.uuid, Service)) { _svc = v.uuid; yield break; }
            }
            yield return null;
        } while (s == BleApi.ScanStatus.AVAILABLE || s == BleApi.ScanStatus.PROCESSING);
    }

    IEnumerator ScanCharacteristics()
    {
        _hasData = _hasControl = false;
        BleApi.ScanCharacteristics(_dev, _svc);
        BleApi.ScanStatus s;
        do
        {
            s = BleApi.PollCharacteristic(out BleApi.Characteristic c, false);
            if (s == BleApi.ScanStatus.AVAILABLE)
            {
                Debug.Log("[BLE]   Characteristic: " + c.uuid);
                if (Eq(c.uuid, BikeData)) _hasData = true;
                if (Eq(c.uuid, Control))  _hasControl = true;
            }
            yield return null;
        } while (s == BleApi.ScanStatus.AVAILABLE || s == BleApi.ScanStatus.PROCESSING);
    }

    static bool Eq(string a, string b)
        => string.Equals(a?.Trim().Trim('\0'), b?.Trim().Trim('\0'), StringComparison.OrdinalIgnoreCase);

    void OnApplicationQuit()
    {
        if (IsReady) Write(new byte[] { 0x08 });   // Stop
        IsReady = false;
        _subscribed = false;
        BleApi.Quit();
    }
}
