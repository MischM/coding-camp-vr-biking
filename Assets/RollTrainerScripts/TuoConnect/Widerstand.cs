using UnityEngine;

/// <summary>
/// Minimalbeispiel zum Verstellen des Widerstands.
/// Auf ein leeres GameObject ziehen, Play druecken, im Inspector am Regler
/// ziehen - der Trainer stellt sich sofort nach.
///
/// Steigung ist dimensionslos: 0.05 = 5 %, 0.10 = Maximum des Tuo.
/// </summary>
public class Widerstand : MonoBehaviour
{
    [Range(-0.1f, 0.1f)] public float steigung = 0f;

    Trainer _t;

    void Start() => _t = FindFirstObjectByType<Trainer>();

    void Update()
    {
        // Der Trainer legt sich selbst an, existiert im ersten Frame aber
        // vielleicht noch nicht - darum jedes Mal kurz nachfassen.
        if (_t == null) _t = FindFirstObjectByType<Trainer>();
        if (_t != null) _t.SetResistance(steigung);
    }
}
