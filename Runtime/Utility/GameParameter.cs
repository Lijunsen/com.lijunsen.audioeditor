using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ypzxAudioEditor.Utility;

namespace ypzxAudioEditor
{
    [System.Serializable]
    public class GameParameter:AEGameSyncs
    {
        [SerializeField]
        private float minValue;
        [SerializeField]
        private float maxValue;
        [SerializeField]
        private float defaultValue;
        [SerializeField]
        private float value;

        
        public GameParameter(string name, int id) : base(name, id)
        {
            minValue = 0;
            maxValue = 100;
            value = defaultValue =  50;

        }

        public override void Init()
        {
            base.Init();
            //Value = DefaultValue;
        }

        public float MinValue
        {
            get => minValue;
            set
            {
                if (value > maxValue)
                {
                    minValue = maxValue-Mathf.Pow(10,Mathf.FloorToInt(Mathf.Log10(maxValue))-1);
                    return;
                }
                minValue = value;
                if (defaultValue < minValue)
                {
                    defaultValue = minValue;
                }
            }
        }

        public float MaxValue
        {
            get => maxValue;
            set
            {
                if (value < minValue)
                {
                    maxValue = minValue + Mathf.Pow(10, Mathf.FloorToInt(Mathf.Log10(minValue)) - 1);
                    return;
                }
                maxValue = value;
                if (defaultValue > maxValue)
                {
                    defaultValue = maxValue;
                }
            }
        }

        public float DefaultValue
        {
            get => defaultValue;
            set => defaultValue = Mathf.Clamp(value, minValue, maxValue);
        }
        public float Value { get =>value; set=>this.value = Mathf.Clamp(value,minValue,maxValue); }

        /// <summary>
        /// 归一化的值
        /// </summary>
        public float ValueAtNormalized => (value - MinValue) / (MaxValue - MinValue);
    }
}