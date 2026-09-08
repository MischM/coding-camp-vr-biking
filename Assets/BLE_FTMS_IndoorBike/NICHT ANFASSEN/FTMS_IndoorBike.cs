// ============================================================================
// Diese Klasse ist ERSETZT durch Assets/Scripts/BleTrainer.cs.
//
// Der alte Code hatte fuenf Fehler, die verhindert haben, dass der Tuo den
// Widerstand aendert:
//   1. Nur 0x00 (Request Control) gesendet, aber nie 0x07 (Start).
//   2. Keine Wartezeit zwischen den Befehlen - SendData ist asynchron.
//   3. Slider-Rohwert landete in den Steigungs-Bytes (Einheit 0.01%),
//      50 am Slider bedeutete also 0.50% Steigung. Dazu Crr=0 und Cw=0.
//   4. SubscribeCharacteristic_Write bei JEDEM Write statt einmal beim Verbinden.
//   5. Rate-Limit 0.1s statt 1.0s -> BLE-Verbindung bricht ab.
//
// Die Datei bleibt nur leer bestehen, damit Unity die .meta-Datei und damit
// die GUID nicht verwirft. Loeschen ist unproblematisch, sobald das Projekt
// einmal sauber laeuft.
// ============================================================================
