# PROJECT_PLAN.md

이 파일은 미션 16의 두 노트북(`modeling.ipynb`, `inference.ipynb`)과 심화(Unity Sentis)
각 섹션의 진행 상태를 추적하는 체크리스트입니다.

## 작업 진행 방침

각 섹션은 아래 순서로 진행합니다. 이전 단계가 완료되지 않으면 다음 단계로 넘어가지 않습니다.

> **계획 확인** → **구현 진행** → **구현 검증** → **실험 결과 확인** → **실험 결과 분석** → **완료 확인**

'계획 확인'을 제외한 나머지 단계 명칭을 셀의 이름으로 사용하지 말 것.
적용 가능한 단계만 포함한다. 실험이 없는 섹션(탐색·문서화 위주)은 실험 관련 단계를 생략한다.

## 문서 섹션 계층 구조

```
대문자.       →  최상위 섹션    예) A. 프로젝트 목적
대문자-소문자. →  하위 섹션     예) A-a. 계획 확인
대문자-소문자-숫자. → 세부 항목  예) A-a-1. 수행 방향 정리
```

계층을 건너뛰지 않는다. 최상위(`대문자.`) 없이 하위(`대문자-소문자.`)를 단독으로 사용하지 않는다.

## 노트북 경계

| 섹션 | 파일 | 실행 환경 |
|---|---|---|
| A | (공통 계획) | — |
| B ~ F | `modeling.ipynb` | WSL2 로컬 (RTX 5050) — Colab 예비 |
| G ~ H | `inference.ipynb` | WSL2 로컬 (CPU 기준 측정) |
| I | 제출물 정리 | WSL2 로컬 |
| J ~ K | 심화 (Unity Sentis) | Windows + Unity 6.3 LTS |

---

## A. 프로젝트 목적 및 계획

### A-a. 계획 확인

- [x] A-a-1. 프로젝트 목적 이해 (모델 3종 포맷 변환 + ONNX 추론 검증)
- [x] A-a-2. 소재 모델 확정 — 미션 06 흉부 X-ray 폐렴 진단, MobileNetV3-Large 전이학습
- [x] A-a-3. 수행 방향 정리 (재학습 → FP32 .pth → INT8 .pth → ONNX → 추론 비교)
- [x] A-a-4. 평가 원칙 승계 확인 — accuracy 단독 판단 금지, Recall(폐렴)·FN 우선
- [x] A-a-5. 제출 요건 확인 (zip 구성 2종, 모델 파일명 `mission_16_*` 규칙)

### A-b. 완료 확인

- [x] A-b-1. 전체 계획 문서화 완료 및 사용자 승인

---

## B. 환경 설정 및 데이터 준비 (modeling.ipynb)

### B-a. 계획 확인

- [x] B-a-1. 데이터 경로 상수 확인 (로컬 절대경로 기본 + `is_colab` 시 kagglehub 분기)
- [x] B-a-2. SEED=42 고정 범위 확인 (random / numpy / torch / torch.cuda)
- [x] B-a-3. 전처리 방침 확인 — 미션 06 승계: CLAHE(2.0, 8×8) → 종횡비 유지 패딩 →
      224×224, ImageNet 정규화, 침윤 소견을 훼손하는 대비(contrast) 증강 금지
- [x] B-a-4. train/val/test 구성 확인 — 미션 06 승계: 원본 val 16장은 유의성 부족으로
      train+val 병합 후 stratified 80:20 재분배, test 624장 원본 유지 (사용자 승인)

### B-b. 구현 진행

- [x] B-b-1. 환경 설정 골격 구현 (미션 05~ 공통 패턴: 폰트, 경로 상수, 시드, 장치 감지)
- [x] B-b-2. Dataset / DataLoader 구현 (클래스 불균형 현황 출력 포함)
- [x] B-b-3. 전처리·증강 파이프라인 구현

### B-c. 구현 검증

