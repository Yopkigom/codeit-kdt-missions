# 미션 18 구조도

보고서 제출 항목인 **서비스 구조도 · ERD · 주요 흐름**을 mermaid로 정리한 문서다.
설계 근거는 [CLAUDE.md](../../CLAUDE.md), 설계 내용은 [mission-concept.md](../draft/mission-concept.md)를 본다.

## A. 시스템 구성

### A-a. 전체 구성

수집은 로컬에서 1회만 수행하고, 그 결과인 시드 DB를 이미지에 굽는다.
따라서 **런타임에는 TMDB API 키가 필요 없고 외부 데이터 소스에 접근하지 않는다.**

```mermaid
flowchart TB
    subgraph PREP["준비 단계 · 로컬 1회 실행"]
        direction LR
        TMDB[("TMDB API")]
        NSMC[("NSMC 데이터셋")]
        COLLECT["수집 · 매핑 스크립트"]
        SEEDDB[("시드 SQLite")]
        TMDB --> COLLECT
        NSMC --> COLLECT
        COLLECT --> SEEDDB
    end

    subgraph RUN["서비스 단계 · 배포"]
        direction LR
        subgraph FE["Streamlit Community Cloud"]
            UI["Streamlit 앱<br/>화면만 담당"]
        end
        subgraph BE["Cloud Run · 1GiB · max-instances=1"]
            API["FastAPI"]
            MODEL["ONNX 감성 분석 모델<br/>lifespan 1회 로드"]
            DB[("SQLite")]
            API --> MODEL
            API --> DB
        end
        UI -->|HTTPS REST| API
    end

    SEEDDB -.->|이미지 빌드 시 포함| DB

    USER(["사용자"]) --> UI
```

**핵심 제약**: 모든 데이터는 백엔드가 소유한다. Streamlit은 화면만 담당하며
로컬 파일·세션 영속화 등 별도 저장 기능을 두지 않는다.

### A-b. 백엔드 내부 구성

```mermaid
flowchart TB
    REQ["HTTP 요청"] --> ROUTER

    subgraph APP["FastAPI 애플리케이션"]
        ROUTER["라우터<br/>movies · reviews"]
        SCHEMA["Pydantic 스키마<br/>검증 · 직렬화"]
        SERVICE["서비스 계층<br/>평점 집계 · 감성 연동"]
        REPO["리포지토리<br/>DB 접근"]
        INFER["추론 모듈<br/>tokenize → run → softmax"]

        ROUTER --> SCHEMA
        SCHEMA --> SERVICE
        SERVICE --> REPO
        SERVICE --> INFER
    end

    REPO --> SQLITE[("SQLite")]
    INFER --> ORT["onnxruntime 세션<br/>.onnx + .onnx.data"]

    LIFESPAN["lifespan 시작"] -.->|1회 로드| ORT
```

추론 모듈을 서비스 계층에서 분리하는 이유는 모델 교체·성능 측정 시
라우터와 DB 접근 코드를 건드리지 않기 위해서다.

## B. 데이터 모델 (ERD)

```mermaid
erDiagram
    MOVIE ||--o{ REVIEW : "리뷰를 가진다"

    MOVIE {
        integer id PK "자동 채번"
        integer tmdb_id "TMDB 원본 ID · unique"
        text title "제목"
        date release_date "개봉일"
        text director "credits에서 추출"
        text genre "장르"
        text poster_url "URL만 저장"
        real external_rating "TMDB vote_average 0~10"
        boolean is_seed "시드 데이터 여부 · 배포본 삭제 보호"
    }

    REVIEW {
        integer id PK "자동 채번"
        integer movie_id FK "ON DELETE CASCADE"
        text author "작성자"
        text title "제목"
        text content "내용"
        datetime created_at "정렬 기준"
        text sentiment_label "부정 · 중립 · 긍정"
        integer sentiment_score "-1 · 0 · +1"
        real confidence "max softmax · 표기용"
        text model_version "평균 오염 방지"
    }
```

### B-a. 저장되지 않는 값

`sentiment_rating`(영화 평점)은 **컬럼이 아니라 파생값**이다.
소속 리뷰 `sentiment_score`의 산술평균이며 범위는 `-1 ~ +1`이다.
캐시 컬럼으로 둘지 매 조회 시 집계할지는 미정이다(`PROJECT_PLAN.md` C-a-4).

`external_rating`과 혼동하지 않는다. 두 값은 출처도 범위도 다른 별개 필드다.

## C. 주요 흐름

### C-a. 리뷰 등록 — 감성 분석 포함

추론은 **등록 시 1회**만 수행하고 결과를 저장한다.
조회할 때마다 추론하면 목록 10건당 10회 추론이 발생한다.

