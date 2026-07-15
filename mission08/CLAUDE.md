# CLAUDE.md — 미션 08

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Overview

**축구 경기 이미지 분할 모델 개발 — Semantic Segmentation.**
축구 경기 영상 내 객체를 **픽셀 단위 11개 클래스**로 분할한다.
**U-Net 사용이 필수 요건**이며, **Mask2Former**를 추가 개발 대상으로 병행한다.

과제: [스프린트 미션8](https://www.codeit.kr/topics/studyGroupStep6985c044aa647f3831b02824/lessons/12069) · 작성 2026-04-10

## Key Files

- `08_2팀_신호정.ipynb` — 전체 파이프라인
- `football_seg/` — **데이터 + 체크포인트 (폴더 내 동봉, 423MB)**
  - `COCO_Football Pixel.json` — COCO 형식 픽셀 어노테이션
  - `images/`, `mask_cache/` — 원본 이미지 및 마스크 캐시
  - `About Acme AI.txt`, `www.acmeai.tech ODataset 3 ... .pdf` — 데이터 출처 문서
- `NanumGothic.ttf` — matplotlib 한글 폰트

> **경로 주의**: 원래 Colab의 `/content/drive/MyDrive/football_seg/`에 있던 것을
> `./football_seg/`로 옮겨 담았다. 노트북 상단 경로 상수를 이에 맞게 수정해야 한다.

체크포인트: `best_unet.pth`, `best_unet_baseline.pth`, `best_unet_final.pth`,
`best_mask2former.pth`, `best_m2f_baseline.pth`, `best_m2f_final.pth` 등 보존됨.

## Notebook Section Structure

| 섹션 | 내용 |
|---|---|
| A | 프로젝트의 목적 (수행 방향 / 수행 계획) |
| B | **축구 경기 객체 클래스 정의 및 특성 고찰** — 클래스별 특성 / 모델 설계에 미치는 영향 |
| C | 데이터 리뷰 — 개요 / 구조 / **클래스 분포 분석** / **소량 데이터 대응 전략** |
| D | 모델 비교 및 선정 — U-Net / Mask2Former / 비교 요약 / 세부 모델 선정 |
| E | 프로젝트 수행 환경 설정 (미션 05 골격 계승) |
| F~ | 모델 구현 · 학습 · 평가 |

## 핵심 난점 (섹션 B·C에 근거)

1. **객체 크기 편차가 극심하다** — 축구장/관중석(거대) vs 공(극소).
   작은 객체를 놓치지 않는 구조가 필요하며, 이것이 U-Net의 skip connection과
   Mask2Former 선택의 근거다.
2. **객체 겹침** — 선수 간 오클루전.
3. **소량 데이터** — 섹션 C-d에 전용 대응 전략(증강 등)이 정리되어 있다.

> 클래스 불균형이 심하므로 **전체 픽셀 정확도(pixel accuracy)로 평가하면 안 된다.**
> mIoU 및 **클래스별 IoU**를 반드시 함께 볼 것 (공 같은 소수 클래스가 0이어도 전체 수치는 높게 나온다).

## 모델

| 모델 | 역할 | 라이브러리 |
|---|---|---|
| **U-Net** | 주 개발 대상 (필수 요건) | `segmentation_models_pytorch` |
| **Mask2Former** | 추가 개발 대상 | `transformers` |

## Dependencies

`torch`, `torchvision`, `torchmetrics`, `segmentation_models_pytorch`, `transformers`,
`albumentations`(증강), `optuna`, `cv2`, `PIL`. **GPU 필요.**

## 작업 시 유의점

- `A. / A-a.` 계층 표기 규약 적용 대상이다.
- 증강(`albumentations`) 파이프라인은 **마스크에도 동일 변환이 적용**되어야 한다.
  이미지에만 적용하고 마스크를 누락하는 것이 이 태스크의 대표적 버그다.
