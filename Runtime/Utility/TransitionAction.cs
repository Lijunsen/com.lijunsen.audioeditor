using System;
using System.Collections.Generic;
using UnityEngine;
using ypzxAudioEditor.Utility.EasingCore;

namespace ypzxAudioEditor.Utility
{
    public static class TransitionAction
    {
        private static readonly Dictionary<object, TransitionActionUnit> _actionDictionary = new Dictionary<object, TransitionActionUnit>();

        public static void OnUpdate(object target,float duration,float startValue,float endValue,ref Action<float> iterator,Action<float> updateFunc,Action completedFunc = null, EaseType easeType = EaseType.Linear)
        {
            _actionDictionary.TryGetValue(target,out var targetAction);
            if (targetAction != null)
            {
                targetAction.EndEvaluate();
                _actionDictionary.Remove(target);
            }
            var newTransitionActionUnit = new TransitionActionUnit();
            _actionDictionary.Add(target, newTransitionActionUnit);
            completedFunc += () =>
            {
                _actionDictionary.Remove(target);
            };
            newTransitionActionUnit.OnUpdate(duration,startValue,endValue,ref iterator,updateFunc, completedFunc, easeType);

        }

        public static void EndTransition(object target)
        {
            _actionDictionary.TryGetValue(target, out  var transitionAction);
            if (transitionAction != null)
            {
                transitionAction.EndEvaluate();
                _actionDictionary.Remove(target);
            }
        }
    }


    internal class TransitionActionUnit
    {
        //private object Data { get; }
        //private object targetValue;
        private float _process = 0;
        private float _duration;
        private float _startValue;
        private float _endValue;
        private EaseType _easeType;
        private float Process
        {
            get => _process;
            set => _process = Mathf.Clamp01(value);
        }
        private event Action<float> IteratorInternal;
        private event Action<float> Action;
        private event Action TransitionComplete;

        public void OnUpdate(float duration, float startValue, float endValue, ref Action<float> iterator, Action<float> updateFunc = null, Action callback = null, EaseType easeType = EaseType.Linear)
        {
            _duration = duration;
            _startValue = startValue;
            _endValue = endValue;
            _easeType = easeType;
            if (null == this.Action)
            {
                this.Action = updateFunc;
            }
            else
            {
                List<Delegate> list = new List<Delegate>(this.Action.GetInvocationList());
                if (!list.Contains(updateFunc))
                {
                    this.Action += updateFunc;
                }
            }
            IteratorInternal += Evaluate;

            iterator += (x) =>
            {
                IteratorInternal?.Invoke(x);
            };
            TransitionComplete += callback;
        }

        private void Evaluate(float deltatime)
        {
            Process += deltatime / _duration;
            var tweenValue = Easing.Get(_easeType).Invoke(Process);
            var reslutValue = Mathf.Lerp(_startValue, _endValue, tweenValue);

            Action?.Invoke(reslutValue);

            if (Process >= 1)
            {
                Completed();
            }
        }

        private void Completed()
        {
            IteratorInternal -= Evaluate;
            IteratorInternal = null;
            TransitionComplete?.Invoke();
        }

        public void EndEvaluate()
        {
            if (IteratorInternal != null)
            {
                IteratorInternal -= Evaluate;
                IteratorInternal = null;
            }
        }
    }
}