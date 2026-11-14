from zap_relay.app import World, create_app
from zap_relay.config import Settings
from zap_relay.world import World as WorldModule


def test_app_still_exports_world_and_create_app() -> None:
    assert callable(create_app)
    assert World is WorldModule


def test_world_routes_are_registered() -> None:
    settings = Settings(
        mode="standin",
        api_key="",
        model="reactor/visko-orbis-stable",
        port=8787,
    )
    paths = {route.path for route in create_app(settings).routes}
    expected = {
        "/health",
        "/",
        "/stream.mjpg",
        "/world/kill",
        "/ws/world",
        "/capabilities",
        "/speech/transcribe",
        "/nodes/update",
    }
    assert expected <= paths
