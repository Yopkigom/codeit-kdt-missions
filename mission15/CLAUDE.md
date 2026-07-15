# CLAUDE.md — 미션 15

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Overview

**학생 성적 예측 — Docker 기반 재현성 및 협업 환경 구성.**

> **이 미션의 평가 대상은 모델 성능이 아니라 MLOps 구조다.**
> 회귀 모델 자체는 의도적으로 단순하며(LinearRegression),
> **두 연구자가 사전 파일 공유 없이 Docker 이미지만으로 동일 결과를 재현**하는 것이 핵심이다.

미션 11~14가 모델링 중심이라면, 미션 15는 **배포·재현성 중심**이다.

## Key Files

```
mission15/
├── report-materials.md / .pdf        # 제출 보고서 (아키텍처 도식 포함)
├── data/
│   ├── mission15_train.csv           # 7,000행 × 6열
│   └── mission15_test.csv            # 3,000행
└── mission-result/
    ├── researcher1/                  # 학습 담당
    │   ├── modeling.ipynb            # 전처리 · EDA · 모델링
    │   ├── train.py                  # 학습 스크립트 (이미지 진입점)
    │   ├── Dockerfile                # python:3.11-slim-bookworm
    │   ├── requirements.txt
    │   └── output/model.pkl          # 산출 모델 (1,606 bytes)
    └── researcher2/                  # 추론 담당
        ├── inference.ipynb
        ├── Dockerfile.jupyter
        ├── docker-compose.yml        # trainer + notebook 서비스
        ├── requirements.txt          # ⚠ 이미지에서 docker cp로 추출한 것
        └── shared/                   # 공유 볼륨
            ├── model.pkl
            └── result.csv            # 최종 추론 결과 (3,000행)
```

## Docker Hub

```
https://hub.docker.com/r/yopkigom/student-perf-train
이미지: yopkigom/student-perf-train:1.0
digest: sha256:fc70881b0c5ea9a5d9c617d6f529d66412f1a37eb5ce9f38f5cf431a22975ef9
```

## 아키텍처

```
[연구자 1]  modeling.ipynb → train.py → Dockerfile → student-perf-train:1.0
                                                            │ docker push
                                                       Docker Hub
                                                            │ docker pull
[연구자 2]  trainer 컨테이너 ──docker cp──> requirements.txt + test.csv
                    │                              │
                    │                       Dockerfile.jupyter (동일 베이스·동일 requirements)
                    │                              │
            docker-compose ── 공유 볼륨 ./shared ──┤
                    │                              │
              model.pkl  ────────────────> inference.ipynb → result.csv
```

## 핵심 설계 포인트 — "버전 불일치의 구조적 차단"

> **연구자 2는 `requirements.txt`를 전달받지 않는다.**
> 대신 **이미지에서 `docker cp`로 직접 추출**해 그대로 빌드한다.
> 파일을 주고받는 과정이 없으므로 **버전 불일치가 발생할 경로 자체가 사라진다.**
>
> 두 이미지 모두 `python:3.11-slim-bookworm` 베이스로 통일되어 있다.
> 검증: 추출본과 원본 `diff` 동일, 노트북/컨테이너 RMSE 완전 일치.

**이 구조를 훼손하지 말 것.** 편의를 위해 `requirements.txt`를 직접 복사해두면
미션의 핵심 논지가 무너진다.

## 모델링 결과 (참고)

| 항목 | 값 |
|---|---|
| 데이터 | 7,000행 → 중복 제거 6,936행 |
| 지배적 변수 | `Previous Scores` (목표변수와 상관 **0.914**) |
| 최종 모델 | `StandardScaler + LinearRegression` 파이프라인 |
| RMSE / R² | **2.0375** / 0.989 (목표변수 범위 10~100) |

선형 구조가 뚜렷해 선형 회귀 계열을 채택했다. Ridge와 차이가 사실상 없다(2.0376).
**모델을 고도화할 이유가 없는 데이터**이며, 그것이 이 미션의 설계 의도이기도 하다.

## Dependencies

`scikit-learn`, `pandas` + **Docker / docker-compose**. GPU 불필요.

## 작업 시 유의점

- 재현 검증 기준은 **"노트북 RMSE == 컨테이너 RMSE"** 다.
  코드를 수정했다면 이 등식이 유지되는지 반드시 재확인할 것.
- `mission-result/researcher2/.ipynb_checkpoints/`는 잔재다 (정리 대상).
