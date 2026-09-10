using UnityEngine;

// ===========================================================================
// GEAENDERT beim Umzug aufs XR Origin (Stand vorher: Commit d6f5b74)
//
// 1. Der Boden wird unter dem KOPF abgetastet (neues Feld "kopf"), nicht unter
//    dem Rig-Ursprung. Der Fahrer steht selten genau darauf - sonst werden
//    Hoehe und Steigung an der falschen Stelle des Terrains gemessen.
// 2. BUGFIX: trifft der Strahl nichts, faehrt die Neigung jetzt auf 0 zurueck.
//    Vorher passierte im else-Zweig gar nichts: Bremskraft behielt den letzten
//    Wert fuer immer und der Tuo bremste weiter, obwohl gar kein Boden da war.
// 3. Die Steigung wird gemittelt, bevor sie an Bremskraft geht (neuer Regler
//    "neigungsGlaettung"). Sie kommt aus EINEM Raycast pro Frame und springt
//    auf Terrain-Details; der Tuo funkt nur 1x pro Sekunde und griff sich
//    bisher genau den Wert, der zufaellig gerade anlag.
// 4. Die Hoehe wird geglaettet aufgesetzt (neuer Regler "hoehenGlaettung").
//    Die harte Zuweisung liess die Kamera bei jeder Bodenwelle senkrecht
//    mitruckeln - eine der haeufigsten Uebelkeitsursachen in VR. 0 = wie frueher.
// 5. Glaettung ist jetzt bildratenunabhaengig (1-e^-kt statt k*dt), damit
//    dieselbe Einstellung im Editor (60 fps) und im Headset (90/120 fps)
//    gleich faehrt.
// 6. FindFirstObjectByType laeuft nur noch alle 0.5 s. Vorher lief die Suche
//    nach Bremskraft in JEDEM Frame durch die ganze Szene.
//
// ACHTUNG Szene: im XR Rig unter "Locomotion" muessen Move, Turn, Gravity und
// Jump abgeschaltet werden. Gravity schreibt sonst gegen aufBodenSetzen
// dieselbe Hoehe, Turn gegen Lenken, Move gegen dieses Skript.
//
// NOCH OFFEN: Trainer.Speed behaelt bei einem BLE-Abbruch den zuletzt
// empfangenen Wert - man faehrt dann endlos weiter. Der Watchdog dafuer gehoert
// nach Trainer.cs, und die Datei war hier ausdruecklich nicht dran.
// ===========================================================================

/// <summary>
/// Bewegt den Fahrer mit dem Tempo, das der Tuo meldet, und setzt ihn auf den
/// Boden. Schreibt NUR die Position, nie die Rotation - die kommt von Lenken.
///
/// Auf dasselbe GameObject wie Lenken ziehen. Beim VR-Aufbau ist das das
/// XR ORIGIN. Gefahren wird in transform.forward des Rigs, NICHT in
/// Blickrichtung der Kamera: den Kopf zu drehen soll die Fahrtrichtung nicht
/// aendern, genau wie auf einem echten Fahrrad.
/// </summary>
public class Fortbewegen : MonoBehaviour
{
    [Header("Fahren")]
    [Tooltip("Wie schnell die Geschwindigkeit dem Trainer folgt.\n" +
             "Der Tuo meldet nur 1x pro Sekunde - ungefiltert wuerde das Rad im\n" +
             "Sekundentakt ruckeln, was in VR uebel wird.\n" +
             "Zeitkonstante = 1/glaettung Sekunden. 2 = eine halbe Sekunde.\n" +
             "Kleiner = traeger und glatter, groesser = direkter und ruckliger.")]
    [Range(0.5f, 6f)] public float glaettung = 2f;

    [Tooltip("Die Kamera des Fahrers (Main Camera im XR Rig).\n" +
             "Leer lassen: wird unter diesem Objekt automatisch gesucht.\n" +
             "Wozu: der Boden wird UNTER DEM FAHRER abgetastet, nicht unter dem\n" +
             "Rig-Ursprung. Der Fahrer steht selten genau auf diesem Ursprung -\n" +
             "sonst misst man Hoehe und Steigung an der falschen Stelle.")]
    public Transform kopf;

