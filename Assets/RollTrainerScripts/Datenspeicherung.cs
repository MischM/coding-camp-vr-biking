using System;
using System.IO;
using UnityEngine;

// ===========================================================================
// Viertes Skript neben Fortbewegen, Lenken und Bremskraft.
// Es fasst nichts an, es schaut nur zu und schreibt am Ende eine Datei.
// ===========================================================================

/// <summary>
/// Misst die Durchschnittsgeschwindigkeit einer Fahrt und speichert sie beim
/// Druck auf "Beenden" als CSV im Ordner Assets/RollTrainerScripts.
///
/// GEAENDERT: schreibt CSV statt Text, und standardmaessig ALLE Fahrten als
/// Zeilen in EINE Datei. Genau dafuer ist CSV da - eine Tabelle mit einer
/// einzigen Zeile pro Datei koennte man auch als Text speichern. So kann man
/// die Datei in Excel oder Numbers oeffnen und die Fahrten des ganzen Camps
/// nebeneinander sehen, sortieren und ein Diagramm daraus machen.
/// Wer lieber pro Fahrt eine eigene Datei will: Haekchen unten wegnehmen.
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

    [Tooltip("Name der Datei, ohne Endung.\n" +
             "Bei einer eigenen Datei pro Fahrt haengt das Skript Datum und\n" +
             "Uhrzeit an, damit keine Fahrt eine aeltere ueberschreibt.")]
    public string dateiName = "Fahrten";

    [Tooltip("Alle Fahrten als Zeilen in EINE CSV schreiben.\n" +
             "Angehakt: eine Tabelle, die mit jeder Fahrt eine Zeile laenger\n" +
             "wird - in Excel sofort auswertbar und vergleichbar.\n" +
             "Weg: pro Fahrt eine eigene Datei mit Datum im Namen.")]
    public bool alleFahrtenInEineDatei = true;

    [Tooltip("Kommas statt Punkte als Dezimaltrennzeichen.\n" +
             "Deutsche und Schweizer Excel-Versionen erwarten das Komma -\n" +
             "sonst landen 12.4 km/h als Text in der Zelle und man kann nicht\n" +
             "damit rechnen. Fuer englische Versionen wegnehmen.")]
    public bool dezimalKomma = true;

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
    [SerializeField] float tempoMultiplikator = 1f;   // Massstab, mit dem diese Fahrt lief
    [SerializeField] float streckeImSpielMeter;       // Strecke mal Massstab
    [SerializeField] float durchschnittWatt;          // ueber die Fahrzeit gemittelt
    [SerializeField] string letzteDatei = "-";

    Trainer     _t;
    Fortbewegen _f;           // nur um den Tempo-Multiplikator abzulesen
    float       _naechsteSuche;   // drosselt die Suche nach beiden
    bool        _multiErfasst;    // true = Multiplikator dieser Fahrt steht fest
    float       _wattSekunden;    // aufsummierte Leistung, geteilt ergibt den Schnitt

    void Update()
    {
        // Der Trainer legt sich zum Teil erst beim Play an. Darum hier nachfassen
        // statt in Start() - aber gedrosselt, die Suche geht durch die ganze Szene.
        if (_t == null || _f == null)
        {
            if (Time.time >= _naechsteSuche)
            {
                _naechsteSuche = Time.time + 0.5f;
                if (_t == null) _t = FindFirstObjectByType<Trainer>();
                if (_f == null) _f = FindFirstObjectByType<Fortbewegen>();
            }
            // Ohne Trainer gibt es nichts zu messen. Ohne Fortbewegen schon -
            // dann wird der Multiplikator einfach als 1 notiert.
            if (_t == null) return;
        }

        float tempoKmh = _t.Speed;

        // Steht das Rad, passiert gar nichts: keine Zeit, keine Strecke.
        faehrtGerade = tempoKmh > startAbKmh;
        if (!faehrtGerade) return;

        // Beim ERSTEN Meter festhalten, mit welchem Massstab gefahren wird.
        // Bewusst nicht laufend nachlesen: wer waehrend der Fahrt am Regler
        // dreht, soll die schon gefahrenen Meter nicht rueckwirkend umdeuten.
        if (!_multiErfasst)
        {
            _multiErfasst = true;
            tempoMultiplikator = _f != null ? _f.tempoMultiplikator : 1f;
        }

        // Zeit und Strecke aufaddieren. Tempo durch 3.6 macht aus km/h m/s,
        // mal der Dauer dieses Frames ergibt die Meter dieses Frames.
        fahrzeitSekunden += Time.deltaTime;
        streckeMeter     += (tempoKmh / 3.6f) * Time.deltaTime;

        if (tempoKmh > hoechstesTempoKmh) hoechstesTempoKmh = tempoKmh;

        // Leistung ueber die Zeit aufsummieren (Wattsekunden), nicht die
        // einzelnen Messwerte mitteln: der Tuo meldet nur etwa 1x pro Sekunde,
        // und die Frames sind unterschiedlich lang. Wer stattdessen alle
        // Messwerte addiert und durch ihre Anzahl teilt, gewichtet einen
        // kurzen Frame genauso stark wie einen langen.
        _wattSekunden += _t.Power * Time.deltaTime;

        // Strecke geteilt durch Zeit IST der Durchschnitt - man muss keine
        // einzelnen Messwerte sammeln und am Ende mitteln.
        durchschnittKmh    = (streckeMeter / fahrzeitSekunden) * 3.6f;
        durchschnittWatt   = _wattSekunden / fahrzeitSekunden;

        // Im Spiel zurueckgelegt: die getretene Strecke mal dem Massstab.
        streckeImSpielMeter = streckeMeter * tempoMultiplikator;
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

        DateTime jetzt = DateTime.Now;

        string datei = alleFahrtenInEineDatei
            ? Path.Combine(ordner, dateiName + ".csv")
            : Path.Combine(ordner, dateiName + "_" + jetzt.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv");

        // Semikolon statt Komma: deutsche und schweizer Excel-Versionen
        // erwarten das so, und mit Dezimalkommas waere ein Komma als Trenner
        // ohnehin zweideutig. Die Zeile "sep=;" ganz oben sagt Excel dasselbe
        // noch einmal ausdruecklich - andere Programme ueberlesen sie.
        bool neueDatei = !File.Exists(datei);

        string kopf = "sep=;\n" +
                      "Datum;Uhrzeit;Durchschnitt_kmh;Hoechstes_Tempo_kmh;" +
                      "Fahrzeit_s;Strecke_getreten_m;Strecke_im_Spiel_m;" +
                      "Tempo_Multiplikator;Durchschnitt_Watt\n";

        string zeile = string.Join(";", new[]
        {
            jetzt.ToString("yyyy-MM-dd"),
            jetzt.ToString("HH:mm:ss"),
            Zahl(durchschnittKmh,   1),
            Zahl(hoechstesTempoKmh, 1),
            Zahl(fahrzeitSekunden,  0),
            Zahl(streckeMeter,        0),
            Zahl(streckeImSpielMeter, 0),
            Zahl(tempoMultiplikator,  2),
            Zahl(durchschnittWatt,    0)
        }) + "\n";

        // Anhaengen statt Ueberschreiben - so waechst die Tabelle mit jeder
        // Fahrt. Bei einer eigenen Datei pro Fahrt ist sie ohnehin immer neu.
        if (neueDatei) File.WriteAllText(datei, kopf + zeile);
        else           File.AppendAllText(datei, zeile);

        letzteDatei = datei;
        Debug.Log("[Datenspeicherung] Gespeichert: " + datei + "\n" + zeile);

#if UNITY_EDITOR
        // Damit die neue Datei sofort im Project-Fenster auftaucht.
        UnityEditor.AssetDatabase.Refresh();
#endif

        // Alles zurueck auf Anfang fuer die naechste Fahrt.
        fahrzeitSekunden    = 0f;
        streckeMeter        = 0f;
        streckeImSpielMeter = 0f;
        durchschnittKmh     = 0f;
        hoechstesTempoKmh   = 0f;
        durchschnittWatt    = 0f;
        _wattSekunden       = 0f;

        // Damit die naechste Fahrt den dann eingestellten Massstab erwischt.
        _multiErfasst = false;
    }

    /// <summary>Zahl fuer die CSV formatieren. Immer mit fester Nachkommastelle
    /// und unabhaengig davon, auf welche Sprache der Rechner steht - sonst
    /// schreibt derselbe Code auf zwei Laptops zwei verschiedene Dateien.</summary>
    string Zahl(float wert, int nachkomma)
    {
        string s = wert.ToString("F" + nachkomma,
                                 System.Globalization.CultureInfo.InvariantCulture);
        return dezimalKomma ? s.Replace('.', ',') : s;
    }

    void OnGUI()
    {
        if (!knopfAnzeigen) return;

        // Unter der Anzeige von TrainerUI, damit sich die zwei nicht ueberdecken.
        GUILayout.BeginArea(new Rect(20, 290, 300, 130), GUI.skin.box);

        GUILayout.Label(faehrtGerade ? "Aufzeichnung laeuft" : "wartet auf Bewegung");
        GUILayout.Label("Durchschnitt  " + durchschnittKmh.ToString("F1") + " km/h");
        GUILayout.Label("Fahrzeit      " + fahrzeitSekunden.ToString("F0") + " s");
        GUILayout.Label("Leistung      " + durchschnittWatt.ToString("F0") + " W");

        if (GUILayout.Button("Beenden und speichern", GUILayout.Height(28))) Beenden();

        GUILayout.EndArea();
    }
}
