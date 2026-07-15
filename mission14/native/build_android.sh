#!/usr/bin/env bash
# Build libllama_bridge.so (Android arm64-v8a) using the Android NDK.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

LLAMA_DIR="${LLAMA_DIR:-/mnt/wsl_data/llama_build/llama.cpp}"
: "${ANDROID_NDK_HOME:?set ANDROID_NDK_HOME to the NDK root}"

# R-d/C: optional Vulkan (Adreno) GPU offload. VULKAN=1 로 활성화.
#   요구: NDK android-28 Vulkan 헤더(포함), 호스트 glslc/shaderc(vulkan-shaders-gen 빌드용).
#   주의: ggml-vulkan 정적 백엔드 등록/셰이더 생성이 까다로워 실패 시 CPU 빌드(A+B)로 폴백.
VULKAN_ARGS=()
if [[ "${VULKAN:-0}" == "1" ]]; then
  VULKAN_ARGS=(-DGGML_VULKAN=ON)
  echo "[build] Vulkan(GPU) offload ENABLED"
fi

cmake -S "$HERE" -B "$HERE/build_android" -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
  -DANDROID_ABI=arm64-v8a \
  -DANDROID_PLATFORM=android-28 \
  -DCMAKE_BUILD_TYPE=Release \
  -DLLAMA_DIR="$LLAMA_DIR" \
  -DGGML_OPENMP=OFF \
  -DANDROID_STL=c++_static \
  -DGGML_CPU_ARM_ARCH=armv8.2-a+dotprod+i8mm \
  "${VULKAN_ARGS[@]}"

cmake --build "$HERE/build_android" -j

# 디버그 심볼 제거 (~46MB -> ~5MB, APK 경량화)
STRIP="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/$(uname -s | tr '[:upper:]' '[:lower:]')-x86_64/bin/llvm-strip"
[ -x "$STRIP" ] && "$STRIP" --strip-unneeded "$HERE/build_android/libllama_bridge.so" && echo "stripped debug symbols"

echo "built: $HERE/build_android/libllama_bridge.so ($(stat -c%s "$HERE/build_android/libllama_bridge.so") bytes)"
