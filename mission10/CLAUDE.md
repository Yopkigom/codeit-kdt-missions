# CLAUDE.md — 미션 10

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Overview

**뉴스 카테고리 예측 모델 개발 — NLP 텍스트 분류.**
20 Newsgroups 데이터를 분류하되, **진짜 주제는 "임베딩 방식이 성능에 미치는 영향"의 통제된 비교**다.

**NLP 계열(미션 10~14)의 출발점**이며, 여기서 임베딩 → 미션 11 Transformer →
미션 12 BERT/GPT → 미션 13 PEFT/On-Device로 이어진다.

과제: [스프린트 미션10](https://www.codeit.kr/topics/studyGroupStep6985c080aa647f3831b0de75/lessons/13927) · 작성 2026-05-15

## ⚠ 데이터 경로

**GloVe 임베딩**은 용량(2.9GB) 문제로 트리 밖에 분리되어 있다.

```
/mnt/wsl_data/datasets/glove/
├── glove.6B.50d.txt
├── glove.6B.100d.txt      # 노트북 기본 사용
├── glove.6B.200d.txt
├── glove.6B.300d.txt
└── glove.6B.zip
```

노트북은 `./glove.6B.100d.txt` 상대경로를 전제한다. **실행 전 경로 수정 필수.**

> 20 Newsgroups 본문은 `sklearn.datasets`로 런타임에 다운로드되므로 별도 파일이 없다.

## Key Files

- `10_2팀_신호정.ipynb` — 전체 파이프라인
- `NanumGothic.ttf` — matplotlib 한글 폰트

산출물: `embedding_matrices.pt`, `all_histories.json`, `experiment_env.json`

## Notebook Section Structure

| 섹션 | 내용 |
|---|---|
| A | 프로젝트의 목적 (수행방향 / 수행계획 / **변인 정의**) |
| B | **임베딩 별 모델 성능 측정 구조 설계** — 구조적 문제 예측 / 해결 설계 |
| C | 뉴스 카테고리의 의미와 분류 — 발전 과정 / 한국·미국 미디어 표준 대분류 |
| D | 20 Newsgroups 데이터 분석 및 전처리 (a~g) |
| E~ | 모델 구현 · 학습 · 평가 |

### 섹션 D 상세

| | 내용 |
|---|---|
| a | 원본 데이터 로드 및 내용 확인 |
| b | 카테고리 확인 |
| c | 카테고리별 문서 수 |
| d | 카테고리별 문서 길이 분포 |
| e | 기준 이하 단어 수록 문서 제거 |
| f | 데이터 클렌징 |
| g | **카테고리별 난이도 예측 비교** |

## 핵심 설계 포인트

> **이 미션의 핵심은 섹션 A-c(변인 정의)와 섹션 B다.**
> 임베딩 방식만을 독립변인으로 분리하기 위해, 나머지 조건(모델 구조·학습률·시드·전처리)을
> 전부 통제하는 실험 구조를 **먼저 설계한 뒤** 학습에 들어간다.
>
> 따라서 이 노트북을 수정할 때 **임베딩 외의 변인을 건드리면 실험 전체가 무효**가 된다.
> 무언가를 바꾸려면 섹션 A-c의 변인 정의를 먼저 갱신할 것.

`embedding_matrices.pt`와 `experiment_env.json`은 이 통제 조건을 고정·재현하기 위한 산출물이다.

## Dependencies

`torch`, `gensim`(임베딩), `nltk`(전처리), `sklearn`(20 Newsgroups 로드 · 평가). **GPU 권장.**

## 작업 시 유의점

- `A. / A-a.` 계층 표기 규약 적용 대상이다.
- `nltk`는 최초 실행 시 토크나이저 리소스 다운로드가 필요하다 (`punkt`, `stopwords` 등).
- GloVe 파일은 수백 MB~1GB의 **텍스트** 파일이다. 로드가 느리므로
  파싱 결과를 `embedding_matrices.pt`로 캐싱하는 구조가 이미 들어가 있다. 이를 우회하지 말 것.
