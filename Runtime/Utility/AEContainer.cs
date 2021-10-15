using System.Collections.Generic;

namespace AudioEditor.Runtime.Utility
{
    internal enum RandomContainerPlayType
    {
        Standard,
        Shuffle
    }

    internal enum ContainerPlayMode
    {
        Step,
        Continuous
    }

    internal enum SequenceContainerPlayType
    {
        Restart,
        ReverseOrder
    }

    internal enum CrossFadeType
    {
        Linear,
        Delay
    }

    internal interface IAEContainer
    {
        ContainerPlayMode PlayMode { get; set; }
        List<int> ChildrenID { get; set; }

    }

}
