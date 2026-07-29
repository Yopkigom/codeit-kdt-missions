# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# CLAUDE.md — 미션 18

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## 필수 참조 파일

문서는 `docs/draft/`(과제 지침·구상)와 `docs/plan/`(작업 순서·설계 사양)으로 나뉜다.
작업 시작 전 반드시 아래 문서를 순서대로 읽는다.

1. [docs/draft/mission-guide.md](docs/draft/mission-guide.md) — 원본 과제 지침
   (필수/`심화` 기능 구분, 제출 안내)
2. [docs/draft/mission-concept.md](docs/draft/mission-concept.md) — **서비스 구상**.
   확정된 설계 결정을 서술한다 (무엇을 만드는가).
   최초 9단계 원안은 이 문서 `H. 원안 기록`에 이력으로만 남아 있다
3. [docs/plan/PROJECT_PLAN.md](docs/plan/PROJECT_PLAN.md) — **작업 순서**. 체크리스트와
   진행 상태만 담는다 (어떤 순서로 진행하는가).
   판단 근거·제약은 담지 않으며 그쪽은 이 문서가 기준이다
4. [docs/plan/architecture.md](docs/plan/architecture.md) — **구조도 · ERD · 주요 흐름**(mermaid).
   보고서 제출 항목의 원본이다. 설계가 바뀌면 이 문서도 함께 갱신한다
5. [docs/plan/screens.md](docs/plan/screens.md) — **화면 레이아웃 · 네비게이션 · 공통 UI 요소**.
   프론트엔드 구현(H 섹션)의 기준이다
6. [docs/plan/implementation.md](docs/plan/implementation.md) — **구성 요소별 구현 사양**.
   폴더 구조 · API 계약 · DB 스키마 · 모듈 책임. 코드 작성의 직접 기준이다

세 문서는 역할이 다르다. 진행 순서는 `PROJECT_PLAN.md`, 설계 내용은 `mission-concept.md`,
**판단 근거와 제약은 이 문서**를 본다.

**현재 상태**: 구현 진행 중. `3팀_신호정_미션18/`에 백엔드·프론트엔드 코드가 있으며
백엔드 테스트(23건)와 화면 렌더링 검증까지 통과했다.
시드 데이터(영화 6편 · 리뷰 72건) 적재와 통합 검증까지 마쳤다.
**남은 작업**: 배포(J) → 보고서(K).

실측 기록은 [docs/plan/model-eval.md](docs/plan/model-eval.md)(모델)과
[docs/plan/service-eval.md](docs/plan/service-eval.md)(서비스 통합)에 있다.
제출용 public 저장소: `Yopkigom/mission18-movie-review-sentiment`.

## 확정 사항

계획 검토에서 결정된 항목이다. 재론하지 말고, 바꾸려면 세 문서를 함께 갱신한다.

| 항목 | 결정 |
|---|---|
| 감성 스칼라 | `부정 -1` / `중립 0` / `긍정 +1`, 평점 = 산술평균 |
| 평점 표기 | 저장·API는 원값, **표시만 0\~5 별점 환산** |
| 평점 필드 | `external_rating`(TMDB)과 `sentiment_rating`(파생) **분리** |
| 영화 메타데이터 | **TMDB API** (크롤링 폐기) |
| 리뷰 데이터 | **NSMC** 공개 데이터셋을 영화에 임의 매핑 |
| 감성 분석 모델 | 미션 13 ONNX **그대로 재사용** (재-export 없음) |
| DB | **SQLite** |
| 백엔드 호스팅 | **Cloud Run** (메모리 1GiB, `--max-instances=1`) |
| 프론트 호스팅 | **Streamlit Community Cloud** (Vercel 불가) |
| Vector DB | **제외** |
| 저장소 | 아카이브 private 유지 + 제출 전용 public 저장소 신설 |
| 리뷰 `제목` 필드 | **유지**. NSMC에 없으므로 시드는 본문에서 파생 생성 |
| 제출 폴더 | `3팀_신호정_미션18` |

## Overview

**영화 정보 · 리뷰 · 리뷰 감성 분석 웹 애플리케이션 — Streamlit(프론트) + FastAPI(백엔드).**

