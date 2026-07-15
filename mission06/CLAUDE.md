# CLAUDE.md — 미션 06

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Overview

**흉부 X-ray 분석 폐렴 진단 모델 개발.**
전이학습 기반 이진 분류(정상 / 폐렴).

> **⚠ 최우선 목표는 정확도가 아니라 위음성(False Negative) 최소화다.**
> 의료 도메인이므로 "폐렴 환자를 정상으로 판정"하는 오류가 인명과 직결된다.
> 모델을 수정·재평가할 때 **accuracy가 올라도 FN(=Recall 저하)이 늘면 개악**이다.

과제: [스프린트 미션6](https://www.codeit.kr/topics/studyGroupStep6985c044aa647f3831b02820/lessons/11894) · 작성 2026-04-01

## ⚠ 데이터 경로

```
/mnt/wsl_data/datasets/chest-xray-pneumonia/
├── Chest-X-Ray-Images-Pneumonia.zip
└── chest-x-ray-images-pneumonia/     # 압축 해제본
```

노트북은 `kagglehub` 또는 `./` 상대경로를 전제한다. 실행 시 경로 상수를 수정할 것.

## Key Files

- `06_2팀_신호정.ipynb` — **제출 최종본** (123셀)
- `NanumGothic.ttf` — matplotlib 한글 폰트

> 구버전 `미션6_2팀_신호정.ipynb`(118셀)는 `_trash/`로 격리되어 있다. 최종본은 `06_` 쪽이다.

## Notebook Section Structure

| 섹션 | 내용 |
|---|---|
| A | 프로젝트의 목적 (수행 방향 / 수행 계획) |
| B | **폐렴의 임상적 양상과 진단의 중요성** |
| C | **흉부 X-ray를 통한 폐렴 진단 요소** — 침윤/경화, 공기 기관지 조영, 실루엣 징후, 늑막 삼출 |
| D | 데이터 리뷰 — 구성 / 사례 확인 / 전처리 전략 수립 |
| E | 사전 학습 모델 선별 — 연산 장치 여건 분석 / 구현 방침 / 모델 선별 |
| F~ | 환경 설정 · 학습 · 평가 |

## 핵심 설계 포인트

- **섹션 B·C가 단순 배경 설명이 아니다.**
  방사선학적 소견(침윤·경화·실루엣 징후 등)이 **전처리 전략과 augmentation 설계의 근거**로 쓰인다.
  예: 대비(contrast) 관련 증강을 함부로 넣으면 침윤 소견이 뭉개진다.
  전처리를 수정할 때는 섹션 C의 진단 요소를 훼손하지 않는지 먼저 확인할 것.
- 모델 선별(섹션 E)이 **연산 장치 여건 분석에서 출발**한다 (미션 05에서 확립된 패턴).

## 평가 지표

FN 최소화가 목표이므로 **Recall(민감도)을 우선**하되 전체 진단 정확도의 균형을 유지한다.
Confusion Matrix를 항상 함께 확인할 것. 단일 accuracy 수치만 보고 판단하지 말 것.

## Dependencies

`torch`, `torchvision`, `optuna`, `sklearn`, `cv2`, `PIL`, `kagglehub`. **GPU 필요.**

## 작업 시 유의점

- `A. / A-a.` 계층 표기 규약 적용 대상이다.
- 클래스 불균형(폐렴 > 정상)이 존재한다. 샘플링·가중치 전략 변경 시 FN에 미치는 영향을 반드시 재측정.
