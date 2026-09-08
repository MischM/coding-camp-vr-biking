using UnityEngine;

/// <summary>
/// Fahrradphysik in SI-Einheiten. Reine Rechnerei, keine Unity-Objekte,
/// keine Bluetooth-Abhaengigkeit - dadurch testbar und ueberall verwendbar.
///
/// WICHTIG ZUM VERSTAENDNIS:
/// Der Trainer rechnet den Luftwiderstand SELBST. Wir schicken ihm mit
/// Opcode 0x11 nur die Koeffizienten (Crr und Cw), er misst laufend die
/// Geschwindigkeit und stellt die Magnetbremse nach. Das passiert intern
/// im Trainer und ist NICHT an unser 1-Hz-Sendelimit gebunden.
///
/// Die Formeln hier dienen deshalb zwei Zwecken:
///   1. Aus verstaendlichen Groessen (CdA, Masse, Crr) die FTMS-Bytes bauen
///   2. Vorhersagen, wieviel Watt eine Situation kosten wird - fuer die UI,
///      fuers Kalibrieren und um zu pruefen, ob der Trainer plausibel bremst
///
/// Python-Vergleich: das hier waere ein Modul mit lauter Funktionen und
/// Konstanten. "static class" heisst in C#: man kann davon kein Objekt
/// erzeugen, man ruft nur RidePhysics.AeroPowerW(...) auf.
/// </summary>
public static class RidePhysics
{
    // =====================================================================
    // Naturkonstanten
    // =====================================================================

    /// <summary>Erdbeschleunigung in m/s².</summary>
    public const float G = 9.80665f;

    /// <summary>Luftdichte auf Meereshoehe bei 15 °C, in kg/m³.</summary>
    public const float RhoSeaLevel = 1.225f;

    /// <summary>Luftdichte auf ca. 500 m (Schweizer Mittelland), in kg/m³.</summary>
    public const float Rho500m = 1.167f;

    // =====================================================================
    // Typische CdA-Werte (Stirnflaeche × Luftwiderstandsbeiwert, in m²)
    //
    // CdA ist die Groesse, die den Luftwiderstand bestimmt. Sie haengt fast
    // nur von der Sitzposition ab, kaum vom Rad.
    // =====================================================================

    public const float CdA_Zeitfahrposition = 0.22f;
    public const float CdA_Rennrad_Unterlenker = 0.28f;
    public const float CdA_Rennrad_Oberlenker = 0.32f;   // Standard fuer uns
    public const float CdA_Gravel_Aufrecht = 0.40f;
    public const float CdA_Citybike_Aufrecht = 0.55f;

    // =====================================================================
    // Typische Rollwiderstandsbeiwerte Crr (dimensionslos)
    // =====================================================================

    public const float Crr_Rennrad_Asphalt = 0.004f;
    public const float Crr_Gravel_Asphalt = 0.006f;    // Standard fuer uns
    public const float Crr_MTB_Schotter = 0.012f;

    // =====================================================================
    // Einheiten
    // =====================================================================

    public static float KmhToMs(float kmh) => kmh / 3.6f;
    public static float MsToKmh(float ms) => ms * 3.6f;

    // =====================================================================
    // Die drei Widerstaende
    // =====================================================================

    /// <summary>
    /// LUFTWIDERSTAND — steigt QUADRATISCH mit der Geschwindigkeit.
    ///
    ///     F_luft = 0.5 · rho · CdA · v²        [Newton]
    ///
    /// Doppelte Geschwindigkeit = vierfache Kraft. Das ist der Grund, warum
    /// schnell fahren so unverhaeltnismaessig anstrengend wird.
    /// </summary>
    public static float AeroForceN(float cdA, float airDensity, float speedMs)
        => 0.5f * airDensity * cdA * speedMs * speedMs;

    /// <summary>
    /// Leistung gegen den Luftwiderstand. Leistung = Kraft × Geschwindigkeit,
    /// also steigt sie KUBISCH:
    ///
    ///     P_luft = 0.5 · rho · CdA · v³        [Watt]
    ///
    /// Beispiel Rennrad, CdA 0.32, rho 1.225:
    ///     20 km/h ->   34 W
    ///     30 km/h ->  113 W
    ///     40 km/h ->  269 W
    ///     50 km/h ->  525 W
    /// </summary>
    public static float AeroPowerW(float cdA, float airDensity, float speedMs)
        => AeroForceN(cdA, airDensity, speedMs) * speedMs;

    /// <summary>
    /// ROLLWIDERSTAND — steigt LINEAR mit der Geschwindigkeit.
    ///
    ///     F_roll = Crr · m · g                 [Newton]
    ///     P_roll = Crr · m · g · v             [Watt]
    ///
    /// Bei niedrigem Tempo dominiert er, ab ca. 20 km/h uebernimmt die Luft.
    /// </summary>
    public static float RollingForceN(float crr, float totalMassKg)
        => crr * totalMassKg * G;

    public static float RollingPowerW(float crr, float totalMassKg, float speedMs)
        => RollingForceN(crr, totalMassKg) * speedMs;

