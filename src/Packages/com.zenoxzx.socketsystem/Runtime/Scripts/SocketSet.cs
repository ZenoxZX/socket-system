using System.Collections.Generic;
using UnityEngine;

namespace ZenoxZX.SocketSystem
{
    [CreateAssetMenu(menuName = "ZenoxZX/Socket System/Socket Set", fileName = "SocketSet")]
    public class SocketSet : ScriptableObject
    {
        [SerializeField] private RigSignature m_Rig;
        [SerializeField] private List<SocketDefinition> m_Sockets = new();

        public RigSignature Rig => m_Rig;
        public IReadOnlyList<SocketDefinition> Sockets => m_Sockets;
        public int SocketCount => m_Sockets.Count;

        public SocketDefinition GetSocket(int index) => m_Sockets[index];

        public void SetRig(RigSignature rig)
        {
            m_Rig = rig;
        }

        public void AddSocket(SocketDefinition socket)
        {
            m_Sockets.Add(socket);
        }

        public void RemoveSocketAt(int index)
        {
            m_Sockets.RemoveAt(index);
        }

        public void SetData(RigSignature rig, List<SocketDefinition> sockets)
        {
            m_Rig = rig;
            m_Sockets = sockets ?? new List<SocketDefinition>();
        }
    }
}
