using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// ===========================================================================
// Auf dem Weg bleiben - wer abkommt, landet wieder am Start.
//
// Erkannt wird der Weg an dem, was auf das Terrain GEMALT ist: an jedem
// Abtastpunkt liefert die Alphamap, mit welchem Gewicht jeder Terrain-Layer
// dort liegt. Liegt zu wenig "Weg-Layer" unter dem Fahrer, ist er daneben.
// Kein Collider, kein Trigger, keine Physics-Layer noetig - malt ihr den Weg
// weiter, gilt das sofort, ohne irgendetwas nachzuziehen.
//
// ACHTUNG, der haeufigste Stolperstein: die Reihenfolge der Layer IM TERRAIN
// ist nicht die Reihenfolge der Dateien im Projektordner. Darum werden die
// Layer hier ueber ihren NAMEN aufgeloest, und beim Start steht in der Console,
// welche Layer das Terrain hat und welche davon als Weg gelten.
//
// Laeuft in LateUpdate und mit hoher Execution Order, also NACH Fortbewegen.
// Andersherum wuerde Fortbewegen im selben Frame ueber das Umsetzen schreiben.
// ===========================================================================

/// <summary>
/// Setzt den Fahrer an den Start zurueck, sobald er zu lange neben dem
/// aufgemalten Weg faehrt.
///
/// Auf dasselbe GameObject wie Fortbewegen und Lenken ziehen - beim VR-Aufbau
/// also das XR ORIGIN.
/// </summary>
[DefaultExecutionOrder(200)]
public class AufDemWegBleiben : MonoBehaviour
{
    // =====================================================================
    // DER WEG
    // =====================================================================
    [Header("Der Weg")]
    [Tooltip("Das Terrain, auf dem der Weg gemalt ist.\n" +
             "Leer lassen: es wird das aktive Terrain der Szene genommen.\n" +
             "Nur ausfuellen, wenn mehrere Terrains in der Szene liegen.")]
    public Terrain terrain;

    [Tooltip("Die Namen der Terrain-Layer, die als WEG gelten.\n" +
             "Also die, mit denen ihr den Weg gemalt habt.\n" +
             "Leerzeichen und Gross-/Kleinschreibung sind egal: 'NewLayer5'\n" +
             "findet auch 'NewLayer 5'.\n" +
             "Welche Layer das Terrain ueberhaupt hat, steht beim Start in der\n" +
             "Console - dort auch nachschauen, wenn ein Name nicht passt.")]
    public string[] erlaubteLayer = { "2", "NewLayer 5" };

    [Tooltip("Ab welchem Anteil Weg-Layer man als 'auf dem Weg' gilt.\n" +
             "0.5 heisst: mehr als die Haelfte unter dem Fahrer muss Weg sein.\n" +
             "An den Raendern gehen die Texturen weich ineinander ueber, darum\n" +
             "ist das kein harter Rand.\n" +
             "Kleiner = grosszuegiger (auch der Randbereich zaehlt noch),\n" +
             "groesser = strenger (man muss mittig fahren).")]
    [Range(0.05f, 1f)] public float schwelle = 0.5f;

    // =====================================================================
    // ZURUECK AN DEN START
    // =====================================================================
    [Header("Zurueck an den Start")]
    [Tooltip("Wohin zurueckgesetzt wird.\n" +
             "Ein leeres GameObject an die Startstelle legen und hier hinein\n" +
             "ziehen. Seine BLICKRICHTUNG (blauer Pfeil im Editor) bestimmt,\n" +
             "in welche Richtung der Fahrer danach schaut - also den Weg\n" +
             "entlang drehen, nicht in den Wald.\n" +
             "Leer lassen: es wird die Stelle gemerkt, an der die Szene\n" +
             "gestartet ist.")]
    public Transform start;

    [Tooltip("Wie lange man neben dem Weg sein darf, bevor zurueckgesetzt wird.\n" +
             "Nicht auf 0 stellen: an Kurvenraendern und Uebergaengen streift\n" +
             "man kurz daneben, und sofortiges Zuruecksetzen fuehlt sich\n" +
             "ungerecht an.")]
    [Range(0f, 3f)] public float karenz = 0.7f;

