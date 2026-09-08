using UnityEngine;

/// <summary>
/// Bewegt das Fahrer-Objekt durch die Landschaft - angetrieben von der
/// Geschwindigkeit, die der Trainer misst. Wer schneller tritt, faehrt schneller.
///
/// EINRICHTEN:
///   1. Ein GameObject als Fahrer anlegen (Capsule, Kamera, Modell - egal)
///   2. Diese Komponente drauf
///   3. "trainerSource" auf das GameObject ziehen, das BleTrainer oder
///      MockTrainer traegt (im Testaufbau ist das "FTMS_UI")
///   4. "groundMask" auf die Layer setzen, in denen euer Boden liegt
///
/// Lenken mit A/D oder Pfeiltasten, damit man ueberhaupt irgendwo hinkommt.
/// </summary>
public class RiderMover : MonoBehaviour
{
    [Header("Antrieb")]
    [Tooltip("GameObject mit BleTrainer oder MockTrainer.")]
    public MonoBehaviour trainerSource;

    [Tooltip("Massstab zwischen echter und virtueller Welt.\n" +
             "1 = 1 km/h am Trainer sind 1 km/h im Spiel.\n" +
             "Groesser = die Strecke fuehlt sich kuerzer an.")]
    [Range(0.2f, 5f)] public float speedMultiplier = 1f;

    [Tooltip("Falls kein Trainer verbunden ist: mit dieser Geschwindigkeit " +
             "fahren, damit man die Landschaft testen kann. 0 = stehenbleiben.")]
    [Range(0f, 40f)] public float testSpeedKmh = 0f;

    [Header("Lenken")]
    [Range(0f, 180f)] public float turnSpeedDegPerSec = 60f;

    [Header("Bodenkontakt")]
    [Tooltip("Fahrer auf dem Boden halten (Raycast nach unten). " +
             "Aus, wenn ihr eine eigene Physik benutzt.")]
    public bool stickToGround = true;

    [Tooltip("In welchen Layern liegt der Boden?")]
    public LayerMask groundMask = ~0;

    [Tooltip("Von wie weit oben nach unten geschossen wird. Muss groesser sein " +
             "als der groesste Hoehensprung pro Bild.")]
    public float rayStartHeight = 5f;

    [Tooltip("Wie weit ueber dem Boden das Objekt schwebt.")]
    public float heightOffset = 0f;

    // ---------- oeffentlich abfragbar ----------

    /// <summary>Aktuelle Geschwindigkeit in km/h, so wie sie zum Fahren benutzt wird.</summary>
    public float SpeedKmh { get; private set; }

    /// <summary>Seit dem Start zurueckgelegte Strecke in Metern.</summary>
    public float DistanceMeters { get; private set; }

    /// <summary>Summe aller Aufwaertsbewegungen in Metern (Hoehenmeter).</summary>
    public float ElevationGainMeters { get; private set; }

    // ---------- intern ----------
    private ITrainer _trainer;
    private float _trainerSpeedKmh;
    private float _lastHeight;
    private bool _hoeheInitialisiert;

    private void Start()
    {
        _trainer = trainerSource as ITrainer;
        if (_trainer != null)
            _trainer.OnData += OnTrainerData;
        else if (trainerSource != null)
            Debug.LogWarning("[RiderMover] trainerSource implementiert kein ITrainer.");
    }

    private void OnDestroy()
    {
        if (_trainer != null) _trainer.OnData -= OnTrainerData;
    }

    private void OnTrainerData(TrainerData d) => _trainerSpeedKmh = d.SpeedKmh;

    private void Update()
    {
        // --- Geschwindigkeit bestimmen -------------------------------
        // Wenn ein Trainer Daten liefert, gilt der. Sonst der Testwert,
        // damit man die Landschaft auch ohne Hardware abfahren kann.
        SpeedKmh = (_trainer != null && _trainerSpeedKmh > 0.1f)
            ? _trainerSpeedKmh * speedMultiplier
            : testSpeedKmh;

        // --- Lenken ---------------------------------------------------
        float lenken = Input.GetAxis("Horizontal");   // A/D und Pfeiltasten
        transform.Rotate(0f, lenken * turnSpeedDegPerSec * Time.deltaTime, 0f);

        // --- Vorwaerts ------------------------------------------------
        // km/h in m/s, dann mal der Zeit seit dem letzten Bild.
        // Time.deltaTime sorgt dafuer, dass die Geschwindigkeit unabhaengig
        // von der Bildrate ist.
        float metersThisFrame = (SpeedKmh / 3.6f) * Time.deltaTime;
        transform.position += transform.forward * metersThisFrame;
        DistanceMeters += metersThisFrame;

        // --- Auf den Boden setzen -------------------------------------
        if (stickToGround)
        {
            Vector3 von = transform.position + Vector3.up * rayStartHeight;
            if (Physics.Raycast(von, Vector3.down, out RaycastHit treffer,
                                rayStartHeight * 3f, groundMask))
            {
                Vector3 p = transform.position;
                p.y = treffer.point.y + heightOffset;
                transform.position = p;
            }
        }

        // --- Hoehenmeter zaehlen --------------------------------------
        if (!_hoeheInitialisiert)
        {
            _lastHeight = transform.position.y;
            _hoeheInitialisiert = true;
        }
        else
        {
            float delta = transform.position.y - _lastHeight;
            if (delta > 0f) ElevationGainMeters += delta;
            _lastHeight = transform.position.y;
        }
    }

    [ContextMenu("Zaehler zuruecksetzen")]
    public void ResetCounters()
    {
        DistanceMeters = 0f;
        ElevationGainMeters = 0f;
        _hoeheInitialisiert = false;
    }
}
