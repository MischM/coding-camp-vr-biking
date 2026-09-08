using UnityEngine;

/// <summary>
/// Misst die Steigung der Landschaft unter dem Fahrer und schickt sie an den
/// Trainer. Das ist das fehlende Glied zwischen Spielwelt und Bremse.
///
/// Funktioniert mit JEDER Art von Landschaft - Unity Terrain, Mesh, Spline -
/// weil nur die tatsaechliche Bewegung des Fahrers ausgewertet wird:
///
///     Steigung % = Hoehendifferenz / horizontale Strecke × 100
///
/// EINRICHTEN:
///   1. Diese Komponente auf ein beliebiges GameObject (z.B. dasselbe wie
///      RiderMover, oder ein leeres "GameLogic"-Objekt)
///   2. "rider" auf das Fahrer-Objekt ziehen
///   3. "trainerSource" auf das GameObject mit BleTrainer bzw. MockTrainer
/// </summary>
public class RouteGrade : MonoBehaviour
{
    [Header("Verbindungen")]
    [Tooltip("Das Objekt, das sich durch die Landschaft bewegt.")]
    public Transform rider;

    [Tooltip("GameObject mit BleTrainer oder MockTrainer.")]
    public MonoBehaviour trainerSource;

    [Header("Messung")]
    [Tooltip("Ueber welche horizontale Strecke die Steigung gemittelt wird.\n\n" +
             "Klein (1 m)  = reagiert sofort, aber zappelt bei unebenem Boden.\n" +
             "Gross (10 m) = ruhig, aber Kuppen kommen verzoegert.\n\n" +
             "3 bis 5 m ist ein guter Kompromiss.")]
    [Range(0.5f, 20f)] public float sampleDistanceMeters = 4f;

    [Tooltip("Zusaetzliche Glaettung in Sekunden. 0 = aus.\n" +
             "Verhindert Rucken, wenn das Gelaende kleine Stufen hat.")]
    [Range(0f, 3f)] public float smoothingSeconds = 0.6f;

    [Tooltip("Auf diesen Bereich begrenzen. Der Tuo kann ohnehin nicht mehr " +
             "als 10 % und die Kalibrierung im BleTrainer deckelt zusaetzlich.")]
    [Range(2f, 20f)] public float maxGradePercent = 10f;

    [Header("Ausgabe")]
    [Tooltip("Aus, wenn ihr die Steigung nur anzeigen und noch nicht an den " +
             "Trainer schicken wollt.")]
    public bool applyToTrainer = true;

    // ---------- oeffentlich abfragbar ----------

    /// <summary>Geglaettete Steigung in Prozent. Positiv = bergauf.</summary>
    public float GradePercent { get; private set; }

    /// <summary>Ungeglaetteter Rohwert der letzten Messung, in Prozent.</summary>
    public float RawGradePercent { get; private set; }

    // ---------- intern ----------
    private ITrainer _trainer;
    private Vector3 _letzterMesspunkt;
    private bool _gestartet;

    private void Start()
    {
        _trainer = trainerSource as ITrainer;
        if (_trainer == null && trainerSource != null)
            Debug.LogWarning("[RouteGrade] trainerSource implementiert kein ITrainer.");

        if (rider == null)
        {
            Debug.LogError("[RouteGrade] Kein 'rider' zugewiesen - es gibt nichts zu messen.");
            enabled = false;
            return;
        }

        _letzterMesspunkt = rider.position;
        _gestartet = true;
    }

    private void Update()
    {
        if (!_gestartet) return;

        Vector3 jetzt = rider.position;

        // Horizontale Strecke seit dem letzten Messpunkt. Die Hoehe (y) wird
        // dabei ignoriert - deshalb nur x und z.
        Vector3 flach = jetzt - _letzterMesspunkt;
        flach.y = 0f;
        float horizontal = flach.magnitude;

        // Erst messen, wenn genug Strecke zusammengekommen ist. Sonst teilt man
        // durch fast null und bekommt Unsinn.
        if (horizontal >= sampleDistanceMeters)
        {
            float hoehendifferenz = jetzt.y - _letzterMesspunkt.y;

            RawGradePercent = Mathf.Clamp(
                hoehendifferenz / horizontal * 100f,
                -maxGradePercent, maxGradePercent);

            _letzterMesspunkt = jetzt;
        }

        // Glaetten: sich schrittweise an den Rohwert herantasten, statt
        // sofort zu springen. Bei smoothingSeconds = 0 wird direkt uebernommen.
        GradePercent = smoothingSeconds > 0.01f
            ? Mathf.Lerp(GradePercent, RawGradePercent, Time.deltaTime / smoothingSeconds)
            : RawGradePercent;

        // An den Trainer. SetGrade darf beliebig oft aufgerufen werden -
        // der BleTrainer drosselt intern selbst auf 1 Hz.
        if (applyToTrainer) _trainer?.SetGrade(GradePercent);
    }

    /// <summary>
    /// Steigung einer bekannten Strecke, ohne zu fahren. Fuer vorgeplante
    /// Segmente statt gemessener Bewegung.
    ///
    ///     1200 m Strecke, 84 Hoehenmeter  ->  84 / 1200 * 100  =  7.0 %
    /// </summary>
    public static float GradeOfSegment(float distanceMeters, float elevationMeters)
        => distanceMeters > 0.01f ? elevationMeters / distanceMeters * 100f : 0f;

    /// <summary>
    /// Setzt die Steigung direkt, ohne Messung - z.B. fuer ein Streckenprofil
    /// aus einer Datei. Dafuer "applyToTrainer" ausschalten und stattdessen
    /// diese Methode aufrufen.
    /// </summary>
    public void SetGradeDirectly(float gradePercent)
    {
        GradePercent = Mathf.Clamp(gradePercent, -maxGradePercent, maxGradePercent);
        RawGradePercent = GradePercent;
        _trainer?.SetGrade(GradePercent);
    }
}
