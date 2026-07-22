"""손글씨 숫자 인식 Streamlit 웹 서비스.

가이드가 요구하는 4개 화면 영역을 구성합니다.
- 입력 캔버스 : 마우스로 숫자를 그린다.
- 전처리 이미지 표시 : 모델 입력(28x28)으로 변환한 결과를 확대해 보여준다.
- 모델 추론 결과 : 0 ~ 9 각 클래스 확률을 막대 차트로 시각화한다.
- 이미지 저장소 : 그린 이미지와 예측 레이블·확률을 누적 표시한다(세션 한정).
"""

import base64
import hashlib
import io

import numpy as np
import pandas as pd
from PIL import Image
import streamlit as st
# streamlit과의 호환성 문제가 streamlit_drawable_canvas에 있어,
# requirements.txt에 아래를 명시했습니다.
# streamlit==1.27.2
# streamlit-drawable-canvas==0.9.3
from streamlit_drawable_canvas import st_canvas

# 모델을 다운로드하고 ONNX 추론 세션을 로드하고, 세션을 기준으로 추론하는 유틸리티 함수를 가져옵니다.
from model_utils import load_session, predict
# 캔버스에서 입력된 RGBA 배열을 MNIST 모델(ONNX) 입력용 텐서와 28x28 전처리 이미지로 변환하는 함수를 가져옵니다.
from preprocess import preprocess

CANVAS_SIZE = 280
DIGITS = [str(i) for i in range(10)]

# 저장소 표시 설정. 8열 그리드로 배치하고, 8개 이상 쌓이면 스크롤 영역으로 만듭니다.
HISTORY_COLUMNS = 8
HISTORY_SCROLL_THRESHOLD = 8
HISTORY_MAX_HEIGHT_PX = 260


def _canvas_signature(rgba: np.ndarray) -> str:
    """캔버스 배열 내용의 해시값입니다. 그림이 동일하면 같은 값이 나옵니다."""
    return hashlib.sha1(np.ascontiguousarray(rgba).tobytes()).hexdigest()


def _thumb_to_data_uri(image: Image.Image) -> str:
    """PIL 이미지를 PNG data URI로 변환합니다(HTML img 태그에 인라인 삽입용)."""
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")

st.set_page_config(page_title="손글씨 숫자 인식", page_icon="✍️", layout="wide")
st.title("✍️ 손글씨 숫자 인식")
st.caption("검은 배경에 흰색으로 0 ~ 9 숫자를 그리면 ONNX 모델이 예측합니다.")

# model_utils.py에서 세션 간 캐싱된 추론 세션을 획득합니다(최초 1회 다운로드, 로드)
session = load_session()

if "history" not in st.session_state:
    st.session_state["history"] = []

with st.sidebar:
    st.header("설정")
    stroke_width = st.slider("펜 굵기", min_value=10, max_value=40, value=22)
    st.markdown(
        "- 숫자는 캔버스 **중앙**에 크게 그리세요.\n"
        "- 좌측 상단 휴지통 아이콘으로 지울 수 있습니다."
    )

# 화면을 좌우 2열로 나누어, 좌측에 입력 캔버스, 우측에 전처리 이미지와 추론 결과를 표시합니다.
col_canvas, col_result = st.columns(2)

# 좌측 캔버스 영역을 정의합니다.
with col_canvas:
    st.subheader("이 곳에 숫자를 그리세요 (0 ~ 9)")
    canvas = st_canvas(
        fill_color="#000000",
        stroke_width=stroke_width,
        stroke_color="#FFFFFF",
        background_color="#000000",
        height=CANVAS_SIZE,
        width=CANVAS_SIZE,
        drawing_mode="freedraw",
        key="canvas",
    )

# 캔버스에 실제로 그림이 있는지 확인합니다 (검은 배경이면 RGB 합이 0)
# 이 부분은 마우스 버튼을 뗄 때마다 실행되므로, 전처리와 추론은 실제 그림이 있을 때만 수행합니다.
# UX 변화가 있으면 계속해서 트리거되기 때문에, 현재 사용 중인 mnist 모델에서나(추론 속도 ms 단위) 사용할 수 있는 코드 구조입니다. 
has_drawing = (
    canvas.image_data is not None
    and np.asarray(canvas.image_data)[:, :, :3].sum() > 0
)

