# CLAUDE.md — 미션 14

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

상위 규약은 [`../../CLAUDE.md`](../../CLAUDE.md)를 따른다.

## 프로젝트 개요

On-device RAG 챗봇의 **수직 슬라이스**(문서 처리 → 임베딩/검색 → 추론 → 실기기 배포)를 만드는 미션14 과제입니다.
`14_4팀_신호정.ipynb` 단일 노트북에 실험 계획·설계·진행을 모두 기록하며, 검증된 프로토타입을 Unity(6000.3.17f1) 챗봇 앱으로 포팅해 Galaxy S25에서 작동을 확인하는 것이 최종 목표입니다.

현재 상태: 노트북 A~X 전 섹션 작성 완료(초안). 전역 설정은 **A-e** 셀(`ROOT_PATH` 등 정의)입니다.

작업 계획의 단일 출처는 [PROJECT_PLAN.md](PROJECT_PLAN.md)입니다(A-b 수행 계획 + 섹션별 작업 계획). 섹션 작업 시 반드시 참조할 것.

## 작업 진행 방식

- **단일 패스 우선**: 개발 프로세스 전반 경험이 목표이므로 투기적 반복 실험·튜닝은 하지 않는다. 측정은 1회, 되돌림은 작동 실패 등 부득이한 경우로 한정한다.
- 작업은 **B ~ X 까지 섹션 단위로** 나눠 진행한다.
- **B ~ W** 섹션은 각 `계획 확인`(X-a) 셀에서 PROJECT_PLAN.md의 A-b 수행 계획과 지금까지의 수행 내역을 고려한 수행 계획을 요약 설명한 뒤 본 작업을 시작한다.
- 섹션/하위 구성 법칙을 준수한다: `## 섹션`은 알파벳 순(B, C, …), 하위는 `### X-a`, `### X-b` … 순이며 각 섹션의 첫 하위는 `계획 확인`(X-a)으로 시작한다. (결론 섹션 X는 예외 — `정량 결과 요약`으로 시작)

## 노트북 작성 규약

- 코드셀 주석은 **한글**로, 구분선 없이 심플하게 작성한다. (이 프로젝트 한정 규칙 — 전역 "English comments" 규칙을 이 노트북에서는 한글로 대체)
- 코드셀 **출력**에는 이모지를 넣지 않고 구분선도 넣지 않는다.

## 실행 환경

- 검증 환경: WSL2 + RTX 5050 Laptop (로컬)
- conda env `ai` (Python 3.12.13). 노트북 커널 이름은 `ai`. 명령 실행/패키지 설치 전 이 env가 활성인지 확인할 것.
- `jupyter nbconvert --to notebook --execute 14_4팀_신호정.ipynb` 로 전체 실행. 개별 셀은 노트북에서 직접 실행.
- Colab 이중 실행 지원: 모든 환경 의존 코드는 전역 `is_colab` 플래그로 분기해야 함(로컬 vs `/content/drive` 마운트). 새 셀 작성 시 이 규칙을 깨지 말 것.

## 경로/디렉터리 규약

전역 설정 셀이 `ROOT_PATH` 기준으로 아래 경로를 정의함. 하드코딩 대신 항상 이 변수를 사용할 것.

- `DataTable/` (`DATA_ROOT`): 검색 대상 입력 문서. `2024년_원천징수의무자를_위한_연말정산신고안내.pdf`.
- `Import/` (`IMPORT_ROOT`): 실행에 필요한 에셋. `NanumGothic.ttf`(시각화 한글 폰트).
- `Export/` (`EXPORT_ROOT`): 산출물 백업 위치. ONNX 임베딩 모델, 배포용 인덱스 파일, VectorDB, 골든 Q&A 셋, 라우팅 검증 질의 셋을 여기에 저장.

## 핵심 설계 원칙 (코드 작성 시 반드시 준수)

- **재현성**: `SEED = 42`를 random/numpy/torch에 전역 고정. 신규 무작위성 도입 시 동일 시드 적용.
- **벡터 공간 일치**: 배포용 인덱스는 **기기와 동일한 아티팩트**(ONNX 임베딩 모델)로 생성해 인덱스/질의 벡터 공간 불일치를 방지. 프로토타입 검색의 기준 경로는 인덱스 파일에 대한 **brute-force 코사인 유사도**이며, VectorDB는 오케스트레이션 편의용 보조 수단일 뿐. 인덱스 파일에는 매니페스트(임베딩 모델·양자화·차원·pooling/L2/prefix·청킹 파라미터·문서 해시)를 동봉해 재빌드·기기 로드 시 대조한다.
- **ONNX 패리티**: 임베딩 모델 변환 시 PyTorch 대비 코사인 ≥ 0.999. Pooling(mean/CLS)·L2 정규화·prefix 규칙(`query:`/`passage:`)을 변환 전후 동일하게 고정.
- **포팅 패리티**: LLM은 프로토타입과 동일 GGUF 양자화, 임베딩은 ONNX. Unity 측은 `Microsoft.ML.Tokenizers` 토크나이저 + C# 코사인 검색 + C# RAG 상태기계로 재구현하며, 토큰 ID·질의 임베딩이 Python 기준과 일치하는지 검증.
- **Fallback 대비**: RAG 그래프(LangGraph)에 검색 점수 임계값 라우터 노드를 처음부터 반영(범위 밖/불확실 질의 → LLM API fallback). 1차 라우팅 신호는 최상위 코사인 유사도, logprob은 보조 신호.

## 성능 합격선

Galaxy S25 기준, 생성(decode) 256토큰에서 wall-clock(prefill + decode) **≤ 15초**. prefill·decode·TTFT는 분리 측정. 미달 시 더 작은 모델 교체 / top-k 축소 / 컨텍스트 압축으로 대응.

## 평가 기준

- 검색 측(brute-force 경로): Context Recall / Precision.
- 생성 측: Faithfulness ≥ 0.8, Answer Relevancy(LLM-as-a-Judge).
- 핵심 지표 표본 50개 이상 또는 신뢰구간 병기.
