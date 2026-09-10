using UnityEngine;

/// <summary>
/// Lenkung ueber die zwei Controller am Lenker - nach dem Vorbild eines echten
/// Fahrrads: der Einschlag ist keine Richtung, sondern eine DREHGESCHWINDIGKEIT.
///
/// Lenker eingeschlagen halten  -> das Rad dreht sich weiter und weiter
/// Lenker in der Mitte          -> geradeaus, in die zuletzt erreichte Richtung
///
/// Auf dasselbe GameObject wie Fortbewegen ziehen (das Fahrrad).
/// Dieses Skript schreibt NUR die Rotation, Fortbewegen nur die Position.
///
/// Beim ersten Frame wird die aktuelle Lenkerstellung als "geradeaus" gemerkt.
/// Darum ist es egal, wie der Trainer im Zimmer steht - halte den Lenker beim
/// Start einfach gerade. Spaeter jederzeit neu: Rechtsklick auf den Kopf dieser
/// Komponente im Inspector -> "Neu kalibrieren".
/// </summary>
[DefaultExecutionOrder(-100)]
public class Lenken : MonoBehaviour
{
    // =====================================================================
    // DIE ZWEI CONTROLLER
    // =====================================================================
    [Header("Controller am Lenker")]
    [Tooltip("Der Controller am linken Lenkergriff.")]
    public Transform controllerLinks;

    [Tooltip("Der Controller am rechten Lenkergriff.")]
    public Transform controllerRechts;

    // =====================================================================
    // LENKGEFUEHL
    // =====================================================================
    [Header("Lenkgefuehl")]
    [Tooltip("Wie viel Grad pro Sekunde sich das Rad dreht, pro Grad Einschlag.\n" +
             "2 bedeutet: Lenker 30 Grad eingeschlagen -> 60 Grad Drehung pro Sekunde,\n" +
             "also eine Viertelkurve in gut anderthalb Sekunden.\n" +
             "Groesser = agiler, kleiner = traeger.")]
    [Range(0.2f, 6f)] public float empfindlichkeit = 2f;

    [Tooltip("Obergrenze in Grad pro Sekunde. Verhindert, dass ein voll\n" +
             "eingeschlagener Lenker das Rad herumreisst - in VR wird das\n" +
             "sonst schnell unangenehm.")]
    [Range(15f, 270f)] public float maxDrehrate = 90f;

    [Tooltip("Einschlag unter diesem Wert wird ignoriert, in Grad.\n" +
             "Controller zittern immer etwas, und niemand haelt einen Lenker\n" +
             "exakt gerade. Ohne Totzone driftet das Rad langsam vor sich hin.")]
    [Range(0f, 15f)] public float totzone = 3f;

    [Tooltip("Wie schnell der gemessene Einschlag dem Lenker folgt.\n" +
             "Glaettet das Zittern der Controller. Zeitkonstante = 1/Wert Sekunden,\n" +
             "genau wie bei Fortbewegen.")]
    [Range(1f, 20f)] public float glaettung = 8f;

    [Tooltip("Anhaken, falls die Lenkung seitenverkehrt reagiert.")]
    public bool lenkungInvertieren = false;

    [Tooltip("Lenken nur, wenn auch getreten wird - wie im echten Leben, wo ein\n" +
             "stehendes Rad nicht lenkbar ist. Zum Testen ohne Trainer AUS lassen,\n" +
             "sonst bewegt sich gar nichts.")]
    public bool nurBeimFahren = false;

    // =====================================================================
    // ANZEIGE - nur zum Zuschauen
    // =====================================================================
    [Header("Anzeige (nur lesen)")]
    [SerializeField] float stangenLaenge;   // Abstand der Controller, sollte ~0.5 m sein
    [SerializeField] float lenkwinkel;      // Einschlag gegenueber der Nullstellung
    [SerializeField] float drehrate;        // was daraus wird, in Grad pro Sekunde
    [SerializeField] float blickrichtung;   // Kompassrichtung des Fahrrads
    [SerializeField] bool  kalibriert;      // false = Nullstellung noch nicht gemerkt

    Trainer _t;

    // Die Nullstellung: die flache Lenkerrichtung, die "geradeaus" bedeutet.
    // Muss auf Klassenebene stehen, sonst waere sie jeden Frame wieder weg.
    Vector3 _referenz;

