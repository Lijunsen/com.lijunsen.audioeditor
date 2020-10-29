using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;

namespace ypzxAudioEditor.Utility
{
    [Obsolete]
    public class ValueSimulation
    {

        public List<ValueSubPart> valueSubPartList;

        public ValueSimulation()
        {
            valueSubPartList = new List<ValueSubPart>();
        }

        public void AddNewSubPart(float value, ValueSubPartType type, int id)
        {
            valueSubPartList.Add(new ValueSubPart(value,type,id));
        }

        public ValueSubPart GetSubPartById(int id)
        {
            return valueSubPartList.Find(x => x.id == id);
        }

        public float GetAllOutputValue()
        {
            var resultValue = 1f;
            foreach (var valueSubPart in valueSubPartList)
            {
                resultValue *= valueSubPart.currentValue;
            }
            return resultValue;
        }

        public void DoTransition(Playable playable, FrameData info)
        {
            foreach (var valueSubPart in valueSubPartList)
            {
                valueSubPart.DoTransition(playable,info);
            }
        }


        public class ValueSubPart
        {
            public int id;
            public float currentValue;
            public ValueSubPartType type;
            private TransitionState _transitionState;
            public float startValueOfTransition;
            public float targetValueOfTansition;
            public float startTransitionTime;
            public float transitionDurationTime;
            public event Action transitionCompletedCallback;

            public ValueSubPart(float value, ValueSubPartType type, int id)
            {
                this.currentValue = value;
                this.type = type;
            }

            public void StartTransition(float startValue, float targetValue, float durationTime, Action callback = null)
            {
                startValueOfTransition = startValue;
                targetValueOfTansition = targetValue;
                currentValue = startValue;
                transitionDurationTime = durationTime;
                _transitionState = TransitionState.PrepareToStart;
                transitionCompletedCallback = callback;
            }

            public void DoTransition(Playable playable, FrameData info)
            {
                if (_transitionState == TransitionState.PrepareToStart)
                {
                    startTransitionTime = (float)playable.GetTime();
                    _transitionState = TransitionState.InTransitioning;
                }

                if (_transitionState == TransitionState.InTransitioning)
                {
                    currentValue = startValueOfTransition + (targetValueOfTansition - startValueOfTransition) * 1 / transitionDurationTime * ((float)playable.GetTime() - startTransitionTime);
                }

                if (Mathf.Abs((float)playable.GetTime() - startTransitionTime) > transitionDurationTime)
                {
                    _transitionState = TransitionState.TransitionCompleted;
                    transitionCompletedCallback?.Invoke();
                }
            }

            private enum TransitionState
            {
                PrepareToStart,
                InTransitioning,
                TransitionCompleted
            }
        }

        public enum ValueSubPartType
        {
            RTPC,
            State
        }
    }




}