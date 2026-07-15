# PROJECT_PLAN.md — 미션12 구현 계획

## 확정된 설계 결정

| 항목 | 결정 |
|------|------|
| BPE vocab | 모델별 별도 학습 (`bert_vocab.json` / `gpt_vocab.json`) |
| GPT seq_len | 512 (BERT와 동일) |
| 임베딩 방식 | 평균풀링 (TextRank용) |
| TextRank k | 3 고정 (extractive 정답 개수) |
| Beam width | 4, max_new_tokens=150 |
| ROUGE | ROUGE-1/2/L, MeCab 형태소 단위 |

---

## 진행 상황

### ✅ STEP-01 — 섹션 B: BERT vs GPT 이론 고찰 마크다운
- 추출형/생성형 원리, 두 방식 비교 테이블 작성 완료

### ✅ STEP-02 — 섹션 C: EDA/전처리 설계 마크다운
- 전처리 파이프라인 5단계, EDA 시각화 항목 정의 완료

---

### ✅ STEP-03 — 섹션 C: EDA/전처리 코드 셀

구현할 내용:
- `load_data()`: 6개 JSON 파일 로드, 도메인 태그 부여
- `clean_text()`: NFKC 정규화, 제어문자 제거, 연속 공백 정리
- 중복 문서 제거 (원문 결합 기준)
- 길이 필터링 (문장 수 > 30 제거)
- EDA 시각화: 도메인별 문서 수, 문장 수 분포, 글자 수 분포, extractive 위치 분포
- 전처리 결과를 `processed_docs.pkl`로 저장

---

### ✅ STEP-04 — 섹션 D: 토큰화/어휘사전 마크다운

내용:
- BERT/GPT vocab 별도 학습 결정 근거
- 특수 토큰 인덱스 배치 방식
- 저장 파일 규칙: `bert_vocab.json`, `gpt_vocab.json`

---

### ✅ STEP-05 — 섹션 D: 토큰화/어휘사전 코드 셀

구현할 내용:
- `build_corpus()`: 전처리된 문서에서 코퍼스 텍스트 추출
- BERT BPE 학습 + 특수 토큰 `[CLS],[SEP],[MASK],[PAD],[UNK]` 추가 → `bert_vocab.json` 저장
- GPT BPE 학습 + 특수 토큰 `<bos>,<sep>,<eos>,<pad>` 추가 → `gpt_vocab.json` 저장
- vocab 크기, 토큰 길이 분포 확인 셀

---

### ✅ STEP-06 — 섹션 E: 학습 데이터 생성 마크다운

내용:
- test set 분할 방식 (valid의 10%, seed=42)
- BERT NSP 쌍 구성 방식
- GPT 사전학습 패킹 방식 (seq_len=512, 나머지 버림)
- GPT 파인튜닝 포맷 (`<bos>문서<sep>요약<eos>`)
- 저장 파일명 규칙

---

### ✅ STEP-07 — 섹션 E: 학습 데이터 생성 코드 셀

구현 내용:
- 셀 30 (setup): `os, pickle, random, torch, Tokenizer` 임포트, 토크나이저 재로드, 특수 토큰 ID 상수 정의
- 셀 31 (split): `split_valid_test()` — valid의 10%를 test, 90%를 valid로 분할 (seed=42)
- 셀 32 (BERT NSP): `make_bert_nsp_pairs()` — `encode_batch` 배치 토큰화, 50/50 IsNext/NotNext, [CLS]A[SEP]B[SEP] 포맷 → `bert_{split}.pt`
- 셀 33 (GPT pretrain): `pack_gpt_pretrain()` — `<eos>` 스트림 이어붙이기 후 512 청킹, 미완성 tail 버림 → `gpt_pretrain_{split}.pt` (shape [n_chunks, 512] tensor)
- 셀 34 (GPT finetune): `make_gpt_finetune()` — `<bos>doc<sep>summary<eos>`, `sep_pos`=첫 요약 토큰 인덱스 저장 → `gpt_finetune_{split}.pt`

---

### ✅ STEP-08 — 섹션 F: Dataset/DataLoader 마크다운

내용:
- BERT 동적 마스킹 위치 (`__getitem__` 호출마다)
- GPT loss mask 경계 처리 (`<sep>` 이전 `-100`)
- DataLoader 설정값 근거

---

### ✅ STEP-09 — 섹션 F: Dataset/DataLoader 코드 셀

구현할 내용:
- `BERTDataset`: `__getitem__`에서 MLM 동적 마스킹 (15%, 80/10/10)
- `GPTPretrainDataset`: 패킹된 토큰 시퀀스 로드
- `GPTFinetuneDataset`: `<sep>` 위치 탐지 → labels에서 이전 구간 `-100`
- DataLoader 공통 설정: `num_workers=4`, `pin_memory=True`, `persistent_workers=True`

---

### ✅ STEP-10 — 섹션 G: 모델 구현 마크다운

내용:
- BERT 하이퍼파라미터: 4 layers, hidden 256, 4 heads, intermediate 1024
- GPT 하이퍼파라미터: 동일 스케일
- 양자화 검토 방침 (INT8 동적 양자화)

---

### ✅ STEP-11 — 섹션 G: BERT 모델 코드 셀

