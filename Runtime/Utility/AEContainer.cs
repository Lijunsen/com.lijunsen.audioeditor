using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ypzxAudioEditor.Utility
{
    public enum RandomContainerPlayType
    {
        Standard,
        Shuffle
    }
    public enum ContainerPlayMode
    {
        Step,
        Continuous
    }

    public enum SequenceContainerPlayType
    {
        Restart,
        ReverseOrder
    }
    public interface AEContainer
{
        ContainerPlayMode PlayMode { get; set; }
        List<int> ChildrenID { get; set; }

        //TODO: cantainer的CrossFade功能
}
    
}