```mermaid
sequenceDiagram
    autonumber
    actor U as 사용자
    participant ST as Streamlit
    participant API as FastAPI
    participant INF as 추론 모듈
    participant DB as SQLite

    U->>ST: 영화 선택 · 작성자 · 제목 · 내용 입력
    ST->>API: POST /reviews
    API->>API: Pydantic 검증

    alt 영화 ID가 없음
        API-->>ST: 404 Not Found
    else 정상
        API->>INF: 감성 분석 요청
        INF->>INF: 토큰화 · truncation / padding
        INF->>INF: onnxruntime 추론
        INF->>INF: softmax → label · score · confidence

        alt 추론 실패
            INF-->>API: 오류
            API->>DB: 리뷰 저장 · 감성 null
            Note over API,DB: 평균 평점 계산에서 제외
        else 추론 성공
            INF-->>API: label · score · confidence
            API->>DB: 리뷰 + 감성 결과 저장
        end

        DB-->>API: 저장된 리뷰
        API-->>ST: 201 Created
        ST->>U: 감성 결과 표시
        opt confidence 낮음
            ST->>U: 판정 애매 표기
        end
    end
```

### C-b. 영화 목록 조회 — 평점 환산

별점 환산은 **표현 계층에서만** 수행한다. DB와 API 응답은 `-1 ~ +1` 원값을 유지한다.

```mermaid
sequenceDiagram
    autonumber
    actor U as 사용자
    participant ST as Streamlit
    participant API as FastAPI
    participant DB as SQLite

    U->>ST: 홈 화면 진입
    ST->>API: GET /movies
    API->>DB: 영화 조회 + 리뷰 sentiment_score 평균
    DB-->>API: 영화 목록 · sentiment_rating (-1~+1)
    API-->>ST: 원값 그대로 응답

    loop 영화마다
        ST->>ST: (x + 1) / 2 * 5 로 별점 환산
        alt 리뷰 0건
            ST->>U: 포스터 · 제목 · 평점 미표기
        else 리뷰 있음
            ST->>U: 포스터 · 제목 · 별점
        end
    end
```

### C-c. 영화 상세와 리뷰 페이지네이션

리뷰 목록은 10개 단위이며 정렬 기준은 `created_at` 내림차순이다.
총 페이지 수를 계산해야 하므로 전체 건수를 함께 반환한다.

```mermaid
sequenceDiagram
    autonumber
    actor U as 사용자
    participant ST as Streamlit
    participant API as FastAPI
    participant DB as SQLite

    U->>ST: 영화 카드 선택
    ST->>API: GET /movies/{id}
    API->>DB: 영화 단건 조회
    DB-->>API: 영화
    API-->>ST: 영화 상세

    ST->>API: GET /movies/{id}/reviews?limit=10&offset=0
    API->>DB: 리뷰 조회 · created_at DESC
    DB-->>API: 리뷰 10건 + 전체 건수
    API-->>ST: items · total
    ST->>U: 리뷰 목록 · 3단 감성 표기 · 페이지 이동

    opt 다음 페이지
        U->>ST: 페이지 이동
        ST->>API: offset 증가하여 재요청
    end
```

### C-d. 영화 등록과 삭제

삭제는 리뷰까지 연쇄 제거되므로 되돌릴 수 없다. 화면에서 확인 단계를 둔다.

```mermaid
sequenceDiagram
    autonumber
    actor U as 사용자
    participant ST as Streamlit
    participant API as FastAPI
    participant DB as SQLite

    rect rgb(240, 245, 250)
        Note over U,DB: 등록
        U->>ST: 제목 · 개봉일 · 감독 · 장르 · 포스터 URL
        ST->>API: POST /movies
        API->>API: Pydantic 검증
        alt 검증 실패
            API-->>ST: 422 Unprocessable Entity
        else 정상
            API->>DB: 영화 저장
            DB-->>API: 생성된 영화
            API-->>ST: 201 Created
        end
    end

    rect rgb(250, 242, 242)
        Note over U,DB: 삭제
        U->>ST: 삭제 요청
        ST->>U: 확인 요청
        U->>ST: 확인
        ST->>API: DELETE /movies/{id}
        API->>DB: 영화 삭제
        DB->>DB: ON DELETE CASCADE 로 리뷰 제거
        DB-->>API: 완료
        API-->>ST: 204 No Content
    end
```

## D. 배포 흐름

아카이브 저장소는 private을 유지하고, 제출 코드만 담은 별도 public 저장소를 통해 배포한다.

```mermaid
flowchart LR
    subgraph SRC["제출 전용 public 저장소"]
        FEDIR["frontend/"]
        BEDIR["backend/"]
    end

    subgraph CI["GitHub Actions"]
        BUILD["이미지 빌드<br/>시드 DB · ONNX 포함"]
        PUSH["레지스트리 push"]
        BUILD --> PUSH
    end

    BEDIR --> BUILD
    PUSH --> CR["Cloud Run 배포"]
    FEDIR --> SC["Streamlit Community Cloud"]

    SC -.->|st.secrets 로 base URL 주입| CR

    ARCHIVE[("아카이브 저장소<br/>private 유지")] -.->|코드 사본만 이관| SRC
```

`mission14/gemini.key`가 아카이브 트리에 존재하므로 아카이브 저장소를 public으로
전환하지 않는다. TMDB 키는 준비 단계에서만 쓰고 이미지·저장소에 남기지 않는다.
