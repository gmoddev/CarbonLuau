#!/bin/sh
# Qualification-only Linux process envelope observation. This is an empirical
# high-water sample, not a replacement for the worker's hard RLIMIT_AS.
set -u
"$@" &
Child=$!
PeakVm=0
PeakRss=0
while :; do
    if [ ! -r "/proc/$Child/status" ]; then break; fi
    Reading=$(awk '/^(State:|VmPeak:|VmHWM:)/ { print $1, $2 }' "/proc/$Child/status" 2>/dev/null) || break
    State=$(printf '%s\n' "$Reading" | awk '$1 == "State:" { print $2 }')
    Vm=$(printf '%s\n' "$Reading" | awk '$1 == "VmPeak:" { print $2 }')
    Rss=$(printf '%s\n' "$Reading" | awk '$1 == "VmHWM:" { print $2 }')
    if [ -n "$Vm" ] && [ "$Vm" -gt "$PeakVm" ]; then PeakVm=$Vm; fi
    if [ -n "$Rss" ] && [ "$Rss" -gt "$PeakRss" ]; then PeakRss=$Rss; fi
    if [ "$State" = Z ]; then break; fi
    sleep 0.05
done
wait "$Child"
Status=$?
printf '[CarbonLuau:Persistence] process-memory observed_vmpeak_kib=%s observed_vmhwm_kib=%s exit=%s\n' "$PeakVm" "$PeakRss" "$Status"
exit "$Status"
