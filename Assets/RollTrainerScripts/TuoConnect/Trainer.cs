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

    [Tooltip("Nach einem Fehlschlag automatisch neu versuchen, bis es klappt.")]
    public bool  autoRetry = true;
    [Tooltip("Wartezeit zwischen zwei Versuchen, in Sekunden.")]
    public float retrySeconds = 5f;

    const string Service  = "{00001826-0000-1000-8000-00805f9b34fb}";
    const string BikeData = "{00002ad2-0000-1000-8000-00805f9b34fb}";  // Notify
    const string Control  = "{00002ad9-0000-1000-8000-00805f9b34fb}";  // Write + Indicate

    // Crr und Cw sind KEINE Konstanten mehr. Sie kommen jetzt von aussen ueber
    // SetSimulation() - siehe Bremskraft.cs. Startwerte stehen unten bei den Feldern.
    const float MaxGrade = 0.10f;   // dimensionslos, 0.10 = 10 % (mehr kann der Tuo nicht)

    // ---- die vier Dinge ----
    public float Speed   { get; private set; }   // km/h
    public float Cadence { get; private set; }   // rpm
    public float Power   { get; private set; }   // Watt
    /// <summary>Nur die Steigung setzen. Crr und Cw bleiben, wie sie sind.</summary>
    public void SetResistance(float grade) => _wantedG = Mathf.Clamp(grade, -MaxGrade, MaxGrade);

    /// <summary>Steigung, Untergrund und Luftwiderstand in einem Aufruf. Darf jeden
    /// Frame kommen - gefunkt wird trotzdem hoechstens 1x pro Sekunde.</summary>
    public void SetSimulation(float grade, float crr, float cw)
    {
        _wantedG = Mathf.Clamp(grade, -MaxGrade, MaxGrade);
        _wantedC = Mathf.Clamp(crr,   0f, 0.0255f);   // Bereich, den ein Byte hergibt
        _wantedK = Mathf.Clamp(cw,    0f, 2.55f);
    }

    /// <summary>Steigung, die zuletzt wirklich rausging. Fuer Anzeigen.</summary>
    public float GesendeteSteigung => _sentG == int.MinValue ? 0f : _sentG / 10000f;

    /// <summary>true, wenn der Tuo am Anschlag ist und mehr nicht liefern kann.</summary>
    public bool AmAnschlag => Mathf.Abs(_wantedG) >= MaxGrade - 0.0001f;

    public bool   IsReady { get; private set; }
    public string Status  { get; private set; } = "nicht verbunden";

    string _dev = "", _svc = "";
    bool   _subscribed, _hasData, _hasControl, _busy;
    // Gewuenschte Werte, von aussen gesetzt:
    float  _wantedG;                 // Steigung, dimensionslos (0.05 = 5 %)
    float  _wantedC = 0.006f;        // Crr
    float  _wantedK = 0.20f;         // Cw in kg/m
    // Was davon zuletzt gefunkt wurde - schon in FTMS-Einheiten, darum int:
    int    _sentG = int.MinValue, _sentC = -1, _sentK = -1;
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

    /// <summary>Versucht es so lange, bis die Verbindung steht - fuer VR, wo niemand
    /// einen Knopf druecken kann.</summary>
    IEnumerator ConnectRoutine()
    {
        _busy = true;
        int nr = 0;
        while (!IsReady)
        {
            nr++;
            yield return Versuch();
            if (IsReady || !autoRetry) break;
            Status = $"Versuch {nr} fehlgeschlagen, neuer in {retrySeconds:F0} s";
            Debug.LogWarning("[BLE] " + Status);
            yield return new WaitForSeconds(retrySeconds);
        }
        _busy = false;
    }

    IEnumerator Versuch()
    {
        IsReady = false;
        Status = "suche " + deviceName;
        BleApi.Quit();                              // schliesst alle Handles ...
        yield return new WaitForSeconds(1.5f);      // ... Windows braucht danach Zeit

        yield return Scan();
        if (_dev == "") { Status = "Geraet nicht gefunden"; Debug.LogError("[BLE] " + Status); yield break; }

        // Windows baut die Verbindung erst beim Zugriff auf -> mehrere Anlaeufe
        for (int i = 1; i <= 4 && _svc == ""; i++)
        {
            Status = "Service-Suche " + i + "/4";
            Debug.Log("[BLE] " + Status);
            yield return ScanServices();
            if (_svc == "") yield return new WaitForSeconds(1.5f);
        }
        if (_svc == "") { Status = "FTMS-Service nicht gefunden"; Debug.LogError("[BLE] " + Status); yield break; }

        // PFLICHT, auch wenn man das Ergebnis nicht braeuchte: erst dieser Scan
        // fuellt in der DLL die Handles der Characteristics. Ohne ihn laufen die
        // beiden Subscribe-Aufrufe unten ins Leere - ohne Fehlermeldung.
        yield return ScanCharacteristics();
        if (!_hasData || !_hasControl)
        {
            Status = "Characteristic fehlt";
            Debug.LogError($"[BLE] {Status}. Data={_hasData} Control={_hasControl}");
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
            // Unity-Werte in die Einheiten des Protokolls umrechnen:
            //   Steigung in 0.01 %  -> dimensionslos * 10000   (0.05  -> 500)
            //   Crr      in 0.0001  -> 0.006 * 10000 =  60 = 0x3C
            //   Cw       in 0.01    -> 0.30  * 100   =  30 = 0x1E
            int g = Mathf.RoundToInt(_wantedG * 10000f);
            int c = Mathf.Clamp(Mathf.RoundToInt(_wantedC * 10000f), 0, 255);
            int k = Mathf.Clamp(Mathf.RoundToInt(_wantedK * 100f),   0, 255);

            // Totband von 20 Einheiten = 0.2 % Steigung. Kleineres spuert niemand,
            // und es filtert das Zittern der Neigungsmessung weg.
            // WICHTIG: hier muessen ALLE Werte geprueft werden, die mitgesendet
            // werden. Wer spaeter einen dazunimmt und das hier vergisst, dreht im
            // Inspector und es passiert nichts - ohne jede Fehlermeldung.
            bool neu = _sentG == int.MinValue
                    || Mathf.Abs(g - _sentG) > 20
                    || c != _sentC
                    || k != _sentK;

            if (neu)
            {
                // Opcode 0x11, 7 Bytes, little-endian:
                // [0]=0x11  [1][2]=Wind (bleibt 0)  [3][4]=Steigung  [5]=Crr  [6]=Cw
                Write(new byte[] { 0x11, 0, 0,
                    (byte)(g & 0xFF), (byte)((g >> 8) & 0xFF),
                    (byte)c, (byte)k });
                _sentG = g; _sentC = c; _sentK = k;
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
