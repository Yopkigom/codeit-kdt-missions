# PROJECT_PLAN.md

이 파일은 노트북(`13_4팀_신호정.ipynb`) 각 섹션의 진행 상태를 추적하는 체크리스트입니다.

## 작업 진행 방침

각 섹션은 아래 순서로 진행합니다. 이전 단계가 완료되지 않으면 다음 단계로 넘어가지 않습니다.

> **계획 확인** → **구현 진행** → **구현 검증** → **실험 결과 확인** → **실험 결과 분석** → **완료 확인**

'계획 확인'을 제외한 나머지 단계 명칭을 셀의 이름으로 사용하지 말 것.
적용 가능한 단계만 포함한다. 실험이 없는 섹션(탐색·문서화 위주)은 실험 관련 단계를 생략한다.

## 문서 섹션 계층 구조

노트북 및 이 문서의 섹션 별 셀은 아래 계층 표기를 따른다.

```
대문자.       →  최상위 섹션    예) A. 프로젝트 목적
대문자-소문자. →  하위 섹션     예) A-a. 계획 확인
대문자-소문자-숫자. → 세부 항목  예) A-a-1. 수행 방향 정리
```

계층을 건너뛰지 않는다. 최상위(`대문자.`) 없이 하위(`대문자-소문자.`)를 단독으로 사용하지 않는다.
셀 내부에 숫자로 나열된 내용들은 대문자-소문자-숫자 형식으로 표기하지 않고 숫자로만 표기한다.

---

## A. 프로젝트 목적 및 계획

### A-a. 계획 확인

- [x] A-a-1. 프로젝트 목적 이해 (on-device 감성 분석 파이프라인)
- [x] A-a-2. 수행 방향 정리 (모델 탐색 → 학습 → ONNX → Unity → APK)
- [x] A-a-3. 수행 계획 단계 확인 (1~19단계)
- [x] A-a-4. 추가 고려사항 확인 (Colab 런타임 단절 대비, is_colab 분기)

### A-b. 완료 확인

- [x] A-b-1. 전체 계획 문서화 완료

---

## B. on-device 타겟 BERT 모델 탐색

### B-a. 계획 확인

- [x] B-a-1. 선발 우선순위 기준 확인 (용량 → 정확도 → 처리속도)
- [x] B-a-2. on-device 적합 조건 정리 (메모리·연산 예산, 모바일 ONNX 호환)

### B-b. Hugging Face 직접 탐색

- [x] B-b-1. HuggingFace Hub 탐색 조건 설정 및 검색 실행
- [x] B-b-2. 검색 결과 과다(8985건)로 직접 탐색 부적합 판단

### B-c. 웹 및 AI 서포트 서칭

- [x] B-c-1. 웹 서치 및 AI 서포트를 통한 후보 모델 탐색
- [x] B-c-2. 최종 2종 모델 선발 (KcELECTRA-small-v2022, KoMiniLM)

### B-d. 선발 모델 내용 정리

- [x] B-d-1. 후보 모델 비교표 작성 (파라미터, 구조, 정확도, 라이선스)
- [x] B-d-2. 선발 근거 서술 완료

### B-e. 구현 검증

- [x] B-e-1. 선발된 두 모델 로컬 환경에서 로드 확인 → F 섹션에서 완료
- [x] B-e-2. 토크나이저 기본 동작 확인 → E 섹션에서 완료

### B-f. 완료 확인

- [x] B-f-1. 선발 모델 2종 확정 및 모델 ID 기록 (beomi/KcELECTRA-small-v2022, BM-K/KoMiniLM)

---

## C. 쇼핑몰 리뷰 데이터 형상 조사

### C-a. 계획 확인

- [x] C-a-1. 조사 항목 확인 (경로별 구성, 파일 수량, 스키마, 일치 여부)
- [x] C-a-2. SNS / 쇼핑몰 두 소스 모두 대상임을 확인

### C-b. 구현 진행

