#!/usr/bin/env bash
set -euo pipefail
Root="$(cd "$(dirname "$0")/.." && pwd)"
Build="${1:-$Root/build/linux-x64}"
cmake -S "$Root/native" -B "$Build" -DCMAKE_BUILD_TYPE=Release
cmake --build "$Build" --parallel 4
ctest --test-dir "$Build" --output-on-failure
