using System;
using System.Collections.Generic;
using UnityEngine;



namespace ypzxAudioEditor.Utility
{
    [Serializable]
    public class MyTreeElement : TreeElement
    {
        public AEComponentType Type = AEComponentType.Workunit;
        //TODO 取消勾选时，父级为SwitchContainer的话其OutputList没有进行检测
        public bool enabled = true;

        public MyTreeElement (string name, int depth, int id, AEComponentType type = AEComponentType.Workunit) : base (name, depth, id)
        {
            Type = type;
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