구현할 내용:
- `BERTEmbedding`: 토큰 + 세그먼트 + 위치 임베딩
- `MultiHeadAttention`, `TransformerEncoderLayer`, `TransformerEncoder`
- `MLMHead`, `NSPHead`
- `BERTModel`: 전체 조립
- 파라미터 수 출력

---

### ✅ STEP-12 — 섹션 G: GPT 모델 코드 셀

구현할 내용:
- `GPTEmbedding`: 토큰 + 위치 임베딩
- Causal Mask 생성
- `MultiHeadAttention` (KV 캐시 지원), `TransformerDecoderLayer`, `TransformerDecoder`
- `LMHead`
- `GPTModel`: 전체 조립, label 1칸 시프트
- 파라미터 수 출력

---

### ✅ STEP-13 — 섹션 H: 환경설정 마크다운 + 코드 셀

마크다운:
- `is_colab` 플래그, `DATA_ROOT`/`CKPT_ROOT` 경로 분기 방식
- AMP + BF16, torch.compile, seed 42 근거

코드:
```python
is_colab = 'google.colab' in str(get_ipython())
DATA_ROOT = '/content/drive/MyDrive/mission12/DataTable' if is_colab else './DataTable'
CKPT_ROOT = '/content/drive/MyDrive/mission12/checkpoints' if is_colab else './checkpoints'
```
- Google Drive 마운트 확인 (is_colab 시 경로 출력)
- seed 고정, AMP scaler, torch.compile 적용

---

### ✅ STEP-14 — 섹션 I: 학습 파이프라인 마크다운

내용:
- 5 epoch, 2000 step 평가+저장, 5% warmup 근거
- 체크포인트 파일명 규칙: `{model}_step{step}_{metric:.4f}.pt`
- OOM 대응: batch 64 + Gradient Accumulation 2

---

### ✅ STEP-15 — 섹션 I: BERT 사전학습 코드 셀

구현할 내용:
- `train_bert()`: MLM + NSP 손실 합산 학습 루프
- 2000 step마다 NSP F1, MLM 정확도 평가 + best 체크포인트 저장
- 진행 로그 출력

---

### ✅ STEP-16 — 섹션 I: GPT 사전학습 + 파인튜닝 코드 셀

구현할 내용:
- `train_gpt_pretrain()`: next-token CE, held-out PPL + 50토큰 정성 샘플
- `train_gpt_finetune()`: 요약 구간만 손실 계산, 동일 평가 루프

---

### ✅ STEP-17 — 섹션 J: 요약 산출법 마크다운 + 코드 셀

마크다운:
- BERT TextRank 알고리즘 파라미터 (damping=0.85, max_iter=100)
- GPT 파인튜닝 프롬프트 포맷

코드:
- `bert_extract_summary()`: 문장 임베딩(평균풀링) → 코사인 유사도 행렬 → TextRank → 상위 3문장
- GPT 추론 함수는 섹션 K(STEP-18)에서 구현

---

### ✅ STEP-18 — 섹션 K: 추론 함수 마크다운 + 코드 셀

마크다운:
- KV 캐시 동작 원리 (O(T²) → O(T)), 레이어별 (K,V) 튜플 리스트 방식
- Beam Search 파라미터 테이블 (beam_width=4, max_new_tokens=150, 길이 정규화)

코드:
- `gpt_beam_search()`: `past_key_values` 방식 KV 캐시, beam_width=4, 길이 정규화 선택
- `bert_summarize(doc)`: `bert_extract_summary` 래퍼, 문장 공백 연결
- `gpt_summarize(doc)`: `<bos>doc<sep>` 프롬프트 빌드 → Beam Search → 디코딩
- 스모크 테스트 3건 출력

---

### ✅ STEP-19 — 섹션 L: 평가 마크다운 + 코드 셀

마크다운:
- ROUGE-1/2/L F1 정의, MeCab 형태소 단위 근거, Lead-3 베이스라인 정의

코드:
- `_split_valid_test()`: Section E 동일 로직으로 test set 복원
- `mecab_tokenize()`: MeCab 형태소 분리 (ImportError 시 공백 분리 폴백)
- `compute_rouge()`: `rouge-score` 라이브러리 래핑 (ROUGE-1/2/L F1)
- `lead3_summarize()`: 첫 3문장 추출 베이스라인
- 평가 루프: test 200건 × 3모델 (BERT/GPT/Lead-3)
- ROUGE 비교 테이블 (`pandas` DataFrame)
- 정성 샘플 5건 병렬 출력 (원문/정답/BERT/GPT)

---

## 파일 산출물 정리

| 파일 | 생성 단계 |
|------|---------|
| `processed_docs.pkl` | STEP-03 |
| `bert_vocab.json` | STEP-05 |
| `gpt_vocab.json` | STEP-05 |
| `bert_{train\|valid\|test}.pt` | STEP-07 |
| `gpt_pretrain_{train\|valid\|test}.pt` | STEP-07 |
| `gpt_finetune_{train\|valid\|test}.pt` | STEP-07 |
| `checkpoints/bert_step*_*.pt` | STEP-15 |
| `checkpoints/gpt_pretrain_step*_*.pt` | STEP-16 |
| `checkpoints/gpt_finetune_step*_*.pt` | STEP-16 |