    /// <summary>
    /// STEIGUNGSWIDERSTAND — proportional zur Steigung und zur Masse.
    ///
    ///     F_berg = m · g · sin(alpha)  ≈  m · g · (Steigung% / 100)
    ///     P_berg = F_berg · v                  [Watt]
    ///
    /// Die Naeherung sin(alpha) ≈ tan(alpha) stimmt bis ca. 15 % auf unter
    /// 1 % genau - fuer uns also exakt genug.
    ///
    /// Bei negativer Steigung wird das Ergebnis negativ (Bergab hilft).
    /// </summary>
    public static float ClimbForceN(float gradePercent, float totalMassKg)
        => totalMassKg * G * (gradePercent / 100f);

    public static float ClimbPowerW(float gradePercent, float totalMassKg, float speedMs)
        => ClimbForceN(gradePercent, totalMassKg) * speedMs;

    /// <summary>
    /// Gesamtleistung, die der Fahrer bei dieser Situation treten muss.
    /// Kann negativ werden (steiles Gefaelle) - dann rollt man ohne Treten.
    /// </summary>
    public static float TotalPowerW(float speedMs, float gradePercent, float totalMassKg,
                                    float crr, float cdA, float airDensity)
        => AeroPowerW(cdA, airDensity, speedMs)
         + RollingPowerW(crr, totalMassKg, speedMs)
         + ClimbPowerW(gradePercent, totalMassKg, speedMs);

    // =====================================================================
    // Umrechnung in die FTMS-Bytes von Opcode 0x11
    // =====================================================================

    /// <summary>
    /// Rechnet CdA in den FTMS-Parameter "Wind Resistance Coefficient" um.
    ///
    ///     Cw = 0.5 · rho · CdA                 [kg/m]
    ///
    /// Der Trainer bildet daraus intern wieder F = Cw · v², also genau den
    /// quadratischen Luftwiderstand von oben.
    ///
    /// ACHTUNG: Die FTMS-Spezifikation nennt nur die Einheit kg/m, nicht die
    /// Formel. Manche Firmware rechnet mit Cw = rho · CdA (also doppelt so
    /// gross). Falls sich der Luftwiderstand am echten Geraet zu schwach
    /// anfuehlt, ist das der erste Verdaechtige - dafuer gibt es im BleTrainer
    /// den Regler "windCoefficientScale".
    /// </summary>
    public static float CwFromCdA(float cdA, float airDensity)
        => 0.5f * airDensity * cdA;

    /// <summary>Rueckrechnung, damit man im Inspector sieht, was wirklich gesendet wird.</summary>
    public static float CdAFromCw(float cwKgPerM, float airDensity)
        => airDensity > 0f ? cwKgPerM / (0.5f * airDensity) : 0f;

    /// <summary>Crr -> uint8, Aufloesung 0.0001. Maximum also 0.0255.</summary>
    public static byte EncodeCrr(float crr)
        => (byte)Mathf.Clamp(Mathf.RoundToInt(crr / 0.0001f), 0, 255);

    /// <summary>Cw -> uint8, Aufloesung 0.01 kg/m. Maximum also 2.55 kg/m.</summary>
    public static byte EncodeCw(float cwKgPerM)
        => (byte)Mathf.Clamp(Mathf.RoundToInt(cwKgPerM / 0.01f), 0, 255);

    public static float DecodeCrr(byte raw) => raw * 0.0001f;
    public static float DecodeCw(byte raw) => raw * 0.01f;

    /// <summary>Steigung in Prozent -> sint16, Aufloesung 0.01 %.</summary>
    public static short EncodeGrade(float gradePercent)
        => (short)Mathf.Clamp(Mathf.RoundToInt(gradePercent * 100f), short.MinValue, short.MaxValue);

    // =====================================================================
    // Hilfe beim Kalibrieren
    // =====================================================================

    /// <summary>
    /// Welche Steigung erzeugt bei gegebener Geschwindigkeit dieselbe
    /// Zusatzleistung wie ein Luftwiderstand? Nuetzlich, um ein Gefuehl fuer
    /// die Groessenordnungen zu bekommen.
    /// </summary>
    public static float GradeEquivalentOfPower(float powerW, float totalMassKg, float speedMs)
        => (speedMs > 0.1f && totalMassKg > 0f)
            ? powerW / (totalMassKg * G * speedMs) * 100f
            : 0f;

    /// <summary>Mehrzeilige Tabelle fuers Log: Watt-Bedarf bei typischen Tempi.</summary>
    public static string PowerTable(float gradePercent, float totalMassKg,
                                    float crr, float cdA, float airDensity)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Leistungsbedarf bei {gradePercent:F1} % Steigung, {totalMassKg:F0} kg, " +
                      $"CdA {cdA:F2} m², Crr {crr:F4}:");
        foreach (float kmh in new[] { 10f, 15f, 20f, 25f, 30f, 35f, 40f })
        {
            float v = KmhToMs(kmh);
            sb.AppendLine($"   {kmh,2:F0} km/h : {TotalPowerW(v, gradePercent, totalMassKg, crr, cdA, airDensity),5:F0} W" +
                          $"   (Luft {AeroPowerW(cdA, airDensity, v),4:F0} W," +
                          $" Roll {RollingPowerW(crr, totalMassKg, v),3:F0} W," +
                          $" Berg {ClimbPowerW(gradePercent, totalMassKg, v),4:F0} W)");
        }
        return sb.ToString();
    }
}
