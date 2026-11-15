using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Zap.Constellation
{
    // A separate reading surface: moving it never moves or rescales its learning object.
    public sealed class ExplorationPane : MonoBehaviour
    {
        public XRGrabInteractable Grab { get; private set; }
        public bool IsHeld => Grab != null && Grab.isSelected;
        public bool IsPlaced { get; private set; }
        public string BodyText { get; private set; }
        public string StatusText { get; private set; }
        public bool IsBodyTruncated => _body != null && _body.isTextTruncated;
        private TextMeshPro _title, _body, _status, _handle;
        private Transform _glass;
        private Material _material;
        private BoxCollider _collider;
        private string _lastTitle;
        private bool _lastPending, _lastError;
        private const float Width = .82f, ContentWidth = .72f;

        public void Initialize()
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "Glass reading surface"; _glass = plate.transform;
            _glass.SetParent(transform, false); _glass.localPosition = Vector3.forward * .012f;
            plate.GetComponent<Collider>().enabled = false; Destroy(plate.GetComponent<Collider>());
            _material = new Material(Shader.Find("Zap/GlassPane"));
            plate.GetComponent<Renderer>().sharedMaterial = _material;
            _title = Label("Heading", .40f, FontStyles.Bold, new Color(.94f,.98f,1));
            _body = Label("Explanation", .32f, FontStyles.Normal, new Color(.94f,.98f,1));
            _body.enableAutoSizing = true; _body.fontSizeMin = .28f; _body.fontSizeMax = .32f;
            _status = Label("Response status", .26f, FontStyles.Normal, new Color(.57f,.86f,1));
            _handle = Label("Move handle", .23f, FontStyles.Normal, new Color(.67f,.86f,.94f));
            _handle.text = "GRIP PANE TO MOVE";
            _collider = gameObject.AddComponent<BoxCollider>(); _collider.isTrigger = true;
            var body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            Grab = gameObject.AddComponent<XRGrabInteractable>();
            Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            Grab.throwOnDetach = false; Grab.forceGravityOnDetach = false; Grab.useDynamicAttach = true;
            Grab.colliders.Clear(); Grab.colliders.Add(_collider);
            Grab.selectEntered.AddListener(_ => IsPlaced = true);
        }

        private TextMeshPro Label(string name, float size, FontStyles style, Color color)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false);
            var text = obj.AddComponent<TextMeshPro>(); text.fontSize = size; text.fontStyle = style;
            text.richText = false; text.alignment = TextAlignmentOptions.TopLeft;
            text.color = color; text.overflowMode = TextOverflowModes.Ellipsis;
            // Local shadow keeps letters legible over bright room details through the glass.
            var material = text.fontMaterial;
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(.015f,.04f,.065f,.9f));
            material.SetFloat("_UnderlayDilate", .18f); material.SetFloat("_UnderlaySoftness", .12f);
            return text;
        }

        public void SetContent(string title, string body, string status, bool pending = false, bool error = false)
        {
            title ??= "Exploration"; body ??= ""; status ??= "";
            if (_lastTitle == title && BodyText == body && StatusText == status && _lastPending == pending && _lastError == error) return;
            _lastTitle = title; BodyText = body; StatusText = status; _lastPending = pending; _lastError = error;
            _title.text = title; _body.text = body; _status.text = status;
            _status.color = error ? new Color(1,.77f,.51f) : new Color(.57f,.86f,1);
            _material.SetFloat("_Pending", pending ? 1 : 0);
            // Grow with the answer instead of silently cutting it to the old small sign.
            float heading = Mathf.Clamp(_title.GetPreferredValues(title, ContentWidth, Mathf.Infinity).y, .065f, .16f);
            float content = Mathf.Clamp(_body.GetPreferredValues(body, ContentWidth, Mathf.Infinity).y + .025f, .18f, .83f);
            float footer = Mathf.Clamp(_status.GetPreferredValues(status, ContentWidth, Mathf.Infinity).y, .04f, .10f);
            float height = .15f + heading + content + footer;
            _glass.localScale = new Vector3(Width, height, 1);
            _material.SetVector("_Size", new Vector4(Width,height,0,0));
            _collider.size = new Vector3(Width,height,.025f);
            Layout(_handle, height/2 - .025f, .035f);
            Layout(_title, height/2 - .072f, heading);
            Layout(_body, height/2 - .085f - heading, content);
            Layout(_status, -height/2 + .035f + footer, footer);
        }

        private static void Layout(TextMeshPro text, float top, float height)
        {
            text.rectTransform.sizeDelta = new Vector2(ContentWidth,height);
            text.transform.localPosition = new Vector3(0,top-height/2,0);
        }

        public void Follow(Vector3 position, Transform head)
        {
            if (IsPlaced || IsHeld) return;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position));
        }
        public void Attach() { if (!IsHeld) IsPlaced = false; }
        public void Place(Vector3 position, Quaternion rotation)
        { IsPlaced = true; transform.SetPositionAndRotation(position,rotation); }
        private void OnDestroy() { if (_material != null) Destroy(_material); }
    }
}
