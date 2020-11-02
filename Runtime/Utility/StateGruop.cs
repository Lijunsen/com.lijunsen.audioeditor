using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ypzxAudioEditor.Utility
{
    [System.Serializable]
    public class StateGruop : AEGameSyncs
    {
        public float defaultTransitionTime = 1f;
        public int currentStateID = -1;
        [SerializeField]
        private List<State> stateList;
        public List<StateTransition> customStateTransitionList;
        public StateGruop(string name, int id) : base(name, id)
        {
            stateList = new List<State>();
            customStateTransitionList = new List<StateTransition>();
        }

        public override void Init()
        {
            base.Init();
            if (stateList.Count > 0)
            {
                currentStateID = stateList[0].id;
            }
            else
            {
                currentStateID = -1;
                Debug.LogWarning("[AudioEditor]: 检测到StateGroup没有State子类，请检查，id: "+this.id);
            }
        }

        public State GenerateState(string name, int id,bool isNone)
        {
            var newState = new State(name, id, isNone);
            stateList.Add(newState);
            if (isNone) currentStateID = newState.id;
            return newState;
        }


        // public State GenerateState(string name, int id, float volume, float pitch)
        // {
        //     var newState = new State(name, id, volume, pitch);
        //     stateList.Add(newState);
        //     return newState;
        // }

        public State FindState(int stateID)
        {
            return stateList.Find(x => x.id == stateID);
        }

        public int FindStateIndex(int stateID)
        {
            return stateList.FindIndex(x => x.id == stateID);
        }

        public State GetStateAt(int index)
        {
            return stateList[index];
        }

        public bool RemoveState(int stateID)
        {
            var state = FindState(stateID);
            if (state != null)
            {
                //删除过渡项中State相关的项
                for (int i = 0; i < customStateTransitionList.Count;)
                {
                    if (customStateTransitionList[i].Relate(state))
                    {
                        customStateTransitionList.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }
                stateList.Remove(state);
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取当前的State在序列中的序号
        /// </summary>
        /// <returns></returns>
        public int GetCurrentStateIndex()
        {
            return FindStateIndex(currentStateID);
        }

        // public bool RemoveStateAt(int i)
        // {
        //     if (i < 0 || i >= stateList.Count)
        //     {
        //         Debug.LogError("传入非法参数");
        //         return false;
        //     }
        //     stateList.RemoveAt(i);
        //     return true;
        // }

        public bool ContainsState(int id)
        {
            if (FindState(id) == null)
            {
                return false;
            }
            return true;
        }

        public void GenerateTransition(int sourceId,int destinationId)
        {
            var sourceState = stateList.Find(x=>x.id ==sourceId);
            var destinationState = stateList.Find(x => x.id == destinationId);
            foreach (var stateTransition in customStateTransitionList)
            {
                if (stateTransition.Equal(sourceState, destinationState))
                {
                    return;
                }
            }
            customStateTransitionList.Add(new StateTransition(sourceState,destinationState,defaultTransitionTime));
        }

        public StateTransition GetTransition(int sourceId, int destinationId)
        {
            foreach (var stateTransition in customStateTransitionList)
            {
                if (stateTransition.Equal(sourceId, destinationId))
                {
                    return stateTransition;
                }
            }
            return null;
        }

        public void GenerateTransition(State sourceState,State destinationState)
        {
            foreach (var stateTransition in customStateTransitionList)
            {
                if (stateTransition.Equal(sourceState, destinationState))
                {
                    return;
                }
            }
            customStateTransitionList.Add(new StateTransition(sourceState, destinationState, defaultTransitionTime));
        }

        // public void ChangeTransition(State sourceState, State destinationState, float time)
        // {
        //     foreach (var stateTransition in customStateTransitionList)
        //     {
        //         if (stateTransition.Equals(sourceState, destinationState))
        //         {
        //             return;
        //         }
        //     }
        //
        // }

        public int StateListCount => stateList.Count;
    }

    [System.Serializable]
    public class State : AEGameSyncs
    {
        [SerializeField] private bool _isNone = false;
        // private float volume = 1;
        // private float pitch = 1;

        public State(string name, int id,bool isNone) : base(name, id)
        {
            this._isNone = isNone;
        }

        public bool IsNone => _isNone;

        // public State(string name, int id, float v, float p) : base(name, id)
        // {
        //     volume = v;
        //     pitch = p;
        // }
    }

    [System.Serializable]
    public class StateTransition
    {
        public int sourceStateID;
        public int destinationStateID;
        public float transitionTime;
        public bool canRevert = false;


        public StateTransition(State from, State to, float time)
        {
            sourceStateID = from.id;
            destinationStateID = to.id;
            transitionTime = time;
        }


        public bool Equal(State from,State to)
        {
            if (sourceStateID == from.id && destinationStateID == to.id)
            {
                return true;
            }
            //当可逆时首尾相反也算相等
            if (canRevert && sourceStateID == to.id && destinationStateID == from.id)
            {
                return true;
            }
            return false;
        }

        public bool Equal(int fromId, int toId)
        {
            if (sourceStateID == fromId && destinationStateID == toId)
            {
                return true;
            }

            if (canRevert && sourceStateID == toId && destinationStateID == fromId)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 起始和目的是否与某个state相关
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool Relate(State state)
        {
            if (state.id == sourceStateID || state.id == destinationStateID)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        ///  起始和目的是否与某个transition相关，相反也算
        /// </summary>
        /// <param name="transition"></param>
        /// <returns></returns>
        public bool Relate(StateTransition transition)
        {
            if (transition.sourceStateID == destinationStateID && transition.destinationStateID == destinationStateID)
            {
                return true;
            }

            if (transition.sourceStateID == destinationStateID && transition.destinationStateID == sourceStateID)
            {
                return true;
            }

            return false;
        }

        public bool Relate(int stateID)
        {
            if (stateID == sourceStateID || stateID == destinationStateID)
            {
                return true;
            }
            return false;
        }


    }

}
