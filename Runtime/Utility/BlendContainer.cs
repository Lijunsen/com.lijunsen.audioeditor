using System.Collections;
using System.Collections.Generic;

namespace ypzxAudioEditor.Utility
{
    public class BlendContainer : AEAudioComponent, AEContainer
    {

        public BlendContainer(string name, int id, AEComponentType type) : base(name, id, type)
        {

        }

        public ContainerPlayMode PlayMode { get; set; }
        public List<int> ChildrenID { get; set; }
    }
}