    [Tooltip("Wie lange nach einem Reset nicht geprueft wird.\n" +
             "Wichtig, weil der Tuo weiterlaeuft: ohne diese Pause faehrt man\n" +
             "sofort wieder los und kann in eine Reset-Schleife geraten.")]
    [Range(0f, 5f)] public float schutzNachReset = 1.5f;

    [Tooltip("Beim Zuruecksetzen die Schraeglage des Rigs begradigen.\n" +
             "Das XR Origin steht in der Szene ein paar Grad schief. Ein\n" +
             "gekippter Horizont ist in VR eine der zuverlaessigsten\n" +
             "Uebelkeitsursachen, und weder Lenken noch Fortbewegen fassen\n" +
             "die Schraeglage je an - sie bleibt sonst die ganze Sitzung.\n" +
             "Der Reset ist der natuerliche Moment, den Fahrer wieder\n" +
             "aufzurichten. Ausschalten, falls die Neigung Absicht ist.")]
    public bool waagerechtStellen = true;

    [Tooltip("Die Schwarzblende fuer den Moment des Umsetzens.\n" +
             "Leer lassen: wird in der Szene gesucht. Findet sich keine, wird\n" +
             "hart umgesetzt - das funktioniert, ist in VR aber unangenehm.")]
    public Schwarzblende blende;

    // =====================================================================
    // ABTASTUNG
    // =====================================================================
    [Header("Abtastung")]
    [Tooltip("Die Kamera des Fahrers (Main Camera im XR Rig).\n" +
             "Leer lassen: wird unter diesem Objekt automatisch gesucht.\n" +
             "Wozu: geprueft wird unter dem FAHRER, nicht unter dem\n" +
             "Rig-Ursprung - genau wie in Fortbewegen. Der Fahrer steht selten\n" +
             "genau auf dem Ursprung, sonst pruefen wir die falsche Stelle.")]
    public Transform kopf;

    [Tooltip("Wie oft pro Sekunde geprueft wird.\n" +
             "Die Abfrage der Alphamap legt jedes Mal ein kleines Array an -\n" +
             "in jedem Frame waere das unnoetiger Muell fuer den Speicher.\n" +
             "10x pro Sekunde reicht bei Fahrradtempo voellig.")]
    [Range(1f, 30f)] public float pruefRate = 10f;

    // =====================================================================
    // EREIGNIS
    // =====================================================================
    [Header("Ereignis")]
    [Tooltip("Wird bei jedem Zuruecksetzen ausgeloest.\n" +
             "Hier z.B. einen Sound anhaengen oder einen Zaehler hochzaehlen.")]
    public UnityEvent beimZuruecksetzen;

    // =====================================================================
    // ANZEIGE - nur zum Zuschauen
    // =====================================================================
    [Header("Anzeige (nur lesen)")]
    [SerializeField] float  wegAnteil;         // wie viel Weg-Layer gerade unter dem Fahrer liegt
    [SerializeField] bool   aufDemWeg;
    [SerializeField] float  abseitsSekunden;   // laeuft gegen karenz
    [SerializeField] bool   ausserhalbTerrain;
    [SerializeField] int    resetsBisher;
    [SerializeField] string wegLayerImTerrain; // was als Weg erkannt wurde

    readonly List<int> _wegLayer = new List<int>();

    TerrainData _daten;
    float       _naechstePruefung;
    float       _schutzBis;
    float       _abseitsSeit = -1f;   // -1 = gerade nicht abseits

    // Rueckfallposition, falls kein Startmarker gesetzt ist
    Vector3 _startPunkt;
    float   _startGier;

    CharacterController _cc;   // liegt im XR Rig der Starter Assets mit drin

