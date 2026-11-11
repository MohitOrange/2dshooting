# Creates the Play Store upload key for Neon Brawl. Run it once:
#   powershell -ExecutionPolicy Bypass -File Tools\make-upload-key.ps1
# It makes Keystore\neonbrawl-upload.keystore and Keystore\keystore.properties (both git-ignored),
# which BuildTools.BuildPlayStore / BuildFinal use to sign Builds\Android\NeonBrawl.aab.
# The password is typed here and never printed.

$ErrorActionPreference = 'Stop'
$proj = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $proj 'Keystore'
$store = Join-Path $dir 'neonbrawl-upload.keystore'
$props = Join-Path $dir 'keystore.properties'
$keytool = 'D:\Unity\6000.5.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe'
$alias = 'neonbrawl'

if (Test-Path $store) { Write-Host "An upload key already exists: $store (nothing changed)" -ForegroundColor Yellow; exit 1 }
if (-not (Test-Path $keytool)) { Write-Host "keytool not found at $keytool" -ForegroundColor Red; exit 1 }
New-Item -ItemType Directory -Force $dir | Out-Null

function Plain([Security.SecureString]$s) {
    $p = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($p) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($p) }
}

Write-Host ''
Write-Host '  NEON BRAWL - create your Play Store upload key' -ForegroundColor Cyan
Write-Host '  Pick a password (at least 8 characters). Nothing shows while you type - that is normal.'
Write-Host '  Write it down somewhere safe: every future update needs it.'
Write-Host ''
while ($true) {
    $a = Plain (Read-Host '  Password' -AsSecureString)
    $b = Plain (Read-Host '  Same password again' -AsSecureString)
    if ($a -ne $b) { Write-Host '  The two passwords are different. Try again.' -ForegroundColor Red; continue }
    if ($a.Length -lt 8) { Write-Host '  Too short: use at least 8 characters.' -ForegroundColor Red; continue }
    if ($a -ne $a.Trim()) { Write-Host '  No spaces at the start or end, please.' -ForegroundColor Red; continue }
    break
}

$env:NB_UPLOAD_KEY_PASS = $a
try {
    # keytool prints its progress on stderr; Windows PowerShell turns redirected stderr into errors,
    # which 'Stop' would treat as a failure. Let it run, then judge by the exit code.
    $ErrorActionPreference = 'Continue'
    & $keytool -genkeypair -keystore $store -storetype PKCS12 -alias $alias -keyalg RSA -keysize 2048 -validity 10000 `
        -dname 'CN=Neon Brawl, O=MohitGames' -storepass:env NB_UPLOAD_KEY_PASS -keypass:env NB_UPLOAD_KEY_PASS 2>$null | Out-Null
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($code -ne 0 -or -not (Test-Path $store)) { throw "keytool could not create the keystore (exit code $code)." }
    $lines = @(
        '# Play Store upload key for Neon Brawl. NEVER commit or share this file.',
        'storeFile=Keystore/neonbrawl-upload.keystore',
        "storePassword=$a",
        "keyAlias=$alias",
        "keyPassword=$a"
    )
    [IO.File]::WriteAllLines($props, $lines, (New-Object Text.UTF8Encoding($false)))
}
finally {
    Remove-Item Env:NB_UPLOAD_KEY_PASS -ErrorAction SilentlyContinue
    $a = $null; $b = $null
}

Write-Host ''
Write-Host '  Done! Upload key created.' -ForegroundColor Green
Write-Host "  Back up this whole folder to Google Drive or a USB stick: $dir"
Write-Host '  You can close this tab now.'
