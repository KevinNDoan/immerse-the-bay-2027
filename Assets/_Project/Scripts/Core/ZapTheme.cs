using TMPro;
using UnityEngine;

namespace Zap.Core
{
    /// <summary>
    /// Every look value in one place. Code reads it; the designer edits the asset at
    /// Assets/_Project/Art/Theme/Resources/ZapTheme.asset in the Inspector.
    /// Lengths are in metres. Text sizes are TextMeshPro world-space font sizes.
    /// Behaviour values (physics, grab, timing) do not belong here.
    /// </summary>
    public sealed class ZapTheme : ScriptableObject
    {
        public const string ResourceName = "ZapTheme";

        [Header("Colours")]
        public Color voidColor = ZapColors.Void;
        public Color voidLift = ZapColors.VoidLift;
        public Color nodeColor = ZapColors.White;
        public Color lineColor = ZapColors.White;
        public Color textColor = ZapColors.White;
        public Color paper = ZapColors.Paper;
        public Color ink = ZapColors.Ink;

        [Header("Node")]
        public float nodeDiameter = .11f;
        public float nodeSelectedDiameter = .125f;

        [Header("Note under a node")]
        public float noteOffsetBelowNode = .09f;
        public Vector2 noteBoxSize = new Vector2(.62f, .35f);
        public float topicTextSize = .38f;
        public float summaryTextSize = .24f;
        [Tooltip("The summary shows when the selected node is closer than this.")]
        public float summaryShowDistance = .85f;
        [Tooltip("The note is hidden when the node is further than this.")]
        public float noteHideDistance = 4f;

        [Header("Line between nodes")]
        public float lineWidth = .0025f;

        [Header("Suggested topics")]
        public float suggestedNodeDiameter = .06f;
        public float suggestedDistance = .24f;
        public Color suggestedColor = new Color(.4f, .4f, .4f, 1f);
        public float suggestedTextSize = .2f;
        public Color suggestedTextColor = new Color(1f, 1f, 1f, .7f);

        [Header("Controls card")]
        [Tooltip("How far ahead of the person the card sits, in metres.")]
        public float controlsDistance = 1.3f;
        [Tooltip("How far below eye height the centre of the card sits, in metres.")]
        public float controlsDropBelowEyes = .35f;
        [Tooltip("Full controls card size, in metres.")]
        public Vector2 cardSize = new Vector2(.58f, .5f);
        [Tooltip("Compact in-world controls card size, in metres.")]
        public Vector2 cardCompactSize = new Vector2(.58f, .27f);
        [Tooltip("Space between the card edge and its content, in metres.")]
        public float cardPadding = .03f;
        [Tooltip("Title text size on the full card.")]
        public float cardTitleSize = .45f;
        [Tooltip("Status text size on both card layouts.")]
        public float cardStatusSize = .32f;
        [Tooltip("Control-list text size on the full card.")]
        public float cardBodySize = .28f;
        [Tooltip("Hint text size on the compact card.")]
        public float cardHintSize = .24f;
        [Tooltip("Translucent background colour of the controls card.")]
        public Color cardColor = new Color(.078f, .051f, .149f, .9f);
        [Tooltip("Colour of the card outline and divider.")]
        public Color cardOutlineColor = new Color(.25f, .24f, .29f, 1f);
        [Tooltip("Width of the card outline, in metres.")]
        public float cardOutlineWidth = .004f;
        [Tooltip("Primary controls card text colour.")]
        public Color cardTextColor = new Color(.83f, .82f, .86f, 1f);
        [Tooltip("Secondary controls card text colour.")]
        public Color cardDimTextColor = new Color(.83f, .82f, .86f, .6f);

        [Header("Images and font (empty until the designer adds them)")]
        public Texture2D nodeGlow;
        public Texture2D nodeRing;
        public Texture2D lineImage;
        public Texture2D noteBackground;
        public TMP_FontAsset font;

        private static ZapTheme _current;

        /// <summary>The theme asset, or built-in defaults if the asset is missing.</summary>
        public static ZapTheme Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<ZapTheme>(ResourceName);
                if (_current == null)
                {
                    _current = CreateInstance<ZapTheme>();
                    _current.name = "ZapTheme (built-in defaults)";
                    Debug.LogWarning("ZAP theme asset not found in a Resources folder. Using built-in defaults. Run ZAP > Create Theme Asset.");
                }
                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { _current = null; }
    }
}