    void Awake()
    {
        // Das XR Origin der XRI Starter Assets bringt einen CharacterController
        // mit. Der merkt sich seine eigene Position und rueckt sie im naechsten
        // Frame gegen die Umgebung zurecht - beim Umsetzen muss er kurz aus,
        // sonst zieht er den Fahrer gelegentlich wieder zurueck.
        _cc = GetComponent<CharacterController>();

        // Kamera einmal suchen, nicht jeden Frame - dieselbe Logik wie in
        // Fortbewegen und Lenken.
        if (kopf == null)
        {
            var c = GetComponentInChildren<Camera>();
            if (c != null) kopf = c.transform;
        }

        // Eine fremde Kamera als Abtastpunkt waere unbrauchbar: dann pruefen
        // wir eine voellig andere Stelle der Welt als die, an der gefahren wird.
        if (kopf != null && !kopf.IsChildOf(transform))
        {
            Debug.LogWarning("[AufDemWegBleiben] '" + kopf.name + "' haengt nicht unter '" +
                             name + "'. Pruefe unter dem Ursprung.", this);
            kopf = null;
        }
    }

    void Start()
    {
        // Startpose merken, bevor irgendjemand losfaehrt. Dient als Rueckfall,
        // wenn kein Marker im Feld steckt.
        _startPunkt = kopf != null ? kopf.position : transform.position;
        _startGier  = transform.eulerAngles.y;

        if (terrain == null) terrain = Terrain.activeTerrain;
        if (blende  == null) blende  = FindFirstObjectByType<Schwarzblende>();

        if (terrain == null)
        {
            Debug.LogError("[AufDemWegBleiben] Kein Terrain gefunden. Skript wird " +
                           "abgeschaltet, sonst wuerde es dauernd zuruecksetzen.", this);
            enabled = false;
            return;
        }

        _daten = terrain.terrainData;
        LayerAufloesen();
    }

    // -----------------------------------------------------------------------
    // Aus den Namen im Inspector die Layer-Nummern des Terrains machen.
    // Ueber den Namen und nicht ueber die Nummer, weil die Reihenfolge im
    // Terrain nicht der Reihenfolge im Projektordner entspricht - und weil
    // eine Nummer im Inspector niemandem verraet, welche Textur gemeint ist.
    // -----------------------------------------------------------------------
    void LayerAufloesen()
    {
        _wegLayer.Clear();
        TerrainLayer[] alle = _daten.terrainLayers;

        // Immer ausgeben: ohne diese Liste sucht man sich zu Tode, wenn ein
        // Name nicht passt.
        var uebersicht = new System.Text.StringBuilder();
        uebersicht.Append("[AufDemWegBleiben] Terrain '").Append(terrain.name)
                  .Append("' hat ").Append(alle.Length).Append(" Layer:");
        for (int i = 0; i < alle.Length; i++)
            uebersicht.Append("\n   ").Append(i).Append(" = ")
                      .Append(alle[i] != null ? alle[i].name : "(leer)");

        for (int n = 0; n < erlaubteLayer.Length; n++)
        {
            string gesucht = Normiert(erlaubteLayer[n]);
            if (gesucht.Length == 0) continue;

            bool gefunden = false;
            for (int i = 0; i < alle.Length; i++)
            {
                if (alle[i] == null || Normiert(alle[i].name) != gesucht) continue;
                if (!_wegLayer.Contains(i)) _wegLayer.Add(i);
                gefunden = true;
            }

            if (!gefunden)
                Debug.LogWarning("[AufDemWegBleiben] Kein Terrain-Layer heisst '" +
                                 erlaubteLayer[n] + "'. Siehe Liste in der naechsten " +
                                 "Meldung - Schreibweise pruefen.", this);
        }

        wegLayerImTerrain = string.Join(", ", _wegLayer);
        uebersicht.Append("\n   -> als WEG gilt: ")
                  .Append(_wegLayer.Count > 0 ? wegLayerImTerrain : "NICHTS");

        // Aufloesung der Alphamap mitloggen. Sie ist meist GROEBER als das
        // Gelaende: ist der Weg schmaler als ein paar Texel, wird er beim
        // Malen zu Matsch und der Weg-Anteil kommt nie in die Naehe von 1.
        // Dann hilft keine Schwelle, sondern nur ein breiterer Weg oder eine
        // hoehere Control Texture Resolution in den Terrain Settings.
        float meterProTexel = _daten.size.x / Mathf.Max(1, _daten.alphamapWidth);
        uebersicht.Append("\n   Alphamap: ").Append(_daten.alphamapWidth).Append(" x ")
                  .Append(_daten.alphamapHeight).Append(", also ca. ")
                  .Append(meterProTexel.ToString("0.00"))
                  .Append(" m pro Texel - der Weg sollte deutlich breiter sein als das.");
        Debug.Log(uebersicht.ToString(), this);

        // Ohne erkannten Weg waere alles abseits und der Fahrer haenge sofort
        // in einer Reset-Schleife fest. Lieber abschalten und laut sagen warum.
        if (_wegLayer.Count == 0)
        {
            Debug.LogError("[AufDemWegBleiben] Keiner der eingetragenen Layer existiert " +
                           "im Terrain. Skript wird abgeschaltet, sonst wuerde es ohne " +
                           "Unterlass zuruecksetzen.", this);
            enabled = false;
        }
    }

