using UnityEngine;

// ===========================================================================
// GEAENDERT beim Umzug aufs XR Origin (Stand vorher: Commit d6f5b74)
//
// 1. Neuer Schalter "neigungVonHand". Der Tooltip versprach schon immer "zum
//    Testen von Hand eintragen", aber sobald Fortbewegen lief, ueberschrieb
//    SetzeNeigung() den Wert in jedem einzelnen Frame wieder. Angehakt wird
//    ignoriert, was Fortbewegen meldet.
// 2. FindFirstObjectByType laeuft nur noch alle 0.5 s statt in jedem Frame.
// ===========================================================================

/// <summary>
/// Rechnet die Steigung der Strecke in einen Widerstand um und schickt ihn an
/// den Tuo. Bekommt die Steigung von Fortbewegen per SetzeNeigung().
///
/// Darf auf dasselbe GameObject wie Lenken und Fortbewegen (das XR Origin),
/// muss aber nicht - die beiden finden sich ueber die Szene.
/// </summary>
public class Bremskraft : MonoBehaviour
{
    [Header("Eingang")]
    [Tooltip("Neigung des Fahrrads in Grad. Bergauf positiv bergab negativ.\n" +
             "Kommt im Spiel per SetzeNeigung() von Fortbewegen.\n" +
             "Zum Testen von Hand: unten neigungVonHand anhaken, sonst wird\n" +
             "dieser Wert sofort wieder ueberschrieben.")]
    public float neigungGrad = 0f;  //hier nachher wert von Fahrrad

    [Tooltip("Anhaken, um oben von Hand eine Steigung einzustellen.\n" +
             "Dann ignoriert Bremskraft, was Fortbewegen meldet - praktisch, um\n" +
             "den Widerstand am Bein zu pruefen, ohne eine Strecke zu suchen.\n" +
             "Fuer den normalen Betrieb wieder wegnehmen.")]
    public bool neigungVonHand = false;

    [Header("Verzerrungsteiler")]
    [Tooltip("Grösser als 1 macht es leichter.\n" +
             "Bei 1 ist bei 5.7 Grad Schluss (Maximum des Tuo), bei 2 erst bei 11.3 Grad.\n" +
             "Hier steckt auch die unbekannte Masse-Einstellung des Tuo mit drin - " +
             "einfach so lange drehen, bis es sich richtig anfühlt.")]
    [Range(1f, 3f)] public float verzerrungsTeiler = 2f;

    [Tooltip("Rollwiderstand, dimensionslos.\n" +
             "0.004 Asphalt / 0.006 rauer Belag / 0.008 Schotter / 0.02 Wiese.\n" +
             "Wirkt als konstanter Widerstand, unabhaengig vom Tempo.")]
    [Range(0f, 0.0255f)] public float crr = 0.006f;

    [Tooltip("Luftwiderstand in kg/m. Waechst mit dem QUADRAT der Geschwindigkeit.\n" +
             "0.20 = Rennrad (CdA 0.33) / 0.30 = City oder MTB aufrecht (CdA 0.49).\n" +
             "Das ist der einzige Wert, der schnell und langsam unterschiedlich " +
             "anfuehlen laesst - nicht auf 0 setzen.")]
    [Range(0f, 2.55f)] public float cw = 0.30f;

    [Header("Fahrer")]
    [Tooltip("Gewicht des aktuellen Fahrers in kg.")]
    public float fahrerMasse = 75f;

    [Tooltip("Gewicht, bei dem der Verzerrungsteiler eingestellt wurde.\n" +
             "NICHT die Masse im Tuo - die kennen wir nicht und brauchen wir nicht.\n" +
             "Dank dieser Zahl gilt der Teiler fuer jedes Gewicht: ein schwererer " +
             "Fahrer bekommt automatisch mehr Widerstand.")]
    public float referenzMasse = 75f;

    [Header("Anzeige (nur lesen)")]
    [SerializeField] float  steigungInGameInProzent;   // was die Strecke wirklich hat
    [SerializeField] float  berechnetNachVerzerrungProzent;       // was wir ausgerechnet haben
    [SerializeField] float  gesendetProzent;        // was beim Tuo ankommt
    [SerializeField] bool   amAnschlag;             // Tuo kann nicht mehr
    [SerializeField] string trainerStatus = "-";

    Trainer _t;
    float   _naechsteSuche;   // drosselt die Suche nach dem Trainer

    /// <summary>Wird von Fortbewegen jeden Frame aufgerufen. Bei neigungVonHand
    /// wird der Wert bewusst verworfen, damit man im Inspector testen kann.</summary>
    public void SetzeNeigung(float grad)
    {
        if (!neigungVonHand) neigungGrad = grad;
    }

    void Update()
    {
        // Der Trainer legt sich selbst an, existiert im ersten Frame aber
        // vielleicht noch nicht. Gedrosselt suchen: FindFirstObjectByType geht
        // durch die ganze Szene und darf nicht jeden Frame laufen.
        if (_t == null)
        {
            if (Time.time < _naechsteSuche) return;
            _naechsteSuche = Time.time + 0.5f;
            _t = FindFirstObjectByType<Trainer>();
            if (_t == null) return;
        }

        //grad zu steigung in prozent
        float echteSteigung = Mathf.Tan(neigungGrad * Mathf.Deg2Rad);

        float steigung = echteSteigung / Mathf.Max(1f, verzerrungsTeiler);

        // gewichtkorrektur
        steigung *= fahrerMasse / Mathf.Max(1f, referenzMasse);

        // -4%steigung heisst abwärts gefühl
        _t.SetSimulation(steigung, crr, cw);

        //werte anzeigen
        steigungInGameInProzent = echteSteigung * 100f;
        berechnetNachVerzerrungProzent     = steigung * 100f;
        gesendetProzent      = _t.GesendeteSteigung * 100f;
        amAnschlag           = _t.AmAnschlag;
        trainerStatus        = _t.Status;
    }
}
