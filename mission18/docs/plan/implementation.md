# 미션 18 구현 사양

구성 요소별 구현 내용을 정리한 문서다. 설계 근거는 [CLAUDE.md](../../CLAUDE.md),
구조도는 [architecture.md](architecture.md), 화면은 [screens.md](screens.md)를 본다.

## A. 폴더 구조

제출 규약상 `frontend/`와 `backend/`로 나눈다.
데이터 준비 스크립트는 DB를 만드는 주체가 백엔드이므로 `backend/scripts/`에 둔다.

```
3팀_신호정_미션18/
├── backend/
│   ├── app/
│   │   ├── main.py              # FastAPI 인스턴스 · lifespan · CORS · 라우터 등록
│   │   ├── config.py            # 환경변수 로딩
│   │   ├── database.py          # 엔진 · 세션 · PRAGMA 설정
│   │   ├── models.py            # SQLAlchemy 모델
│   │   ├── schemas.py           # Pydantic 스키마
│   │   ├── routers/
│   │   │   ├── movies.py
│   │   │   ├── reviews.py
│   │   │   └── sentiment.py
│   │   ├── services/
│   │   │   ├── movie_service.py # 평점 집계 포함
│   │   │   └── review_service.py# 감성 분석 연동
│   │   ├── repositories/
│   │   │   ├── movie_repo.py
│   │   │   └── review_repo.py
│   │   └── ml/
│   │       ├── loader.py        # ONNX 세션 · 토크나이저 로드
│   │       └── predictor.py     # 전처리 → 추론 → 후처리
│   ├── ml_assets/               # .onnx + .onnx.data + tokenizer.json
│   ├── data/movies.db           # 시드 DB (이미지에 포함)
│   ├── scripts/
│   │   ├── collect_tmdb.py      # TMDB 수집 (로컬 1회)
│   │   ├── build_reviews.py     # NSMC 선별 · 매핑
│   │   └── seed_db.py           # 시드 DB 생성
│   ├── tests/
│   ├── requirements.txt
│   ├── Dockerfile
│   └── .dockerignore
└── frontend/
    ├── app.py                   # 홈 · 영화 목록 / 상세 분기
    ├── pages/
    │   ├── 1_영화_추가.py
    │   ├── 2_리뷰_등록.py
    │   └── 3_최근_리뷰.py
    ├── lib/
    │   ├── api_client.py        # 백엔드 호출 · 오류 변환
    │   ├── formatting.py        # 별점 환산 · 말줄임
    │   └── components.py        # 포스터 카드 · 감성 배지 · 페이지네이션
    ├── .streamlit/config.toml
    └── requirements.txt
```

## B. 확정한 설계 결정

계획서에 미결로 남아 있던 항목을 아래와 같이 정한다.

### B-a. 평점은 캐시하지 않고 조회 시 집계한다

`sentiment_rating`을 영화 테이블 컬럼으로 두면 **리뷰 등록 · 리뷰 삭제 · 영화 삭제**
세 경로에서 갱신해야 하고, 한 곳만 빠뜨려도 화면의 평점이 조용히 틀어진다.
시드 규모가 수십\~수백 건이라 `LEFT JOIN` + `AVG` 한 번으로 충분하다.

캐시는 리뷰가 수만 건 쌓여 목록 조회가 느려진 것이 **측정된 뒤**에 도입한다.

### B-b. 리뷰가 없으면 평점은 `null`이다

`AVG`는 행이 없으면 `NULL`을 돌려준다. 이 값을 0으로 바꾸지 않는다 —
0은 `중립`이라는 의미를 이미 갖고 있어서, 리뷰 없음과 중립 평가가 구분되지 않는다.
화면에서는 `평점 없음`으로 표기한다.

### B-c. 삭제는 DB가 연쇄 처리한다

리뷰를 애플리케이션 코드로 먼저 지우고 영화를 지우는 방식은 중간 실패 시
고아 데이터가 남는다. FK에 `ON DELETE CASCADE`를 걸어 DB가 처리하게 한다.

⚠ **SQLite는 외래 키 제약이 기본으로 꺼져 있다.** 연결마다
`PRAGMA foreign_keys=ON`을 실행하지 않으면 CASCADE가 동작하지 않고,
테스트에서도 조용히 통과한다.

### B-d. 중복 영화 방지

