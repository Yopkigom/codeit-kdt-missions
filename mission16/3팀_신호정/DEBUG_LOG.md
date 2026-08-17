# DEBUG_LOG.md — 미션 16 디버깅·오류 해결 기록

제출 요건: "미션 중 디버깅, 오류 사항들, 오류 해결 과정을 정리"
작업 중 발생하는 오류를 즉시 아래 형식으로 기록하고, 제출 전 보고서에 요약 반영한다.

기록 형식:

## [날짜] 섹션 — 한 줄 제목

- **증상**: 오류 메시지 또는 이상 동작
- **원인**: 파악된 근본 원인
- **해결**: 적용한 조치 (폴백 경로를 탔다면 그 사실 포함)
- **교훈**: 재발 방지 관점 정리 (선택)

---

## [2026-07-15] B-b-1 — onnxruntime import 시 libcudart.so.12 로드 실패

- **증상**: `modeling.ipynb` 첫 실행에서 `import onnxruntime` 시
  `ImportError: libcudart.so.12: cannot open shared object file` 발생.
  사전 스모크 테스트(단독 파이썬 셸)에서는 동일 import가 정상 작동했음.
- **원인**: onnxruntime GPU 빌드는 CUDA 런타임(libcudart.so.12)을 요구하는데,
  시스템 `LD_LIBRARY_PATH`에는 CUDA 런타임이 없다. 스모크 테스트에서는 torch를
  먼저 import하여 torch 동봉 CUDA 라이브러리가 프로세스에 이미 로드된 상태였고,
  노트북에서는 알파벳순 import로 onnxruntime이 torch보다 먼저 로드되어 실패했다.
- **해결**: B-b-1 임포트 셀에서 torch → onnxruntime 순서로 조정하고 주석으로 근거를 남김.
- **교훈**: GPU 빌드 패키지 간에는 import 순서가 암묵적 의존성이 될 수 있다.
  환경 검증은 실제 실행 경로(노트북 커널)와 동일한 조건에서 수행해야 한다.

## [2026-07-15] C-c-3 — DataLoader 멀티프로세스 워커 교착으로 학습 무한 정지

- **증상**: 학습 실행 후 epoch 1이 10분 넘게 끝나지 않음. GPU 사용률 약 8%(전력 9W),
  체크포인트 미생성. 격리 벤치마크에서는 배치당 101ms(예상 epoch 약 7초)로 GPU 자체는 정상.
- **진단 과정**:
  1. `/proc` 상태 확인 — 워커 16개 전원 `do_sys_poll`(작업 대기), 메인 커널 `futex_wait`,
     load average 0.21. 즉 "느림"이 아니라 전원이 서로를 기다리는 **교착**.
  2. 독립 스크립트로 동일 순서(val 1배치 선소비 → 학습 루프) 재현 +
     `faulthandler.dump_traceback_later`로 스택 확보.
  3. 메인 스레드가 `dataloader.py _clean_up_worker → process.join()`에서 무한 대기함을 확인.
- **원인**: CUDA가 이미 초기화된 프로세스(스레드 47개)를 **fork**로 복제한 DataLoader 워커가
  종료·재개 신호에 응답하지 못함. WSL2 + Jupyter 조합에서 알려진 fork 불안정 패턴.
  `persistent_workers` 사용 시 val 로더 재개(`_reset`) 시점, 미사용 시 epoch마다
  워커 join 시점에 같은 교착이 발생할 수 있다.
- **해결**: `num_workers=0`(메인 프로세스 로딩)으로 고정. 전처리가 약 6ms/장이라
  epoch당 약 1분으로 학습 규모(5 epoch) 대비 수용 가능함을 사전 검증 후 적용.
  spawn 방식은 노트북 정의 클래스(`ChestXrayDataset`)를 워커가 import할 수 없어 배제.
- **교훈**: GPU 사용률이 낮은 "느린 학습"은 처리량 문제가 아니라 교착일 수 있다.
  프로세스 상태(`wchan`)와 스택 덤프로 대기 지점을 특정한 뒤 대응할 것.
  Jupyter 노트북에서는 fork 기반 멀티프로세스 로딩 자체가 구조적 리스크다.

## [2026-07-15] C-d-2 — torch.load가 체크포인트 로드를 거부 (weights_only)

- **증상**: 학습은 정상 완료됐으나 best 체크포인트 재로드에서
  `UnpicklingError: Weights only load failed ... numpy._core.multiarray.scalar was not an allowed global`.
- **원인**: PyTorch 2.6부터 `torch.load`의 기본값이 `weights_only=True`로 변경되어
  화이트리스트 외 객체의 역직렬화를 거부한다. 체크포인트의 `metrics` 딕셔너리에
  sklearn 혼동행렬에서 파생된 **numpy 스칼라(np.float64)** 가 섞여 있었다.
  (json 저장은 np.float64가 float의 서브클래스라 통과했기 때문에 학습 단계에서는 드러나지 않았다.)
- **해결**: `evaluate()`가 지표를 파이썬 기본 타입(float/int)으로 변환해 반환하도록 수정.
  이미 저장된 체크포인트는 일회성 스크립트로 metrics만 캐스팅해 재저장
  (`weights_only=False` 우회는 노트북 코드에 남기지 않음).
- **교훈**: 체크포인트에는 텐서와 파이썬 기본 타입만 담는 것이 안전하다.
  numpy 스칼라는 json에서는 통과하고 torch 안전 로더에서는 거부되는 비대칭이 있다.
