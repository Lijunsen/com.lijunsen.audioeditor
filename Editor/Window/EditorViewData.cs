using AudioEditor.Runtime;
using System;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AudioEditor.Editor
{
    [System.Serializable]
    internal class EditorViewData : ScriptableObject
    {
        [SerializeField]
        private int projectExplorerTabIndex;

        public bool freshPageWhenTabInexChange = true;

        //public Rect WindowPosition = new Rect(300, 200, 1050, 650);

        public float currentVerticalSpliterWidth = 380;
        public float currentHorizontalSpliterHeight = 560;

        [NonSerialized]
        public string searchString;
        
        //列表树数据,顺序为Audio、Event、GameSync、Bank
        public TreeViewState[] myTreeViewStates = new TreeViewState[MyTreeLists.TreeListCount];

        // public event System.Action ProjectExplorerTabIndexChange;


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
}
