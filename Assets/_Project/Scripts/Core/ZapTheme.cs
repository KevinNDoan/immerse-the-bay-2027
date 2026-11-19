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

        [Header("Sky (when set, replaces the flat void colour; filled by ZAP > Build Sky)")]
        public Material skybox;
        [Tooltip("Flat ambient light while the sky is showing, so the marbles match the light sky.")]
        public Color skyAmbient = new Color(.80f, .81f, .84f, 1f);
        [Tooltip("The sky as one 2:1 panorama; frosted glass shows a blurred copy of it.")]
        public Texture2D skyPanorama;

        [Header("Sounds (filled by ZAP > Fill Theme Images)")]
        [Tooltip("Plays where two nodes get connected. Empty = silent.")]
        public AudioClip linkSound;
        [Range(0f, 1f)] public float linkSoundVolume = .6f;

        [Header("Frosted glass UI (cards, notes, labels, panes)")]
        [Tooltip("Off brings back the old dark panels and white text.")]
        public bool useFrostedGlass = true;
        public Color glassTint = new Color(1f, 1f, 1f, .30f);
        [Tooltip("Corner radius in metres. Figma 18 px = 9 mm.")]
        public float glassCornerRadius = .009f;
        [Tooltip("How far the soft white glow reaches in from the edge, in metres.")]
        public float glassGlowWidth = .008f;
        [Range(0f, 1f)] public float glassGlowStrength = .55f;
        [Range(0f, 1f)] public float glassEdgeStrength = .6f;
        [Tooltip("0 = plain tint, 1 = the blurred sky shows through.")]
        [Range(0f, 1f)] public float glassFrost = 1f;
        [Tooltip("Blur of the sky seen through the glass, as a mip level. Higher is blurrier.")]
        public float glassSkyBlur = 6f;
        [Range(0f, 1f)] public float glassOpacity = .92f;
        [Tooltip("Space between text and the glass edge, in metres.")]
        public float glassPadding = .012f;
        public Color glassTextColor = ZapColors.Ink;
        public Color glassDimTextColor = new Color(ZapColors.Ink.r, ZapColors.Ink.g, ZapColors.Ink.b, .65f);
        [Tooltip("Problem and error text on glass.")]
        public Color glassAlertColor = new Color(.878f, .318f, .494f, 1f);

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

        [Header("Start-up intro")]
        [Tooltip("How far ahead of the person the intro animation plays, in metres.")]
        public float introDistance = 2f;
        [Tooltip("Width of the intro animation, in metres.")]
        public float introWidth = 2.4f;
        [Tooltip("Colour that fills the view around the intro animation. Match the animation's own background.")]
        public Color introBackdrop = new Color(.914f, .941f, .953f, 1f);
        [Tooltip("How long the intro takes to fade into the workspace, in seconds.")]
        public float introFadeSeconds = .6f;

        [Header("Controls card")]
        [Tooltip("How far ahead of the person the card sits, in metres.")]
        public float controlsDistance = 1.2f;
        [Tooltip("Closest distance of the in-world controls card from the face, in metres.")]
        public float controlsMinDistanceFromFace = .6f;
        [Tooltip("How far below eye height the centre of the card sits, in metres.")]
        public float controlsDropBelowEyes = .1f;
        [Tooltip("Card size while a tutorial step shows, in metres.")]
        public Vector2 cardSize = new Vector2(.56f, .41f);
        [Tooltip("Card size while no tutorial step shows, in metres.")]
        public Vector2 cardCompactSize = new Vector2(.56f, .31f);
        [Tooltip("Space between the card edge and its content, in metres.")]
        public float cardPadding = .03f;
        [Tooltip("Title text size on the card.")]
        public float cardTitleSize = .4f;
        [Tooltip("Tutorial step text size on the card.")]
        public float cardStepSize = .34f;
        [Tooltip("Status text size on both card layouts.")]
        public float cardStatusSize = .32f;
        [Tooltip("Hint text size on the card.")]
        public float cardHintSize = .24f;
        [Tooltip("Smallest size the person can shrink the card to, as a multiple of its normal size.")]
        public float cardMinScale = .5f;
        [Tooltip("Largest size the person can grow the card to, as a multiple of its normal size.")]
        public float cardMaxScale = 2.5f;
        [Tooltip("How close to a corner the pointer must be to resize the card, in metres.")]
        public float cardCornerReach = .045f;
        [Tooltip("Size of the hide and show button on the card, in metres.")]
        public float cardButtonSize = .05f;
        [Tooltip("Controller hint text size.")]
        public float hintTextSize = .12f;
        [Tooltip("Controller hint line width, in metres.")]
        public float hintLineWidth = .0015f;
        [Tooltip("Translucent background colour of the controls card.")]
        public Color cardColor = new Color(.078f, .051f, .149f, .9f);
        [Tooltip("Colour of the card outline.")]
        public Color cardOutlineColor = new Color(.25f, .24f, .29f, 1f);
        [Tooltip("Width of the card outline, in metres.")]
        public float cardOutlineWidth = .004f;
        [Tooltip("Primary controls card text colour.")]
        public Color cardTextColor = new Color(.83f, .82f, .86f, 1f);
        [Tooltip("Secondary controls card text colour.")]
        public Color cardDimTextColor = new Color(.83f, .82f, .86f, .6f);
        [Tooltip("Colour of a highlighted controller hint.")]
        public Color hintHighlightColor = Color.white;

        [Header("Star images (optional; crystal previews remain available)")]
        [Tooltip("Use designer star art for nodes without a world preview. Recording always uses the glowing crystal.")]
        public bool useStarNodeArt = false;
        [Tooltip("Real diameter of the star core when idle. Figma 64 px = 3.2 cm.")]
        public float starCoreDiameter = .032f;
        [Tooltip("Layer diameters as multiples of the idle core.")]
        public float starGlowScale = 4f;
        public float starRingScale = 2f;
        public float starSparksScale = 2.6f;
        public float starOrbitScale = 4f;
        [Tooltip("Pointed at: core and glow grow by these factors.")]
        public float starPointedCoreScale = 1.1f;
        public float starPointedGlowScale = 1.05f;
        [Tooltip("Grabbed: core and glow grow by these factors.")]
        public float starGrabbedCoreScale = 1.3f;
        public float starGrabbedGlowScale = 1.2f;

        [Header("Note paper (used when Note background is set)")]
        [Tooltip("Space between the text and the paper edge.")]
        public float notePadding = .008f;
        [Tooltip("Smallest side of the square compact note. Figma 200 px = 10 cm.")]
        public float noteMinSize = .10f;
        [Tooltip("Smallest width of the expanded note. Figma 320 px = 16 cm.")]
        public float noteExpandedMinWidth = .16f;

        [Header("Line image (used when Line image is set)")]
        [Tooltip("Full width of the line image including its halo. Figma 16 px = 8 mm.")]
        public float lineImageWidth = .008f;

        [Header("Images and font (filled by ZAP > Fill Theme Images)")]
        public Texture2D nodeCore;
        public Texture2D nodeGlow;
        public Texture2D nodeRing;
        public Texture2D nodeSparks;
        public Texture2D nodeOrbit;
        public Texture2D lineImage;
        public Texture2D noteBackground;
        public Texture2D noteBackgroundPointed;
        public Texture2D noteBackgroundHeld;
        public Texture2D noteExpanded;
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
