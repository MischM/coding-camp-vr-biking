# =============================================================================
# patch_dll.ps1
#
# Patcht BleWinrtDll.dll: GattWriteOption::WriteWithoutResponse -> WriteWithResponse
#
# Warum: der FTMS Control Point 0x2AD9 hat nur die Properties Write und Indicate.
# Die DLL schreibt aber fest einkompiliert mit WriteWithoutResponse. Das geht auf
# BLE-Ebene als ATT Write Command raus, wird vom Trainer stillschweigend verworfen
# und meldet trotzdem "Success" zurueck - weil ein Write Command per Definition
# nicht bestaetigt wird.
#
# Geaendert wird GENAU EIN Byte:
#   Datei-Offset 0x127B2  (virtuelle Adresse 0x1800133B2)
#   41 B8 01 00 00 00  ->  41 B8 00 00 00 00
#   = mov r8d, 1       ->  mov r8d, 0
#   (drittes Argument von WriteValueAsync; WriteWithResponse = 0, ohne = 1)
#
# Die Stelle liegt in der Coroutine SendDataAsync (0x13030-0x1364C) und ist
# ausschliesslich ueber den Export SendData erreichbar. Scan, Connect und die
# Subscriptions laufen ueber andere Funktionen und sind nicht betroffen.
#
# Aufruf aus dem Projekt-Wurzelverzeichnis:
#   powershell -ExecutionPolicy Bypass -File patch_dll.ps1
# =============================================================================

param(
    [string]$Dll = "SteigungsSimulator\Assets\BLE_FTMS_IndoorBike\BleWinrtDll.dll"
)

$OFFSET   = 0x127B2
$ERWARTET = @(0x41, 0xB8)   # die zwei Bytes VOR dem Immediate: mov r8d, imm32

if (-not (Test-Path $Dll)) {
    Write-Host "FEHLER: '$Dll' nicht gefunden." -ForegroundColor Red
    Write-Host "Skript aus dem Ordner starten, in dem SteigungsSimulator liegt."
    exit 1
}

$pfad  = (Resolve-Path $Dll).Path
$bytes = [System.IO.File]::ReadAllBytes($pfad)

# --- Sicherheitscheck: steht dort wirklich 'mov r8d, imm32'? ---
if ($bytes[$OFFSET - 2] -ne $ERWARTET[0] -or $bytes[$OFFSET - 1] -ne $ERWARTET[1]) {
    Write-Host "ABBRUCH: an Offset 0x$('{0:X}' -f $OFFSET) steht nicht das erwartete 'mov r8d'." -ForegroundColor Red
    Write-Host "Vermutlich eine andere DLL-Version. Nichts geaendert."
    exit 1
}

switch ($bytes[$OFFSET]) {
    0 {
        Write-Host "Bereits gepatcht - WriteWithResponse ist aktiv. Nichts zu tun." -ForegroundColor Green
        exit 0
    }
    1 {
        # Backup nur beim ersten Mal
        $backup = "$pfad.original"
        if (-not (Test-Path $backup)) {
            Copy-Item $pfad $backup
            Write-Host "Backup angelegt: $backup"
        }

        $bytes[$OFFSET] = 0
        [System.IO.File]::WriteAllBytes($pfad, $bytes)

        Write-Host "OK - gepatcht auf WriteWithResponse." -ForegroundColor Green
        Write-Host "WICHTIG: Unity muss NEU GESTARTET werden. Native DLLs werden"
        Write-Host "beim ersten Play geladen und erst beim Beenden des Editors freigegeben."
        exit 0
    }
    default {
        Write-Host "ABBRUCH: unerwarteter Wert 0x$('{0:X2}' -f $bytes[$OFFSET]) an Offset 0x$('{0:X}' -f $OFFSET)." -ForegroundColor Red
        exit 1
    }
}
