#!/usr/bin/env bash
# Push the large RAG models to the device's persistentDataPath, bypassing the 4GB ZIP
# limit of the APK/OBB. RagVerification.Ensure() finds them there and skips the copy.
#
# Prereq: the app must be installed AND launched once so /Android/data/<pkg>/files exists
# (or this script creates it via adb mkdir).
#
# Usage:
#   EXPORT=/path/to/Export GGUF=/path/to/gemma-4-E2B-it-Q4_K_M.gguf ./push_assets.sh
#   ./push_assets.sh --all      # also push the small files (spm/npy/json/fixtures)
#
# Env: PACKAGE, EXPORT (Export dir), GGUF (gguf path, defaults to HF cache lookup)
set -euo pipefail

PACKAGE="${PACKAGE:-com.hjseen.texchatbot}"
EXPORT="${EXPORT:-$(pwd)/Export}"
ADB="${ADB:-adb}"        # set ADB=/path/to/adb(.exe) if not on PATH
DST="/sdcard/Android/data/$PACKAGE/files"
PUSH_ALL=0
[[ "${1:-}" == "--all" ]] && PUSH_ALL=1

command -v "$ADB" >/dev/null 2>&1 || { echo "adb not found (set ADB=/path/to/adb)"; exit 1; }
[[ -n "$("$ADB" devices | awk 'NR>1 && $2=="device"{print $1}')" ]] || { echo "no authorized device"; exit 1; }

# locate GGUF (Export first, then HF cache)
GGUF="${GGUF:-}"
if [[ -z "$GGUF" ]]; then
  if [[ -f "$EXPORT/gemma-4-E2B-it-Q4_K_M.gguf" ]]; then
    GGUF="$EXPORT/gemma-4-E2B-it-Q4_K_M.gguf"
  else
    GGUF="$(find "$HOME/.cache/huggingface/hub" -name 'gemma-4-E2B-it-Q4_K_M.gguf' 2>/dev/null | head -n1)"
  fi
fi

echo "device dst : $DST"
"$ADB" shell mkdir -p "$DST"

push() {  # push <src> with existence check
  local src="$1"
  [[ -f "$src" ]] || { echo "  MISSING: $src"; return 1; }
  echo "  -> $(basename "$src") ($(du -h "$src" | cut -f1))"
  "$ADB" push "$src" "$DST/"
}

echo "== large models =="
push "$GGUF"
push "$EXPORT/kure_v1_fp16.onnx"
push "$EXPORT/kure_v1_fp16.onnx.data"   # must sit next to the .onnx (ORT external data)

if [[ "$PUSH_ALL" -eq 1 ]]; then
  echo "== small files (optional; otherwise served from StreamingAssets) =="
  push "$EXPORT/kure_v1_spm.model"
  push "$EXPORT/index_embeddings.npy"
  push "$EXPORT/index_chunks.json"
  push "$EXPORT/index_manifest.json"
  push "$EXPORT/parity_fixtures.json"
fi

echo "Done. Files on device:"
"$ADB" shell ls -la "$DST"
