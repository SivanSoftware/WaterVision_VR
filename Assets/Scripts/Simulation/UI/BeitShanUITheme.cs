using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Shared visual design tokens for all Beit Shan simulation UI:
    /// HighwaySign, DetailPanel, MasterIntroPanel.
    ///
    /// Phase 4 update: switched to Claude's design language — warm cream surfaces,
    /// coral accent, dark brown ink text. Highway signs keep the dark ink background
    /// for outdoor readability across the plant.
    /// </summary>
    public static class BeitShanUITheme
    {
        // ============ Claude design palette ============

        // Surfaces — warm cream / ink
        public static readonly Color SurfaceCream     = new Color(0.96f, 0.95f, 0.92f, 0.97f); // primary detail-panel bg
        public static readonly Color SurfaceDeepCream = new Color(0.93f, 0.91f, 0.86f, 1f);    // header band on detail panel
        public static readonly Color SurfaceInk       = new Color(0.10f, 0.10f, 0.10f, 0.94f); // highway sign bg (outdoor)
        public static readonly Color SurfaceInkDeep   = new Color(0.07f, 0.07f, 0.07f, 1f);    // border / outline on dark

        // Accents — Anthropic coral
        public static readonly Color AccentCoral      = new Color(0.80f, 0.47f, 0.36f, 1f);    // primary action / underline
        public static readonly Color AccentCoralBold  = new Color(0.70f, 0.38f, 0.28f, 1f);    // pressed / dark variant
        public static readonly Color AccentCoralSoft  = new Color(0.92f, 0.72f, 0.62f, 1f);    // hover / subtle highlight

        // Text — on cream vs on ink
        public static readonly Color TextInk          = new Color(0.13f, 0.11f, 0.09f, 1f);    // dark brown on cream
        public static readonly Color TextInkMuted    = new Color(0.40f, 0.36f, 0.30f, 1f);     // muted warm grey on cream
        public static readonly Color TextOnInk        = new Color(0.97f, 0.96f, 0.93f, 1f);    // cream on dark (highway)
        public static readonly Color TextOnInkAccent  = new Color(0.92f, 0.72f, 0.62f, 1f);    // coral on dark

        // Danger (stop button) — dark brick
        public static readonly Color DangerBrick      = new Color(0.55f, 0.22f, 0.18f, 1f);
        public static readonly Color DangerBrickSoft  = new Color(0.75f, 0.40f, 0.32f, 1f);

        // ============ Legacy aliases — keep existing builder code compiling ============
        // (The visual meaning has changed: HeaderBlue is now actually cream, etc.)
        public static readonly Color HeaderBlue          = SurfaceDeepCream;
        public static readonly Color HeaderBlueDeep      = SurfaceDeepCream;
        public static readonly Color BodyBg              = SurfaceCream;
        public static readonly Color HighwayBg           = SurfaceInk;
        public static readonly Color BorderYellow        = AccentCoral;
        public static readonly Color ButtonPrimary       = AccentCoral;
        public static readonly Color ButtonPrimaryHover  = AccentCoralSoft;
        public static readonly Color ButtonDanger        = DangerBrick;
        public static readonly Color TextLight           = TextInk;        // for cream panels — dark brown text
        public static readonly Color TextMuted           = TextInkMuted;
        public static readonly Color TextAccent          = AccentCoral;

        // ============ Font sizes (at canvas scale, before transform.localScale) ============
        public const int FONT_TITLE_HIGHWAY    = 260;
        public const int FONT_SUBTITLE_HIGHWAY = 110;
        public const int FONT_TITLE_DETAIL     = 140;
        public const int FONT_TITLE_MASTER     = 210;
        public const int FONT_BODY             = 70;
        public const int FONT_BODY_LARGE       = 90;
        public const int FONT_BUTTON           = 80;
        public const int FONT_LINK             = 56;

        // ============ Dimensions (pixels — Canvas works at high pixel space, world scaled by Transform) ============
        public const float PIXEL_PER_METER     = 1000f; // canvas size 4000 = 4 meters at scale 0.001

        public const float HIGHWAY_WIDTH_M     = 5.0f;
        public const float HIGHWAY_HEIGHT_M    = 1.8f;
        public const float DETAIL_WIDTH_M      = 2.6f;
        public const float DETAIL_HEIGHT_M     = 1.7f;
        public const float MASTER_WIDTH_M      = 3.5f;
        public const float MASTER_HEIGHT_M     = 2.2f;

        // Padding (in canvas pixels — Claude uses generous spacing)
        public const float PADDING_DETAIL      = 80f;
        public const float PADDING_HIGHWAY     = 60f;
    }
}
