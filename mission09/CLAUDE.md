# CLAUDE.md — 미션 09

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## Overview

**패션 아이템 이미지 생성 모델 개발 — Generative Model.**
FashionMNIST를 학습해 새로운 패션 아이템 이미지를 생성한다.
**cGAN과 Diffusion 두 계열을 모두 구현**하고 결과를 비교해 적합성을 검증하는 것이 목적.

과제: [스프린트 미션9](https://www.codeit.kr/topics/studyGroupStep6985c044aa647f3831b02826/lessons/12070) · 작성 2026-04-15

## Key Files

- `09_2팀_신호정.ipynb` — 전체 파이프라인
- `fashion_mnist/` — **데이터 + 체크포인트 (폴더 내 동봉, 298MB)**
  - `fashion-mnist_train.csv` / `fashion-mnist_test.csv`
  - `train-images-idx3-ubyte` 등 IDX 원본 (`struct`로 직접 파싱)
  - `torchvision/` — torchvision 캐시
  - `checkpoints/cgan_baseline/`, `cgan_final/`, `diff_final/` — 학습 체크포인트
- `NanumGothic.ttf` — matplotlib 한글 폰트

> **경로 주의**: 원래 Colab의 `/content/drive/MyDrive/fashion_mnist/`에 있던 것을
> `./fashion_mnist/`로 옮겨 담았다. 노트북 상단 경로 상수를 이에 맞게 수정해야 한다.

## Notebook Section Structure

| 섹션 | 내용 |
|---|---|
| A | 프로젝트의 목적 (수행 방향 / 수행 계획) |
| B | **패션 아이템의 구분** — 클래스별 특성 / 모델 설계에 미치는 영향 |
| C | 데이터 리뷰 — 개요 / 구조 / 클래스 분포 / 대응 전략 |
| D | 모델 비교 및 선정 — **cGAN** / **Diffusion** / 비교 요약 / 세부 모델 선정 |
| E | 프로젝트 수행 환경 설정 (미션 05 골격 계승) |
| F~ | 모델 구현 · 학습 · 평가 |

## 모델 비교 (섹션 D)

| | cGAN | Diffusion |
|---|---|---|
| 학습 안정성 | 낮음 (mode collapse 위험) | 높음 |
| 샘플링 속도 | 빠름 (1-step) | 느림 (multi-step) |
| 조건부 생성 | 클래스 조건 임베딩 | 클래스 조건 임베딩 |

**두 모델은 동일 데이터·동일 평가 조건에서 비교된다.** 한쪽만 조건을 바꾸지 말 것.

## 평가

`torchmetrics` 기반 생성 품질 지표(FID 계열) 사용.

> **생성 모델은 loss가 낮다고 품질이 좋은 것이 아니다.**
> 특히 GAN의 D/G loss는 품질과 단조 관계가 아니므로,
> **반드시 정량 지표(FID) + 생성 샘플 그리드(정성)를 함께** 확인할 것.
> cGAN에서는 **mode collapse**(다양성 붕괴)를 별도로 점검해야 한다 —
> loss는 멀쩡한데 같은 이미지만 찍어내는 상태가 흔하다.

## Dependencies

`torch`, `torchvision`, `torchmetrics`, `optuna`, `struct`(IDX 파싱). **GPU 필요.**

## 작업 시 유의점

- `A. / A-a.` 계층 표기 규약 적용 대상이다.
- 난수 고정(`SEED = 42`)이 되어 있어도 생성 결과는 샘플링 노이즈에 민감하다.
  결과 비교 시 **동일 시드의 고정 노이즈 벡터**로 샘플링할 것.
