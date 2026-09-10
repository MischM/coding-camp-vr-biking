using UnityEngine;

// ===========================================================================
// Navigation zum Lieferziel - Luftlinie.
//
// Bewusst KEIN Wegenetz, keine Waypoints, kein NavMesh: die Karte ist offenes
// Terrain, ein Weg ist nur aufgemalt. Ein Pfeil auf die Luftlinie braucht
// dagegen kein Setup und zeigt auch dann noch richtig, wenn sich jemand
// voellig verfahren hat.
// ===========================================================================

/// <summary>
/// Richtet das GameObject AUS, AUF DEM DIESES SKRIPT LIEGT: es dreht sich so,
/// dass seine Nase zum Ziel zeigt. Alles, was darunter haengt (das Pfeilmodell),
/// dreht sich mit.
///
/// Aufbau:
///   XR Origin
///     +-- Pfeil          <- dieses Skript drauf, Ziel ins Feld ziehen
///           +-- Modell    (das sichtbare Pfeilmesh)
///
/// Der Pfeil MUSS ein Kind des XR Origin sein, sonst bleibt er stehen,
/// waehrend der Fahrer davonfaehrt.
///
/// Zeigt das Modell in die falsche Richtung, nicht das Mesh drehen, sondern
/// unten "modellKorrektur" benutzen - eine gedrehte Modellachse ist die
/// haeufigste Ursache dafuer, dass der Pfeil "seitlich" zeigt.
/// </summary>
public class Zielpfeil : MonoBehaviour
{
    // =====================================================================
    // DAS ZIEL
    // =====================================================================
    [Header("Ziel")]
    [Tooltip("Wohin der Pfeil zeigen soll.\n" +
             "Einfach das Ziel-GameObject aus der Hierarchie hier hineinziehen\n" +
             "(z.B. das Haus, an das geliefert wird).\n" +
             "Leer = der Pfeil blendet sich aus, siehe ausblendenOhneZiel.\n" +
             "Im Spiel jederzeit umsetzbar per ZielSetzen().")]
    public Transform ziel;

    // =====================================================================
    // VERHALTEN
    // =====================================================================
    [Header("Verhalten")]
    [Tooltip("Wie schnell der Pfeil der Zielrichtung folgt.\n" +
             "Zeitkonstante = 1/Wert Sekunden. 8 = etwa 0.12 s.\n" +
             "Ungeglaettet zittert der Pfeil in jeder Kurve mit dem Tracking mit.\n" +
             "0 = gar nicht glaetten, springt sofort.")]
    [Range(0f, 30f)] public float glaettung = 8f;

    [Tooltip("Nur um die Hochachse drehen.\n" +
             "UNBEDINGT anlassen: sonst kippt der Pfeil an jedem Huegel nach oben\n" +
             "oder unten, weil das Ziel hoeher oder tiefer liegt als der Fahrer.\n" +
             "Ausschalten nur, wenn wirklich in 3D gezeigt werden soll.")]
    public bool nurWaagerecht = true;

    [Tooltip("Ausgleich, falls das Pfeilmodell nicht entlang seiner Z-Achse zeigt.\n" +
             "Unity betrachtet +Z (die blaue Achse) als 'vorne'. Viele Modelle\n" +
             "zeigen aber nach oben oder zur Seite.\n" +
             "Zeigt der Pfeil 90 Grad daneben: hier Y auf 90 oder -90.\n" +
             "Zeigt er genau rueckwaerts: Y auf 180.\n" +
             "Liegt er flach statt aufrecht: X auf 90 oder -90.\n" +
             "Am schnellsten ausprobiert im Play Mode - die Werte bleiben\n" +
             "erhalten, weil sie hier und nicht an der Transform stehen.")]
    public Vector3 modellKorrektur = Vector3.zero;

    [Tooltip("Pfeil verstecken, solange kein Ziel eingetragen ist.\n" +
             "Blendet alle Renderer darunter aus - ohne das zeigt der Pfeil\n" +
             "zwischen zwei Lieferungen irgendwohin.")]
    public bool ausblendenOhneZiel = true;

