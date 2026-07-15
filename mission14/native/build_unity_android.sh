#!/usr/bin/env bash
# Build the Unity Android player (APK + OBB via Split Application Binary) and install
# both to the connected device. The large models ship inside the OBB; on first run the
# app streams them from the OBB to persistentDataPath (see RagVerification.Ensure).
#
# Build + install:
#   PROJECT=/path/to/UnityProject UNITY=/path/to/Unity ./build_unity_android.sh
#   PROJECT=... ./build_unity_android.sh -d            # development build
#
# Install only (pick already-built files, no rebuild):
#   ./build_unity_android.sh --install-only --apk Build/Android/TexChatbot.apk \
#                            --obb Build/Android/main.1.com.hjseen.texchatbot.obb
#
# Env overrides: UNITY, PROJECT, PACKAGE, OUTPUT, VERSION_CODE
set -euo pipefail

PROJECT="${PROJECT:-$(pwd)}"
PACKAGE="${PACKAGE:-com.hjseen.texchatbot}"
OUTPUT="${OUTPUT:-$PROJECT/Build/Android}"
VERSION_CODE="${VERSION_CODE:-}"
UNITY="${UNITY:-}"
ADB="${ADB:-adb}"        # set ADB=/path/to/adb(.exe) if not on PATH

DEV=""
SPLIT=""
INSTALL_ONLY=0
APK=""
OBB=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -d|--development) DEV="-development"; shift ;;
    --split)          SPLIT="-split"; shift ;;   # opt into OBB (only if assets <4GB)
    --install-only)   INSTALL_ONLY=1; shift ;;
    --apk)            APK="$2"; shift 2 ;;
    --obb)            OBB="$2"; shift 2 ;;
    *) echo "unknown arg: $1"; exit 2 ;;
  esac
done

# --- adb / device ---
command -v "$ADB" >/dev/null 2>&1 || { echo "adb not found (set ADB=/path/to/adb)"; exit 1; }
if [[ -z "$("$ADB" devices | awk 'NR>1 && $2=="device"{print $1}')" ]]; then
  echo "no authorized device connected (check '$ADB devices')"; exit 1
fi

build() {
  # --- locate Unity editor (Linux native or Windows .exe under WSL) ---
  if [[ -z "$UNITY" ]]; then
    for c in \
      "$HOME/Unity/Hub/Editor/6000.3.17f1/Editor/Unity" \
      "/opt/unity/editors/6000.3.17f1/Editor/Unity" \
      "/mnt/c/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Unity.exe"; do
      [[ -e "$c" ]] && UNITY="$c" && break
    done
  fi
  [[ -n "$UNITY" && -e "$UNITY" ]] || { echo "Unity editor not found. Set UNITY=/path/to/Unity"; exit 1; }

  mkdir -p "$OUTPUT"
  local log="$OUTPUT/build.log"
  local vc_arg=()
  [[ -n "$VERSION_CODE" ]] && vc_arg=(-versionCode "$VERSION_CODE")

  echo "Unity   : $UNITY"
  echo "Project : $PROJECT"
  echo "Output  : $OUTPUT"
  echo "== Building (Split Application Binary -> APK + OBB) =="
  "$UNITY" -batchmode -nographics \
    -projectPath "$PROJECT" \
    -buildTarget Android \
    -executeMethod TexChatbot.Editor.BuildAndroid.Build \
    -buildOutput "$OUTPUT" "${vc_arg[@]}" $SPLIT $DEV \
    -logFile "$log" \
    || { echo "Unity build failed (tail $log):"; tail -n 40 "$log"; exit 1; }

  APK="$(find "$OUTPUT" -maxdepth 1 -name '*.apk' -print -quit)"
  OBB="$(find "$OUTPUT" -maxdepth 1 -name 'main.*.obb' -print -quit)"
}

[[ "$INSTALL_ONLY" -eq 0 ]] && build

[[ -n "$APK" && -f "$APK" ]] || { echo "APK not found"; exit 1; }
echo "APK : $APK ($(du -h "$APK" | cut -f1))"
if [[ -n "$OBB" && -f "$OBB" ]]; then
  echo "OBB : $OBB ($(du -h "$OBB" | cut -f1))"
else
  echo "OBB : (none — StreamingAssets fit in the APK)"
fi

echo "== Installing APK =="
"$ADB" install -r -d "$APK"

if [[ -n "$OBB" && -f "$OBB" ]]; then
  obb_name="$(basename "$OBB")"
  obb_dir="/sdcard/Android/obb/$PACKAGE"
  echo "== Pushing OBB -> $obb_dir/$obb_name =="
  "$ADB" shell mkdir -p "$obb_dir"
  "$ADB" push "$OBB" "$obb_dir/$obb_name"
fi

echo "== Launching =="
"$ADB" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1 || true
echo "Done. (first run streams models OBB -> persistentDataPath)"