- [x] B-c-1. 배치 1개 shape·dtype·값 범위 확인
- [x] B-c-2. 클래스별 매수 집계가 미션 06 기록과 일치하는지 확인

### B-d. 완료 확인

- [x] B-d-1. 데이터 파이프라인 정상 작동 확인 (사용자 검토 완료)

---

## C. 모델 학습 (modeling.ipynb)

### C-a. 계획 확인

- [x] C-a-1. 모델 로드 방식 확인 — `torchvision.models.quantization.mobilenet_v3_large(quantize=False)`
      (E 섹션 PTQ에서 동일 구조를 재사용하기 위해 양자화 대응 변형을 처음부터 사용)
- [x] C-a-2. 학습 목표 확인 — 최고 성능 재현이 아니라 **변환 파이프라인 검증용 기준 모델** 확보
      (epochs 축소 운용, 예: 5 epoch + 조기 종료)
- [x] C-a-3. 분류 헤드 교체 방식 확인 (2분류, classifier 최종 Linear 교체)
- [x] C-a-4. 학습 지표 확인 — val Recall(폐렴) 우선, confusion matrix 병행

### C-b. 구현 진행

- [x] C-b-1. 사전학습 가중치 로드 및 헤드 교체
- [x] C-b-2. 학습 루프 구현 (AMP, 체크포인트 저장)
- [x] C-b-3. 학습 실행

### C-c. 구현 검증

- [x] C-c-1. 더미 배치 forward 정상 확인 (`[batch, 2]`)
- [x] C-c-2. 학습 손실 수렴 확인

### C-d. 실험 결과 확인

- [x] C-d-1. val accuracy / Recall / FN 기록
- [x] C-d-2. confusion matrix 확인

### C-e. 실험 결과 분석

- [x] C-e-1. 기준(FP32) 모델의 성능 수준이 이후 비교의 기준선으로 충분한지 판단

### C-f. 완료 확인

- [x] C-f-1. 기준 모델 확정 (epoch 3, val Recall 1.0 / FN 0 — 사용자 검토 완료)

---

## D. FP32 .pth 추출 (modeling.ipynb)

### D-a. 계획 확인

- [x] D-a-1. 저장 방식 확인 — `state_dict` 저장, 파일명 `mission_16_mobilenetv3.pth`
- [x] D-a-2. 저장 경로 확인 (`4팀_신호정/models/`)

### D-b. 구현 진행

- [x] D-b-1. `model.eval()` 후 `state_dict` 저장

### D-c. 구현 검증

- [x] D-c-1. 새 모델 인스턴스에 재로드 → 동일 입력 출력 일치 확인 (`torch.allclose`)
- [x] D-c-2. 파일 크기 기록

### D-d. 완료 확인

- [x] D-d-1. FP32 .pth 확보 (16.24 MB — 사용자 검토 완료)

---

## E. INT8 양자화 .pth 추출 (modeling.ipynb)

### E-a. 계획 확인

- [x] E-a-1. 방식 확인 — **PTQ(정적 양자화, eager mode)**: `fuse_model()` → `fbgemm` qconfig
      → `prepare` → 보정(calibration) → `convert`
- [x] E-a-2. 보정 데이터 확인 — train 서브셋 약 300장 (라벨 균형 유지, 시드 고정)
- [x] E-a-3. 저장 방식 확인 — 변환 모델을 `torch.jit.script` 후 `.pth`로 저장
      (inference에서 구조 재구성 없이 로드 가능), 파일명 `mission_16_mobilenetv3_quant.pth`
- [x] E-a-4. 리스크 확인 — SE 블록·hardswish 양자화 이슈는 torchvision 양자화 변형이 처리.
      실패 시 폴백: ① FX graph mode(`quantize_fx`) ② ResNet18 교체 (DEBUG_LOG 기록)

### E-b. 구현 진행

