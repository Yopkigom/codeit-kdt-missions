# CLAUDE.md — 미션 11

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Project Overview

한국어 구어체 → 영어 구어체 기계 번역 실험 노트북.  
Seq2Seq(bi-LSTM + Luong Attention)과 Transformer를 동일 데이터·평가 환경에서 비교하여  
Attention 메커니즘의 성능 개선을 정량적으로 측정하는 것이 목적.

**운영 환경**: Google Colab (L4 GPU), BF16 + AMP, `torch.compile`  
**로컬 개발 환경**: WSL2, `ai` conda 환경 (mamba), Python 3.12

## Key Files

- `11_4팀_신호정.ipynb` — 전체 파이프라인 단일 노트북 (데이터 분석 → 학습 → 평가)
- `NanumGothic.ttf` — 시각화(matplotlib) 한국어 폰트 (로컬 전용)

## Notebook Section Structure

| 섹션 | 내용 |
|------|------|
| A | 프로젝트의 목적 (수행방향 / 수행계획 / 변인 정의) |
| B | 기계 번역의 발전사 (RBMT / SMT / NMT / LLM) |
| C | 데이터 분석 (a.환경설정 / b.파일패스 / c.스키마 / d.test분할 / e.도메인 / f.길이분포 / g.어휘 / h.결측·중복) |
| D | 데이터 전처리 및 토큰화 |
| E | 훈련용 데이터 생성 |
| F | 공용 데이터 셋과 데이터 로더 제작 |
| G | Seq2seq 모델 파이프라인 제작 |
| H | Transformer 모델 파이프라인 제작 |
| I | 파이프라인 환경 설정 및 모델 별 하이퍼파라미터 설정 |
| J | Seq2seq 모델 학습 및 지표 분석 |
| K | Transformer 모델 학습 및 지표 분석 |
| L | Greedy / Beam search 구현 및 비교 |
| M | 평가 지표 산출 및 번역 품질 측정 |
| N | 총평 |

## Data

노트북은 아래 두 파일을 전제로 작성되어 있다 (저장소 미포함):

```
일상생활및구어체_한영_train_set.json   # 1,200,000 쌍
일상생활및구어체_한영_valid_set.json   # 150,000 쌍 → 절반씩 valid/test 분할
```

**Colab Drive 경로**: `/content/drive/MyDrive/KorEngTranslateData/`  
**로컬 경로**: `./` (프로젝트 루트)

### 데이터 분할 (seed=42 고정)

| 분할 | 샘플 수 |
|------|--------|
| train | 1,200,000 |
| valid | 75,000 |
| test | 75,000 |

### EDA 주요 발견

- 결측값: ko / en 모두 0건
- **(ko, en) 중복 쌍: 117,954건 (9.83%)** — 전처리 핵심 제거 대상
- word_ratio > 3.0 또는 < 1/3 필터 예상 제거: 약 1.36%
- 도메인 3종만 존재: 해외영업 · 일상생활 · 해외고객과의채팅 (해외영업 편중)
- 문체 100% 구어체 → 구어체 번역에 최적화된 데이터셋
- 한국어 고유 어절 수(공백 분리) >> 영어 단어 수 → SentencePiece 필요성 수치로 확인

## Environment Setup (Cell C.a)

### Colab 환경 감지 패턴

```python
# sys.modules 방식은 import 전에 False를 반환하므로 사용 금지
try:
    import google.colab
    is_colab = True
except ImportError:
    is_colab = False
```

### 한글 폰트

- **Colab**: `apt-get install fonts-nanum` → `/usr/share/fonts/truetype/nanum/NanumGothic.ttf`
- **로컬**: `./NanumGothic.ttf`

### 전역 상수

```python
SEED = 42
```
`random`, `numpy`, `torch`, `torch.cuda` 모두 동일 seed 고정.

## Tokenization

- **한국어**: MeCab-ko 형태소 분리 → SentencePiece (Unigram LM) 하이브리드
- SentencePiece 설정: `vocab_size=32000`, `model_type='unigram'`, `character_coverage=1.0`
- 특수 토큰 고정: `PAD=0, BOS=1, EOS=2, UNK=3`
- 학습된 모델은 `ko_en_translator.model / .vocab` 로 저장하여 재사용

## Sequence Format

| 역할 | 내용 |
|------|------|
| Encoder Input | `BOS [src tokens] EOS` |
| Decoder Input (teacher forcing) | `BOS [tgt tokens]` |
| Decoder Target (loss) | `[tgt tokens] EOS` (1-step shift) |

## Model Architecture

### Seq2Seq (`arxiv:1508.04025`)
- Encoder: bi-LSTM 2-layer, hidden=512, dropout=0.3, concat projection
- Decoder: uni-LSTM, Luong general(bilinear) Attention, input feeding

### Transformer (`arxiv:1706.03762`)
- `d_model=512`, 6 enc/dec layers, 8 heads, FFN=2048, dropout=0.1

## Hyperparameters

| | Seq2Seq | Transformer |
|---|---|---|
| Optimizer | Adam β=(0.9,0.999) lr=1e-3 | Adam β=(0.9,0.98) ε=1e-9 |
| LR Schedule | ReduceLROnPlateau (factor=0.5, patience=2) | Noam (warmup=4000) |
| Grad Clip | norm=5.0 | norm=1.0 |
| Label Smooth | — | ε=0.1 |
| Random Seed | 42 | 42 |

## Training Pipeline Conventions

- 본 학습 전 **sanity check**: 1000 샘플로 train loss overfit 확인
- 로깅: TensorBoard (`train/val loss`, `perplexity`, `lr`, `token/sec`)
- 체크포인트: val loss 기준 best 저장 + last 2개 유지, Early stopping 적용

## Inference

- Greedy & Beam search (beam=4, length_penalty α=0.6) 모두 구현
- **메인 평가 기준**: beam=4, α=0.6 (두 모델 동일 조건)
- 보조 분석: Transformer에 대해 greedy vs beam 비교

## Evaluation Metrics

| 지표 | 비고 |
|------|------|
| sacreBLEU | signature 기록, `--paired-bs`로 95% CI 산출 |
| ChrF++ | `arxiv:W17-4770` |
| COMET | 모델 버전 기록 |

## Preprocessing Rules

Moses/WMT 가이드 기준:
- NFKC 정규화, 중복 제거, 제어문자 제거
- 길이 필터: 1~250 토큰
- 한-영 길이 비율 1:3 초과 쌍 제외 (`word_ratio > 3.0` 또는 `< 1/3`)
- 언어 식별 필터링

## 필수 참조 문서
- PROJECT_PLAN.md: 세션 시작 시 반드시 읽을 것