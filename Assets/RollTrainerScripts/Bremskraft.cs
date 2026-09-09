using UnityEngine;

public class Bremskraft : MonoBehaviour
{
    [Header("Eingang")]
    [Tooltip("Neigung des Fahrrads in Grad. Bergauf positiv bergab negativ.\n" +
             "Zum Testen hier von Hand eintragen, im Spiel per SetzeNeigung() " +
             "vom Terrain-Skript setzen lassen.")]
    public float neigungGrad = 0f;  //hier nachher wert von Fahrrad




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

    public void SetzeNeigung(float grad) => neigungGrad = grad; //hier wird das paket grad vom Fahrrad mit dem lokalen Neigunggrad gleichgesetzt

    void Update()
    {

        if (_t == null) { _t = FindFirstObjectByType<Trainer>(); return; }

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