- `tmdb_id`에 UNIQUE. 단 **nullable**이다 — 화면에서 수동 등록한 영화에는 TMDB ID가 없다.
  SQLite는 UNIQUE 컬럼에 `NULL` 다중 입력을 허용하므로 문제되지 않는다.
- `(title, release_date)` 복합 UNIQUE. 같은 제목의 리메이크는 개봉일이 달라 통과한다.

### B-e. 토크나이저는 `tokenizers`만 쓴다

`transformers`를 설치하면 의존성이 크게 늘고 경우에 따라 torch까지 끌려온다.
미션 13이 남긴 `tokenizer.json`은 Rust 구현체인 `tokenizers` 패키지로
단독 로드할 수 있다. Cloud Run 메모리 예산에 직접 영향을 주는 선택이다.

## C. 백엔드

### C-a. API 계약

| 메서드 | 경로 | 설명 | 성공 |
|---|---|---|---|
| GET | `/health` | 헬스 체크 · 모델 로드 여부 | 200 |
| GET | `/movies` | 영화 목록 (평균 평점 · 리뷰 수 포함) | 200 |
| POST | `/movies` | 영화 등록 | 201 |
| GET | `/movies/{movie_id}` | 영화 단건 조회 | 200 |
| DELETE | `/movies/{movie_id}` | 영화 삭제 (리뷰 연쇄 삭제) | 204 |
| GET | `/movies/{movie_id}/reviews` | 영화별 리뷰 (페이지네이션) | 200 |
| GET | `/movies/{movie_id}/rating` | 평점 조회 (감성 점수 평균) | 200 |
| POST | `/reviews` | 리뷰 등록 (감성 분석 자동 실행) | 201 |
| GET | `/reviews` | 전체 리뷰 최신순 (페이지네이션) | 200 |
| DELETE | `/reviews/{review_id}` | 리뷰 삭제 | 204 |
| POST | `/sentiment/analyze` | 감성 분석만 수행 (저장 없음) | 200 |

- `GET /reviews`가 "최근 10개 리뷰" 화면을 담당한다(`limit=10&offset=0`).
- `POST /sentiment/analyze`는 저장 없이 모델만 호출한다. 감성 분석 기능을
  Swagger에서 직접 시연할 수 있어 **FastAPI Docs 캡처 제출물에 유리하다.**

#### C-a-1. 상태 코드

| 코드 | 상황 |
|---|---|
| 404 | 존재하지 않는 `movie_id` · `review_id` |
| 409 | 중복 영화 (`tmdb_id` 또는 제목+개봉일 충돌) |
| 422 | Pydantic 검증 실패 (FastAPI 기본) |
| 503 | `/sentiment/analyze`에서 모델 미로드 · 추론 실패 |

리뷰 등록 중 추론이 실패해도 **201을 반환한다.** 리뷰 저장 자체는 성공했기 때문이다.
감성 필드만 `null`로 채우고 응답에 그대로 담아 화면이 안내를 띄우게 한다.

#### C-a-2. 페이지네이션 응답

```json
{
  "items": [],
  "total": 42,
  "limit": 10,
  "offset": 0
}
```

총 페이지 수 계산에 `total`이 필요하다. 정렬은 `created_at DESC, id DESC`
(같은 초에 생성된 시드 리뷰의 순서를 고정하기 위해 `id`를 보조 키로 둔다).

### C-b. Pydantic 스키마

```python
class MovieCreate(BaseModel):
    title: str = Field(min_length=1, max_length=200)
    release_date: date
    director: str | None = Field(default=None, max_length=100)
    genre: str | None = Field(default=None, max_length=100)
    poster_url: HttpUrl | None = None
    external_rating: float | None = Field(default=None, ge=0, le=10)
    tmdb_id: int | None = None


class MovieSummary(BaseModel):
    """목록 화면용. 포스터·제목·평점만 있으면 된다."""
    id: int
    title: str
    poster_url: str | None
    sentiment_rating: float | None   # -1 ~ +1, 리뷰 없으면 None
    review_count: int


class MovieDetail(MovieSummary):
    release_date: date
    director: str | None
    genre: str | None
    external_rating: float | None    # TMDB 0 ~ 10, sentiment_rating 과 별개
    tmdb_id: int | None


class ReviewCreate(BaseModel):
    movie_id: int
    author: str = Field(min_length=1, max_length=50)
    title: str | None = Field(default=None, max_length=200)
    content: str = Field(min_length=1, max_length=2000)


class ReviewOut(BaseModel):
    id: int
    movie_id: int
    author: str
    title: str | None
    content: str
    created_at: datetime
    sentiment_label: str | None      # 부정 · 중립 · 긍정
    sentiment_score: int | None      # -1 · 0 · +1
    confidence: float | None
    model_version: str | None
```

