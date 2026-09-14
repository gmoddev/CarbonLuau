#!/usr/bin/env bash
set -euo pipefail
mkdir -p /work/cache/steamcmd /work/server-linux
if [ ! -f /work/cache/steamcmd/steamcmd.sh ]; then
    curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar xz -C /work/cache/steamcmd
fi
/work/cache/steamcmd/steamcmd.sh +force_install_dir /work/server-linux +login anonymous +app_update 258550 validate +quit
curl -fsSL https://github.com/CarbonCommunity/Carbon/releases/download/production_build/Carbon.Linux.Release.tar.gz -o /work/cache/Carbon.Linux.Release.tar.gz
tar xzf /work/cache/Carbon.Linux.Release.tar.gz -C /work/server-linux
echo '[CarbonLuau:Setup] Linux server downloaded.'
