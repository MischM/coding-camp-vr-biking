using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test-Oberfläche aus dem Referenzprojekt - Klassenname, Feldnamen und
/// Methodennamen sind ABSICHTLICH unverändert, damit die Verdrahtung in
/// test.unity (Buttons, Slider, InputFields) weiter funktioniert.
///
/// Neu: intern läuft alles über ITrainer/BleTrainer statt über die alte
/// FTMS_IndoorBike-Klasse. Der Slider liefert jetzt direkt PROZENT (-10..+10).
/// </summary>
public class FTMS_UI : MonoBehaviour
{
    // --- diese Felder sind in der Szene verdrahtet, Namen nicht ändern! ---
    public Text info;
    public Text resistance_show;

    public string device_name = "TUO";
    public string service_id = "{00001826-0000-1000-8000-00805f9b34fb}";
    public string read_characteristic = "{00002ad2-0000-1000-8000-00805f9b34fb}";
    public string write_characteristic = "{00002ad9-0000-1000-8000-00805f9b34fb}";

    [Header("Testen ohne Hardware")]
    [Tooltip("An = MockTrainer mit Fantasiewerten, kein Bluetooth nötig.")]
    public bool useMockTrainer = false;

    private ITrainer _trainer;
    private BleTrainer _ble;

    // =====================================================================
    public void connect()
    {
        if (_trainer != null) return;   // doppeltes Verbinden verhindern

        if (useMockTrainer)
        {
            _trainer = gameObject.AddComponent<MockTrainer>();
            Debug.Log("[UI] MockTrainer aktiv (kein Bluetooth).");
        }
        else
        {
            _ble = gameObject.AddComponent<BleTrainer>();
            _ble.deviceName = device_name;
            _ble.serviceId = service_id;
            _ble.bikeDataCharacteristic = read_characteristic;
            _ble.controlPointCharacteristic = write_characteristic;
            _trainer = _ble;
            _ble.Connect();
        }

        _trainer.OnData += OnTrainerData;
    }

    /// <summary>Wird vom Slider aufgerufen. val ist jetzt STEIGUNG IN PROZENT.</summary>
    public void write_resistance(float val)
    {
        if (_trainer == null) return;

        _trainer.SetGrade(val);
        if (resistance_show != null)
            resistance_show.text = $"Steigung: {val:F1} %";
    }

    private void OnTrainerData(TrainerData d)
    {
        if (info == null) return;

        string status = _ble != null
            ? (_ble.IsReady ? "bereit" : "verbinde...") + "  |  CP: " + _ble.LastControlPointResponse
            : "Mock";

        info.text = $"Speed: {d.SpeedKmh:F1} km/h\n" +
                    $"Cadence: {d.CadenceRpm:F0} rpm\n" +
                    $"Power: {d.PowerW:F0} W\n" +
                    $"Status: {status}";
    }

    private void OnApplicationQuit()
    {
        if (_ble != null) _ble.Disconnect();
    }

    // --- von den InputFields in der Szene aufgerufen, Namen nicht ändern ---
    public void change_device_name(string v) => device_name = v;
    public void change_service_id(string v) => service_id = v;
    public void change_read_characteristic(string v) => read_characteristic = v;
    public void change_write_characteristic(string v) => write_characteristic = v;
}
