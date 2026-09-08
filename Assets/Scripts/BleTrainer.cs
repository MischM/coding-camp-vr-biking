using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Echter Elite Tuo über BLE FTMS (BleWinrtDll).
/// Als Komponente auf ein leeres GameObject ziehen.
///
/// Ablauf: Scan -> Service -> beide Characteristics -> Notify(0x2AD2) +
/// Indicate(0x2AD9) -> 0x00 Request Control -> 0x07 Start -> dann 0x11 im 1-Hz-Takt.
/// </summary>
public class BleTrainer : MonoBehaviour, ITrainer
{
    // ---------- Konfiguration (im Inspector änderbar) ----------
    [Header("BLE")]
    public string deviceName = "TUO";
    public string serviceId = "{00001826-0000-1000-8000-00805f9b34fb}";
    public string bikeDataCharacteristic = "{00002ad2-0000-1000-8000-00805f9b34fb}"; // Notify
    public string controlPointCharacteristic = "{00002ad9-0000-1000-8000-00805f9b34fb}"; // Write + Indicate

    // =====================================================================
    // FAHRPHYSIK
    //
    // Diese Werte werden NICHT direkt gesendet. Sie werden in RidePhysics in
    // die FTMS-Koeffizienten umgerechnet und mit Opcode 0x11 an den Trainer
    // geschickt. Der Tuo misst dann selbst die Geschwindigkeit und stellt die
    // Bremse laufend nach - der quadratische Luftwiderstand entsteht also im
    // Trainer, nicht hier.
    // =====================================================================

    [Header("Fahrer + Rad")]
    [Tooltip("Koerpergewicht in kg.")]
    [Range(40f, 120f)] public float riderMassKg = 70f;
    [Tooltip("Gewicht des Rads in kg.")]
    [Range(5f, 25f)] public float bikeMassKg = 9f;

    [Header("Luftwiderstand (quadratisch mit v)")]
    [Tooltip("CdA = Stirnflaeche × Luftwiderstandsbeiwert, in m².\n" +
             "Bestimmt den Luftwiderstand fast allein.\n\n" +
             "0.22  Zeitfahrposition\n" +
             "0.28  Rennrad Unterlenker\n" +
             "0.32  Rennrad Oberlenker   <- Standard\n" +
             "0.40  Gravel, aufrecht\n" +
             "0.55  Citybike, aufrecht\n\n" +
             "P_luft = 0.5 · rho · CdA · v³ - doppeltes Tempo, achtfache Leistung.")]
    [Range(0.15f, 0.70f)] public float cdA = 0.32f;

    [Tooltip("Luftdichte in kg/m³. 1.225 = Meereshoehe 15 °C, 1.167 = ca. 500 m.")]
    [Range(0.9f, 1.35f)] public float airDensity = RidePhysics.RhoSeaLevel;

    [Tooltip("Korrekturfaktor fuer den gesendeten Cw.\n\n" +
             "Die FTMS-Spec nennt nur die Einheit kg/m, nicht die Formel. Manche " +
             "Trainer-Firmware rechnet mit Cw = rho·CdA statt 0.5·rho·CdA.\n\n" +
             "Fuehlt sich der Luftwiderstand am Geraet zu schwach an: hochdrehen. " +
             "Zu stark: runter. 1.0 = Lehrbuchformel.")]
    [Range(0.5f, 2.5f)] public float windCoefficientScale = 1.0f;

    [Header("Rollwiderstand (linear mit v)")]
    [Tooltip("Crr, dimensionslos.\n\n" +
             "0.004  Rennrad auf Asphalt\n" +
             "0.006  Gravel auf Asphalt   <- Standard\n" +
             "0.012  MTB auf Schotter\n\n" +
             "Maximum durch FTMS: 0.0255.")]
    [Range(0.002f, 0.0255f)] public float crr = 0.006f;

    // =====================================================================
    // STEIGUNGS-KALIBRIERUNG
    //
    // Die Magnetbremse des Tuo hat einen mechanischen Grundwiderstand. Bei 0 %
    // steht sie schon fast am unteren Anschlag - deshalb fuehlen sich -2 %,
    // -5 % und -10 % voellig gleich an.
    //
    // Loesung: die Steigung aus dem Spiel wird auf einen Bereich abgebildet,
    // der komplett ueber diesem Anschlag liegt. Drei Stuetzpunkte, dazwischen
    // linear interpoliert.
    // =====================================================================

