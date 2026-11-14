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

        [Header("Controls card")]
        public Vector2 controlsCardSize = new Vector2(.5f, .32f);
        public float controlsTextSizeMin = .22f;
        public float controlsTextSizeMax = .35f;
        [Tooltip("The card never comes closer to the face than this.")]
        public float controlsMinDistanceFromFace = .6f;

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
