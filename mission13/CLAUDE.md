# CLAUDE.md — 미션 13

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## 필수 참조 파일

작업 시작 전 반드시 [PROJECT_PLAN.md](PROJECT_PLAN.md)를 읽는다.  
체크리스트 진행 순서와 각 섹션의 완료 여부가 기록되어 있다.

## 문서 섹션 계층 구조

노트북 및 모든 작업 문서의 섹션은 아래 계층 표기를 따른다.

```
대문자.          →  최상위 섹션     예) A. 프로젝트 목적
대문자-소문자.   →  하위 섹션      예) A-a. 계획 확인
대문자-소문자-숫자. → 세부 항목    예) A-a-1. 수행 방향 정리
```

계층을 건너뛰지 않는다. 최상위(`대문자.`) 없이 하위(`대문자-소문자.`)를 단독으로 사용하지 않는다.

## 작업 진행 순서

각 섹션 체크리스트는 아래 순서로만 진행한다. 이전 단계 미완료 시 다음 단계로 넘어가지 않는다.

> **계획 확인** → **구현 진행** → **구현 검증** → **실험 결과 확인** → **실험 결과 분석** → **완료 확인**

## 노트북 셀 위치 표기

수정이 필요한 셀을 지칭할 때 전체 문서 기준 n번째 셀이라 표현하지 않는다.  
반드시 **섹션 계층 내 순서**로 표기한다.

> 올바른 예: `B-b의 3번째 셀`  
> 잘못된 예: `노트북의 15번째 셀`

## 코드 주석 규칙

- 주석은 되도록 **한글**로 작성한다.
- 주석 내에 구분선(`---`, `===`, `#---` 등)을 넣지 않는다.

## Project Overview

쇼핑몰·SNS 리뷰 데이터를 이용한 **한국어 감성 분석 on-device 파이프라인** 구현 과제.  
BERT 계열 경량 모델 2종을 선발하여 Full Fine-Tuning / Freeze / LoRA-PEFT 세 가지 방식으로 학습 후 비교하고, 최적 모델을 ONNX로 변환해 Unity Sentis를 통해 Android(Galaxy S25)에 이식하는 것이 최종 목표.

## Execution Environments

| 용도 | 환경 |
|------|------|
| ipynb 작성·검증 | WSL2 + RTX 5050 Laptop (로컬) |
| 실제 학습 | Google Colab L4 |
| 체크포인트 저장 | Google Drive (Colab 환경) |

**`is_colab` 전역 플래그**로 로컬/Colab 분기를 제어한다. 이 플래그는 **A-c의 1번째 셀**에 위치한다.  
Colab에서 실행 전 반드시 체크포인트 저장 경로가 Google Drive를 가리키고 있는지 확인한다.

**로컬 / Colab 실행 경계**

| 섹션 | 실행 환경 |
|------|-----------|
| A — G | WSL2 로컬 (ipynb 작성·검증) |
| H — P | Google Colab L4 (실제 학습 및 이후 단계) |

H 섹션 진입 전 노트북을 Colab에 업로드하고 `is_colab = True`로 전환한다.

## Running the Notebook

```bash
# 로컬 검증
jupyter notebook "13_4팀_신호정.ipynb"

# 의존성 설치 (로컬)
pip install transformers datasets peft evaluate scikit-learn onnx onnxruntime torch
```

Colab에서는 노트북을 그대로 업로드하고, 맨 위 셀에서 `is_colab = True`로 설정한다.

## Data Layout

```
DataTable/
├── SNS/          # 260 JSON files (100 records each) — 5개 카테고리
│   ├── 01. 패션 / 02. 화장품 / 03. 가전 / 04. IT기기 / 05. 생활
└── 쇼핑몰/       # 1766 JSON files (100 records each) — 4개 카테고리
    ├── 01. 패션 / 02. 화장품 / 03. 가전 / 04. IT기기
```

**공통 JSON 스키마**: `Index`, `RawText`, `Source`, `Domain`, `MainCategory`, `ProductName`, `ReviewScore`, `Syllable`, `Word`, `RDate`, `GeneralPolarity`, `Aspects`

- 레이블: `GeneralPolarity` (3분류 — `"-1"`=부정(0), `"0"`=중립(1), `"1"`=긍정(2); `None` 및 키 누락 레코드 제거)
- `Aspects`: 속성별 감성 리스트 — 필드: `Aspect`, `SentimentText`, `SentimentWord`, `SentimentPolarity`
- 카테고리 컬럼이 없는 파일은 파일 경로 기반으로 출처 컬럼을 부여한다.

## Pipeline Architecture (Notebook Sections)

| 섹션 | 내용 |
|------|------|
| B | on-device 대상 BERT 모델 2종 선발 (용량 → 정확도 → 속도 우선순위) |
| C | 데이터 형상 조사 (경로별 구성·수량·스키마 일치 여부) |
| D | EDA + 전처리 (NFKC 정규화, 중복 제거, 길이 필터링, 제어문자 제거) + 8:1.5:0.5 stratified split |
| E | HuggingFace Dataset 구현 (사전학습 vocab 그대로 사용) |
| F | 모델 로드 |
| G | `transformers.Trainer` 공통 학습기 설정; LoRA는 `get_peft_model`로 래핑 |
| H | 학습 수행 + 체크포인트 저장 |
| I | 손실·정확도 시각화 (macro-F1 기준) |
| J | 종합 평가 및 on-device 모델 선정 (용량 → 정확도 → 처리속도) |
| K | ONNX Export (Fixed Shape, `model.eval()`, dropout 비활성) |
| L | ONNX 검증 (`onnx.checker` + `onnxruntime`, `np.allclose atol=1e-4`) |
| M | Unity Sentis 통합 (ONNX Import, AI Worker, 토크나이저) |
| N | Unity 어플리케이션 구현 (서비스 로직, UI) |
| O | Android APK 빌드 → Galaxy S25 테스트 → 이슈 수정 |
| P | 결론 (모델 선정 근거, 성능 비교, on-device 검증 요약) |

## Key Technical Constraints

- **Split ratio**: train 8 / valid 1.5 / test 0.5 (stratified, `random_state` 고정)
- **공통 하이퍼파라미터**: seed, batch_size, max_seq_len, optimizer, warmup ratio
- **LoRA ONNX Export**: `merge_and_unload()`로 LoRA 가중치를 베이스 모델에 통합한 뒤 export
- **ONNX 포맷**: Fixed Shape (모바일 최적화), 동적 플로우 회피
- **Unity 버전**: Unity 6.3 LTS (6000.3.17f1) + Unity Sentis
- **Android 타겟**: OS 13, Galaxy S25

## CheckPoint Directory

`CheckPoint/` 폴더는 로컬에서 체크포인트를 저장하는 용도로 존재하며, Colab 환경에서는 Google Drive 경로로 대체된다.

## 체크리스트 업데이트 책임

각 섹션의 **완료 확인** 단계 항목을 모두 마친 직후, Claude가 PROJECT_PLAN.md의 해당 `[ ]`를 `[x]`로 업데이트한다.  
사용자의 별도 요청 없이 완료된 항목에 한해 자동으로 반영한다. 완료 여부가 불확실한 항목은 사용자에게 확인 후 업데이트한다.