    [Header("Steigungs-Kalibrierung")]
    [Tooltip("Steigungsbereich im SPIEL, den der Slider bzw. die Strecke liefert.")]
    [Range(2f, 15f)] public float maxGradePercent = 10f;

    [Tooltip("Was gesendet wird, wenn es im Spiel maximal BERGAB geht.\n" +
             "0 = Bremse so weit auf wie moeglich.")]
    [Range(0f, 5f)] public float simGradeAtDownhill = 0f;

    [Tooltip("Was gesendet wird, wenn es im Spiel EBEN ist.\n\n" +
             "Das ist der Grundwiderstand. Zu hoch = die Ebene ist schon " +
             "anstrengend. Zu niedrig = Gefaelle fuehlt sich nicht leichter an, " +
             "weil die Bremse am Anschlag steht.\n\n" +
             "1.5 ist ein realistischer Startwert: kostet bei 25 km/h und 79 kg " +
             "rund 80 W zusaetzlich - deutlich spuerbar, aber nicht anstrengend.")]
    [Range(0f, 5f)] public float simGradeAtFlat = 1.5f;

    [Tooltip("Was gesendet wird, wenn es im Spiel maximal BERGAUF geht.\n" +
             "Der Tuo kann hoechstens 10 % simulieren. 8 laesst Reserve und " +
             "vermeidet, dass die Bremse in die Saettigung laeuft.")]
    [Range(2f, 10f)] public float simGradeAtUphill = 8f;

    [Header("Limits")]
    [Tooltip("Mindestabstand zwischen zwei Control-Point-Writes. NICHT unter 1.0 setzen!")]
    public float writeIntervalSeconds = 1.0f;

    // ---------- ITrainer ----------
    public event Action<TrainerData> OnData;
    public bool IsReady { get; private set; }

    /// <summary>Wird nur gemerkt. Das tatsächliche Senden macht SendLoop() im 1-Hz-Takt.</summary>
    public void SetGrade(float gradePercent)
    {
        _wantedGrade = Mathf.Clamp(gradePercent, -maxGradePercent, maxGradePercent);
    }

    // ---------- interner Zustand ----------
    private string _deviceId = "";
    private string _serviceUuid = "";
    private bool _subscribed;
    private float _wantedGrade;
    private float _lastSentGrade = float.NaN;
    private string _lastError;
    private readonly Dictionary<string, string> _seenNames = new Dictionary<string, string>();
    private readonly Dictionary<string, bool> _connectable = new Dictionary<string, bool>();
    private float _ersteSichtung = -1f;

    public string LastControlPointResponse { get; private set; } = "-";
    public TrainerData Latest { get; private set; }

    // =====================================================================
    // Oeffentliche Werte fuer UI und Spiel-Logik
    // =====================================================================

    /// <summary>Steigung, die das Spiel zuletzt gesetzt hat (Slider/Strecke), in %.</summary>
    public float GameGradePercent => _wantedGrade;

    /// <summary>Steigung, die nach der Kalibrierung wirklich an den Trainer geht, in %.</summary>
    public float SimGradePercent => MapGrade(_wantedGrade);

    /// <summary>Fahrer + Rad in kg.</summary>
    public float TotalMassKg => riderMassKg + bikeMassKg;

    /// <summary>Cw in kg/m, wie er kodiert und gesendet wird.</summary>
    public float WindCoefficient => RidePhysics.CwFromCdA(cdA, airDensity) * windCoefficientScale;

    /// <summary>Aktuelle Geschwindigkeit vom Trainer, in m/s.</summary>
    public float SpeedMs => RidePhysics.KmhToMs(Latest.SpeedKmh);

    /// <summary>Luftwiderstand JETZT, in Newton. Waechst quadratisch mit dem Tempo.</summary>
    public float AeroForceN => RidePhysics.AeroForceN(cdA, airDensity, SpeedMs);

