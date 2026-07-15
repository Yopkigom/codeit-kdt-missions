## 세션 계획

| 세션 | 섹션 | 작업 내용 | 상태 |
|------|------|-----------|------|
| 1 | C.a ~ C.h | 환경 설정 + 데이터 스키마 확인 + EDA (도메인/길이/어휘/결측·중복) | ✅ 완료 |
| 2 | D | 데이터 전처리 + MeCab-ko + SentencePiece 토큰화 | ✅ 완료 |
| 3 | E + F | 훈련용 데이터 생성 + 공용 데이터셋 / 데이터 로더 제작 | ✅ 완료 |
| 4 | G | Seq2seq 모델 파이프라인 제작 → sanity check | ✅ 완료 |
| 5 | H | Transformer 모델 파이프라인 제작 → sanity check | ✅ 완료 |
| 6 | I + J | 파이프라인 환경·하이퍼파라미터 설정 + Seq2seq 학습 및 지표 분석 | ✅ 완료 |
| 7 | K | Transformer 학습 및 지표 분석 | ✅ 완료 |
| 8 | L + M | Greedy / Beam search 구현 및 비교 + 평가 지표 산출 및 번역 품질 측정 | ✅ 완료 |
| 9 | N | 총평 (보고서 정리) | 🔜 |

## 세션별 상세 메모

### 세션 1 ✅ 완료
- `is_colab` 감지: `try: import google.colab` 패턴 확정 (sys.modules 방식 버그 수정)
- `plt.tight_layout(rect=[0, 0, 1, 0.96])` + `plt.suptitle()` 조합으로 suptitle 잘림 방지
- EDA 핵심 수치: 중복 쌍 9.83%, 비율 필터 제거 1.36%, 100% 구어체, 도메인 3종

### 세션 2 ✅ 완료 (D. 데이터 전처리 및 토큰화)
- 전처리 6단계: NFKC 정규화 → 제어문자 제거 → 언어 식별(Hangul/ASCII 체크) → 중복 제거 → 길이 필터(1~250) → 비율 필터(1/3~3.0)
- `_KO_RE` 코드포인트 주석 추가 (AC00-D7A3 / 1100-11FF / 3130-318F)
- MeCab-ko (`python-mecab-ko`): Colab/로컬 공통, 실패 시 공백 분리 폴백
- SentencePiece (Unigram LM, vocab_size=32000) 학습 및 `ko_en_translator.model/.vocab` 저장
- Colab Drive 경로 / 로컬 경로 자동 분기, 모델 캐시 체크(재학습 방지)

### 세션 3 ✅ 완료 (E + F. 훈련용 데이터 생성 + 데이터 로더)
- `encode_source`: BOS [src] EOS, `MAX_SRC_LEN=128` truncation
- `encode_target`: (dec_in=BOS+tgt, dec_tgt=tgt+EOS) 1-step shift, `MAX_TGT_LEN=128`
- Mask 설계: Source padding mask (B,src_len), Causal mask (tgt_len,tgt_len), Target padding mask (B,tgt_len)
- `TranslationDataset(Dataset)`: (src, dec_in, dec_tgt) 삼중 쌍 텐서 반환
- `collate_fn`: 동적 패딩 + `src_key_padding_mask` / `tgt_key_padding_mask` 생성 (PAD=True)
- `DataLoader`: `BATCH_SIZE=128`, `num_workers=min(4,cpu)`, `pin_memory` (CUDA), `persistent_workers`, `drop_last=True` (train)

### 세션 4 ✅ 완료 (G. Seq2seq 모델)
- `Encoder`: bi-LSTM 2-layer, hidden=512, dropout=0.3, fc_out/fc_h/fc_c concat projection
- `LuongAttention`: general score `h_t^T * W_a * h_s`, PAD 마스킹 후 softmax
- `Decoder`: uni-LSTM 2-layer, input feeding `concat(embed, prev_context)`, attentional hidden `tanh(W_c @ concat(h_t, ctx))`
- `Seq2SeqModel`: Encoder + Attention + Decoder 래퍼, 파라미터 수 측정 포함
- sanity check: 1,000 샘플 100 epoch (batch=32), loss 7.50 → 1.41 (81.2% 감소), 통과 기준 < 2.5

### 세션 5 ✅ 완료 (H. Transformer 모델)
- `PositionalEncoding`: sin/cos fixed, max_len=512, embedding scale=√d_model
- `TransformerModel`: d_model=512, nhead=8, 6 enc/dec layers, FFN=2048, dropout=0.1
- **`norm_first=True` (Pre-LN) 채택** — Post-LN은 warmup 없이 loss 5.9 plateau 고착
  - 원인: Post-LN은 초기 gradient가 flat region에 수렴, Adam이 빠져나오지 못함
  - Pre-LN은 단일 배치 1,000 step → loss 0.0001 달성
