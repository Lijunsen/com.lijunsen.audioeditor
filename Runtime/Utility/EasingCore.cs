/*
 * EasingCore (https://github.com/setchi/EasingCore)
 * Copyright (c) 2019 setchi
 * Licensed under MIT (https://github.com/setchi/EasingCore/blob/master/LICENSE)
 */

using UnityEngine;

namespace AudioEditor.Runtime.Utility.EasingCore
{
    internal enum EaseType
    {
        Linear,
        //InBack,
        InBounce,
        InCirc,
        InCubic,
        //InElastic,
        InExpo,
        InQuad,
        InQuart,
        InQuint,
        InSine,
        //OutBack,
        OutBounce,
        OutCirc,
        OutCubic,
        //OutElastic,
        OutExpo,
        OutQuad,
        OutQuart,
        OutQuint,
        OutSine,
        //InOutBack,
        InOutBounce,
        InOutCirc,
        InOutCubic,
        //InOutElastic,
        InOutExpo,
        InOutQuad,
        InOutQuart,
        InOutQuint,
        InOutSine,
    }

    internal delegate float EasingFunction(float t);

    internal static class Easing
    {
        /// <summary>
        /// Gets the easing function
        /// </summary>
        /// <param name="type">Ease type</param>
        /// <returns>Easing function</returns>
        public static EasingFunction Get(EaseType type)
        {
            switch (type)
            {
                case EaseType.Linear: return Linear;
                //case EaseType.InBack: return inBack;
                case EaseType.InBounce: return InBounce;
                case EaseType.InCirc: return InCirc;
                case EaseType.InCubic: return InCubic;
                //case EaseType.InElastic: return inElastic;
                case EaseType.InExpo: return InExpo;
                case EaseType.InQuad: return InQuad;
                case EaseType.InQuart: return InQuart;
                case EaseType.InQuint: return InQuint;
                case EaseType.InSine: return InSine;
                //case EaseType.OutBack: return outBack;
                case EaseType.OutBounce: return OutBounce;
                case EaseType.OutCirc: return OutCirc;
                case EaseType.OutCubic: return OutCubic;
                //case EaseType.OutElastic: return outElastic;
                case EaseType.OutExpo: return OutExpo;
                case EaseType.OutQuad: return OutQuad;
                case EaseType.OutQuart: return OutQuart;
                case EaseType.OutQuint: return OutQuint;
                case EaseType.OutSine: return OutSine;
                //case EaseType.InOutBack: return inOutBack;
                case EaseType.InOutBounce: return InOutBounce;
                case EaseType.InOutCirc: return InOutCirc;
                case EaseType.InOutCubic: return InOutCubic;
                //case EaseType.InOutElastic: return inOutElastic;
                case EaseType.InOutExpo: return InOutExpo;
                case EaseType.InOutQuad: return InOutQuad;
                case EaseType.InOutQuart: return InOutQuart;
                case EaseType.InOutQuint: return InOutQuint;
                case EaseType.InOutSine: return InOutSine;
                default: return Linear;
            }

            float Linear(float t) => t;

            //float inBack(float t) => t * t * t - t * Mathf.Sin(t * Mathf.PI);

            //float outBack(float t) => 1f - inBack(1f - t);

            //float inOutBack(float t) =>
            //    t < 0.5f
            //        ? 0.5f * inBack(2f * t)
            //        : 0.5f * outBack(2f * t - 1f) + 0.5f;

            float InBounce(float t) => 1f - OutBounce(1f - t);

            float OutBounce(float t) =>
                t < 4f / 11.0f ?
                    (121f * t * t) / 16.0f :
                t < 8f / 11.0f ?
                    (363f / 40.0f * t * t) - (99f / 10.0f * t) + 17f / 5.0f :
                t < 9f / 10.0f ?
                    (4356f / 361.0f * t * t) - (35442f / 1805.0f * t) + 16061f / 1805.0f :
                    (54f / 5.0f * t * t) - (513f / 25.0f * t) + 268f / 25.0f;

            float InOutBounce(float t) =>
                t < 0.5f
                    ? 0.5f * InBounce(2f * t)
                    : 0.5f * OutBounce(2f * t - 1f) + 0.5f;

            float InCirc(float t) => 1f - Mathf.Sqrt(1f - (t * t));

            float OutCirc(float t) => Mathf.Sqrt((2f - t) * t);

            float InOutCirc(float t) =>
                t < 0.5f
                    ? 0.5f * (1 - Mathf.Sqrt(1f - 4f * (t * t)))
                    : 0.5f * (Mathf.Sqrt(-((2f * t) - 3f) * ((2f * t) - 1f)) + 1f);

            float InCubic(float t) => t * t * t;

            float OutCubic(float t) => InCubic(t - 1f) + 1f;

            float InOutCubic(float t) =>
                t < 0.5f
                    ? 4f * t * t * t
                    : 0.5f * InCubic(2f * t - 2f) + 1f;

            //float inElastic(float t) => Mathf.Sin(13f * (Mathf.PI * 0.5f) * t) * Mathf.Pow(2f, 10f * (t - 1f));

            //float outElastic(float t) => Mathf.Sin(-13f * (Mathf.PI * 0.5f) * (t + 1)) * Mathf.Pow(2f, -10f * t) + 1f;

            //float inOutElastic(float t) =>
            //    t < 0.5f
            //        ? 0.5f * Mathf.Sin(13f * (Mathf.PI * 0.5f) * (2f * t)) * Mathf.Pow(2f, 10f * ((2f * t) - 1f))
            //        : 0.5f * (Mathf.Sin(-13f * (Mathf.PI * 0.5f) * ((2f * t - 1f) + 1f)) * Mathf.Pow(2f, -10f * (2f * t - 1f)) + 2f);

            float InExpo(float t) => Mathf.Approximately(0.0f, t) ? t : Mathf.Pow(2f, 10f * (t - 1f));

            float OutExpo(float t) => Mathf.Approximately(1.0f, t) ? t : 1f - Mathf.Pow(2f, -10f * t);

            float InOutExpo(float v) =>
                Mathf.Approximately(0.0f, v) || Mathf.Approximately(1.0f, v)
                    ? v
                    : v < 0.5f
                        ? 0.5f * Mathf.Pow(2f, (20f * v) - 10f)
                        : -0.5f * Mathf.Pow(2f, (-20f * v) + 10f) + 1f;

            float InQuad(float t) => t * t;

            float OutQuad(float t) => -t * (t - 2f);

            float InOutQuad(float t) =>
                t < 0.5f
                    ? 2f * t * t
                    : -2f * t * t + 4f * t - 1f;

            float InQuart(float t) => t * t * t * t;

            float OutQuart(float t)
            {
                var u = t - 1f;
                return u * u * u * (1f - t) + 1f;
            }

            float InOutQuart(float t) =>
                t < 0.5f
                    ? 8f * InQuart(t)
                    : -8f * InQuart(t - 1f) + 1f;

            float InQuint(float t) => t * t * t * t * t;

            float OutQuint(float t) => InQuint(t - 1f) + 1f;

            float InOutQuint(float t) =>
                t < 0.5f
                    ? 16f * InQuint(t)
                    : 0.5f * InQuint(2f * t - 2f) + 1f;

            float InSine(float t) => Mathf.Sin((t - 1f) * (Mathf.PI * 0.5f)) + 1f;

            float OutSine(float t) => Mathf.Sin(t * (Mathf.PI * 0.5f));

            float InOutSine(float t) => 0.5f * (1f - Mathf.Cos(t * Mathf.PI));
        }
    }
}