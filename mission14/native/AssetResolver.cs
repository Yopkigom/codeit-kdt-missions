using System;
using System.Collections;
using System.IO;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Networking;
#endif

namespace TexChatbot
{
    // Resolves a runtime asset to a real file path. On Android, copies StreamingAssets ->
    // persistentDataPath (large files streamed to disk, no full byte[]); on desktop it
    // returns the StreamingAssets path directly. alwaysRefresh=true re-copies small
    // config/fixtures each run so updated assets never go stale after an APK update;
    // large models (onnx.data, gguf) are copy-once / adb-pushed.
    public static class AssetResolver
    {
        public static IEnumerator Ensure(string fileName, Action<string> onResolved, bool alwaysRefresh = false)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string dst = Path.Combine(Application.persistentDataPath, fileName);
            if (alwaysRefresh && File.Exists(dst)) File.Delete(dst);
            if (!File.Exists(dst))
            {
                string src = Path.Combine(Application.streamingAssetsPath, fileName);
                using (var req = UnityWebRequest.Get(src))
                {
                    req.downloadHandler = new DownloadHandlerFile(dst) { removeFileOnAbort = true };
                    Debug.Log($"자산 복사: {fileName}");
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"자산 복사 실패 {fileName}: {req.error}");
                        onResolved(null);
                        yield break;
                    }
                }
            }
            onResolved(dst);
#else
            onResolved(Path.Combine(Application.streamingAssetsPath, fileName));
            yield break;
#endif
        }
    }
}