- [x] E-b-1. CPU 이동 + fuse + prepare + 보정 실행
- [x] E-b-2. convert + TorchScript 저장

### E-c. 구현 검증

- [x] E-c-1. 저장 파일 재로드 → 추론 정상 작동 확인
- [x] E-c-2. 파일 크기 기록 (FP32 대비 압축률 확인, 기대 약 1/4)

### E-d. 실험 결과 확인

- [x] E-d-1. 양자화 모델의 test accuracy / Recall / FN 측정

### E-e. 실험 결과 분석

- [x] E-e-1. FP32 대비 Recall·FN 열화 분석 — **열화가 유의하면 보정 데이터 확대·QAT 검토**
      (의료 도메인에서 '용량 이득 vs FN 증가' 트레이드오프가 보고서 핵심 논점)

### E-f. 완료 확인

- [x] E-f-1. INT8 .pth 확보 (4.47 MB, 3.64배 압축, FN 0 유지 — 사용자 검토 완료)

---

## F. ONNX 추출 및 검증 (modeling.ipynb)

### F-a. 계획 확인

- [x] F-a-1. export 방침 확인 — FP32 모델 기준, opset 17, Fixed Shape `(1,3,224,224)`,
      `model.eval()` + dropout 비활성 (미션 13 K 섹션 패턴)
- [x] F-a-2. 검증 순서 확인 — 구조(`onnx.checker`) → 런타임(`onnxruntime`) →
      수치 일치(`np.allclose atol=1e-4`) (미션 13 L 섹션 패턴)

### F-b. 구현 진행

- [x] F-b-1. `torch.onnx.export()` 실행, 파일명 `mission_16_mobilenetv3.onnx`

### F-c. 구현 검증

- [x] F-c-1. 구조 검증 통과
- [x] F-c-2. PyTorch 출력 vs ONNX 출력 `np.allclose(atol=1e-4)` 확인, 최대 오차 기록

### F-d. 완료 확인

- [x] F-d-1. ONNX 확보 — 3종 포맷 추출 완료 (캡처 촬영은 섹션 I에서 수행)

---

## G. ONNX 기반 추론 파이프라인 (inference.ipynb)

### G-a. 계획 확인

- [x] G-a-1. 추론 코드 요건 확인 — onnxruntime 세션 로드, test set 624장 배치 평가
- [x] G-a-2. 전처리 일치 방침 확인 — modeling과 동일한 리사이즈·정규화를
      numpy 기준으로 재구현하고, 동일 샘플 1장으로 양쪽 전처리 결과 일치 검증
- [x] G-a-3. 3종 로드 경로 확인 — FP32(state_dict 재구성) / INT8(TorchScript) / ONNX(ort)

### G-b. 구현 진행

- [x] G-b-1. 전처리 함수 구현 및 일치 검증
- [x] G-b-2. 3종 모델 로더 구현
- [x] G-b-3. 공통 평가 루프 구현 (accuracy / Recall / FN / confusion matrix)

### G-c. 구현 검증

- [x] G-c-1. 각 모델 단건 추론 결과 상호 비교 (동일 이미지 → 라벨 일치 여부)

### G-d. 완료 확인

- [x] G-d-1. 추론 파이프라인 정상 작동 (사용자 검토 완료)

---

## H. 3종 포맷 비교 평가 (inference.ipynb)

### H-a. 계획 확인

- [x] H-a-1. 비교 항목 확인 — 파일 용량 / test accuracy / Recall(폐렴) / FN 개수 /
      CPU 추론 지연시간 (warmup 후 N회 평균, 단일 스레드 고정)
- [x] H-a-2. 지연시간 측정 조건 확인 (`torch.set_num_threads(1)`, ort 세션 옵션 동일 조건)

### H-b. 구현 진행

- [x] H-b-1. 3종 전체 test set 평가 실행
- [x] H-b-2. 종합 비교표 + confusion matrix 3종 시각화