스프린트의 **마지막 미션**이며, 프론트엔드 · 백엔드 · 모델 서빙을 하나의 서비스로
통합하는 것이 목적이다. 미션 17이 Streamlit 단일 앱 + ONNX 추론이었다면,
미션 18은 여기에 **백엔드 분리 · DB · REST API**가 추가된 3-tier 구성이다.

```
[Streamlit]  ──HTTP──>  [FastAPI]  ──>  [DB: 영화 / 리뷰]
   화면만                 데이터 소유         │
                                        [감성 분석 모델 서빙]
```

**핵심 제약**: 모든 데이터는 백엔드가 소유한다. Streamlit 쪽에 별도 저장 기능
(로컬 파일 · 세션 영속화 등)을 두지 않는다 — 가이드에 명시된 요건이다.

## 필수 / `심화` 경계

가이드가 둘을 구분하고 있으므로 임의로 섞지 말 것. `심화`는 선택이지만
`mission-concept.md`는 리뷰·감성 분석까지 수행하는 것을 전제로 작성되어 있다.

| 구분 | 프론트엔드 (Streamlit) | 백엔드 (FastAPI) |
|---|---|---|
| **필수** | 영화 목록(제목·포스터·평균 평점), 영화 추가 폼 | 영화 등록 / 전체·단건 조회 / 삭제 |
| **`심화`** | 리뷰 등록, 감성 분석 결과 표시, 최근 10개 리뷰 목록 | 리뷰 CRUD, 평점 조회(감성 점수 평균), 감성 분석 |

## 데이터 모델

`mission-concept.md` 원안에 필드를 보강한 구조다. ERD가 보고서 제출 항목이므로
여기서 벗어나면 보고서도 함께 갱신한다.

| 영화 | 리뷰 |
|---|---|
| ID, 제목, 개봉일, 감독, 장르, 포스터 URL, `external_rating`, `tmdb_id` | ID, 작성자, 영화 ID(FK), 제목, 내용, `created_at`, `sentiment_label`, `sentiment_score`, `model_version` |

- **평점 2종은 별개 필드다.** `external_rating`은 TMDB `vote_average`(0\~10) 수집값,
  `sentiment_rating`은 감성 분석 결과의 평균(-1\~+1) **파생값**이다. 한 필드에 두면 덮어쓴다.
- **감성 스칼라**: `부정 -1` / `중립 0` / `긍정 +1`. 영화 평점은 소속 리뷰 스칼라의 산술평균.
  **표시만 0\~5 별점으로 환산**한다(`(x + 1) / 2 * 5`). 환산은 표현 계층에서만 수행하고
  DB·API 응답은 원값을 유지한다.
- **추론은 리뷰 등록 시 1회**만 하고 결과를 저장한다. 조회 시마다 추론하면 목록 10건당
  10회 추론이 발생한다.
- `created_at`은 "최근 10개 리뷰" 화면과 페이지네이션 정렬 기준이라 생략할 수 없다.
  `model_version`은 모델 교체 시 과거 점수와 기준이 달라져 평균이 오염되는 것을 막는다.

## 감성 분석 모델 — 미션 13 자산 재사용

가이드는 "적절한 모델 리서치" + "**모델 경량화 방식에 대해 고민**"을 요구한다.
이 과정에서 이미 만든 자산이 있으므로 처음부터 새로 학습하지 않는다.

```
../mission13/CheckPoint/modelA_full_ft.onnx (+ .onnx.data, 합계 약 128MB)
../mission13/vocab_for_onnx/               # tokenizer.json · vocab.txt
```

- ELECTRA 계열 경량 모델(hidden 256 · 12 layers · vocab 54,343), **3-class 분류**.
  `mission-concept.md`의 `좋아요 / 보통 / 별로에요` 3단 표기와 클래스 수가 맞는다.
  단 `id2label`이 `LABEL_0~2`이므로 **어느 인덱스가 어느 감성인지 미션 13 노트북에서
  확인 후 고정**할 것. 추정으로 매핑하지 말 것.