    /// <summary>Leistungsanteil gegen die Luft, in Watt. Waechst kubisch.</summary>
    public float AeroPowerW => RidePhysics.AeroPowerW(cdA, airDensity, SpeedMs);

    /// <summary>Leistungsanteil gegen den Rollwiderstand, in Watt.</summary>
    public float RollingPowerW => RidePhysics.RollingPowerW(crr, TotalMassKg, SpeedMs);

    /// <summary>Leistungsanteil gegen die Steigung, in Watt. Negativ bergab.</summary>
    public float ClimbPowerW => RidePhysics.ClimbPowerW(SimGradePercent, TotalMassKg, SpeedMs);

    /// <summary>
    /// Rechnerisch erwartete Gesamtleistung bei aktuellem Tempo und aktueller
    /// Steigung. Vergleich mit Latest.PowerW zeigt, ob der Trainer plausibel bremst.
    /// </summary>
    public float ExpectedPowerW => AeroPowerW + RollingPowerW + ClimbPowerW;

    // =====================================================================
    // Verbindungsaufbau
    // =====================================================================
    public void Connect() => StartCoroutine(ConnectRoutine());

    private IEnumerator ConnectRoutine()
    {
        IsReady = false;
        // Quit() schliesst in der DLL alle Geraete- und Service-Handles. Windows
        // braucht danach einen Moment, bis die alte Verbindung wirklich weg ist -
        // 0.2s waren zu knapp und fuehrten zu haengenden Reconnects.
        BleApi.Quit();
        yield return new WaitForSeconds(1.5f);

        yield return ScanForDevice();
        if (_deviceId.Length == 0) { Debug.LogError($"[BLE] Gerät '{deviceName}' nicht gefunden."); yield break; }

        // Service-Suche darf ruhig ein paar Anläufe brauchen: Windows baut die
        // Verbindung erst beim ersten Zugriff auf, der erste Versuch geht oft leer aus.
        for (int versuch = 1; versuch <= 4 && _serviceUuid.Length == 0; versuch++)
        {
            Debug.Log($"[BLE] Service-Suche, Versuch {versuch}/4 ...");
            yield return ScanForService();
            if (_serviceUuid.Length == 0) yield return new WaitForSeconds(1.5f);
        }
        if (_serviceUuid.Length == 0)
        {
            Debug.LogError($"[BLE] Service {serviceId} nicht gefunden. " +
                           "Siehe die Liste der gefundenen Services oben im Log - ist sie leer, " +
                           "blockiert eine andere App die Verbindung oder der Tuo ist nicht in Windows gekoppelt.");
            yield break;
        }

        // Beide Characteristics müssen im Scan auftauchen, sonst stimmen die UUIDs nicht.
        bool foundData = false, foundControl = false;
        yield return ScanCharacteristics(r =>
        {
            if (r == bikeDataCharacteristic) foundData = true;
            if (r == controlPointCharacteristic) foundControl = true;
        });
        if (!foundData || !foundControl)
        {
            Debug.LogError($"[BLE] Characteristic fehlt. Data={foundData} Control={foundControl}");
            yield break;
        }

        // FIX 1: Beide Subscriptions EINMAL beim Verbinden - nicht bei jedem Write.
        BleApi.SubscribeCharacteristic_Read(_deviceId, _serviceUuid, bikeDataCharacteristic, false);
        BleApi.SubscribeCharacteristic_Write(_deviceId, _serviceUuid, controlPointCharacteristic, false);
        _subscribed = true;
        yield return new WaitForSeconds(0.5f);   // CCCD-Writes durchlassen

        // FIX 2: Pflichtsequenz mit echten Pausen dazwischen.
        Debug.Log("[BLE] -> 0x00 Request Control");
        WriteControlPoint(new byte[] { 0x00 });
        yield return new WaitForSeconds(1.0f);

        Debug.Log("[BLE] -> 0x07 Start/Resume");
        WriteControlPoint(new byte[] { 0x07 });
        yield return new WaitForSeconds(1.0f);

        IsReady = true;
        Debug.Log("[BLE] bereit - Steigung kann gesetzt werden.");
        StartCoroutine(SendLoop());
    }

