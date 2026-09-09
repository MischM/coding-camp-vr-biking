using System.Globalization;
using UnityEngine;

/// <summary>
/// Minimale Bedienflaeche: verbinden, Steigung eintippen, Messwerte sehen.
/// Auf dasselbe GameObject wie Trainer ziehen. Braucht keinen Canvas (IMGUI).
/// </summary>
[RequireComponent(typeof(Trainer))]
public class TrainerUI : MonoBehaviour
{
    Trainer _t;
    string _eingabe = "0";

    void Awake() => _t = GetComponent<Trainer>();

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 300, 250), GUI.skin.box);

        GUILayout.Label("Status: " + _t.Status);
        if (GUILayout.Button("Verbinden", GUILayout.Height(28))) _t.Connect();

        GUILayout.Space(12);
        GUILayout.Label("Steigung (dimensionslos, 0.1 = Maximum)");
        GUILayout.BeginHorizontal();
        _eingabe = GUILayout.TextField(_eingabe, GUILayout.Width(110));
        if (GUILayout.Button("Setzen") &&
            float.TryParse(_eingabe.Replace(',', '.'), NumberStyles.Float,
                           CultureInfo.InvariantCulture, out float g))
            _t.SetResistance(g);
        GUILayout.EndHorizontal();

        GUILayout.Space(12);
        GUILayout.Label("Speed    " + _t.Speed.ToString("F1")   + " km/h");
        GUILayout.Label("Cadence  " + _t.Cadence.ToString("F0") + " rpm");
        GUILayout.Label("Power    " + _t.Power.ToString("F0")   + " W");

        GUILayout.EndArea();
    }
}
