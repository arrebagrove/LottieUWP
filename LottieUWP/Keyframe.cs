﻿using System.Collections.Generic;
using Windows.Data.Json;

namespace LottieUWP
{
    public interface IKeyframe<out T>
    {
        T StartValue { get; }
        T EndValue { get; }
        float? StartFrame { get; }
        float? EndFrame { get; set; }
        float StartProgress { get; }
        bool ContainsProgress(float progress);
        bool Static { get; }
        float EndProgress { get; }
        IInterpolator Interpolator { get; }
    }

    public class Keyframe<T> : IKeyframe<T>
    {
        /// <summary>
        /// Some animations get exported with insane cp values in the tens of thousands.
        /// PathInterpolator fails to create the interpolator in those cases and hangs.
        /// Clamping the cp helps prevent that.
        /// </summary>
        private const float MaxCpValue = 100;
        private static readonly IInterpolator LinearInterpolator = new LinearInterpolator();

        /// <summary>
        /// The json doesn't include end frames. The data can be taken from the start frame of the next
        /// keyframe though.
        /// </summary>
        internal static void SetEndFrames<TU, TV>(IList<TU> keyframes) where TU : IKeyframe<TV>
        {
            int size = keyframes.Count;
            for (int i = 0; i < size - 1; i++)
            {
                // In the json, the value only contain their starting frame.
                keyframes[i].EndFrame = keyframes[i + 1].StartFrame;
            }
            var lastKeyframe = keyframes[size - 1];
            if (lastKeyframe.StartValue == null)
            {
                // The only purpose the last keyframe has is to provide the end frame of the previous
                // keyframe.
                //noinspection SuspiciousMethodCalls
                keyframes.Remove(lastKeyframe);
            }
        }

        private readonly LottieComposition _composition;
        public T StartValue { get; }
        public T EndValue { get; }
        public IInterpolator Interpolator { get; }
        public float? StartFrame { get; }
        public float? EndFrame { get; set; }

        public Keyframe(LottieComposition composition, T startValue, T endValue, IInterpolator interpolator, float? startFrame, float? endFrame)
        {
            _composition = composition;
            StartValue = startValue;
            EndValue = endValue;
            Interpolator = interpolator;
            StartFrame = startFrame;
            EndFrame = endFrame;
        }

        public virtual float StartProgress => StartFrame.Value / _composition.DurationFrames;

        public virtual float EndProgress => EndFrame == null ? 1f : EndFrame.Value / _composition.DurationFrames;

        public virtual bool Static => Interpolator == null;

        public virtual bool ContainsProgress(float progress)
        {
            return progress >= StartProgress && progress <= EndProgress;
        }

        public override string ToString()
        {
            return "Keyframe{" + "startValue=" + StartValue + ", endValue=" + EndValue + ", startFrame=" + StartFrame + ", endFrame=" + EndFrame + ", interpolator=" + Interpolator + '}';
        }

        internal class KeyFrameFactory
        {
            internal static Keyframe<T> NewInstance(JsonObject json, LottieComposition composition, float scale, IAnimatableValueFactory<T> valueFactory)
            {
                PointF cp1 = null;
                PointF cp2 = null;
                float startFrame = 0;
                T startValue = default(T);
                T endValue = default(T);
                IInterpolator interpolator = null;

                if (json.ContainsKey("t"))
                {
                    startFrame = (float)json.GetNamedNumber("t", 0);
                    var startValueJson = json.GetNamedArray("s", null);
                    if (startValueJson != null)
                    {
                        startValue = valueFactory.ValueFromObject(startValueJson, scale);
                    }

                    var endValueJson = json.GetNamedArray("e", null);
                    if (endValueJson != null)
                    {
                        endValue = valueFactory.ValueFromObject(endValueJson, scale);
                    }

                    var cp1Json = json.GetNamedObject("o", null);
                    var cp2Json = json.GetNamedObject("i", null);
                    if (cp1Json != null && cp2Json != null)
                    {
                        cp1 = JsonUtils.PointFromJsonObject(cp1Json, scale);
                        cp2 = JsonUtils.PointFromJsonObject(cp2Json, scale);
                    }

                    bool hold = (int)json.GetNamedNumber("h", 0) == 1;

                    if (hold)
                    {
                        endValue = startValue;
                        // TODO: create a HoldInterpolator so progress changes don't invalidate.
                        interpolator = LinearInterpolator;
                    }
                    else if (cp1 != null)
                    {
                        cp1 = new PointF(MiscUtils.Clamp(cp1.X, -scale, scale),
                            MiscUtils.Clamp(cp1.Y, -MaxCpValue, MaxCpValue));
                        cp2 = new PointF(MiscUtils.Clamp(cp2.X, -scale, scale),
                            MiscUtils.Clamp(cp2.Y, -MaxCpValue, MaxCpValue));
                        interpolator = new PathInterpolator(cp1.X / scale, cp1.Y / scale, cp2.X / scale, cp2.Y / scale);
                    }
                    else
                    {
                        interpolator = LinearInterpolator;
                    }
                }
                else
                {
                    startValue = valueFactory.ValueFromObject(json, scale);
                    endValue = startValue;
                }
                return new Keyframe<T>(composition, startValue, endValue, interpolator, startFrame, null);
            }

            internal static IList<IKeyframe<T>> ParseKeyframes(JsonArray json, LottieComposition composition, float scale, IAnimatableValueFactory<T> valueFactory)
            {
                int length = json.Count;
                if (length == 0)
                {
                    return new List<IKeyframe<T>>();
                }
                IList<IKeyframe<T>> keyframes = new List<IKeyframe<T>>();
                for (uint i = 0; i < length; i++)
                {
                    keyframes.Add(NewInstance(json.GetObjectAt(i), composition, scale, valueFactory));
                }

                SetEndFrames<IKeyframe<T>, T>(keyframes);
                return keyframes;
            }
        }
    }
}