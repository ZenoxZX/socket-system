using System;
using UnityEngine;

namespace ZenoxZX.SocketSystem
{
    [Serializable]
    public class RigSignature
    {
        [SerializeField] private uint m_StructuralHash;
        [SerializeField] private BoneSnapshot[] m_Bones;
        [SerializeField] private string m_SourceGuid;

        public RigSignature()
        {
            m_Bones = Array.Empty<BoneSnapshot>();
        }

        public RigSignature(uint structuralHash, BoneSnapshot[] bones, string sourceGuid)
        {
            m_StructuralHash = structuralHash;
            m_Bones = bones ?? Array.Empty<BoneSnapshot>();
            m_SourceGuid = sourceGuid;
        }

        public uint StructuralHash => m_StructuralHash;
        public BoneSnapshot[] Bones => m_Bones;
        public string SourceGuid => m_SourceGuid;

        public bool HasBones => m_Bones != null && m_Bones.Length > 0;
    }
}
