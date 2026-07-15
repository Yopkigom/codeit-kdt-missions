#!/usr/bin/env bash
# Cross-compile llama_bridge.dll (Windows x64) from WSL2 using llvm-mingw (no sudo).
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

LLAMA_DIR="${LLAMA_DIR:-/mnt/wsl_data/llama_build/llama.cpp}"
MINGW="${MINGW:-$HOME/toolchains/llvm-mingw}"
export PATH="$MINGW/bin:$PATH"

cmake -S "$HERE" -B "$HERE/build_win" -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DLLAMA_DIR="$LLAMA_DIR" \
  -DCMAKE_SYSTEM_NAME=Windows \
  -DCMAKE_C_COMPILER=x86_64-w64-mingw32-clang \
  -DCMAKE_CXX_COMPILER=x86_64-w64-mingw32-clang++ \
  -DGGML_OPENMP=OFF \
  -DCMAKE_SHARED_LINKER_FLAGS="-static" \
  -DGGML_AVX2=ON -DGGML_FMA=ON -DGGML_F16C=ON  # AVX2 PC 가속; -static로 libc++/unwind 정적

cmake --build "$HERE/build_win" -j
echo "built: $HERE/build_win/llama_bridge.dll"
