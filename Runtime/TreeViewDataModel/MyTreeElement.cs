using System;
using System.Collections.Generic;
using UnityEngine.Serialization;


namespace AudioEditor.Runtime.Utility
{
    [Serializable]
    internal class MyTreeElement : TreeElement
    {
        [FormerlySerializedAs("Type")]
        public AEComponentType type;
        public bool enabled = true;

        public MyTreeElement(string name, int depth, int id, AEComponentType type = AEComponentType.WorkUnit) : base(name, depth, id)
        {
            this.type = type;
        }

        public void GetAllChildrenIDByDeepSearch(ref HashSet<int> IDSet)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].hasChildren)
                {
                    (children[i] as MyTreeElement).GetAllChildrenIDByDeepSearch(ref IDSet);
                }
                if (enabled)
                {
                    IDSet.Add(children[i].id);
                }
            }
        }
    }

}
