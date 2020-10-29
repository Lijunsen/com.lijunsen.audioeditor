using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ypzxAudioEditor.Utility
{
    public enum AEEffectType
    {
        LowPassFilter,
        HighPassFilter,
        ReverbFilter,
        ReverbZoneFilter,
        EchoFilter,
        ChorusFilter,
        DistortionFilter
    }

    [Serializable]
    public abstract class AEEffect 
    {
        public AEEffectType type;


        /// <summary>
        /// 将数据同步至一个AudioReverbFilter
        /// </summary>
        /// <param name="ARF"></param>
        public abstract void SyncDataToFilter<T>(T filter) where T : Behaviour;

        public abstract void Reset();
    }

    [Serializable]
    public class AELowPassFilter : AEEffect
    {
        [SerializeField]
        private float _cutoffFrequency = 5000;
        [SerializeField]
        private float _lowpassResonanceQ = 1;

        public AELowPassFilter()
        {
            type = AEEffectType.LowPassFilter;
            Reset();
        }

        public override void SyncDataToFilter<T>(T filter)
        {
            if (filter is AudioLowPassFilter ALPF)
            {
                ALPF.cutoffFrequency = CutoffFrequency;
                ALPF.lowpassResonanceQ = LowPassResonanceQ;
            }
        }

        public sealed override void Reset()
        {
            CutoffFrequency = 5000;
            LowPassResonanceQ = 1;
        }

        public float CutoffFrequency
        {
            get => _cutoffFrequency;
            set => _cutoffFrequency = Mathf.Clamp(value, 10, 22000);
        }

        public float LowPassResonanceQ
        {
            get => _lowpassResonanceQ;
            set => _lowpassResonanceQ = Mathf.Clamp(value, 1, 10);
        }

    }

    [Serializable]
    public class AEHighPassFilter : AEEffect
    {
        [SerializeField]
        private float _cutoffFrequency = 5000;
        [SerializeField]
        private float _highpassResonanceQ = 1;

        public AEHighPassFilter()
        {
            type = AEEffectType.HighPassFilter;
            Reset();
        }

        public override void SyncDataToFilter<T>(T filter)
        {
            if (filter is AudioHighPassFilter AHPF)
            {
                AHPF.cutoffFrequency = CutoffFrequency;
                AHPF.highpassResonanceQ = HighPassResonanceQ;
            }
        }

        public sealed override void Reset()
        {
            CutoffFrequency = 5000;
            HighPassResonanceQ = 1;
        }


        public float CutoffFrequency
        {
            get => _cutoffFrequency;
            set => _cutoffFrequency = Mathf.Clamp(value, 10, 22000);
        }

        public float HighPassResonanceQ
        {
            get => _highpassResonanceQ;
            set => _highpassResonanceQ = Mathf.Clamp(value, 1, 10);
        }
    }

    [Serializable]
    public class AEReverbFilter : AEEffect
    {
        public AudioReverbPreset preset;
        public float dryLevel;
        public float room;
        public float roomHF;
        public float roomLF;
        public float decayTime;
        public float decayHFRatio;
        public float reflectionsLevel;
        public float reflectionsDelay;
        public float reverbLevel;
        public float reverbDelay;
        public float hfReference;
        public float lfReference;
        public float diffusion;
        public float density;

        public AEReverbFilter()
        {
            type = AEEffectType.ReverbFilter;
            Reset();
        }
        /// <summary>
        /// 设置将要储存的数据
        /// </summary>
        /// <param name="ARF"></param>
        public void SetData(AudioReverbFilter ARF)
        {
            preset = ARF.reverbPreset;
            //从User切换到其他预设时dryLevel的值不会改变
            //dryLevel = ARF.dryLevel;
            room = ARF.room;
            roomHF = ARF.roomHF;
            roomLF = ARF.roomLF;
            decayTime = ARF.decayTime;
            decayHFRatio = ARF.decayHFRatio;
            reflectionsLevel = ARF.reflectionsLevel;
            //reflectionsDelay也是
            //reflectionsDelay = ARF.reflectionsDelay;
            reverbLevel = ARF.reverbLevel;
            reverbDelay = ARF.reverbDelay;
            hfReference = ARF.hfReference;
            lfReference = ARF.lfReference;
            diffusion = ARF.diffusion;
            density = ARF.density;
        }

        public override void SyncDataToFilter<T>(T filter)
        {
            if (filter is AudioReverbFilter ARF)
            {
                if (preset == AudioReverbPreset.User)
                {
                    ARF.reverbPreset = preset;
                    ARF.dryLevel = dryLevel;
                    ARF.room = room;
                    ARF.roomHF = roomHF;
                    ARF.roomLF = roomLF;
                    ARF.decayTime = decayTime;
                    ARF.decayHFRatio = decayHFRatio;
                    ARF.reflectionsLevel = reflectionsLevel;
                    ARF.reflectionsDelay = reflectionsDelay;
                    ARF.reverbLevel = reverbLevel;
                    ARF.reverbDelay = reverbDelay;
                    ARF.hfReference = hfReference;
                    ARF.lfReference = lfReference;
                    ARF.diffusion = diffusion;
                    ARF.density = density;
                }
                else
                {
                    ARF.reverbPreset = preset;
                }
            }
        }

        public sealed override void Reset()
        {
            preset = AudioReverbPreset.User;
            dryLevel = room = roomHF = roomLF = 0;
            decayTime = 1;
            decayHFRatio = 0.5f;
            reflectionsLevel = -10000;
            reflectionsDelay = reverbLevel = 0;
            reverbDelay = 0.04f;
            hfReference = 5000;
            lfReference = 250;
            diffusion = density = 100;

        }
    }

    [Serializable]
    public class AEReverbZone : AEEffect
    {
        public float minDistance;
        public float maxDistance;

        public AudioReverbPreset preset;
        public int room;
        public int roomHF;
        public int roomLF;
        public float decayTime;
        public float decayHFRatio;
        public int reflections;
        public float reflectionsDelay;
        public int reverb;
        public float reverbDelay;
        public float hfReference;
        public float lfReference;
        public float diffusion;
        public float density;

        public AEReverbZone()
        {
            type = AEEffectType.ReverbZoneFilter;
            Reset();
        }

        public void SetData(AudioReverbZone ARZ)
        {
            minDistance = ARZ.minDistance;
            maxDistance = ARZ.maxDistance;
            preset = ARZ.reverbPreset;
            room = ARZ.room;
            roomHF = ARZ.roomHF;
            roomLF = ARZ.roomLF;
            decayTime = ARZ.decayTime;
            decayHFRatio = ARZ.decayHFRatio;
            reflections = ARZ.reflections;
            reflectionsDelay = ARZ.reflectionsDelay;
            reverb = ARZ.reverb;
            reverbDelay = ARZ.reverbDelay;
            hfReference = ARZ.HFReference;
            lfReference = ARZ.LFReference;
            diffusion = ARZ.diffusion;
            density = ARZ.density;
        }

        public override void SyncDataToFilter<T>(T filter)
        {
            if (filter is AudioReverbZone ARZ)
            {
                ARZ.minDistance = minDistance;
                ARZ.minDistance = minDistance;
                ARZ.maxDistance = maxDistance;
                ARZ.reverbPreset = preset;
                ARZ.room = room;
                ARZ.roomHF = roomHF;
                ARZ.roomLF = roomLF;
                ARZ.decayTime = decayTime;
                ARZ.decayHFRatio = decayHFRatio;
                ARZ.reflections = reflections;
                ARZ.reflectionsDelay = reflectionsDelay;
                ARZ.reverb = reverb;
                ARZ.reverbDelay = reverbDelay;
                ARZ.HFReference = hfReference;
                ARZ.LFReference = lfReference;
                ARZ.diffusion = diffusion;
                ARZ.density = density;
            }
        }

        public sealed override void Reset()
        {
            minDistance = 10;
            maxDistance = 15;

            preset = AudioReverbPreset.Generic;
            room = -1000;
            roomHF = -100;
            roomLF = 0;
            decayTime = 1.49f;
            decayHFRatio = 0.83f;
            reflections = -2602;
            reflectionsDelay = 0.007f;
            reverb = 200;
            reverbDelay = 0.011f;
            hfReference = 5000;
            lfReference = 250;
            diffusion = 100;
            density = 100;
        }
    }

    [Serializable]
    public class AEEchoFilter : AEEffect
    {
        public int delay;
        public float decayRatio;
        public float wetMix;
        public float dryMix;

        public AEEchoFilter()
        {
            type = AEEffectType.EchoFilter;
            Reset();
        }

        public override void SyncDataToFilter<T>(T filter)
        {
            if (filter is AudioEchoFilter AEF)
            {
                AEF.delay = delay;
                AEF.decayRatio = decayRatio;
                AEF.wetMix = wetMix;
                AEF.dryMix = dryMix;
            }
        }

        public sealed override void Reset()
        {
            delay = 500;
            decayRatio = 0.5f;
            wetMix = 1;
            dryMix = 1;
        }
    }

    [Serializable]
    public class AEChorusFilter : AEEffect
    {
        public float dryMix;
        public float wetMix1;
        public float wetMix2;
        public float wetMix3;
        public float delay;
        public float rate;
        public float depth;

        public AEChorusFilter()
        {
            type = AEEffectType.ChorusFilter;
            Reset();
        }

        public override void SyncDataToFilter<T>(T filter)
        {
            if (filter is AudioChorusFilter ACF)
            {
                ACF.dryMix = dryMix;
                ACF.wetMix1 = wetMix1;
                ACF.wetMix2 = wetMix2;
                ACF.wetMix3 = wetMix3;
                ACF.delay = delay;
                ACF.rate = rate;
                ACF.depth = depth;
            }
        }

        public sealed override void Reset()
        {
            dryMix = 0.5f;
            wetMix1 = 0.5f;
            wetMix2 = 0.5f;
            wetMix3 = 0.5f;
            delay = 40f;
            rate = 0.8f;
            depth = 0.03f;
        }
    }


    [Serializable]
    public class AEDistortionFilter : AEEffect
    {
        public float distortionLevel;

        public AEDistortionFilter()
        {
            type = AEEffectType.DistortionFilter;
        }

        public override void SyncDataToFilter<T>(T filter)
        {
            if (filter is AudioDistortionFilter ADF)
            {
                ADF.distortionLevel = 0.5f;
            }
        }

        public override void Reset()
        {
            distortionLevel = 0.5f;
        }
    }

}