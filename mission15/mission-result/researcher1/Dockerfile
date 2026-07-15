# 베이스 이미지로 Python 3.11-slim-bookworm을 사용 (공통)
FROM python:3.11-slim-bookworm

# 작업 디렉토리를 /app으로 설정
WORKDIR /app

# 필요한 패키지 의존성 설정 먼저 복사
COPY requirements.txt .
# requirements.txt 파일을 기반으로 패키지 설치
RUN pip install --no-cache-dir -r requirements.txt

# 필요한 코드 파일과 데이터 디렉토리를 컨테이너로 복사
COPY train.py .
COPY data/ ./data/

# 컨테이너 시작 시 실행 명령 설정 : train.py 파일을 실행
CMD ["python", "train.py"]