- 파라미터 수: Transformer ~77 M (H.b 실행 시 정확한 수치 출력)
- sanity check: 1,000 샘플 100 epoch (dropout=0.0, lr=5e-3), loss 7.28 → 2.04 (72.0% 감소), 통과 기준 < 2.5
- 소요: 11,566 초 (RTX 5050 Laptop / WSL2)

### 세션 6 ✅ 완료 (I + J. 하이퍼파라미터 + Seq2seq 학습)
- I.a: SEED=42 고정, DEVICE/AMP_DTYPE 자동 감지, torch.compile 옵션, CKPT_DIR(Drive)/LOG_DIR 생성
- I.b: 하이퍼파라미터 상수 정의 (S2S_LR=1e-3, S2S_CLIP=5.0, TF_WARMUP=4000, MAX_EPOCHS=30, EARLY_STOP_PAT=5)
- J.a: EarlyStopping / save_checkpoint / run_epoch 유틸리티 (AMP + GradScaler 포함, Seq2seq·Transformer 공용)
- J.b: Seq2seq 학습 루프 (Adam + ReduceLROnPlateau, TensorBoard 4종 로깅, best+last 2 체크포인트)
- **J 학습 결과**: Best val loss 1.1137 (epoch 25), perplexity 3.05 / epoch 27~ NaN (logit 폭주, BF16 + no label smooth)
- best checkpoint: `s2s_best_ep025_loss1.1137.pt` → **Drive 용량 이슈로 유실**
  → 보존된 최신 체크포인트: `s2s_best_ep021_loss1.1256.pt`
- **J.b-recover-best 복구 결과**: ep021→ep024 재학습으로 `s2s_best_ep024_loss1.1240.pt` 복구
  - ep024 val_loss 1.1240 (ep021의 1.1256 대비 개선 ★), ep025에서 NaN 재발 → 안전 차단
  - 원래 ep025 best(1.1137) 미복구 / ep024가 사용 가능한 최선 체크포인트 → L/M 추론 기준 변경

### 세션 7 ✅ 완료 (K. Transformer 학습)
- Adam β=(0.9,0.98) ε=1e-9, Noam warmup=4000, label smooth ε=0.1, grad clip norm=1.0
- Noam 스케줄러: LambdaLR (lr=1.0), `sched.step()` per batch (run_epoch `sched=` 파라미터)
- `nn.CrossEntropyLoss(label_smoothing=0.1, ignore_index=PAD_ID)`
- **K 학습 결과**: Best val loss 2.2168 (epoch 23), perplexity 9.18 / early stopping epoch 28
- tok/s ≈ 13,500 (Seq2seq 9,100 대비 +48%, teacher forcing 병렬화)
- best checkpoint: `tf_best_ep023_loss2.2168.pt` (Drive 저장)
- **val loss 직접 비교 불가**: label_smoothing=0.1 페널티 floor ≈ 1.04 포함
  → 페널티 제거 후 NLL 추정 ≈ 1.31 (Seq2seq 1.11 대비 +0.20)
  → 번역 품질 비교는 M 섹션 BLEU/ChrF++로 판단

### 세션 8 ✅ 완료 (L + M. 추론 + 평가)
- L.a: 체크포인트 로딩 (`s2s_best_ep024_loss1.1240.pt`, `tf_best_ep023_loss2.2168.pt`)
- L.b: `greedy_decode` (argmax, 두 모델 공용) + `beam_decode` (beam=4, α=0.6 GNMT penalty) + `translate` 헬퍼
- L.c: Transformer greedy vs beam 10 샘플 비교 (보조 분석)
- M.a: test 75k 전체 beam-4 추론 → sacreBLEU(intl, signature) + 95% CI(n=1000 bootstrap) + ChrF++(word_order=2) + COMET(wmt22-comet-da) 산출
- M.b: COMET 기준 상위/하위 5개 사례 출력 + 오류 유형 분류 표
- **Seq2seq 추론 기준**: `s2s_best_ep024_loss1.1240.pt` (ep025 유실 → ep024 복구본, val_loss 1.1240)
- 결과 기입: Colab 실행 후 M.a-결과·M.b-결과 markdown 셀 채울 것
- **J.b-recover-best 셀 추가** + 복구 완료: ep021→ep024 재학습, `_RECOVER_MAX_EP=26` NaN 차단

### 세션 9 예정 (N. 총평)
- 두 모델 성능 비교 종합
- Attention 메커니즘의 성능 개선 정량적 결론
- 한계(limitation) 및 향후 개선 방향 기술
