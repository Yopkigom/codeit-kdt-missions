## 문서 목적

이 문서는 미션 17("MNIST 손글씨 숫자 인식 Streamlit 웹 서비스 + Docker 배포")의
구현 계획 체크리스트다. `mission-guide.md`의 요건을 세부 작업 단위로 분해하여
진행 상태를 추적한다. 원래의 8단계 개요를 A~H 섹션으로 세분화한 것이며,
거시적 순서(가이드 파악 → UX 구상 → 개발 요소 정리 → 도커 환경 설정 →
Streamlit 구현 → 로컬 확인 → Docker Hub 배포 → 제출) 자체는 바뀌지 않는다.

## 작업 진행 방침

각 섹션은 아래 순서로 진행한다. 이전 단계가 완료되지 않으면 다음 단계로 넘어가지 않는다.

> **계획 확인** → **구현 진행** → **구현 검증** → **완료 확인**

이 미션은 학습/실험이 아니라 구현·배포이므로 "실험 결과 확인·분석" 단계는 생략한다.
순수 검토·의사결정만 있는 섹션(예: 계획 수립 자체)은 구현 진행/검증 단계를 생략한다.

## 문서 섹션 계층 구조

```
대문자.        →  최상위 섹션    예) A. 미션 파악 및 전체 계획 수립
대문자-소문자.  →  하위 섹션     예) A-a. 계획 확인
대문자-소문자-숫자. → 세부 항목  예) A-a-1. mission-guide.md 요건 파악
```

계층을 건너뛰지 않는다. 최상위(`대문자.`) 없이 하위(`대문자-소문자.`)를 단독으로 쓰지 않는다.
이 계층 표기는 **이 계획 문서와 보고서 등 "문서"에만** 적용한다.
`.py` 코드 주석에는 적용하지 않는다(코드 주석은 한글 + 구분선 금지만 따른다).

## 섹션 - 산출물 대응표

| 섹션 | 산출물 |
|---|---|
| A | (본 계획 문서) |
| B | 화면 레이아웃 설계 메모 (보고서 초안에 재사용 가능) |
| C | 모듈·인터페이스 설계 메모 |
| D | `Dockerfile`, `requirements.txt`, `.dockerignore` |
| E | 앱 코드 (`app.py` 등) |
| F | 로컬 동작 확인 기록 |
| G | Docker Hub 이미지 |
| H | 보고서 PDF, 코드 zip |

---

## 보안(시크릿) 방침 — 모든 섹션 공통

이 미션은 모델을 **런타임에 GitHub(공개 LFS 리소스)에서 다운로드**하므로 애초에
다운로드용 자격증명이 없다. 그 외 시크릿도 코드·이미지에 남지 않도록 설계 단계부터 차단한다.
(상위 `CLAUDE.md` 보안 규약 — mission14 `gemini.key` 선례 준수)

- **자격증명을 아예 만들지 않는다(우선)**: 모델을 **GitHub 공개 저장소(onnx/models)**의
  LFS 미디어 URL에서 받으므로 다운로드에 토큰·API 키가 필요 없다.
  (Google Drive 재호스팅 방식은 용량·다운로드 쿼터 부담이 있어 채택하지 않는다.)
- **부득이 토큰/키가 필요하면**: 코드에 하드코딩하지 않고 환경변수(`os.environ`) ·
  Streamlit `secrets` · Docker secret으로 주입한다. **이미지 레이어에 굽지 않는다**
  (레이어에 한 번 들어가면 이후 삭제해도 이력에 남는다).
- **격리 대상 파일**: `.key` · `.env` · 자격증명·토큰 파일과 모델 캐시는
  `.dockerignore` + `.gitignore` 양쪽에 추가한다.
- **최종 산출물 점검**: 보고서 PDF · 코드 zip · Docker Hub 이미지 · 아티팩트 어디에도
  시크릿이 포함되지 않는지 제출 직전 확인한다.

---

## A. 미션 파악 및 전체 계획 수립

### A-a. 계획 확인

