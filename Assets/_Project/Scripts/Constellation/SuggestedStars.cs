using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Zap.Core;

namespace Zap.Constellation
{
    public sealed class SuggestedStars : MonoBehaviour
    {
        private sealed class Star
        {
            public GameObject Root;
            public Transform Orb;
            public SphereCollider Collider;
            public XRSimpleInteractable Interactable;
            public TextMeshPro Label;
            public FrostPanel Glass;
            public string GlassText;
            public Bounds GlassBounds;
            public string Topic;
        }

        private readonly Star[] _stars = new Star[SuggestionPicker.MaxSuggestions];
        private ConstellationController _controller;
        private Transform _content;
        private Material _material;
        private TopicBall _cachedSource;
        private string[] _cachedSuggestions;
        private string[] _topics = System.Array.Empty<string>();
        private int _cachedBallCount = -1;
        private float _animationStarted;
        private bool _initialized;
        private bool _suppressed;
        private TopicBall _suppressedSource;
        private string[] _suppressedSuggestions;

        public void Initialize(ConstellationController controller, Transform content)
        {
            _controller = controller;
            _content = content;
            _material = new Material(Shader.Find("Zap/Solid"));
            _material.SetColor("_BaseColor", ZapTheme.Current.suggestedColor);
            for (int i = 0; i < _stars.Length; i++) _stars[i] = CreateStar(i);
            _initialized = true;
        }

        private Star CreateStar(int index)
        {
            var root = new GameObject("Suggested star");
            root.transform.SetParent(_content, false);
            var collider = root.AddComponent<SphereCollider>();
            var interactable = root.AddComponent<XRSimpleInteractable>();

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Suggested orb";
            orb.transform.SetParent(root.transform, false);
            var orbCollider = orb.GetComponent<Collider>();
            orbCollider.enabled = false;
            Destroy(orbCollider);
            orb.GetComponent<Renderer>().sharedMaterial = _material;

            var labelObject = new GameObject("Suggested topic");
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Top;
            label.rectTransform.pivot = new Vector2(.5f, 1f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = false;

            var star = new Star
            {
                Root = root,
                Orb = orb.transform,
                Collider = collider,
                Interactable = interactable,
                Label = label,
                // A small frosted pill behind the label when the theme turns glass on.
                Glass = FrostPanel.Create(labelObject.transform, "Label glass", new Vector3(0f, 0f, .002f))
            };
            interactable.selectEntered.AddListener(_ => Keep(index));
            root.SetActive(false);
            return star;
        }

        private void Update()
        {
            if (!_initialized || _controller == null) return;

            var source = _controller.Current;
            var camera = Camera.main;
            if (source == null || source.Data == null || source.Grab == null || source.Grab.isSelected
                || _controller.InWorld || _controller.Busy || _controller.IsListening || camera == null)
            {
                HideAll();
                return;
            }

            var suggestions = source.Data.suggestions;
            if (_suppressed)
            {
                if (source == _suppressedSource && ReferenceEquals(suggestions, _suppressedSuggestions))
                {
                    HideAll();
                    return;
                }
                _suppressed = false;
            }

            int ballCount = _controller.Balls.Count;
            if (source != _cachedSource || !ReferenceEquals(suggestions, _cachedSuggestions) || ballCount != _cachedBallCount)
                Recompute(source, suggestions, ballCount);

            if (_topics.Length == 0)
            {
                HideAll();
                return;
            }

            Layout(source, camera.transform);
        }

        private void Recompute(TopicBall source, string[] suggestions, int ballCount)
        {
            _cachedSource = source;
            _cachedSuggestions = suggestions;
            _cachedBallCount = ballCount;
            _topics = SuggestionPicker.Pick(source.Data, _controller.Balls.Select(ball => ball.Data));
            _animationStarted = Time.unscaledTime;

            for (int i = 0; i < _stars.Length; i++)
            {
                bool visible = i < _topics.Length;
                var star = _stars[i];
                if (visible)
                {
                    star.Topic = _topics[i];
                    star.Label.text = star.Topic;
                    star.Root.transform.localScale = Vector3.zero;
                }
                if (star.Root.activeSelf != visible) star.Root.SetActive(visible);
            }
            IgnoreRealNodeCollisions();
        }

        private void Layout(TopicBall source, Transform camera)
        {
            var theme = ZapTheme.Current;
            _material.SetColor("_BaseColor", theme.suggestedColor);

            Vector3 toCamera = camera.position - source.transform.position;
            if (toCamera.sqrMagnitude < .000001f) toCamera = camera.forward;
            toCamera.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, toCamera);
            if (right.sqrMagnitude < .000001f) right = Vector3.right;
            else right.Normalize();
            Vector3 up = Vector3.Cross(toCamera, right);

            float elapsed = Time.unscaledTime - _animationStarted;
            for (int i = 0; i < _topics.Length; i++)
            {
                var star = _stars[i];
                if (!star.Root.activeSelf)
                {
                    star.Root.SetActive(true);
                    IgnoreRealNodeCollisions(star.Collider);
                }

                float angle = AngleFor(_topics.Length, i) * Mathf.Deg2Rad;
                star.Root.transform.position = source.transform.position
                    + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * theme.suggestedDistance;

                star.Orb.localScale = Vector3.one * theme.suggestedNodeDiameter;
                star.Collider.radius = Mathf.Max(.045f, .75f * theme.suggestedNodeDiameter);
                star.Label.fontSize = theme.suggestedTextSize;
                star.Label.color = star.Glass != null ? theme.glassDimTextColor : theme.suggestedTextColor;
                if (theme.font != null) star.Label.font = theme.font;
                star.Label.rectTransform.sizeDelta = new Vector2(.3f, .08f);
                star.Label.transform.localPosition = Vector3.down * (theme.suggestedNodeDiameter * .75f);
                star.Label.transform.rotation = Quaternion.LookRotation(star.Label.transform.position - camera.position);
                if (star.Glass != null)
                {
                    if (star.GlassText != star.Label.text)
                    {
                        star.Label.ForceMeshUpdate();
                        star.GlassBounds = star.Label.textInfo.characterCount > 0 ? star.Label.textBounds : new Bounds();
                        star.GlassText = star.Label.text;
                    }
                    // A slim pill: half the note's glass padding around the label text.
                    float pad = theme.glassPadding * .5f;
                    var b = star.GlassBounds;
                    star.Glass.SetSize(new Vector2(b.size.x + pad * 4f, b.size.y + pad * 2f));
                    star.Glass.transform.localPosition = new Vector3(b.center.x, b.center.y, .002f);
                }

                float revealTime = elapsed - i * .12f;
                float scale = revealTime <= 0 ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(revealTime / .35f));
                if (revealTime > .35f)
                    scale *= 1 + .08f * Mathf.Sin((revealTime - .35f) * Mathf.PI + i * Mathf.PI * 2 / 3);
                if (star.Interactable.isHovered) scale *= 1.2f;
                star.Root.transform.localScale = Vector3.one * scale;
            }
        }

