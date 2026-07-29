"""report.html을 PDF로 변환한다 (제출물).

mermaid는 브라우저에서 그려지므로 SVG 렌더가 끝난 뒤에 인쇄해야 한다.
Chrome의 --print-to-pdf는 렌더 완료를 기다려 주지 않아 Playwright를 쓴다.

실행:
    python make_report_pdf.py
"""
from __future__ import annotations

import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

BASE_DIR = Path(__file__).resolve().parent
HTML_PATH = BASE_DIR / "report.html"
PDF_PATH = BASE_DIR / "3팀_신호정_미션18" / "3팀_신호정_미션18_보고서.pdf"
EXPECTED_DIAGRAMS = 5


def main() -> int:
    if not HTML_PATH.exists():
        print(f"report.html이 없습니다. make_report_html.py를 먼저 실행하세요.", file=sys.stderr)
        return 1

    PDF_PATH.parent.mkdir(parents=True, exist_ok=True)

    with sync_playwright() as p:
        browser = p.chromium.launch()
        page = browser.new_page()
        page.goto(HTML_PATH.as_uri(), wait_until="networkidle", timeout=120000)

        # mermaid 다이어그램이 모두 SVG로 바뀔 때까지 기다린다
        page.wait_for_function(
            f"document.querySelectorAll('.mermaid svg').length >= {EXPECTED_DIAGRAMS}",
            timeout=60000,
        )
        page.wait_for_timeout(2000)

        rendered = page.evaluate("document.querySelectorAll('.mermaid svg').length")
        images = page.evaluate(
            "Array.from(document.images).filter(i => !i.complete || i.naturalWidth === 0).length"
        )
        print(f"mermaid 렌더: {rendered}개 / 로드 실패 이미지: {images}개")

        page.pdf(
            path=str(PDF_PATH),
            format="A4",
            print_background=True,
            margin={"top": "14mm", "bottom": "14mm", "left": "12mm", "right": "12mm"},
        )
        browser.close()

    size_mb = PDF_PATH.stat().st_size / 1024 / 1024
    print(f"생성 완료: {PDF_PATH}  ({size_mb:.1f} MB)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
