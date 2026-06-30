using System;
using UnityEngine;

namespace ZenoxZX.SocketSystem
{
    [Serializable]
    public class SocketDefinition
    {
        [SerializeField] private string m_Name;
        [SerializeField] private string m_ParentBoneName;
        [SerializeField] private Vector3 m_LocalPosition;
        [SerializeField] private Quaternion m_LocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 m_LocalScale = Vector3.one;
        [SerializeField] private HumanBodyBones m_HumanBoneHint = HumanBodyBones.LastBone;

        public SocketDefinition()
        {
        }

        public SocketDefinition(string name, string parentBoneName)
        {
            m_Name = name;
            m_ParentBoneName = parentBoneName;
            m_LocalPosition = Vector3.zero;
            m_LocalRotation = Quaternion.identity;
            m_LocalScale = Vector3.one;
            m_HumanBoneHint = HumanBodyBones.LastBone;
        }

        public SocketDefinition(SocketDefinition other)
        {
            m_Name = other.m_Name;
            m_ParentBoneName = other.m_ParentBoneName;
            m_LocalPosition = other.m_LocalPosition;
            m_LocalRotation = other.m_LocalRotation;
            m_LocalScale = other.m_LocalScale;
            m_HumanBoneHint = other.m_HumanBoneHint;
        }

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string ParentBoneName
        {
            get => m_ParentBoneName;
            set => m_ParentBoneName = value;
        }

        public Vector3 LocalPosition
        {
            get => m_LocalPosition;
            set => m_LocalPosition = value;
        }

        public Quaternion LocalRotation
        {
            get => m_LocalRotation;
            set => m_LocalRotation = value;
        }

        public Vector3 LocalScale
        {
            get => m_LocalScale;
            set => m_LocalScale = value;
        }

        public HumanBodyBones HumanBoneHint
        {
            get => m_HumanBoneHint;
            set => m_HumanBoneHint = value;
        }
    }
}