    // =====================================================================
    // Sende-Schleife: FIX 3 - garantiert max. 1 Write pro Sekunde
    // =====================================================================
    private IEnumerator SendLoop()
    {
        var wait = new WaitForSeconds(writeIntervalSeconds);
        while (IsReady)
        {
            // Nur senden, wenn sich die Steigung um >0.05% geändert hat.
            if (float.IsNaN(_lastSentGrade) || Mathf.Abs(_wantedGrade - _lastSentGrade) > 0.05f)
            {
                WriteControlPoint(BuildSimulationPacket(_wantedGrade));
                _lastSentGrade = _wantedGrade;
            }
            yield return wait;
        }
    }

    /// <summary>
    /// Opcode 0x11 SetIndoorBikeSimulationParameters, 7 Bytes, alles little-endian.
    /// Beispiel 10%: 11 00 00 E8 03 32 33
    /// </summary>
    public byte[] BuildSimulationPacket(float gradePercent)
    {
        short grade = RidePhysics.EncodeGrade(MapGrade(gradePercent));  // 0.01 %
        short wind = 0;                                                 // 0.001 m/s

        // Hier entsteht der Luftwiderstand: CdA -> Cw -> ein Byte im Paket.
        // Der Trainer bildet daraus intern F = Cw · v², also quadratisch.
        byte cwRaw = RidePhysics.EncodeCw(WindCoefficient);
        byte crrRaw = RidePhysics.EncodeCrr(crr);

        return new byte[]
        {
            0x11,
            (byte)(wind  & 0xFF), (byte)((wind  >> 8) & 0xFF),
            (byte)(grade & 0xFF), (byte)((grade >> 8) & 0xFF),
            crrRaw,
            cwRaw
        };
    }

    /// <summary>
    /// Bildet die Steigung aus dem Spiel auf die Steigung ab, die wirklich
    /// gesendet wird. Stueckweise linear ueber drei Stuetzpunkte:
    ///
    ///     Spiel -maxGrade  ->  simGradeAtDownhill
    ///     Spiel        0   ->  simGradeAtFlat      (Grundwiderstand)
    ///     Spiel +maxGrade  ->  simGradeAtUphill
    ///
    /// Warum ueberhaupt: die Magnetbremse hat einen mechanischen Anschlag nach
    /// unten. Wuerde man 0 % senden, stuende sie dort schon fast an und jedes
    /// Gefaelle fuehlte sich gleich an. Mit dem Versatz bleibt nach unten Luft.
    /// </summary>
    public float MapGrade(float gradePercent)
    {
        float s = Mathf.Clamp(gradePercent, -maxGradePercent, maxGradePercent);
        float t = maxGradePercent > 0f ? Mathf.Abs(s) / maxGradePercent : 0f;

        return s >= 0f
            ? Mathf.Lerp(simGradeAtFlat, simGradeAtUphill, t)
            : Mathf.Lerp(simGradeAtFlat, simGradeAtDownhill, t);
    }

    private void WriteControlPoint(byte[] payload)
    {
        if (_deviceId.Length == 0) return;

        var data = new BleApi.BLEData
        {
            buf = new byte[512],
            size = (short)payload.Length,
            deviceId = _deviceId,
            serviceUuid = _serviceUuid,
            characteristicUuid = controlPointCharacteristic
        };
        Array.Copy(payload, data.buf, payload.Length);

        // ACHTUNG: SendData(..., block=false) liefert IMMER false zurueck -
        // die DLL setzt das Ergebnis nur im blockierenden Modus. Der
        // Rueckgabewert ist hier also wertlos, echte Fehler kommen ueber GetError().
        BleApi.SendData(in data, false);

        // Bei 0x11 zusaetzlich anzeigen, was wirklich rausgeht - wegen der
        // Kalibrierung ist die gesendete Steigung nicht die aus dem Spiel.
        string extra = (payload.Length == 7 && payload[0] == 0x11)
            ? $"   -> Steigung {BitConverter.ToInt16(payload, 3) / 100f:F1} %" +
              $", Crr {RidePhysics.DecodeCrr(payload[5]):F4}" +
              $", Cw {RidePhysics.DecodeCw(payload[6]):F2} kg/m"
            : "";
        Debug.Log("[BLE] TX " + BitConverter.ToString(payload).Replace("-", " ") + extra);
    }