    [Header("Boden")]
    [Tooltip("Auf welche Layer der Raycast reagiert.\n" +
             "UNBEDINGT auf den Terrain-Layer stellen. Steht hier Everything,\n" +
             "trifft der Strahl womöglich das Fahrrad selbst und die Steigung\n" +
             "ist dauerhaft 0.")]
    public LayerMask bodenLayer = ~0;

    [Tooltip("Wie weit ueber dem Fahrrad der Strahl startet, in Metern.\n" +
             "Startet er genau im Ursprung, liegt er bergab schon unter dem\n" +
             "Boden und trifft nichts mehr.")]
    public float strahlStartHoehe = 1f;

    [Tooltip("Maximale Laenge des Strahls in Metern.")]
    public float strahlLaenge = 5f;

    [Tooltip("Setzt das Fahrrad auf die Bodenhoehe.\n" +
             "Ausschalten, falls ein anderes Skript das Aufsetzen uebernimmt -\n" +
             "zwei Skripte, die dieselbe Hoehe schreiben, zappeln gegeneinander.\n" +
             "ACHTUNG: der Gravity Provider im XR Rig ist genau so ein Skript.\n" +
             "Entweder den abschalten oder dieses Haekchen wegnehmen.\n" +
             "Die Drehung fasst dieses Skript nie an, nur die Hoehe.")]
    public bool aufBodenSetzen = true;

    [Tooltip("Wie schnell die Hoehe dem Boden folgt.\n" +
             "0 = hart aufsetzen wie frueher. Auf welligem Terrain ruckelt die\n" +
             "Kamera dann bei jeder Bodenwelle senkrecht mit - eine der\n" +
             "haeufigsten Ursachen fuer Uebelkeit in VR.\n" +
             "8 entspricht etwa 0.12 s und buegelt das weg, ohne dass man in\n" +
             "Huegeln versinkt.")]
    [Range(0f, 20f)] public float hoehenGlaettung = 8f;

    [Header("Steigung an den Tuo")]
    [Tooltip("Wie stark die Steigung gemittelt wird, bevor sie an Bremskraft geht.\n" +
             "Grund: die Steigung kommt aus EINEM Raycast pro Frame und springt\n" +
             "auf Terrain-Details herum. Der Tuo funkt aber nur 1x pro Sekunde\n" +
             "und greift sich dabei den Wert, der zufaellig gerade anliegt -\n" +
             "der Widerstand fuehlt sich dadurch beliebig an.\n" +
             "Zeitkonstante = 1/Wert Sekunden. 1.5 = 0.67 s, also ungefaehr das\n" +
             "Sendeintervall. Kleiner = ruhiger, aber traeger an Kuppen.")]
    [Range(0.2f, 6f)] public float neigungsGlaettung = 1.5f;

    // ANZEIGEzum Zuschauen
    [Header("Anzeige (nur lesen)")]
    [SerializeField] float tempoKmh;        // was der Tuo meldet, springt im 1-Hz-Takt
    [SerializeField] float tempoMs;         // geglaettet, damit wird wirklich gefahren
    [SerializeField] float neigungRoh;      // was der Raycast in diesem Frame sagt
    [SerializeField] float neigungGrad;     // gemittelt, das geht an den Tuo
    [SerializeField] bool  bodenGefunden;   // false = Strahl trifft nichts

    Trainer    _t;
    Bremskraft _b;
    float      _naechsteSuche;   // drosselt die Suche nach Trainer und Bremskraft

    float _v;
    float _neigung;              // der gemittelte Wert ueber Frames hinweg

    void Awake()
    {
        // Kamera einmal suchen, nicht jeden Frame. GetComponentInChildren sucht
        // nur nach unten: im XR Rig findet es die Main Camera, im Sphere-Test
        // findet es nichts und wir tasten wie frueher unter dem Ursprung ab.
        if (kopf == null)
        {
            var c = GetComponentInChildren<Camera>();
            if (c != null) kopf = c.transform;
        }

        // Der Kopf muss unter diesem Objekt haengen, sonst wird der Boden an
        // einer voellig fremden Stelle der Welt abgetastet.
        if (kopf != null && !kopf.IsChildOf(transform))
        {
            Debug.LogWarning("[Fortbewegen] '" + kopf.name + "' haengt nicht unter '" +
                             name + "'. Taste den Boden unter dem Ursprung ab.", this);
            kopf = null;
        }
    }