        private void IgnoreRealNodeCollisions()
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                var star = _stars[i];
                if (star != null) IgnoreRealNodeCollisions(star.Collider);
            }
        }

        private void IgnoreRealNodeCollisions(SphereCollider starCollider)
        {
            if (starCollider == null || !starCollider.enabled || !starCollider.gameObject.activeInHierarchy) return;
            var balls = _controller.Balls;
            for (int i = 0; i < balls.Count; i++)
            {
                var ballCollider = balls[i].GetComponent<SphereCollider>();
                if (ballCollider != null && ballCollider.enabled && ballCollider.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(starCollider, ballCollider);
            }
        }

        private static float AngleFor(int count, int index)
        {
            if (count == 1) return 90;
            if (count == 2) return index == 0 ? 135 : 45;
            return 150 - index * 60;
        }

        public bool TryKeep(Collider collider)
        {
            if (collider == null) return false;
            for (int i = 0; i < _stars.Length; i++)
            {
                var star = _stars[i];
                if (star == null || !star.Root.activeInHierarchy) continue;
                if (collider == star.Collider || collider.transform.IsChildOf(star.Root.transform))
                {
                    Keep(i);
                    return true;
                }
            }
            return false;
        }

        private void Keep(int index)
        {
            if (index < 0 || index >= _topics.Length || _cachedSource == null) return;
            var source = _cachedSource;
            var star = _stars[index];
            _controller.KeepSuggestion(source, star.Topic, star.Root.transform.position);
            _suppressed = true;
            _suppressedSource = source;
            _suppressedSuggestions = source.Data.suggestions;
            HideAll();
        }

        private void HideAll()
        {
            for (int i = 0; i < _stars.Length; i++)
                if (_stars[i] != null && _stars[i].Root.activeSelf) _stars[i].Root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
