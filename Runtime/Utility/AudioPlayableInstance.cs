using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AudioEditor.Runtime.Utility.EasingCore;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace AudioEditor.Runtime.Utility
{
    internal sealed class AudioPlayableInstance
    {
        private PlayableGraph graph;
        private AudioEditorPlayable targetPlayable;
        public AudioPlayableOutput playableOutput;
        public AudioSource audioSource;

        public AEAudioComponent Data
        {
            get
            {
                if (targetPlayable != null)
                {
                    return targetPlayable.Data;
                }
                else
                {
                    AudioEditorDebugLog.LogError("生成错误配置的实例，TargetPlayable不能为NULL");
                    return null;
                }
            }
        }

        public string Name
        {
            get
            {
                if (targetPlayable != null)
                {
                    return targetPlayable.name;
                }
                else
                {
                    AudioEditorDebugLog.LogError("生成错误配置的实例，TargetPlayable不能为null");
                    return "";
                }
            }
        }

        public int ID
        {
            get
            {
                if (targetPlayable != null)
                {
                    return targetPlayable.id;
                }
                else
                {
                    AudioEditorDebugLog.LogError("生成错误配置的实例，TargetPlayable不能为null");
                    return -1;
                }
            }
        }

        internal AudioPlayableInstance(int componentId, ref PlayableGraph graph, AudioEditorPlayable targetPlayable, GameObject eventAttachObject, GameObject playableObject)
        {
            this.graph = graph;
            this.targetPlayable = targetPlayable;
            this.audioSource = playableObject.GetComponent<AudioSource>();
            playableOutput = AudioPlayableOutput.Create(graph, targetPlayable == null?"":targetPlayable.name, playableObject.GetComponent<AudioSource>());

            if (targetPlayable == null)
            {
                AudioEditorDebugLog.LogError("生成错误配置的实例，TargetPlayable不能为null");
                return;
            }

            var treeElement = AudioEditorManager.GetTreeElementByID<AEAudioComponent>(componentId);

            var childPlayable = targetPlayable;
            if (treeElement != null)
            {

                while (treeElement.parent != null)
                {
                    var parentElement = treeElement.parent as MyTreeElement;
                    if (parentElement == null)
                    {
                        AudioEditorDebugLog.LogError("数据错误");
                        break;
                    }
                    if (parentElement.type == AEComponentType.WorkUnit)
                    {
                        break;
                    }

                    if (parentElement.type == AEComponentType.ActorMixer)
                    {
                        var actorMixerData = AudioEditorManager.GetAEComponentDataByID<ActorMixer>(parentElement.id);
                        if (actorMixerData != null)
                        {
                            var actorMixerPlayable = new ActorMixerPlayable(actorMixerData, ref graph,childPlayable);

                            childPlayable.parentActorMixer = actorMixerPlayable;
                            childPlayable = actorMixerPlayable;
                        }
                        else
                        {
                            AudioEditorDebugLog.LogError("ActorMixer数据获取失败");
                        }
                    }

                    treeElement = parentElement;
                }
            }
            
            playableOutput.SetSourcePlayable(childPlayable.PlayableBehaviour.instance);

            playableOutput.SetUserData(eventAttachObject);
            targetPlayable.playableOutput = playableOutput;


            if (Data != null)
            {
                AudioEditorManager.AudioComponentDataChanged += SyncAudioComponentData;
                //作为子组件时AudioSource传入为null
                if (this.audioSource != null)
                {
                    SyncAudioSource();

                }
            }

            graph.Play();
        }

        public void Pause()
        {
            targetPlayable.Pause();
        }

        public void Pause(float fadeTime, EaseType easeType = EaseType.Linear)
        {
            targetPlayable.Pause(fadeTime, easeType);
        }

        public void Resume()
        {
            targetPlayable.Resume();
        }

        public void Resume(float fadeTime, EaseType easeType = EaseType.Linear)
        {
            targetPlayable.Resume(fadeTime, easeType);
        }

        public void Stop()
        {
            targetPlayable.Stop();
        }

        public void Stop(float fadeTime, EaseType easeType = EaseType.Linear, Action fadeCompletedCallBack = null)
        {
            targetPlayable.Stop(fadeTime, easeType, fadeCompletedCallBack);
        }

        public void Replay()
        {
            targetPlayable.Replay();
        }

        public void Replay(float fadeTime, EaseType easeType = EaseType.Linear)
        {
            targetPlayable.Replay(fadeTime, easeType);
        }

        public void Delay(float delayTime, Action callback)
        {
            targetPlayable.Delay(delayTime, callback);
        }

        public bool IsDone()
        {
            if (targetPlayable == null)
            {
                return true;
            }
            return targetPlayable.IsDone();
        }

        public void Destroy()
        {
            AudioEditorManager.AudioComponentDataChanged -= SyncAudioComponentData;

            targetPlayable.Destroy();
            targetPlayable = null;
            if (playableOutput.IsOutputValid())
            {
                playableOutput.SetTarget(null);
                playableOutput.SetUserData(null);
                //这行在执行淡出的stop事件时会直接导致unity崩溃，崩溃原因为“试图访问无效的地址”，具体原因未知
                //graph.DestroyOutput(playableOutput);
                playableOutput = AudioPlayableOutput.Null;
                graph.Stop();
                graph.Destroy();
            }
        }

        public bool Relate(int id)
        {
            return targetPlayable.Relate(id);
        }


        public void GetAudioClips(ref List<AudioClip> clips)
        {
            targetPlayable.GetAudioClips(ref clips);
        }

        public bool IsStopWhenGameObjectDestroy()
        {
            return GetActualData(AEComponentDataOverrideType.OtherSetting).stopWhenGameObjectDestroy;
        }

        public int GetLimitPlayNumber()
        {
            return GetActualData(AEComponentDataOverrideType.OtherSetting).limitPlayNumber;
        }

        public void GetPlayableState(out double playTime, out double durationTime, out bool isTheLastTimePlay)
        {
            targetPlayable.GetPlayState(out playTime, out durationTime, out isTheLastTimePlay);
        }

        public bool CompareType<T>() where T : AudioEditorPlayable
        {
            if (targetPlayable is T)
            {
                return true;
            }
            return false;
        }


        private void SyncAudioSource()
        {
            audioSource.playOnAwake = false;

            audioSource.outputAudioMixerGroup = GetActualData(AEComponentDataOverrideType.OutputMixerGroup).outputMixer;

            //Debug.Log(audioSource.outputAudioMixerGroup==null?"null":audioSource.outputAudioMixerGroup.name);

            audioSource.loop = Data.loop;
            audioSource.priority = Data.priority;

            //audioSource.mute = Data.mute;
            //audioSource.bypassEffects = Data.bypassEffects;

            //音量实际修改为Playable的权重
            // audiosource.volume = Data.volume;
            //测试pitch对于playable系统无效，需要调用playable的speed
            // audiosource.pitch = Data.pitch;

            SyncEffectData();
            SyncAttenuationData();
        }


        private void SyncEffectData()
        {
            if (audioSource == null || audioSource.gameObject == null || Data == null) return;
            var data = GetActualData(AEComponentDataOverrideType.Effect);

            foreach (var effectSetting in data.effectSettings)
            {
                switch (effectSetting.type)
                {
                    case AEEffectType.LowPassFilter:
                        var lowPassFilter = audioSource.gameObject.GetComponent<AudioLowPassFilter>();
                        if (lowPassFilter == null)
                        {
                            lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
                        }
                        effectSetting.SyncDataToFilter(lowPassFilter);
                        break;
                    case AEEffectType.HighPassFilter:
                        var highPassFilter = audioSource.gameObject.GetComponent<AudioHighPassFilter>();
                        if (highPassFilter == null)
                        {
                            highPassFilter = audioSource.gameObject.AddComponent<AudioHighPassFilter>();
                        }
                        effectSetting.SyncDataToFilter(highPassFilter);
                        break;
                    case AEEffectType.ReverbFilter:
                        var reverbFilter = audioSource.gameObject.GetComponent<AudioReverbFilter>();
                        if (reverbFilter == null)
                        {
                            reverbFilter = audioSource.gameObject.AddComponent<AudioReverbFilter>();
                        }
                        effectSetting.SyncDataToFilter(reverbFilter);
                        break;
                    //case AEEffectType.ReverbZoneFilter:
                    //    var reverZoneFilter = audioSource.GetComponent<AudioReverbZone>();
                    //    if (reverZoneFilter == null)
                    //    {
                    //        reverZoneFilter = audioSource.gameObject.AddComponent<AudioReverbZone>();
                    //    }
                    //    effectSetting.SyncDataToFilter(reverZoneFilter);
                    //    break;
                    case AEEffectType.EchoFilter:
                        var echoFilter = audioSource.GetComponent<AudioEchoFilter>();
                        if (echoFilter == null)
                        {
                            echoFilter = audioSource.gameObject.AddComponent<AudioEchoFilter>();
                        }
                        effectSetting.SyncDataToFilter(echoFilter);
                        break;
                    case AEEffectType.ChorusFilter:
                        var chorusFilter = audioSource.GetComponent<AudioChorusFilter>();
                        if (chorusFilter == null)
                        {
                            chorusFilter = audioSource.gameObject.AddComponent<AudioChorusFilter>();
                        }
                        effectSetting.SyncDataToFilter(chorusFilter);
                        break;
                    case AEEffectType.DistortionFilter:
                        var distortionFilter = audioSource.GetComponent<AudioDistortionFilter>();
                        if (distortionFilter == null)
                        {
                            distortionFilter = audioSource.gameObject.AddComponent<AudioDistortionFilter>();
                        }
                        effectSetting.SyncDataToFilter(distortionFilter);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }


        private void SyncAttenuationData()
        {
            var data = GetActualData(AEComponentDataOverrideType.Attenuation);

            audioSource.panStereo = data.panStereo;
            audioSource.spatialBlend = data.spatialBlend;
            audioSource.reverbZoneMix = data.reverbZoneMix;
            audioSource.dopplerLevel = data.dopplerLevel;
            audioSource.spread = data.spread;
            audioSource.minDistance = data.MinDistance;
            audioSource.maxDistance = data.MaxDistance;

            foreach (var animationCurveSetting in data.attenuationCurveSettings)
            {
                switch (animationCurveSetting.attenuationCurveType)
                {
                    case AttenuationCurveType.OutputVolume:
                        audioSource.rolloffMode = AudioRolloffMode.Custom;
                        audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, animationCurveSetting.curveData);
                        break;
                    case AttenuationCurveType.SpatialBlend:
                        audioSource.SetCustomCurve(AudioSourceCurveType.SpatialBlend, animationCurveSetting.curveData);
                        break;
                    case AttenuationCurveType.Spread:
                        audioSource.SetCustomCurve(AudioSourceCurveType.Spread, animationCurveSetting.curveData);
                        break;
                    case AttenuationCurveType.ReverbZoneMix:
                        audioSource.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, animationCurveSetting.curveData);
                        break;
                    case AttenuationCurveType.LowPass:
                        var lowPassFilter = audioSource.gameObject.GetComponent<AudioLowPassFilter>();
                        if (lowPassFilter == null)
                        {
                            lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
                        }
                        lowPassFilter.customCutoffCurve = animationCurveSetting.curveData;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }


        /// <summary>
        /// 同步编辑器中修改的数据
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dataChangeType"></param>
        /// <param name="data"></param>
        private void SyncAudioComponentData(int id, AudioComponentDataChangeType dataChangeType, object data)
        {

            switch (dataChangeType)
            {
                case AudioComponentDataChangeType.AudioClipChange:
                    if (ID != id) break;
                    if (targetPlayable is SoundSFXPlayable soundSFXPlayable)
                    {
                        soundSFXPlayable.ClipPlayable.SetClip((Data as SoundSFX).clip);
                        Replay();
                    }
                    break;
                case AudioComponentDataChangeType.ContainerSettingChange:
                    if (ID != id) break;
                    Debug.LogError("未完成");
                    break;
                case AudioComponentDataChangeType.LoopIndexChange:
                    if (ID != id) break;
                    //每当循环次数改变都更新实例的实际循环数
                    targetPlayable.PlayableBehaviour.ResetLoop();
                    break;
                case AudioComponentDataChangeType.AddEffect:
                    if (ID != id) break;
                    SyncEffectData();
                    break;
                case AudioComponentDataChangeType.DeleteEffect:
                    if (ID != id) break;
                    if (data == null)
                    {
                        Debug.LogError("[AudioEditor]: 未传递要删除的Effect类型");
                        break;
                    }
                    if (audioSource == null) break;
                    switch ((AEEffectType)data)
                    {
                        case AEEffectType.LowPassFilter:
                            AudioEditorManager.SafeDestroy(audioSource.gameObject.GetComponent<AudioLowPassFilter>());
                            break;
                        case AEEffectType.HighPassFilter:
                            AudioEditorManager.SafeDestroy(audioSource.gameObject.GetComponent<AudioHighPassFilter>());
                            break;
                        case AEEffectType.ReverbFilter:
                            AudioEditorManager.SafeDestroy(audioSource.gameObject.GetComponent<AudioReverbFilter>());
                            break;
                        //case AEEffectType.ReverbZoneFilter:
                        //    AudioEditorManager.SafeDestroy(audioSource.gameObject.GetComponent<AudioReverbZone>());
                        //    break;
                        case AEEffectType.EchoFilter:
                            AudioEditorManager.SafeDestroy(audioSource.gameObject.GetComponent<AudioEchoFilter>());
                            break;
                        case AEEffectType.ChorusFilter:
                            AudioEditorManager.SafeDestroy(audioSource.gameObject.GetComponent<AudioChorusFilter>());
                            break;
                        case AEEffectType.DistortionFilter:
                            AudioEditorManager.SafeDestroy(audioSource.gameObject.GetComponent<AudioDistortionFilter>());
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(data), data, null);
                    }
                    break;
                case AudioComponentDataChangeType.ContainerChildrenChange:
                case AudioComponentDataChangeType.SwitchContainerGroupSettingChange:
                    if (Relate(id))
                    {
                        AudioEditorManager.ReInstanatiatePlayable(this);
                    }
                    break;

                case AudioComponentDataChangeType._3DSetting:
                case AudioComponentDataChangeType.OtherSetting:
                case AudioComponentDataChangeType.General:
                default:
                    
                    if (GetRelateActorMixer(id) != null || ID == id)
                    {
                        if (audioSource != null)
                        {
                            SyncAudioSource();
                        }
                        targetPlayable.PlayableBehaviour.RefreshPlayableChain();
                        targetPlayable.SyncStateDirectly();
                        //PlayableBehaviour.SyncPlayableVolume();
                        //PlayableBehaviour.SyncPlayablePitch();

                    }
                    break;
            }
        }
        
        [Obsolete]
        private AudioMixerGroup GetActualMixerGroup()
        {
            if ((targetPlayable.Data.overrideFunctionType & AEComponentDataOverrideType.OutputMixerGroup) != AEComponentDataOverrideType.OutputMixerGroup && targetPlayable.parentActorMixer != null)
            {
                int index = 0;
                var targetActorMixer = targetPlayable.parentActorMixer;

                while ((targetActorMixer.Data.overrideFunctionType & AEComponentDataOverrideType.OutputMixerGroup) != AEComponentDataOverrideType.OutputMixerGroup && targetActorMixer.parentActorMixer != null)
                {
                    targetActorMixer = targetActorMixer.parentActorMixer;
                    index++;
                    if (index > 100)
                    {
                        AudioEditorDebugLog.LogWarning("父级ActorMixer达到上限值");
                        break;
                    }
                }

                return targetActorMixer.Data.outputMixer;
            }
            else
            {
                return targetPlayable.Data.outputMixer;
            }
        }

        private ActorMixerPlayable GetRelateActorMixer(int actorMixerId)
        {
            if (targetPlayable == null) return null;

            var componentPlayable = targetPlayable;
            while (componentPlayable.parentActorMixer != null)
            {
                if (componentPlayable.parentActorMixer.id == actorMixerId)
                {
                    return componentPlayable.parentActorMixer;
                }
                componentPlayable = componentPlayable.parentActorMixer;
            }
            return null;
        }

        /// <summary>
        /// 根据覆盖选项获取实际对应的数据
        /// </summary>
        /// <param name="dataType">获取哪一类型的数据</param>
        /// <returns>自身或者对应ActorMixer的数据</returns>
        internal AEAudioComponent GetActualData(AEComponentDataOverrideType? dataType = null)
        {
            if (dataType == null) return Data;
            var data = Data;
            if ((data.overrideFunctionType & dataType) != dataType && targetPlayable.parentActorMixer != null)
            {
                int index = 0;
                var targetActorMixer = targetPlayable.parentActorMixer;

                while ((targetActorMixer.Data.overrideFunctionType & dataType) != dataType && targetActorMixer.parentActorMixer != null)
                {
                    targetActorMixer = targetActorMixer.parentActorMixer;
                    index++;
                    if (index > 100)
                    {
                        AudioEditorDebugLog.LogWarning("父级ActorMixer达到上限值");
                        break;
                    }
                }

                data = targetActorMixer.Data;
            }

            return data;
        }


        /// <summary>
        /// 根据触发时机类型计算Playable距离接下来的节拍的时间间隔
        /// </summary>
        /// <param name="occasion">触发类型</param>
        /// <returns></returns>
        internal float GetPlayableNextBeatsTime(AEEventTriggerOccasion occasion)
        {
            double timeDifference = 0;
            if (targetPlayable is EmptyPlayable == false)
            {
                GetPlayableState(out var playTime, out var durationTime, out var isTheLastTimePlay);
                var data = GetActualData(AEComponentDataOverrideType.OtherSetting);
                if (data != null && data.tempo != 0 && data.beatsPerMeasure != 0)
                {
                    if (playTime >= 0 && playTime < durationTime)
                    {
                        var beatPerSecond = 60 / data.tempo;
                        var index = 0;
                        switch (occasion)
                        {
                            case AEEventTriggerOccasion.NextBar:
                                index = (int)((playTime - data.offset) /
                                              (beatPerSecond * data.beatsPerMeasure)) + 1;
                                timeDifference =
                                    beatPerSecond * data.beatsPerMeasure * index +
                                    data.offset - playTime;

                                break;
                            case AEEventTriggerOccasion.NextBeat:
                                index = (int)((playTime - data.offset) / beatPerSecond) + 1;
                                timeDifference =
                                    beatPerSecond * index + data.offset - playTime;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
            }
            return (float)timeDifference;
        }
    }
}
