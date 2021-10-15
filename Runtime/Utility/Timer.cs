using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AudioEditor.Runtime.Utility
{
    public class AETimer
    {
        private float targetTime;
        private float currentTime;
        private bool isTiming;
        private bool isDestoryWhenTimingEnd = true;
        private bool isEnd;
        private bool isDestroyed;
        private bool isRepeate;

        public bool IsEnd => isEnd;
        public bool IsDestroyed => isDestroyed;

        private Action onCompleteAction;
        private Action<float> onUpdateAction;

        private void Update(float deltaTime)
        {
            if (isTiming)
            {
                currentTime += deltaTime;
                onUpdateAction?.Invoke(Mathf.Clamp01( currentTime / targetTime));
                if (currentTime >= targetTime)
                {
                    if (isRepeate == false)
                    {
                        isEnd = true;

                        onCompleteAction?.Invoke();

                        if (isDestoryWhenTimingEnd)
                        {
                            Destroy();
                        }
                    }
                    else
                    {
                        onCompleteAction?.Invoke();
                        RestartTimer();
                    }

                }
            }
        }

        public void Start()
        {

        }

        public float GetLeftTime()
        {
            return Mathf.Clamp(targetTime - currentTime, 0, targetTime);
        }

        public void PauseTimer()
        {
            if (isEnd)
            {
                if (AudioEditorManager.debugMode)
                {
                    AudioEditorDebugLog.LogWarning("计时已结束");
                }
            }
            else
            {
                if (isTiming)
                {
                    isTiming = false;
                }
            }
        }

        public void ContinueTimer()
        {
            if (isEnd)
            {
                if (AudioEditorManager.debugMode)
                {
                    AudioEditorDebugLog.LogWarning("计时已结束，请重新开始计时");
                }
            }
            else
            {
                if (isTiming == false)
                {
                    isTiming = true;
                }

            }
        }

        public void RestartTimer()
        {
            if (isDestroyed == true)
            {
                AudioEditorDebugLog.LogWarning("计时器已被销毁");
            }
            else
            {
                currentTime = 0;
                isEnd = false;
                isTiming = true;
            }
        }

        public void Destroy()
        {
            AudioEditorManager.OnUpdateAction -= Update;

            isTiming = false;
            isEnd = true;
            isDestroyed = true;
            onUpdateAction = null;
            onCompleteAction = null;
        }

        public static AETimer CreateNewTimer(float duration, Action completeAction, Action<float> updateAction = null, bool destroyWhenTimingEnd = true, bool repeate = false)
        {
            if (duration == 0)
            {
                completeAction?.Invoke();
                return null;
            }

            var timer = new AETimer();
            AudioEditorManager.OnUpdateAction += timer.Update;


            timer.targetTime = duration;
            timer.isEnd = false;
            timer.isTiming = true;

            timer.onCompleteAction = completeAction;
            if (updateAction != null)
            {
                timer.onUpdateAction = updateAction;
            }

            timer.isRepeate = repeate;
            timer.isDestoryWhenTimingEnd = destroyWhenTimingEnd;
            return timer;
        }
    }
}
