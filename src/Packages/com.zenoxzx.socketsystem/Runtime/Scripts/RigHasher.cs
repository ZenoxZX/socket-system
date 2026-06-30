using System.Collections.Generic;
using UnityEngine;

namespace ZenoxZX.SocketSystem
{
    // Rig kimliği yalnızca kemik isimleri + hiyerarşisinden üretilir; pose (local TRS) DAHİL DEĞİLDİR
    // (aynı iskelet farklı pose'larda durabilir, soketler kemiğin child'ı olarak relative kalır).
    public static class RigHasher
    {
        private const uint k_FnvOffsetBasis = 2166136261u;
        private const uint k_FnvPrime = 16777619u;

        public static BoneSnapshot[] BuildSnapshot(Transform root)
        {
            if (root == null)
                return System.Array.Empty<BoneSnapshot>();

            HashSet<Transform> rawBones = CollectSkinnedBones(root);
            if (rawBones.Count == 0)
            {
                List<BoneSnapshot> fallback = new();
                CollectRecursive(root, null, null, fallback);
                return fallback.ToArray();
            }

            // Ortak atayı PARENT ZİNCİRİ EKLENMEMİŞ ham kemiklerden hesapla; aksi halde container
            // node (CC_Male/Goalkeeper) da ortak ata sanılır. Ham kemiklerin ortak atası = armature kökü.
            Transform skeletonRoot = FindCommonAncestor(rawBones, root);
            if (skeletonRoot == null)
                skeletonRoot = root;

            // Filtre: ham kemikler + bunların armature köküne kadarki ara parent'ları. Zincir
            // skeletonRoot'ta durduğu için onun ÜstündeKİ container snapshot'a girmez.
            HashSet<Transform> bones = new();
            foreach (Transform bone in rawBones)
                AddParentsUpTo(bone, skeletonRoot, bones);

            List<BoneSnapshot> snapshots = new();
            CollectRecursive(skeletonRoot, null, bones, snapshots);
            return snapshots.ToArray();
        }

        // Parent zinciri EKLENMEZ: yalnızca rootBone + renderer.bones (gerçek deforme eden kemikler).
        public static HashSet<Transform> CollectSkinnedBones(Transform root)
        {
            HashSet<Transform> bones = new();

            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer.rootBone != null)
                    bones.Add(renderer.rootBone);

                Transform[] rendererBones = renderer.bones;
                if (rendererBones == null)
                    continue;

                foreach (Transform bone in rendererBones)
                {
                    if (bone != null)
                        bones.Add(bone);
                }
            }

            return bones;
        }

        private static void AddParentsUpTo(Transform bone, Transform stopAt, HashSet<Transform> bones)
        {
            Transform current = bone;
            while (current != null && bones.Add(current))
            {
                if (current == stopAt)
                    break;
                current = current.parent;
            }
        }

        private static Transform FindCommonAncestor(HashSet<Transform> bones, Transform limit)
        {
            Transform ancestor = null;

            foreach (Transform bone in bones)
            {
                if (ancestor == null)
                {
                    ancestor = bone;
                    continue;
                }

                ancestor = LowestCommonAncestor(ancestor, bone, limit);
                if (ancestor == null)
                    return null;
            }

            return ancestor;
        }

        private static Transform LowestCommonAncestor(Transform a, Transform b, Transform limit)
        {
            HashSet<Transform> ancestorsOfA = new();
            for (Transform t = a; t != null; t = t.parent)
            {
                ancestorsOfA.Add(t);
                if (t == limit)
                    break;
            }

            for (Transform t = b; t != null; t = t.parent)
            {
                if (ancestorsOfA.Contains(t))
                    return t;
                if (t == limit)
                    break;
            }

            return null;
        }

        private static void CollectRecursive(Transform current, string parentName, HashSet<Transform> filter, List<BoneSnapshot> output)
        {
            bool isBone = filter == null || filter.Contains(current);
            string childParentName = parentName;

            if (isBone)
            {
                output.Add(new BoneSnapshot(current.name, parentName));
                childParentName = current.name;
            }

            int childCount = current.childCount;
            if (childCount == 0)
                return;

            List<Transform> children = new(childCount);
            for (int i = 0; i < childCount; i++)
                children.Add(current.GetChild(i));

            children.Sort(static (a, b) => string.CompareOrdinal(a.name, b.name));

            foreach (Transform child in children)
                CollectRecursive(child, childParentName, filter, output);
        }

        public static RigSignature BuildSignature(BoneSnapshot[] bones, string sourceGuid)
        {
            return new RigSignature(ComputeStructuralHash(bones), bones, sourceGuid);
        }

        public static RigSignature BuildSignature(Transform root, string sourceGuid)
        {
            return BuildSignature(BuildSnapshot(root), sourceGuid);
        }

        public static uint ComputeStructuralHash(BoneSnapshot[] bones)
        {
            uint hash = k_FnvOffsetBasis;
            if (bones == null)
                return hash;

            foreach (BoneSnapshot bone in bones)
            {
                hash = HashString(hash, bone.Name);
                hash = HashString(hash, bone.ParentName);
                hash = HashByte(hash, 0);
            }

            return hash;
        }

        public static RigMatchResult Validate(RigSignature stored, Transform liveRoot)
        {
            return Validate(stored, BuildSnapshot(liveRoot));
        }

        public static RigMatchResult Validate(RigSignature stored, BoneSnapshot[] live)
        {
            uint liveStructural = ComputeStructuralHash(live);
            bool structuralMatch = liveStructural == stored.StructuralHash;

            string[] missing = System.Array.Empty<string>();
            if (!structuralMatch && stored.HasBones)
                missing = FindMissingBones(stored.Bones, live);

            return new RigMatchResult(structuralMatch, missing);
        }

        private static string[] FindMissingBones(BoneSnapshot[] stored, BoneSnapshot[] live)
        {
            HashSet<string> liveNames = new(live.Length);
            foreach (BoneSnapshot bone in live)
                liveNames.Add(bone.Name);

            List<string> missing = new();
            foreach (BoneSnapshot bone in stored)
            {
                if (!liveNames.Contains(bone.Name))
                    missing.Add(bone.Name);
            }

            return missing.ToArray();
        }

        private static uint HashByte(uint hash, byte value)
        {
            hash ^= value;
            hash *= k_FnvPrime;
            return hash;
        }

        private static uint HashString(uint hash, string value)
        {
            if (value == null)
                return HashByte(hash, 1);

            foreach (char c in value)
            {
                hash = HashByte(hash, (byte)(c & 0xFF));
                hash = HashByte(hash, (byte)((c >> 8) & 0xFF));
            }

            return hash;
        }
    }

    public readonly struct RigMatchResult
    {
        public readonly bool StructuralMatch;
        public readonly string[] MissingBones;

        public RigMatchResult(bool structuralMatch, string[] missingBones)
        {
            StructuralMatch = structuralMatch;
            MissingBones = missingBones;
        }

        public bool IsMatch => StructuralMatch;
    }
}