    // =====================================================================
    // Diagnose: im Inspector auf die drei Punkte rechts oben am BleTrainer
    // klicken, dann erscheinen diese Eintraege im Menue.
    // =====================================================================

    [ContextMenu("Test: Startsequenz erneut senden")]
    public void ResendStartSequence() => StartCoroutine(ResendRoutine());

    private IEnumerator ResendRoutine()
    {
        Debug.Log("[BLE] --- Startsequenz erneut ---");
        BleApi.SubscribeCharacteristic_Write(_deviceId, _serviceUuid, controlPointCharacteristic, false);
        yield return new WaitForSeconds(0.5f);
        WriteControlPoint(new byte[] { 0x00 });
        yield return new WaitForSeconds(1.5f);
        WriteControlPoint(new byte[] { 0x07 });
        yield return new WaitForSeconds(1.5f);
        _lastSentGrade = float.NaN;      // erzwingt erneutes Senden der Steigung
        Debug.Log("[BLE] --- fertig ---");
    }

    [ContextMenu("Test: fest 10% senden")]
    public void SendTenPercent()
    {
        WriteControlPoint(BuildSimulationPacket(10f));
    }

    /// <summary>
    /// Diagnose: fragt GetError() der DLL ab und loggt IMMER - auch wenn leer.
    /// Die normale Abfrage in Update() unterdrueckt Wiederholungen, dieser Button nicht.
    /// </summary>
    /// <summary>
    /// Druckt ins Log, was die aktuellen Physik-Einstellungen bedeuten - ohne
    /// Trainer, ohne Treten. Zum Abschaetzen, bevor man sich draufsetzt.
    /// </summary>
    [ContextMenu("Physik: Leistungstabelle ins Log")]
    public void LogPowerTable()
    {
        float cw = WindCoefficient;
        Debug.Log(
            $"[Physik] Masse {TotalMassKg:F0} kg | CdA {cdA:F2} m² | rho {airDensity:F3} kg/m³ | Crr {crr:F4}\n" +
            $"[Physik] gesendetes Cw = {cw:F3} kg/m -> Byte 0x{RidePhysics.EncodeCw(cw):X2} " +
            $"(Rundung ergibt effektiv {RidePhysics.DecodeCw(RidePhysics.EncodeCw(cw)):F2} kg/m)\n" +
            $"[Physik] gesendetes Crr -> Byte 0x{RidePhysics.EncodeCrr(crr):X2}\n\n" +
            "--- EBEN (Spiel 0 %) ---\n" +
            RidePhysics.PowerTable(MapGrade(0f), TotalMassKg, crr, cdA, airDensity) +
            "\n--- MAXIMAL BERGAUF ---\n" +
            RidePhysics.PowerTable(MapGrade(maxGradePercent), TotalMassKg, crr, cdA, airDensity) +
            "\n--- MAXIMAL BERGAB ---\n" +
            RidePhysics.PowerTable(MapGrade(-maxGradePercent), TotalMassKg, crr, cdA, airDensity));
    }

    /// <summary>
    /// Faehrt die Steigungsstufen durch und protokolliert, was der Trainer
    /// tatsaechlich an Leistung verlangt. Damit findet man den mechanischen
    /// Anschlag: sobald die gemessene Leistung bei sinkender Steigung nicht
    /// mehr faellt, ist simGradeAtDownhill zu niedrig angesetzt.
    ///
    /// Waehrend des Durchlaufs GLEICHMAESSIG WEITERTRETEN, sonst sind die
    /// Messwerte wertlos.
    /// </summary>
    [ContextMenu("Kalibrierung: Stufen durchfahren (bitte treten!)")]
    public void RunCalibration() => StartCoroutine(CalibrationRoutine());

