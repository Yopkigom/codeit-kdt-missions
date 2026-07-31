"""report.md를 인쇄용 HTML(report.html)로 변환합니다. PDF 발행용입니다.

브라우저에서 report.html을 열고 Ctrl+P → "PDF로 저장"으로 PDF를 만듭니다.
mermaid 다이어그램이 네트워크 없이도 렌더링되도록 mermaid.js를 HTML에 인라인합니다.

실행:
    python make_report_html.py
"""

import html as html_lib
import os
import re
import urllib.request

import markdown

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
MD_PATH = os.path.join(BASE_DIR, "report.md")
HTML_PATH = os.path.join(BASE_DIR, "report.html")
MERMAID_PATH = os.path.join(BASE_DIR, "mermaid.min.js")
MERMAID_URL = "https://cdn.jsdelivr.net/npm/mermaid@10.9.1/dist/mermaid.min.js"

# ```mermaid 블록은 markdown 변환 전에 빼두었다가 나중에 되돌립니다.
MERMAID_BLOCK = re.compile(r"```mermaid\n(.*?)```", re.DOTALL)

PAGE_TEMPLATE = """<!DOCTYPE html>
<html lang="ko">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>__TITLE__</title>
<style>
* { box-sizing: border-box; }
body {
  font-family: "Malgun Gothic", "맑은 고딕", "NanumGothic", "Nanum Gothic",
               "Noto Sans KR", "Apple SD Gothic Neo", sans-serif;
  line-height: 1.75;
  color: #1a1a1a;
  background: #ffffff;
  max-width: 900px;
  margin: 0 auto;
  padding: 40px 32px 80px;
  font-size: 15px;
  word-break: keep-all;
}
h1 { font-size: 1.9em; border-bottom: 3px solid #2b6cb0; padding-bottom: .4em; margin-top: 0; }
h2 { font-size: 1.45em; color: #2b6cb0; border-bottom: 1px solid #cbd5e0;
     padding-bottom: .3em; margin-top: 2em; }
h3 { font-size: 1.18em; margin-top: 1.8em; }
h4 { font-size: 1.03em; margin-top: 1.4em; color: #2d3748; }
h1, h2, h3, h4 { page-break-after: avoid; }
table { border-collapse: collapse; width: 100%; margin: 1em 0; font-size: .94em; }
th, td { border: 1px solid #cbd5e0; padding: .5em .7em; text-align: left; vertical-align: top; }
th { background: #edf2f7; font-weight: 600; }
tr:nth-child(even) td { background: #f7fafc; }
code { background: #edf2f7; padding: .12em .35em; border-radius: 3px;
       font-family: "D2Coding", "Consolas", "Menlo", monospace; font-size: .9em; }
pre { background: #f7fafc; border: 1px solid #e2e8f0; border-radius: 6px;
      padding: 1em; overflow-x: auto; }
pre code { background: none; padding: 0; }
blockquote { border-left: 4px solid #90cdf4; margin: 1em 0; padding: .4em 1em;
             background: #f7fbff; color: #2d3748; }
hr { border: none; border-top: 1px solid #e2e8f0; margin: 2.4em 0; }
a { color: #2b6cb0; }
ul, ol { padding-left: 1.4em; }
li { margin: .3em 0; }
.mermaid { text-align: center; background: #fff; border: 1px solid #e2e8f0;
           border-radius: 6px; padding: 1em; margin: 1.2em 0; }
img { display: block; max-width: 100%; height: auto; margin: 1.2em auto;
      border: 1px solid #e2e8f0; border-radius: 6px; }
table, pre, .mermaid, blockquote, img { page-break-inside: avoid; break-inside: avoid; }
.toc { background: #f7fafc; border: 1px solid #e2e8f0; border-radius: 6px;
       padding: .6em 1.4em 1em; margin: 2em 0; font-size: .94em; }
.toc-title { font-weight: 600; margin: .8em 0 .2em; color: #2b6cb0; }
.toc ul { margin: .2em 0; padding-left: 1.2em; }
.toc > ul { padding-left: 0; list-style: none; }
.print-hint { background: #fffbeb; border: 1px solid #f6e05e; border-radius: 6px;
              padding: .8em 1.2em; margin-bottom: 2em; font-size: .92em; }
@media print {
  body { max-width: none; padding: 0; font-size: 10.5pt; }
  .print-hint { display: none; }
  a { text-decoration: none; color: #1a1a1a; }
  /* 이미지는 페이지를 넘어 이어지지 않는다. 인쇄 영역(A4 269mm)을 넘으면
     아래가 잘리므로 높이를 제한해 세로로 긴 캡처도 한 장에 담는다 */
  img { max-height: 240mm; width: auto; object-fit: contain; }
}
@page { size: A4; margin: 16mm 14mm; }
</style>
</head>
<body>
<div class="print-hint">
  <strong>PDF로 저장하는 방법</strong> — 이 페이지에서 <code>Ctrl+P</code>(Mac은 <code>Cmd+P</code>) →
  대상을 <em>"PDF로 저장"</em> 선택 → 배경 그래픽 옵션을 켜면 표 음영까지 인쇄됩니다.
  다이어그램이 보이지 않으면 1~2초 기다린 뒤 인쇄하세요. (이 안내는 인쇄물에 나오지 않습니다.)
</div>
<div class="toc">
  <div class="toc-title">목차</div>
  __TOC__
</div>
__CONTENT__
<script>__MERMAID_JS__</script>
<script>
  mermaid.initialize({ startOnLoad: true, theme: "neutral", securityLevel: "loose" });
</script>
</body>
</html>
"""


