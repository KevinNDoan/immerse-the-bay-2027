"""Settings from the environment and relay/.env."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv

ENV_PATH = Path(__file__).resolve().parent.parent / ".env"
DEFAULT_MODEL = "reactor/visko-orbis-stable"
DEFAULT_PORT = 8787


@dataclass(frozen=True)
class Settings:
    mode: str
    api_key: str
    model: str
    port: int

    def __repr__(self) -> str:
        return f"Settings(mode={self.mode!r}, model={self.model!r}, port={self.port})"


def load_settings(env_file: Path | None = None) -> Settings:
    """Read settings. Refuse live mode when REACTOR_API_KEY is empty."""
    path = ENV_PATH if env_file is None else env_file
    load_dotenv(path, override=False)

    mode = _text("ORBIS_MODE", "standin")
    api_key = _text("REACTOR_API_KEY", "")
    model = _text("ORBIS_MODEL", DEFAULT_MODEL)
    port = _port(_text("RELAY_PORT", str(DEFAULT_PORT)))

    if mode not in ("standin", "live"):
        raise SystemExit("ORBIS_MODE must be standin or live")
    if mode == "live" and api_key == "":
        raise SystemExit("REACTOR_API_KEY is required when ORBIS_MODE=live")
    return Settings(mode=mode, api_key=api_key, model=model, port=port)


def _text(name: str, default: str, *, empty_is_default: bool = True) -> str:
    value = os.environ.get(name)
    if value is None:
        return default
    stripped = value.strip()
    if stripped == "" and empty_is_default:
        return default
    return stripped


def _port(value: str) -> int:
    try:
        port = int(value)
    except ValueError:
        raise SystemExit("RELAY_PORT must be an integer") from None
    if port <= 0 or port > 65535:
        raise SystemExit("RELAY_PORT must be an integer")
    return port
