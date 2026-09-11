using UnityEngine;

// ===========================================================================
// GEAENDERT beim Umzug aufs XR Origin (Stand vorher: Commit d6f5b74)
//
// 1. Der Einschlag wird LOKAL gemessen statt in Weltkoordinaten. Im XR Rig sind
//    die Controller Kinder des XR Origin. Wer in Weltkoordinaten misst und
//    gleichzeitig das Origin dreht, misst seine eigene Drehung wieder mit -
//    das schaukelt sich in einer halben Sekunde bis zum Anschlag auf und
//    kreiselt dann weiter, auch wenn der Lenker laengst gerade steht.
//    Siehe InverseTransformDirection unten in Schritt 2.
// 2. Gedreht wird um den KOPF (neues Feld "kopf"), nicht um den Rig-Ursprung.
//    Der Fahrer steht nie genau auf dem Ursprung und wurde vorher bei jeder
//    Kurve seitlich auf einer Kreisbahn herumgeschwenkt.
//    Feld leer = Kamera darunter wird beim Start selbst gesucht. Keine
//    gefunden (z.B. im Sphere-Test) = wie frueher um den eigenen Ursprung.
// 3. Glaettung ist jetzt bildratenunabhaengig (1-e^-kt statt k*dt) und steht
//    auf 20 statt 8, also deutlich direkter. Die Controller liefern mit
//    90-120 Hz - der 1-Sekunden-Takt betrifft NUR den Tuo, nicht die Lenkung.
// 4. Kalibriert wird erst bei plausiblem Controller-Abstand (0.25-1.0 m).
//    Vorher konnte beim ersten Frame eine Nullstellung aus noch gar nicht
//    getrackten Controllern haengenbleiben - fuer den Rest der Sitzung falsch.
// 5. FindFirstObjectByType laeuft nur noch alle 0.5 s statt in jedem Frame.
// ===========================================================================

