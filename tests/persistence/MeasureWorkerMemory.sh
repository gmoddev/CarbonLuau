#!/bin/sh
# Qualification-only Linux sampling of the actual supervised storage children.
# Run inside an isolated test container; process names are not an authority API.
set -u
"$@" &
Parent=$!
PeakVm=0
PeakRss=0
Samples=0
while :; do
    for Child in $(ps -e -o pid=,comm= | awk '$2 == "carbonluau_stor" { print $1 }'); do
        if [ ! -r "/proc/$Child/status" ]; then continue; fi
        Reading=$(awk '/^(State:|VmPeak:|VmHWM:)/ { print $1, $2 }' "/proc/$Child/status" 2>/dev/null) || continue
        State=$(printf '%s\n' "$Reading" | awk '$1 == "State:" { print $2 }')
        if [ "$State" = Z ]; then continue; fi
        Vm=$(printf '%s\n' "$Reading" | awk '$1 == "VmPeak:" { print $2 }')
        Rss=$(printf '%s\n' "$Reading" | awk '$1 == "VmHWM:" { print $2 }')
        if [ -n "$Vm" ] && [ "$Vm" -gt "$PeakVm" ]; then PeakVm=$Vm; fi
        if [ -n "$Rss" ] && [ "$Rss" -gt "$PeakRss" ]; then PeakRss=$Rss; fi
        Samples=$((Samples + 1))
    done
    if [ ! -r "/proc/$Parent/status" ]; then break; fi
    State=$(awk '$1 == "State:" { print $2 }' "/proc/$Parent/status" 2>/dev/null) || break
    if [ "$State" = Z ]; then break; fi
    sleep 0.05
done
wait "$Parent"
Status=$?
printf '[CarbonLuau:Persistence] supervised-worker-memory samples=%s observed_vmpeak_kib=%s observed_vmhwm_kib=%s exit=%s\n' "$Samples" "$PeakVm" "$PeakRss" "$Status"
exit "$Status"
