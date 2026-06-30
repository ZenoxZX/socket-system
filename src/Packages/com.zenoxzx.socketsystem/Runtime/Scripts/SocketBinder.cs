using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZenoxZX.SocketSystem
{
    public class SocketBinder : MonoBehaviour
    {
        [SerializeField] private SocketSet m_SocketSet;
        [SerializeField] private Animator m_Animator;
        [SerializeField] private bool m_BindOnAwake = true;

        private readonly Dictionary<string, Transform> m_Sockets = new();
        private bool m_IsBound;

        public event Action SocketsBound;

        public bool IsBound => m_IsBound;
        public SocketSet SocketSet => m_SocketSet;
        public IReadOnlyDictionary<string, Transform> Sockets => m_Sockets;

        private void Awake()
        {
            if (m_Animator == null)
                m_Animator = GetComponentInChildren<Animator>();

            if (m_BindOnAwake)
                Bind();
        }

        public Transform GetSocket(string socketName)
        {
            m_Sockets.TryGetValue(socketName, out Transform socket);
            return socket;
        }

        public bool TryGetSocket(string socketName, out Transform socket)
        {
            return m_Sockets.TryGetValue(socketName, out socket);
        }

        public void Bind()
        {
            if (m_IsBound)
                return;

            if (m_SocketSet == null)
            {
                Debug.LogError($"[SocketBinder] {name}: No SocketSet assigned; cannot bind.", this);
                return;
            }

            Transform rigRoot = ResolveRigRoot();
            if (rigRoot == null)
            {
                Debug.LogError($"[SocketBinder] {name}: Rig root not found (no Animator and transform is empty).", this);
                return;
            }

            if (!ValidateRig(rigRoot))
                return;

            BuildSockets(rigRoot);

            m_IsBound = true;
            SocketsBound?.Invoke();
        }

        private Transform ResolveRigRoot()
        {
            return m_Animator != null ? m_Animator.transform : transform;
        }

        private bool ValidateRig(Transform rigRoot)
        {
            RigMatchResult match = RigHasher.Validate(m_SocketSet.Rig, rigRoot);

            if (!match.StructuralMatch)
            {
                string bones = match.MissingBones.Length > 0
                    ? $" Missing bones: {string.Join(", ", match.MissingBones)}"
                    : string.Empty;
                Debug.LogError(
                    $"[SocketBinder] {name}: Rig does not match '{m_SocketSet.name}'. " +
                    $"Sockets not applied (wrong rig).{bones}", this);
                return false;
            }

            return true;
        }

        private void BuildSockets(Transform rigRoot)
        {
            m_Sockets.Clear();

            foreach (SocketDefinition definition in m_SocketSet.Sockets)
            {
                if (string.IsNullOrEmpty(definition.Name))
                {
                    Debug.LogWarning($"[SocketBinder] {name}: Skipped a socket with no name.", this);
                    continue;
                }

                if (m_Sockets.ContainsKey(definition.Name))
                {
                    Debug.LogWarning($"[SocketBinder] {name}: Skipped duplicate socket name '{definition.Name}'.", this);
                    continue;
                }

                Transform parentBone = ResolveBone(rigRoot, definition);
                if (parentBone == null)
                {
                    Debug.LogWarning(
                        $"[SocketBinder] {name}: Parent bone for socket '{definition.Name}' " +
                        $"'{definition.ParentBoneName}' not found; skipped.", this);
                    continue;
                }

                Transform socket = new GameObject(definition.Name).transform;
                socket.SetParent(parentBone, false);
                socket.localPosition = definition.LocalPosition;
                socket.localRotation = definition.LocalRotation;
                socket.localScale = definition.LocalScale;

                m_Sockets[definition.Name] = socket;
            }
        }

        private Transform ResolveBone(Transform rigRoot, SocketDefinition definition)
        {
            if (m_Animator != null && m_Animator.isHuman && definition.HumanBoneHint != HumanBodyBones.LastBone)
            {
                Transform bone = m_Animator.GetBoneTransform(definition.HumanBoneHint);
                if (bone != null)
                    return bone;
            }

            return FindByName(rigRoot, definition.ParentBoneName);
        }

        private static Transform FindByName(Transform root, string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return null;

            if (root.name == boneName)
                return root;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform found = FindByName(root.GetChild(i), boneName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