    void Update()
    {
        // Trainer und Bremskraft legen sich zum Teil erst beim Play an, darum
        // hier nachfassen und nicht in Start(). Aber gedrosselt: die Suche geht
        // durch die ganze Szene und darf nicht jeden Frame laufen.
        if ((_t == null || _b == null) && Time.time >= _naechsteSuche)
        {
            _naechsteSuche = Time.time + 0.5f;
            if (_t == null) _t = FindFirstObjectByType<Trainer>();
            if (_b == null) _b = FindFirstObjectByType<Bremskraft>();
        }
        if (_t == null) return;

        // ¨ziel_velocity
        float ziel = _t.Speed / 3.6f;

        // glätten, gegen ruckartiges speed verschieben.
        // 1 - e^(-k*dt) statt k*dt, damit die Einstellung bei 60, 90 und 120 fps
        // dasselbe Fahrgefuehl ergibt.
        _v = Mathf.Lerp(_v, ziel, 1f - Mathf.Exp(-glaettung * Time.deltaTime));

        // vooorwäärts
        transform.position += transform.forward * _v * Time.deltaTime;

        // boden suchen - unter dem FAHRER, nicht unter dem Rig-Ursprung
        Vector3 abtastPunkt = transform.position;
        if (kopf != null)
        {
            abtastPunkt.x = kopf.position.x;
            abtastPunkt.z = kopf.position.z;
        }
        Vector3 start = abtastPunkt + Vector3.up * strahlStartHoehe;

        // Faktor fuer die Mittelung der Steigung, siehe neigungsGlaettung.
        float nk = 1f - Mathf.Exp(-neigungsGlaettung * Time.deltaTime);

        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit,
                            strahlLaenge + strahlStartHoehe, bodenLayer))
        {
            bodenGefunden = true;

            // ---- 5. Steigung MIT Vorzeichen --------------------------------
            // Vector3.Angle(normal, up) waere falsch: das gibt immer 0..180,
            // also fuer +5 Grad und -5 Grad denselben Wert. Der Tuo wuerde
            // bergab genauso bremsen wie bergauf.
            //
            // Stattdessen: die Blickrichtung flach auf die Hangflaeche legen.
            // Deren y-Komponente ist der Sinus der Steigung IN FAHRTRICHTUNG -
            // positiv bergauf, negativ bergab. Genau was Bremskraft braucht.
            Vector3 hang = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
            neigungRoh = Mathf.Asin(Mathf.Clamp(hang.y, -1f, 1f)) * Mathf.Rad2Deg;

            // gemittelt, damit der Tuo bei seinem Sekunden-Tick keinen
            // zufaelligen Ausreisser erwischt
            _neigung = Mathf.Lerp(_neigung, neigungRoh, nk);

            // auf oberfläceh setzen
            if (aufBodenSetzen)
            {
                Vector3 p = transform.position;
                p.y = hoehenGlaettung <= 0f
                    ? hit.point.y
                    : Mathf.Lerp(p.y, hit.point.y, 1f - Mathf.Exp(-hoehenGlaettung * Time.deltaTime));
                transform.position = p;
            }
        }
        else
        {
            bodenGefunden = false;
            neigungRoh = 0f;

            // WICHTIG: hier muss die Neigung wirklich zurueckgefahren werden.
            // Frueher passierte in diesem Zweig gar nichts - Bremskraft behielt
            // den letzten Wert und der Tuo bremste ewig weiter, obwohl unter dem
            // Fahrer gar kein Boden mehr war.
            _neigung = Mathf.Lerp(_neigung, 0f, nk);
        }

        // paket an Bremskraft skript
        if (_b != null) _b.SetzeNeigung(_neigung);

        // anzeige
        neigungGrad = _neigung;
        tempoKmh    = _t.Speed;
        tempoMs     = _v;
    }
}