    // Leerzeichen, Unterstriche und Gross-/Kleinschreibung wegwerfen, damit
    // 'NewLayer5', 'NewLayer 5' und 'newlayer_5' alle dasselbe treffen.
    static string Normiert(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace(" ", "").Replace("_", "").ToLowerInvariant();
    }

    // -----------------------------------------------------------------------
    // LateUpdate und Execution Order 200: erst faehrt Fortbewegen den Fahrer,
    // dann pruefen wir, wo er gelandet ist. Umgekehrt wuerde Fortbewegen im
    // selben Frame ueber das Umsetzen drueberschreiben.
    // -----------------------------------------------------------------------
    void LateUpdate()
    {
        // Nach einem Reset kurz nichts tun. Der Tuo laeuft weiter, der Fahrer
        // rollt also sofort wieder an - ohne diese Pause kann das Umsetzen
        // sich selbst nachtriggern.
        if (Time.time < _schutzBis)
        {
            _abseitsSeit    = -1f;
            abseitsSekunden = 0f;
            return;
        }

        if (Time.time < _naechstePruefung) return;
        _naechstePruefung = Time.time + 1f / Mathf.Max(1f, pruefRate);

        Vector3 punkt = kopf != null ? kopf.position : transform.position;
        wegAnteil = WegAnteilAn(punkt);         // -1 = ausserhalb des Terrains
        ausserhalbTerrain = wegAnteil < 0f;
        aufDemWeg = !ausserhalbTerrain && wegAnteil >= schwelle;

        if (aufDemWeg)
        {
            _abseitsSeit    = -1f;
            abseitsSekunden = 0f;
            return;
        }

        // Ueber Zeitstempel zaehlen, nicht ueber Frames: geprueft wird nur
        // 10x pro Sekunde, deltaTime aufzuaddieren waere schlicht falsch.
        if (_abseitsSeit < 0f) _abseitsSeit = Time.time;
        abseitsSekunden = Time.time - _abseitsSeit;

        if (abseitsSekunden >= karenz) Zuruecksetzen();
    }

    /// <summary>Wie viel Weg liegt an dieser Stelle unter dem Fahrer?
    /// 0 = gar keiner, 1 = nur Weg. -1 heisst: ausserhalb des Terrains.</summary>
    float WegAnteilAn(Vector3 weltPunkt)
    {
        if (_daten == null) return -1f;

        // Weltkoordinate in die 0..1-Koordinate des Terrains umrechnen.
        Vector3 relativ = weltPunkt - terrain.transform.position;
        float nx = relativ.x / _daten.size.x;
        float nz = relativ.z / _daten.size.z;

        // Ausserhalb der Karte gibt es keinen Weg mehr - das gilt als abseits,
        // sonst koennte man einfach vom Terrain herunterfahren.
        if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) return -1f;

        // Die Alphamap hat ihre eigene Aufloesung, meist grober als das Gelaende.
        int mx = Mathf.Clamp(Mathf.RoundToInt(nx * (_daten.alphamapWidth  - 1)),
                             0, _daten.alphamapWidth  - 1);
        int mz = Mathf.Clamp(Mathf.RoundToInt(nz * (_daten.alphamapHeight - 1)),
                             0, _daten.alphamapHeight - 1);

        // GetAlphamaps(x, y, ...): x laeuft entlang der Welt-X-Achse,
        // y entlang der Welt-Z-Achse. Genau hier vertut man sich gern.
        float[,,] gewichte = _daten.GetAlphamaps(mx, mz, 1, 1);

