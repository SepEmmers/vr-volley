<#
.SYNOPSIS
  Quest device helper for the VR Volley project. Uses Unity's bundled adb (no global SDK needed).

.EXAMPLE
  .\quest-adb.ps1 devices
  .\quest-adb.ps1 install .\Builds\VRVolley.apk
  .\quest-adb.ps1 launch
  .\quest-adb.ps1 fps
  .\quest-adb.ps1 logcat
  .\quest-adb.ps1 refresh 90
  .\quest-adb.ps1 stop
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('devices', 'install', 'launch', 'stop', 'fps', 'logcat', 'refresh', 'screenshot', 'adb')]
    [string]$Command,

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'
$PackageId = 'com.belvok.volleyvr'

function Get-ProjectRoot {
    $dir = $PSScriptRoot
    while ($dir -and -not (Test-Path (Join-Path $dir 'ProjectSettings\ProjectVersion.txt'))) {
        $dir = Split-Path $dir -Parent
    }
    if (-not $dir) { throw 'Could not locate the Unity project root (ProjectSettings\ProjectVersion.txt).' }
    return $dir
}

function Get-Adb {
    $onPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    $root = Get-ProjectRoot
    $versionLine = Get-Content (Join-Path $root 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1
    $version = ($versionLine -split ':\s*')[1].Trim()
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$version\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
    ) + (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' })

    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    throw "adb.exe not found. Install the Android Build Support module (with SDK & NDK) for Unity $version."
}

$adb = Get-Adb

switch ($Command) {
    'devices'    { & $adb devices -l }
    'install'    {
        if (-not $Rest) { throw 'Usage: install <path-to-apk>' }
        & $adb install -r -g $Rest[0]
    }
    'launch'     { & $adb shell monkey -p $PackageId -c android.intent.category.LAUNCHER 1 | Out-Null; "Launched $PackageId" }
    'stop'       { & $adb shell am force-stop $PackageId; "Stopped $PackageId" }
    'fps'        { & $adb logcat -c; & $adb logcat -s VrApi }
    'logcat'     { & $adb logcat -c; & $adb logcat -s Unity:V CRASH:E AndroidRuntime:E DEBUG:E libc:F }
    'refresh'    {
        $hz = if ($Rest) { $Rest[0] } else { '72' }
        & $adb shell setprop debug.oculus.refreshRate $hz
        "Requested refresh rate $hz Hz (test-only; restart the app). Production code must set it via OVRManager / OpenXR."
    }
    'screenshot' {
        $out = Join-Path (Get-ProjectRoot) ("Logs\quest_{0:yyyyMMdd_HHmmss}.png" -f (Get-Date))
        & $adb shell screencap -p /sdcard/vrvolley_cap.png
        & $adb pull /sdcard/vrvolley_cap.png $out | Out-Null
        & $adb shell rm /sdcard/vrvolley_cap.png
        "Saved $out"
    }
    'adb'        { & $adb @Rest }
}
