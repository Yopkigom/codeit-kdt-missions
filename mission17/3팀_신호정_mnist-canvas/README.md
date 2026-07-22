# MNIST 손글씨 숫자 인식 (Streamlit + ONNX)

캔버스에 마우스로 그린 숫자를 MNIST ONNX 모델로 예측하는 웹 서비스.
미션 17 산출물(코드잇 K-DT AI 엔지니어, 3팀 신호정).

## 화면 구성

1. **입력 캔버스** — `streamlit-drawable-canvas`로 숫자를 그린다(검은 배경 + 흰 펜).
2. **전처리 이미지** — 모델 입력(28×28)으로 변환한 결과를 확대 표시한다.
3. **추론 결과** — 0~9 각 클래스 확률을 막대 차트로 시각화한다.
4. **이미지 저장소** — 그린 이미지와 예측 레이블·확률을 세션 내에 누적한다.

## 실행

### Docker Hub 이미지로 실행

```bash
docker run --rm -p 8501:8501 yopkigom/input-recognition-pilot:1.0
```

브라우저에서 `http://localhost:8501` 접속.

### 소스에서 빌드

```bash
docker build -t input-recognition-pilot:1.0 .
docker run --rm -p 8501:8501 input-recognition-pilot:1.0
```

### 로컬(파이썬) 실행

```bash
pip install -r requirements.txt
streamlit run app.py
```

## 구조

| 파일 | 역할 |
|---|---|
| `app.py` | Streamlit UI 4영역과 세션 상태 |
| `model_utils.py` | 모델 다운로드·캐싱, ONNX 추론(softmax 포함) |
| `preprocess.py` | 캔버스 RGBA → (1,1,28,28) 텐서 + 전처리 미리보기 |
| `verify_preprocess.py` | 전처리 설계 근거·파이프라인 정확도 재현 검증(개발용, 이미지 미포함) |

## 모델

- 출처: [onnx/models](https://github.com/onnx/models/tree/main/validated/vision/classification/mnist/model)의 `mnist-12.onnx` (opset 12).
- 이미지에 굽지 않고 **런타임에 GitHub LFS 미디어 엔드포인트**에서 받아 캐싱한다.
  (Git LFS라 `raw.githubusercontent.com`은 포인터만 반환 → `media.githubusercontent.com/media/...` 사용.)
- 입력 `Input3` `(1,1,28,28)` float32, 출력 `Plus214_Output_0` `(1,10)`.
  모델 내부에 softmax가 없어 raw logit을 반환하므로 애플리케이션에서 softmax를 적용한다.

## 전처리 결정 근거(실측)

데이터는 MNIST 공식 테스트셋(t10k)이며 PyTorch 공식 미러(ossci-datasets S3)에서 받는다.
표본은 테스트셋 **앞에서부터 순서대로** 사용한다(무작위 추출 아님).

| 후보 (앞 500장) | 정확도 |
|---|---|
| raw 0~255 (반전 없음) | 98.6% |
| /255 0~1 (반전 없음) | 98.6% |
| **MNIST 표준화 (반전 없음)** | **98.8%** |
| raw 0~255 (반전) | 31.0% |
| /255 0~1 (반전) | 34.4% |

모델이 Conv/ReLU/MaxPool 선형 계열이라 양의 스칼라 배율은 argmax를 바꾸지 않는다.
따라서 raw와 /255가 동일하며, 실제로 갈리는 것은 **반전 여부**다.
스케일은 softmax 가독성을 위해 MNIST 표준화(0.1307/0.3081)를 채택했다.
손그림 인식률 향상을 위해 바운딩 박스 크롭 후 20×20 리사이즈 → 28×28 중앙 정렬(MNIST 관례)을 수행한다.

### 재현 방법

```bash
python verify_preprocess.py
# 또는 컨테이너에서
docker run --rm -v "$PWD":/work -w /work --entrypoint python input-recognition-pilot:1.0 verify_preprocess.py
```

`preprocess.py`를 실제로 통과시킨 파이프라인 정확도는 **95.7%**(앞 300장)다.
28×28을 280×280으로 늘렸다가 다시 줄이는 이중 리사이즈가 들어가 실험 1보다 낮게 나오는 것이 정상이다.
