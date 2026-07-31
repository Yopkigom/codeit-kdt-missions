"""제출용 zip을 만듭니다.

Info-ZIP의 zip 명령은 한글 파일명을 UTF-8 바이트 그대로 저장하면서
UTF-8 플래그(general purpose bit 11)를 세우지 않습니다. Windows 탐색기는
플래그가 없으면 이름을 CP949로 해석하다 실패해 "압축 폴더가 올바르지 않습니다"를
띄웁니다. 표준 라이브러리 zipfile은 비ASCII 이름에 플래그를 자동으로 세우므로
여기서는 zipfile로 직접 씁니다.

실행:
    python make_submission_zip.py
"""
from __future__ import annotations

import zipfile
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent
SRC_DIR = BASE_DIR / "3팀_신호정_미션18"
ZIP_PATH = BASE_DIR / "3팀_신호정_미션18.zip"

# 제외 대상. 시크릿과 캐시, 그리고 용량 때문에 뺀 모델 가중치입니다.
EXCLUDED_DIRS = {"__pycache__", ".pytest_cache", ".git"}
EXCLUDED_NAMES = {".env"}
EXCLUDED_SUFFIXES = {".pyc", ".onnx"}


def is_excluded(path: Path) -> bool:
    rel = path.relative_to(SRC_DIR)
    if EXCLUDED_DIRS.intersection(rel.parts):
        return True
    if path.name in EXCLUDED_NAMES:
        return True
    if path.suffix in EXCLUDED_SUFFIXES:
        return True
    if path.name.endswith(".onnx.data"):        # external data 파일
        return True
    if rel.parts[:2] == ("backend", "data") and path.suffix == ".json":
        return True                              # 수집 중간 산출물
    return False


def main() -> int:
    if not SRC_DIR.is_dir():
        print(f"원본 폴더가 없습니다: {SRC_DIR}")
        return 1

    files = sorted(p for p in SRC_DIR.rglob("*") if p.is_file() and not is_excluded(p))

    if ZIP_PATH.exists():
        ZIP_PATH.unlink()

    with zipfile.ZipFile(ZIP_PATH, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for path in files:
            zf.write(path, arcname=str(path.relative_to(SRC_DIR.parent)))

    with zipfile.ZipFile(ZIP_PATH) as zf:
        entries = zf.infolist()
        flagged = sum(1 for i in entries if i.flag_bits & 0x800)
        bad = zf.testzip()

    size_mb = ZIP_PATH.stat().st_size / 1024 / 1024
    print(f"생성 완료: {ZIP_PATH.name}  ({size_mb:.1f} MB, {len(entries)}개 파일)")
    print(f"UTF-8 파일명 플래그: {flagged}/{len(entries)}")
    print(f"무결성 검사: {'실패 ' + bad if bad else '통과'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
