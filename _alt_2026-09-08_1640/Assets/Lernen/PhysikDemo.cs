using UnityEngine;

/// <summary>
/// LERN-SKRIPT. Braucht keinen Trainer, kein Bluetooth, keine Szene-Verdrahtung.
///
/// So benutzt du es:
///   1. Leeres GameObject in der Szene anlegen (Hierarchy -> Rechtsklick -> Create Empty)
///   2. Add Component -> "Physik Demo"
///   3. Oben im Inspector die Werte einstellen
///   4. Drei Punkte am Komponenten-Header -> einen der Menuepunkte waehlen
///   5. Ergebnis steht in der Konsole
///
/// Play-Modus ist nicht noetig - ContextMenu-Methoden laufen auch im Editor.
/// </summary>
public class PhysikDemo : MonoBehaviour
{
    [Header("Situation")]
    [Tooltip("Wie schnell faehrst du?")]
    [Range(5f, 60f)] public float tempoKmh = 25f;

    [Tooltip("Wie steil ist es? Negativ = bergab.")]
    [Range(-10f, 15f)] public float steigungProzent = 3f;

    [Header("Fahrer und Rad")]
    [Range(40f, 120f)] public float fahrerKg = 70f;
    [Range(5f, 25f)] public float radKg = 9f;

    [Header("Koeffizienten")]
    [Tooltip("CdA in m². 0.28 Unterlenker, 0.32 Oberlenker, 0.55 Citybike aufrecht.")]
    [Range(0.15f, 0.70f)] public float cdA = 0.32f;

    [Tooltip("Crr. 0.004 Rennrad Asphalt, 0.006 Gravel, 0.012 MTB Schotter.")]
    [Range(0.002f, 0.0255f)] public float crr = 0.006f;

    [Tooltip("Luftdichte kg/m³. 1.225 = Meereshoehe.")]
    [Range(0.9f, 1.35f)] public float luftdichte = 1.225f;

    // Abkuerzung, damit es unten lesbar bleibt
    private float MasseKg => fahrerKg + radKg;

    // =====================================================================

    /// <summary>
    /// Rechnet Schritt fuer Schritt vor, mit eingesetzten Zahlen.
    /// Das ist der Menuepunkt, mit dem du anfangen solltest.
    /// </summary>
    [ContextMenu("1) Schritt fuer Schritt vorrechnen")]
    public void RechneVor()
    {
        // --- Erst mal alles in SI-Einheiten bringen ---------------------
        // In der Physik rechnet man in Metern, Sekunden, Kilogramm.
        // km/h ist eine Alltagseinheit und muss umgerechnet werden.
        float v = tempoKmh / 3.6f;          // m/s
        float m = MasseKg;                  // kg
        float g = 9.80665f;                 // m/s², Erdbeschleunigung
        float rho = luftdichte;             // kg/m³

        var t = new System.Text.StringBuilder();
        t.AppendLine("================ SCHRITT FUER SCHRITT ================");
        t.AppendLine($"Gegeben:  {tempoKmh:F1} km/h  =  {v:F2} m/s");
        t.AppendLine($"          Masse {m:F0} kg (Fahrer {fahrerKg:F0} + Rad {radKg:F0})");
        t.AppendLine($"          Steigung {steigungProzent:F1} %");
        t.AppendLine($"          CdA {cdA:F2} m²,  Crr {crr:F4},  rho {rho:F3} kg/m³");
        t.AppendLine();

        // --- 1. LUFTWIDERSTAND -----------------------------------------
        // Die Luft, durch die du dich schiebst, wehrt sich. Wie stark,
        // haengt vom Quadrat der Geschwindigkeit ab.
        float fLuft = 0.5f * rho * cdA * v * v;
        float pLuft = fLuft * v;

        t.AppendLine("--- 1. LUFTWIDERSTAND ---");
        t.AppendLine("   Kraft:    F = 0.5 · rho · CdA · v²");
        t.AppendLine($"             F = 0.5 · {rho:F3} · {cdA:F2} · {v:F2}²");
        t.AppendLine($"             F = {fLuft:F1} N");
        t.AppendLine("   Leistung: P = F · v          (Leistung = Kraft mal Geschwindigkeit)");
        t.AppendLine($"             P = {fLuft:F1} · {v:F2}  =  {pLuft:F0} W");
        t.AppendLine("   -> weil v zweimal in F und nochmal in P steckt, waechst P mit v³");
        t.AppendLine();

        // --- 2. ROLLWIDERSTAND -----------------------------------------
        // Der Reifen verformt sich beim Abrollen und frisst dabei Energie.
        // Das haengt am Gewicht, nicht an der Geschwindigkeit.
        float fRoll = crr * m * g;
        float pRoll = fRoll * v;

        t.AppendLine("--- 2. ROLLWIDERSTAND ---");
        t.AppendLine("   Kraft:    F = Crr · m · g");
        t.AppendLine($"             F = {crr:F4} · {m:F0} · {g:F2}  =  {fRoll:F1} N");
        t.AppendLine($"             (konstant - egal wie schnell du faehrst)");
        t.AppendLine("   Leistung: P = F · v");
        t.AppendLine($"             P = {fRoll:F1} · {v:F2}  =  {pRoll:F0} W");
        t.AppendLine();

        // --- 3. STEIGUNG -----------------------------------------------
        // Bergauf hebst du dein Gewicht gegen die Schwerkraft an.
        // Exakt waere m·g·sin(alpha). Bei kleinen Winkeln ist sin(alpha)
        // fast gleich der Steigung in Prozent geteilt durch 100 - bis 15 %
        // liegt der Fehler unter 1 %, das reicht uns.
        float fBerg = m * g * (steigungProzent / 100f);
        float pBerg = fBerg * v;

        t.AppendLine("--- 3. STEIGUNG ---");
        t.AppendLine("   Kraft:    F = m · g · (Steigung% / 100)");
        t.AppendLine($"             F = {m:F0} · {g:F2} · {steigungProzent / 100f:F3}  =  {fBerg:F1} N");
        t.AppendLine("   Leistung: P = F · v");
        t.AppendLine($"             P = {fBerg:F1} · {v:F2}  =  {pBerg:F0} W");
        if (steigungProzent < 0f)
            t.AppendLine("   -> negativ, weil es bergab geht. Die Schwerkraft hilft mit.");
        t.AppendLine();

        // --- SUMME -----------------------------------------------------
        float gesamt = pLuft + pRoll + pBerg;
        t.AppendLine("--- SUMME ---");
        t.AppendLine($"   Luft {pLuft:F0} W  +  Rollen {pRoll:F0} W  +  Steigung {pBerg:F0} W  =  {gesamt:F0} W");
        t.AppendLine();
        t.AppendLine($"   Anteile:  Luft {Anteil(pLuft, gesamt)}  " +
                     $"Rollen {Anteil(pRoll, gesamt)}  Steigung {Anteil(pBerg, gesamt)}");
        t.AppendLine();
        t.AppendLine($"   Zum Vergleich: {Einordnung(gesamt)}");
        t.AppendLine("=====================================================");

        Debug.Log(t.ToString());
    }

