# llama_bridge — Unity 직접 통합 (D-d 되돌림)

LLMUnity가 Gemma 4(2026-04) 아키텍처를 미지원(E-b 검증 실패)하여, llama.cpp를
Unity에 직접 통합하기 위한 최소 C-ABI 래퍼입니다. P/Invoke로 호출합니다.

## 패리티 기준
- llama.cpp는 프로토타입(`llama-cpp-python==0.3.29`)이 vendoring한 커밋
  **`f05cf4676af46c2f017c0e6ba25b6e20204f700e`** 에 핀합니다.
- 모델: `gemma-4-E2B-it-Q4_K_M.gguf` (프로토타입과 동일 GGUF).
- 채팅 템플릿은 GGUF 내장 템플릿(`llama_chat_apply_template`)을 사용해
  프로토타입 `create_chat_completion` 과 포맷을 일치시킵니다.

## 구성
| 파일 | 역할 |
| --- | --- |
| `llama_bridge.h` / `.cpp` | C-ABI 래퍼 (load/tokenize/format_chat/generate, 스트리밍 콜백, 타이밍) |
| `CMakeLists.txt` | llama+ggml 정적 링크 → 단일 공유 라이브러리 |
| `LlamaBridge.cs` | Unity C# P/Invoke 바인딩 + `LlamaModel`(load/format/chat/generate, IL2CPP AOT 콜백) |
| `LlamaVerification.cs` | E-b-2 검증 드라이버 MonoBehaviour (로드→스트리밍 생성→체크리스트 자동 판정) |
| `build_windows.sh` | Windows x64 `llama_bridge.dll` (llvm-mingw 교차컴파일) |
| `build_android.sh` | Android arm64-v8a `libllama_bridge.so` (NDK) |

## 빌드
```bash
# Windows DLL (Editor) — llvm-mingw 필요
LLAMA_DIR=/mnt/wsl_data/llama_build/llama.cpp MINGW=$HOME/toolchains/llvm-mingw ./build_windows.sh

# Android .so (기기) — NDK 필요
ANDROID_NDK_HOME=/path/to/ndk LLAMA_DIR=/mnt/wsl_data/llama_build/llama.cpp ./build_android.sh
```

## Unity 배치
- `llama_bridge.dll` → `Assets/Plugins/x86_64/`
- `libllama_bridge.so` → `Assets/Plugins/Android/libs/arm64-v8a/`
- `LlamaBridge.cs` → `Assets/Scripts/`
- 모델 GGUF → `StreamingAssets` → 최초 실행 시 `persistentDataPath` 복사

## E-b-2 검증 실행
1. `llama_bridge.dll` → `Assets/Plugins/x86_64/`, `LlamaBridge.cs`·`LlamaVerification.cs` → `Assets/Scripts/`
2. GGUF를 `Assets/StreamingAssets/` 에 배치
3. 빈 GameObject에 `LlamaVerification` 부착 → Play
4. Console에 체크리스트(PASS/FAIL)와 측정(prefill/TTFT/tok-s/wall-clock), 응답이 출력됨
   - 실시간 토큰을 보려면 인스펙터의 Output Text에 UI Text를 연결(선택)

## IL2CPP 주의
토큰 스트리밍 콜백은 **static + `[MonoPInvokeCallback]`** 이어야 AOT(Android)에서
크래시하지 않습니다. `LlamaModel.TokenThunk` 참고.

---

## N. RAG 파이프라인 이식 (C#)

E에서 LLM(llama.cpp 직접 통합)을 검증한 뒤, N에서 임베딩·검색·토크나이저·상태기계를
C#으로 이식한 부분입니다. namespace `TexChatbot`.

| 파일 | 섹션 | 역할 |
| --- | --- | --- |
| `KureTokenizer.cs` | N-f | `Microsoft.ML.Tokenizers` SentencePiece(Unigram) + XLM-R 오프셋(`sp_id+1`, `<s>`0…`</s>`2 래핑, SP unk0→HF unk3). prefix 없음. |
| `OnnxEmbedder.cs` | N-c | `Microsoft.ML.OnnxRuntime`로 `kure_v1_fp16.onnx` 추론 → `last_hidden_state` CLS + L2. pooler `tanh` 무시. |
| `IndexStore.cs` | N-d | `index_embeddings.npy`(fp32 [N,1024]) + `index_chunks.json` + `index_manifest.json` 로드·대조. |
| `CosineRetriever.cs` | N-e | 정규화 벡터 내적(=코사인) brute-force top-k. 프로토타입 J-b 동일 경로. |
| `RagPipeline.cs` | N-g | L-d LangGraph 1:1 이식(retrieve→route_by_score(≥0.5)→generate/fallback). 임계값·top-k·800자 캡·6000토큰 예산·시스템 프롬프트 동일. fallback은 교체 가능 훅(U/W). |
| `RagVerification.cs` | N-h/O-b | `parity_fixtures.json` 골든 대비 토큰 ID 완전 일치(N-h) + 질의 임베딩 코사인 ≥0.999(O-b) 자동 판정. RAG sanity(선택). |

### Unity 의존성 (UPM/NuGet)
- `Microsoft.ML.OnnxRuntime` (+ Android arm64 네이티브 `libonnxruntime.so`)
- `Microsoft.ML.Tokenizers`
- `com.unity.nuget.newtonsoft-json` (JSON 로드)

### 자산 배치 (StreamingAssets → 최초 실행 시 persistentDataPath 복사)
- `kure_v1_spm.model`(~5MB), `kure_v1_fp16.onnx` + `kure_v1_fp16.onnx.data`(~1.1GB),
  `index_embeddings.npy`, `index_chunks.json`, `index_manifest.json`, `parity_fixtures.json`