/// <summary>
/// Lenkung ueber die zwei Controller am Lenker - nach dem Vorbild eines echten
/// Fahrrads: der Einschlag ist keine Richtung, sondern eine DREHGESCHWINDIGKEIT.
///
/// Lenker eingeschlagen halten  -> das Rad dreht sich weiter und weiter
/// Lenker in der Mitte          -> geradeaus, in die zuletzt erreichte Richtung
///
/// Auf dasselbe GameObject wie Fortbewegen ziehen. Das ist beim VR-Aufbau das
/// XR ORIGIN, denn nur dann wird der Fahrer selbst mitgedreht und mitbewegt.
/// Dieses Skript kuemmert sich um die Drehung, Fortbewegen um das Vorwaerts.
/// (Genau genommen verschiebt die Drehung um den Kopf das Rig auch ein wenig -
/// aber nur waagerecht und nur so, dass der Kopf stehen bleibt. Die Hoehe
/// fasst nur Fortbewegen an, da kommen sich die beiden nicht in die Quere.)
///
/// WICHTIG (und der Grund fuer die lokale Messung weiter unten): im XR Rig sind
/// die Controller KINDER des XR Origin. Wer den Einschlag in Weltkoordinaten
/// misst und gleichzeitig das Origin dreht, misst seine eigene Drehung wieder
/// mit - das schaukelt sich in einer halben Sekunde bis zum Anschlag auf.
/// Darum wird hier im LOKALEN System des Rigs gemessen: dort steht der Lenker
/// still, solange der Fahrer ihn stillhaelt, egal wie weit sich das Rig dreht.
///
/// Beim ersten brauchbaren Frame wird die aktuelle Lenkerstellung als
/// "geradeaus" gemerkt. Darum ist es egal, wie der Trainer im Zimmer steht -
/// halte den Lenker beim Start einfach gerade. Spaeter jederzeit neu:
/// Rechtsklick auf den Kopf dieser Komponente im Inspector -> "Neu kalibrieren".
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

    [Tooltip("Die Kamera des Fahrers (Main Camera im XR Rig).\n" +
             "Leer lassen: wird unter diesem Objekt automatisch gesucht.\n" +
             "Wozu: gedreht wird um den KOPF des Fahrers, nicht um den Ursprung\n" +
             "des Rigs. Der Fahrer steht nie genau auf dem Rig-Ursprung, und eine\n" +
             "Drehung um diesen Punkt wuerde ihn bei jeder Kurve seitlich auf\n" +
             "einer Kreisbahn herumschwenken - in VR sehr unangenehm.\n" +
             "Wird keine Kamera gefunden (z.B. im Sphere-Test), wird wie frueher\n" +
             "um den eigenen Ursprung gedreht.")]
    public Transform kopf;

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
             "ACHTUNG, das hat NICHTS mit dem 1-Sekunden-Takt des Tuo zu tun:\n" +
             "die Controller liefern mit voller Bildrate (90-120 Hz). Hier geht\n" +
             "es nur darum, das Zittern des Trackings wegzufiltern.\n" +
             "Zeitkonstante = 1/Wert Sekunden. 20 = 0.05 s, praktisch direkt.\n" +
             "Zu klein gewaehlt fuehlt sich die Lenkung gummiartig an.")]
    [Range(1f, 40f)] public float glaettung = 20f;

    [Tooltip("Anhaken, falls die Lenkung seitenverkehrt reagiert.")]
    public bool lenkungInvertieren = false;

    [Tooltip("Lenken nur, wenn auch getreten wird - wie im echten Leben, wo ein\n" +
             "stehendes Rad nicht lenkbar ist. Zum Testen ohne Trainer AUS lassen,\n" +
             "sonst bewegt sich gar nichts.")]
    public bool nurBeimFahren = false;

    // =====================================================================
    // KALIBRIERUNG
    // =====================================================================
    [Header("Kalibrierung")]
    [Tooltip("Plausibler Abstand der beiden Controller in Metern.\n" +
             "Beim Start sind die Controller oft noch nicht getrackt und liegen\n" +
             "beide im Ursprung oder in einer Default-Pose. Wuerde in diesem\n" +
             "Moment kalibriert, waere die Nullstellung fuer den Rest der Sitzung\n" +
             "Muell. Darum wird erst kalibriert, wenn der Abstand in diesen\n" +
             "Bereich faellt - also wirklich zwei Haende am Lenker liegen.")]
    public float minStangenLaenge = 0.25f;
    public float maxStangenLaenge = 1.0f;

    // =====================================================================
    // ANZEIGE - nur zum Zuschauen
    // =====================================================================
    [Header("Anzeige (nur lesen)")]
    [SerializeField] float stangenLaenge;   // Abstand der Controller, sollte ~0.5 m sein
    [SerializeField] float lenkwinkel;      // Einschlag gegenueber der Nullstellung
    [SerializeField] float drehrate;        // was daraus wird, in Grad pro Sekunde
    [SerializeField] float blickrichtung;   // Kompassrichtung des Fahrrads
    [SerializeField] bool  kalibriert;      // false = Nullstellung noch nicht gemerkt
    [SerializeField] bool  drehtUmKopf;     // false = dreht um den eigenen Ursprung

    Trainer _t;
    float   _naechsteSuche;   // drosselt die Suche nach dem Trainer

    // Die Nullstellung: die flache Lenkerrichtung, die "geradeaus" bedeutet.
    // Sie steht im LOKALEN System dieses Objekts, dreht sich also automatisch
    // mit, wenn das Rig sich dreht. Genau das verhindert die Rueckkopplung.
    Vector3 _referenz;

    // Der geglaettete Einschlag - ueber Frames hinweg gebraucht.
    float _winkel;

    /// <summary>Merkt sich die aktuelle Lenkerstellung als "geradeaus".
    /// Im Inspector per Rechtsklick auf den Komponentenkopf aufrufbar,
    /// oder aus einem anderen Skript, z.B. per Knopfdruck im Spiel.</summary>
    [ContextMenu("Neu kalibrieren")]
    public void Kalibrieren() => kalibriert = false;

    void Awake()
    {
        // Kamera einmal suchen, nicht jeden Frame. GetComponentInChildren sucht
        // nur NACH UNTEN - im XR Rig findet es die Main Camera, im Sphere-Test
        // findet es nichts und wir drehen wie frueher um den eigenen Ursprung.
        if (kopf == null)
        {
            var c = GetComponentInChildren<Camera>();
            if (c != null) kopf = c.transform;
        }

        // Der Kopf MUSS unter diesem Objekt haengen. Eine fremde Kamera als
        // Drehpunkt wuerde das ganze Rig auf einer Kreisbahn um sie herum
        // schleudern - lieber vorher meckern und auf den Ursprung zurueckfallen.
        if (kopf != null && !kopf.IsChildOf(transform))
        {
            Debug.LogWarning("[Lenken] '" + kopf.name + "' haengt nicht unter '" +
                             name + "'. Als Drehpunkt unbrauchbar, ignoriere ihn.", this);
            kopf = null;
        }
    }

    void Update()
    {
        // Ohne beide Controller nichts tun - aber auch nicht abstuerzen.
        if (controllerLinks == null || controllerRechts == null) return;

        // ---- 1. Die Stange ---------------------------------------------------
        // Punkt minus Punkt ergibt den Vektor von links nach rechts, also
        // entlang des Lenkers.
        Vector3 stangeWelt = controllerRechts.position - controllerLinks.position;
        stangenLaenge = stangeWelt.magnitude;

        // ---- 2. Ins lokale System des Rigs holen -----------------------------
        // DAS ist die entscheidende Zeile. InverseTransformDirection rechnet den
        // Weltvektor in das Koordinatensystem dieses Objekts um. Dreht sich das
        // Rig, drehen die Controller als Kinder mit - im lokalen System bleibt
        // die Stange dabei exakt stehen. Ohne diesen Schritt wuerde jede Drehung
        // sich selbst weiter antreiben, bis das Rig unkontrolliert kreiselt.
        Vector3 stangeLokal = transform.InverseTransformDirection(stangeWelt);

        // ---- 3. Flach legen --------------------------------------------------
        // Eine Hand hoeher als die andere soll nicht lenken, darum die Hoehe
        // rausrechnen.
        Vector3 flach = Vector3.ProjectOnPlane(stangeLokal, Vector3.up);

        // Liegen die Controller uebereinander, hat der flache Vektor keine
        // Richtung mehr. Dann lieber gar nichts tun als in NaN zu laufen.
        if (flach.sqrMagnitude < 0.0001f) return;
        flach.Normalize();

        // ---- 4. Nullstellung -------------------------------------------------
        // Erst kalibrieren, wenn der Abstand plausibel ist. Sonst merken wir uns
        // beim allerersten Frame eine Stellung aus noch nicht getrackten
        // Controllern - und die bliebe fuer den Rest der Sitzung falsch.
        if (!kalibriert)
        {
            if (stangenLaenge < minStangenLaenge || stangenLaenge > maxStangenLaenge)
            {
                drehrate = 0f;
                return;
            }
            _referenz = flach;
            _winkel = 0f;
            kalibriert = true;
        }

        // ---- 5. Einschlag messen --------------------------------------------
        // SignedAngle gibt den Winkel MIT Vorzeichen um die Hochachse. Genau
        // das brauchen wir: links und rechts muessen sich unterscheiden.
        // (Vector3.Angle koennte das nicht, das ist immer positiv.)
        float roh = Vector3.SignedAngle(_referenz, flach, Vector3.up);

        // Glaetten, sonst zittert die Lenkung mit dem Tracking mit.
        // 1 - e^(-k*dt) statt einfach k*dt: nur so ist die Glaettung von der
        // Bildrate unabhaengig. Sonst fuehlt sich dieselbe Einstellung im Editor
        // (60 fps) anders an als im Headset (90 oder 120 fps).
        _winkel = roh;//  Mathf.Lerp(_winkel, roh, 1f - Mathf.Exp(-glaettung * Time.deltaTime));
        lenkwinkel = roh;

        // ---- 6. Totzone ------------------------------------------------------
        // Nicht einfach auf 0 setzen, sondern den Betrag um die Totzone
        // VERKLEINERN. Sonst gaebe es an der Schwelle einen Sprung von 0 auf
        // die volle Drehrate.
        float eingeschlagen = Mathf.Sign(_winkel) * Mathf.Max(0f, Mathf.Abs(_winkel) - totzone);

        // ---- 7. Aus Einschlag wird Drehgeschwindigkeit -----------------------
        // Das ist der Kern des Ganzen: der Winkel bestimmt nicht, WOHIN das Rad
        // zeigt, sondern WIE SCHNELL es sich dreht. Deshalb faehrt man Kurven,
        // statt schraeg zu stehen.
        drehrate = Mathf.Clamp(eingeschlagen * empfindlichkeit, -maxDrehrate, maxDrehrate);
        if (lenkungInvertieren) drehrate = -drehrate;

        // Optional: bei Stillstand nicht lenken koennen.
        if (nurBeimFahren)
        {
            if (_t == null && Time.time >= _naechsteSuche)
            {
                // Nicht jeden Frame suchen - FindFirstObjectByType geht durch die
                // ganze Szene und kostet bei 90 Hz spuerbar Zeit.
                _naechsteSuche = Time.time + 0.5f;
                _t = FindFirstObjectByType<Trainer>();
            }
            // Unter 5 km/h faehrt die Lenkwirkung sanft auf 0 herunter.
            if (_t != null) drehrate *= Mathf.Clamp01(_t.Speed / 5f);
            else            drehrate = 0f;   // kein Trainer da -> es faehrt niemand
        }

        // ---- 8. Drehen -------------------------------------------------------
        // Grad pro Sekunde mal Sekunden dieses Frames ergibt Grad in diesem
        // Frame. Um die WELT-Hochachse, damit das Rad nicht kippt.
        float winkelDiesesFrame = drehrate * Time.deltaTime;

        if (kopf != null)
            // Um den Kopf des Fahrers drehen. Sonst wird der Fahrer, der ja
            // irgendwo neben dem Rig-Ursprung steht, bei jeder Kurve seitlich
            // auf einer Kreisbahn herumgeschwenkt.
            transform.RotateAround(kopf.position, Vector3.up, winkelDiesesFrame);
        else
            transform.Rotate(0f, winkelDiesesFrame, 0f, Space.World);

        blickrichtung = transform.eulerAngles.y;
        drehtUmKopf   = kopf != null;
    }

    public float GetLenkwinkel()
    {
        return lenkwinkel;
    }
}