- 미션 13은 Full FT / Freeze / **LoRA-PEFT** 3종을 비교했고 ONNX는 Fixed Shape로
  export되어 있다. "경량화 고민" 항목은 이 비교 결과를 근거로 서술할 수 있다.
- **엣지/서빙 제약**: `.onnx` + `.onnx.data`(external data) 2파일이 짝이며 분리하면 로드에
  실패한다. Fixed Shape이므로 **입력 시퀀스 길이가 export 시점 값으로 고정**된다 —
  리뷰 길이가 이를 넘으면 truncation, 짧으면 padding이 필수다.
- **미션 13 ONNX를 그대로 쓴다.** 재-export하지 않는다.
- `confidence`(= `max(softmax(logits))`)를 산출해 저장하되 **보정 로직에는 쓰지 않는다.**
  저확신 예측은 화면에 `판정 애매` 등으로 표기하는 것이 1차 대응이다 — 추가 의존성도
  지연도 없이 모델의 불확실성을 사용자에게 전달할 수 있다.
- **도메인 이동 주의**: 미션 13의 학습 도메인은 쇼핑몰·SNS 리뷰인데, 본 미션의 추론 대상은
  영화 리뷰(NSMC)다. 성능 저하 가능성이 있으므로 NSMC 정답 라벨로 실측하고
  그 결과를 보고서 논점으로 삼는다.

## 데이터 소스

크롤링 계획은 폐기됐다. 두 개의 공식/공개 소스를 쓴다.

| 대상 | 소스 | 주의 |
|---|---|---|
| 영화 메타데이터 | **TMDB API** | 감독은 `/movie/{id}`에 없다 — `/movie/{id}/credits`의 `crew`에서 `job == "Director"` 추출. **출처 표기 의무** 있음 |
| 리뷰 | **NSMC** (네이버 영화 리뷰 20만 건) | 필드는 `id / document / label`뿐 — 제목·작성자·등록일은 파생 생성. **리뷰-영화 대응은 임의 배정**이므로 보고서에 명시 |

TMDB 리뷰를 쓰지 않는 이유: 대부분 영어라 한국어 모델에 넣을 수 없고,
영화당 편수가 적어 "각 영화당 리뷰 10건 이상" 캡처 요건을 채우지 못한다.

**시드는 감성이 고르게 섞이도록 큐레이션한다.** 전부 `좋아요`로 쏠리면 3단 표기 기능을
화면으로 증명할 수 없다. NSMC 라벨로 긍/부정을 배분하고 중립은 모델 예측으로 선별한다.

## 배포

```
[Streamlit Community Cloud]  ──HTTPS──>  [Cloud Run: FastAPI + ONNX + SQLite]
        (제출용 public repo)                        (동일 컨테이너)
```

- **Vercel은 Streamlit을 구동할 수 없다**(서버리스, 상시 프로세스·WebSocket 불가).
  가이드도 Streamlit Cloud를 명시한다.
- **백엔드는 Cloud Run** — 메모리를 1GiB로 지정할 수 있다. Render 무료 티어는 512MB 고정이라
  ONNX 128MB + onnxruntime + 토크나이저 + FastAPI 조합에 빠듯하다(추정 피크 400\~600MB).
- **SQLite × Cloud Run 제약**: 컨테이너 파일시스템이 인메모리라 ① 쓰기가 메모리 예산을
  잠식하고 ② 인스턴스 교체 시 데이터가 사라지며 ③ 인스턴스가 2개 이상이면 각자 다른 DB를 본다.
  → 시드 데이터를 이미지에 굽고 `--max-instances=1`로 고정하며,
  "배포본은 데모용, 영속 저장은 로컬 기준"임을 보고서에 명시한다.
- 모델은 `lifespan`에서 **1회 로드 후 재사용**. 요청마다 세션을 만들지 않는다.
- 이미지 태그는 버전 태그 + 연관 태그를 붙인다. `latest` 단독 금지(미션 15·17 관례).
- 백엔드 base URL은 `localhost` 하드코딩 금지 — 환경변수 / `st.secrets` 주입.
- **Vector DB는 도입하지 않는다.** 미션 18에 검색·RAG 요건이 없고, 하이퍼파라미터·인덱스
  영속성·이웃 라벨 편향 관리 부담이 평가 대상이 아닌 곳에 붙는다. 저확신 예측 대응이
  필요하다는 근거는 B-c-5(NSMC 도메인 성능 실측)에서 확인된 뒤에 검토한다 —
  관측치 없이 보정 장치를 먼저 설계하지 않는다.

