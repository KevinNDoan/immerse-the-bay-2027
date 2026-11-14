from __future__ import annotations

from pathlib import Path

import pytest

from zap_relay.config import load_settings


def test_live_mode_requires_reactor_api_key(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    monkeypatch.setenv("ORBIS_MODE", "live")
    monkeypatch.setenv("REACTOR_API_KEY", "")
    with pytest.raises(SystemExit, match="REACTOR_API_KEY"):
        load_settings(tmp_path / "missing.env")