    private IEnumerator CalibrationRoutine()
    {
        float[] stufen = { -maxGradePercent, -maxGradePercent * 0.5f, 0f,
                            maxGradePercent * 0.5f, maxGradePercent };
        float merker = _wantedGrade;

        Debug.Log("[Kalibrierung] Start - bitte gleichmaessig treten. 5 Stufen à 12 s.");

        foreach (float spielGrad in stufen)
        {
            SetGrade(spielGrad);
            _lastSentGrade = float.NaN;                 // Senden erzwingen
            yield return new WaitForSeconds(8f);        // einpendeln lassen

            float summe = 0f; int n = 0;
            for (int i = 0; i < 4; i++)                 // 4 s mitteln
            {
                yield return new WaitForSeconds(1f);
                summe += Latest.PowerW; n++;
            }
            float gemessen = n > 0 ? summe / n : 0f;

            Debug.Log($"[Kalibrierung] Spiel {spielGrad,6:F1} %  ->  gesendet {MapGrade(spielGrad),5:F1} %  |  " +
                      $"{Latest.SpeedKmh,5:F1} km/h  |  gemessen {gemessen,5:F0} W  |  " +
                      $"erwartet {ExpectedPowerW,5:F0} W");
        }

        SetGrade(merker);
        Debug.Log("[Kalibrierung] Fertig. Faellt die gemessene Leistung nach unten nicht " +
                  "weiter, steht die Bremse am Anschlag -> simGradeAtFlat erhoehen.");
    }

    [ContextMenu("Diagnose: DLL-Fehler abfragen")]
    public void DumpError()
    {
        var err = new BleApi.ErrorMessage();
        BleApi.GetError(out err);
        string msg = err.msg?.Trim().Trim('\0') ?? "";
        if (!IstFehler(msg))
            Debug.Log($"[Diagnose] GetError() = \"{msg}\" -> kein Fehler.");
        else
            Debug.LogWarning("[Diagnose] GetError() = \"" + msg + "\"  <-- ECHTER FEHLER");
    }

    /// <summary>
    /// Diagnose: schickt 10% und fragt danach den DLL-Fehler ab. Das ist der
    /// entscheidende Test - meldet die DLL "Error writing value to characteristic",
    /// bestaetigt das die WriteWithoutResponse-Theorie.
    /// </summary>
    [ContextMenu("Diagnose: 10% senden + Fehler pruefen")]
    public void SendAndCheck() => StartCoroutine(SendAndCheckRoutine());

    private IEnumerator SendAndCheckRoutine()
    {
        Debug.Log("[Diagnose] --- vor dem Write ---");
        DumpError();
        _lastError = null;                       // Unterdrueckung in Update() zuruecksetzen
        WriteControlPoint(BuildSimulationPacket(10f));
        yield return new WaitForSeconds(1.0f);   // SendData ist asynchron, kurz warten
        Debug.Log("[Diagnose] --- nach dem Write ---");
        DumpError();
        Debug.Log("[Diagnose] Letzte Control-Point-Antwort: " + LastControlPointResponse);
    }

    [ContextMenu("Test: Widerstand 0x04 Level 100")]
    public void SendResistanceLevel()
    {
        // Fallback, falls der Tuo 0x11 ignoriert: SetTargetResistanceLevel.
        WriteControlPoint(new byte[] { 0x04, 100 });
    }

    // =====================================================================
    // Empfang
    // =====================================================================
    private void Update()
    {
        if (!_subscribed) return;

        var res = new BleApi.BLEData();
        while (BleApi.PollData(out res, false))
        {
            // FIX 4: Aussortieren, von WELCHER Characteristic das Paket kam.
            // Der Originalcode hat die Control-Point-Antwort als Fahrdaten geparst.
            //
            // Absicherung: Falls die DLL characteristicUuid beim Empfang gar nicht
            // fuellt (kann sein - der Originalcode hat es nie ausgewertet), faellt
            // die Erkennung auf den Paketinhalt zurueck. Control-Point-Antworten
            // beginnen laut FTMS-Spec IMMER mit 0x80.
            bool uuidBekannt = Equal(res.characteristicUuid, controlPointCharacteristic)
                            || Equal(res.characteristicUuid, bikeDataCharacteristic);

            if (!uuidBekannt)
            {
                if (res.size >= 3 && res.buf[0] == 0x80) HandleControlResponse(res);
                else HandleBikeData(res);
            }
            else if (Equal(res.characteristicUuid, controlPointCharacteristic))
                HandleControlResponse(res);
            else
                HandleBikeData(res);
        }

        var err = new BleApi.ErrorMessage();
        BleApi.GetError(out err);
        // Die DLL initialisiert ihren Fehlerpuffer mit dem String "Ok" - das ist
        // KEIN Fehler, sondern heisst "bisher nichts passiert". Rausfiltern.
        if (!IstFehler(err.msg)) return;
        if (err.msg != _lastError)
        {
            Debug.LogError("[BLE] " + err.msg);
            _lastError = err.msg;
        }
    }