## ⚠ 저장소 분리 (필수)

아카이브 저장소 `Yopkigom/codeit-kdt-missions`는 **private을 유지한다.**
`mission14/gemini.key`(실제 API 키)가 트리에 존재하며, 현재는 `.gitignore`로 막혀
커밋 이력에 오른 적이 없다 — public 전환은 이 방어선을 무의미하게 만든다.

Streamlit Community Cloud 연결용으로 **미션 18 제출 코드만 담은 별도 public 저장소를 신설**한다.

## 실행 (구현 이후)

제출 규약상 코드는 `frontend/` · `backend/`로 분리한다. 로컬 실행은 conda `ai` 환경 기준:

```bash
# 백엔드 — Swagger UI는 http://localhost:8000/docs (FastAPI Docs 캡처가 제출 항목)
uvicorn app.main:app --reload --port 8000   # backend/ 에서

# 프론트엔드
streamlit run app.py                        # frontend/ 에서
```

FastAPI Docs 전체 캡처가 제출물이므로 **모든 엔드포인트에 `summary` / `description` /
응답 모델을 채워둔다.** 캡처 직전에 몰아서 쓰지 말 것.

## 제출 규약

```
mission18/3팀_신호정_미션18/
├── (보고서 PDF) — 서비스 개요 · 구조도(프론트/백/모델 서빙) · ERD
│                  · FastAPI Docs 전체 캡처 · 동작 캡처
└── (코드) — frontend/ 와 backend/ 로 분리
```

- 동작 캡처 요건: **영화 3개 이상**, **각 영화당 리뷰 10개 이상**. 리뷰 목록이
  10개 단위 페이지네이션이므로 시드 데이터 수량을 여기에 맞춰 준비한다.
- 보고서에 반드시 명시할 항목: TMDB 출처 표기, NSMC 리뷰-영화 대응이 임의 배정이라는 사실,
  배포본 SQLite의 비영속성, 도메인 이동에 따른 모델 성능 변화.

## 문서·코드 규약

- **문서(계획서·보고서)**: 섹션 계층 표기(`A.` / `A-a.` / `A-a-1.`)를 적용한다.
- **도식**: mermaid로 작성해 `docs/plan/architecture.md`에 둔다. 오프라인 렌더가 필요하면
  `../mission17/mermaid.min.js`를 쓴다(미션 17 보고서 생성에 사용한 사본).
- **코드(`.py`)**: 이 미션은 노트북이 아니므로 노트북용 규약(섹션 계층 표기·"셀 위치 표기")을
  코드 주석에 적용하지 않는다. 상위 규약의 기본만 따른다 — 되도록 한글,
  주석 내 구분선(`---`, `===` 등) 금지.

## 작업 시 유의점

- **TMDB API 키 관리**: 수집은 **로컬에서 1회 실행**해 DB에 적재하고, 적재본만 배포한다.
  이렇게 하면 런타임에 키가 필요 없다. 키는 `.env` + `.gitignore`로 관리하고
  코드·이미지 레이어·보고서·제출물 어디에도 남기지 않는다(미션 14 `gemini.key` 선례).
  포스터는 URL만 저장하고 이미지 파일을 저장소에 복사하지 않는다.
- **상위 문서와의 차이**: 상위 `CLAUDE.md`는 "아카이브는 git 저장소가 아니다"라고 하나,
  현재 `missions/`는 git 저장소다(`origin`: `Yopkigom/codeit-kdt-missions`, private).
- 이 미션에는 `/mnt/wsl_data/datasets/` 분리 대상 데이터셋이 없다. NSMC는 공개 데이터셋이므로
  용량에 따라 `/mnt/wsl_data/datasets/` 분리 여부를 F 단계에서 판단한다.
