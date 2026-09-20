using UnityEngine;

namespace JuiceUp
{
    /// <summary>
    /// Utility helpers used by the UI juicing runtime.
    /// </summary>
    public static class JuiceMath
    {
        public enum CurvePreset
        {
            EaseInOut,
            EaseOutBack,
            EaseOutElastic,
            EaseOutQuad,
            EaseInQuad,
            EaseOutExpo,
            EaseInCubic,
            EaseOutCubic,
            EaseInOutCubic,
            EaseInQuart,
            EaseOutQuart,
            EaseInOutQuart,
            EaseInSine,
            EaseOutSine,
            EaseInOutSine,
            EaseInCirc,
            EaseOutCirc,
            EaseInOutCirc,
            EaseInBounce,
            EaseOutBounce,
            EaseInOutBounce,
            EaseInExpo,
            EaseInOutExpo
        }

        /// <summary>
        /// Common default ease when none is provided.
        /// </summary>
        public static AnimationCurve DefaultEase => AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>
        /// Named curve presets for quick selection.
        /// </summary>
        public static AnimationCurve GetPreset(CurvePreset preset)
        {
            switch (preset)
            {
                case CurvePreset.EaseOutBack:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 3f),
                        new Keyframe(0.7f, 1.1f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseOutElastic:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 5f),
                        new Keyframe(0.4f, 1.15f),
                        new Keyframe(0.7f, 0.95f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseOutQuad:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 2f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseInQuad:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 2f, 0f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseOutExpo:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 5f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseInCubic:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(1f, 1f, 3f, 0f));
                case CurvePreset.EaseOutCubic:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 3f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseInOutCubic:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.5f, 0.5f, 1.5f, 1.5f),
                        new Keyframe(1f, 1f, 0f, 0f));
                case CurvePreset.EaseInQuart:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(1f, 1f, 4f, 0f));
                case CurvePreset.EaseOutQuart:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 4f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseInOutQuart:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.5f, 0.5f, 2f, 2f),
                        new Keyframe(1f, 1f, 0f, 0f));
                case CurvePreset.EaseInSine:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 1.5708f, 0f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseOutSine:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 1.5708f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseInOutSine:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0.7854f, 0f),
                        new Keyframe(0.5f, 0.5f, 0.7854f, 0.7854f),
                        new Keyframe(1f, 1f, 0f, 0.7854f));
                case CurvePreset.EaseInCirc:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.2f, 0.02f, 0.2f, 0f),
                        new Keyframe(0.4f, 0.08f, 0.4f, 0f),
                        new Keyframe(0.6f, 0.2f, 0.6f, 0f),
                        new Keyframe(0.8f, 0.4f, 0.8f, 0f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseOutCirc:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.2f, 0.6f),
                        new Keyframe(0.4f, 0.8f),
                        new Keyframe(0.6f, 0.92f),
                        new Keyframe(0.8f, 0.98f),
                        new Keyframe(1f, 1f, 0f, 0f));
                case CurvePreset.EaseInOutCirc:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.5f, 0.5f, 1f, 1f),
                        new Keyframe(1f, 1f, 0f, 0f));
                case CurvePreset.EaseInBounce:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.1f, 0.01f),
                        new Keyframe(0.2f, 0.04f),
                        new Keyframe(0.3f, 0.11f),
                        new Keyframe(0.4f, 0.25f),
                        new Keyframe(0.5f, 0.44f),
                        new Keyframe(0.6f, 0.64f),
                        new Keyframe(0.7f, 0.81f),
                        new Keyframe(0.8f, 0.93f),
                        new Keyframe(0.9f, 0.99f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseOutBounce:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.1f, 0.01f),
                        new Keyframe(0.2f, 0.07f),
                        new Keyframe(0.3f, 0.19f),
                        new Keyframe(0.4f, 0.36f),
                        new Keyframe(0.5f, 0.56f),
                        new Keyframe(0.6f, 0.75f),
                        new Keyframe(0.7f, 0.89f),
                        new Keyframe(0.8f, 0.96f),
                        new Keyframe(0.9f, 0.99f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseInOutBounce:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.2f, 0.02f, 0.1f, 0f),
                        new Keyframe(0.4f, 0.12f, 0.3f, 0f),
                        new Keyframe(0.5f, 0.5f, 0.5f, 0.5f),
                        new Keyframe(0.6f, 0.88f, 0f, 0.3f),
                        new Keyframe(0.8f, 0.98f, 0f, 0.1f),
                        new Keyframe(1f, 1f, 0f, 0f));
                case CurvePreset.EaseInExpo:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.1f, 0.001f, 0.01f, 0f),
                        new Keyframe(0.2f, 0.004f, 0.04f, 0f),
                        new Keyframe(0.3f, 0.012f, 0.12f, 0f),
                        new Keyframe(0.4f, 0.031f, 0.31f, 0f),
                        new Keyframe(0.5f, 0.062f, 0.62f, 0f),
                        new Keyframe(0.6f, 0.125f, 1.25f, 0f),
                        new Keyframe(0.7f, 0.25f, 2.5f, 0f),
                        new Keyframe(0.8f, 0.5f, 5f, 0f),
                        new Keyframe(1f, 1f));
                case CurvePreset.EaseInOutExpo:
                    return new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 0f),
                        new Keyframe(0.2f, 0.006f, 0.06f, 0f),
                        new Keyframe(0.4f, 0.031f, 0.31f, 0f),
                        new Keyframe(0.5f, 0.5f, 2.5f, 2.5f),
                        new Keyframe(0.6f, 0.969f, 0f, 0.31f),
                        new Keyframe(0.8f, 0.994f, 0f, 0.06f),
                        new Keyframe(1f, 1f, 0f, 0f));
                default:
                    return DefaultEase;
            }
        }

        /// <summary>
        /// Random preset helper for quick experimentation.
        /// </summary>
        public static AnimationCurve RandomPresetCurve()
        {
            var values = (CurvePreset[])System.Enum.GetValues(typeof(CurvePreset));
            var pick = values[Random.Range(0, values.Length)];
            return GetPreset(pick);
        }

        /// <summary>
        /// Safely evaluate an AnimationCurve with clamped input.
        /// </summary>
        public static float Evaluate(AnimationCurve curve, float t)
        {
            if (curve == null || curve.length == 0)
                return Mathf.Clamp01(t);

            return curve.Evaluate(Mathf.Clamp01(t));
        }

        /// <summary>
        /// Simple critically damped spring-like response in [0,1].
        /// </summary>
        public static float Spring01(float t, float damping = 0.35f, float frequency = 8f)
        {
            t = Mathf.Clamp01(t);
            var omega = Mathf.PI * 2f * frequency;
            var decay = Mathf.Exp(-damping * omega * t);
            return 1f - decay * Mathf.Cos((1f - damping) * omega * t);
        }

        /// <summary>
        /// Clamp and randomize a duration within provided min/max bounds.
        /// </summary>
        public static float RandomDuration(float min, float max, float clampMin, float clampMax)
        {
            var d = Random.Range(min, max);
            return Mathf.Clamp(d, clampMin, clampMax);
        }

        /// <summary>
        /// Applies variation jitter to an element's offsets and timings.
        /// </summary>
        public static void ApplyVariation(UiJuiceAnimator.JuiceElement e, float durationMin, float durationMax)
        {
            if (e == null)
                return;

            float posVar = Random.Range(0.85f, 1.25f);
            float rotVar = Random.Range(0.85f, 1.25f);
            float scaleVar = Random.Range(0.85f, 1.2f);
            float fadeVar = Random.Range(0.7f, 1.2f);
            float delayVar = Random.Range(0.7f, 1.2f);
            float durVar = Random.Range(0.9f, 1.15f);
            float curveVar = Random.Range(0.85f, 1.25f);

            e.positionOffset *= posVar;
            e.rotationOffset *= rotVar;
            e.scaleFrom = Vector3.one + (e.scaleFrom - Vector3.one) * scaleVar;
            e.fadeFrom *= fadeVar;
            e.delay *= delayVar;
            e.duration = Mathf.Clamp(e.duration * durVar, durationMin, durationMax);
            e.curveAmplitude *= curveVar;

            if (Random.value < 0.25f)
                e.ease = RandomPresetCurve();
        }

        /// <summary>
        /// Apply intensity scaling to offsets, scale deltas, fade, curve amplitude and duration.
        /// </summary>
        public static void ApplyIntensity(UiJuiceAnimator.JuiceElement e, float intensity, float durationMin, float durationMax)
        {
            if (e == null)
                return;

            float power = Mathf.Max(0.1f, intensity);

            e.positionOffset *= power;
            e.rotationOffset *= power;
            e.scaleFrom = Vector3.one + (e.scaleFrom - Vector3.one) * power;
            e.fadeFrom *= Mathf.Clamp01(power);
            e.curveAmplitude *= power;

            float durationScale = power >= 1f
                ? Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(power - 1f))
                : Mathf.Lerp(1f, 1.3f, Mathf.Clamp01(1f - power));

            e.duration = Mathf.Clamp(e.duration * durationScale, durationMin, durationMax);
        }

        /// <summary>
        /// Evaluate a curved path between two positions.
        /// </summary>
        public static Vector2 EvaluateCurvePosition(Vector2 startPos, Vector2 endPos, float eased, bool useCurve, float curveAmplitude)
        {
            var basePos = Vector2.LerpUnclamped(startPos, endPos, eased);
            if (!useCurve || Mathf.Abs(curveAmplitude) < 0.001f)
                return basePos;

            var dir = endPos - startPos;
            if (dir.sqrMagnitude < 0.0001f)
                return basePos;

            var perp = new Vector2(-dir.y, dir.x).normalized;
            return basePos + perp * (curveAmplitude * Mathf.Sin(Mathf.PI * eased));
        }

        /// <summary>
        /// Compute shake jitter offsets (position and rotation) using Perlin noise.
        /// </summary>
        public static void SampleShake(UiJuiceAnimator.JuiceElement e, float time, out Vector2 posOffset, out float rotOffset)
        {
            posOffset = Vector2.zero;
            rotOffset = 0f;
            if (e == null || !e.useShakeJitter || (e.shakeMagnitude <= 0f && e.shakeRotationMagnitude <= 0f))
                return;

            float tShake = time * e.shakeFrequency + e.shakeSeed;
            posOffset.x = (Mathf.PerlinNoise(tShake, 0.123f) - 0.5f) * 2f * e.shakeMagnitude;
            posOffset.y = (Mathf.PerlinNoise(0.456f, tShake) - 0.5f) * 2f * e.shakeMagnitude;

            if (e.shakeRotationMagnitude > 0f)
            {
                rotOffset = (Mathf.PerlinNoise(tShake, tShake * 0.37f) - 0.5f) * 2f * e.shakeRotationMagnitude;
            }
        }

        // ============================================
        // Additional Easing Functions
        // ============================================

        /// <summary>
        /// Ease in cubic interpolation.
        /// </summary>
        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>
        /// Ease out cubic interpolation.
        /// </summary>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float f = t - 1f;
            return f * f * f + 1f;
        }

        /// <summary>
        /// Ease in-out cubic interpolation.
        /// </summary>
        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        /// <summary>
        /// Ease in quart interpolation.
        /// </summary>
        public static float EaseInQuart(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * t;
        }

        /// <summary>
        /// Ease out quart interpolation.
        /// </summary>
        public static float EaseOutQuart(float t)
        {
            t = Mathf.Clamp01(t);
            float f = t - 1f;
            return 1f - f * f * f * f;
        }

        /// <summary>
        /// Ease in-out quart interpolation.
        /// </summary>
        public static float EaseInOutQuart(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;
        }

        /// <summary>
        /// Ease in sine interpolation.
        /// </summary>
        public static float EaseInSine(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Cos((t * Mathf.PI) / 2f);
        }

        /// <summary>
        /// Ease out sine interpolation.
        /// </summary>
        public static float EaseOutSine(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin((t * Mathf.PI) / 2f);
        }

        /// <summary>
        /// Ease in-out sine interpolation.
        /// </summary>
        public static float EaseInOutSine(float t)
        {
            t = Mathf.Clamp01(t);
            return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
        }

        /// <summary>
        /// Bounce ease out interpolation.
        /// </summary>
        public static float EaseOutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
            {
                return n1 * t * t;
            }
            else if (t < 2f / d1)
            {
                return n1 * (t -= 1.5f / d1) * t + 0.75f;
            }
            else if (t < 2.5f / d1)
            {
                return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            }
            else
            {
                return n1 * (t -= 2.625f / d1) * t + 0.984375f;
            }
        }

        // ============================================
        // Vector Utilities
        // ============================================

        /// <summary>
        /// Smoothly interpolate between two Vector2 values with easing.
        /// </summary>
        public static Vector2 LerpEased(Vector2 a, Vector2 b, float t, AnimationCurve curve = null)
        {
            float eased = curve != null ? Evaluate(curve, t) : t;
            return Vector2.LerpUnclamped(a, b, eased);
        }

        /// <summary>
        /// Smoothly interpolate between two Vector3 values with easing.
        /// </summary>
        public static Vector3 LerpEased(Vector3 a, Vector3 b, float t, AnimationCurve curve = null)
        {
            float eased = curve != null ? Evaluate(curve, t) : t;
            return Vector3.LerpUnclamped(a, b, eased);
        }

        /// <summary>
        /// Rotate a Vector2 by degrees around origin.
        /// </summary>
        public static Vector2 RotateVector2(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }

        /// <summary>
        /// Get perpendicular vector to the given direction.
        /// </summary>
        public static Vector2 GetPerpendicular(Vector2 direction)
        {
            return new Vector2(-direction.y, direction.x).normalized;
        }

        /// <summary>
        /// Calculate distance between two Vector2 positions.
        /// </summary>
        public static float Distance2D(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(a, b);
        }

        /// <summary>
        /// Normalize a Vector2 safely (returns zero if magnitude is too small).
        /// </summary>
        public static Vector2 SafeNormalize(Vector2 v, float epsilon = 0.0001f)
        {
            float mag = v.magnitude;
            return mag > epsilon ? v / mag : Vector2.zero;
        }

        // ============================================
        // Color Utilities
        // ============================================

        /// <summary>
        /// Interpolate between two colors with easing.
        /// </summary>
        public static Color LerpColorEased(Color a, Color b, float t, AnimationCurve curve = null)
        {
            float eased = curve != null ? Evaluate(curve, t) : t;
            return Color.LerpUnclamped(a, b, eased);
        }

        /// <summary>
        /// Multiply color brightness by a factor.
        /// </summary>
        public static Color Brighten(Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a
            );
        }

        /// <summary>
        /// Darken color by a factor.
        /// </summary>
        public static Color Darken(Color color, float factor)
        {
            return new Color(
                color.r * factor,
                color.g * factor,
                color.b * factor,
                color.a
            );
        }

        /// <summary>
        /// Set color alpha while preserving RGB.
        /// </summary>
        public static Color SetAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        /// <summary>
        /// Convert color to grayscale.
        /// </summary>
        public static Color ToGrayscale(Color color)
        {
            float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return new Color(gray, gray, gray, color.a);
        }

        // ============================================
        // Animation Curve Utilities
        // ============================================

        /// <summary>
        /// Create a bounce curve with specified bounces.
        /// </summary>
        public static AnimationCurve CreateBounceCurve(int bounces = 3, float bounceHeight = 0.3f)
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0f);
            
            float step = 1f / (bounces + 1);
            for (int i = 1; i <= bounces; i++)
            {
                float t = step * i;
                float height = bounceHeight * (1f - t * 0.5f);
                curve.AddKey(t, 1f + height);
            }
            
            curve.AddKey(1f, 1f);
            return curve;
        }

        /// <summary>
        /// Create an elastic curve with specified elasticity.
        /// </summary>
        public static AnimationCurve CreateElasticCurve(float elasticity = 0.5f)
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0f);
            curve.AddKey(0.3f, 1f + elasticity);
            curve.AddKey(0.6f, 1f - elasticity * 0.5f);
            curve.AddKey(1f, 1f);
            return curve;
        }

        /// <summary>
        /// Reverse an animation curve (mirror it).
        /// </summary>
        public static AnimationCurve ReverseCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
                return DefaultEase;

            AnimationCurve reversed = new AnimationCurve();
            for (int i = 0; i < curve.length; i++)
            {
                Keyframe key = curve[i];
                reversed.AddKey(1f - key.time, 1f - key.value);
            }
            return reversed;
        }

        /// <summary>
        /// Scale curve values by a multiplier.
        /// </summary>
        public static AnimationCurve ScaleCurve(AnimationCurve curve, float scale)
        {
            if (curve == null || curve.length == 0)
                return DefaultEase;

            AnimationCurve scaled = new AnimationCurve();
            for (int i = 0; i < curve.length; i++)
            {
                Keyframe key = curve[i];
                scaled.AddKey(key.time, key.value * scale);
            }
            return scaled;
        }

        // ============================================
        // Timing Utilities
        // ============================================

        /// <summary>
        /// Calculate stagger delay for element at index.
        /// </summary>
        public static float CalculateStagger(int index, float staggerAmount)
        {
            return index * Mathf.Max(0f, staggerAmount);
        }

        /// <summary>
        /// Normalize time value to 0-1 range based on start and duration.
        /// </summary>
        public static float NormalizeTime(float currentTime, float startTime, float duration)
        {
            if (duration <= 0f)
                return 1f;
            
            float elapsed = currentTime - startTime;
            return Mathf.Clamp01(elapsed / duration);
        }

        /// <summary>
        /// Check if time is within animation range.
        /// </summary>
        public static bool IsTimeInRange(float time, float startTime, float duration)
        {
            return time >= startTime && time <= startTime + duration;
        }

        /// <summary>
        /// Calculate ping-pong value (0 to 1 and back).
        /// </summary>
        public static float PingPong(float t, float length = 1f)
        {
            return Mathf.PingPong(t, length) / length;
        }

        /// <summary>
        /// Calculate repeat value (loops from 0 to 1).
        /// </summary>
        public static float Repeat(float t, float length = 1f)
        {
            return Mathf.Repeat(t, length) / length;
        }

        // ============================================
        // Math Helpers
        // ============================================

        /// <summary>
        /// Smooth step interpolation (smoother than linear).
        /// </summary>
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            x = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// Smoother step interpolation (even smoother).
        /// </summary>
        public static float SmootherStep(float edge0, float edge1, float x)
        {
            x = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }

        /// <summary>
        /// Map value from one range to another.
        /// </summary>
        public static float MapRange(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }

        /// <summary>
        /// Clamp value to range.
        /// </summary>
        public static float ClampRange(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Linear interpolation with unclamped result.
        /// </summary>
        public static float LerpUnclamped(float a, float b, float t)
        {
            return Mathf.LerpUnclamped(a, b, t);
        }

        /// <summary>
        /// Inverse linear interpolation.
        /// </summary>
        public static float InverseLerp(float a, float b, float value)
        {
            return Mathf.InverseLerp(a, b, value);
        }

        // ============================================
        // Oscillation Functions
        // ============================================

        /// <summary>
        /// Sine wave oscillation.
        /// </summary>
        public static float OscillateSine(float time, float frequency = 1f, float amplitude = 1f)
        {
            return Mathf.Sin(time * frequency * Mathf.PI * 2f) * amplitude;
        }

        /// <summary>
        /// Cosine wave oscillation.
        /// </summary>
        public static float OscillateCosine(float time, float frequency = 1f, float amplitude = 1f)
        {
            return Mathf.Cos(time * frequency * Mathf.PI * 2f) * amplitude;
        }

        /// <summary>
        /// Triangle wave oscillation.
        /// </summary>
        public static float OscillateTriangle(float time, float frequency = 1f, float amplitude = 1f)
        {
            float t = Mathf.Repeat(time * frequency, 1f);
            return (t < 0.5f ? t * 4f - 1f : 3f - t * 4f) * amplitude;
        }

        /// <summary>
        /// Square wave oscillation.
        /// </summary>
        public static float OscillateSquare(float time, float frequency = 1f, float amplitude = 1f)
        {
            float t = Mathf.Repeat(time * frequency, 1f);
            return (t < 0.5f ? 1f : -1f) * amplitude;
        }

        // ============================================
        // Bezier Curve Utilities
        // ============================================

        /// <summary>
        /// Calculate point on quadratic Bezier curve.
        /// </summary>
        public static Vector2 BezierQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        /// <summary>
        /// Calculate point on cubic Bezier curve.
        /// </summary>
        public static Vector2 BezierCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            float u2 = u * u;
            float u3 = u2 * u;
            float t2 = t * t;
            float t3 = t2 * t;
            return u3 * p0 + 3f * u2 * t * p1 + 3f * u * t2 * p2 + t3 * p3;
        }

        // ============================================
        // UI-Specific Calculations
        // ============================================

        /// <summary>
        /// Calculate screen-space position from world position.
        /// </summary>
        public static Vector2 WorldToScreenSpace(Vector3 worldPos, Camera cam = null)
        {
            if (cam == null)
                cam = Camera.main;
            
            if (cam == null)
                return Vector2.zero;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            return new Vector2(screenPos.x, screenPos.y);
        }

        /// <summary>
        /// Calculate normalized position in rect (0-1 range).
        /// </summary>
        public static Vector2 NormalizeRectPosition(RectTransform rect, Vector2 localPos)
        {
            Rect rectBounds = rect.rect;
            return new Vector2(
                Mathf.InverseLerp(rectBounds.xMin, rectBounds.xMax, localPos.x),
                Mathf.InverseLerp(rectBounds.yMin, rectBounds.yMax, localPos.y)
            );
        }

        /// <summary>
        /// Get rect center position.
        /// </summary>
        public static Vector2 GetRectCenter(RectTransform rect)
        {
            return rect.rect.center;
        }

        /// <summary>
        /// Calculate rect size.
        /// </summary>
        public static Vector2 GetRectSize(RectTransform rect)
        {
            return rect.rect.size;
        }

        // ============================================
        // Noise and Random Utilities
        // ============================================

        /// <summary>
        /// Generate Perlin noise value for 2D position.
        /// </summary>
        public static float Noise2D(float x, float y)
        {
            return Mathf.PerlinNoise(x, y);
        }

        /// <summary>
        /// Generate random Vector2 within circle radius.
        /// </summary>
        public static Vector2 RandomInCircle(float radius)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(0f, radius);
            return new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );
        }

        /// <summary>
        /// Generate random Vector2 on circle perimeter.
        /// </summary>
        public static Vector2 RandomOnCircle(float radius)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }

        /// <summary>
        /// Random sign (-1 or 1).
        /// </summary>
        public static float RandomSign()
        {
            return Random.value < 0.5f ? -1f : 1f;
        }

        /// <summary>
        /// Random value with bias toward center.
        /// </summary>
        public static float RandomBiased(float min, float max, float bias = 0.5f)
        {
            float t = Mathf.Pow(Random.value, bias);
            return Mathf.Lerp(min, max, t);
        }

        // ============================================
        // Angle and Rotation Utilities
        // ============================================

        /// <summary>
        /// Calculate angle between two Vector2 directions.
        /// </summary>
        public static float AngleBetween(Vector2 from, Vector2 to)
        {
            return Vector2.Angle(from, to);
        }

        /// <summary>
        /// Rotate angle towards target angle.
        /// </summary>
        public static float RotateTowards(float current, float target, float maxDelta)
        {
            float delta = Mathf.DeltaAngle(current, target);
            if (Mathf.Abs(delta) <= maxDelta)
                return target;
            return current + Mathf.Sign(delta) * maxDelta;
        }

        /// <summary>
        /// Normalize angle to -180 to 180 range.
        /// </summary>
        public static float NormalizeAngle(float angle)
        {
            return Mathf.DeltaAngle(0f, angle);
        }

        /// <summary>
        /// Convert degrees to radians.
        /// </summary>
        public static float DegToRad(float degrees)
        {
            return degrees * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Convert radians to degrees.
        /// </summary>
        public static float RadToDeg(float radians)
        {
            return radians * Mathf.Rad2Deg;
        }
    }
}
