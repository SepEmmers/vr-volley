<#
.SYNOPSIS
  Run Unity in batch mode for compile checks, tests, and builds. The Editor must be CLOSED
  (Unity locks the project while it's open).

.EXAMPLE
  .\unity-batch.ps1 compile
  .\unity-batch.ps1 test-editmode
  .\unity-batch.ps1 test-playmode
  .\unity-batch.ps1 build-android
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('compile', 'test-editmode', 'test-playmode', 'build-android')]
    [string]$Command
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
while ($root -and -not (Test-Path (Join-Path $root 'ProjectSettings\ProjectVersion.txt'))) {
    $root = Split-Path $root -Parent
}
if (-not $root) { throw 'Unity project root not found.' }

$version = ((Get-Content (Join-Path $root 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1) -split ':\s*')[1].Trim()
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $version not installed at $unity" }

if (Test-Path (Join-Path $root 'Temp\UnityLockfile')) {
    $locked = $false
    try { $fs = [IO.File]::Open((Join-Path $root 'Temp\UnityLockfile'), 'Open', 'ReadWrite', 'None'); $fs.Close() } catch { $locked = $true }
    if ($locked) { throw 'The Unity Editor has this project open. Close it or use the MCP for Unity tools instead.' }
}

$logDir = Join-Path $root 'Logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$log = Join-Path $logDir "batch_${Command}_$stamp.log"

function Q([string]$s) { '"' + $s + '"' }   # Start-Process (PS 5) does not quote array args

$unityArgs = @('-batchmode', '-nographics', '-projectPath', (Q $root), '-logFile', (Q $log))
switch ($Command) {
    'compile'       { $unityArgs += @('-quit') }
    'test-editmode' { $unityArgs += @('-runTests', '-testPlatform', 'EditMode', '-testResults', (Q (Join-Path $logDir "tests_editmode_$stamp.xml"))) }
    'test-playmode' { $unityArgs += @('-runTests', '-testPlatform', 'PlayMode', '-testResults', (Q (Join-Path $logDir "tests_playmode_$stamp.xml"))) }
    'build-android' { $unityArgs += @('-quit', '-buildTarget', 'Android', '-executeMethod', 'VRVolley.Editor.BuildScript.BuildAndroid') }
}

Write-Host "Running Unity $version ($Command)... log: $log"
$p = Start-Process -FilePath $unity -ArgumentList ($unityArgs -join ' ') -Wait -PassThru -NoNewWindow
$code = $p.ExitCode

$errors = Select-String -Path $log -Pattern 'error CS\d+|Exception:|Test .* failed|Build Failed|Compilation failed' -ErrorAction SilentlyContinue
if ($errors) {
    Write-Host "---- Errors ($($errors.Count)) ----"
    $errors | Select-Object -First 50 | ForEach-Object { Write-Host $_.Line.Trim() }
}

$results = Get-ChildItem $logDir -Filter "tests_*_$stamp.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($results) {
    [xml]$x = Get-Content $results.FullName
    $run = $x.'test-run'
    Write-Host ("Tests: {0} passed, {1} failed, {2} skipped  ({3})" -f $run.passed, $run.failed, $run.skipped, $results.Name)
}

Write-Host "Exit code: $code"
exit $code