# 캔버스 내용이 그대로인 rerun(슬라이더 조작, 버튼 클릭 등)에서는 추론을 건너뜁니다.
# 배열 해시를 세션에 보관해, 동일한 입력이면 이전 결과를 그대로 재사용합니다.
probs, pred_label, preview = None, None, None
if has_drawing:
    canvas_array = np.asarray(canvas.image_data)
    signature = _canvas_signature(canvas_array)
    cached = st.session_state.get("last_inference")

    if cached is not None and cached["signature"] == signature:
        # 동일 입력이므로 전처리, 추론을 모두 생략합니다.
        preview = cached["preview"]
        probs = cached["probs"]
        pred_label = cached["pred_label"]
    else:
        tensor, preview = preprocess(canvas_array) # preprocess.py 에서 모델 입력 텐서와 전처리 이미지를 반환합니다.
        probs = predict(session, tensor) # model_utils.py에서 ONNX 추론 세션을 기준으로 예측 확률을 반환합니다.
        pred_label = int(np.argmax(probs))
        st.session_state["last_inference"] = {
            "signature": signature,
            "preview": preview,
            "probs": probs,
            "pred_label": pred_label,
        }

# 우측 결과 영역을 정의합니다.
with col_result:
    st.subheader("모델 추론용 전처리 이미지")
    if preview is not None:
        # NEAREST로 확대해 픽셀 단위 전처리 결과를 육안 확인합니다.
        st.image(preview.resize((140, 140), Image.NEAREST), caption="모델 입력 미리보기")
    else: # empty state 정의
        st.info("캔버스에 숫자를 그리면 전처리 결과가 표시됩니다.")

    st.subheader("모델 추론 결과")
    if probs is not None:
        st.metric("예측 숫자", pred_label, f"{probs[pred_label] * 100:.1f}%")
        chart_df = pd.DataFrame({"확률": probs}, index=DIGITS)
        st.bar_chart(chart_df)
    else: # empty state 정의
        st.info("예측 결과가 여기에 표시됩니다.")

# 저장소 영역을 나누기 위해 divider를 추가합니다.
st.divider()

# 저장소 영역을 화면 좌측의 40% 만 사용하도록 정의합니다.
action_col, _ = st.columns([2, 3])

with action_col:
    # 저장, 비우기 영역을 좌우 2열로 나누어 버튼을 배치합니다.
    save_col, clear_col = st.columns(2)
    with save_col:
        if st.button("저장소에 추가", use_container_width=True, disabled=probs is None):
            st.session_state["history"].insert(
                0,
                {
                    "thumb": preview.resize((56, 56), Image.NEAREST),
                    "label": pred_label,
                    "prob": float(probs[pred_label]),
                },
            )
    with clear_col:
        if st.button(
            "저장소 비우기",
            use_container_width=True,
            disabled=not st.session_state["history"],
        ):
            st.session_state["history"] = []

st.subheader("추론 결과 이미지 저장소")
history = st.session_state["history"]
if not history:
    st.caption("저장된 이미지가 없습니다. 예측 후 '저장소에 추가'를 눌러 보세요.")
else:
    # streamlit 1.27.2에는 st.container(height=...)가 없어 스크롤 영역을 만들 수 없습니다.
    # (drawable-canvas 호환 때문에 streamlit을 올릴 수 없어 버전 제약이 있습니다.)
    # 그래서 썸네일을 data URI로 인라인한 HTML을 직접 렌더링해 overflow 스크롤을 구현합니다.
    cards = []
    for item in history:
        cards.append(
            '<figure style="margin:0;text-align:center;">'
            f'<img src="{_thumb_to_data_uri(item["thumb"])}" width="56" height="56" '
            'style="image-rendering:pixelated;display:block;" />'
            '<figcaption style="font-size:0.75rem;color:inherit;opacity:0.85;">'
            f'{item["label"]} ({item["prob"] * 100:.0f}%)</figcaption>'
            "</figure>"
        )

    container_style = (
        f"display:grid;grid-template-columns:repeat({HISTORY_COLUMNS},56px);"
        "gap:12px;justify-content:start;"
    )
    if len(history) >= HISTORY_SCROLL_THRESHOLD:
        container_style += (
            f"max-height:{HISTORY_MAX_HEIGHT_PX}px;overflow-y:auto;padding-right:8px;"
        )

    st.markdown(
        f'<div style="{container_style}">{"".join(cards)}</div>',
        unsafe_allow_html=True,
    )