    // =====================================================================
    // ANZEIGE - nur zum Zuschauen
    // =====================================================================
    [Header("Anzeige (nur lesen)")]
    [SerializeField] float distanzMeter;    // Luftlinie zum Ziel
    [SerializeField] float abweichungGrad;  // wie weit das Ziel neben der Fahrtrichtung liegt
    [SerializeField] bool  hatZiel;

    Renderer[] _renderer;
    bool       _sichtbar = true;

    void Awake()
    {
        // Einmal einsammeln statt in jedem Frame - der Pfeil hat meist nur
        // einen Renderer, aber GetComponentsInChildren geht trotzdem durch
        // den ganzen Teilbaum.
        _renderer = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>Neues Ziel setzen, z.B. fuer die naechste Lieferung.
    /// null uebergeben blendet den Pfeil aus.</summary>
    public void ZielSetzen(Transform neuesZiel) => ziel = neuesZiel;

    /// <summary>Gleiche Sache, aber mit einem GameObject - so laesst es sich
    /// direkt aus einem UnityEvent im Inspector aufrufen.</summary>
    public void ZielSetzen(GameObject neuesZiel)
        => ziel = neuesZiel != null ? neuesZiel.transform : null;

    // LateUpdate, nicht Update: Lenken dreht das XR Origin in Update. Als Kind
    // dreht sich der Pfeil dabei mit. Erst danach seine Weltdrehung zu setzen
    // spart ein Frame Nachzappeln in jeder Kurve.
    void LateUpdate()
    {
        hatZiel = ziel != null;
        if (ausblendenOhneZiel) Sichtbar(hatZiel);
        if (!hatZiel) return;

        // ---- 1. Richtung zum Ziel -------------------------------------------
        Vector3 zumZiel = ziel.position - transform.position;
        distanzMeter = zumZiel.magnitude;

        // Hoehe rausrechnen. Ohne das kippt der Pfeil an Huegeln.
        if (nurWaagerecht) zumZiel.y = 0f;

        // Direkt auf dem Ziel gibt es keine Richtung mehr - dann lieber die
        // letzte behalten als in NaN zu laufen.
        if (zumZiel.sqrMagnitude < 0.0001f) return;
        zumZiel.Normalize();

        // ---- 2. Anzeige: wie weit liegt das Ziel neben der Fahrtrichtung ----
        Transform bezug = transform.parent != null ? transform.parent : transform;
        Vector3 fahrtrichtung = Vector3.ProjectOnPlane(bezug.forward, Vector3.up);
        if (fahrtrichtung.sqrMagnitude > 0.0001f)
            abweichungGrad = Vector3.SignedAngle(fahrtrichtung.normalized, zumZiel, Vector3.up);

        // ---- 3. Drehen -------------------------------------------------------
        // Erst zum Ziel schauen, dann die Modellkorrektur DAHINTER anhaengen:
        // so wirkt sie im lokalen System des Pfeils, dreht also das Modell in
        // sich zurecht statt die Zielrichtung zu verfaelschen.
        Quaternion soll = Quaternion.LookRotation(zumZiel, Vector3.up)
                        * Quaternion.Euler(modellKorrektur);

        // 1 - e^(-k*dt) statt k*dt, damit sich die Einstellung im Editor
        // (60 fps) und im Headset (90/120 fps) gleich anfuehlt.
        transform.rotation = glaettung <= 0f
            ? soll
            : Quaternion.Slerp(transform.rotation, soll,
                               1f - Mathf.Exp(-glaettung * Time.deltaTime));
    }

    void Sichtbar(bool an)
    {
        if (_sichtbar == an || _renderer == null) return;
        _sichtbar = an;
        for (int i = 0; i < _renderer.Length; i++)
            if (_renderer[i] != null) _renderer[i].enabled = an;
    }

    // Im Editor eine Linie zum Ziel zeichnen - dann sieht man sofort, ob das
    // richtige Objekt im Feld steckt.
    void OnDrawGizmosSelected()
    {
        if (ziel == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, ziel.position);
        Gizmos.DrawWireSphere(ziel.position, 1f);
    }
}
