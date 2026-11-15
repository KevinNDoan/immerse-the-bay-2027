"""Settings from the environment and relay/.env."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv

ENV_PATH = Path(__file__).resolve().parent.parent / ".env"
DEFAULT_MODEL = "reactor/visko-orbis-stable"
DEFAULT_PORT = 8787
SETTING_NAMES = ("ORBIS_MODE", "REACTOR_API_KEY", "ORBIS_MODEL", "RELAY_PORT")


@dataclass(frozen=True)
class Settings:
    mode: str
    api_key: str
    model: str
    port: int
    max_width: int = 1280
    resolution: str = ""
    speech_model: str = "base.en"
    openai_key: str = ""
    node_model: str = "gpt-6-luna"
    panorama_model: str = "gpt-image-2"
    worldlabs_key: str = ""
    worldlabs_model: str = "marble-1.1"
    world_provider: str = "auto"
    world_generation_enabled: bool = False
    world_test_world_id: str = ""
    world_render_mode: str = "mesh"
    world_splat_quality: str = "500k"

    def __post_init__(self):
        if self.world_render_mode not in ("mesh", "splats"):
            raise ValueError("WORLD_RENDER_MODE must be mesh or splats.")
        if self.world_splat_quality not in ("100k", "150k", "500k"):
            raise ValueError("WORLD_SPLAT_QUALITY must be 100k, 150k or 500k.")

    def __repr__(self) -> str:
        return f"Settings(mode={self.mode!r}, model={self.model!r}, port={self.port})"


def load_settings(env_file: Path | None = None) -> Settings:
    """Read settings. Refuse live mode when REACTOR_API_KEY is empty."""
    path = ENV_PATH if env_file is None else env_file
    if env_file is None:
        load_dotenv(ENV_PATH.parent.parent / ".env", override=False)
    load_dotenv(path, override=False)

    mode = _text("ORBIS_MODE", "standin")
    api_key = _text("REACTOR_API_KEY", "")
    model = _text("ORBIS_MODEL", DEFAULT_MODEL)
    port = _port(_text("RELAY_PORT", str(DEFAULT_PORT)))

    if mode not in ("standin", "live"):
        raise SystemExit("ORBIS_MODE must be standin or live")
    if mode == "live" and api_key == "":
        raise SystemExit("REACTOR_API_KEY is required when ORBIS_MODE=live")
    try:
        max_width = int(_text("RELAY_MAX_WIDTH", "1280"))
        if not 320 <= max_width <= 1920:
            raise ValueError()
    except ValueError:
        raise SystemExit("RELAY_MAX_WIDTH must be between 320 and 1920") from None
    return Settings(mode=mode, api_key=api_key, model=model, port=port,
                    max_width=max_width, resolution=_text("ORBIS_RESOLUTION", ""),
                    speech_model=_text("SPEECH_MODEL", "base.en"),
                    openai_key=_text("OPENAI_API_KEY", ""),
                    node_model=_text("NODE_MODEL", "gpt-6-luna"),
                    panorama_model=_text("PANORAMA_MODEL", "gpt-image-2", empty_is_default=False),
                    worldlabs_key=_text("WORLD_LABS_API_KEY", ""),
                    worldlabs_model=_text("WORLD_LABS_MODEL", "marble-1.1"),
                    world_provider=_text("WORLD_PROVIDER", "auto"),
                    world_generation_enabled=_text("WORLD_GENERATION_ENABLED", "false").lower() == "true",
                    world_test_world_id=_text("WORLD_TEST_WORLD_ID", ""),
                    world_render_mode=_text("WORLD_RENDER_MODE", "mesh"),
                    world_splat_quality=_text("WORLD_SPLAT_QUALITY", "500k"))


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
