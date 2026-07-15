# CLAUDE.md — 미션 04

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Overview

**정기 예금 가입 증대를 위한 데이터 분석 및 예측 모델 제작.**
은행 텔레마케팅 캠페인 데이터를 분석해 가입 고객의 특성을 파악하고,
예측 모델과 **SHAP 해석**을 근거로 마케팅 전략을 제안하는 미션.

**결과물의 무게중심이 모델 성능이 아니라 "설명 가능성 → 전략 제안"에 있다.**

## Key Files

- `미션4_6팀_신호정.ipynb` — 전체 파이프라인
- `미션4_6팀_신호정.pdf` — 제출 보고서
- `bank-additional-full.csv` / `bank-additional-names.txt` — 원본 데이터 (동봉)
- `NanumGothic.ttf` — matplotlib 한글 폰트

## Data

UCI ML Repository — **Bank Marketing** (2008-05 ~ 2010-11, 포르투갈 소재 은행)
<https://archive.ics.uci.edu/dataset/222/bank+marketing>

> 출처 논문: Moro, S., Cortez, P., & Rita, P. (2014).
> *A Data-Driven Approach to Predict the Success of Bank Telemarketing.*
> Decision Support Systems. <http://dx.doi.org/10.1016/j.dss.2014.03.001>

여러 변형본 중 **위 논문에서 사용된 데이터(`bank-additional-full`)** 를 선택했다.

## Notebook Section Structure

| 섹션 | 내용 |
|---|---|
| 1 | 요구 사항 |
| 2 | 데이터 분석 (EDA) — 출처 / 컬럼 설명 / 기본 통계 / 주제별 분석 / 인사이트 총정리 |
| 3 | 데이터 전처리 — 전략 정리 → 구현 → 결과 확인 |
| 4 | 학습 데이터 세트 구축 |
| 5 | 평가 지표 |
| 6 | 모델 학습 및 평가 — 선정 → 선언 → 초기 학습 → **Optuna 튜닝** |
| 7 | 모델 분석 — Random Forest Feature Importance, **SHAP** |
| 8 | 결론: 마케팅 전략 보고 |

## 도출된 전략 (섹션 8)

1. 경제 지표에 따른 **타이밍 전략**
2. **마케팅 비수기 5월** 회피
3. **휴대전화 중심** 캠페인 전개
4. 가려진 **신용 불량 여부**가 가입 예측을 불투명하게 만듦

## 모델 · Dependencies

`XGBoost`, `LightGBM`, `RandomForest` + **`Optuna`**(HPO) + **`SHAP`**(해석). GPU 선택적.

## 작업 시 유의점

- **클래스 불균형 데이터**다 (정기예금 가입률이 낮음).
  정확도(accuracy)로 평가하면 안 되며, 섹션 5에 정의된 지표를 따를 것.
- SHAP 분석 결과가 섹션 8 전략의 직접적 근거다.
  모델이나 전처리를 바꾸면 **SHAP 재계산 → 전략 문구까지 함께 갱신**해야 한다.
- 데이터가 동봉되어 있어 노트북을 그대로 실행할 수 있다.
- 이 미션은 `A. / A-a.` 계층 표기를 쓰지 않는다 (미션 05부터 도입된 규약).