    /// <summary>
    /// Zeigt, warum Luftwiderstand so brutal mit dem Tempo waechst.
    /// </summary>
    [ContextMenu("2) Warum Tempo so teuer ist")]
    public void TempoReihe()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("========== LEISTUNG UEBER DEM TEMPO ==========");
        t.AppendLine($"(Ebene, {MasseKg:F0} kg, CdA {cdA:F2}, Crr {crr:F4})");
        t.AppendLine();
        t.AppendLine("  Tempo  |  Luft  | Rollen |  gesamt | Luftanteil");
        t.AppendLine("  -------+--------+--------+---------+-----------");

        for (float kmh = 10f; kmh <= 50f; kmh += 5f)
        {
            float v = kmh / 3.6f;
            float pLuft = 0.5f * luftdichte * cdA * v * v * v;
            float pRoll = crr * MasseKg * 9.80665f * v;
            float ges = pLuft + pRoll;
            t.AppendLine($"  {kmh,2:F0} km/h | {pLuft,5:F0} W | {pRoll,5:F0} W | {ges,6:F0} W | {Anteil(pLuft, ges)}");
        }

        t.AppendLine();
        t.AppendLine("  Von 20 auf 40 km/h: doppeltes Tempo, aber ACHTFACHE Luftleistung.");
        t.AppendLine("  Grund: P = 0.5 · rho · CdA · v³ - und 2³ = 8.");
        t.AppendLine("  Unter ~20 km/h dominiert das Rollen, darueber die Luft.");
        t.AppendLine("=============================================");