- [x] C-b-1. 전체 JSON 파일 경로 수집 및 소스·카테고리별 집계
- [x] C-b-2. 파일별 스키마 키 목록 추출
- [x] C-b-3. 레코드 수(파일당 100건) 일치 여부 확인
- [x] C-b-4. `GeneralPolarity` 값 범위 확인

### C-c. 구현 검증

- [x] C-c-1. 전체 파일 스키마 일치 여부 일괄 검증
- [x] C-c-2. 스키마 불일치 파일 목록 출력

### C-d. 실험 결과 확인

- [x] C-d-1. 총 레코드 수, 소스·카테고리별 분포 확인
- [x] C-d-2. 스키마 이상 파일 존재 여부 기록

### C-e. 완료 확인

- [x] C-e-1. 데이터 형상 조사 결과 노트북에 기술 완료

---

## D. EDA, 전처리, 분할

### D-a. 계획 확인

- [x] D-a-1. EDA 항목 확인 (카테고리, 규모, 길이, 어휘 분포, 라벨 분포, 결측·중복 비율)
- [x] D-a-2. 전처리 항목 확인 (NFKC 정규화, 중복 제거, 길이 필터링, 제어문자 제거)
- [x] D-a-3. 분할 비율 확인 (train 8 : valid 1.5 : test 0.5, stratified, random_state 고정)

### D-b. 구현 진행

- [x] D-b-1. 전체 JSON → DataFrame 통합 로드, 출처 컬럼 부여
- [x] D-b-2. EDA 실행 (길이 분포, 어휘 분포, 라벨 분포, 결측·중복 비율)
- [x] D-b-3. 클래스 불균형 파악 및 처리 방침 결정
- [x] D-b-4. 전처리 파이프라인 구현 및 적용
- [x] D-b-5. stratified split 구현 및 적용

### D-c. 구현 검증

- [x] D-c-1. 전처리 전·후 샘플 비교 출력
- [x] D-c-2. 분할 후 train/valid/test 레이블 분포 확인 (불균형 유지 여부)
- [x] D-c-3. 중복·결측 잔존 여부 재확인

### D-d. 실험 결과 확인

- [x] D-d-1. 분할 결과 레코드 수 기록
- [x] D-d-2. 전처리 후 어휘 분포 변화 기록

### D-e. 완료 확인

- [x] D-e-1. 전처리된 train/valid/test 데이터 저장 완료

---

## E. 데이터셋 구현

### E-a. 계획 확인

- [x] E-a-1. HuggingFace Dataset/DataLoader 구현 방식 확인
- [x] E-a-2. 토크나이저 사용 방침 확인 (사전학습 vocab 그대로 사용)
- [x] E-a-3. 두 모델 각각 별도 Dataset 필요 여부 확인 → 단일 SentimentDataset 클래스로 통합

### E-b. 구현 진행

- [x] E-b-1. 모델 A용 Dataset 클래스 구현
- [x] E-b-2. 모델 B용 Dataset 클래스 구현
- [x] E-b-3. DataLoader 구성 (batch_size, shuffle 등)

### E-c. 구현 검증

- [x] E-c-1. 배치 1개 출력하여 shape·dtype 확인
- [x] E-c-2. 토크나이저 출력과 모델 입력 형식 일치 확인

### E-d. 완료 확인

- [x] E-d-1. 두 모델 각각 train/valid/test DataLoader 정상 작동 확인

---

## F. 모델 준비

### F-a. 계획 확인

- [x] F-a-1. 분류 헤드 구성 방식 확인 (부정/중립/긍정 3분류, `num_labels=3`)
- [x] F-a-2. 사전학습 가중치 로드 방식 확인

### F-b. 구현 진행

- [x] F-b-1. 모델 A 로드 및 분류 헤드 설정
- [x] F-b-2. 모델 B 로드 및 분류 헤드 설정

### F-c. 구현 검증

- [x] F-c-1. 두 모델 forward pass 정상 작동 확인 (더미 배치)
- [x] F-c-2. 출력 logit shape 확인 (`[batch, 3]`)

### F-d. 완료 확인

- [x] F-d-1. 두 모델 준비 완료

---

