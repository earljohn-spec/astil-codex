"""Dependency-free structural validation for the UI prototype."""

from __future__ import annotations

from collections import Counter
from html.parser import HTMLParser
from pathlib import Path


class PrototypeParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.ids: list[str] = []
        self.external_dependencies: list[str] = []
        self.inline_scripts = 0

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = dict(attrs)
        if identifier := values.get("id"):
            self.ids.append(identifier)
        if tag == "script":
            if source := values.get("src"):
                self.external_dependencies.append(source)
            else:
                self.inline_scripts += 1
        if tag == "link" and (href := values.get("href")):
            self.external_dependencies.append(href)


def main() -> None:
    path = Path(__file__).with_name("index.html")
    parser = PrototypeParser()
    parser.feed(path.read_text(encoding="utf-8"))
    parser.close()

    duplicate_ids = sorted(name for name, count in Counter(parser.ids).items() if count > 1)
    required_ids = {
        "avatar",
        "messages",
        "messageInput",
        "sendButton",
        "micButton",
        "policyButton",
        "actionCard",
        "approveAction",
        "emergencyStop",
    }
    missing_ids = sorted(required_ids.difference(parser.ids))

    if duplicate_ids:
        raise SystemExit(f"Duplicate element IDs: {', '.join(duplicate_ids)}")
    if missing_ids:
        raise SystemExit(f"Missing required element IDs: {', '.join(missing_ids)}")
    if parser.external_dependencies:
        raise SystemExit(
            "Prototype must remain offline; external dependencies found: "
            + ", ".join(parser.external_dependencies)
        )
    if parser.inline_scripts != 1:
        raise SystemExit(f"Expected one inline script, found {parser.inline_scripts}")

    print(
        f"UI prototype valid: {len(parser.ids)} IDs, "
        f"{parser.inline_scripts} inline script, no external dependencies"
    )


if __name__ == "__main__":
    main()
