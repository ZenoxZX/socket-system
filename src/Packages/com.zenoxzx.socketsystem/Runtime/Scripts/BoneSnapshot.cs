using System;
using UnityEngine;

namespace ZenoxZX.SocketSystem
{
    [Serializable]
    public class BoneSnapshot
    {
        [SerializeField] private string m_Name;
        [SerializeField] private string m_ParentName;

        public BoneSnapshot()
        {
        }

        public BoneSnapshot(string name, string parentName)
        {
            m_Name = name;
            m_ParentName = parentName;
        }

        public string Name => m_Name;
        public string ParentName => m_ParentName;
    }
}
