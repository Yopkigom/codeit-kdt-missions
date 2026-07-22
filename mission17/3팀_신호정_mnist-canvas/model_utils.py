"""CPU 기반 MNIST ONNX 모델 관리(다운로드, 캐싱)와 추론 기능을 제공하는 유틸리티입니다.

모델은 런타임에 GitHub LFS 미디어 엔드포인트에서 다운로드하며, 세션 간 캐싱합니다.
- 확인 결과 onnx/models 저장소는 Git LFS로 관리되고 있으며,  
    raw.githubusercontent.com은 130바이트 LFS 포인터만 반환합니다.
    때문에 반드시 media.githubusercontent.com/media/... 를 사용해야 온전히 다운로드가 가능합니다.
- 공개 리소스라 다운로드에 자격증명이 필요 없습니다.
"""

import os
import tempfile

import numpy as np
import onnxruntime as ort
import requests
import streamlit as st

# 콤마 주의 : 없어야 URL 한 줄 인식이 됩니다.
MODEL_URL = (
    "https://media.githubusercontent.com/media/onnx/models/main/"
    "validated/vision/classification/mnist/model/mnist-12.onnx"
)

CACHE_DIR = os.environ.get(
    "MODEL_CACHE_DIR", # Dockerfile에서 정의된 모델 캐싱 경로 환경변수
    os.path.join(os.path.expanduser("~"), ".cache", "mnist-canvas"), # 만약 환경변수가 없으면 홈 디렉토리 아래에 캐시
)

MODEL_PATH = os.path.join(CACHE_DIR, "mnist-12.onnx")

# LFS 포인터(약 130B)를 실제 모델(약 26KB)로 오인하지 않기 위한 하한 값을 정의합니다.
_MIN_VALID_BYTES = 10_000
_DOWNLOAD_TIMEOUT = 30


def _download_model() -> str:
    """모델을 캐시에 확보하고 경로를 반환한다. 유효 캐시가 있으면 재사용한다."""
    os.makedirs(CACHE_DIR, exist_ok=True)
    if os.path.exists(MODEL_PATH) and os.path.getsize(MODEL_PATH) > _MIN_VALID_BYTES:
        return MODEL_PATH

    try:
        response = requests.get(MODEL_URL, timeout=_DOWNLOAD_TIMEOUT)
        response.raise_for_status()
    except requests.exceptions.RequestException as e:
        raise RuntimeError(f"모델 다운로드 실패: {e}") from e
    
    if len(response.content) < _MIN_VALID_BYTES:
        # LFS 포인터가 내려온 경우(잘못된 URL 등)를 방어하기 위한 예외처리입니다.
        raise RuntimeError(
            "다운로드한 모델이 비정상적으로 작습니다(LFS 포인터 의심). URL을 확인하세요."
        )

    # 원자적 교체로 부분 파일이 캐시에 남지 않게 한다.
    fd, tmp_path = tempfile.mkstemp(dir=CACHE_DIR, suffix=".part") # 독립적인 임시 파일 생성
    with os.fdopen(fd, "wb") as f:
        f.write(response.content)
    os.replace(tmp_path, MODEL_PATH) # 임시 파일에 기록된 내용을 캐시 파일 내용으로 원자적 교체(파일 핸들 교체)
    return MODEL_PATH


@st.cache_resource(show_spinner="MNIST 모델을 불러오는 중...")
def load_session() -> ort.InferenceSession:
    """ONNX 추론 세션을 로드하고 세션 간 캐싱합니다.

    @st.cache_resource로 프로세스 수명 동안 세션을 재사용해,
    매 요청마다 재다운로드·재초기화하지 않습니다.
    """
    path = _download_model()
    return ort.InferenceSession(path, providers=["CPUExecutionProvider"])


def _softmax(logits: np.ndarray) -> np.ndarray:
    """수치 안정 softmax. 최댓값을 빼 오버플로를 방지합니다."""
    shifted = logits - logits.max()
    exp = np.exp(shifted)
    return exp / exp.sum()


def predict(session: ort.InferenceSession, tensor: np.ndarray) -> np.ndarray:
    """(1,1,28,28) 텐서를 받아 0 ~ 9 클래스 확률 (10,)을 반환합니다.

    모델 출력(Plus214_Output_0)은 softmax가 없는 raw logit이므로 직접 softmax를 적용합니다.
    """
    input_name = session.get_inputs()[0].name
    logits = session.run(None, {input_name: tensor})[0].ravel()
    return _softmax(logits)
