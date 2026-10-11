using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WaterVision.ASL;

/// <summary>
/// One-shot editor menu that authors all 50 ASL signs into the SignCatalog ScriptableObject
/// at Assets/Resources/ASL/SignCatalog.asset. Re-run to overwrite (it asks before clobbering).
///
/// Each sign is hand-transcribed from Lifeprint.com / Handspeak.com video references,
/// then mapped to keyframe data (hand target position chest-relative, finger curls,
/// head tilt). DISCLAIMER: this is an interpreter-style approximation, not certified ASL.
/// </summary>
public static class SeedSignCatalog
{
    const string OUT_FOLDER = "Assets/Resources/ASL";
    const string OUT_PATH   = "Assets/Resources/ASL/SignCatalog.asset";

    [MenuItem("Tools/ASL/Seed Sign Catalog")]
    public static void Seed()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(OUT_FOLDER))
            AssetDatabase.CreateFolder("Assets/Resources", "ASL");

        var existing = AssetDatabase.LoadAssetAtPath<SignCatalog>(OUT_PATH);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog(
                "Seed Sign Catalog",
                $"SignCatalog already exists at\n{OUT_PATH}\nwith {existing.SignCount} signs.\n\nOverwrite?",
                "Overwrite", "Cancel")) return;
            existing.signs.Clear();
            existing.aliases.Clear();
            existing.stoplist.Clear();
        }
        else
        {
            existing = ScriptableObject.CreateInstance<SignCatalog>();
            AssetDatabase.CreateAsset(existing, OUT_PATH);
        }

        // ── Populate ─────────────────────────────────────────────────────────
        existing.signs    = BuildSigns();
        existing.aliases  = BuildAliases();
        existing.stoplist = BuildStoplist();

        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ASL] Seeded SignCatalog at {OUT_PATH} with " +
                  $"{existing.signs.Count} signs, " +
                  $"{existing.aliases.Count} aliases, " +
                  $"{existing.stoplist.Count} stop-words.");
    }

    // Convenience builder: a finger curl row matching the ASLSigningCharacterManager
    // 5-finger convention (idx, mid, ring, pinky, thumb).
    struct C
    {
        public float i, m, r, p, t;
        public static C Open => new C { i = 0,    m = 0,    r = 0,    p = 0,    t = 0    };
        public static C Fist => new C { i = .85f, m = .85f, r = .85f, p = .85f, t = .55f };
        public static C Flat => new C { i = 0,    m = 0,    r = 0,    p = 0,    t = .55f };
        public static C Point => new C { i = 0,   m = .85f, r = .85f, p = .85f, t = .50f };
        public static C ThumbOut => new C { i = .85f, m = .85f, r = .85f, p = .85f, t = 0 };
        public static C OpenB => new C { i = 0,   m = 0,    r = 0,    p = 0,    t = .65f };
    }

    static void Apply(ref SignKeyframe k, bool right, C c)
    {
        if (right) { k.r_idx = c.i; k.r_mid = c.m; k.r_ring = c.r; k.r_pinky = c.p; k.r_thumb = c.t; }
        else       { k.l_idx = c.i; k.l_mid = c.m; k.l_ring = c.r; k.l_pinky = c.p; k.l_thumb = c.t; }
    }

    // Common chest-relative positions
    static readonly Vector3 NEUTRAL_R = new Vector3( 0.18f, 1.30f, 0.32f);
    static readonly Vector3 NEUTRAL_L = new Vector3(-0.18f, 1.30f, 0.32f);
    static readonly Vector3 HIP_L     = new Vector3(-0.16f, 0.95f, 0.12f); // resting left hand for 1-hand signs
    static readonly Vector3 HIP_R     = new Vector3( 0.16f, 0.95f, 0.12f);

    // ── BUILDERS ─────────────────────────────────────────────────────────────

    static SignKeyframe Frame(
        Vector3 rPos, Vector3 rEul, C rCurl,
        Vector3 lPos, Vector3 lEul, C lCurl,
        float headTilt = 0, float torsoLean = 0,
        float hold = 0.10f, float transition = 0.25f)
    {
        var k = new SignKeyframe {
            rightHandLocalPos   = rPos, rightHandLocalEuler = rEul,
            leftHandLocalPos    = lPos, leftHandLocalEuler  = lEul,
            headTiltDeg = headTilt, torsoLeanDeg = torsoLean,
            holdSeconds = hold,     transitionSeconds = transition
        };
        Apply(ref k, true,  rCurl);
        Apply(ref k, false, lCurl);
        return k;
    }

    static SignDefinition Make(string word, float speed, string notes, params SignKeyframe[] frames)
        => new SignDefinition { word = word, frames = frames, playbackSpeed = speed, notes = notes };

    static List<SignDefinition> BuildSigns()
    {
        var s = new List<SignDefinition>();

        // ══ GREETINGS (5) ════════════════════════════════════════════════════
        // HELLO — right flat hand at forehead, swing out to the side (military salute)
        s.Add(Make("hello", 1.0f, "Right flat-B hand salutes from forehead outward.",
            Frame(new Vector3(0.18f, 1.65f,  0.28f), new Vector3(0,  10,   0), C.Flat,  HIP_L, Vector3.zero, C.Fist),
            Frame(new Vector3(0.42f, 1.62f,  0.25f), new Vector3(0,  35,  -5), C.Flat,  HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.30f)));

        // HI — same as hello (alias added below)
        s.Add(Make("hi", 1.0f, "Same shape as HELLO, slightly smaller sweep.",
            Frame(new Vector3(0.18f, 1.65f,  0.28f), new Vector3(0,  10,   0), C.Flat,  HIP_L, Vector3.zero, C.Fist),
            Frame(new Vector3(0.38f, 1.62f,  0.25f), new Vector3(0,  30,  -5), C.Flat,  HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f)));

        // BYE — wave the open hand (fingers wiggle approximated by curl variation)
        s.Add(Make("bye", 1.2f, "Wave open hand at shoulder level.",
            Frame(new Vector3(0.22f, 1.55f,  0.30f), new Vector3(0,   0, -10), C.Open,  HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.22f, 1.55f,  0.30f), new Vector3(0,   0,  10), new C{i=.3f,m=.3f,r=.3f,p=.3f,t=0},  HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.20f),
            Frame(new Vector3(0.22f, 1.55f,  0.30f), new Vector3(0,   0, -10), C.Open,  HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.20f)));

        // GOOD-MORNING — chin flat hand forward + opposite hand under elbow
        s.Add(Make("good-morning", 0.9f, "GOOD (flat hand from chin down) + MORNING (rising forearm).",
            Frame(new Vector3(0.10f, 1.55f,  0.30f), new Vector3( 30,  0, 0), C.Flat,    new Vector3(-0.10f, 1.50f, 0.30f), Vector3.zero, C.Flat, 0, 0, 0.12f, 0.30f),
            Frame(new Vector3(0.15f, 1.20f,  0.40f), new Vector3(-20,  0, 0), C.Flat,    new Vector3(-0.05f, 1.10f, 0.35f), Vector3.zero, C.Flat, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.20f, 1.45f,  0.40f), new Vector3(-30,  0, 0), C.Flat,    new Vector3(-0.05f, 1.10f, 0.35f), Vector3.zero, C.Flat, 0, 0, 0.10f, 0.30f)));

        // GOODNIGHT — flat hand arcs down to other flat hand (sun setting under horizon)
        s.Add(Make("goodnight", 0.9f, "Right flat hand arcs down onto left flat hand back.",
            Frame(new Vector3(0.10f, 1.60f,  0.35f), new Vector3(-30,  0, 0), C.Flat,    new Vector3(-0.10f, 1.20f, 0.40f), new Vector3( 20, 0, 0), C.Flat, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(-0.05f, 1.20f, 0.40f), new Vector3(-50,  0, 0), C.Flat,    new Vector3(-0.10f, 1.20f, 0.40f), new Vector3( 20, 0, 0), C.Flat, 5, 0, 0.15f, 0.30f)));

        // ══ SOCIAL (8) ═══════════════════════════════════════════════════════
        // PLEASE — flat hand on chest, small circular motion
        s.Add(Make("please", 0.8f, "Flat hand on chest, small CW circle (approximated as 2 keyframes).",
            Frame(new Vector3(0.05f, 1.25f, 0.18f), new Vector3(-15, -30,  0), C.Flat,  HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.10f, 1.20f, 0.18f), new Vector3(-15, -30, -10), C.Flat, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.05f, 1.25f, 0.18f), new Vector3(-15, -30,  0), C.Flat,  HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f)));

        // THANK-YOU — flat fingers touch chin, then forward (kiss outward)
        s.Add(Make("thank-you", 0.85f, "Fingertips touch chin, move forward+down.",
            Frame(new Vector3(0.05f, 1.55f, 0.32f), new Vector3(-30,  0,  0), C.Flat, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.12f, 0.30f),
            Frame(new Vector3(0.15f, 1.40f, 0.50f), new Vector3(-50,  0,  0), C.Flat, HIP_L, Vector3.zero, C.Fist, 5, 5, 0.15f, 0.35f)));

        // SORRY — fist on chest, small CCW circle
        s.Add(Make("sorry", 0.85f, "Right fist on chest, small circular motion.",
            Frame(new Vector3(0.05f, 1.30f, 0.18f), new Vector3(0, -20, 0), C.Fist, HIP_L, Vector3.zero, C.Fist, 10, 0, 0.12f, 0.30f),
            Frame(new Vector3(0.10f, 1.25f, 0.18f), new Vector3(0, -20, 10), C.Fist, HIP_L, Vector3.zero, C.Fist, 10, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.05f, 1.30f, 0.18f), new Vector3(0, -20, 0), C.Fist, HIP_L, Vector3.zero, C.Fist, 10, 0, 0.10f, 0.25f)));

        // YOU-WELCOME — same handshape as THANK-YOU but answers it; approximated as gentle wave
        s.Add(Make("welcome", 0.9f, "Open hand sweeps inward toward chest (welcoming gesture).",
            Frame(new Vector3(0.35f, 1.40f, 0.30f), new Vector3(0, -10, -10), C.Open, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.10f, 1.35f, 0.32f), new Vector3(0, -30,   0), C.Open, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.30f)));

        // YES — S-handshape nod
        s.Add(Make("yes", 1.4f, "Right S-fist nods at the wrist.",
            Frame(new Vector3(0.20f, 1.40f, 0.35f), new Vector3(  0,  0, 0), C.Fist, HIP_L, Vector3.zero, C.Fist, 5, 0, 0.06f, 0.20f),
            Frame(new Vector3(0.20f, 1.38f, 0.35f), new Vector3( 35,  0, 0), C.Fist, HIP_L, Vector3.zero, C.Fist, 8, 0, 0.06f, 0.12f),
            Frame(new Vector3(0.20f, 1.40f, 0.35f), new Vector3(  0,  0, 0), C.Fist, HIP_L, Vector3.zero, C.Fist, 5, 0, 0.06f, 0.12f)));

        // NO — index+middle+thumb close (like a duck beak shutting)
        s.Add(Make("no", 1.4f, "Index+middle+thumb close in beak motion.",
            Frame(new Vector3(0.18f, 1.40f, 0.40f), new Vector3(0, -10, 0), new C{i=0,m=0,r=.85f,p=.85f,t=0}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.06f, 0.20f),
            Frame(new Vector3(0.18f, 1.40f, 0.40f), new Vector3(0, -10, 0), new C{i=.50f,m=.50f,r=.85f,p=.85f,t=.50f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.10f)));

        // MAYBE — both flat hands palm-up alternate up/down
        s.Add(Make("maybe", 0.9f, "Both palms up, alternate weighing scales.",
            Frame(new Vector3(0.20f, 1.30f, 0.30f), new Vector3(-90,  0, 0), C.Flat,  new Vector3(-0.20f, 1.20f, 0.30f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.20f, 1.20f, 0.30f), new Vector3(-90,  0, 0), C.Flat,  new Vector3(-0.20f, 1.30f, 0.30f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.20f, 1.30f, 0.30f), new Vector3(-90,  0, 0), C.Flat,  new Vector3(-0.20f, 1.20f, 0.30f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.10f, 0.25f)));

        // OK — index + thumb form circle, others up
        s.Add(Make("ok", 1.0f, "F-hand (index-thumb circle, M/R/P extended).",
            Frame(new Vector3(0.22f, 1.45f, 0.40f), new Vector3(0, 0, 0), new C{i=.50f,m=0,r=0,p=0,t=.55f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.15f, 0.30f)));

        // ══ PRONOUNS (6) ═════════════════════════════════════════════════════
        // I (me) — index points to own chest
        s.Add(Make("i", 1.0f, "Index point at sternum.",
            Frame(new Vector3(0.02f, 1.30f, 0.10f), new Vector3(0, -45, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 5, 0, 0.15f, 0.25f)));
        s.Add(Make("me", 1.0f, "Same as I.",
            Frame(new Vector3(0.02f, 1.30f, 0.10f), new Vector3(0, -45, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 5, 0, 0.15f, 0.25f)));

        // YOU — index points outward
        s.Add(Make("you", 1.0f, "Index point straight forward.",
            Frame(new Vector3(0.20f, 1.40f, 0.50f), new Vector3(0,  10, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.12f, 0.25f)));

        // WE — index touches one shoulder, arcs to other
        s.Add(Make("we", 0.9f, "Index touches right then left shoulder.",
            Frame(new Vector3(0.05f, 1.45f, 0.15f), new Vector3(0, -30, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(-0.05f, 1.45f, 0.15f), new Vector3(0,  30, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.30f)));

        // THEY — index sweep to the right (the others)
        s.Add(Make("they", 0.9f, "Index sweep to the side.",
            Frame(new Vector3(0.15f, 1.40f, 0.45f), new Vector3(0,   0, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.40f, 1.40f, 0.35f), new Vector3(0,  40, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f)));

        // IT — index points down-forward
        s.Add(Make("it", 1.0f, "Index point downward-forward.",
            Frame(new Vector3(0.20f, 1.20f, 0.45f), new Vector3(30, 10, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f)));

        // ══ COMMON VERBS (10) ════════════════════════════════════════════════
        // WANT — both hands open, palms up, pull toward body (hungry hands)
        s.Add(Make("want", 0.9f, "Open claw hands, palms up, drag toward body.",
            Frame(new Vector3(0.25f, 1.35f, 0.50f), new Vector3(-60, 0, 0), C.OpenB, new Vector3(-0.25f, 1.35f, 0.50f), new Vector3(-60, 0, 0), C.OpenB, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.20f, 1.25f, 0.25f), new Vector3(-60, 0, 0), new C{i=.35f,m=.35f,r=.35f,p=.35f,t=.40f}, new Vector3(-0.20f, 1.25f, 0.25f), new Vector3(-60, 0, 0), new C{i=.35f,m=.35f,r=.35f,p=.35f,t=.40f}, 0, 5, 0.12f, 0.30f)));

        // NEED — index hook down twice (X handshape, downward bounce)
        s.Add(Make("need", 1.2f, "X-handshape (hooked index) bounces downward.",
            Frame(new Vector3(0.18f, 1.40f, 0.40f), new Vector3(45, 0, 0), new C{i=.6f,m=.85f,r=.85f,p=.85f,t=.5f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(0.18f, 1.20f, 0.45f), new Vector3(60, 0, 0), new C{i=.6f,m=.85f,r=.85f,p=.85f,t=.5f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.06f, 0.10f),
            Frame(new Vector3(0.18f, 1.30f, 0.42f), new Vector3(50, 0, 0), new C{i=.6f,m=.85f,r=.85f,p=.85f,t=.5f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.06f, 0.10f)));

        // HAVE — both flat hands touch chest with fingertips
        s.Add(Make("have", 0.9f, "Both flat hands, fingertips touch chest.",
            Frame(new Vector3(0.10f, 1.40f, 0.30f), new Vector3(-30, 0, 0), C.Flat, new Vector3(-0.10f, 1.40f, 0.30f), new Vector3(-30, 0, 0), C.Flat, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.05f, 1.30f, 0.15f), new Vector3(-30, 0, 0), C.Flat, new Vector3(-0.05f, 1.30f, 0.15f), new Vector3(-30, 0, 0), C.Flat, 0, 5, 0.15f, 0.25f)));

        // HELP — right fist sits on top of left flat palm, both rise together
        s.Add(Make("help", 0.9f, "Right fist on left flat palm, both lift.",
            Frame(new Vector3(0.00f, 1.20f, 0.40f), new Vector3(0, 0, 0), C.Fist, new Vector3(-0.05f, 1.15f, 0.40f), new Vector3(-30, 0, 0), C.Flat, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.00f, 1.40f, 0.45f), new Vector3(0, 0, 0), C.Fist, new Vector3(-0.05f, 1.35f, 0.45f), new Vector3(-30, 0, 0), C.Flat, 0, 0, 0.12f, 0.30f)));

        // GO — both index fingers point forward, jab outward
        s.Add(Make("go", 1.2f, "Both index point forward, both jab outward.",
            Frame(new Vector3(0.15f, 1.35f, 0.25f), new Vector3(0, 0, 0), C.Point, new Vector3(-0.15f, 1.35f, 0.25f), new Vector3(0, 0, 0), C.Point, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(0.20f, 1.30f, 0.55f), new Vector3(0, 0, 0), C.Point, new Vector3(-0.20f, 1.30f, 0.55f), new Vector3(0, 0, 0), C.Point, 0, 0, 0.10f, 0.20f)));

        // COME — index fingers curl, both pull toward body
        s.Add(Make("come", 1.0f, "Both index fingers beckon inward.",
            Frame(new Vector3(0.25f, 1.30f, 0.55f), new Vector3(0, 0, 0), C.Point, new Vector3(-0.25f, 1.30f, 0.55f), new Vector3(0, 0, 0), C.Point, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.15f, 1.35f, 0.25f), new Vector3(0, 0, 0), C.Point, new Vector3(-0.15f, 1.35f, 0.25f), new Vector3(0, 0, 0), C.Point, 0, 0, 0.10f, 0.25f)));

        // STOP — right karate chop onto left flat palm
        s.Add(Make("stop", 1.3f, "Right flat-B hand chops onto left flat-B palm.",
            Frame(new Vector3(0.10f, 1.50f, 0.40f), new Vector3(0, -30, -20), C.Flat, new Vector3(-0.10f, 1.25f, 0.40f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(-0.05f, 1.25f, 0.40f), new Vector3(0, -30, -90), C.Flat, new Vector3(-0.10f, 1.25f, 0.40f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.15f, 0.15f)));

        // EAT — flat-O hand to mouth
        s.Add(Make("eat", 0.9f, "Flat-O (fingertips+thumb pinch) taps mouth.",
            Frame(new Vector3(0.10f, 1.55f, 0.30f), new Vector3(0, 0, 0), new C{i=.55f,m=.55f,r=.55f,p=.55f,t=.55f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.05f, 1.62f, 0.20f), new Vector3(0, 0, 0), new C{i=.55f,m=.55f,r=.55f,p=.55f,t=.55f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.20f)));

        // DRINK — C handshape to mouth (cup tipping)
        s.Add(Make("drink", 0.9f, "C-hand tips toward mouth (drinking cup).",
            Frame(new Vector3(0.18f, 1.40f, 0.30f), new Vector3(0, 0, 0), new C{i=.35f,m=.35f,r=.35f,p=.35f,t=.35f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.10f, 1.62f, 0.20f), new Vector3(-40, 0, 0), new C{i=.35f,m=.35f,r=.35f,p=.35f,t=.35f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.12f, 0.25f)));

        // WORK — both fists, right taps top of left
        s.Add(Make("work", 1.1f, "Right S-fist taps top of left S-fist twice.",
            Frame(new Vector3(0.05f, 1.30f, 0.40f), new Vector3(0, 0, 0), C.Fist, new Vector3(-0.05f, 1.20f, 0.40f), new Vector3(0, 0, 0), C.Fist, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(0.05f, 1.20f, 0.40f), new Vector3(0, 0, 0), C.Fist, new Vector3(-0.05f, 1.20f, 0.40f), new Vector3(0, 0, 0), C.Fist, 0, 0, 0.05f, 0.08f),
            Frame(new Vector3(0.05f, 1.30f, 0.40f), new Vector3(0, 0, 0), C.Fist, new Vector3(-0.05f, 1.20f, 0.40f), new Vector3(0, 0, 0), C.Fist, 0, 0, 0.05f, 0.08f)));

        // ══ EMOTION / STATE (6) ══════════════════════════════════════════════
        // GOOD — flat hand from chin moves down to other flat palm
        s.Add(Make("good", 0.95f, "Flat hand from chin moves down onto other flat palm.",
            Frame(new Vector3(0.05f, 1.55f, 0.32f), new Vector3(-30, 0, 0), C.Flat, new Vector3(-0.05f, 1.20f, 0.40f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(-0.02f, 1.22f, 0.40f), new Vector3(-50, 0, 0), C.Flat, new Vector3(-0.05f, 1.20f, 0.40f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.12f, 0.25f)));

        // BAD — flat hand from chin flips downward
        s.Add(Make("bad", 1.2f, "Flat hand from chin flips palm-down outward.",
            Frame(new Vector3(0.05f, 1.55f, 0.32f), new Vector3(-30, 0, 0), C.Flat, HIP_L, Vector3.zero, C.Fist, -5, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.18f, 1.30f, 0.45f), new Vector3(60, 30, 0), C.Flat, HIP_L, Vector3.zero, C.Fist, -10, 0, 0.10f, 0.20f)));

        // HAPPY — flat hand brushes chest upward twice
        s.Add(Make("happy", 1.0f, "Flat hand brushes upward on chest twice.",
            Frame(new Vector3(0.08f, 1.20f, 0.18f), new Vector3(-20, -10, 0), C.Flat, HIP_L, Vector3.zero, C.Fist, -5, 0, 0.06f, 0.15f),
            Frame(new Vector3(0.08f, 1.40f, 0.18f), new Vector3(-20, -10, 0), C.Flat, HIP_L, Vector3.zero, C.Fist, -5, 0, 0.05f, 0.10f),
            Frame(new Vector3(0.08f, 1.20f, 0.18f), new Vector3(-20, -10, 0), C.Flat, HIP_L, Vector3.zero, C.Fist, -5, 0, 0.05f, 0.10f),
            Frame(new Vector3(0.08f, 1.40f, 0.18f), new Vector3(-20, -10, 0), C.Flat, HIP_L, Vector3.zero, C.Fist, -5, 0, 0.05f, 0.10f)));

        // SAD — open hands at face, drag down
        s.Add(Make("sad", 0.85f, "Open hands at face, drag downward (tears).",
            Frame(new Vector3(0.10f, 1.65f, 0.20f), new Vector3(-20, 0, 0), new C{i=0,m=0,r=0,p=0,t=.3f}, new Vector3(-0.10f, 1.65f, 0.20f), new Vector3(-20, 0, 0), new C{i=0,m=0,r=0,p=0,t=.3f}, 10, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.10f, 1.25f, 0.25f), new Vector3(-20, 0, 0), new C{i=0,m=0,r=0,p=0,t=.3f}, new Vector3(-0.10f, 1.25f, 0.25f), new Vector3(-20, 0, 0), new C{i=0,m=0,r=0,p=0,t=.3f}, 15, 0, 0.10f, 0.35f)));

        // TIRED — both bent-V hands on chest, drop slightly
        s.Add(Make("tired", 0.8f, "Bent-V hands on chest fall slightly.",
            Frame(new Vector3(0.08f, 1.35f, 0.18f), new Vector3(-30, 0, 0), new C{i=.4f,m=.4f,r=.85f,p=.85f,t=.5f}, new Vector3(-0.08f, 1.35f, 0.18f), new Vector3(-30, 0, 0), new C{i=.4f,m=.4f,r=.85f,p=.85f,t=.5f}, 5, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.08f, 1.25f, 0.20f), new Vector3(-45, 0, 0), new C{i=.4f,m=.4f,r=.85f,p=.85f,t=.5f}, new Vector3(-0.08f, 1.25f, 0.20f), new Vector3(-45, 0, 0), new C{i=.4f,m=.4f,r=.85f,p=.85f,t=.5f}, 8, 0, 0.15f, 0.25f)));

        // SICK — middle fingers touch forehead + stomach
        s.Add(Make("sick", 0.85f, "Middle fingertips touch forehead and stomach (8-hand).",
            Frame(new Vector3(0.05f, 1.70f, 0.22f), new Vector3(0, 0, 0), new C{i=0,m=.55f,r=0,p=0,t=.4f}, new Vector3(-0.05f, 1.10f, 0.22f), new Vector3(0, 0, 0), new C{i=0,m=.55f,r=0,p=0,t=.4f}, 10, 0, 0.12f, 0.30f)));

        // ══ DOMAIN WORDS (10) ════════════════════════════════════════════════
        // WATER — W-handshape taps chin
        s.Add(Make("water", 1.0f, "W-handshape (index+mid+ring up) taps chin.",
            Frame(new Vector3(0.08f, 1.55f, 0.30f), new Vector3(0, 0, 0), new C{i=0,m=0,r=0,p=.85f,t=.55f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(0.05f, 1.60f, 0.20f), new Vector3(0, 0, 0), new C{i=0,m=0,r=0,p=.85f,t=.55f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.15f),
            Frame(new Vector3(0.08f, 1.55f, 0.30f), new Vector3(0, 0, 0), new C{i=0,m=0,r=0,p=.85f,t=.55f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.08f, 0.15f)));

        // ROOM — both R-hands frame a square (R = index+middle crossed)
        s.Add(Make("room", 0.9f, "Both R-hands draw a square outline.",
            Frame(new Vector3(0.20f, 1.45f, 0.40f), new Vector3(0, 0, 0), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, new Vector3(-0.20f, 1.45f, 0.40f), new Vector3(0, 0, 0), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.20f, 1.20f, 0.40f), new Vector3(0, 0, 0), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, new Vector3(-0.20f, 1.20f, 0.40f), new Vector3(0, 0, 0), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, 0, 0, 0.10f, 0.25f)));

        // FRIEND — both index fingers hook together, twice
        s.Add(Make("friend", 0.9f, "Index fingers hook (R over L, then L over R).",
            Frame(new Vector3(0.05f, 1.30f, 0.40f), new Vector3(0, 0, -30), new C{i=.4f,m=.85f,r=.85f,p=.85f,t=.5f}, new Vector3(-0.05f, 1.30f, 0.40f), new Vector3(0, 0, 30), new C{i=.4f,m=.85f,r=.85f,p=.85f,t=.5f}, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.05f, 1.30f, 0.40f), new Vector3(0, 0, 30), new C{i=.4f,m=.85f,r=.85f,p=.85f,t=.5f}, new Vector3(-0.05f, 1.30f, 0.40f), new Vector3(0, 0, -30), new C{i=.4f,m=.85f,r=.85f,p=.85f,t=.5f}, 0, 0, 0.10f, 0.20f)));

        // PERSON — both P-hands move downward in parallel
        s.Add(Make("person", 0.9f, "Both flat-P hands sweep downward in parallel.",
            Frame(new Vector3(0.18f, 1.55f, 0.35f), new Vector3(0, 0, 0), new C{i=0,m=.45f,r=.85f,p=.85f,t=.4f}, new Vector3(-0.18f, 1.55f, 0.35f), new Vector3(0, 0, 0), new C{i=0,m=.45f,r=.85f,p=.85f,t=.4f}, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.18f, 1.10f, 0.35f), new Vector3(0, 0, 0), new C{i=0,m=.45f,r=.85f,p=.85f,t=.4f}, new Vector3(-0.18f, 1.10f, 0.35f), new Vector3(0, 0, 0), new C{i=0,m=.45f,r=.85f,p=.85f,t=.4f}, 0, 0, 0.10f, 0.30f)));

        // NAME — both H-hands cross, right taps on left
        s.Add(Make("name", 1.1f, "Right H-hand taps on left H-hand, twice.",
            Frame(new Vector3(0.05f, 1.35f, 0.40f), new Vector3(0, 0, -45), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, new Vector3(-0.05f, 1.30f, 0.40f), new Vector3(0, 0, 45), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(0.05f, 1.32f, 0.40f), new Vector3(0, 0, -45), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, new Vector3(-0.05f, 1.30f, 0.40f), new Vector3(0, 0, 45), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, 0, 0, 0.06f, 0.10f),
            Frame(new Vector3(0.05f, 1.35f, 0.40f), new Vector3(0, 0, -45), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, new Vector3(-0.05f, 1.30f, 0.40f), new Vector3(0, 0, 45), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, 0, 0, 0.06f, 0.10f)));

        // WHAT — both hands open, palms up, shake side-to-side (5-handshape)
        s.Add(Make("what", 1.0f, "Open 5-hands palms up, small side-shake.",
            Frame(new Vector3(0.22f, 1.30f, 0.40f), new Vector3(-90, 0, 0), C.Open, new Vector3(-0.22f, 1.30f, 0.40f), new Vector3(-90, 0, 0), C.Open, 5, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.25f, 1.30f, 0.40f), new Vector3(-90, 0, -10), C.Open, new Vector3(-0.25f, 1.30f, 0.40f), new Vector3(-90, 0, 10), C.Open, 5, 0, 0.08f, 0.15f),
            Frame(new Vector3(0.22f, 1.30f, 0.40f), new Vector3(-90, 0, 0), C.Open, new Vector3(-0.22f, 1.30f, 0.40f), new Vector3(-90, 0, 0), C.Open, 5, 0, 0.08f, 0.15f)));

        // WHERE — index finger wags side to side
        s.Add(Make("where", 1.0f, "Index up, wag side-to-side.",
            Frame(new Vector3(0.15f, 1.50f, 0.40f), new Vector3(0, -10, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 5, 0, 0.06f, 0.20f),
            Frame(new Vector3(0.20f, 1.50f, 0.40f), new Vector3(0,  20, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 5, 0, 0.05f, 0.10f),
            Frame(new Vector3(0.10f, 1.50f, 0.40f), new Vector3(0, -20, 0), C.Point, HIP_L, Vector3.zero, C.Fist, 5, 0, 0.05f, 0.10f)));

        // WHEN — left index points up, right index circles around it then taps
        s.Add(Make("when", 0.9f, "Right index circles around stationary left index, then taps.",
            Frame(new Vector3(0.10f, 1.40f, 0.45f), new Vector3(0, 0, 0), C.Point, new Vector3(-0.05f, 1.40f, 0.40f), new Vector3(0, 0, 0), C.Point, 0, 0, 0.08f, 0.25f),
            Frame(new Vector3(0.10f, 1.50f, 0.55f), new Vector3(0, 0, 0), C.Point, new Vector3(-0.05f, 1.40f, 0.40f), new Vector3(0, 0, 0), C.Point, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(-0.05f, 1.42f, 0.42f), new Vector3(0, 0, 0), C.Point, new Vector3(-0.05f, 1.40f, 0.40f), new Vector3(0, 0, 0), C.Point, 0, 0, 0.08f, 0.20f)));

        // WHY — Y-handshape (thumb+pinky) taps temple
        s.Add(Make("why", 1.0f, "Y-handshape taps temple area.",
            Frame(new Vector3(0.18f, 1.60f, 0.25f), new Vector3(0, -30, 0), new C{i=.85f,m=.85f,r=.85f,p=0,t=0}, HIP_L, Vector3.zero, C.Fist, 10, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.22f, 1.50f, 0.35f), new Vector3(0, -10, 0), new C{i=.85f,m=.85f,r=.85f,p=0,t=0}, HIP_L, Vector3.zero, C.Fist, 10, 0, 0.10f, 0.20f)));

        // HOW — both bent-V hands palm-down, rotate to palm-up
        s.Add(Make("how", 0.9f, "Both bent-V hands roll from palm-down to palm-up.",
            Frame(new Vector3(0.10f, 1.30f, 0.40f), new Vector3(0, 0, -30), new C{i=.4f,m=.4f,r=.85f,p=.85f,t=.5f}, new Vector3(-0.10f, 1.30f, 0.40f), new Vector3(0, 0, 30), new C{i=.4f,m=.4f,r=.85f,p=.85f,t=.5f}, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.10f, 1.30f, 0.40f), new Vector3(-90, 0, -30), new C{i=.4f,m=.4f,r=.85f,p=.85f,t=.5f}, new Vector3(-0.10f, 1.30f, 0.40f), new Vector3(-90, 0, 30), new C{i=.4f,m=.4f,r=.85f,p=.85f,t=.5f}, 5, 0, 0.10f, 0.30f)));

        // ══ DEMO-SPECIFIC (5) — WaterVision context ═════════════════════════
        // VR — fingerspell-style V to face (V then R)
        s.Add(Make("vr", 1.0f, "V handshape near face (approx for VR letters).",
            Frame(new Vector3(0.20f, 1.55f, 0.30f), new Vector3(0, -20, 0), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f),
            Frame(new Vector3(0.20f, 1.55f, 0.30f), new Vector3(0, -20, 0), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.20f)));

        // MODEL — both M-hands frame a small box
        s.Add(Make("model", 0.9f, "Both M-hands frame a small object.",
            Frame(new Vector3(0.15f, 1.40f, 0.45f), new Vector3(0, 0, 0), new C{i=.7f,m=.7f,r=.7f,p=.85f,t=.45f}, new Vector3(-0.15f, 1.40f, 0.45f), new Vector3(0, 0, 0), new C{i=.7f,m=.7f,r=.7f,p=.85f,t=.45f}, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.15f, 1.20f, 0.45f), new Vector3(0, 0, 0), new C{i=.7f,m=.7f,r=.7f,p=.85f,t=.45f}, new Vector3(-0.15f, 1.20f, 0.45f), new Vector3(0, 0, 0), new C{i=.7f,m=.7f,r=.7f,p=.85f,t=.45f}, 0, 0, 0.10f, 0.25f)));

        // SIMULATION — both S-fists rotate alternately (machinery)
        s.Add(Make("simulation", 1.0f, "Both S-fists rotate alternately like gears.",
            Frame(new Vector3(0.15f, 1.30f, 0.40f), new Vector3(0, 0, 0), C.Fist, new Vector3(-0.15f, 1.30f, 0.40f), new Vector3(0, 0, 0), C.Fist, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(0.15f, 1.30f, 0.40f), new Vector3(0, 0, 45), C.Fist, new Vector3(-0.15f, 1.30f, 0.40f), new Vector3(0, 0, -45), C.Fist, 0, 0, 0.08f, 0.15f),
            Frame(new Vector3(0.15f, 1.30f, 0.40f), new Vector3(0, 0, -45), C.Fist, new Vector3(-0.15f, 1.30f, 0.40f), new Vector3(0, 0, 45), C.Fist, 0, 0, 0.08f, 0.15f)));

        // SEE — V-handshape from eyes outward
        s.Add(Make("see", 1.0f, "V-hand sweeps from eyes forward.",
            Frame(new Vector3(0.10f, 1.65f, 0.20f), new Vector3(0, 0, 0), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.08f, 0.20f),
            Frame(new Vector3(0.20f, 1.55f, 0.50f), new Vector3(0, 0, 0), new C{i=0,m=0,r=.85f,p=.85f,t=.5f}, HIP_L, Vector3.zero, C.Fist, 0, 0, 0.10f, 0.25f)));

        // LEARN — right open hand "picks up" knowledge from left flat palm into forehead
        s.Add(Make("learn", 0.9f, "Right open hand from left palm to forehead (taking knowledge in).",
            Frame(new Vector3(0.00f, 1.25f, 0.40f), new Vector3(-60, 0, 0), C.Open, new Vector3(-0.05f, 1.20f, 0.40f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.10f, 0.30f),
            Frame(new Vector3(0.05f, 1.65f, 0.20f), new Vector3(-30, 0, 0), new C{i=.55f,m=.55f,r=.55f,p=.55f,t=.55f}, new Vector3(-0.05f, 1.20f, 0.40f), new Vector3(-90, 0, 0), C.Flat, 0, 0, 0.12f, 0.30f)));

        TagEmotions(s);
        TagPalmDirs(s);
        return s;
    }

    // Wave 2.7: tag signs with PalmDir (viewer-perspective palm orientation) per
    // keyframe. Values from the GPT validation pass — see ASLBubbleSettings for the
    // PalmDir → Euler table (live-tunable). Untagged frames stay PalmDir.Default and
    // fall back to the raw rightHandLocalEuler / leftHandLocalEuler authored above.
    static void TagPalmDirs(List<SignDefinition> s)
    {
        // helper: same direction on every frame of one hand
        static void AllR(SignDefinition def, PalmDir d) { for (int i = 0; i < def.frames.Length; i++) def.frames[i].palmDirRight = d; }
        static void AllL(SignDefinition def, PalmDir d) { for (int i = 0; i < def.frames.Length; i++) def.frames[i].palmDirLeft  = d; }

        for (int i = 0; i < s.Count; i++)
        {
            var def = s[i];
            switch (def.word)
            {
                case "hello": case "hi": case "bye": case "where":
                    AllR(def, PalmDir.Forward); break;
                case "please": case "sorry": case "more":
                    AllR(def, PalmDir.Inward); break;
                case "yes": case "no": case "eat": case "drink":
                    AllR(def, PalmDir.SideRight); break;
                case "what":
                    AllR(def, PalmDir.Up); break;
                case "thank-you":
                case "good":
                    // Per-keyframe transition Inward → Forward (chin-out motion).
                    if (def.frames.Length >= 1) def.frames[0].palmDirRight = PalmDir.Inward;
                    for (int k = 1; k < def.frames.Length; k++) def.frames[k].palmDirRight = PalmDir.Forward;
                    break;
                case "help":
                    // Two-handed: right fist Forward, left palm UP (the platter).
                    AllR(def, PalmDir.Forward);
                    AllL(def, PalmDir.Up);
                    break;
            }
            s[i] = def;
        }
    }

    // Wave 2: tag signs with a facial expression (real palette: smile/mouth/head/gaze).
    // Words not listed stay Neutral. Matched against the canonical lowercase word.
    static void TagEmotions(List<SignDefinition> s)
    {
        var map = new Dictionary<string, SignEmotion>
        {
            ["hello"]=SignEmotion.Greeting, ["hi"]=SignEmotion.Greeting, ["bye"]=SignEmotion.Greeting,
            ["good-morning"]=SignEmotion.Greeting, ["welcome"]=SignEmotion.Greeting,
            ["thank-you"]=SignEmotion.Positive, ["yes"]=SignEmotion.Positive, ["good"]=SignEmotion.Positive,
            ["please"]=SignEmotion.Positive, ["love"]=SignEmotion.Positive, ["happy"]=SignEmotion.Positive,
            ["nice"]=SignEmotion.Positive, ["friend"]=SignEmotion.Positive,
            ["what"]=SignEmotion.Question, ["who"]=SignEmotion.Question, ["where"]=SignEmotion.Question,
            ["when"]=SignEmotion.Question, ["why"]=SignEmotion.Question, ["how"]=SignEmotion.Question,
            ["which"]=SignEmotion.Question,
            ["big"]=SignEmotion.Emphasis, ["stop"]=SignEmotion.Emphasis, ["help"]=SignEmotion.Emphasis,
            ["important"]=SignEmotion.Emphasis, ["more"]=SignEmotion.Emphasis,
            ["wow"]=SignEmotion.Surprise, ["surprise"]=SignEmotion.Surprise,
            ["sorry"]=SignEmotion.Sad, ["sad"]=SignEmotion.Sad,
            ["no"]=SignEmotion.Negation,
        };
        for (int i = 0; i < s.Count; i++)
        {
            if (map.TryGetValue(s[i].word, out var e))
            {
                var def = s[i];
                def.emotion = e;
                s[i] = def;
            }
        }
    }

    // ── Aliases ──────────────────────────────────────────────────────────────
    static List<StringPair> BuildAliases() => new() {
        // verbs — common conjugations
        new() { from = "running", to = "go" },
        new() { from = "ran",     to = "go" },
        new() { from = "going",   to = "go" },
        new() { from = "goes",    to = "go" },
        new() { from = "coming",  to = "come" },
        new() { from = "came",    to = "come" },
        new() { from = "comes",   to = "come" },
        new() { from = "wanted",  to = "want" },
        new() { from = "wants",   to = "want" },
        new() { from = "wanting", to = "want" },
        new() { from = "needed",  to = "need" },
        new() { from = "needs",   to = "need" },
        new() { from = "having",  to = "have" },
        new() { from = "had",     to = "have" },
        new() { from = "has",     to = "have" },
        new() { from = "helping", to = "help" },
        new() { from = "helped",  to = "help" },
        new() { from = "helps",   to = "help" },
        new() { from = "stopped", to = "stop" },
        new() { from = "stops",   to = "stop" },
        new() { from = "stopping",to = "stop" },
        new() { from = "ate",     to = "eat" },
        new() { from = "eaten",   to = "eat" },
        new() { from = "eating",  to = "eat" },
        new() { from = "drank",   to = "drink" },
        new() { from = "drinking",to = "drink" },
        new() { from = "drinks",  to = "drink" },
        new() { from = "working", to = "work" },
        new() { from = "worked",  to = "work" },
        new() { from = "works",   to = "work" },
        new() { from = "seeing",  to = "see" },
        new() { from = "saw",     to = "see" },
        new() { from = "seen",    to = "see" },
        new() { from = "learning",to = "learn" },
        new() { from = "learned", to = "learn" },
        // pronouns
        new() { from = "my",      to = "i" },
        new() { from = "mine",    to = "i" },
        new() { from = "your",    to = "you" },
        new() { from = "yours",   to = "you" },
        new() { from = "our",     to = "we" },
        new() { from = "us",      to = "we" },
        new() { from = "ours",    to = "we" },
        new() { from = "them",    to = "they" },
        new() { from = "their",   to = "they" },
        new() { from = "its",     to = "it" },
        // social
        new() { from = "thanks",  to = "thank-you" },
        new() { from = "thank",   to = "thank-you" },
        new() { from = "ty",      to = "thank-you" },
        new() { from = "hey",     to = "hi" },
        new() { from = "bye-bye", to = "bye" },
        new() { from = "goodbye", to = "bye" },
        new() { from = "yeah",    to = "yes" },
        new() { from = "yep",     to = "yes" },
        new() { from = "yup",     to = "yes" },
        new() { from = "nope",    to = "no" },
        new() { from = "nah",     to = "no" },
        new() { from = "okay",    to = "ok" },
        new() { from = "alright", to = "ok" },
        // emotion
        new() { from = "great",   to = "good" },
        new() { from = "nice",    to = "good" },
        new() { from = "fine",    to = "good" },
        new() { from = "awful",   to = "bad" },
        new() { from = "terrible",to = "bad" },
        new() { from = "joyful",  to = "happy" },
        new() { from = "glad",    to = "happy" },
        new() { from = "unhappy", to = "sad" },
        new() { from = "exhausted",to = "tired" },
        new() { from = "ill",     to = "sick" },
        // domain
        new() { from = "rooms",   to = "room" },
        new() { from = "friends", to = "friend" },
        new() { from = "people",  to = "person" },
        new() { from = "persons", to = "person" },
        new() { from = "named",   to = "name" },
        new() { from = "names",   to = "name" },
        new() { from = "models",  to = "model" },
        new() { from = "sim",     to = "simulation" },
        new() { from = "simulate",to = "simulation" },
        new() { from = "virtual-reality", to = "vr" },
    };

    // ── Stoplist (never signed) ──────────────────────────────────────────────
    static List<string> BuildStoplist() => new() {
        // articles
        "the", "a", "an",
        // common copulas / aux that ASL drops
        "is", "are", "was", "were", "be", "been", "being", "am",
        "do", "does", "did", "done",
        // prepositions that are usually omitted
        "of", "to", "in", "on", "at", "for", "with", "by",
        // fillers
        "um", "uh", "like", "you-know", "well",
        // demonstratives we don't sign separately
        "this", "that", "these", "those",
        // conjunctions
        "and", "or", "but", "so",
    };
}
