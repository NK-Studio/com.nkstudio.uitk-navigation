using UnityEngine;

namespace NKStudio.UITKNavigation.Animation.Presets
{
    /// <summary>
    /// Provides UI Transition Preset Curves functionality.
    /// </summary>
    internal static class UITransitionPresetCurves
    {
        private const int BezierSamples = 24;
        private const int SpringSamples = 48;

        /// <summary>
        /// Performs the easy operation.
        /// </summary>
        internal static AnimationCurve Easy() => Bezier(0.25f, 0.1f, 0.25f, 1f);

        /// <summary>
        /// Performs the in easy operation.
        /// </summary>
        internal static AnimationCurve InEasy() => Bezier(0.42f, 0f, 1f, 1f);

        /// <summary>
        /// Performs the out easy operation.
        /// </summary>
        internal static AnimationCurve OutEasy() => Bezier(0f, 0f, 0.58f, 1f);

        /// <summary>
        /// Performs the in out easy operation.
        /// </summary>
        internal static AnimationCurve InOutEasy() => Bezier(0.42f, 0f, 0.58f, 1f);

        /// <summary>
        /// Performs the spring operation.
        /// </summary>
        internal static AnimationCurve Spring()
        {
            var keys = new Keyframe[SpringSamples + 1];
            for (int i = 0; i <= SpringSamples; i++)
            {
                float x = i / (float)SpringSamples;
                float y = EvaluateSpring(x);
                float slope = NumericSlope(x, EvaluateSpring);
                keys[i] = new Keyframe(x, y, slope, slope);
            }

            return new AnimationCurve(keys);
        }

        private static float EvaluateSpring(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return (Mathf.Sin(progress * Mathf.PI * (0.2f + 2.5f * progress * progress * progress))
                    * Mathf.Pow(1f - progress, 2.2f) + progress)
                   * (1f + 1.2f * (1f - progress));
        }

        private static float NumericSlope(float x, System.Func<float, float> function)
        {
            const float h = 1e-3f;
            float left = Mathf.Clamp01(x - h);
            float right = Mathf.Clamp01(x + h);
            float span = right - left;
            return span <= 0f ? 0f : (function(right) - function(left)) / span;
        }

        /// <summary>
        /// Performs the bezier operation.
        /// </summary>
        private static AnimationCurve Bezier(float ax, float ay, float bx, float by)
        {
            var keys = new Keyframe[BezierSamples + 1];
            for (int i = 0; i <= BezierSamples; i++)
            {
                float x = i / (float)BezierSamples;
                float t = SolveTime(x, ax, bx);
                float y = CalcBezier(t, ay, by);

                float dx = GetSlope(t, ax, bx);
                float dy = GetSlope(t, ay, by);
                float slope = Mathf.Abs(dx) < 1e-5f ? 0f : dy / dx;

                keys[i] = new Keyframe(x, y, slope, slope);
            }

            keys[0].value = 0f;
            keys[BezierSamples].value = 1f;
            return new AnimationCurve(keys);
        }

        private static float A(float a, float b) => 1f - 3f * b + 3f * a;
        private static float B(float a, float b) => 3f * b - 6f * a;
        private static float C(float a) => 3f * a;

        private static float CalcBezier(float t, float a, float b) =>
            ((A(a, b) * t + B(a, b)) * t + C(a)) * t;

        private static float GetSlope(float t, float a, float b) =>
            3f * A(a, b) * t * t + 2f * B(a, b) * t + C(a);

        /// <summary>
        /// Performs the solve time operation.
        /// </summary>
        private static float SolveTime(float x, float ax, float bx)
        {
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float slope = GetSlope(t, ax, bx);
                if (Mathf.Abs(slope) < 1e-6f)
                    break;

                float error = CalcBezier(t, ax, bx) - x;
                if (Mathf.Abs(error) < 1e-6f)
                    break;

                t -= error / slope;
            }

            return Mathf.Clamp01(t);
        }
    }
}