### H-c. 실험 결과 확인

- [x] H-c-1. 비교표 완성 (용량·정확도·Recall·FN·지연시간)

### H-d. 실험 결과 분석

- [x] H-d-1. 양자화의 용량·속도 이득 vs Recall·FN 변화 분석
- [x] H-d-2. On-Device 배포 관점 결론 (엣지 기기 메모리·연산 예산 대비 적합성)

### H-e. 완료 확인

- [x] H-e-1. 비교 평가 완료 (사용자 검토 완료)

---

## I. 산출물 정리 및 제출 (본 미션)

### I-a. 계획 확인

- [x] I-a-1. zip 포함 항목 확인 — modeling.ipynb / inference.ipynb / 보고서 PDF /
      용량 캡처 / 디버깅 정리 (모델 파일·데이터셋·advanced_assets 제외)

### I-b. 구현 진행

- [x] I-b-1. `report-materials.md` 작성 (파이프라인 요약, 비교표, FN 분석, 디버깅 요약)
- [x] I-b-2. PDF 변환
- [x] I-b-3. `DEBUG_LOG.md` 최종 정리
- [x] I-b-4. zip 생성

### I-c. 완료 확인

- [x] I-c-1. 제출 폴더명 팀명 확정 — `4팀_신호정` 유지 (사용자 확정)
- [ ] I-c-2. 본 미션 제출 준비 완료

---

## J. (심화) Unity Sentis 기반 mnist_cnn.onnx 추론

### J-a. 계획 확인

- [ ] J-a-1. 자산 확인 — `advanced_assets/mnist_cnn.onnx` (1.7MB, PyTorch 2.6.0 export,
      입력명 `input`) + 타겟 이미지 3장 확보 완료
- [ ] J-a-2. Unity 환경 확인 — Unity 6.3 LTS (6000.3.17f1) + Sentis (미션 13 환경 재사용)
- [ ] J-a-3. 전처리 방침 확인 — 28×28 grayscale, **정규화 방식은 Python에서 선행 검증**:
      `/255` vs MNIST 표준 정규화(0.1307/0.3081) 두 방식을 onnxruntime으로 먼저 비교하여
      정답 라벨이 나오는 쪽을 C#에 이식 (실습 코드 전처리 미상이므로 확정 후 진행)
- [ ] J-a-4. 백엔드 확인 — CPU(Burst) 기본, GPUCompute 비교는 여유 시

### J-b. 구현 진행

- [ ] J-b-1. Python 선행 검증 스크립트 실행 → 전처리 방식·기대 라벨 확정
- [ ] J-b-2. Unity 프로젝트 구성, ONNX Import
- [ ] J-b-3. 추론 스크립트 구현 (Texture2D → Tensor 전처리, Worker 실행, argmax)
- [ ] J-b-4. 결과 표시 UI (이미지 + 예측 라벨)

### J-c. 구현 검증

- [ ] J-c-1. Editor 플레이 모드에서 3장 추론 실행
- [ ] J-c-2. Python 선행 검증 결과와 라벨 일치 확인

### J-d. 실험 결과 확인

- [ ] J-d-1. 3장 예측 라벨 기록 및 화면 캡처

### J-e. 완료 확인

- [ ] J-e-1. Unity 추론 완료

---

## K. (심화) 산출물 정리 및 제출

### K-a. 계획 확인

- [ ] K-a-1. zip 포함 항목 확인 — README.md / 실행 코드 전체(**onnx 제외**) / 결과 캡처

### K-b. 구현 진행

- [ ] K-b-1. Unity C# 스크립트를 `advanced/Scripts/`로 복사
- [ ] K-b-2. README.md 작성 (개요, Unity·Sentis 버전, 실행 방법, 전처리 결정 근거)
- [ ] K-b-3. 심화 zip 생성

### K-c. 완료 확인

- [ ] K-c-1. 심화 제출 준비 완료