        Debug.Log(t.ToString());
    }

    /// <summary>
    /// Vergleicht dieselbe Fahrt auf verschiedenen Steigungen.
    /// </summary>
    [ContextMenu("3) Was kostet ein Huegel?")]
    public void HuegelVergleich()
    {
        float v = tempoKmh / 3.6f;
        float pLuft = 0.5f * luftdichte * cdA * v * v * v;
        float pRoll = crr * MasseKg * 9.80665f * v;
        float basis = pLuft + pRoll;

        var t = new System.Text.StringBuilder();
        t.AppendLine("========== STEIGUNG BEI KONSTANTEM TEMPO ==========");
        t.AppendLine($"({tempoKmh:F0} km/h, {MasseKg:F0} kg)");
        t.AppendLine();
        t.AppendLine("  Steigung |  Steigungsanteil | gesamt");
        t.AppendLine("  ---------+------------------+--------");

        foreach (float grad in new[] { -8f, -4f, -2f, 0f, 2f, 4f, 6f, 8f, 10f })
        {
            float pBerg = MasseKg * 9.80665f * (grad / 100f) * v;
            t.AppendLine($"  {grad,6:F1} % | {pBerg,10:F0} W       | {basis + pBerg,5:F0} W");
        }

        t.AppendLine();
        t.AppendLine($"  Jedes Prozent Steigung kostet bei diesem Tempo rund " +
                     $"{MasseKg * 9.80665f * 0.01f * v:F0} W.");
        t.AppendLine("  Bei doppeltem Gewicht waere es doppelt so viel - deshalb");
        t.AppendLine("  sind leichte Fahrer bergauf im Vorteil, in der Ebene aber nicht.");
        t.AppendLine("==================================================");

        Debug.Log(t.ToString());
    }

    /// <summary>
    /// Zeigt, wie aus den Werten oben die sieben Bytes werden, die per
    /// Bluetooth zum Trainer gehen.
    /// </summary>
    [ContextMenu("4) Was wird zum Trainer geschickt?")]
    public void ZeigeBytes()
    {
        // Cw ist der Parameter, den FTMS fuer den Luftwiderstand vorsieht.
        // Er ist genau der konstante Teil unserer Formel:
        //     F = 0.5 · rho · CdA · v²  =  Cw · v²      mit  Cw = 0.5 · rho · CdA
        float cw = 0.5f * luftdichte * cdA;

        // Jetzt in die FTMS-Einheiten umrechnen und in Bytes packen.
        short grad = (short)Mathf.RoundToInt(steigungProzent * 100f);  // 0.01 %
        short wind = 0;                                                // 0.001 m/s
        byte crrByte = (byte)Mathf.Clamp(Mathf.RoundToInt(crr / 0.0001f), 0, 255);
        byte cwByte = (byte)Mathf.Clamp(Mathf.RoundToInt(cw / 0.01f), 0, 255);

        byte[] paket =
        {
            0x11,
            (byte)(wind & 0xFF), (byte)((wind >> 8) & 0xFF),
            (byte)(grad & 0xFF), (byte)((grad >> 8) & 0xFF),
            crrByte,
            cwByte
        };

        var t = new System.Text.StringBuilder();
        t.AppendLine("========== DAS BLUETOOTH-PAKET ==========");
        t.AppendLine("Opcode 0x11 SetIndoorBikeSimulationParameters, 7 Bytes");
        t.AppendLine();
        t.AppendLine($"  Byte 0    0x11              Befehlsnummer");
        t.AppendLine($"  Byte 1-2  Wind {wind,6}     Einheit 0.001 m/s -> {wind * 0.001f:F3} m/s");
        t.AppendLine($"  Byte 3-4  Steigung {grad,5}  Einheit 0.01 %    -> {grad * 0.01f:F2} %");
        t.AppendLine($"  Byte 5    Crr {crrByte,7}     Einheit 0.0001    -> {crrByte * 0.0001f:F4}");
        t.AppendLine($"  Byte 6    Cw  {cwByte,7}     Einheit 0.01 kg/m -> {cwByte * 0.01f:F2} kg/m");
        t.AppendLine();
        t.AppendLine($"  -> {System.BitConverter.ToString(paket).Replace("-", " ")}");
        t.AppendLine();
        t.AppendLine($"Cw kommt so zustande:  Cw = 0.5 · rho · CdA");
        t.AppendLine($"                       Cw = 0.5 · {luftdichte:F3} · {cdA:F2} = {cw:F3} kg/m");
        t.AppendLine();
        t.AppendLine("Zahlen ueber ein Byte hinaus werden in mehrere Bytes zerlegt,");
        t.AppendLine("niederwertiges zuerst (little-endian). Deshalb sieht die Steigung");
        t.AppendLine("in der Byte-Reihe 'verdreht' aus.");
        t.AppendLine();
        t.AppendLine("Der Trainer speichert diese Koeffizienten und rechnet danach");
        t.AppendLine("SELBST staendig F = Cw · v² aus seiner gemessenen Geschwindigkeit.");
        t.AppendLine("Wir muessen also nicht dauernd nachsenden.");
        t.AppendLine("=========================================");

        Debug.Log(t.ToString());
    }

    // =====================================================================
    // kleine Helfer
    // =====================================================================

    private static string Anteil(float teil, float ganzes)
        => Mathf.Abs(ganzes) < 0.01f ? "  -  " : $"{teil / ganzes * 100f,3:F0} %";

    private static string Einordnung(float watt)
    {
        if (watt < 80f) return "gemuetliches Ausrollen";
        if (watt < 150f) return "lockeres Grundlagentempo";
        if (watt < 220f) return "zuegig, laesst sich stundenlang halten";
        if (watt < 300f) return "anstrengend, ca. eine Stunde durchhaltbar";
        if (watt < 400f) return "hart, wenige Minuten";
        return "Sprint - nur Sekunden";
    }
}
