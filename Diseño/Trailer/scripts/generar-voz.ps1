<#
Genera la voz en off del trailer: una linea por archivo, public/voz/NN.wav.

  npm run voz                       todas las lineas, con el perfil de voz/guion.json
  npm run voz -- -Variantes         la linea de prueba con cada perfil (public/voz/variantes/)
  npm run voz -- -Solo 03,04        solo esas lineas

Los textos y los perfiles (voz, velocidad, filtro de ffmpeg) estan en voz/guion.json.
El TTS crudo queda en voz/crudo/ (no se versiona). Motores: winrt (voces OneCore
como Raul y Sabina), sapi (SAPI de 64 bits) y sapi32 (SAPI de 32 bits: Loquendo). Al final mide con ffprobe la
duracion de cada linea y la escribe en public/voz/duraciones.json: esas duraciones
son las que usa el timeline.

Voz manual: si existe voz/manual/NN.wav (por ejemplo, generada con Loquendo por
fuera), esa linea no se sintetiza: se procesa ese archivo (se recortan los
silencios de las puntas, las pausas internas de mas de 0,3 s quedan en 0,3 s y
pasa a 48 kHz mono) y el resultado va a public/voz/NN.wav. El original no se toca.
#>
param(
    [switch]$Variantes,
    [string]$Solo = ''
)
$ErrorActionPreference = 'Stop'

$Raiz = Split-Path -Parent $PSScriptRoot
$Guion = Get-Content -Raw -Encoding UTF8 (Join-Path $Raiz 'voz/guion.json') | ConvertFrom-Json
$DirCrudo = Join-Path $Raiz 'voz/crudo'
$DirVoz = Join-Path $Raiz 'public/voz'
$DirManual = Join-Path $Raiz 'voz/manual'
New-Item -ItemType Directory -Force $DirCrudo, $DirVoz, $DirManual | Out-Null

# Las lineas manuales ya vienen con su voz: solo se limpian y las pausas internas
# largas quedan en `pausaMaxima` segundos
$PerfilManual = [pscustomobject]@{
    nombre      = 'Manual'
    filtro      = 'highpass=f=70,acompressor=threshold=-24dB:ratio=3:attack=5:release=80:makeup=4,alimiter=limit=0.89:level=0'
    pausaMaxima = 0.3
}

# --- ffmpeg / ffprobe: el del PATH o el que instalo winget
function Buscar-Herramienta([string]$nombre) {
    $cmd = Get-Command $nombre -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $paquetes = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    $exe = Get-ChildItem $paquetes -Filter "$nombre.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*Gyan.FFmpeg*' } | Select-Object -First 1
    if ($exe) { return $exe.FullName }
    throw "No encuentro $nombre. Instalalo con: winget install Gyan.FFmpeg"
}
$Ffmpeg = Buscar-Herramienta 'ffmpeg'
$Ffprobe = Buscar-Herramienta 'ffprobe'

# --- TTS de Windows (WinRT: voces OneCore como Raul; SAPI como respaldo)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
Add-Type -AssemblyName System.Speech
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
$AsTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Esperar($operacion, [Type]$tipo) {
    $tarea = $AsTask.MakeGenericMethod($tipo).Invoke($null, @($operacion))
    $tarea.Wait() | Out-Null
    $tarea.Result
}

# Los "..." del guion son pausas. El idioma tiene que ser el de la voz: con SSML,
# System.Speech cambia de voz si no coincide.
function A-Ssml([string]$texto, [string]$idioma) {
    $t = [Security.SecurityElement]::Escape($texto)
    $t = $t.Replace([string][char]0x2026, '...') -replace '\.\.\.', '<break time="350ms"/>'
    "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='$idioma'>$t</speak>"
}

