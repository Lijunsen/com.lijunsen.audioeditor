using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioEditor.Runtime.Utility
{
    internal abstract class AEComponent
    {
        public string name;
        public int id;

        public AEComponent(string name, int id)
        {
            this.name = name;
            this.id = id;
        }

        public virtual void Init()
        {
        }
    }

    [Serializable]
    internal abstract class AEAudioComponent : AEComponent
    {
        public AEComponentType unitType;
        public AudioMixerGroup outputMixer;
        //public string mixerName;
        public bool mute;
        public bool bypassEffects;

        public bool loop;
        public bool loopInfinite;
        public int loopTimes = 2;


        /// <summary>
        /// 用于在UI中隐藏或显示某些控件，与生成逻辑无关,其值可能会未及时更新
        /// </summary>
        public bool isContianerChild;

        public bool fadeIn = false;
        public float fadeInTime;
        public EasingCore.EaseType fadeInType;

        public bool fadeOut = false;
        public float fadeOutTime;
        public EasingCore.EaseType fadeOutType;


        public bool unloadClipWhenPlayEnd = false;
        public bool stopWhenGameObjectDestroy = false;

        public float volume = 1;
        public bool randomVolume = false;
        public float minVolume = 0;
        public float maxVolume = 1;

        public float pitch = 1;
        public bool randomPitch = false;
        public float minPitch = 0;
        public float maxPitch = 1;

        public float delayTime;

        public float panStereo = 0;
        public float spatialBlend = 0;
        public float reverbZoneMix = 1;
        public float dopplerLevel = 0;
        public float spread = 0;
        public int priority = 128;

        public int limitPlayNumber = 6;

        public float tempo = 120;
        public int beatsPerMeasure = 4;
        public float offset = 0;


        [SerializeField]
        private float minDistance = 1;
        [SerializeField]
        private float maxDistance = 500;
        public List<AttenuationCurveSetting> attenuationCurveSettings;
        public List<GameParameterCurveSetting> gameParameterCurveSettings;
        [SerializeReference]
        public List<AEEffect> effectSettings;

        public List<StateSetting> stateSettings;
        
        public AEComponentDataOverrideType overrideFunctionType;

        public float MinDistance
        {
            get => minDistance;
            set
            {
                if (value < 0)
                {
                    minDistance = 0;
                    return;
                }

                if (value > MaxDistance)
                {
                    minDistance = MaxDistance - 0.01f;
                    return;
                }
                minDistance = value;
            }
        }

        public float MaxDistance
        {
            get => maxDistance;
            set
            {
                if (value < minDistance)
                {
                    maxDistance = minDistance + 0.01f;
                    return;
                }
                maxDistance = value;
            }
        }

        public AEAudioComponent(string name, int id, AEComponentType type) : base(name, id)
        {
            unitType = type;
            effectSettings = new List<AEEffect>();
            attenuationCurveSettings = new List<AttenuationCurveSetting>();
            gameParameterCurveSettings = new List<GameParameterCurveSetting>();
            stateSettings = new List<StateSetting>();
        }


        public void ResetDistance(float minDistance, float maxDistance)
        {
            this.minDistance = minDistance;
            this.maxDistance = maxDistance;
        }
    }

    [Flags]
    internal enum AEComponentDataOverrideType
    {
        OutputMixerGroup = 0b0001,
        Effect = 0b0010,
        Attenuation = 0b0100,
        OtherSetting = 0b1000
    }

    internal enum AttenuationCurveType
    {
        OutputVolume,
        SpatialBlend,
        Spread,
        LowPass,
        ReverbZoneMix
    }

    [Serializable]
    internal class AttenuationCurveSetting
    {
        public AttenuationCurveType attenuationCurveType;
        public AnimationCurve curveData;

        public AttenuationCurveSetting(AttenuationCurveType type, float startPointValue, float endPointValue)
        {
            attenuationCurveType = type;
            curveData = new AnimationCurve(new Keyframe(0, Mathf.Clamp01(startPointValue)), new Keyframe(1, Mathf.Clamp01(endPointValue)));
        }

        public AttenuationCurveSetting(AttenuationCurveType type)
        {
            attenuationCurveType = type;
            switch (type)
            {
                case AttenuationCurveType.OutputVolume:
                    // var keyframe =new Keyframe[2];
                    // keyframe[0] =  new Keyframe(0,0);
                    // keyframe[1] = new Keyframe(1,1);
                    // curveData = new AnimationCurve(keyframe);
                    curveData = AnimationCurve.Linear(0, 1, 1, 0);
                    break;
                case AttenuationCurveType.SpatialBlend:
                    curveData = AnimationCurve.Linear(0, 1, 1, 0);
                    break;
                case AttenuationCurveType.Spread:
                    curveData = AnimationCurve.Linear(0, 1, 1, 1);
                    break;
                case AttenuationCurveType.LowPass:
                    curveData = AnimationCurve.Linear(0, 0, 1, 1);
                    break;
                case AttenuationCurveType.ReverbZoneMix:
                    curveData = AnimationCurve.Linear(0, 1, 1, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }

    internal enum GameParameterTargetType
    {
        Volume,
        Pitch,

    }

    [Serializable]
    internal class GameParameterCurveSetting
    {
        public GameParameterTargetType targetType;
        public int gameParameterId;
        public AnimationCurve curveData;
        public float yAxisMax;
        public float yAxisMin;

        public GameParameterCurveSetting(GameParameterTargetType targetType, int gameParameterId)
        {
            this.targetType = targetType;
            this.gameParameterId = gameParameterId;
            curveData = AnimationCurve.Linear(0, 0, 1, 1);
            switch (targetType)
            {
                case GameParameterTargetType.Volume:
                    yAxisMax = 1;
                    yAxisMin = 0;
                    break;
                case GameParameterTargetType.Pitch:
                    yAxisMax = 3;
                    yAxisMin = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null);
            }

        }

        public float GetYxisValue(float xAixsValue)
        {
            return yAxisMin + (yAxisMax - yAxisMin) * curveData.Evaluate(xAixsValue);
        }
    }

    [System.Serializable]
    internal class StateSetting
    {
        public int stateGroupId;
        public List<float> volumeList;
        public List<float> pitchList;

        public StateSetting(StateGruop stateGoup)
        {
            stateGroupId = stateGoup.id;
            volumeList = new List<float>();
            pitchList = new List<float>();
            for (int i = 0; i < stateGoup.StateListCount; i++)
            {
                volumeList.Add(1);
                pitchList.Add(1);
            }
        }

        public void Reset(StateGruop stateGoup)
        {
            stateGroupId = stateGoup.id;
            volumeList.Clear();
            pitchList.Clear();
            for (int i = 0; i < stateGoup.StateListCount; i++)
            {
                volumeList.Add(1);
                pitchList.Add(1);
            }
        }
    }
}