- 대용량(`.onnx.data`, GGUF)은 `DownloadHandlerFile`로 스트리밍 복사(단일 `byte[]` OOM 회피).

### 패리티 골든
`Export/parity_fixtures.json` — Python(KURE-v1 + fp16 ONNX, CLS+L2, prefix 없음) 기준
샘플 19문장의 토큰 ID·질의 임베딩·top-5 chunk_id. N-h(토큰 ID)·O-b(코사인·top-5) 판정 기준.
SP 모델은 bge-m3의 `sentencepiece.bpe.model`(KURE-v1과 동일 토크나이저, Python에서 토큰 ID 일치 확인).

검증 운영 메모: 기기 재검증은 `am force-stop` 후 재기동해야 `Start()`가 재실행됨.
갱신 골든/설정은 persistentDataPath 직접 push 또는 `AssetResolver.Ensure(alwaysRefresh:true)`로 매 실행 갱신(소형 파일)해 stale을 회피.

---

## P. 어플리케이션 서비스로직·UI (C#, MVP)

N의 `RagPipeline` 위에 챗봇 앱 계층을 올린 부분. namespace `TexChatbot`, TextMeshPro, 씬/프리팹 + 직렬화 참조.

| 파일 | 계층 | 역할 |
| --- | --- | --- |
| `ChatService.cs` | Model | RAG 상태기계를 비동기(백그라운드 스레드)+토큰 스트리밍으로 래핑. `ConcurrentQueue<ChatEvent>`(Stage/Token/Done/Error), `ChatResult`(Source/Pages/TopScore), `IsBusy` 가드. |
| `ChatPresenter.cs` | Presenter | `ChatService` 이벤트 → `ChatView` 갱신. 메인 스레드 `Tick()` 펌프(토큰 append=TTFT, 완료 시 출처 표시). |
| `ChatView.cs` | View | 직렬화 참조만(로직 없음). `OnSubmit` 이벤트 + AddUserMessage/AddBotBubble/SetStatus/ShowProgress. |
| `ChatBubble.cs` | View(prefab) | 메시지 버블: 스트리밍 텍스트 + 페이지 칩(InScope) / Fallback 배지(OutOfScope). |
| `ChatApp.cs` | Composition root | 자산 해소 → 파이프라인+ChatService 비동기 빌드 → Presenter 와이어링. 빈 GameObject에 부착 + 씬 ChatView 할당. |
| `AssetResolver.cs` | 공용 | StreamingAssets→persistentDataPath 해소(대용량 copy-once, 소형 alwaysRefresh). |
| (`RagPipeline.cs`) | — | `RagStage`+`onStage` 콜백 가산(상태 표시용, 라우팅 불변). |

### 씬 구성
Canvas(Overlay) → ScrollView/Content(VerticalLayoutGroup+ContentSizeFitter) + InputBar(TMP_InputField+Button) + StatusText + ProgressRoot(Slider).
프리팹 UserBubble/BotBubble(ChatBubble)·PageChip. 빈 GameObject에 `ChatApp` 부착 후 직렬화 필드 연결.
한글 폰트: TMP_FontAsset(NanumGothic 등) 적용. 작동 판정은 Q(Editor→S25).

---

## R. 정량 측정 도구 (C#)

- 속도(R-b): 앱에 계측 로그 내장 — `[R-b]` 라인(prefill·TTFT·decode tok/s·n_prompt·256토큰 wall-clock 투영), `[R-b] model load (cold)`. `RagState.Timings`(LbTimings) → ChatService 로그.
- 품질(R-c): `GoldenEvalRunner.cs` — 골든 Q&A 50문항을 파이프라인에 돌려 답변·검색 top-k·타이밍을 `persistentDataPath/eval_unity_answers.json`으로 덤프. `golden_qa.json`을 StreamingAssets에 배치, 빈 GameObject에 부착 후 Play. 산출물을 `adb pull` → 노트북 R-c 셀에서 Python(M과 동일 Gemini judge)으로 재채점.
- 배포 설정 = **A+B (CPU)**: A=`RagPipeline` top-3/400자, B=`lb_load` `n_threads_batch=6`. baseline 101s→44.6s(256토큰 wall-clock 투영). C(Vulkan)는 빌드 툴체인 제약으로 미적용(향후 과제, `VULKAN=1`/`nGpuLayers=99` 준비됨).

---

## W. LLM API Fallback 재이식 (C#)

`GeminiFallback.cs` — 프로토타입 U-c `gemini_fallback_answer`를 C#으로 1:1 이식. 범위 밖 질의(top_score<0.5)에서 `RagPipeline`의 fallback 훅을 Gemini 호출로 교체(라우팅·구조 불변).

- 동일 `FALLBACK_SYSTEM` 프롬프트·모델(`gemini-flash-lite-latest`)·429 재시도.
- **`HttpClient`(워커 스레드 블로킹)** 사용 — fallback 훅이 ChatService 워커에서 동기 호출되므로 UnityWebRequest(메인 스레드) 대신. 요청/응답 JSON은 Newtonsoft.
- API 키: `gemini.key` 파일(StreamingAssets 또는 adb push to persistentDataPath, **소스·빌드 미포함**). 없으면 안내 메시지로 폴백.
- 연결: `ChatApp`이 키 파일을 찾으면 `new RagPipeline(..., fallback: gemini.Answer)`.
- **네이티브 재빌드 불필요**(순수 C#) → R의 A+B `.so` 재사용, C# 재컴파일 + APK만.
