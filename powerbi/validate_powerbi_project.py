#!/usr/bin/env python3
"""Validate Power BI project files against the published schema expectations."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Iterable

_DOCS_URL = "https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-overview"
_ALLOWED_PBISM_KEYS = {"$schema", "version", "settings"}
_ALLOWED_SETTINGS_KEYS = {"qnaEnabled", "qnaLsdlSharingPermissions"}


def _find_definition_files(project_root: Path) -> Iterable[Path]:
    """Yield every definition.pbism file under the given project root."""
    return project_root.glob("**/definition.pbism")


def _validate_pbism_file(pbism_path: Path) -> list[str]:
    """Validate a single .pbism file and return the discovered issues."""
    issues: list[str] = []

    try:
        data = json.loads(pbism_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:  # pragma: no cover - defensive guard
        issues.append(f"{pbism_path}: invalid JSON ({exc})")
        return issues

    unexpected_keys = set(data.keys()) - _ALLOWED_PBISM_KEYS
    if unexpected_keys:
        issues.append(
            f"{pbism_path}: unsupported top-level properties: {sorted(unexpected_keys)}"
        )

    settings = data.get("settings")
    if settings is not None:
        if not isinstance(settings, dict):
            issues.append(f"{pbism_path}: `settings` must be an object when present")
        else:
            unexpected_setting_keys = set(settings.keys()) - _ALLOWED_SETTINGS_KEYS
            if unexpected_setting_keys:
                issues.append(
                    f"{pbism_path}: unsupported settings: {sorted(unexpected_setting_keys)}"
                )

    return issues


def validate_project(project_root: Path) -> int:
    """Validate all semantic model definition files contained in the project root."""
    pbism_files = list(_find_definition_files(project_root))
    if not pbism_files:
        print(f"No definition.pbism files found under {project_root}", file=sys.stderr)
        return 1

    errors: list[str] = []
    for pbism_path in pbism_files:
        errors.extend(_validate_pbism_file(pbism_path))

    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        print(
            "\nRefer to the Power BI project schema guidance for supported properties: "
            f"{_DOCS_URL}",
            file=sys.stderr,
        )
        return 1

    return 0


def _build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Validate Power BI project semantic model definitions",
    )
    parser.add_argument(
        "project_root",
        type=Path,
        nargs="?",
        default=Path("powerbi"),
        help="Directory that contains one or more Power BI project folders",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _build_arg_parser()
    args = parser.parse_args(argv)
    project_root: Path = args.project_root
    return validate_project(project_root)


if __name__ == "__main__":  # pragma: no cover - CLI entry point
    sys.exit(main())