def ensure_mermaid_js() -> str:
    """mermaid.min.js를 확보해 내용을 반환합니다. 없으면 내려받습니다."""
    if not os.path.exists(MERMAID_PATH):
        print(f"  mermaid.min.js 다운로드: {MERMAID_URL}")
        urllib.request.urlretrieve(MERMAID_URL, MERMAID_PATH)
    with open(MERMAID_PATH, encoding="utf-8") as f:
        return f.read()


def extract_mermaid(md_text: str):
    """mermaid 블록을 빼내고 자리표시자로 치환합니다."""
    blocks = []

    def replace(match: re.Match) -> str:
        blocks.append(match.group(1).rstrip())
        return f"\n\nMERMAIDPLACEHOLDER{len(blocks) - 1}\n\n"

    return MERMAID_BLOCK.sub(replace, md_text), blocks


def restore_mermaid(html_text: str, blocks: list) -> str:
    """자리표시자를 mermaid 렌더링 대상 <pre>로 되돌립니다.

    블록 내용을 HTML 이스케이프해야 <br/> 같은 표기가 DOM 요소로 파싱되지 않고
    mermaid에 문자열 그대로 전달됩니다.
    """
    for index, block in enumerate(blocks):
        escaped = html_lib.escape(block)
        html_text = html_text.replace(
            f"<p>MERMAIDPLACEHOLDER{index}</p>",
            f'<pre class="mermaid">{escaped}</pre>',
        )
    return html_text


def main() -> None:
    with open(MD_PATH, encoding="utf-8") as f:
        md_text = f.read()

    # 첫 번째 h1을 문서 제목으로 사용합니다.
    title_match = re.search(r"^#\s+(.+)$", md_text, re.MULTILINE)
    title = title_match.group(1).strip() if title_match else "보고서"

    md_text, mermaid_blocks = extract_mermaid(md_text)

    converter = markdown.Markdown(extensions=["tables", "fenced_code", "toc"])
    body_html = converter.convert(md_text)
    body_html = restore_mermaid(body_html, mermaid_blocks)

    page = (
        PAGE_TEMPLATE.replace("__TITLE__", html_lib.escape(title))
        .replace("__TOC__", converter.toc)
        .replace("__CONTENT__", body_html)
        .replace("__MERMAID_JS__", ensure_mermaid_js())
    )

    with open(HTML_PATH, "w", encoding="utf-8") as f:
        f.write(page)

    print(f"생성 완료: {HTML_PATH}")
    print(f"  제목        : {title}")
    print(f"  mermaid 블록: {len(mermaid_blocks)}개")
    print(f"  파일 크기   : {os.path.getsize(HTML_PATH):,} bytes")


if __name__ == "__main__":
    main()
