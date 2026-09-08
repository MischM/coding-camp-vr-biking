using System;
using UnityEngine;

/// <summary>
/// Fake-Trainer ohne Hardware. Erzeugt plausible Werte, die auf die
/// gesetzte Steigung reagieren: mehr Steigung -> langsamer, mehr Watt.
/// Damit könnt ihr zu zweit parallel arbeiten, auch wenn nur einer den Tuo hat.
/// </summary>
public class MockTrainer : MonoBehaviour, ITrainer
{
    public event Action<TrainerData> OnData;
    public bool IsReady => true;

    [Header("Simulation")]
    public float flatSpeedKmh = 30f;      // Tempo bei 0% Steigung
    public float updateHz = 4f;
    public float noise = 0.6f;

    private float _grade;
    private float _nextUpdate;

    public void SetGrade(float gradePercent)
    {
        _grade = Mathf.Clamp(gradePercent, -10f, 10f);
    }

    private void Update()
    {
        if (Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + 1f / updateHz;

        // Grobe, aber glaubwürdige Faustformel:
        // pro Prozent Steigung ca. 2 km/h langsamer, Minimum 5 km/h.
        float speed = Mathf.Max(5f, flatSpeedKmh - _grade * 2f);
        float cadence = Mathf.Clamp(speed * 2.7f, 50f, 105f);

        // Leistung = Steigungsanteil + Rollen/Luft. m = 80 kg gesamt.
        float v = speed / 3.6f;                          // m/s
        float pClimb = 80f * 9.81f * (_grade / 100f) * v;
        float pRoll = 80f * 9.81f * 0.005f * v;
        float pAir = 0.5f * 1.225f * 0.4f * v * v * v;
        float power = Mathf.Max(0f, pClimb + pRoll + pAir);

        var d = new TrainerData
        {
            SpeedKmh = speed + UnityEngine.Random.Range(-noise, noise),
            CadenceRpm = cadence + UnityEngine.Random.Range(-noise, noise),
            PowerW = power + UnityEngine.Random.Range(-noise * 5f, noise * 5f)
        };
        OnData?.Invoke(d);
    }
}
