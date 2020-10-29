using System.Collections;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using ypzxAudioEditor.Utility;

namespace ypzxAudioEditor
{
    [System.Serializable]
    public class EditorViewData : ScriptableObject
    {
        [SerializeField]
        private int projectExplorerTabIndex;

        public bool freshPageWhenTabInexChange = true;
        //public Rect WindowPosition =new Rect(300, 200, 1050, 650);

        public float _currentVerticalSpliterWidth = 360;
        public float _currentHorizontalSpliterHeight =500;

        public string searchString;
        //列表树数据,顺序为Audio、Event、GameSync、Bank
        public TreeViewState[] MyTreeViewStates = new TreeViewState[4];
        public MyTreeLists myTreeLists = new MyTreeLists();

       // public event System.Action ProjectExplorerTabIndexChange;
        public void Init()
        {
            
        }
        public MyTreeElement GetTreeListElementByID(int id)
        {
            for (int i = 0; i < myTreeLists[ProjectExplorerTabIndex].Count; i++)
            {
                if (myTreeLists[ProjectExplorerTabIndex][i].id == id)
                {
                    return myTreeLists[ProjectExplorerTabIndex][i];
                }
            }
            Debug.LogError("匹配树形列表id失败，请检查id");
            return null;
        }

        public MyTreeElement GetTreeListElementByID(List<MyTreeElement> treeList, int id)
        {
            for (int i = 0; i < treeList.Count; i++)
            {
                if (treeList[i].id == id)
                {
                    return treeList[i];
                }
            }
            Debug.LogError("匹配树形列表id失败，请检查id");
            return null;
        }

        public int GetCurrentSelectElmentID()
        {
          return  MyTreeViewStates[projectExplorerTabIndex].lastClickedID;
        }

        public int ProjectExplorerTabIndex
        {
            get => projectExplorerTabIndex;
            set
            {
                projectExplorerTabIndex = value;
               // ProjectExplorerTabIndexChange?.Invoke();
            }
        }
    }

    [System.Serializable]
     public class MyTreeLists
     {
        [SerializeField]
        private List<MyTreeElement> AudioTreeList = new List<MyTreeElement>();
        [SerializeField]
        private List<MyTreeElement> EventTreeList = new List<MyTreeElement>();
        [SerializeField]
        private List<MyTreeElement> GameSyncTreeList = new List<MyTreeElement>();
        [SerializeField]
        private List<MyTreeElement> BankTreeList = new List<MyTreeElement>();

        public List<MyTreeElement> this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        return AudioTreeList;
                    case 1:
                        return EventTreeList;
                    case 2:
                        return GameSyncTreeList;
                    case 3:
                        return BankTreeList;
                    default:
                        Debug.LogError("读取列表树失败，请检查");
                        return null;
                }
            }
            set
            {
                switch (i)
                {
                    case 0:
                        AudioTreeList = value;
                        break;
                    case 1:
                         EventTreeList = value;
                        break;
                    case 2:
                        GameSyncTreeList = value;
                        break;
                    case 3:
                        BankTreeList = value;
                        break;
                    default:
                        Debug.LogError("储存列表树失败，请检查");
                        break;
                }
            }
        }
    }
}
