# CLAUDE.md — 미션 16

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## 필수 참조 파일

작업 시작 전 반드시 [PROJECT_PLAN.md](PROJECT_PLAN.md)를 읽는다.
체크리스트 진행 순서와 각 섹션의 완료 여부가 기록되어 있다.

## Project Overview

**모델 포맷 변환 실습.** 미션 06(흉부 X-ray 폐렴 진단)의 MobileNetV3 전이학습 모델을
재학습한 뒤 3종 포맷으로 추출하고, ONNX 기반 추론 코드로 동작을 검증한다.

| 산출 포맷 | 파일명 | 방식 |
|---|---|---|
| PyTorch FP32 | `mission_16_mobilenetv3.pth` | `state_dict` 저장 |
| PyTorch INT8 | `mission_16_mobilenetv3_quant.pth` | PTQ(정적 양자화) 후 TorchScript 저장 |
| ONNX | `mission_16_mobilenetv3.onnx` | opset 17, Fixed Shape `(1,3,224,224)` |

**미션 06의 설계 원칙을 승계한다**: 평가는 accuracy 단독이 아니라
**Recall(폐렴)·FN 개수를 우선**으로 하며, 양자화 전후 FN 변화가 보고서의 핵심 논점이다.

**(심화)** 제공된 `mnist_cnn.onnx`를 **Unity Sentis(C#)** 환경에서 로드하여
타겟 이미지 3장을 추론한다. 채점 제외 항목이나, Unity → On-Device AI 포트폴리오
연계 가치가 있어 수행한다.

## 폴더 구성

```
mission16/
├── CLAUDE.md / PROJECT_PLAN.md
├── NanumGothic.ttf
├── 4팀_신호정/                  # 제출 폴더 (⚠ 팀명 미확정 — 확정 시 개명)
│   ├── modeling.ipynb           # 학습 + 3종 포맷 추출
│   ├── inference.ipynb          # ONNX 기반 추론 + 3종 비교 평가
│   ├── report-materials.md      # 요약 보고서 원고 (→ PDF 변환 제출)
│   ├── DEBUG_LOG.md             # 디버깅·오류 해결 과정 (제출 요건)
│   ├── models/                  # 추출 모델 3종 (용량 캡처 대상)
│   ├── captures/                # 용량 확인 캡처 등
│   └── advanced/                # 심화 제출물 (별도 zip)
│       ├── README.md
│       ├── Scripts/             # Unity C# 스크립트 사본
│       └── captures/            # 추론 결과 화면 캡처
└── advanced_assets/             # mnist_cnn.onnx + 타겟 이미지 (⚠ zip 제외)
```

## ⚠ 데이터 경로

본 미션 학습·평가 데이터는 미션 06과 동일하다.

```
/mnt/wsl_data/datasets/chest-xray-pneumonia/chest-x-ray-images-pneumonia/
```

노트북의 경로 상수는 위 절대경로를 기본값으로 하고, Colab 실행 시 `kagglehub`로 분기한다.
전처리·증강은 미션 06 섹션 C·D의 방사선학적 근거를 따른다
(대비 관련 증강이 침윤 소견을 훼손하지 않도록 주의).

## Execution Environments

| 용도 | 환경 |
|------|------|
| 학습·변환·추론 | WSL2 + RTX 5050 Laptop (로컬 우선 — 데이터 5.8k장, MobileNetV3 경량) |
| 예비 | Google Colab L4 (`is_colab` 분기, 미션 13 패턴) |
| 양자화 백엔드 | `fbgemm` (x86 로컬 실행 기준. 모바일 배포 시 `qnnpack` — 보고서 논점) |
| 심화 | Unity 6.3 LTS (6000.3.17f1) + Unity Sentis, Windows 측 프로젝트 |

난수 고정 `SEED = 42` (random / numpy / torch / torch.cuda).

## 제출 요건 (zip 2개)

1. **본 미션 zip**: `modeling.ipynb`, `inference.ipynb`, 보고서 PDF,
   모델 3종 용량 확인 캡처 1장, 디버깅·오류 해결 과정 정리
2. **심화 zip**: `README.md`, 실행 코드 전체(**onnx 모델 파일 제외**), 추론 결과 캡처

`advanced_assets/`와 데이터셋은 어떤 zip에도 포함하지 않는다.

## 문서 규약

섹션 계층 표기(`A.` / `A-a.` / `A-a-1.`), 셀 위치 표기(`B-b의 3번째 셀`),
한글 주석, 구분선 금지 — 모두 상위 규약 및 미션 13과 동일.

각 섹션의 **완료 확인** 단계를 마친 직후, Claude가 PROJECT_PLAN.md의 해당
`[ ]`를 `[x]`로 갱신한다. 완료 여부가 불확실한 항목은 사용자 확인 후 갱신한다.
