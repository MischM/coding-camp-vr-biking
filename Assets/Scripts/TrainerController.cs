using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bindeglied zwischen Trainer und UI. Ziehe im Inspector ENTWEDER das
/// BleTrainer- ODER das MockTrainer-GameObject in "trainerSource".
/// Der Rest des Codes weiß nicht, welches davon es ist - das ist der Sinn von ITrainer.
/// </summary>
public class TrainerController : MonoBehaviour
{
    [Tooltip("GameObject mit BleTrainer oder MockTrainer drauf")]
    public MonoBehaviour trainerSource;

    [Header("UI (optional)")]
    public Text infoText;
    public Text gradeText;
    public Slider gradeSlider;      // Range im Inspector: -10 bis 10

    private ITrainer _trainer;

    private void Start()
    {
        _trainer = trainerSource as ITrainer;
        if (_trainer == null)
        {
            Debug.LogError("trainerSource implementiert kein ITrainer!");
            return;
        }

        _trainer.OnData += HandleData;

        if (gradeSlider != null)
            gradeSlider.onValueChanged.AddListener(OnSliderChanged);

        // Beim echten Trainer die Verbindung starten.
        if (_trainer is BleTrainer ble) ble.Connect();
    }

    private void OnDestroy()
    {
        if (_trainer != null) _trainer.OnData -= HandleData;
    }

    private void HandleData(TrainerData d)
    {
        if (infoText != null)
            infoText.text = d.ToString();
    }

    /// <summary>Slider darf beliebig schnell feuern - BleTrainer drosselt selbst auf 1 Hz.</summary>
    public void OnSliderChanged(float value)
    {
        _trainer.SetGrade(value);
        if (gradeText != null) gradeText.text = $"Steigung: {value:F1} %";
    }

    // ---------------------------------------------------------------
    // Später für die Strecke: Durchschnittssteigung aus Distanz + Höhenmetern.
    // Beispiel: 1200 m Strecke, 84 Höhenmeter -> 84/1200*100 = 7.0 %
    // ---------------------------------------------------------------
    public static float AverageGrade(float distanceMeters, float elevationGainMeters)
    {
        if (distanceMeters <= 0f) return 0f;
        return elevationGainMeters / distanceMeters * 100f;
    }

    /// <summary>Steigung aus einer Strecke an Position x setzen.</summary>
    public void ApplySegment(float segmentLengthMeters, float segmentElevationMeters)
    {
        _trainer.SetGrade(AverageGrade(segmentLengthMeters, segmentElevationMeters));
    }
}
