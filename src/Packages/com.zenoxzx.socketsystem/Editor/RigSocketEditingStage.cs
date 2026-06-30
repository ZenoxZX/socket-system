using System.Collections.Generic;
using ZenoxZX.SocketSystem;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZenoxZX.SocketSystem.Editor
{
    public class RigSocketEditingStage : PreviewSceneStage
    {
        private GameObject m_SourcePrefab;
        private SocketSet m_Set;
        private GameObject m_RigInstance;
        private Animator m_Animator;
        private Transform m_RigRoot;
        private SceneView m_SceneView;
        private readonly List<Transform> m_SocketTransforms = new();

        public SocketSet Set => m_Set;

        public static RigSocketEditingStage Enter(GameObject sourcePrefab, SocketSet set)
        {
            RigSocketEditingStage stage = CreateInstance<RigSocketEditingStage>();
            stage.m_SourcePrefab = sourcePrefab;
            stage.m_Set = set;

            StageUtility.GoToStage(stage, false);

            stage.m_SceneView = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView
                : EditorWindow.GetWindow<SceneView>();
            stage.FrameRig();
            stage.m_SceneView.Focus();

            return stage;
        }

        protected override GUIContent CreateHeaderContent()
        {
            string label = m_Set != null ? m_Set.name : "Socket Editing";
            return new GUIContent(label);
        }

        protected override bool OnOpenStage()
        {
            base.OnOpenStage();

            if (m_SourcePrefab == null || m_Set == null)
                return false;

            m_RigInstance = Instantiate(m_SourcePrefab);
            m_RigInstance.name = m_SourcePrefab.name;
            m_RigInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(m_RigInstance, scene);

            m_Animator = m_RigInstance.GetComponentInChildren<Animator>();
            m_RigRoot = m_Animator != null ? m_Animator.transform : m_RigInstance.transform;

            SceneVisibilityManager.instance.DisablePicking(m_RigInstance, true);

            BuildSocketTransforms();
            return true;
        }

        private void FrameRig()
        {
            if (m_SceneView == null || m_RigInstance == null)
                return;

            Renderer[] renderers = m_RigInstance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            m_SceneView.Frame(bounds, true);
        }

        private void BuildSocketTransforms()
        {
            m_SocketTransforms.Clear();

            foreach (SocketDefinition definition in m_Set.Sockets)
            {
                Transform bone = ResolveBone(definition);
                if (bone == null)
                {
                    m_SocketTransforms.Add(null);
                    continue;
                }

                GameObject socketGo = new(definition.Name);
                socketGo.AddComponent<SocketMarker>();
                Transform socket = socketGo.transform;
                socket.SetParent(bone, false);
                socket.localPosition = definition.LocalPosition;
                socket.localRotation = definition.LocalRotation;
                socket.localScale = definition.LocalScale;
                SceneVisibilityManager.instance.EnablePicking(socketGo, true);
                m_SocketTransforms.Add(socket);
            }
        }

        protected override void OnCloseStage()
        {
            Selection.activeObject = null;
            WriteBackToSet();
            base.OnCloseStage();
            FocusEditorWindowDeferred();
        }

        private static void FocusEditorWindowDeferred()
        {
            EditorApplication.delayCall += SecondFrame;

            static void SecondFrame() => EditorApplication.delayCall += Focus;
            static void Focus() => EditorWindow.FocusWindowIfItsOpen<SocketEditorWindow>();
        }

        private void WriteBackToSet()
        {
            if (m_Set == null)
                return;

            List<SocketDefinition> result = new();
            IReadOnlyList<SocketDefinition> source = m_Set.Sockets;

            for (int i = 0; i < source.Count; i++)
            {
                SocketDefinition definition = new(source[i]);
                Transform socket = i < m_SocketTransforms.Count ? m_SocketTransforms[i] : null;

                if (socket != null && socket.parent != null)
                {
                    definition.LocalPosition = socket.localPosition;
                    definition.LocalRotation = socket.localRotation;
                    definition.LocalScale = socket.localScale;
                }

                result.Add(definition);
            }

            Undo.RecordObject(m_Set, "Edit Sockets in Scene");
            m_Set.SetData(m_Set.Rig, result);
            EditorUtility.SetDirty(m_Set);
            AssetDatabase.SaveAssets();
        }

        private Transform ResolveBone(SocketDefinition definition)
        {
            if (m_Animator != null && m_Animator.isHuman && definition.HumanBoneHint != HumanBodyBones.LastBone)
            {
                Transform bone = m_Animator.GetBoneTransform(definition.HumanBoneHint);
                if (bone != null)
                    return bone;
            }

            return FindByName(m_RigRoot, definition.ParentBoneName);
        }

        private static Transform FindByName(Transform root, string boneName)
        {
            if (root == null || string.IsNullOrEmpty(boneName))
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