    /// <summary>True nur bei einer echten Fehlermeldung der DLL.</summary>
    private static bool IstFehler(string msg)
    {
        string m = msg?.Trim().Trim('\0') ?? "";
        return m.Length > 0 && !m.Equals("Ok", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Antwort ist immer: 0x80, angefragter Opcode, Result Code. 0x01 = Success.</summary>
    private void HandleControlResponse(BleApi.BLEData res)
    {
        if (res.size < 3) return;
        byte op = res.buf[1], result = res.buf[2];
        string meaning = result switch
        {
            0x01 => "Success",
            0x02 => "Op Code not supported",
            0x03 => "Invalid Parameter",
            0x04 => "Operation Failed",
            0x05 => "Control Not Permitted (Request Control fehlt!)",
            _ => "Unbekannt"
        };
        LastControlPointResponse = $"0x{op:X2} -> {meaning}";
        if (result == 0x01) Debug.Log("[BLE] RX OK  " + LastControlPointResponse);
        else Debug.LogWarning("[BLE] RX ERR " + LastControlPointResponse);
    }

    /// <summary>
    /// Indoor Bike Data 0x2AD2. Reihenfolge der Felder ist fest, die Flags-Bitmaske
    /// sagt nur, welche davon drin sind. Bit 0 ist INVERTIERT.
    /// </summary>
    private void HandleBikeData(BleApi.BLEData res)
    {
        if (res.size < 2) return;

        int i = 0;
        ushort flags = BitConverter.ToUInt16(res.buf, i); i += 2;
        var d = Latest;

        // FIX 5: Original war "(flags & 0) == 0" - das ist IMMER wahr,
        // weil x & 0 immer 0 ergibt. Richtig ist Bit 0 mit Maske 1.
        if ((flags & 0x0001) == 0) { d.SpeedKmh = BitConverter.ToUInt16(res.buf, i) / 100f; i += 2; }
        if ((flags & 0x0002) != 0) { i += 2; }                                    // Average Speed
        if ((flags & 0x0004) != 0) { d.CadenceRpm = BitConverter.ToUInt16(res.buf, i) / 2f; i += 2; }
        if ((flags & 0x0008) != 0) { i += 2; }                                    // Average Cadence
        if ((flags & 0x0010) != 0) { i += 3; }                                    // Total Distance = uint24 (3 Bytes!)
        if ((flags & 0x0020) != 0) { i += 2; }                                    // Resistance Level
        if ((flags & 0x0040) != 0) { d.PowerW = BitConverter.ToInt16(res.buf, i); i += 2; }
        // weitere Felder brauchen wir nicht

        Latest = d;
        OnData?.Invoke(d);
    }

    // =====================================================================
    // Scan-Helfer
    // =====================================================================
    private IEnumerator ScanForDevice()
    {
        _deviceId = "";
        _seenNames.Clear();
        _connectable.Clear();
        _ersteSichtung = -1f;

        BleApi.StartDeviceScan();
        var dev = new BleApi.DeviceUpdate();
        BleApi.ScanStatus status;
        do
        {
            status = BleApi.PollDevice(ref dev, false);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                // Name und isConnectable kommen in GETRENNTEN Advertisement-Paketen.
                // Beides muss da sein, sonst kennt Windows das Geraet noch nicht
                // gut genug und die Service-Suche liefert nichts.
                if (dev.nameUpdated) _seenNames[dev.id] = dev.name;
                if (dev.isConnectableUpdated) _connectable[dev.id] = dev.isConnectable;

                bool hatName = _seenNames.TryGetValue(dev.id, out string n) && !string.IsNullOrEmpty(n);
                bool istVerbindbar = _connectable.TryGetValue(dev.id, out bool c) && c;
                bool nameStimmt = hatName && n.Trim().StartsWith(deviceName, StringComparison.OrdinalIgnoreCase);

                if (nameStimmt)
                {
                    // Zeitpunkt merken, ab dem wir den richtigen Namen sehen.
                    if (_ersteSichtung < 0f) _ersteSichtung = Time.time;

                    // Die DLL initialisiert ihre DeviceUpdate-Struktur nicht auf null.
                    // isConnectableUpdated kann daher Muell enthalten, und in
                    // DeviceWatcher_Updated wird der Name gar nicht erst gesetzt.
                    // Deshalb: bevorzugt auf 'connectable' warten, aber nach 4
                    // Sekunden trotzdem versuchen statt ewig zu haengen.
                    bool gedulded = Time.time - _ersteSichtung > 4f;

                    if (istVerbindbar || gedulded)
                    {
                        _deviceId = dev.id;
                        Debug.Log($"[BLE] Geraet gefunden: '{n}'  connectable={istVerbindbar}"
                                  + (gedulded && !istVerbindbar ? "  (ohne Bestaetigung, Versuch trotzdem)" : ""));
                        yield break;   // Scan NICHT stoppen - der DLL ist das lieber
                    }
                }
            }
            yield return null;
        } while (status == BleApi.ScanStatus.AVAILABLE || status == BleApi.ScanStatus.PROCESSING);

        // Nichts gefunden: alles auflisten, was ueberhaupt gesehen wurde.
        Debug.LogWarning("[BLE] '" + deviceName + "' nicht gefunden. Gesehene Geraete:");
        foreach (var kv in _seenNames)
        {
            _connectable.TryGetValue(kv.Key, out bool conn);
            Debug.LogWarning($"    '{kv.Value}'  connectable={conn}");
        }
    }

    private IEnumerator ScanForService()
    {
        _serviceUuid = "";
        BleApi.ScanServices(_deviceId);
        BleApi.Service svc;
        BleApi.ScanStatus status;
        int gesehen = 0;
        do
        {
            status = BleApi.PollService(out svc, false);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                gesehen++;
                Debug.Log("[BLE]   Service: " + svc.uuid);   // zeigt, was der Tuo wirklich anbietet
                if (Equal(svc.uuid, serviceId))
                {
                    _serviceUuid = svc.uuid;
                    Debug.Log("[BLE] FTMS-Service gefunden.");
                    yield break;
                }
            }
            yield return null;
        } while (status == BleApi.ScanStatus.AVAILABLE || status == BleApi.ScanStatus.PROCESSING);

        if (gesehen == 0)
            Debug.LogWarning("[BLE] Gar keine Services zurueckgekommen - Verbindung kam nicht zustande.");
    }