## G. 훈련 파이프라인 구현 및 하이퍼파라미터 설정

### G-a. 계획 확인

- [x] G-a-1. Full Fine-Tuning / Freeze / LoRA-PEFT 구현 차이점 확인
- [x] G-a-2. 공통 하이퍼파라미터 값 확정 (lr=2e-5, epochs=5, warmup=0.1, wd=0.01, seed=42)
- [x] G-a-3. `transformers.Trainer` 공통 학습기 사용 방침 확인 (WeightedTrainer)
- [x] G-a-4. LoRA 설정값 결정 (r=8, alpha=16, dropout=0.1, target=query/key/value)

### G-b. 구현 진행

- [x] G-b-1. Full Fine-Tuning 훈련기 구현 (`build_full_ft`)
- [x] G-b-2. Freeze(인코더 동결) 훈련기 구현 (`build_freeze`, classifier+pooler 제외)
- [x] G-b-3. LoRA-PEFT 훈련기 구현 (`build_lora`, `get_peft_model`, `modules_to_save=['classifier']`)
- [x] G-b-4. 체크포인트 저장 경로 설정 (로컬: `CheckPoint/`, Colab: Google Drive)
- [x] G-b-5. ONNX export 대비 저장 설정 (H에서 `model.eval()` + dropout 비활성 적용, K에서 export)

### G-c. 구현 검증

- [x] G-c-1. 각 훈련기 로컬에서 1 step 실행 확인 (6개 조합 전부 통과)
- [x] G-c-2. Freeze 방식에서 동결 레이어 파라미터 grad 비활성 확인 (모델 A/B 모두 통과)
- [x] G-c-3. LoRA 방식에서 학습 가능 파라미터 수 출력 확인 (A: 1.27%, B: 0.48%)

### G-d. 완료 확인

- [x] G-d-1. 3종 훈련기 × 2종 모델 = 6개 조합 로컬 검증 완료

---

## H. 모델 학습 수행

### H-a. 계획 확인

- [x] H-a-1. `is_colab = True` 설정 및 Google Drive 마운트 확인 (H-b pre-flight assert 구현)
- [x] H-a-2. 체크포인트 저장 경로가 Google Drive를 가리키는지 명시적 확인 (assert CKPT_ROOT 구현)
- [x] H-a-3. 런타임 단절 대비 재개(resume) 처리 확인 (`checkpoint-*` 감지 + `resume_from_checkpoint`)

### H-b. 구현 진행

- [x] H-b-1. 모델 A — Full Fine-Tuning 학습 실행
- [x] H-b-2. 모델 A — Freeze 학습 실행
- [x] H-b-3. 모델 A — LoRA-PEFT 학습 실행
- [x] H-b-4. 모델 B — Full Fine-Tuning 학습 실행
- [x] H-b-5. 모델 B — Freeze 학습 실행
- [x] H-b-6. 모델 B — LoRA-PEFT 학습 실행

### H-c. 구현 검증

- [x] H-c-1. 각 조합 체크포인트 파일 저장 확인 (Google Drive)
- [x] H-c-2. 학습 로그 정상 출력 확인

### H-d. 실험 결과 확인

- [x] H-d-1. 6개 조합 학습 완료 여부 확인
- [x] H-d-2. 각 조합의 학습 손실 수렴 여부 확인

### H-e. 실험 결과 분석

- [x] H-e-1. 학습 시간 비교 기록 (Full FT vs Freeze vs LoRA)
- [x] H-e-2. 학습 곡선 이상(과적합, 발산 등) 여부 확인

### H-f. 완료 확인

- [x] H-f-1. 6개 조합 전부 학습 완료 및 최종 체크포인트 저장 확인

---

## I. 학습 결과 시각화 및 평가

### I-a. 계획 확인

- [x] I-a-1. 시각화 항목 확인 (학습/검증 손실, 정확도, macro-F1)
- [x] I-a-2. macro-F1 계산 방식 확인

### I-b. 구현 진행