        float summe = 0f;
        int   anzahl = gewichte.GetLength(2);
        for (int i = 0; i < _wegLayer.Count; i++)
        {
            int layer = _wegLayer[i];
            if (layer < anzahl) summe += gewichte[0, 0, layer];
        }
        return Mathf.Clamp01(summe);
    }

    /// <summary>Fahrer an den Start setzen. Auch von aussen aufrufbar,
    /// z.B. fuer einen "Neustart"-Knopf.</summary>
    [ContextMenu("Jetzt zuruecksetzen")]
    public void Zuruecksetzen()
    {
        // Schutzzeit SOFORT setzen, nicht erst nach der Blende: waehrend
        // abgeblendet wird, laeuft LateUpdate weiter und wuerde sonst gleich
        // noch einen Reset anstossen.
        _schutzBis      = Time.time + schutzNachReset + (blende != null ? 1f : 0f);
        _abseitsSeit    = -1f;
        abseitsSekunden = 0f;

        if (blende != null) blende.Blenden(Versetzen);
        else                Versetzen();
    }

    void Versetzen()
    {
        Vector3 zielPunkt = start != null ? start.position      : _startPunkt;
        float   zielGier  = start != null ? start.eulerAngles.y : _startGier;

        // Den CharacterController fuer den Sprung stilllegen, siehe Awake.
        bool ccWarAn = _cc != null && _cc.enabled;
        if (ccWarAn) _cc.enabled = false;

        // ---- 1. Blickrichtung ------------------------------------------------
        // Zielausrichtung: die Gier vom Startmarker, dazu wahlweise waagerecht.
        Quaternion sollDrehung = waagerechtStellen
            ? Quaternion.Euler(0f, zielGier, 0f)
            : Quaternion.Euler(transform.eulerAngles.x, zielGier, transform.eulerAngles.z);

        if (kopf != null)
        {
            // Um den KOPF drehen, nicht um den Rig-Ursprung. Genau wie in
            // Lenken: der Fahrer steht neben dem Ursprung und wuerde sonst auf
            // einer Kreisbahn um ihn herum geschleudert.
            // RotateAround kann das hier nicht leisten, weil auch die Kippung
            // wegmuss - also die Aenderung selbst ausrechnen und das Rig so
            // versetzen, dass der Kopf dabei stehen bleibt.
            Quaternion aenderung = sollDrehung * Quaternion.Inverse(transform.rotation);
            Vector3    kopfVorher = kopf.position;
            Vector3    rigZumKopf = transform.position - kopfVorher;

            transform.rotation = sollDrehung;
            transform.position = kopfVorher + aenderung * rigZumKopf;
        }
        else
        {
            transform.rotation = sollDrehung;
        }

        // ---- 2. Position -----------------------------------------------------
        // Waagerecht so verschieben, dass der KOPF auf dem Startpunkt landet.
        // Das Rig selbst landet dann irgendwo daneben - genau richtig, denn
        // gemeint ist ja, wo der Fahrer steht.
        Vector3 bezug   = kopf != null ? kopf.position : transform.position;
        Vector3 versatz = zielPunkt - bezug;
        versatz.y = 0f;
        transform.position += versatz;

        // Hoehe hart setzen, nicht geglaettet. Fortbewegen zieht die Hoehe mit
        // hoehenGlaettung nach - nach einem Sprung ueber die halbe Karte wuerde
        // man sonst erst langsam auf den Boden absinken oder darin stecken.
        Vector3 p = transform.position;
        p.y = zielPunkt.y;
        transform.position = p;

        if (ccWarAn) _cc.enabled = true;

        resetsBisher++;
        beimZuruecksetzen?.Invoke();
    }

    // Im Editor zeigen, wo geprueft wird und wohin es zurueckgeht.
    void OnDrawGizmosSelected()
    {
        Vector3 punkt = kopf != null ? kopf.position : transform.position;
        Gizmos.color = aufDemWeg ? Color.green : Color.red;
        Gizmos.DrawWireSphere(punkt, 0.5f);

        if (start != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(start.position, 1f);
            Gizmos.DrawRay(start.position, start.forward * 3f);
        }
    }
}
