using System;
using UnityEngine;

namespace Zap.Constellation
{
    // Local pose response is independent of the slower generated environment.
    public sealed class WorldNavigation
    {
        private Transform _rig;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private float _sceneYaw, _nextCommand;
        private bool _active, _turnLatched, _moving, _stopPending;
        private Zap.World.GeneratedEnvironment _environment;
        private Vector3 _velocity;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public void Enter(Zap.World.GeneratedEnvironment environment = null)
        {
            if (_active) { _environment = environment; _velocity = Vector3.zero; return; }
            _rig = GameObject.Find("XR Origin (XR Rig)")?.transform;
            if (_rig == null || Camera.main == null) return;
            _savedPosition = _rig.position; _savedRotation = _rig.rotation;
            _environment = environment;
            _velocity = Vector3.zero;
            _sceneYaw = Camera.main.transform.eulerAngles.y;
            _active = true; _turnLatched = _moving = _stopPending = false;
            _nextCommand = Time.unscaledTime;
        }

        public void Tick(Vector2 move, Vector2 look, bool canSteer, Action<string,string> steer)
        {
            if (!_active || _rig == null || Camera.main == null) return;
            var head = Camera.main.transform;
            if (Mathf.Abs(look.x) < .3f) _turnLatched = false;
            if (canSteer && Mathf.Abs(look.x) > .65f && !_turnLatched)
            {
                _rig.RotateAround(head.position, Vector3.up, Mathf.Sign(look.x) * 30);
                _turnLatched = true;
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .04f);
            float magnitude = Mathf.Clamp01((move.magnitude - .15f) / .85f);
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = _rig.forward;
            Vector3 desired = canSteer ? (forward * move.y + Vector3.Cross(Vector3.up, forward) * move.x).normalized * magnitude : Vector3.zero;
            _velocity = canSteer ? Vector3.MoveTowards(_velocity, desired, dt * (magnitude > .01f ? 3.5f : 8)) : Vector3.zero;
            bool moving = _velocity.sqrMagnitude > .0001f;
            if (_moving && !moving) _stopPending = true;
            if (moving)
            {
                Vector3 direction = _velocity.normalized;
                Vector3 feet = new Vector3(head.position.x, _rig.position.y, head.position.z);
                Vector3 step = Slide(feet, head.position, _velocity * dt);
                if (_environment != null)
                {
                    bool grounded = _environment.SurfaceBelow(feet + step + Vector3.up * .3f, .65f, out var ground);
                    // Ignore tiny reconstruction gaps without stepping over a real ledge.
                    if (!grounded)
                        foreach (var offset in new[] { Vector3.right * .08f, Vector3.left * .08f, Vector3.forward * .08f, Vector3.back * .08f })
                            if (_environment.SurfaceBelow(feet + step + offset + Vector3.up * .3f, .65f, out ground)) { grounded = true; break; }
                    if (!grounded || Mathf.Abs(ground.point.y - feet.y) > .23f) step = Vector3.zero;
                    else if (Mathf.Abs(ground.point.y - feet.y) > .015f)
                        step.y = Mathf.Clamp(ground.point.y - feet.y, -.45f * dt, .45f * dt);
                }
                _rig.position += step;
                if (_environment == null && Time.unscaledTime >= _nextCommand)
                {
                    float bearing = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg - _sceneYaw + 360, 360);
                    string[] directions = { "front", "front-right", "right", "back-right", "back", "back-left", "left", "front-left" };
                    string heading = directions[Mathf.RoundToInt(bearing / 45) % 8];
                    steer("move", "Take one slow short step toward the " + heading +
                        " of the established panorama, then settle. Translate the viewpoint at eye level; keep the panorama orientation and horizon fixed. No zoom or rotation.");
                    _nextCommand = Time.unscaledTime + 2.8f; _stopPending = false;
                }
            }
            else if (_environment == null && _stopPending && canSteer && Time.unscaledTime >= _nextCommand - 1.8f)
            {
                steer("move", "Stop all camera translation now. Hold this eye-level viewpoint perfectly stationary while the subjects and surroundings continue living.");
                _stopPending = false; _nextCommand = Time.unscaledTime + 1;
            }
            _moving = moving;
        }

        // Move to contact, then slide along walls instead of discarding the entire step.
        public Vector3 Slide(Vector3 feet, Vector3 head, Vector3 delta)
        {
            Vector3 travelled = Vector3.zero, remaining = delta;
            for (int iteration = 0; iteration < 3 && remaining.sqrMagnitude > .0000001f; iteration++)
            {
                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;
                Vector3 bottom = feet + travelled + Vector3.up * .28f;
                Vector3 top = new Vector3(head.x, Mathf.Max(head.y - .15f, feet.y + .5f), head.z) + travelled;
                int count = Physics.CapsuleCastNonAlloc(bottom, top, .17f, direction, _hits, distance + .02f, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity; Vector3 normal = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    var hit = _hits[i];
                    if (hit.collider.GetComponentInParent<Zap.World.GeneratedEnvironment>() != _environment || _environment == null ||
                        hit.normal.y > .65f || Vector3.Dot(direction, hit.normal) >= -.01f) continue;
                    if (hit.distance < nearest) { nearest = hit.distance; normal = hit.normal; }
                }
                if (float.IsPositiveInfinity(nearest)) { travelled += remaining; break; }
                float advance = Mathf.Clamp(nearest - .02f, 0, distance);
                travelled += direction * advance;
                remaining -= direction * advance;
                normal.y = 0; normal.Normalize();
                remaining = Vector3.ProjectOnPlane(remaining, normal); remaining.y = 0;
            }
            return travelled;
        }

        public void Exit()
        {
            if (_active && _rig != null) _rig.SetPositionAndRotation(_savedPosition, _savedRotation);
            _active = false; _moving = _stopPending = false;
            _environment = null; _velocity = Vector3.zero;
        }
    }
}
