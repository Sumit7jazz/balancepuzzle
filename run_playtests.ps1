<#
.SYNOPSIS
  Headless PlayMode test runner for the Balance Puzzle (Stage 1) project.

.DESCRIPTION
  Locates the installed Unity 6 executable, runs the PlayMode suite
  (Assets/Tests/PlayMode/AutomatedBalanceBotTest.cs) in -batchmode -nographics,
  then checks the exit code plus AI_Test_Report.json and prints
  TESTS PASSED or TESTS FAILED.

  Exact command (run from the Unity project root, i.e. the folder holding
  Assets/, Packages/ and ProjectSettings/):

    powershell -ExecutionPolicy Bypass -File .\run_playtests.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectRoot

$testResults = Join-Path $projectRoot 'test_results.xml'
$unityLog    = Join-Path $projectRoot 'unity_test_run.log'
$aiReport    = Join-Path $projectRoot 'AI_Test_Report.json'

# ---- 1. Locate Unity.exe -----------------------------------------------------
function Find-UnityExecutable {
  # a) Explicit override: $env:UNITY_PATH or -UnityPath style env
  if ($env:UNITY_PATH -and (Test-Path $env:UNITY_PATH)) {
    return $env:UNITY_PATH
  }

  # b) Unity Hub installs: prefer the version pinned in ProjectVersion.txt
  $pinned = $null
  $versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
  if (Test-Path $versionFile) {
    $m = Select-String -Path $versionFile -Pattern 'm_EditorVersion:\s*(\S+)' | Select-Object -First 1
    if ($m) { $pinned = $m.Matches[0].Groups[1].Value }
  }

  $hubRoot = 'C:\Program Files\Unity\Hub\Editor'
  if (Test-Path $hubRoot) {
    $candidates = Get-ChildItem -Path $hubRoot -Directory |
      Where-Object { Test-Path (Join-Path $_.FullName 'Editor\Unity.exe') }
    if ($candidates) {
      if ($pinned) {
        $exact = $candidates | Where-Object { $_.Name -eq $pinned } | Select-Object -First 1
        if ($exact) { return Join-Path $exact.FullName 'Editor\Unity.exe' }
        Write-Warning "Pinned Unity $pinned not found under Hub; falling back to newest 6000.x."
      }
      $best = $candidates |
        Where-Object { $_.Name -like '6000.*' } |
        Sort-Object Name -Descending |
        Select-Object -First 1
      if (-not $best) { $best = $candidates | Sort-Object Name -Descending | Select-Object -First 1 }
      return Join-Path $best.FullName 'Editor\Unity.exe'
    }
  }

  # c) Classic default install
  $classic = 'C:\Program Files\Unity\Editor\Unity.exe'
  if (Test-Path $classic) { return $classic }

  return $null
}

$unityPath = Find-UnityExecutable
if (-not $unityPath) {
  Write-Error 'Unity.exe not found. Set $env:UNITY_PATH or install Unity 6 via Unity Hub.'
  exit 2
}
Write-Host "Unity: $unityPath" -ForegroundColor Cyan
& $unityPath -version 2>$null | Select-Object -First 1 | ForEach-Object { Write-Host "Version: $_" -ForegroundColor Cyan }

# ---- Sanity checks -----------------------------------------------------------
if (-not (Test-Path (Join-Path $projectRoot 'Assets\Tests\PlayMode\AutomatedBalanceBotTest.cs'))) {
  Write-Error 'AutomatedBalanceBotTest.cs missing — Step B was not completed.'
  exit 2
}
$manifest = Join-Path $projectRoot 'Packages\manifest.json'
if ((Test-Path $manifest) -and -not (Select-String -Path $manifest -Pattern 'com.unity.test-framework' -Quiet)) {
  Write-Warning 'com.unity.test-framework not in Packages/manifest.json — install it first.'
}

# ---- Project lock guard -------------------------------------------------------
# A batch run cannot open the project while the editor holds it: Unity aborts
# immediately (surfaces as exit code -1 and no test output). Fail fast with a
# clear message instead. A lockfile with NO Unity process behind it is stale
# (crash/kill leftover) and safe to remove.
$lockFile = Join-Path $projectRoot 'Temp\UnityLockfile'
$unityRunning = Get-Process Unity -ErrorAction SilentlyContinue
if ($unityRunning -and (Test-Path $lockFile)) {
  Write-Error ('A Unity editor is running with this project (PIDs: {0}). ' -f
    (($unityRunning | ForEach-Object { $_.Id }) -join ', ') +
    'Close the Unity editor first, then re-run this script. Aborting to avoid exit code -1.')
  exit 2
}
if ((Test-Path $lockFile) -and (-not $unityRunning)) {
  Write-Warning 'Removing stale Temp/UnityLockfile (no Unity process running).'
  Remove-Item $lockFile -Force
}

# Stale report from a previous run must not greenwash this one.
if (Test-Path $aiReport) { Remove-Item $aiReport -Force }

# ---- 2. Run PlayMode tests headless ------------------------------------------
Write-Host 'Running PlayMode tests (batchmode, nographics)...' -ForegroundColor Yellow
# Start-Process (not &) so Unity's real exit code is captured: the call
# operator misreports it as -1 from non-console hosts and returns before
# teardown flushes AI_Test_Report.json.
$unityArgs = @('-runTests', '-projectPath', $projectRoot, '-testPlatform', 'PlayMode',
  '-testResults', $testResults, '-batchmode', '-nographics', '-logFile', $unityLog)
$unityExit = (Start-Process -FilePath $unityPath -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow).ExitCode
Write-Host "Unity exit code: $unityExit"

# ---- 3. Verdict ---------------------------------------------------------------
$reportStatus = $null
if (Test-Path $aiReport) {
  try {
    $reportStatus = (Get-Content $aiReport -Raw | ConvertFrom-Json).status
    Write-Host "AI_Test_Report.json status: $reportStatus"
  } catch {
    Write-Warning "AI_Test_Report.json exists but could not be parsed: $($_.Exception.Message)"
  }
} else {
  Write-Warning 'AI_Test_Report.json was NOT generated.'
}

if (($unityExit -eq 0) -and ($reportStatus -eq 'PASSED')) {
  Write-Host 'TESTS PASSED' -ForegroundColor Green
  exit 0
} else {
  Write-Host 'TESTS FAILED - Check AI_Test_Report.json' -ForegroundColor Red
  if (Test-Path $testResults) { Write-Host "NUnit results : $testResults" }
  Write-Host "Unity log     : $unityLog"
  Write-Host "AI report     : $aiReport"
  exit 1
}
