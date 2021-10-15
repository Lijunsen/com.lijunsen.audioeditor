using AudioEditor.Runtime.Utility.EasingCore;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioEditor.Runtime.Utility
{
    /// <summary>
    /// 一个执行过渡缓动动作的类
    /// </summary>
    internal static class TransitionAction
    {
        private static readonly Dictionary<int, TransitionActionUnit> actionDictionary = new Dictionary<int, TransitionActionUnit>();

        /// <summary>
        /// 执行一个缓动，会在一段时间内将一个值根据缓动类型从起始数值缓动到结束数值
        /// </summary>
        /// <param name="target">设置target，target具有互斥性，即后设置的target缓动会停止前一个相同的target缓动</param>
        /// <param name="duration">持续时间</param>
        /// <param name="startValue">起始数值</param>
        /// <param name="endValue">结束数值</param>
        /// <param name="iterator">迭代器</param>
        /// <param name="updateFunc">迭代函数，会传回数值当前的缓动状态值</param>
        /// <param name="completedFunc">完成后的回调函数</param>
        /// <param name="beInterruptedFunc">被中断后的回调函数</param>
        /// <param name="easeType">缓动的类型</param>
        public static void OnUpdate(object target, float duration, float startValue, float endValue, ref Action<float> iterator, Action<float> updateFunc, Action completedFunc = null, Action beInterruptedFunc = null, EaseType easeType = EaseType.Linear)
        {
            var targetHashCode = target.GetHashCode();
            actionDictionary.TryGetValue(targetHashCode, out var targetAction);
            if (targetAction != null)
            {
                targetAction.EndEvaluate();
                actionDictionary.Remove(targetHashCode);
            }
            var newTransitionActionUnit = new TransitionActionUnit();
            actionDictionary.Add(targetHashCode, newTransitionActionUnit);
            completedFunc += () =>
            {
                actionDictionary.Remove(targetHashCode);
            };
            newTransitionActionUnit.OnUpdate(duration, startValue, endValue, ref iterator, updateFunc, completedFunc, beInterruptedFunc, easeType);
        }

        /// <summary>
        /// 主动停止对应target上的缓动
        /// </summary>
        /// <param name="target"></param>
        public static void EndTransition(object target)
        {
            var targetHashCode = target.GetHashCode();
            actionDictionary.TryGetValue(targetHashCode, out var transitionAction);
            if (transitionAction != null)
            {
                transitionAction.EndEvaluate();
                actionDictionary.Remove(targetHashCode);
            }
        }

        public static void OnSilence(float duration, ref Action<float> iterator, Action completedFunc = null, Action beInterruptedFunc = null)
        {
            var newTransitionActionUnit = new TransitionActionUnit();
            newTransitionActionUnit.OnUpdate(duration, 0, 1, ref iterator, (newValue) =>
            {
            }, completedFunc, beInterruptedFunc, EaseType.Linear);
        }
    }


    internal class TransitionActionUnit
    {
        //private object Data { get; }
        //private object targetValue;
        private float process = 0;
        private float duration;
        private float startValue;
        private float endValue;
        private EaseType easeType;

        private event Action<float> IteratorInternal;
        private event Action<float> DoUpdateFunc;
        private event Action TransitionCompleteFunc;
        private event Action TransitionBeInterruptedFunc;

        private float Process
        {
            get => process;
            set => process = Mathf.Clamp01(value);
        }

        public void OnUpdate(float duration, float startValue, float endValue, ref Action<float> iterator, Action<float> updateFunc = null, Action callback = null, Action beInterruptedFunc = null, EaseType easeType = EaseType.Linear)
        {
            this.duration = duration;
            this.startValue = startValue;
            this.endValue = endValue;
            this.easeType = easeType;
            if (null == this.DoUpdateFunc)
            {
                this.DoUpdateFunc = updateFunc;
            }
            else
            {
                List<Delegate> list = new List<Delegate>(this.DoUpdateFunc.GetInvocationList());
                if (!list.Contains(updateFunc))
                {
                    this.DoUpdateFunc += updateFunc;
                }
            }
            IteratorInternal += Evaluate;

            iterator += (x) =>
            {
                IteratorInternal?.Invoke(x);
            };

            TransitionCompleteFunc = callback;
            TransitionBeInterruptedFunc = beInterruptedFunc;
        }

        private void Evaluate(float deltatime)
        {
            //process为0到1
            Process += deltatime / duration;
            var tweenValue = Easing.Get(easeType).Invoke(Process);
            var reslutValue = Mathf.Lerp(startValue, endValue, tweenValue);

            DoUpdateFunc?.Invoke(reslutValue);

            if (Process >= 1)
            {
                Completed();
            }
        }

        private void Completed()
        {
            IteratorInternal -= Evaluate;
            IteratorInternal = null;
            TransitionCompleteFunc?.Invoke();
            TransitionCompleteFunc = null;
            TransitionBeInterruptedFunc = null;
        }

        public void EndEvaluate()
        {
            if (IteratorInternal != null)
            {
                IteratorInternal -= Evaluate;
                IteratorInternal = null;
                TransitionBeInterruptedFunc?.Invoke();
            }
        }
    }
}