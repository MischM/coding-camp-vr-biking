using System;
using System.IO;
using UnityEngine;

// ===========================================================================
// Viertes Skript neben Fortbewegen, Lenken und Bremskraft.
// Es fasst nichts an, es schaut nur zu und schreibt am Ende eine Datei.
// ===========================================================================

/// <summary>
/// Misst die Durchschnittsgeschwindigkeit einer Fahrt und speichert sie beim
/// Druck auf "Beenden" als Textdatei im Ordner Assets/RollTrainerScripts.
///
/// Ablauf:
///  1. Die Messung startet von selbst, sobald der Tuo ein Tempo ueber 0 meldet.
///  2. Mitgezaehlt wird nur die Zeit, in der wirklich gefahren wird. Steht das
///     Rad still, laeuft die Uhr nicht weiter - Pausen druecken den Schnitt
///     also nicht nach unten.
///  3. Ein Klick auf "Beenden" schreibt das Ergebnis in eine Datei und setzt
///     die Messung zurueck, damit man gleich die naechste Fahrt aufzeichnen kann.
///
/// Setup: keines noetig. Genau wie Trainer legt sich dieses Skript beim Druecken
/// von Play selbst an (siehe AutoStart unten). Wer es lieber im Inspector sehen
/// und einstellen will, zieht es zusaetzlich auf ein GameObject - dann wird kein
/// zweites angelegt.
/// </summary>
public class Datenspeicherung : MonoBehaviour
{
    // =====================================================================
    // Startet sich beim Druecken von Play von selbst, damit man nichts auf ein
    // GameObject ziehen muss. Dasselbe Muster wie in Trainer.cs.
    // Liegt das Skript schon irgendwo in der Szene, passiert hier nichts - dann
    // gilt das Objekt aus der Szene mit seinen Einstellungen.
    // =====================================================================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (FindFirstObjectByType<Datenspeicherung>() != null) return;

        var go = new GameObject("Datenspeicherung (auto)");
        DontDestroyOnLoad(go);
        go.AddComponent<Datenspeicherung>();
    }

    [Header("Einstellungen")]
    [Tooltip("Ab diesem Tempo gilt es als \"faehrt\", in km/h.\n" +
             "Nicht exakt 0 nehmen: der Tuo meldet beim Ausrollen gerne noch\n" +
             "winzige Restwerte, und die wuerden die Uhr weiterlaufen lassen.")]
    public float startAbKmh = 0.5f;

    [Tooltip("Anfang des Dateinamens. Datum und Uhrzeit haengt das Skript an,\n" +
             "damit keine Fahrt eine aeltere ueberschreibt.")]
    public string dateiName = "Fahrt";

    [Tooltip("Zeigt den Beenden-Knopf im Spiel an (unter der Trainer-Anzeige).\n" +
             "Wegnehmen, wenn der Knopf spaeter aus dem VR-Menue kommen soll -\n" +
             "dann einfach Beenden() von dort aufrufen.")]
    public bool knopfAnzeigen = true;

    [Header("Anzeige (nur lesen)")]
    [SerializeField] bool  faehrtGerade;          // true = Uhr laeuft
    [SerializeField] float fahrzeitSekunden;      // nur die Zeit in Bewegung
    [SerializeField] float streckeMeter;          // daraus wird der Schnitt
    [SerializeField] float durchschnittKmh;       // Strecke geteilt durch Fahrzeit
    [SerializeField] float hoechstesTempoKmh;
    [SerializeField] string letzteDatei = "-";

    Trainer _t;
    float   _naechsteSuche;   // drosselt die Suche nach dem Trainer

    void Update()
    {
        // Der Trainer legt sich zum Teil erst beim Play an. Darum hier nachfassen
        // statt in Start() - aber gedrosselt, die Suche geht durch die ganze Szene.
        if (_t == null)
        {
            if (Time.time < _naechsteSuche) return;
            _naechsteSuche = Time.time + 0.5f;
            _t = FindFirstObjectByType<Trainer>();
            if (_t == null) return;
        }

        float tempoKmh = _t.Speed;

        // Steht das Rad, passiert gar nichts: keine Zeit, keine Strecke.
        faehrtGerade = tempoKmh > startAbKmh;
        if (!faehrtGerade) return;

        // Zeit und Strecke aufaddieren. Tempo durch 3.6 macht aus km/h m/s,
        // mal der Dauer dieses Frames ergibt die Meter dieses Frames.
        fahrzeitSekunden += Time.deltaTime;
        streckeMeter     += (tempoKmh / 3.6f) * Time.deltaTime;

        if (tempoKmh > hoechstesTempoKmh) hoechstesTempoKmh = tempoKmh;

        // Strecke geteilt durch Zeit IST der Durchschnitt - man muss keine
        // einzelnen Messwerte sammeln und am Ende mitteln.
        durchschnittKmh = (streckeMeter / fahrzeitSekunden) * 3.6f;
    }

    /// <summary>Fahrt beenden: Daten in eine Datei schreiben und alles auf 0
    /// setzen. Kann auch aus einem anderen Skript aufgerufen werden, z.B. von
    /// einem Knopf im VR-Menue.</summary>
    public void Beenden()
    {
        if (fahrzeitSekunden <= 0f)
        {
            Debug.Log("[Datenspeicherung] Noch keine Fahrt aufgezeichnet - nichts gespeichert.");
            return;
        }

        // Im Editor landet die Datei direkt neben diesem Skript. Im fertigen
        // Build gibt es den Assets-Ordner nicht mehr, dann nimmt Unity seinen
        // eigenen Speicherort (persistentDataPath).
        string ordner = Path.Combine(Application.dataPath, "RollTrainerScripts");
        if (!Directory.Exists(ordner)) ordner = Application.persistentDataPath;

        string datei = Path.Combine(
            ordner, dateiName + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");

        string text =
            "Fahrt vom " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "\n" +
            "-----------------------------------\n" +
            "Durchschnitt     " + durchschnittKmh.ToString("F1")   + " km/h\n" +
            "Hoechstes Tempo  " + hoechstesTempoKmh.ToString("F1") + " km/h\n" +
            "Fahrzeit         " + fahrzeitSekunden.ToString("F0")  + " s\n" +
            "Strecke          " + streckeMeter.ToString("F0")      + " m\n";

        File.WriteAllText(datei, text);
        letzteDatei = datei;
        Debug.Log("[Datenspeicherung] Gespeichert: " + datei + "\n" + text);

#if UNITY_EDITOR
        // Damit die neue Datei sofort im Project-Fenster auftaucht.
        UnityEditor.AssetDatabase.Refresh();
#endif

        // Alles zurueck auf Anfang fuer die naechste Fahrt.
        fahrzeitSekunden  = 0f;
        streckeMeter      = 0f;
        durchschnittKmh   = 0f;
        hoechstesTempoKmh = 0f;
    }

    void OnGUI()
    {
        if (!knopfAnzeigen) return;

        // Unter der Anzeige von TrainerUI, damit sich die zwei nicht ueberdecken.
        GUILayout.BeginArea(new Rect(20, 290, 300, 130), GUI.skin.box);

        GUILayout.Label(faehrtGerade ? "Aufzeichnung laeuft" : "wartet auf Bewegung");
        GUILayout.Label("Durchschnitt  " + durchschnittKmh.ToString("F1") + " km/h");
        GUILayout.Label("Fahrzeit      " + fahrzeitSekunden.ToString("F0") + " s");

        if (GUILayout.Button("Beenden und speichern", GUILayout.Height(28))) Beenden();

        GUILayout.EndArea();
    }
}
