# CLAUDE.md — 미션 12

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Project Overview

한국어 문서 요약을 위한 BERT(추출형)과 GPT(생성형) 모델을 **처음부터 구현**하는 Jupyter Notebook 과제.
Codeit 스프린트 미션12. 런타임: Google Colab L4 GPU.

## Development Environment

```bash
# Notebook 실행 (로컬 검증용)
jupyter notebook "12_4팀_신호정.ipynb"

# 주요 의존성 (Colab 기준)
# torch, tokenizers (HuggingFace BPE), mecab-python3, rouge-score, NanumGothic.ttf
```

**이중 환경 운영:**
- **로컬 검증:** WSL2 + RTX 5050 Laptop — 셀 단위 작동 확인
- **실 학습:** Google Colab L4 GPU — 전체 학습 실행

전역 `is_colab` 플래그로 환경별 분기 처리. 모든 셀에서 이 플래그를 참조해 경로/옵션을 결정해야 함.

## Data

`DataTable/` 아래 6개 JSON 파일 (train/valid × news/law/editorial), 합계 약 1.6GB:

| 파일 | 문서 수 |
|------|--------|
| `train_original_news.json` | 243,983 |
| `train_original_law.json` | 24,329 |
| `train_original_editorial.json` | 56,760 |

**문서 스키마:**
```
documents[i] = {
  "text": [{"index": int, "sentence": str, "highlight_indices": str}, ...],
  "extractive": [int, int, int],   # 추출 요약 문장 인덱스 (3개)
  "abstractive": [str]             # 생성 요약 정답
}
```
`valid` 셋의 10%를 test set으로 분할. 나머지 90%가 validation.

## Architecture

### 구현 흐름 (노트북 섹션 순서)

```
B (이론) → C (EDA/전처리) → D (토큰화/어휘사전) → E (학습데이터 생성)
→ F (Dataset/DataLoader) → G (모델 구현) → H (환경설정)
→ I (학습 파이프라인) → J (요약 산출법) → K (추론 함수) → L (평가)
```

### BERT (추출형 요약)
- **특수 토큰:** `[CLS]`, `[SEP]`, `[MASK]`, `[PAD]`, `[UNK]`
- **아키텍처:** 토큰 + 세그먼트 + 위치 임베딩 → Transformer 인코더(4 layers, hidden 256) → MLM 헤드 + NSP 헤드
- **사전학습:** MLM (동적 마스킹 15%, 80/10/10 구성) + NSP (50% IsNext / 50% NotNext)
- **요약:** 사전학습된 인코더로 문장 임베딩(평균풀링 또는 CLS) → TextRank 중심성 상위 k문장 추출 (비지도)
- **평가지표:** NSP F1, MLM CE(정확도)
- **양자화 필요성 검토** 필요

### GPT (생성형 요약)
- **특수 토큰:** `<bos>`, `<sep>`, `<eos>`, `<pad>`
- **아키텍처:** 토큰 + 위치 임베딩 → Causal Mask → Transformer 디코더 → LM 헤드 (label 1칸 시프트)
- **사전학습:** 문서를 `<eos>`로 이어붙여 seq_len 단위로 패킹, next-token CE
- **파인튜닝 포맷:** `<bos>문서<sep>요약<eos>` — 손실은 요약 구간에서만 계산, 문서가 길면 원문 뒷부분 truncate(요약은 온전히 보존)
- **추론:** KV 캐시 + Beam Search
- **평가지표:** Perplexity (PPL), 50토큰 이어쓰기 정성 샘플

### 공통 하이퍼파라미터
- **batch_size:** 128 (OOM 시 64 + Gradient Accumulation 2로 전환)
- **epochs:** 5
- **평가/저장 주기:** 2000 step마다
- **Warmup:** 전체 스텝의 5%
- **토크나이저:** BPE (`tokenizers` 라이브러리로 코퍼스에서 직접 학습, vocab 파일로 저장)
- **최종 평가:** ROUGE-N (MeCab 형태소 단위), Lead-3 베이스라인 비교, 정성 샘플 출력 (원문/정답/BERT/GPT 병렬)
- **학습 환경:** AMP + BF16, `torch.compile`, Random Seed 42 고정
- **DataLoader:** `num_workers`, `pin_memory`, `persistent_workers` 활성화

## Key Implementation Constraints

- BERT 토큰 한도(512)를 고려해 전처리 시 길이 필터링 필요
- 어휘 사전과 전처리된 데이터셋은 파일로 저장해 세션 재시작 후에도 재사용
- 시각화에 `NanumGothic.ttf` 폰트 로드 필요 (한국어 출력)
- GPT 파인튜닝 시 요약 구간 마스킹: loss mask로 `<sep>` 이전 토큰은 gradient 차단

## Colab 운영 주의사항

- 체크포인트 저장 경로가 **Google Drive**를 바라보는지 학습 시작 전 명시적으로 확인
- `.pt` 파일은 best only 저장, 이전 파일 즉시 삭제 (Drive 용량 관리)
- 학습 파이프라인 시작 전 Drive 마운트 및 경로 출력으로 검증


## 작업 지침

- PROJECT_PLAN.md 파일을 참조할 것
- 각 문서의 단원은 영문 대문자, 소문자, 숫자 순으로 시작할 것
- 각 문서의 단원 시작 부분과 python 셀의 앞부분에 작업 내용에 대한 설명을 추가할 것
- 되도록 '~습니다', '~하였습니다' 식의 존대 서술을 할 것
- 각 스텝 별로 작업을 진행하고, 매번 작업 전 작업 내용에 대한 확인을 거칠 것