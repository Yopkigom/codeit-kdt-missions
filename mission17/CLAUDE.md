# CLAUDE.md — 미션 17

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## 필수 참조 파일

작업 시작 전 반드시 아래 두 문서를 순서대로 읽는다.

1. [mission-guide.md](mission-guide.md) — 미션 소개, UI/기능 가이드라인, 제출 안내 (원본 과제 지침)
2. [mission-concept.md](mission-concept.md) — 8단계 구현 계획 (작업 순서 규약)

**현재 상태**: 이 두 문서만 존재하며 코드·폴더는 아직 생성되지 않았다.
즉 이 미션은 **착수 전(계획 단계)** 이다. 새로 작업을 시작할 때는
`mission-concept.md`의 순서(가이드 파악 → UX 구상 → 개발 요소 정리 →
도커 환경 설정 → Streamlit 구현 → 로컬 확인 → Docker Hub 배포 → 제출)를
건너뛰지 말 것.

## Overview

**MNIST 손글씨 숫자 인식 — Streamlit 기반 웹 서비스 + Docker 배포.**

사용자가 캔버스에 마우스로 숫자를 그리면, [ONNX Model Zoo의 MNIST 모델](https://github.com/onnx/models/tree/main/validated/vision/classification/mnist/model)로
추론해 예측 결과를 보여주는 웹 앱을 만들고, 이를 Docker 이미지로 빌드해
Docker Hub에 배포한다. 미션 13·14가 On-Device 포팅(ONNX/모바일) 중심이고
미션 15가 재현성(Docker) 중심이었다면, 미션 17은 **그 둘의 결합** —
ONNX 추론 서비스를 컨테이너로 패키징해 배포하는 미션이다.

## 필수 UI 구성 (4개 화면 영역)

가이드가 명시한 요건이므로 임의로 축소하지 말 것.

| 영역 | 내용 |
|---|---|
| 입력 캔버스 | `streamlit-drawable-canvas`로 마우스 드로잉 입력 |
| 전처리 이미지 표시 | 모델 입력 규격으로 변환된 이미지를 시각적으로 표시 (사용자가 전처리 결과를 확인 가능해야 함) |
| 모델 추론 결과 | 0~9 각 레이블의 예측 확률을 막대 차트로 시각화 |
| 이미지 저장소 | 그린 이미지 + 예측 레이블/확률을 함께 누적 표시 (세션 내 히스토리) |

## 필수 기능

- **모델 관리**: MNIST ONNX 모델(**`mnist-12.onnx`**)을 **런타임에 GitHub LFS 미디어
  엔드포인트에서 다운로드** + 세션 간 캐싱
  (Streamlit이면 `@st.cache_resource` 계열이 자연스러운 선택 — 매 요청마다 재다운로드/재로딩하지 않도록 할 것).
  모델을 이미지에 굽지 않는다.
  ⚠ **`raw.githubusercontent.com`은 130B LFS 포인터만 반환**하므로,
  `media.githubusercontent.com/media/onnx/models/main/.../mnist-12.onnx`를 써야 한다.
- **이미지 처리 및 추론**: 캔버스 원본 이미지를 모델 입력 사양에 맞게 전처리하는 함수와,
  전처리 결과로 추론을 수행하는 함수를 분리 개발. **전처리는 Pillow 전용**(OpenCV 미사용).
  (확인: `mnist-12.onnx`는 입력 `Input3` `(1,1,28,28)` float32, 출력 `Plus214_Output_0` `(1,10)`.
  **모델 내부에 Softmax가 없어 출력은 raw logit** → 앱에서 softmax를 직접 적용한다.
  정규화 상수·흑백 반전 여부는 고정 테스트 이미지로 실측 확정할 것 — 캔버스 원본은 반전·중앙 정렬이 필요할 가능성이 높다.)

## 배포

- **Dockerfile** 작성 → 이미지 빌드 → `localhost`에서 기능 확인 후 Docker Hub 업로드
- Docker Hub 저장소: **`yopkigom/input-recognition-pilot`** (버전 태그 + 연관 태그, `latest` 단독 금지 — 미션 15 관례)
- 모델은 이미지에 포함하지 않고 **런타임에 GitHub LFS 미디어 엔드포인트에서 다운로드**한다(캐싱)

## 보안 주의 (시크릿 유출 방지)

상위 `CLAUDE.md` 보안 규약(mission14 `gemini.key` 선례)을 이 미션에도 적용한다.
모델을 런타임에 **GitHub 공개 저장소(LFS)**에서 받으므로 다운로드용 자격증명이 애초에 없다.
그 외 시크릿도 코드·이미지에 남지 않게 설계로 차단한다.

- **자격증명을 만들지 않는 것을 우선**한다: 모델을 GitHub 공개 LFS 미디어 URL에서 받으므로
  토큰·API 키가 필요 없다(Google Drive 재호스팅은 용량·다운로드 쿼터 부담이 있어 배제).
- 부득이 토큰/키가 필요하면 **하드코딩 금지** — 환경변수 · Streamlit `secrets` · Docker secret으로
  주입하고, **이미지 레이어에 굽지 않는다**(레이어에 한 번 들어가면 삭제해도 이력에 남는다).
- `.key` · `.env` · 자격증명·토큰 파일과 모델 캐시는 **`.dockerignore` + `.gitignore` 양쪽**에 추가한다.
- 제출 직전, 보고서 PDF · 코드 zip · Docker Hub 이미지 · 아티팩트에 시크릿이 없는지 최종 확인한다.

## 제출 규약

`mission-guide.md`의 제출 안내를 그대로 따른다.

```
mission17/3팀_신호정_mnist-canvas/
├── (보고서) — 프로젝트 개요 · 코드 설명 · Docker Hub URL 포함 PDF
└── (코드)   — 주석 포함 소스 코드 + 관련 파일 전체를 zip
```

폴더명은 **`3팀_신호정_mnist-canvas`** 로 확정됐다(팀은 3팀 — mission16의 4팀에서 변경).
폴더는 구현 시작(도커 환경 설정) 시점에 생성한다.

## 문서 작성 규약

**문서(계획서·보고서)**: 섹션 계층 표기(`A.` / `A-a.` / `A-a-1.`)를 적용한다.
`mission-concept.md`가 이 표기를 따른다.

**코드(`.py`) 주석**: 이 미션은 노트북이 아니라 Streamlit 앱 중심이다.
따라서 노트북용 규약(**섹션 계층 표기·"셀 위치 표기"**)은 **코드 주석에 적용하지 않는다.**
코드 주석은 상위 규약의 기본만 따른다 — 되도록 한글, 주석 내 구분선(`---` 등) 금지.

## 작업 시 유의점

- `mission-concept.md`의 단계를 건너뛰지 말 것. 특히 "도커 환경 설정"은
  Streamlit 구현 이전 단계로 명시되어 있다 — 로컬에서 라이브러리 설치로 먼저
  개발하고 나중에 컨테이너화하는 순서로 바꾸는 것은 계획과 어긋난다.
- 이 미션에는 데이터셋 분리(상위 CLAUDE.md의 `/mnt/wsl_data/datasets/` 표) 대상 항목이 없다.
  ONNX 모델은 ONNX Model Zoo(GitHub, LFS)에서 런타임에 내려받는 외부 리소스이며,
  로컬 데이터셋 경로 문제와는 무관하다.
