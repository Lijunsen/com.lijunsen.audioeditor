using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace ypzxAudioEditor.Utility
{
    public abstract class AEComponent
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
    public abstract class AEAudioComponent : AEComponent
    {
        public AEComponentType unitType;
        public AudioMixerGroup outputMixer;
        //public string mixerName;
        public bool mute;
        public bool bypassEffects;

        public bool loop;
        public bool loopInfinite;
        public int loopTimes = 2;

        //用于在UI中隐藏或显示某些控件，与生成逻辑无关
        public bool isContianerChild;

        public bool fadeIn = false;
        public float fadeInTime;
        public FadeType fadeInType;

        public bool fadeOut = false;
        public float fadeOutTime;
        public FadeType fadeOutType;

        public float delayTime;

        public bool unloadClipWhenPlayEnd = false;
        public bool stopWhenGameObjectDestroy = false;

        public float volume = 1;
        public float pitch = 1;
        public float panStereo = 0;
        public float spatialBlend = 0;
        public float reverbZoneMix = 1;
        public float dopplerLevel = 0;
        public float spread = 0;
        public int priority = 128;

        public int limitPlayNumber = 6;


        [SerializeField]
        private float _minDistance = 1;
        [SerializeField]
        private float _maxDistance = 500;
        public List<AttenuationCurveSetting> attenuationCurveSettings = new List<AttenuationCurveSetting>();
        public List<GameParameterCurveSetting> gameParameterCurveSettings = new List<GameParameterCurveSetting>();
        [SerializeReference]
        public List<AEEffect> effectSettings = new List<AEEffect>();

        public List<StateSetting> StateSettings = new List<StateSetting>();

        public AEAudioComponent(string name, int id, AEComponentType type) : base(name, id)
        {
            unitType = type;
            attenuationCurveSettings = new List<AttenuationCurveSetting>();
            gameParameterCurveSettings = new List<GameParameterCurveSetting>();
            StateSettings = new List<StateSetting>();
        }


        public float MinDistance
        {
            get => _minDistance;
            set
            {
                if (value < 0)
                {
                    _minDistance = 0;
                    return;
                }

                if (value > MaxDistance)
                {
                    _minDistance = MaxDistance - 0.01f;
                    return;
                }
                _minDistance = value;
            }
        }

        public float MaxDistance
        {
            get => _maxDistance;
            set
            {
                if (value < _minDistance)
                {
                    _maxDistance = _minDistance + 0.01f;
                    return;
                }
                _maxDistance = value;
            }
        }

        public void ResetDistance(float minDistance, float maxDistance)
        {
            _minDistance = minDistance;
            _maxDistance = maxDistance;
        }
    }

    public enum AttenuationCurveType
    {
        OutputVolume,
        SpatialBlend,
        Spread,
        LowPass,
        ReverbZoneMix
    }

    [Serializable]
    public class AttenuationCurveSetting
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

    public enum GameParameterTargetType
    {
        Volume,
        Pitch,

    }

    [Serializable]
    public class GameParameterCurveSetting
    {
        public GameParameterTargetType targetType;
        public int gameParameterID;
        public AnimationCurve curveData;
        public float yAxisMax;
        public float yAxisMin;

        public GameParameterCurveSetting(GameParameterTargetType targetType)
        {
            this.targetType = targetType;
            gameParameterID = -1;
            curveData = AnimationCurve.Linear(0,0,1,1);
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
    public class StateSetting
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
