# CLAUDE.md — 미션 07

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Overview

**강아지와 고양이 얼굴 포착 모델 개발 — Object Detection.**
동물의 얼굴(face) 영역을 감지하는 검출 모델. **SSD를 주 대상**, **Faster R-CNN을 병행** 개발한다.

목표는 mAP 최대화가 아니라 **다양한 포즈에서의 얼굴 식별률**이다.
동물은 사람과 달리 촬영 포즈를 취해주지 않으므로, 비정형 포즈에 대한 강건성이 평가의 핵심.

과제: [스프린트 미션7](https://www.codeit.kr/topics/studyGroupStep6985c044aa647f3831b02822/lessons/11967) · 작성 2026-04-06

## ⚠ 데이터 경로

```
/mnt/wsl_data/datasets/oxford-iiit-pet/
├── The_Oxford-IIIT_Pet_Dataset.zip
└── the_oxford-iiit_pet/     # 압축 해제본
```

**Oxford-IIIT Pet Dataset** — 어노테이션이 **Pascal VOC XML** 형식이다 (`xml` 파서 사용).

## Key Files

- `07_2팀_신호정.ipynb` — 전체 파이프라인
- `NanumGothic.ttf` — matplotlib 한글 폰트

## Notebook Section Structure

| 섹션 | 내용 |
|---|---|
| A | 프로젝트의 목적 (수행 방향 / 수행 계획) |
| B | **강아지🐶와 고양이🐱의 얼굴 특성 비교** — 각 특성 / 차이점 |
| C | 데이터 리뷰 — 구성 분석 / 사례 확인 / 전처리 전략 수립 |
| D | 모델 구현 방침 — 연산 장치 여건 / SSD·Faster R-CNN 방침 / **장·단점 및 하이퍼파라미터 계획표** |
| E | 프로젝트 수행 환경 설정 (미션 05의 골격 계승) |
| F~ | 모델 구현 · 학습 · 평가 |

## 모델

| 모델 | 위치 | 특징 |
|---|---|---|
| **SSD** | 주 개발 대상 | 1-stage, 속도 우위 |
| **Faster R-CNN** | 병행 개발 | 2-stage, 정확도 우위 |

두 모델은 **동일 데이터 · 동일 평가 조건**에서 비교된다. 한쪽만 조건을 바꾸지 말 것.

## 핵심 설계 포인트

- **섹션 B(얼굴 특성 비교)가 anchor/aspect ratio 설계의 근거**다.
  개와 고양이는 주둥이 길이·귀 위치·얼굴 종횡비가 다르다.
  anchor box 설정을 수정할 때는 섹션 B의 관찰과 정합하는지 확인할 것.
- 섹션 D의 **하이퍼파라미터 계획표**가 실험의 기준선이다. 즉흥적으로 값을 바꾸지 말고 표를 갱신할 것.

## 평가

`torchmetrics`의 Detection 지표(mAP 계열) 사용. IoU 임계값 설정에 따라 결과가 크게 달라지므로,
두 모델 비교 시 **IoU 임계값을 반드시 동일하게** 유지할 것.

## Dependencies

`torch`, `torchvision`, `torchmetrics`, `optuna`, `sklearn`, `cv2`, `PIL`, `kagglehub`, `xml`. **GPU 필요.**

## 작업 시 유의점

- `A. / A-a.` 계층 표기 규약 적용 대상이다.
- 이 데이터셋은 품종(breed) 라벨도 포함하지만, 본 미션의 1차 목표는 **얼굴 영역 검출**이다.
  품종 판별은 부차 목표이므로 우선순위를 혼동하지 말 것.
