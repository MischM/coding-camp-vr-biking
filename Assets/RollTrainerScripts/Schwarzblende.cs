using System.Collections;
using UnityEngine;

// ===========================================================================
// Kurzes Abblenden vor dem Umsetzen des Fahrers.
//
// Warum ueberhaupt: den Fahrer mitten in der Fahrt ohne Uebergang an eine
// andere Stelle zu setzen, ist eine der zuverlaessigsten Uebelkeitsursachen in
// VR - das Bild springt, der Gleichgewichtssinn nicht. Ein Fuenftel Sekunde
// Schwarz reicht, damit das Gehirn den Schnitt als Schnitt akzeptiert.
//
// Das schwarze Bild wird zur LAUFZEIT selbst erzeugt (Quad vor der Kamera plus
// passendes Material). Im Editor ist also nichts einzurichten: Skript aufs
// XR Origin ziehen, fertig.
// ===========================================================================

/// <summary>
/// Blendet die Sicht schwarz, fuehrt im dunklen Moment eine Aktion aus und
/// blendet wieder auf. Wird von AufDemWegBleiben benutzt, laesst sich aber
/// genauso aus jedem UnityEvent aufrufen.
/// </summary>
public class Schwarzblende : MonoBehaviour
{
    [Header("Zeiten in Sekunden")]
    [Tooltip("Wie lange das Abdunkeln dauert.\n" +
             "Kurz halten - der Fahrer faehrt waehrenddessen blind weiter.")]
    [Range(0f, 2f)] public float abblendZeit = 0.2f;

    [Tooltip("Wie lange es ganz schwarz bleibt, nachdem umgesetzt wurde.\n" +
             "Etwas Schwarz nach dem Sprung hilft: sonst sieht man die neue\n" +
             "Umgebung schon, waehrend das Bild noch nachzieht.")]
    [Range(0f, 2f)] public float schwarzZeit = 0.1f;

    [Tooltip("Wie lange das Aufblenden dauert.\n" +
             "Ruhig laenger als das Abblenden - langsam ins Bild kommen ist\n" +
             "angenehmer, als hineingeworfen zu werden.")]
    [Range(0f, 3f)] public float aufblendZeit = 0.4f;

    [Header("Aufbau")]
    [Tooltip("Die Kamera des Fahrers.\n" +
             "Leer lassen: wird unter diesem Objekt automatisch gesucht.\n" +
             "Die Blende MUSS an der Kamera haengen, sonst dreht der Fahrer\n" +
             "einfach den Kopf an ihr vorbei.")]
    public Transform kopf;

    [Tooltip("Abstand der schwarzen Flaeche vor dem Auge, in Metern.\n" +
             "Muss groesser sein als die Near Clip Plane der Kamera (sonst wird\n" +
             "sie weggeschnitten) und klein genug, dass nichts davor passt.")]
    [Range(0.1f, 1f)] public float abstand = 0.25f;

    [Header("Anzeige (nur lesen)")]
    [SerializeField] float schwaerze;   // 0 = klar, 1 = ganz schwarz
    [SerializeField] bool  laeuft;

    Material _material;
    Renderer _renderer;

    void Awake()
    {
        if (kopf == null)
        {
            var c = GetComponentInChildren<Camera>();
            if (c != null) kopf = c.transform;
        }

        if (kopf == null)
        {
            Debug.LogWarning("[Schwarzblende] Keine Kamera unter '" + name +
                             "' gefunden. Es wird nicht geblendet, nur umgesetzt.", this);
            enabled = false;
            return;
        }

        FlaecheBauen();
        Schwaerze(0f);
    }

