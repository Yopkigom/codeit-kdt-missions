"""캔버스 원본 이미지를 MNIST ONNX 모델 입력 규격으로 변환합니다.

전처리 방식은 실제 MNIST 500장에 대해 실험한 결과를 기반으로 결정했습니다.
- MNIST 공식 테스트셋(t10k) 이며, PyTorch 공식 미러(ossci-datasets S3) 에서 받았습니다.  
    https://ossci-datasets.s3.amazonaws.com/mnist/t10k-images-idx3-ubyte.gz  (1,648,877 B)  
    https://ossci-datasets.s3.amazonaws.com/mnist/t10k-labels-idx1-ubyte.gz  (4,542 B)  
- 그 결과, 흰 글씨/검은 배경(반전 없음) 98.6% vs 반전 31% → 반전 없음이 압도적으로 높습니다.  
- 스케일은 argmax에 영향이 없으나(모델이 선형 계열), softmax 가독성을 위해 MNIST 표준화(mean=0.1307, std=0.3081)를 적용합니다.  
- 전처리는 용량 최소화를 위해 Pillow + numpy만 사용합니다.(OpenCV 미사용)  
"""

import numpy as np
from PIL import Image

MNIST_MEAN = 0.1307
MNIST_STD = 0.3081

# 전경(글씨) 판정 임계값. 검은 배경(0) 위 흰 획(고값)을 구분합니다.
_FG_THRESHOLD = 20


def _to_grayscale(rgba: np.ndarray) -> Image.Image:
    """캔버스 RGBA 배열을 검은 배경에 합성한 뒤 그레이스케일로 변환합니다.

    streamlit-drawable-canvas의 image_data는 미입력 영역이 투명(alpha=0)일 수 있어,
    검은 배경에 알파 합성해 'MNIST와 동일한 흰 글씨/검은 배경'을 보장합니다.
    """
    img = Image.fromarray(rgba.astype(np.uint8), mode="RGBA")
    black_bg = Image.new("RGBA", img.size, (0, 0, 0, 255))
    return Image.alpha_composite(black_bg, img).convert("L")


def _center_digit(gray: Image.Image) -> Image.Image:
    """MNIST 관례에 맞춰 글씨를 20x20 박스로 리사이즈 후 28x28 중앙에 배치합니다.

    손그림은 위치·크기가 제각각이라, 바운딩 박스로 잘라 중앙 정렬하면
    학습 분포(중앙 정렬된 숫자)와 가까워져 인식률이 오릅니다.
    """
    arr = np.asarray(gray)
    coords = np.argwhere(arr > _FG_THRESHOLD)
    if coords.size == 0:
        # 전경이 없으면(빈 입력) 그대로 축소해 반환한다.
        return gray.resize((28, 28), Image.LANCZOS) # LANCZOS(란초스)는 PIL에서 가장 고품질 리사이즈 필터입니다.

    y0, x0 = coords.min(axis=0)
    y1, x1 = coords.max(axis=0) + 1
    crop = gray.crop((int(x0), int(y0), int(x1), int(y1)))

    w, h = crop.size
    scale = 20.0 / max(w, h)
    new_w, new_h = max(1, round(w * scale)), max(1, round(h * scale))
    crop = crop.resize((new_w, new_h), Image.LANCZOS) # 업스케일링, 다운스케일링 모두에서 사용

    canvas = Image.new("L", (28, 28), 0)
    canvas.paste(crop, ((28 - new_w) // 2, (28 - new_h) // 2))
    return canvas


def preprocess(rgba: np.ndarray):
    """캔버스 RGBA 배열을 (1,1,28,28) float32 텐서와 28x28 미리보기로 변환합니다.

    Returns:
        tensor: 모델 입력용 (1,1,28,28) float32 (MNIST 표준화 적용)
        preview: 전처리 결과 확인용 28x28 그레이스케일 PIL 이미지
    """
    gray = _to_grayscale(rgba)
    preview = _center_digit(gray)

    arr = np.asarray(preview, dtype=np.float32)
    normalized = (arr / 255.0 - MNIST_MEAN) / MNIST_STD
    tensor = normalized.reshape(1, 1, 28, 28).astype(np.float32)
    return tensor, preview