- [x] I-b-1. 학습·검증 손실 곡선 그래프 구현
- [x] I-b-2. 정확도·macro-F1 곡선 그래프 구현
- [x] I-b-3. 6개 조합 비교 시각화 구현

### I-c. 구현 검증

- [x] I-c-1. 그래프 정상 출력 확인 (x축 step/epoch, y축 지표값)

### I-d. 실험 결과 확인

- [x] I-d-1. 조합별 최고 valid macro-F1 수치 기록

### I-e. 실험 결과 분석

- [x] I-e-1. Full FT / Freeze / LoRA 방식 간 성능·학습 효율 비교 분석
- [x] I-e-2. 모델 A vs 모델 B 성능 차이 분석

### I-f. 완료 확인

- [x] I-f-1. 시각화 및 지표 기록 완료

---

## J. 종합 평가 및 on-device 대상 모델 선정

### J-a. 계획 확인

- [x] J-a-1. 선정 우선순위 확인 (용량 → 정확도 → 처리속도)
- [x] J-a-2. test set 최종 평가 방법 확인

### J-b. 구현 진행

- [x] J-b-1. 6개 조합 모두 test set 최종 평가 실행
- [x] J-b-2. 종합 평가표 작성 (모델, 방식, 용량, test macro-F1, 추론 속도)

### J-c. 구현 검증

- [x] J-c-1. 평가표 수치 재현 가능 여부 확인 (seed 고정 상태)

### J-d. 실험 결과 확인

- [x] J-d-1. 6개 조합 종합 평가표 완성

### J-e. 실험 결과 분석

- [x] J-e-1. 선정 기준(용량 → 정확도 → 처리속도) 적용하여 최적 조합 결정
- [x] J-e-2. 선정 근거 서술

### J-f. 완료 확인

- [x] J-f-1. on-device 이식 대상 모델 1종 확정

---

## K. ONNX Export

### K-a. 계획 확인

- [x] K-a-1. 선정 모델이 LoRA-PEFT 방식일 경우 `merge_and_unload()` 선행 필요 확인
- [x] K-a-2. Fixed Shape export 방침 확인 (dynamic axes 사용 금지)
- [x] K-a-3. `model.eval()` + dropout 비활성 + 동적 플로우 회피 적용 방침 확인

### K-b. 구현 진행

- [x] K-b-1. (LoRA인 경우) `merge_and_unload()` 실행하여 가중치 통합 — Full FT 선정으로 해당 없음
- [x] K-b-2. 더미 입력 텐서 생성 (Fixed Shape 기준)
- [x] K-b-3. `torch.onnx.export()` 실행

### K-c. 구현 검증

- [x] K-c-1. ONNX 파일 생성 확인 (`modelA_full_ft.onnx`)
- [x] K-c-2. 파일 크기 확인 및 기록 (1.5 MB, opset 14)

### K-d. 완료 확인

- [x] K-d-1. ONNX 파일 저장 완료

---

## L. ONNX 작동 검증

### L-a. 계획 확인

- [x] L-a-1. 검증 순서 확인 (구조 검증 → 런타임 검증 → 수치 일치 확인)
- [x] L-a-2. `np.allclose atol=1e-4` 기준 확인

### L-b. 구현 진행

- [x] L-b-1. `onnx.checker.check_model()` 구조 검증 실행 — 통과 (opset 18, ir_version 10)
- [x] L-b-2. `onnxruntime.InferenceSession` 런타임 검증 실행 — 초기화 통과
- [x] L-b-3. PyTorch 출력 vs ONNX 출력 `np.allclose` 비교 구현

### L-c. 구현 검증

- [x] L-c-1. 구조 검증 통과 확인
- [x] L-c-2. 런타임 실행 오류 없음 확인

### L-d. 실험 결과 확인

- [x] L-d-1. `np.allclose(atol=1e-4)` 결과 `True` 확인
- [x] L-d-2. 불일치 시 최대 오차 수치 기록 — 최대 오차 9.54e-07 (허용 범위 내)

### L-e. 실험 결과 분석

- [x] L-e-1. 불일치 발생 시 원인 파악 — 수치 불일치 없음, 분석 생략