`content` 상한 2000자는 모델 입력 길이와 무관한 **저장 단계의 방어선**이다.
truncation은 추론 모듈이 따로 수행한다.

### C-c. DB 스키마

```sql
CREATE TABLE movie (
    id              INTEGER PRIMARY KEY,
    tmdb_id         INTEGER UNIQUE,
    title           TEXT    NOT NULL,
    release_date    DATE    NOT NULL,
    director        TEXT,
    genre           TEXT,
    poster_url      TEXT,
    external_rating REAL,
    UNIQUE (title, release_date)
);

CREATE TABLE review (
    id              INTEGER PRIMARY KEY,
    movie_id        INTEGER NOT NULL REFERENCES movie(id) ON DELETE CASCADE,
    author          TEXT    NOT NULL,
    title           TEXT,
    content         TEXT    NOT NULL,
    created_at      TIMESTAMP NOT NULL,
    sentiment_label TEXT,
    sentiment_score INTEGER,
    confidence      REAL,
    model_version   TEXT
);

CREATE INDEX idx_review_movie   ON review (movie_id, created_at DESC);
CREATE INDEX idx_review_created ON review (created_at DESC);
```

인덱스 2종은 각각 영화별 리뷰 페이지네이션과 최근 리뷰 화면의 정렬을 받친다.

### C-d. 추론 모듈

```
입력 텍스트
  → tokenizers 로 인코딩 (max_length 고정, truncation · padding)
  → onnxruntime 세션 run
  → softmax
  → argmax → 라벨 인덱스
  → 라벨 매핑 표로 부정/중립/긍정 결정      (B-c-1 실측값 사용)
  → 스칼라 변환 (-1 / 0 / +1)
  → confidence = max(softmax)
```

- 세션은 `lifespan`에서 1회 생성해 앱 상태에 보관한다. 요청마다 만들지 않는다.
- **Fixed Shape**이므로 `max_length`는 export 시점 값으로 고정한다. 코드에서
  임의 지정하지 말고 세션 입력 shape에서 읽어 쓴다.
- 라벨 매핑은 상수 표로 두고 `model_version`과 함께 관리한다.
  `id2label`이 `LABEL_0~2`라 코드에 인덱스를 직접 쓰면 나중에 근거를 잃는다.
- 모델 로드 실패 시 앱을 죽이지 않는다. `/health`가 `model_loaded: false`를 보고하고,
  리뷰 등록은 감성 `null`로 계속 동작한다. 영화 CRUD(필수 기능)까지 막을 이유가 없다.

### C-e. 설정

| 환경변수 | 기본값 | 용도 |
|---|---|---|
| `DATABASE_URL` | `sqlite:///./data/movies.db` | DB 경로 |
| `ML_ASSETS_DIR` | `./ml_assets` | ONNX · 토크나이저 위치 |
| `MODEL_VERSION` | `mission13-modelA-full-ft` | 리뷰에 기록 |
| `ALLOWED_ORIGINS` | `*` (로컬) / 배포 시 Streamlit 도메인 | CORS |

TMDB 키는 **런타임 설정에 넣지 않는다.** `scripts/`에서만 `.env`로 읽는다.

## D. 프론트엔드

### D-a. 화면 분기

`app.py`가 `st.query_params`의 `movie_id` 유무로 목록과 상세를 나눈다.
상세를 `pages/`에 두면 사이드바에 항상 노출되어 "영화를 고르지 않은 상세"라는
빈 화면이 메뉴에 남는다. 분기 방식은 Streamlit 버전 의존성도 없다.

```python
# app.py
movie_id = st.query_params.get("movie_id")
if movie_id is None:
    render_movie_list()
else:
    render_movie_detail(int(movie_id))
```

### D-b. API 클라이언트

```python
# lib/api_client.py
class ApiError(Exception):
    """화면이 사용자 메시지로 바꿔 쓰기 위한 예외."""
```

- base URL은 `st.secrets["BACKEND_BASE_URL"]` → 환경변수 순으로 읽는다. 하드코딩 금지.
- 타임아웃을 넉넉히 준다. Cloud Run이 유휴에서 깨어날 때 첫 요청이 느리다.
- `requests` 예외와 4xx/5xx를 모두 `ApiError`로 변환한다.
  화면 코드에 HTTP 상태 코드 분기가 흩어지지 않게 한다.

