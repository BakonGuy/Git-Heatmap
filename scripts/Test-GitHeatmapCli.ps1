param(
    [string]$ExePath = "D:\_AppDev\Utilities\Git-Heatmap\Git-Heatmap\bin\Debug\net9.0-windows\Git-Heatmap.exe",
    [string[]]$CliArgs = @("export", "--png"),
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Executable not found: $ExePath"
}

$timestamp = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss-fff"), $PID
$logRoot = Join-Path "D:\_AppDev\Utilities\Git-Heatmap\tmp-output" "cli-test-$timestamp"
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

$stdoutPath = Join-Path $logRoot "stdout.txt"
$stderrPath = Join-Path $logRoot "stderr.txt"
$summaryPath = Join-Path $logRoot "summary.txt"
$argsTextPath = Join-Path $logRoot "args.txt"

$startTime = Get-Date
$quotedArgs = $CliArgs | ForEach-Object {
    if ($_ -match '\s|["]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
}
$argumentString = [string]::Join(" ", $quotedArgs)

Set-Content -LiteralPath $argsTextPath -Value @(
    "ExePath: $ExePath"
    "Args: $argumentString"
    "TimeoutSeconds: $TimeoutSeconds"
    "Started: $startTime"
)

$beforeIds = @(
    Get-Process -Name "Git-Heatmap","GitHeatmap.Cli" -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty Id
)

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $ExePath
$psi.Arguments = $argumentString
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.WorkingDirectory = [System.IO.Path]::GetDirectoryName($ExePath)

$proc = New-Object System.Diagnostics.Process
$proc.StartInfo = $psi
[void]$proc.Start()

$stdoutTask = $proc.StandardOutput.ReadToEndAsync()
$stderrTask = $proc.StandardError.ReadToEndAsync()

$finished = $proc.WaitForExit($TimeoutSeconds * 1000)
$timedOut = -not $finished

$after = Get-Process -Name "Git-Heatmap","GitHeatmap.Cli" -ErrorAction SilentlyContinue
$newProcesses = @()
if ($after) {
    $newProcesses = $after | Where-Object { $beforeIds -notcontains $_.Id }
}

if ($timedOut) {
    foreach ($p in $newProcesses) {
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch {}
    }

    try { Stop-Process -Id $proc.Id -Force -ErrorAction Stop } catch {}
}

if (-not $proc.HasExited) {
    [void]$proc.WaitForExit(2000)
}

$stdout = ""
$stderr = ""
try { $stdout = $stdoutTask.GetAwaiter().GetResult() } catch {}
try { $stderr = $stderrTask.GetAwaiter().GetResult() } catch {}

Set-Content -LiteralPath $stdoutPath -Value $stdout
Set-Content -LiteralPath $stderrPath -Value $stderr

$endTime = Get-Date
$exitCode = $null
if (-not $timedOut) {
    $exitCode = $proc.ExitCode
}

$processSnapshot = Get-Process -Name "Git-Heatmap","GitHeatmap.Cli" -ErrorAction SilentlyContinue |
    Select-Object Id, ProcessName, StartTime, CPU, WS, MainWindowTitle

$summary = @(
    "Started: $startTime"
    "Ended: $endTime"
    "DurationSeconds: $([math]::Round(($endTime - $startTime).TotalSeconds, 3))"
    "TimedOut: $timedOut"
    "ExitCode: $exitCode"
    "LogRoot: $logRoot"
    ""
    "New processes observed during run:"
)

if ($newProcesses.Count -eq 0) {
    $summary += "  (none)"
} else {
    foreach ($p in $newProcesses) {
        $summary += "  Id=$($p.Id) Name=$($p.ProcessName) Start=$($p.StartTime)"
    }
}

$summary += ""
$summary += "Current Git-Heatmap* processes after cleanup:"
if (-not $processSnapshot) {
    $summary += "  (none)"
} else {
    foreach ($p in $processSnapshot) {
        $summary += "  Id=$($p.Id) Name=$($p.ProcessName) CPU=$($p.CPU) WS=$($p.WS)"
    }
}

Set-Content -LiteralPath $summaryPath -Value $summary

Write-Host "CLI test complete."
Write-Host "Timed out: $timedOut"
Write-Host "Exit code: $exitCode"
Write-Host "Logs: $logRoot"

if ($timedOut) {
    exit 124
}

exit 0
