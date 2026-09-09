using UnityEngine;


public class Fortbewegen : MonoBehaviour
{
    [Header("Fahren")]
    [Tooltip("Wie schnell die Geschwindigkeit dem Trainer folgt.\n" +
             "Der Tuo meldet nur 1x pro Sekunde - ungefiltert wuerde das Rad im\n" +
             "Sekundentakt ruckeln, was in VR uebel wird.\n" +
             "Zeitkonstante = 1/glaettung Sekunden. 2 = eine halbe Sekunde.\n" +
             "Kleiner = traeger und glatter, groesser = direkter und ruckliger.")]
    [Range(0.5f, 6f)] public float glaettung = 2f;

   


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
             "Die Drehung fasst dieses Skript nie an, nur die Hoehe.")]
    public bool aufBodenSetzen = true;

    // ANZEIGEzum Zuschauen
    [Header("Anzeige (nur lesen)")]
    [SerializeField] float tempoKmh;        // was der Tuo meldet, springt im 1-Hz-Takt
    [SerializeField] float tempoMs;         // geglaettet, damit wird wirklich gefahren
    [SerializeField] float neigungGrad;     // + bergauf, - bergab
    [SerializeField] bool  bodenGefunden;   // false = Strahl trifft nichts

    Trainer    _t;
    Bremskraft _b;

   
    float _v;

    void Update()
    {
        // Trainer und Bremskraft legen sich zum Teil erst beim Play an, darum
        // hier nachfassen und nicht in Start().
        if (_t == null) { _t = FindFirstObjectByType<Trainer>(); return; }
        if (_b == null) _b = FindFirstObjectByType<Bremskraft>();

        // ¨ziel_velocity
        float ziel = _t.Speed / 3.6f;

        // gltten, gegen ruckartiges speed verschieben
        _v = Mathf.Lerp(_v, ziel, Time.deltaTime * glaettung);

        // vooorwäärts
        transform.position += transform.forward * _v * Time.deltaTime;

        //boden suchen
        Vector3 start = transform.position + Vector3.up * strahlStartHoehe;

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
            neigungGrad = Mathf.Asin(Mathf.Clamp(hang.y, -1f, 1f)) * Mathf.Rad2Deg;

            // paket an Bremskraft skript
            if (_b != null) _b.SetzeNeigung(neigungGrad);

            // auf oberfläceh setzen
            if (aufBodenSetzen)
            {
                Vector3 p = transform.position;
                p.y = hit.point.y;
                transform.position = p;
            }
        }
        else
        {
           
            bodenGefunden = false;
        }

        // anzeige
        tempoKmh = _t.Speed;
        tempoMs  = _v;
    }
}