    private IEnumerator ScanCharacteristics(Action<string> onFound)
    {
        BleApi.ScanCharacteristics(_deviceId, _serviceUuid);
        BleApi.Characteristic ch;
        BleApi.ScanStatus status;
        do
        {
            status = BleApi.PollCharacteristic(out ch, false);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                Debug.Log("[BLE] Characteristic: " + ch.uuid);
                if (Equal(ch.uuid, bikeDataCharacteristic)) onFound(bikeDataCharacteristic);
                if (Equal(ch.uuid, controlPointCharacteristic)) onFound(controlPointCharacteristic);
            }
            yield return null;
        } while (status == BleApi.ScanStatus.AVAILABLE || status == BleApi.ScanStatus.PROCESSING);
    }

    /// <summary>UUID-Vergleich ohne Rücksicht auf Groß-/Kleinschreibung und trailing \0.</summary>
    private static bool Equal(string a, string b)
        => string.Equals(a?.Trim().Trim('\0'), b?.Trim().Trim('\0'), StringComparison.OrdinalIgnoreCase);

    private void OnApplicationQuit() => Disconnect();

    public void Disconnect()
    {
        if (IsReady) WriteControlPoint(new byte[] { 0x08 });   // Stop
        IsReady = false;
        _subscribed = false;
        BleApi.Quit();
    }
}