- [x] A-a-1. `mission-guide.md` 전체 요건 파악 — UI 4영역, 필수 기능 2종, 배포 2단계, 제출 형식
- [x] A-a-2. ONNX 모델 소스·버전 확정 — [onnx/models MNIST 저장소](https://github.com/onnx/models/tree/main/validated/vision/classification/mnist/model)의
      **`mnist-12.onnx`** (opset 12, 최신 `onnxruntime` 호환 확인, 26KB) 사용.
      ⚠ 이 저장소 파일은 **Git LFS**로 관리되어 `raw.githubusercontent.com` URL은
      실제 모델이 아닌 **130바이트 LFS 포인터 텍스트**를 반환한다. 실제 바이너리는
      **LFS 미디어 엔드포인트** `media.githubusercontent.com/media/onnx/models/main/...`
      로 받아야 한다(검증 완료).
- [x] A-a-3. 가이드 문구 오탈자 의심 항목 기록 — "1부터 9까지의 각 레이블"은 MNIST가
      0~9 10개 클래스라는 점을 감안하면 오탈자로 추정된다. 구현은 우선 0~9 전체
      시각화로 진행한다.
- [x] A-a-4. 클래스 범위 확정 — **0~9 (10클래스)** 로 진행 (사용자 확정)
- [x] A-a-5. 제출 폴더명 확정 — **`3팀_신호정_mnist-canvas`** (팀 3팀, 부가명칭 `mnist-canvas`, 사용자 확정).
      폴더는 구현 시작(D 섹션) 시점에 생성한다.
- [x] A-a-6. 참고 선례 확인 — 미션 13(ONNX 변환 경험), 미션 15(Docker Hub 배포 전체 흐름) 관례 검토

### A-b. 완료 확인

- [x] A-b-1. 전체 계획 문서화 완료 및 사용자 승인 (2026-07-21 승인)

---

## B. UX 구상 (화면 4영역 설계)

### B-a. 계획 확인

- [x] B-a-1. 전체 레이아웃 방식 초안 — `st.columns`로 좌(입력 캔버스)/우(전처리 이미지 +
      추론 결과) 분할, 하단에 이미지 저장소(히스토리)를 배치. 실제 구현 중 조정 가능한 초안이다.
- [x] B-a-2. 입력 캔버스 요건 정리 — `streamlit-drawable-canvas` 사용. 배경/펜 색상 조합은
      MNIST 원본 데이터 규약(검정 배경 + 흰 글씨)과 일치시킬지, 아니면 사용자에게 익숙한
      조합(흰 배경 + 검은 펜) 후 전처리 단계에서 반전할지 C 섹션에서 함께 결정한다.
- [x] B-a-3. 전처리 이미지 표시 요건 정리 — 실제 모델 입력 해상도(28×28 추정)를 그대로
      보여주면 육안 확인이 어려우므로 확대 표시(`st.image(..., width=140)` 등)한다.
- [x] B-a-4. 추론 결과 시각화 요건 정리 — 클래스별(0~9, A-a-4 확정 후 조정) 확률 막대 차트 +
      최고 확률 레이블 강조 표시.
- [x] B-a-5. 이미지 저장소 요건 정리 — `st.session_state` 리스트에 (썸네일, 예측 레이블,
      확률) 누적. 새로고침 시 초기화되는 세션 한정 저장임을 보고서에 명시한다.

### B-b. 완료 확인

- [x] B-b-1. 화면 구상 확정 (사용자 검토 완료)

---

## C. 개발 요소 정리 (모듈·인터페이스 설계)

### C-a. 계획 확인

- [x] C-a-1. 모듈 분리 방침 초안 — `app.py`(UI·상태) / `model_utils.py`(모델 다운로드·
      캐싱·추론) / `preprocess.py`(이미지 전처리) 3분리. 규모가 작으면 병합 가능하다.
- [x] C-a-2. 모델 다운로드·캐싱 방식 확정 — 최초 실행 시 **GitHub LFS 미디어
      엔드포인트**(`media.githubusercontent.com/media/onnx/models/main/.../mnist-12.onnx`)에서
      `.onnx`를 받아 로컬 캐시 경로에 저장하고, `onnxruntime.InferenceSession` 객체 자체를
      `@st.cache_resource`로 캐싱한다(요청마다 재다운로드·재초기화 방지가 핵심 요건).
      **주의: `raw.githubusercontent.com`은 LFS 포인터(130B)만 주므로 사용하지 않는다.**
      공개 리소스라 다운로드에 자격증명이 필요 없다(보안 방침 참조).
- [x] C-a-3. 전처리 함수 인터페이스 초안 — 입력: 캔버스 RGBA numpy 배열,
      출력: 모델 input spec에 맞춘 텐서. **전처리는 Pillow(+numpy)만 사용한다**
      (OpenCV 미사용 → 컨테이너에 `libGL1` 등 시스템 라이브러리 불필요, 사용자 확정).
      그레이스케일 변환 → 리사이즈 → 정규화 → 배치·채널 차원 추가의 대략적 순서만 확정한다.
- [x] C-a-4. 모델 input/output spec 확인 완료(`onnx`로 검증) —
      **INPUT `Input3` `[1,1,28,28]` float32**(배치 고정 1), **OUTPUT `Plus214_Output_0` `[1,10]` float32**.
      마지막 노드가 `Add`로 끝나 **모델 내부에 Softmax가 없다 → 출력은 raw logit**.
      전처리 세부(흑백 반전·정규화 상수)는 구현 시 고정 테스트 이미지로 실측 확정한다.
- [x] C-a-5. 추론 함수 인터페이스 확정 — 입력: 전처리된 `(1,1,28,28)` float32 텐서,
      출력: 10개 클래스 확률. **모델 출력이 raw logit이므로(C-a-4) 추론 함수에서
      softmax를 직접 적용**해 확률로 변환한 뒤 막대 차트에 넘긴다.
- [x] C-a-6. 예외 처리 범위 확정 — 캔버스 미입력(빈 화면) 상태에서 추론 버튼 클릭 시 처리,
      모델 다운로드 실패 시 처리

### C-b. 완료 확인

- [x] C-b-1. 개발 요소(모듈·인터페이스) 확정 (사용자 검토 완료)

---

## D. 도커 환경 설정

### D-a. 계획 확인

- [x] D-a-1. 베이스 이미지 확정 — `python:3.11-slim-bookworm` (미션 15와 통일, 이미 검증된 베이스)
- [x] D-a-2. 필요 라이브러리 확정 — `streamlit`, `streamlit-drawable-canvas`,
      `onnxruntime`, `numpy`, `Pillow`, 모델 다운로드용 `requests`(또는 표준 `urllib`).
      GitHub LFS 미디어 엔드포인트는 일반 HTTP GET으로 받으므로 `gdown`은 불필요하다.
      **OpenCV는 쓰지 않는다**(C-a-3, Pillow 전용).
      **`streamlit`과 `streamlit-drawable-canvas`는 버전을 함께 핀 고정**한다
      (drawable-canvas가 최신 streamlit과 자주 깨지므로, 검증된 조합으로 고정).
- [x] D-a-3. 포트·엔트리포인트 확정 — Streamlit 기본 포트 8501 노출,
      `streamlit run app.py --server.address=0.0.0.0`
- [x] D-a-4. 모델 파일 처리 방식 옵션 정리 — (a) 빌드 시점에 이미지에 내장
      (b) 최초 컨테이너 실행 시 다운로드 후 캐시. 가이드가 "모델 다운로드" 자체를
      필수 기능으로 명시하고 있으므로 (b)를 권장안으로 둔다.
- [x] D-a-5. 모델 파일 처리 방식 확정 — **(b) 런타임 다운로드**. **GitHub LFS 미디어
      엔드포인트**에서 최초 실행 시 내려받아 캐시한다(사용자 확정 — Google Drive는 용량·
      다운로드 쿼터 이슈로 배제). 이미지에는 모델을 굽지 않는다.

### D-b. 구현 진행

- [x] D-b-1. `requirements.txt` 작성 (버전 핀 고정 — streamlit 1.27.2 + drawable-canvas 0.9.3)
- [x] D-b-2. `Dockerfile` 작성 (python:3.11-slim-bookworm, libgomp1 추가, 헬스체크 포함)
- [x] D-b-3. `.dockerignore` 작성 — 모델 캐시·자격증명 파일(`.key`/`.env` 등)·
      `__pycache__`·`.git` 등 빌드 컨텍스트에서 제외 (`.gitignore`도 동일 격리)

### D-c. 구현 검증

- [x] D-c-1. `docker build` 성공 확인 (핀 고정 버전 전부 정상 설치)
- [x] D-c-2. 이미지 용량 확인 — **980MB** (`input-recognition-pilot:1.0`)

### D-d. 완료 확인

- [x] D-d-1. 도커 환경 확정 (빌드·기동·헬스체크 통과)

---

## E. Streamlit 웹 구현 (필수 기능 포함)

### E-a. 계획 확인

- [x] E-a-1. 구현 순서 확정 — 모델 다운로드·캐싱 → 전처리 함수 → 추론 함수 → UI 4영역
      순으로 상향식(bottom-up) 구현한다. UI 연결 전에 핵심 로직을 독립적으로 검증하기 위함이다.
- [x] E-a-2. 단위 검증 방식 확정 — UI 연결 전, 별도 스크립트에서 고정 테스트 이미지
      (저장된 손글씨 숫자 PNG 등)로 전처리·추론 함수만 먼저 검증한다.

### E-b. 구현 진행

- [x] E-b-1. 모델 다운로드·캐싱 함수 구현 (`model_utils.py`, `@st.cache_resource`)
- [x] E-b-2. 전처리 함수 구현 (`preprocess.py`, Pillow 전용, MNIST 중앙 정렬)
- [x] E-b-3. 추론 함수 구현 (`model_utils.predict`, raw logit → softmax)
- [x] E-b-4. UI 4영역 구현 및 연결 (`app.py`)

### E-c. 구현 검증

- [x] E-c-1. 전처리·추론 함수 단위 검증 — 실제 MNIST 300장 시뮬레이션 캔버스 입력
      전처리→추론 정확도 **95.7%**, shape/dtype/softmax 합·빈 입력 방어 확인
- [x] E-c-2. 코드 경로 end-to-end 확인 — 컨테이너 내부에서 모델 다운로드→세션→predict 정상.
      (브라우저 캔버스 클릭 확인은 F-b-2/3에서 사용자 검토)

### E-d. 완료 확인

- [x] E-d-1. 필수 기능 전체 구현 완료 (모델 관리·전처리·추론·UI 4영역)

---

## F. 로컬 동작 확인 (localhost)

### F-a. 계획 확인

- [x] F-a-1. 확인 범위 확정 — 컨테이너 기동(`docker run`) → `localhost:8501` 접속 →
      4영역 전체 정상 동작(여러 숫자로 반복 테스트) → 이미지 저장소 누적 확인

### F-b. 구현 검증

- [x] F-b-1. 컨테이너 기동 및 접속 확인 — `docker run -p 8501:8501`, health `200/ok`, streamlit 정상 서빙
- [x] F-b-2. 숫자별 그려서 예측 결과 확인 (브라우저에서 사용자 검토 완료)
- [x] F-b-3. 이미지 저장소 누적·표시 정상 확인 (스크롤 UX 포함, 사용자 검토 완료)

### F-c. 완료 확인

- [x] F-c-1. 로컬 동작 확인 완료 (사용자 검토) — 1차 코드 리뷰 반영분(중복 추론 방지,
      저장소 스크롤, 검증 스크립트) 포함 확인

---

## G. Docker Hub 배포

### G-a. 계획 확인

- [x] G-a-1. 네이밍 관례 확인 — 미션 15의 `yopkigom/<repo>:<version>` 관례를 따른다
- [x] G-a-2. 이미지 저장소명 확정 — **`yopkigom/input-recognition-pilot`** (사용자 확정).
      연관 태그를 부여한다(태그 방침은 G-a-3).
- [x] G-a-3. 태깅 방침 확정 — 명시적 버전 태그(`:1.0` 등) 사용, `latest` 단독 사용 금지
      (미션 15 관례 재사용)

### G-b. 구현 진행

- [x] G-b-1. `docker tag` 및 `docker push` — `:1.0`, `:latest` 두 태그 푸시 완료
      (digest `sha256:ae278ad5…`, 일부 레이어는 미션15 `student-perf-train`에서 재사용)
- [ ] G-b-2. Docker Hub 저장소 설명 작성 — `docker push`로는 설정 불가.
      Docker Hub 웹 UI에서 README 내용을 붙여넣어야 함 (사용자 수행 필요)

### G-c. 구현 검증

- [x] G-c-1. 로컬 이미지 완전 삭제 후 Docker Hub에서 재 `pull` → digest 일치,
      health 200, 모델 다운로드(26,143B)·추론(확률 합 1.0) 정상, 로그 에러 없음

### G-d. 완료 확인

- [x] G-d-1. Docker Hub 배포 완료 —
      **https://hub.docker.com/r/yopkigom/input-recognition-pilot**
      (`docker pull yopkigom/input-recognition-pilot:1.0`)

---

## H. 제출

### H-a. 계획 확인

- [x] H-a-1. 제출 항목 확정 — ① 보고서 PDF(프로젝트 개요·코드 설명·Docker Hub URL)
      ② 코드 zip(주석 포함 소스 및 관련 파일 전체)
- [x] H-a-2. 제출 폴더명 확정 — **`3팀_신호정_mnist-canvas`** (A-a-5와 동일)

### H-b. 구현 진행

- [ ] H-b-1. 보고서 작성 (Markdown 초안 → PDF 변환)
- [ ] H-b-2. 코드 zip 생성 (모델 캐시·**시크릿/자격증명 파일**·대용량 산출물 제외 확인 — 보안 방침)

### H-c. 완료 확인

- [ ] H-c-1. 제출 준비 완료
