"""전처리 설계 근거와 추론 파이프라인 정확도를 재현 검증하는 스크립트입니다.

preprocess.py 독스트링이 인용하는 수치를 누구나 재현할 수 있도록 실험을 코드로 남깁니다.
- 실험 1: 스케일, 반전 후보 비교 (반전 없음이 옳다는 설계 근거)
- 실험 2: preprocess.py를 실제로 통과시킨 파이프라인 정확도
- 실험 3: 빈 입력(전경 없음) 방어 확인

데이터는 MNIST 공식 테스트셋(t10k)이며, PyTorch 공식 미러(ossci-datasets S3)에서 받습니다.
원본 호스트(yann.lecun.com)가 불안정해 미러를 사용합니다.

실행:
    python verify_preprocess.py

이 스크립트는 개발/검증용이라 Docker 이미지에는 포함하지 않습니다.
컨테이너에서 실행하려면 소스를 마운트해서 쓰면 됩니다.
    docker run --rm -v "$PWD":/work -w /work --entrypoint python <image> verify_preprocess.py
"""

import gzip
import os
import urllib.request

import numpy as np
import onnxruntime as ort
from PIL import Image

# 실제 앱과 동일한 코드 경로를 검증하기 위해 제출 모듈을 그대로 가져옵니다.
# _download_model은 내부 함수지만, streamlit 실행 문맥 없이 모델만 확보하려고 직접 사용합니다.
from model_utils import _download_model, predict
from preprocess import MNIST_MEAN, MNIST_STD, preprocess

MNIST_BASE_URL = "https://ossci-datasets.s3.amazonaws.com/mnist/"
IMAGES_FILE = "t10k-images-idx3-ubyte.gz"
LABELS_FILE = "t10k-labels-idx1-ubyte.gz"

CACHE_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".verify_cache")

# 표본은 테스트셋 앞에서부터 순서대로 사용합니다(무작위 추출 아님).
SCALE_SAMPLES = 500
PIPELINE_SAMPLES = 300

CANVAS_SIZE = 280

# IDX 파일 포맷의 매직 넘버(이미지 2051, 라벨 2049)로 정품 여부를 확인합니다.
_IMAGE_MAGIC = 2051
_LABEL_MAGIC = 2049


def _fetch(filename: str) -> str:
    """MNIST 파일을 캐시에 확보하고 경로를 반환합니다."""
    os.makedirs(CACHE_DIR, exist_ok=True)
    path = os.path.join(CACHE_DIR, filename)
    if not os.path.exists(path):
        print(f"  다운로드: {filename}")
        urllib.request.urlretrieve(MNIST_BASE_URL + filename, path)
    return path


def _load_images(path: str) -> np.ndarray:
    """IDX 이미지 파일을 (N, 28, 28) uint8 배열로 읽습니다."""
    with gzip.open(path, "rb") as f:
        data = f.read()
    magic = int.from_bytes(data[0:4], "big")
    if magic != _IMAGE_MAGIC:
        raise RuntimeError(f"MNIST 이미지 파일이 아닙니다(magic={magic}).")
    count = int.from_bytes(data[4:8], "big")
    rows = int.from_bytes(data[8:12], "big")
    cols = int.from_bytes(data[12:16], "big")
    return np.frombuffer(data[16:], np.uint8).reshape(count, rows, cols)


def _load_labels(path: str) -> np.ndarray:
    """IDX 라벨 파일을 (N,) uint8 배열로 읽습니다."""
    with gzip.open(path, "rb") as f:
        data = f.read()
    magic = int.from_bytes(data[0:4], "big")
    if magic != _LABEL_MAGIC:
        raise RuntimeError(f"MNIST 라벨 파일이 아닙니다(magic={magic}).")
    return np.frombuffer(data[8:], np.uint8)


def _to_canvas_rgba(image28: np.ndarray) -> np.ndarray:
    """28x28 MNIST 이미지를 280x280 RGBA(흰 글씨/검은 배경)로 확대해 캔버스 입력을 흉내냅니다."""
    enlarged = Image.fromarray(image28).resize((CANVAS_SIZE, CANVAS_SIZE), Image.NEAREST)
    gray = np.asarray(enlarged, dtype=np.uint8)
    rgba = np.zeros((CANVAS_SIZE, CANVAS_SIZE, 4), dtype=np.uint8)
    rgba[:, :, 0] = gray
    rgba[:, :, 1] = gray
    rgba[:, :, 2] = gray
    rgba[:, :, 3] = 255
    return rgba


