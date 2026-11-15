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
    routes = create_app(settings).routes
    paths = {route.path for route in routes}
    expected = {
        "/health",
        "/",
        "/stream.mjpg",
        "/world/kill",
        "/ws/world",
        "/capabilities",
        "/speech/transcribe",
        "/nodes/update",
        "/world/prepare",
        "/world/jobs/{key}",
        "/world/preview",
        "/world/assets/{key}/{filename}",
        "/world/study",
        "/world/explore",
    }
    assert expected <= paths
    for path in expected:
        assert sum(route.path == path for route in routes) == 1, path
    transport_paths = {"/health", "/", "/stream.mjpg", "/world/kill", "/ws/world"}
    for route in routes:
        if route.path in expected:
            module = "zap_relay.world" if route.path in transport_paths else "zap_relay.app"
            assert route.endpoint.__module__ == module, route.path
