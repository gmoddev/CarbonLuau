param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string]$Arguments = '',
    [string]$ChildProcessName = ''
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "Executable not found: $Executable"
}
if ($Arguments.Length -eq 0) {
    $Process = Start-Process -FilePath $Executable -WindowStyle Hidden -PassThru
} else {
    $Process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru
}
$PeakWorkingBytes = [int64]0
$PeakPrivateBytes = [int64]0
$PeakVirtualBytes = [int64]0
$ChildPeakWorkingBytes = [int64]0
$ChildPeakPrivateBytes = [int64]0
$ChildSamples = 0
$Samples = 0
while (-not $Process.HasExited) {
    $Observed = Get-Process -Id $Process.Id -ErrorAction SilentlyContinue
    if ($null -ne $Observed) {
        $PeakWorkingBytes = [Math]::Max($PeakWorkingBytes, [int64]$Observed.PeakWorkingSet64)
        $PeakPrivateBytes = [Math]::Max($PeakPrivateBytes, [int64]$Observed.PeakPagedMemorySize64)
        $PeakVirtualBytes = [Math]::Max($PeakVirtualBytes, [int64]$Observed.PeakVirtualMemorySize64)
        $Samples++
    }
    if ($ChildProcessName.Length -gt 0) {
        foreach ($Child in @(Get-Process -Name $ChildProcessName -ErrorAction SilentlyContinue)) {
            $ChildPeakWorkingBytes = [Math]::Max($ChildPeakWorkingBytes, [int64]$Child.PeakWorkingSet64)
            $ChildPeakPrivateBytes = [Math]::Max($ChildPeakPrivateBytes, [int64]$Child.PeakPagedMemorySize64)
            $ChildSamples++
        }
    }
    Start-Sleep -Milliseconds 25
    $Process.Refresh()
}
$Process.WaitForExit()
Write-Output "exit=$($Process.ExitCode) samples=$Samples peak-working-bytes=$PeakWorkingBytes peak-private-bytes=$PeakPrivateBytes peak-virtual-bytes=$PeakVirtualBytes"
if ($ChildProcessName.Length -gt 0) {
    Write-Output "child-name=$ChildProcessName child-samples=$ChildSamples child-peak-working-bytes=$ChildPeakWorkingBytes child-peak-private-bytes=$ChildPeakPrivateBytes"
}
if ($Process.ExitCode -ne 0) { exit $Process.ExitCode }
