"""보고서용 화면 캡처 (K-b-1 · K-b-2).

백엔드(8000)와 Streamlit(8501)이 떠 있는 상태에서 실행한다.
Streamlit은 WebSocket으로 그려지므로 렌더 대기 시간을 넉넉히 준다.

실행:
    python capture.py
"""
from __future__ import annotations

import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

BASE_DIR = Path(__file__).resolve().parent
OUT_DIR = BASE_DIR / "captures"
FRONT = "http://localhost:8501"
API = "http://localhost:8080"

VIEWPORT = {"width": 1440, "height": 960}
RENDER_WAIT_MS = 4000


def shoot(page, url: str, name: str, *, wait: int = RENDER_WAIT_MS,
          full_page: bool = True, before=None) -> None:
    page.goto(url, wait_until="networkidle", timeout=60000)
    page.wait_for_timeout(wait)
    if before is not None:
        before(page)
        page.wait_for_timeout(2000)
    path = OUT_DIR / f"{name}.png"
    page.screenshot(path=str(path), full_page=full_page)
    print(f"  저장: {path.name}")


def click_sidebar(page, label: str) -> None:
    page.locator('[data-testid="stSidebar"]').get_by_text(label, exact=True).first.click()
    page.wait_for_timeout(RENDER_WAIT_MS)


def main() -> int:
    OUT_DIR.mkdir(exist_ok=True)

    with sync_playwright() as p:
        browser = p.chromium.launch()
        page = browser.new_page(viewport=VIEWPORT, device_scale_factor=2)

        print("프론트엔드 캡처")
        shoot(page, FRONT, "01_영화목록")

        movie_id = 1
        shoot(page, f"{FRONT}/?movie_id={movie_id}", "02_영화상세_리뷰1페이지")

        # 2페이지로 이동해 페이지네이션 동작을 남긴다.
        # Streamlit 버튼 라벨은 '다음 ›' 이므로 부분 일치로 찾는다
        def next_page(pg):
            pg.locator("button", has_text="다음").first.scroll_into_view_if_needed()
            pg.locator("button", has_text="다음").first.click()

        shoot(page, f"{FRONT}/?movie_id={movie_id}", "03_영화상세_리뷰2페이지",
              before=next_page)

        page.goto(FRONT, wait_until="networkidle")
        page.wait_for_timeout(RENDER_WAIT_MS)
        click_sidebar(page, "영화 추가")
        page.screenshot(path=str(OUT_DIR / "04_영화추가.png"), full_page=True)
        print("  저장: 04_영화추가.png")

        click_sidebar(page, "리뷰 등록")
        page.screenshot(path=str(OUT_DIR / "05_리뷰등록_폼.png"), full_page=True)
        print("  저장: 05_리뷰등록_폼.png")

        # 리뷰를 실제로 등록해 감성 분석 결과 카드를 남긴다
        page.get_by_label("작성자").fill("신호정")
        page.get_by_label("제목").fill("연출이 정말 좋았다")
        page.get_by_label("내용").fill(
            "계단 구도만으로 계급을 설명하는 장면이 인상적이었고 배우들의 연기도 훌륭했다."
        )
        page.get_by_role("button", name="등록").first.click()
        page.wait_for_timeout(6000)
        page.screenshot(path=str(OUT_DIR / "06_리뷰등록_감성결과.png"), full_page=True)
        print("  저장: 06_리뷰등록_감성결과.png")

        click_sidebar(page, "최근 리뷰")
        page.screenshot(path=str(OUT_DIR / "07_최근리뷰.png"), full_page=True)
        print("  저장: 07_최근리뷰.png")

        print("FastAPI Docs 캡처")
        page.goto(f"{API}/docs", wait_until="networkidle", timeout=60000)
        page.wait_for_timeout(3000)
        page.screenshot(path=str(OUT_DIR / "08_fastapi_docs_전체.png"), full_page=True)
        print("  저장: 08_fastapi_docs_전체.png")

        # 대표 엔드포인트 하나를 펼쳐 스키마까지 보이게 한다
        page.get_by_role("button", name="post /reviews").first.click()
        page.wait_for_timeout(1500)
        page.screenshot(path=str(OUT_DIR / "09_fastapi_docs_리뷰등록.png"), full_page=True)
        print("  저장: 09_fastapi_docs_리뷰등록.png")

        browser.close()

    print(f"\n캡처 {len(list(OUT_DIR.glob('*.png')))}건 → {OUT_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