    // Der geglaettete Einschlag - ebenfalls ueber Frames hinweg gebraucht.
    float _winkel;

    /// <summary>Merkt sich die aktuelle Lenkerstellung als "geradeaus".
    /// Im Inspector per Rechtsklick auf den Komponentenkopf aufrufbar,
    /// oder aus einem anderen Skript, z.B. per Knopfdruck im Spiel.</summary>
    [ContextMenu("Neu kalibrieren")]
    public void Kalibrieren() => kalibriert = false;

    void Update()
    {
        // Ohne beide Controller nichts tun - aber auch nicht abstuerzen.
        if (controllerLinks == null || controllerRechts == null) return;

        // ---- 1. Die Stange ---------------------------------------------------
        // Punkt minus Punkt ergibt den Vektor von links nach rechts, also
        // entlang des Lenkers.
        Vector3 stange = controllerRechts.position - controllerLinks.position;
        stangenLaenge = stange.magnitude;

        // ---- 2. Flach legen --------------------------------------------------
        // Eine Hand hoeher als die andere soll nicht lenken, darum die Hoehe
        // rausrechnen.
        Vector3 flach = Vector3.ProjectOnPlane(stange, Vector3.up);

        // Liegen die Controller uebereinander, hat der flache Vektor keine
        // Richtung mehr. Dann lieber gar nichts tun als in NaN zu laufen.
        if (flach.sqrMagnitude < 0.0001f) return;
        flach.Normalize();

        // ---- 3. Nullstellung -------------------------------------------------
        // Beim ersten brauchbaren Frame merken wir uns, wo der Lenker steht.
        // Ab da ist alles ein Vergleich dagegen - die Ausrichtung des Trainers
        // im Zimmer spielt keine Rolle mehr.
        if (!kalibriert)
        {
            _referenz = flach;
            _winkel = 0f;
            kalibriert = true;
        }

        // ---- 4. Einschlag messen --------------------------------------------
        // SignedAngle gibt den Winkel MIT Vorzeichen um die Hochachse. Genau
        // das brauchen wir: links und rechts muessen sich unterscheiden.
        // (Vector3.Angle koennte das nicht, das ist immer positiv.)
        float roh = Vector3.SignedAngle(_referenz, flach, Vector3.up);

        // Glaetten, sonst zittert die Lenkung mit dem Tracking mit.
        _winkel = Mathf.Lerp(_winkel, roh, Time.deltaTime * glaettung);
        lenkwinkel = _winkel;

        // ---- 5. Totzone ------------------------------------------------------
        // Nicht einfach auf 0 setzen, sondern den Betrag um die Totzone
        // VERKLEINERN. Sonst gaebe es an der Schwelle einen Sprung von 0 auf
        // die volle Drehrate.
        float eingeschlagen = Mathf.Sign(_winkel) * Mathf.Max(0f, Mathf.Abs(_winkel) - totzone);

        // ---- 6. Aus Einschlag wird Drehgeschwindigkeit -----------------------
        // Das ist der Kern des Ganzen: der Winkel bestimmt nicht, WOHIN das Rad
        // zeigt, sondern WIE SCHNELL es sich dreht. Deshalb faehrt man Kurven,
        // statt schraeg zu stehen.
        drehrate = Mathf.Clamp(eingeschlagen * empfindlichkeit, -maxDrehrate, maxDrehrate);
        if (lenkungInvertieren) drehrate = -drehrate;

        // Optional: bei Stillstand nicht lenken koennen.
        if (nurBeimFahren)
        {
            if (_t == null) _t = FindFirstObjectByType<Trainer>();
            // Unter 5 km/h faehrt die Lenkwirkung sanft auf 0 herunter.
            if (_t != null) drehrate *= Mathf.Clamp01(_t.Speed / 5f);
        }

        // ---- 7. Drehen -------------------------------------------------------
        // Grad pro Sekunde mal Sekunden dieses Frames ergibt Grad in diesem
        // Frame. Um die WELT-Hochachse, damit das Rad nicht kippt.
        transform.Rotate(0f, drehrate * Time.deltaTime, 0f, Space.World);

        blickrichtung = transform.eulerAngles.y;
    }
}
