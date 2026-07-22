# 미션 15와 동일한 검증된 베이스 이미지 사용.
FROM python:3.11-slim-bookworm

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    MODEL_CACHE_DIR=/app/.model_cache

WORKDIR /app

# onnxruntime는 OpenMP(libgomp1)를 요구한다. Pillow 전용이라 libGL 등은 불필요.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgomp1 \
    && rm -rf /var/lib/apt/lists/*

# 의존성을 먼저 설치해 레이어 캐시를 활용한다.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# 애플리케이션 소스만 복사한다(모델은 런타임에 다운로드).
COPY app.py model_utils.py preprocess.py ./

EXPOSE 8501

# streamlit 헬스 엔드포인트로 컨테이너 상태를 점검한다.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD python -c "import urllib.request,sys; sys.exit(0 if urllib.request.urlopen('http://localhost:8501/_stcore/health').read()==b'ok' else 1)"

ENTRYPOINT ["streamlit", "run", "app.py", \
    "--server.address=0.0.0.0", "--server.port=8501", "--server.headless=true"]
