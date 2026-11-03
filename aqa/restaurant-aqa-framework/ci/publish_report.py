"""Publish a small TRX-derived report without copying raw logs or browser traces."""

import html
import json
import re
import sys
from pathlib import Path
from xml.etree import ElementTree


TOKEN = re.compile(r"(?:eyJ[A-Za-z0-9_-]{12,}\.)[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}|\b(?:AKIA|ASIA)[A-Z0-9]{16}\b")


def safe_name(value):
    # Names come from source-defined scenarios. A dynamic token is never published.
    return "[redacted test name]" if TOKEN.search(value) else value[:300]


def failure_kind(result):
    output = " ".join(result.itertext()).lower()
    if "err_name_not_resolved" in output:
        kind = "DNS resolution"
    elif any(word in output for word in ("etimedout", "resource temporarily unavailable", "connection refused")):
        kind = "API connection"
    elif "timeout" in output or "timed out" in output:
        kind = "Browser or request timeout"
    elif "assert" in output:
        kind = "Assertion"
    else:
        kind = "Test or setup error"
    for component in ("waiterpreorderpage", "openwaitergrouporder", "guestscompletegroupjourney", "inviteguestthroughui"):
        line = re.search(rf"\b{component}\.cs:line\s+(\d+)", output)
        if line:
            return f"{kind} ({component}.cs:{line.group(1)})"
        if component in output:
            return f"{kind} ({component})"
    return kind


def main(source, destination, expected):
    destination.mkdir(parents=True, exist_ok=True)
    files = sorted(source.glob("*.trx"), key=lambda path: path.stat().st_mtime, reverse=True)
    if not files:
        rows = []
        note = "No TRX was generated. Check the GitHub Actions job setup and test steps."
        ok = False
    else:
        root = ElementTree.parse(files[0]).getroot()
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        rows = []
        for result in root.findall(".//t:Results/t:UnitTestResult", ns):
            outcome = result.get("outcome", "Unknown")
            rows.append({
                "name": safe_name(result.get("testName", "Unnamed test")),
                "outcome": outcome if outcome in {"Passed", "Failed", "NotExecuted"} else "Unknown",
                "duration": result.get("duration", ""),
                "failure": failure_kind(result) if outcome == "Failed" else "",
            })
        note = "Details stay in the private runner log; browser traces and raw reports are not uploaded."
        ok = len(rows) == expected and all(row["outcome"] == "Passed" for row in rows)
    counts = {status: sum(row["outcome"] == status for row in rows)
              for status in ("Passed", "Failed", "NotExecuted", "Unknown")}
    report = {"total": len(rows), "counts": counts, "note": note, "tests": rows}
    (destination / "summary.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")

    body = ["<!doctype html><html lang='en'><meta charset='utf-8'>",
            "<title>AQA group pre-order results</title>",
            "<style>body{font:16px system-ui;max-width:1000px;margin:2rem auto;padding:0 1rem}"
            "table{border-collapse:collapse;width:100%}th,td{padding:.5rem;text-align:left;"
            "border-bottom:1px solid #ddd}.Failed{color:#b00020}.Passed{color:#087632}</style>",
            "<h1>Group pre-order AQA</h1>",
            f"<p>Total {len(rows)} · Passed {counts['Passed']} · Failed {counts['Failed']} "
            f"· Not executed {counts['NotExecuted']} · Unknown {counts['Unknown']}</p>",
            f"<p>{html.escape(note)}</p>",
            "<table><thead><tr><th>Test</th><th>Outcome</th><th>Duration</th><th>Failure category</th></tr></thead><tbody>"]
    for row in rows:
        cells = "".join(f"<td>{html.escape(str(row[key]))}</td>"
                        for key in ("name", "outcome", "duration", "failure"))
        body.append(f"<tr class='{row['outcome']}'>{cells}</tr>")
    body.append("</tbody></table></html>")
    (destination / "index.html").write_text("\n".join(body) + "\n")
    print(f"Publishable AQA report: {len(rows)} tests, {counts['Passed']} passed, {counts['Failed']} failed")
    return 0 if ok else 1


if __name__ == "__main__":
    if len(sys.argv) not in (3, 4):
        raise SystemExit("Usage: publish_report.py <TestResults> <publish directory> [expected count]")
    expected_count = int(sys.argv[3]) if len(sys.argv) == 4 else 45
    if expected_count < 1:
        raise SystemExit("Expected count must be positive")
    raise SystemExit(main(Path(sys.argv[1]), Path(sys.argv[2]), expected_count))