    void FlaecheBauen()
    {
        // URP zuerst - das Projekt laeuft auf URP. Die Alternativen sind nur
        // Fallschirme, falls die Pipeline mal wechselt.
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");

        _material = new Material(sh) { name = "Schwarzblende (Laufzeit)" };

        // Das Material auf Transparenz umstellen. Bei URP/Unlit reicht die
        // Farbe allein nicht - ohne diese Schalter bleibt es undurchsichtig
        // und man sieht dauerhaft schwarz.
        if (_material.HasProperty("_Surface")) _material.SetFloat("_Surface", 1f);  // 1 = Transparent
        if (_material.HasProperty("_Blend"))   _material.SetFloat("_Blend", 0f);    // 0 = Alpha
        if (_material.HasProperty("_ZWrite"))  _material.SetFloat("_ZWrite", 0f);
        if (_material.HasProperty("_SrcBlend"))
            _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (_material.HasProperty("_DstBlend"))
            _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // Ganz zuletzt zeichnen, damit auch Nebel, Wasser und andere
        // transparente Sachen wirklich verdeckt werden.
        _material.renderQueue = 4000;

        GameObject flaeche = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flaeche.name = "Schwarzblende (automatisch erzeugt)";
        flaeche.hideFlags = HideFlags.DontSave;

        // Der Collider des Quads wuerde dem Fahrer sonst direkt vor dem Gesicht
        // im Weg stehen - auch fuer die Bodenabtastung von Fortbewegen.
        var col = flaeche.GetComponent<Collider>();
        if (col != null) Destroy(col);

        flaeche.transform.SetParent(kopf, false);
        flaeche.transform.localPosition = new Vector3(0f, 0f, abstand);
        flaeche.transform.localRotation = Quaternion.identity;

        // Grosszuegig dimensioniert: das Sichtfeld eines Headsets ist breit,
        // und an den Raendern darf nichts vorbeischauen.
        flaeche.transform.localScale = new Vector3(abstand * 6f, abstand * 6f, 1f);

        _renderer = flaeche.GetComponent<Renderer>();
        _renderer.sharedMaterial      = _material;
        _renderer.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows      = false;
        _renderer.lightProbeUsage     = UnityEngine.Rendering.LightProbeUsage.Off;
        _renderer.enabled             = false;
    }

    /// <summary>Abblenden, im dunklen Moment "beimSchwarz" ausfuehren, aufblenden.
    /// Genau dafuer ist die Blende da: das Umsetzen gehoert in beimSchwarz.</summary>
    public void Blenden(System.Action beimSchwarz)
    {
        // Laeuft schon eine Blende, nicht noch eine starten - sonst kaempfen
        // zwei Coroutinen um dieselbe Schwaerze. Die Aktion trotzdem
        // ausfuehren, sie darf auf keinen Fall verschluckt werden.
        if (laeuft || !enabled || !gameObject.activeInHierarchy)
        {
            beimSchwarz?.Invoke();
            return;
        }
        StartCoroutine(Ablauf(beimSchwarz));
    }

    /// <summary>Nur blenden, ohne etwas dazwischen zu tun.
    /// So aus einem UnityEvent im Inspector aufrufbar.</summary>
    public void Blenden() => Blenden(null);

    IEnumerator Ablauf(System.Action beimSchwarz)
    {
        laeuft = true;

        yield return Ueberblenden(schwaerze, 1f, abblendZeit);

        // Der eigentliche Sprung - jetzt, wo niemand etwas sieht.
        beimSchwarz?.Invoke();

        if (schwarzZeit > 0f) yield return new WaitForSeconds(schwarzZeit);

        yield return Ueberblenden(1f, 0f, aufblendZeit);

        laeuft = false;
    }

    IEnumerator Ueberblenden(float von, float nach, float dauer)
    {
        if (dauer <= 0f) { Schwaerze(nach); yield break; }

        float t = 0f;
        while (t < dauer)
        {
            // unscaledDeltaTime, damit die Blende auch dann laeuft, wenn
            // jemand spaeter mal die Zeit anhaelt (Pausemenue).
            t += Time.unscaledDeltaTime;
            Schwaerze(Mathf.Lerp(von, nach, Mathf.Clamp01(t / dauer)));
            yield return null;
        }
        Schwaerze(nach);
    }

    void Schwaerze(float wert)
    {
        schwaerze = Mathf.Clamp01(wert);
        if (_material == null) return;

        Color c = new Color(0f, 0f, 0f, schwaerze);
        if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
        if (_material.HasProperty("_Color"))     _material.SetColor("_Color", c);

        // Bei 0 ganz abschalten: ein voll transparentes Vollbild-Quad kostet
        // in VR sonst in JEDEM Frame Fuellrate, obwohl man nichts davon sieht.
        if (_renderer != null) _renderer.enabled = schwaerze > 0.002f;
    }
}