### L-f. 완료 확인

- [x] L-f-1. ONNX 수치 검증 통과 확인

---

## M. Unity 엔진 기반 ONNX 시스템 통합

### M-a. 계획 확인

- [x] M-a-1. Unity 버전 확인 (Unity 6.3 LTS — 6000.3.17f1)
- [x] M-a-2. Unity Sentis 패키지 버전 확인
- [x] M-a-3. Unity Sentis 토크나이저 호환 여부 사전 확인 (내장 불호환 확인 → C# 자체 구현)

### M-b. 구현 진행

- [x] M-b-1. ONNX 파일 Unity 프로젝트에 Import (onnx.data 동반 파일 포함 이슈 해결)
- [x] M-b-2. AI Worker(추론 호출 래퍼) 구현
- [x] M-b-3. 런타임 로드 및 Backend 설정 (CPU/GPU)
- [x] M-b-4. 토크나이저 구현 (Sentis 내장 불호환 → C# WordPiece 자체 구현, vocab.txt 추출)
- [x] M-b-5. Unity 토크나이저 출력과 Python 토크나이저 출력 일치 확인

### M-c. 구현 검증

- [x] M-c-1. Unity Editor에서 추론 실행 및 결과 출력 확인
- [x] M-c-2. 긍정/부정 샘플 각 1건 이상 추론 결과 확인

### M-d. 완료 확인

- [x] M-d-1. Unity 시스템 통합 완료

---

## N. Unity 엔진 기반 어플리케이션 구현

### N-a. 계획 확인

- [x] N-a-1. 서비스 로직 요구사항 확인 (텍스트 입력 → 감성 분석 → 결과 표시)
- [x] N-a-2. UI 요구사항 확인

### N-b. 구현 진행

- [x] N-b-1. 서비스 로직 구현 (Callback 기반 모델 준비 전파, CSV 로더, 추론 호출)
- [x] N-b-2. UI 구현 (리뷰 리스트, 시작·평가·초기화 버튼)

### N-c. 구현 검증

- [x] N-c-1. Unity Editor 플레이 모드에서 전체 흐름 동작 확인 (영상 첨부)
- [x] N-c-2. 엣지 케이스 테스트 (빈 입력, 매우 긴 텍스트 등)

### N-d. 완료 확인

- [x] N-d-1. 어플리케이션 구현 완료 (APK 공유)

---

## O. Android 작동 확인 및 개선 사항 정리

### O-a. 계획 확인

- [x] O-a-1. 빌드 타겟 확인 (Android OS 13, Galaxy S25)
- [x] O-a-2. APK 빌드 설정 확인 (IL2CPP, ARM64)

### O-b. 구현 진행

- [x] O-b-1. Android APK 빌드
- [x] O-b-2. Galaxy S25에 APK 설치

### O-c. 구현 검증

- [x] O-c-1. 기기에서 앱 실행 확인
- [x] O-c-2. 추론 결과 정확도 샘플 확인

### O-d. 실험 결과 확인

- [x] O-d-1. 발견된 이슈 목록 작성 — 이슈 없음
- [x] O-d-2. 기기 추론 속도 측정 기록

### O-e. 실험 결과 분석

- [x] O-e-1. 이슈 원인 분석 — 이슈 없음, 생략

### O-f. 완료 확인

- [x] O-f-1. 주요 이슈 수정 완료 — 이슈 없음
- [x] O-f-2. 시연 영상 촬영 및 첨부 (N-c YouTube 링크)
- [x] O-f-3. APK 파일 백업 (Google Drive 공유)

---

## P. 결론

### P-a. 계획 확인

- [x] P-a-1. 결론에 포함할 항목 확인 (모델 선정 근거, 성능 비교, on-device 이식 결과)

### P-b. 구현 진행

- [x] P-b-1. 결론 작성 (학습 방식 비교 요약, 선정 모델 근거, on-device 검증 결과) — O 섹션으로 통합

### P-c. 완료 확인

- [ ] P-c-1. 결론 작성 완료 및 제출 준비
