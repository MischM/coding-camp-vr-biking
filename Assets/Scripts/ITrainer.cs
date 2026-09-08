using System;

/// <summary>
/// Ein Messwert-Paket vom Trainer (aus Indoor Bike Data 0x2AD2).
/// struct statt class = wird auf dem Stack kopiert, kein Garbage Collector-Druck.
/// </summary>
public struct TrainerData
{
    public float SpeedKmh;
    public float CadenceRpm;
    public float PowerW;

    public override string ToString()
        => $"{SpeedKmh:F1} km/h | {CadenceRpm:F0} rpm | {PowerW:F0} W";
}

/// <summary>
/// Gemeinsame Schnittstelle für echten Trainer (BleTrainer) und Fake (MockTrainer).
/// Der Rest des Spiels kennt NUR dieses Interface - so kannst du ohne Hardware
/// entwickeln und am Ende eine Zeile umstellen.
///
/// Python-Vergleich: wie eine abstrakte Basisklasse mit @abstractmethod,
/// nur dass C# das zur Compile-Zeit erzwingt.
/// </summary>
public interface ITrainer
{
    /// <summary>Feuert bei jedem neuen Messwert. Abonnieren mit: trainer.OnData += MeineMethode;</summary>
    event Action<TrainerData> OnData;

    /// <summary>True, sobald Request Control + Start durch sind und Steuerbefehle wirken.</summary>
    bool IsReady { get; }

    /// <summary>Steigung in Prozent, z.B. 7.5f für 7.5%. Wird auf ±10% begrenzt (Tuo-Limit).
    /// Darf beliebig oft aufgerufen werden - das Senden wird intern auf 1 Hz gedrosselt.</summary>
    void SetGrade(float gradePercent);
}