# Voces SAPI de 32 bits (Loquendo): solo las ve el PowerShell de 32 bits, asi que
# la sintesis corre en ese proceso.
function Sintetizar32([string]$ssml, $perfil, [string]$destino) {
    $archivoSsml = [IO.Path]::Combine($DirCrudo, 'ssml-32.xml')
    [IO.File]::WriteAllText($archivoSsml, $ssml, (New-Object Text.UTF8Encoding($false)))
    $rate = [int](($perfil.velocidad - 1) * 10)
    $script = @"
`$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Speech
`$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
`$s.SelectVoice('$($perfil.voz)')
`$s.Rate = $rate
`$s.SetOutputToWaveFile('$destino')
`$s.SpeakSsml([IO.File]::ReadAllText('$archivoSsml', [Text.Encoding]::UTF8))
`$s.Dispose()
"@
    $codificado = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
    & "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -EncodedCommand $codificado
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $destino)) { throw "La voz de 32 bits '$($perfil.voz)' no genero $destino" }
}

function Sintetizar([string]$texto, $perfil, [string]$destino) {
    $idioma = 'es-MX'
    if ($perfil.PSObject.Properties['idioma']) { $idioma = $perfil.idioma }
    $ssml = A-Ssml $texto $idioma
    if ($perfil.motor -eq 'sapi32') {
        Sintetizar32 $ssml $perfil $destino
        return
    }
    if ($perfil.motor -eq 'sapi') {
        $s = New-Object System.Speech.Synthesis.SpeechSynthesizer
        $s.SelectVoice($perfil.voz)
        $s.Rate = [int](($perfil.velocidad - 1) * 10)
        $s.SetOutputToWaveFile($destino)
        $s.SpeakSsml($ssml)
        $s.Dispose()
        return
    }
    $voz = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices |
        Where-Object { $_.DisplayName -like "*$($perfil.voz)*" } | Select-Object -First 1
    if (-not $voz) { throw "No esta instalada la voz '$($perfil.voz)'" }
    $sy = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
    $sy.Voice = $voz
    $sy.Options.SpeakingRate = [double]$perfil.velocidad
    $sy.Options.AudioPitch = [double]$perfil.tono
    $flujo = Esperar ($sy.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $tam = [uint32]$flujo.Size
    $lector = New-Object Windows.Storage.Streams.DataReader($flujo.GetInputStreamAt(0))
    $null = Esperar ($lector.LoadAsync($tam)) ([uint32])
    $bytes = New-Object byte[] $tam
    $lector.ReadBytes($bytes)
    [IO.File]::WriteAllBytes($destino, $bytes)
    $sy.Dispose()
}

# Recorta los silencios de las puntas y aplica el filtro del perfil
function Procesar([string]$crudo, $perfil, [string]$salida) {
    $recorte = 'silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.02,' +
        'areverse,silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05,areverse'
    & $Ffmpeg -y -hide_banner -loglevel error -i $crudo -af "$recorte,$($perfil.filtro)" -ar 48000 -ac 1 -c:a pcm_s16le $salida
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg fallo con $crudo" }
    if ($perfil.PSObject.Properties['pausaMaxima']) { AcortarPausas $salida $perfil.pausaMaxima }
}

# Las pausas internas de mas de $maxima segundos quedan en $maxima: se saca el medio
# de cada una (silencedetect + aselect; el silenceremove de ffmpeg 9 no lo hace).
function AcortarPausas([string]$archivo, [double]$maxima) {
    $c = [Globalization.CultureInfo]::InvariantCulture
    # silencedetect escribe por stderr: que no lo tome como error
    $ErrorActionPreference = 'Continue'
    $log = & $Ffmpeg -hide_banner -nostats -i $archivo -af "silencedetect=n=-45dB:d=$($maxima.ToString($c))" -f null - 2>&1 | Out-String
    $inicios = [regex]::Matches($log, 'silence_start: ([0-9.]+)') | ForEach-Object { [double]::Parse($_.Groups[1].Value, $c) }
    $finales = [regex]::Matches($log, 'silence_end: ([0-9.]+)') | ForEach-Object { [double]::Parse($_.Groups[1].Value, $c) }
    $cortes = @()
    for ($i = 0; $i -lt [math]::Min(@($inicios).Count, @($finales).Count); $i++) {
        $a = @($inicios)[$i] + $maxima / 2
        $b = @($finales)[$i] - $maxima / 2
        if ($b -gt $a) { $cortes += "between(t,$($a.ToString('0.000', $c)),$($b.ToString('0.000', $c)))" }
    }
    if ($cortes.Count -eq 0) { return }
    $tmp = "$archivo.tmp.wav"
    & $Ffmpeg -y -hide_banner -loglevel error -i $archivo -af "aselect='not($($cortes -join '+'))',asetpts=N/SR/TB" -c:a pcm_s16le $tmp
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg fallo acortando pausas de $archivo" }
    Move-Item -Force $tmp $archivo
}

function Duracion([string]$archivo) {
    [double](& $Ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 $archivo)
}

function TextoTts($linea) {
    if ($linea.PSObject.Properties['pronunciacion']) { return $linea.pronunciacion }
    $linea.texto
}

# --- Variantes de una misma linea, para elegir perfil
if ($Variantes) {
    $linea = $Guion.lineas | Where-Object { $_.id -eq $Guion.lineaDePrueba }
    $dir = Join-Path $DirVoz 'variantes'
    New-Item -ItemType Directory -Force $dir | Out-Null
    foreach ($p in $Guion.perfiles.PSObject.Properties) {
        $crudo = Join-Path $DirCrudo "variante-$($p.Name).wav"
        $nombre = ($p.Value.nombre.ToLower() -replace '[^a-z0-9]+', '-')
        $salida = Join-Path $dir "$($p.Name)-$nombre.wav"
        Sintetizar (TextoTts $linea) $p.Value $crudo
        Procesar $crudo $p.Value $salida
        "{0}  {1,-18} {2:N2} s  public/voz/variantes/{3}" -f $p.Name, $p.Value.nombre, (Duracion $salida), (Split-Path -Leaf $salida)
    }
    return
}

# --- Todas las lineas (o las pedidas)
$perfil = $Guion.perfiles.($Guion.perfil)
$pedidas = @($Solo -split '[,\s]+' | Where-Object { $_ })
$generadas = @()
foreach ($linea in $Guion.lineas) {
    if ($pedidas.Count -and $pedidas -notcontains $linea.id) { continue }
    $manual = Join-Path $DirManual "$($linea.id).wav"
    if (Test-Path $manual) {
        Procesar $manual $PerfilManual (Join-Path $DirVoz "$($linea.id).wav")
        continue
    }
    $crudo = Join-Path $DirCrudo "$($linea.id).wav"
    Sintetizar (TextoTts $linea) $perfil $crudo
    $generadas += $linea
}

# Una voz que no esta leyendo el texto (por ejemplo, un Loquendo sin licencia, que
# dice siempre el mismo aviso) da el mismo audio para lineas distintas. Se chequea
# antes de procesar, asi no pisa la voz buena de public/voz/.
$porHash = $generadas | Group-Object { (Get-FileHash (Join-Path $DirCrudo "$($_.id).wav") -Algorithm MD5).Hash } |
    Where-Object { $_.Count -gt 1 }
if ($porHash) {
    $repetidas = ($porHash | ForEach-Object { ($_.Group | ForEach-Object id) -join ', ' }) -join ' / '
    throw "La voz '$($perfil.voz)' devuelve el mismo audio para lineas distintas ($repetidas): no esta leyendo el texto. No se toco public/voz/."
}
foreach ($linea in $generadas) {
    Procesar (Join-Path $DirCrudo "$($linea.id).wav") $perfil (Join-Path $DirVoz "$($linea.id).wav")
}

# --- Duraciones de lo que suena (public/voz/NN.wav)
$fps = 30
$dur = [ordered]@{}
$total = 0.0
$hayManuales = $false
foreach ($linea in $Guion.lineas) {
    $esManual = Test-Path (Join-Path $DirManual "$($linea.id).wav")
    if ($esManual) { $hayManuales = $true }
    $archivo = Join-Path $DirVoz "$($linea.id).wav"
    $rel = "voz/$($linea.id).wav"
    if (-not (Test-Path $archivo)) { "{0}  FALTA {1}" -f $linea.id, $rel; continue }
    $s = Duracion $archivo
    $total += $s
    $dur[$linea.id] = [ordered]@{
        texto   = $linea.texto
        archivo = $rel
        seg     = [math]::Round($s, 3)
        frames  = [int][math]::Ceiling($s * $fps)
        manual  = $esManual
    }
    "{0}  {1,5:N2} s  {2}{3}" -f $linea.id, $s, $linea.texto, $(if ($esManual) { '  (manual)' } else { '' })
}
$salidaJson = [ordered]@{
    perfil = $(if ($hayManuales) { 'manual (voz/manual/)' } else { "$($Guion.perfil) - $($perfil.nombre)" })
    fps    = $fps
    lineas = $dur
}
$json = $salidaJson | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $DirVoz 'duraciones.json'), $json, (New-Object Text.UTF8Encoding($false)))
"Total: {0:N2} s de voz. Escrito public/voz/duraciones.json" -f $total
