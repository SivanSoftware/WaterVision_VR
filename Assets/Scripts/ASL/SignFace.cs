using System;
using UnityEngine;

/// <summary>
/// Facial-expression vocabulary for the ASL signing avatar, constrained to what
/// THIS Ready Player Me avatar can actually do offline (RPM servers are down, so
/// no ARKit morphs). The avatar exposes only two blendshapes — <c>mouthSmile</c>
/// and <c>mouthOpen</c> — plus eye bones (gaze) and the head bone. There is NO
/// blink, NO eyebrows, NO squint, NO jaw. Every emotion below is therefore
/// expressed only through smile + mouth-open + head tilt + a gaze bias.
/// </summary>
/// <summary>
/// Semantic palm-orientation tag per keyframe. ASL palm direction is per-sign
/// (HELLO = palm forward, PLEASE = palm inward, etc.), not a global rig setting.
/// Mapping PalmDir → Euler lives in <see cref="ASLBubbleSettings"/> so each
/// direction stays live-tunable across the whole catalog.
/// </summary>
/// <remarks>Directions are from the VIEWER's perspective:
/// Default = use the raw keyframe Euler (legacy / unauthored).
/// Forward = palm faces the viewer.   Inward = palm faces signer's body.
/// SideLeft = pinky-edge toward viewer. SideRight = thumb-edge toward viewer.
/// Up = palm faces the ceiling.       Down = palm faces the floor.</remarks>
public enum PalmDir { Default = 0, Forward, Inward, SideLeft, SideRight, Up, Down }

public enum SignEmotion
{
    Neutral = 0,   // resting face
    Greeting,      // warm smile (hello, hi, welcome)
    Positive,      // gentle smile (yes, good, thank-you, love)
    Question,      // chin up + slight mouth (what, who, where, why)
    Emphasis,      // mouth open, head forward (big, stop, help, important)
    Surprise,      // mouth open wide, head back (wow, surprise)
    Sad,           // no smile, head down (sorry, sad)
    Negation,      // neutral mouth — the "no" head-shake comes from the sign itself
}

/// <summary>
/// A concrete facial pose, in the avatar's REAL channels. Blendshape weights are
/// 0..100; <see cref="headTiltDeg"/> is a pitch bias on the head bone (+ = chin up);
/// <see cref="gazeYawBias"/> nudges the eye gaze (deg, + = toward avatar's right).
/// </summary>
[Serializable]
public struct FacePose
{
    public float smile;        // mouthSmile blendshape weight (0..100)
    public float mouthOpen;    // mouthOpen blendshape weight (0..100)
    public float headTiltDeg;  // head-bone pitch bias (+ chin up)
    public float gazeYawBias;  // eye gaze yaw bias (deg)

    public static FacePose Lerp(FacePose a, FacePose b, float t) => new FacePose
    {
        smile       = Mathf.Lerp(a.smile,       b.smile,       t),
        mouthOpen   = Mathf.Lerp(a.mouthOpen,   b.mouthOpen,   t),
        headTiltDeg = Mathf.Lerp(a.headTiltDeg, b.headTiltDeg, t),
        gazeYawBias = Mathf.Lerp(a.gazeYawBias, b.gazeYawBias, t),
    };

    public FacePose Scaled(float k) => new FacePose
    {
        smile = smile * k, mouthOpen = mouthOpen * k,
        headTiltDeg = headTiltDeg * k, gazeYawBias = gazeYawBias * k,
    };
}

/// <summary>
/// Maps each <see cref="SignEmotion"/> to a <see cref="FacePose"/> using only the
/// channels this avatar supports. Honest by design — Question/Negation lean on head
/// motion because no eyebrow blendshape exists.
/// </summary>
public static class FaceExpressionPresets
{
    public static FacePose For(SignEmotion e)
    {
        switch (e)
        {
            case SignEmotion.Greeting:  return new FacePose { smile = 70f, mouthOpen =  5f, headTiltDeg =  0f };
            case SignEmotion.Positive:  return new FacePose { smile = 55f, mouthOpen =  0f, headTiltDeg =  0f };
            case SignEmotion.Question:  return new FacePose { smile =  0f, mouthOpen = 12f, headTiltDeg =  6f };
            case SignEmotion.Emphasis:  return new FacePose { smile =  0f, mouthOpen = 35f, headTiltDeg = -3f };
            case SignEmotion.Surprise:  return new FacePose { smile = 10f, mouthOpen = 55f, headTiltDeg =  4f };  // head back
            case SignEmotion.Sad:       return new FacePose { smile =  0f, mouthOpen =  0f, headTiltDeg =  8f };  // head UP (apology in ASL)
            case SignEmotion.Negation:  return new FacePose { smile =  0f, mouthOpen =  0f, headTiltDeg =  0f };
            case SignEmotion.Neutral:
            default:                    return default;  // all zero
        }
    }
}