### D-c. 표현 계층

```python
# lib/formatting.py
def to_stars(rating: float | None) -> str:
    """감성 평점(-1~+1)을 0~5 별점으로 환산한다. 표시 전용."""
```

- 별점 환산은 **여기서만** 한다. 백엔드로 되돌려 보내지 않는다.
- 감성 배지와 `판정 애매` 표기 임계값은 프런트 설정에 둔다.
  임계값은 B-c-5 실측 후 정한다 — 그전까지는 표기 기능만 만들고 값을 확정하지 않는다.

### D-d. 캐시

목록·상세 조회에 짧은 TTL 캐시를 걸되, **등록·삭제 성공 직후 반드시 무효화**한다.
방금 추가한 영화가 목록에 없으면 사용자는 저장이 실패했다고 판단한다.

## E. 준비 스크립트

로컬에서 한 번만 실행하며 배포 이미지에는 실행 결과(`data/movies.db`)만 들어간다.

| 스크립트 | 입력 | 출력 |
|---|---|---|
| `collect_tmdb.py` | TMDB API 키 (`.env`) | 영화 메타데이터 JSON |
| `build_reviews.py` | NSMC 원본 | 영화별 리뷰 배정 결과 JSON |
| `seed_db.py` | 위 두 산출물 + ONNX 모델 | `data/movies.db` |

- `collect_tmdb.py`는 `/movie/{id}`와 `/movie/{id}/credits`를 함께 호출한다.
  감독은 `crew`에서 `job == "Director"`로 뽑는다. 요청 간격을 둔다.
- `build_reviews.py`는 NSMC 라벨로 긍/부정을 고르게 배분한다.
  제목 · 작성자 · `created_at`은 여기서 파생 생성한다.
- `seed_db.py`가 **적재 시점에 감성 분석을 수행해 결과까지 저장**한다.
  런타임에 시드 리뷰를 다시 추론하지 않는다.

## F. 컨테이너

```
python:3.12-slim
  → requirements 설치
  → app/ · ml_assets/ · data/movies.db 복사
  → uvicorn 실행 (PORT 환경변수 사용)
```

- `.onnx`와 `.onnx.data`를 **함께** 복사한다. 하나만 빠지면 로드에 실패한다.
- `.dockerignore`에 `scripts/`, `tests/`, `.env`, `__pycache__`를 넣는다.
  수집 스크립트와 키가 이미지 레이어에 남지 않게 한다.
- Cloud Run 배포는 메모리 1GiB, `--max-instances=1`.

## G. 실측으로 확정한 값

구현 전에 미정이던 항목이다. 근거는 [model-eval.md](model-eval.md)에 있다.

| 항목 | 확정값 | 근거 |
|---|---|---|
| 라벨 인덱스 → 감성 | `0=부정 · 1=중립 · 2=긍정` | 미션 13 `LABEL_MAP` + 골든 케이스 실측 (B-c-1) |
| 모델 입력 `max_length` | **256** (세션 입력 shape에서 읽는다) | 세션 입력 `[1, 256]` |
| 모델 입력 dtype | **int32** (Unity Sentis 호환 export) | 세션 입력 타입 |
| softmax 적용 | **필요함** — 출력은 raw logit | 출력 합 ≠ 1, 음수 포함 (B-b-3) |
| `판정 애매` 임계값 | **`confidence < 0.90`** | 0.90 미만 구간 엄격 정확도 < 0.5 (B-c-5) |
| 시드 영화 목록 | 기생충 · 괴물 · 헤어질 결심 · 부산행 · 신세계 · 올드보이 (6편) | NSMC가 한국 영화 리뷰라 도메인을 맞춘다 |
| 시드 리뷰 수 | 영화당 **12건**, 감성 구성은 **영화마다 다르게** 배정 | 균등 배분 시 6편의 평점이 모두 0.167로 같아진다 (service-eval.md A-a) |
| 저확신 표본 | 영화당 **2건** (정답 일치 · confidence 0.55~0.90) | 없으면 `판정 애매` 표기를 화면으로 증명할 수 없다 |
| NSMC 원본 위치 | `/mnt/wsl_data/datasets/nsmc/` (트리 외부 분리) | 상위 규약의 데이터셋 분리 관례 |