def experiment_scale_and_inversion(session, images, labels) -> dict:
    """스케일, 반전 후보별 정확도를 비교해 전처리 설계 근거를 재현합니다.

    모델이 Conv/ReLU/MaxPool 선형 계열이라 양의 스칼라 배율은 argmax를 바꾸지 않습니다.
    따라서 raw와 /255의 정확도는 같게 나오며, 갈리는 것은 반전 여부입니다.
    """
    input_name = session.get_inputs()[0].name
    samples = images[:SCALE_SAMPLES].astype(np.float32)
    answers = labels[:SCALE_SAMPLES]

    candidates = {
        "raw 0~255 (반전 없음)": lambda a: a,
        "/255 0~1 (반전 없음)": lambda a: a / 255.0,
        "MNIST 표준화 (반전 없음)": lambda a: (a / 255.0 - MNIST_MEAN) / MNIST_STD,
        "raw 0~255 (반전)": lambda a: 255.0 - a,
        "/255 0~1 (반전)": lambda a: 1.0 - a / 255.0,
    }

    results = {}
    for name, transform in candidates.items():
        correct = 0
        for i in range(SCALE_SAMPLES):
            tensor = transform(samples[i]).reshape(1, 1, 28, 28).astype(np.float32)
            predicted = int(session.run(None, {input_name: tensor})[0].argmax())
            correct += int(predicted == answers[i])
        results[name] = correct / SCALE_SAMPLES * 100
    return results


def experiment_pipeline(session, images, labels) -> float:
    """preprocess.py를 실제로 통과시켜 파이프라인 정확도를 측정합니다.

    28x28을 280x280으로 늘렸다가 다시 줄이는 이중 리사이즈가 들어가므로,
    실험 1의 직접 추론보다 수치가 다소 낮게 나오는 것이 정상입니다.
    """
    correct = 0
    for i in range(PIPELINE_SAMPLES):
        tensor, preview = preprocess(_to_canvas_rgba(images[i]))
        assert tensor.shape == (1, 1, 28, 28), f"입력 shape 이상: {tensor.shape}"
        assert tensor.dtype == np.float32, f"입력 dtype 이상: {tensor.dtype}"
        assert preview.size == (28, 28), f"미리보기 크기 이상: {preview.size}"

        probs = predict(session, tensor)
        assert probs.shape == (10,), f"확률 shape 이상: {probs.shape}"
        assert abs(float(probs.sum()) - 1.0) < 1e-5, "softmax 합이 1이 아닙니다."

        correct += int(int(probs.argmax()) == labels[i])
    return correct / PIPELINE_SAMPLES * 100


def experiment_empty_input() -> bool:
    """빈 캔버스(전경 없음)가 예외 없이 처리되는지 확인합니다."""
    empty = np.zeros((CANVAS_SIZE, CANVAS_SIZE, 4), dtype=np.uint8)
    empty[:, :, 3] = 255
    tensor, preview = preprocess(empty)
    return tensor.shape == (1, 1, 28, 28) and preview.size == (28, 28)


def main() -> None:
    print("MNIST 테스트셋 준비")
    images = _load_images(_fetch(IMAGES_FILE))
    labels = _load_labels(_fetch(LABELS_FILE))
    print(f"  로드 완료: {len(images)}장 {images.shape[1]}x{images.shape[2]}, 라벨 {len(labels)}개")

    print("\nONNX 모델 준비")
    model_path = _download_model()
    print(f"  모델: {model_path} ({os.path.getsize(model_path):,} bytes)")
    session = ort.InferenceSession(model_path, providers=["CPUExecutionProvider"])

    print(f"\n[실험 1] 스케일, 반전 후보 비교 (앞 {SCALE_SAMPLES}장)")
    for name, accuracy in experiment_scale_and_inversion(session, images, labels).items():
        print(f"  {name:26s} {accuracy:5.1f}%")

    print(f"\n[실험 2] preprocess.py 파이프라인 검증 (앞 {PIPELINE_SAMPLES}장)")
    print(f"  시뮬레이션 캔버스 입력 정확도 {experiment_pipeline(session, images, labels):5.1f}%")

    print("\n[실험 3] 빈 입력 방어")
    print(f"  결과: {'통과' if experiment_empty_input() else '실패'}")


if __name__ == "__main__":
    main()
