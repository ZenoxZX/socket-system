using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZenoxZX.SocketSystem.Editor
{
    public class SocketEditorWindow : EditorWindow
    {
        private enum Tab
        {
            Rig,
            Socket,
            Visual
        }

        [SerializeField] private Tab m_Tab = Tab.Rig;
        [SerializeField] private GameObject m_Source;
        [SerializeField] private AnimationClip m_PreviewClip;
        [SerializeField] private float m_PreviewClipTime;
        [SerializeField] private HandleMode m_HandleMode = HandleMode.Move;

        private enum HandleMode
        {
            Move,
            Rotate
        }

        private enum CameraMode
        {
            Orbit,
            Fly
        }

        [SerializeField] private CameraMode m_CameraMode = CameraMode.Orbit;
        [SerializeField] private SocketSet m_Target;
        [SerializeField] private bool m_ShowAllBones = true;

        private Animator m_Animator;
        private Transform m_RigRoot;
        private int m_SelectedSocket = -1;
        private Vector2 m_ScrollPosition;
        private string[] m_BoneNames;
        private string[] m_BoneLabels;
        private string[] m_HumanBoneNames;
        private string[] m_HumanBoneLabels;
        private readonly Dictionary<string, HumanBodyBones> m_HumanBoneByName = new();

        private PreviewRenderUtility m_Preview;
        private GameObject m_PreviewInstance;
        private Animator m_PreviewAnimator;
        private Transform m_PreviewRigRoot;
        private Vector3 m_PreviewPivot;
        private float m_PreviewDistance = 4f;
        private Vector2 m_PreviewAngles = new(120f, -10f);
        private bool m_PreviewDragging;

        private Vector3 m_FlyPosition;
        private Vector2 m_FlyAngles;
        private float m_FlySpeed = 2f;
        private readonly HashSet<KeyCode> m_FlyKeysDown = new();
        private double m_LastFlyTime;

        private RigSocketEditingStage m_Stage;
        private bool InStageMode => m_Stage != null;

        private readonly Dictionary<string, GameObject> m_PreviewVisuals = new();
        private readonly Dictionary<string, GameObject> m_VisualInstances = new();
        private bool m_VisualsDirty;

        private int SocketCount => m_Target != null ? m_Target.SocketCount : 0;
        private SocketDefinition SocketAt(int index) => m_Target.GetSocket(index);

        private bool IsHumanoid => m_Animator != null && m_Animator.isHuman;

        [MenuItem("Tools/ZenoxZX/Socket System/Socket Editor")]
        private static void Open()
        {
            GetWindow<SocketEditorWindow>("Socket Editor");
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += OnEditorUpdate;
            EditorSceneManager.sceneClosing += OnSceneClosing;

            m_Preview = new PreviewRenderUtility();
            m_Preview.camera.fieldOfView = 30f;
            m_Preview.camera.nearClipPlane = 0.01f;
            m_Preview.camera.farClipPlane = 1000f;

            m_Preview.lights[0].intensity = 1.2f;
            m_Preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            m_Preview.lights[1].intensity = 0.8f;
            m_Preview.lights[1].transform.rotation = Quaternion.Euler(-30f, -120f, 0f);

            if (m_Source != null)
                RefreshRig();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            EditorSceneManager.sceneClosing -= OnSceneClosing;

            DestroyPreviewInstance();
            m_Preview?.Cleanup();
            m_Preview = null;
        }

        private void OnEditorUpdate()
        {
            if (m_CameraMode == CameraMode.Fly && m_PreviewDragging && m_FlyKeysDown.Count > 0)
                Repaint();
        }

        private void OpenInSceneView()
        {
            if (m_Source == null || m_Target == null)
                return;

            m_Stage = RigSocketEditingStage.Enter(m_Source, m_Target);
            Repaint();
        }

        private void OnSceneClosing(Scene closingScene, bool removingScene)
        {
            if (m_Stage == null || closingScene != m_Stage.scene)
                return;

            m_Stage = null;
            RefreshRig();
            Repaint();
        }

        private void DestroyPreviewInstance()
        {
            m_VisualInstances.Clear();

            if (m_PreviewInstance != null)
            {
                DestroyImmediate(m_PreviewInstance);
                m_PreviewInstance = null;
            }

            m_PreviewAnimator = null;
            m_PreviewRigRoot = null;
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        private const float k_PropertiesWidth = 340f;

        private const float k_AnimationBarHeight = 38f;

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint)
                UpdateFly();

            if (m_VisualsDirty && Event.current.type == EventType.Layout)
            {
                m_VisualsDirty = false;
                if (m_SelectedSocket >= 0 && m_SelectedSocket < SocketCount)
                    RebuildVisual(SocketAt(m_SelectedSocket).Name);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawPreviewPane();
                    DrawAnimationBar();
                }

                DrawPropertiesPane();
            }
        }

        private void DrawPreviewPane()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (InStageMode)
            {
                DrawPreviewMessage(rect, "Editing in Scene View.\nUse the breadcrumb to go back when done.");
                return;
            }

            if (m_PreviewInstance == null || m_Preview == null)
            {
                DrawPreviewMessage(rect, "Assign a rig source from the panel on the right.");
                return;
            }

            if (rect.width < 1f || rect.height < 1f)
                rect = new Rect(rect.x, rect.y, Mathf.Max(rect.width, 1f), Mathf.Max(rect.height, 1f));

            RenderPreview(rect);
            DrawSocketsOverlay(rect);
            DrawSelectedSocketHandle(rect);
            DrawHandleModeToggle(rect);
            DrawCameraModeToggle(rect);
            HandlePreviewInput(rect);
        }

        private void DrawPreviewMessage(Rect rect, string message)
        {
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
            GUIStyle centered = new(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            GUI.Label(rect, message, centered);
        }

        private void DrawCameraModeToggle(Rect rect)
        {
            Rect modeRect = new(rect.xMax - 146f, rect.y + 6f, 140f, 22f);
            CameraMode mode = (CameraMode)GUI.Toolbar(modeRect, (int)m_CameraMode, new[] { "Orbit", "Fly" });
            SetCameraMode(mode);

            if (m_CameraMode == CameraMode.Fly)
            {
                Rect hintRect = new(rect.xMax - 146f, rect.y + 30f, 140f, 16f);
                GUI.Label(hintRect, "Right-drag: look · WASD/QE", EditorStyles.miniLabel);
            }
        }

        private void DrawAnimationBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(k_AnimationBarHeight)))
            {
                using (new EditorGUI.DisabledScope(m_PreviewInstance == null))
                {
                    EditorGUI.BeginChangeCheck();
                    m_PreviewClipTime = EditorGUILayout.Slider(m_PreviewClipTime, 0f, 1f);
                    bool timeChanged = EditorGUI.EndChangeCheck();

                    EditorGUI.BeginChangeCheck();
                    m_PreviewClip = (AnimationClip)EditorGUILayout.ObjectField(
                        m_PreviewClip, typeof(AnimationClip), false, GUILayout.Width(180f));
                    bool clipChanged = EditorGUI.EndChangeCheck();

                    if (timeChanged || clipChanged)
                        ApplyPreviewPose();
                }
            }
        }

        private void ApplyPreviewPose()
        {
            if (m_PreviewInstance == null)
                return;

            if (m_PreviewClip != null)
            {
                m_PreviewClip.SampleAnimation(m_PreviewInstance, m_PreviewClipTime * m_PreviewClip.length);

                m_PreviewInstance.transform.position = Vector3.zero;
                m_PreviewInstance.transform.rotation = Quaternion.identity;
            }

            Repaint();
        }

        private void RenderPreview(Rect rect)
        {
            PositionPreviewCamera();

            m_Preview.BeginPreview(rect, GUIStyle.none);
            m_Preview.camera.Render();
            Texture result = m_Preview.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);
        }

        private void DrawHandleModeToggle(Rect rect)
        {
            if (m_SelectedSocket < 0 || m_SelectedSocket >= SocketCount)
                return;

            Rect toggleRect = new(rect.x + 6f, rect.y + 6f, 130f, 22f);
            int mode = GUI.Toolbar(toggleRect, (int)m_HandleMode, new[] { "Move (W)", "Rotate (E)" });
            m_HandleMode = (HandleMode)mode;
        }

        private static readonly int s_HandleHash = "SocketHandle".GetHashCode();
        private const float k_MoveHandleRadius = 11f;

        private void DrawSelectedSocketHandle(Rect rect)
        {
            if (m_SelectedSocket < 0 || m_SelectedSocket >= SocketCount)
                return;

            SocketDefinition socket = SocketAt(m_SelectedSocket);
            Transform bone = ResolveBoneTransform(socket, m_PreviewAnimator, m_PreviewRigRoot);
            if (bone == null)
                return;

            if (m_HandleMode != HandleMode.Move)
                return;

            Vector3 worldPos = bone.TransformPoint(socket.LocalPosition);
            if (!TryWorldToGui(rect, worldPos, out Vector2 guiPoint))
                return;

            int controlId = GUIUtility.GetControlID(s_HandleHash, FocusType.Passive);
            Event e = Event.current;

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && Vector2.Distance(e.mousePosition, guiPoint) <= k_MoveHandleRadius)
                    {
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        RecordSocketEdit();
                        DragMove(rect, socket, bone, worldPos, e.mousePosition);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;

                case EventType.Repaint:
                    DrawMoveGizmo(rect, worldPos, bone.rotation * socket.LocalRotation, guiPoint);
                    break;
            }
        }

        private void DragMove(Rect rect, SocketDefinition socket, Transform bone, Vector3 worldPos, Vector2 mousePosition)
        {
            float depth = m_Preview.camera.WorldToScreenPoint(worldPos).z;
            Vector3 newWorld = GuiToWorld(rect, mousePosition, depth);
            socket.LocalPosition = bone.InverseTransformPoint(newWorld);
        }

        private void SnapSocketToCamera()
        {
            if (m_SelectedSocket < 0 || m_SelectedSocket >= SocketCount)
                return;

            SocketDefinition socket = SocketAt(m_SelectedSocket);
            Transform bone = ResolveBoneTransform(socket, m_PreviewAnimator, m_PreviewRigRoot);
            if (bone == null)
                return;

            Transform cam = m_Preview.camera.transform;
            RecordSocketEdit();
            socket.LocalPosition = bone.InverseTransformPoint(cam.position);
            socket.LocalRotation = (Quaternion.Inverse(bone.rotation) * cam.rotation).normalized;
            Repaint();
        }

        private const float k_AxisWorldLength = 0.12f;

        private void DrawMoveGizmo(Rect rect, Vector3 worldPos, Quaternion worldRot, Vector2 guiPoint)
        {
            Handles.BeginGUI();

            DrawAxis(rect, worldPos, worldRot * Vector3.right, guiPoint, Color.red);
            DrawAxis(rect, worldPos, worldRot * Vector3.up, guiPoint, Color.green);
            DrawAxis(rect, worldPos, worldRot * Vector3.forward, guiPoint, Color.blue);

            Handles.color = new Color(1f, 0.92f, 0.2f);
            Handles.DrawWireDisc(guiPoint, Vector3.forward, k_MoveHandleRadius);
            Handles.DrawWireDisc(guiPoint, Vector3.forward, k_MoveHandleRadius - 2f);

            Handles.EndGUI();
        }

        private void DrawAxis(Rect rect, Vector3 worldPos, Vector3 worldDir, Vector2 origin, Color color)
        {
            if (!TryWorldToGui(rect, worldPos + worldDir * k_AxisWorldLength, out Vector2 tip))
                return;

            Handles.color = color;
            Handles.DrawAAPolyLine(3f, origin, tip);

            Vector2 dir = (tip - origin).normalized;
            Vector2 perp = new(-dir.y, dir.x);
            Handles.DrawAAConvexPolygon(tip, tip - dir * 7f + perp * 4f, tip - dir * 7f - perp * 4f);
        }

        private Vector3 GuiToWorld(Rect rect, Vector2 guiPoint, float depth)
        {
            float nx = (guiPoint.x - rect.x) / rect.width;
            float ny = 1f - (guiPoint.y - rect.y) / rect.height;
            Vector3 screen = new(nx * m_Preview.camera.pixelWidth, ny * m_Preview.camera.pixelHeight, depth);
            return m_Preview.camera.ScreenToWorldPoint(screen);
        }

        private const float k_SocketScreenRadius = 7f;

        private void DrawSocketsOverlay(Rect rect)
        {
            Handles.BeginGUI();

            for (int i = 0; i < SocketCount; i++)
            {
                if (!TryGetSocketMatrix(SocketAt(i), out Matrix4x4 matrix))
                    continue;

                Vector3 worldPosition = matrix.GetColumn(3);
                if (!TryWorldToGui(rect, worldPosition, out Vector2 guiPoint))
                    continue;

                bool isSelected = m_SelectedSocket == i;
                Color color = isSelected ? Color.yellow : Color.cyan;
                float radius = isSelected ? k_SocketScreenRadius * 1.4f : k_SocketScreenRadius;

                Handles.color = color;
                Handles.DrawSolidDisc(guiPoint, Vector3.forward, radius);

                GUIStyle label = new(EditorStyles.miniBoldLabel) { normal = { textColor = color } };
                GUI.Label(new Rect(guiPoint.x + radius + 2f, guiPoint.y - 8f, 160f, 16f), SocketAt(i).Name, label);
            }

            Handles.EndGUI();
        }

        private bool TryWorldToGui(Rect rect, Vector3 worldPosition, out Vector2 guiPoint)
        {
            Vector3 screen = m_Preview.camera.WorldToScreenPoint(worldPosition);
            guiPoint = default;

            if (screen.z <= 0f)
                return false;

            float nx = screen.x / m_Preview.camera.pixelWidth;
            float ny = screen.y / m_Preview.camera.pixelHeight;
            guiPoint = new Vector2(rect.x + nx * rect.width, rect.y + (1f - ny) * rect.height);
            return true;
        }

        private void PositionPreviewCamera()
        {
            Camera camera = m_Preview.camera;

            if (m_CameraMode == CameraMode.Fly)
            {
                camera.transform.position = m_FlyPosition;
                camera.transform.rotation = Quaternion.Euler(m_FlyAngles.y, m_FlyAngles.x, 0f);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 1000f;
                return;
            }

            Quaternion rotation = Quaternion.Euler(m_PreviewAngles.y, m_PreviewAngles.x, 0f);
            Vector3 direction = rotation * Vector3.forward;

            camera.transform.position = m_PreviewPivot - direction * m_PreviewDistance;
            camera.transform.rotation = rotation;
            camera.nearClipPlane = Mathf.Max(0.01f, m_PreviewDistance * 0.05f);
            camera.farClipPlane = m_PreviewDistance * 10f + 100f;
        }

        private void PanCamera(Vector2 mouseDelta)
        {
            Quaternion rotation = m_Preview.camera.transform.rotation;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            if (m_CameraMode == CameraMode.Fly)
            {
                m_FlyPosition += (-right * mouseDelta.x + up * mouseDelta.y) * (m_FlySpeed * 0.01f);
            }
            else
            {
                float panSpeed = m_PreviewDistance * 0.002f;
                m_PreviewPivot += (-right * mouseDelta.x + up * mouseDelta.y) * panSpeed;
            }
        }

        private static bool IsFlyKey(KeyCode key)
        {
            return key is KeyCode.W or KeyCode.A or KeyCode.S or KeyCode.D or KeyCode.Q or KeyCode.E;
        }

        private void UpdateFly()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - m_LastFlyTime);
            m_LastFlyTime = now;

            if (m_CameraMode != CameraMode.Fly || !m_PreviewDragging || m_FlyKeysDown.Count == 0)
                return;

            dt = Mathf.Min(dt, 0.1f);

            Quaternion rotation = Quaternion.Euler(m_FlyAngles.y, m_FlyAngles.x, 0f);
            Vector3 dir = Vector3.zero;
            if (m_FlyKeysDown.Contains(KeyCode.W)) dir += rotation * Vector3.forward;
            if (m_FlyKeysDown.Contains(KeyCode.S)) dir += rotation * Vector3.back;
            if (m_FlyKeysDown.Contains(KeyCode.A)) dir += rotation * Vector3.left;
            if (m_FlyKeysDown.Contains(KeyCode.D)) dir += rotation * Vector3.right;
            if (m_FlyKeysDown.Contains(KeyCode.E)) dir += Vector3.up;
            if (m_FlyKeysDown.Contains(KeyCode.Q)) dir += Vector3.down;

            if (dir != Vector3.zero)
                m_FlyPosition += dir.normalized * (m_FlySpeed * dt);
        }

        private void SetCameraMode(CameraMode mode)
        {
            if (mode == m_CameraMode)
                return;

            Camera camera = m_Preview.camera;
            if (mode == CameraMode.Fly)
            {
                m_FlyPosition = camera.transform.position;
                Vector3 euler = camera.transform.rotation.eulerAngles;
                m_FlyAngles = new Vector2(euler.y, euler.x);
            }
            else
            {
                Vector3 forward = Quaternion.Euler(m_FlyAngles.y, m_FlyAngles.x, 0f) * Vector3.forward;
                m_PreviewPivot = m_FlyPosition + forward * m_PreviewDistance;
                m_PreviewAngles = m_FlyAngles;
            }

            m_CameraMode = mode;
            m_FlyKeysDown.Clear();
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    m_PreviewDragging = rect.Contains(e.mousePosition) && GUIUtility.hotControl == 0;
                    break;

                case EventType.MouseUp:
                    m_PreviewDragging = false;
                    m_FlyKeysDown.Clear();
                    break;

                case EventType.MouseDrag:
                    if (!m_PreviewDragging)
                        break;

                    if (e.button == 1 && m_CameraMode == CameraMode.Fly)
                    {
                        m_FlyAngles.x += e.delta.x * 0.3f;
                        m_FlyAngles.y = Mathf.Clamp(m_FlyAngles.y + e.delta.y * 0.3f, -89f, 89f);
                        e.Use();
                        Repaint();
                    }
                    else if (e.button == 1)
                    {
                        m_PreviewAngles.x += e.delta.x * 0.5f;
                        m_PreviewAngles.y = Mathf.Clamp(m_PreviewAngles.y + e.delta.y * 0.5f, -89f, 89f);
                        e.Use();
                        Repaint();
                    }
                    else if (e.button == 2)
                    {
                        PanCamera(e.delta);
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.KeyDown when m_CameraMode == CameraMode.Fly && m_PreviewDragging && IsFlyKey(e.keyCode):
                    m_FlyKeysDown.Add(e.keyCode);
                    e.Use();
                    break;

                case EventType.KeyUp when IsFlyKey(e.keyCode):
                    m_FlyKeysDown.Remove(e.keyCode);
                    e.Use();
                    break;

                case EventType.ScrollWheel when rect.Contains(e.mousePosition) && m_CameraMode == CameraMode.Fly:
                    m_FlySpeed = Mathf.Clamp(m_FlySpeed * (1f - e.delta.y * 0.08f), 0.1f, 50f);
                    e.Use();
                    Repaint();
                    break;

                case EventType.ScrollWheel when rect.Contains(e.mousePosition):
                    m_PreviewDistance = Mathf.Max(0.1f, m_PreviewDistance * (1f + e.delta.y * 0.05f));
                    e.Use();
                    Repaint();
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.F && (e.command || e.control) && e.shift:
                    SnapSocketToCamera();
                    e.Use();
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.F:
                    FocusOnBounds();
                    e.Use();
                    Repaint();
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.W:
                    m_HandleMode = HandleMode.Move;
                    e.Use();
                    Repaint();
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.E:
                    m_HandleMode = HandleMode.Rotate;
                    e.Use();
                    Repaint();
                    break;
            }
        }

        private void DrawPropertiesPane()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(k_PropertiesWidth)))
            {
                m_Tab = (Tab)GUILayout.Toolbar((int)m_Tab, new[] { "Rig", "Socket", "Visual" });
                EditorGUILayout.Space();

                m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

                switch (m_Tab)
                {
                    case Tab.Rig:
                        DrawRigTab();
                        break;
                    case Tab.Socket:
                        DrawSocketTab();
                        break;
                    case Tab.Visual:
                        DrawVisualTab();
                        break;
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRigTab()
        {
            DrawSourceSection();

            if (m_RigRoot == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Assign a rigged GameObject, prefab or FBX. The Animator is found automatically.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Validate Rig"))
                ValidateRig();
        }

        private void DrawVisualTab()
        {
            if (m_Target == null || m_PreviewInstance == null)
            {
                EditorGUILayout.HelpBox("Assign a rig source and a Socket Set first.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Preview Objects", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Assign a visual object per socket to preview how it sits (not saved).", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            for (int i = 0; i < SocketCount; i++)
            {
                string socketName = SocketAt(i).Name;
                m_PreviewVisuals.TryGetValue(socketName, out GameObject current);

                EditorGUI.BeginChangeCheck();
                GameObject next = (GameObject)EditorGUILayout.ObjectField(socketName, current, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                    SetPreviewVisual(socketName, next);
            }
        }

        private void SetPreviewVisual(string socketName, GameObject prefab)
        {
            if (prefab != null)
                m_PreviewVisuals[socketName] = prefab;
            else
                m_PreviewVisuals.Remove(socketName);

            RebuildVisual(socketName);
            Repaint();
        }

        private void RebuildVisual(string socketName)
        {
            if (m_VisualInstances.TryGetValue(socketName, out GameObject existing) && existing != null)
                DestroyImmediate(existing);
            m_VisualInstances.Remove(socketName);

            if (m_PreviewInstance == null)
                return;
            if (!m_PreviewVisuals.TryGetValue(socketName, out GameObject prefab) || prefab == null)
                return;

            int index = IndexOfSocket(socketName);
            if (index < 0)
                return;

            SocketDefinition socket = SocketAt(index);
            Transform bone = ResolveBoneTransform(socket, m_PreviewAnimator, m_PreviewRigRoot);
            if (bone == null)
                return;

            GameObject instance = Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            Transform t = instance.transform;
            t.SetParent(bone, false);
            t.localPosition = socket.LocalPosition;
            t.localRotation = socket.LocalRotation;
            t.localScale = socket.LocalScale;
            m_VisualInstances[socketName] = instance;
        }

        private int IndexOfSocket(string socketName)
        {
            for (int i = 0; i < SocketCount; i++)
            {
                if (SocketAt(i).Name == socketName)
                    return i;
            }

            return -1;
        }

        private void RebuildAllVisuals()
        {
            foreach (string socketName in m_PreviewVisuals.Keys)
                RebuildVisual(socketName);
        }

        private void DrawSocketTab()
        {
            if (m_RigRoot == null)
            {
                EditorGUILayout.HelpBox("Assign a rig source from the Rig tab first.", MessageType.Info);
                return;
            }

            DrawTargetSection();

            if (m_Target == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Assign a Socket Set or create one with 'New' to add sockets.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            DrawSelectedSocketDetail();
            EditorGUILayout.Space();
            DrawSocketList();
            EditorGUILayout.Space();
            DrawBakeAction();
            EditorGUILayout.Space();
            DrawSceneEditAction();
        }

        private void DrawSceneEditAction()
        {
            using (new EditorGUI.DisabledScope(m_Source == null || InStageMode))
            {
                if (GUILayout.Button("Open in Scene View"))
                    OpenInSceneView();
            }

            if (InStageMode)
                EditorGUILayout.HelpBox("Editing in Scene View. Changes are written when you return via the breadcrumb.", MessageType.Info);
        }

        private void DrawSelectedSocketDetail()
        {
            EditorGUILayout.LabelField("Selected Socket", EditorStyles.boldLabel);

            if (m_SelectedSocket < 0 || m_SelectedSocket >= SocketCount)
            {
                EditorGUILayout.LabelField("No socket selected.");
                return;
            }

            EditorGUILayout.LabelField("Name", SocketAt(m_SelectedSocket).Name);
            EditorGUILayout.LabelField("Parent Bone", SocketAt(m_SelectedSocket).ParentBoneName);
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            m_Source = (GameObject)EditorGUILayout.ObjectField("Rig Source", m_Source, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
                RefreshRig();

            if (m_RigRoot != null)
            {
                EditorGUILayout.LabelField("Character Name", m_Source != null ? m_Source.name : "-");
                EditorGUILayout.LabelField("Rig Type", IsHumanoid ? "Humanoid" : "Generic");
                EditorGUILayout.LabelField("Bone Count", (m_BoneNames != null ? m_BoneNames.Length : 0).ToString());

                if (IsHumanoid)
                    m_ShowAllBones = EditorGUILayout.Toggle("Show All Bones", m_ShowAllBones);
            }
        }

        private void RefreshRig()
        {
            m_Animator = null;
            m_RigRoot = null;
            m_BoneNames = null;
            m_BoneLabels = null;
            m_HumanBoneNames = null;
            m_HumanBoneLabels = null;
            m_HumanBoneByName.Clear();
            m_SelectedSocket = -1;
            DestroyPreviewInstance();

            if (m_Source == null)
                return;

            m_Animator = m_Source.GetComponentInChildren<Animator>();
            m_RigRoot = m_Animator != null ? m_Animator.transform : m_Source.transform;

            SetupPreviewInstance();
            BuildHumanBoneMap();

            BoneSnapshot[] snapshot = RigHasher.BuildSnapshot(m_RigRoot);
            m_BoneNames = new string[snapshot.Length];
            m_BoneLabels = new string[snapshot.Length];

            List<string> humanNames = new();
            List<string> humanLabels = new();
            for (int i = 0; i < snapshot.Length; i++)
            {
                string boneName = snapshot[i].Name;
                m_BoneNames[i] = boneName;

                if (m_HumanBoneByName.TryGetValue(boneName, out HumanBodyBones human))
                {
                    string label = $"{ObjectNames.NicifyVariableName(human.ToString())} ({boneName})";
                    m_BoneLabels[i] = label;
                    humanNames.Add(boneName);
                    humanLabels.Add(label);
                }
                else
                {
                    m_BoneLabels[i] = boneName;
                }
            }

            m_HumanBoneNames = humanNames.ToArray();
            m_HumanBoneLabels = humanLabels.ToArray();
        }

        private void BuildHumanBoneMap()
        {
            if (m_Animator == null || !m_Animator.isHuman)
                return;

            for (HumanBodyBones bone = 0; bone < HumanBodyBones.LastBone; bone++)
            {
                Transform t = m_Animator.GetBoneTransform(bone);
                if (t != null)
                    m_HumanBoneByName[t.name] = bone;
            }
        }

        private void SetupPreviewInstance()
        {
            if (m_Preview == null)
                return;

            m_PreviewInstance = Instantiate(m_Source);
            m_PreviewInstance.hideFlags = HideFlags.HideAndDontSave;
            m_PreviewInstance.transform.position = Vector3.zero;
            m_PreviewInstance.transform.rotation = Quaternion.identity;

            m_PreviewAnimator = m_PreviewInstance.GetComponentInChildren<Animator>();
            m_PreviewRigRoot = m_PreviewAnimator != null ? m_PreviewAnimator.transform : m_PreviewInstance.transform;

            m_Preview.AddSingleGO(m_PreviewInstance);

            RebuildAllVisuals();
            FocusOnBounds();
        }

        private void FocusOnBounds()
        {
            if (m_PreviewInstance == null || m_Preview == null)
                return;

            Bounds bounds = CalculateBounds(m_PreviewInstance);
            m_PreviewPivot = bounds.center;

            float radius = bounds.extents.magnitude;
            float halfFov = m_Preview.camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            m_PreviewDistance = radius / Mathf.Sin(halfFov) * 1.1f;
            if (m_PreviewDistance < 0.01f)
                m_PreviewDistance = 4f;
        }

        private static Bounds CalculateBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("Target Socket Set", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                m_Target = (SocketSet)EditorGUILayout.ObjectField("Socket Set", m_Target, typeof(SocketSet), false);
                if (EditorGUI.EndChangeCheck())
                    m_SelectedSocket = -1;

                if (GUILayout.Button("New", GUILayout.Width(60f)))
                    CreateNewSocketSet();
            }
        }

        private void CreateNewSocketSet()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Socket Set", "SocketSet", "asset", "Choose a location for the Socket Set asset.");
            if (string.IsNullOrEmpty(path))
                return;

            SocketSet asset = CreateInstance<SocketSet>();
            if (m_RigRoot != null)
                asset.SetRig(RigHasher.BuildSignature(m_RigRoot, ResolveSourceGuid()));

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            m_Target = asset;
            m_SelectedSocket = -1;
        }

        private void DrawSocketList()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Sockets", EditorStyles.boldLabel);
                if (GUILayout.Button("+ Add", GUILayout.Width(70f)))
                    AddSocket();
            }

            for (int i = 0; i < SocketCount; i++)
            {
                if (!DrawSocketEntry(i))
                    break;
            }
        }

        private void AddSocket()
        {
            Undo.RegisterCompleteObjectUndo(m_Target, "Add Socket");
            string parent = m_BoneNames != null && m_BoneNames.Length > 0 ? m_BoneNames[0] : string.Empty;
            m_Target.AddSocket(new SocketDefinition($"Socket_{SocketCount}", parent));
            m_SelectedSocket = SocketCount - 1;
            EditorUtility.SetDirty(m_Target);
        }

        private bool DrawSocketEntry(int index)
        {
            SocketDefinition socket = SocketAt(index);
            bool expanded = m_SelectedSocket == index;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string title = (expanded ? "▼ " : "▶ ") + socket.Name;
                    if (GUILayout.Button(title, EditorStyles.boldLabel))
                        m_SelectedSocket = expanded ? -1 : index;

                    if (GUILayout.Button("✕", GUILayout.Width(24f)))
                    {
                        Undo.RegisterCompleteObjectUndo(m_Target, "Remove Socket");
                        m_Target.RemoveSocketAt(index);
                        EditorUtility.SetDirty(m_Target);
                        if (m_SelectedSocket >= SocketCount)
                            m_SelectedSocket = -1;
                        return false;
                    }
                }

                if (!expanded)
                    return true;

                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.TextField("Name", socket.Name);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordSocketEdit();
                    socket.Name = name;
                }

                DrawBoneSelector(socket);

                EditorGUI.BeginChangeCheck();
                Vector3 position = EditorGUILayout.Vector3Field("Local Position", socket.LocalPosition);
                Vector3 euler = EditorGUILayout.Vector3Field("Local Rotation", socket.LocalRotation.eulerAngles);
                Vector3 scale = EditorGUILayout.Vector3Field("Local Scale", socket.LocalScale);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordSocketEdit();
                    socket.LocalPosition = position;
                    socket.LocalRotation = Quaternion.Euler(euler);
                    socket.LocalScale = scale;
                }
            }

            return true;
        }

        private void RecordSocketEdit()
        {
            if (m_Target == null)
                return;

            Undo.RegisterCompleteObjectUndo(m_Target, "Edit Socket");
            EditorUtility.SetDirty(m_Target);
            m_VisualsDirty = true;
            Repaint();
        }

        private void DrawBoneSelector(SocketDefinition socket)
        {
            if (m_BoneNames == null || m_BoneNames.Length == 0)
            {
                EditorGUI.BeginChangeCheck();
                string typed = EditorGUILayout.TextField("Parent Bone", socket.ParentBoneName);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordSocketEdit();
                    socket.ParentBoneName = typed;
                    socket.HumanBoneHint = HumanBodyBones.LastBone;
                }
                return;
            }

            bool humanoidOnly = IsHumanoid && !m_ShowAllBones;
            string[] names = humanoidOnly ? m_HumanBoneNames : m_BoneNames;
            string[] labels = humanoidOnly ? m_HumanBoneLabels : m_BoneLabels;

            int currentIndex = System.Array.IndexOf(names, socket.ParentBoneName);

            if (currentIndex < 0)
            {
                names = AppendCurrent(names, socket.ParentBoneName);
                labels = AppendCurrent(labels, $"{socket.ParentBoneName} (non-humanoid)");
                currentIndex = names.Length - 1;
            }

            int newIndex = EditorGUILayout.Popup("Parent Bone", currentIndex, labels);
            if (newIndex == currentIndex)
                return;

            RecordSocketEdit();
            string boneName = names[Mathf.Clamp(newIndex, 0, names.Length - 1)];
            socket.ParentBoneName = boneName;
            socket.HumanBoneHint = m_HumanBoneByName.TryGetValue(boneName, out HumanBodyBones human)
                ? human
                : HumanBodyBones.LastBone;
        }

        private static string[] AppendCurrent(string[] source, string extra)
        {
            string[] result = new string[source.Length + 1];
            System.Array.Copy(source, result, source.Length);
            result[source.Length] = extra;
            return result;
        }

        private void DrawBakeAction()
        {
            using (new EditorGUI.DisabledScope(m_Target == null || m_RigRoot == null))
            {
                if (GUILayout.Button("Update Rig Signature"))
                    BakeRigSignature();
            }
        }

        private void BakeRigSignature()
        {
            RigSignature signature = RigHasher.BuildSignature(m_RigRoot, ResolveSourceGuid());

            Undo.RecordObject(m_Target, "Update Rig Signature");
            m_Target.SetRig(signature);
            EditorUtility.SetDirty(m_Target);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SocketEditor] Rig signature updated for '{m_Target.name}'.", m_Target);
        }

        private string ResolveSourceGuid()
        {
            Object assetForGuid = m_Source;

            if (PrefabUtility.IsPartOfPrefabInstance(m_Source))
                assetForGuid = PrefabUtility.GetCorrespondingObjectFromSource(m_Source);

            string path = AssetDatabase.GetAssetPath(assetForGuid);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private void ValidateRig()
        {
            if (m_Target == null || m_RigRoot == null)
                return;

            string sourceGuid = ResolveSourceGuid();
            RigSignature stored = m_Target.Rig;

            if (!string.IsNullOrEmpty(sourceGuid) && sourceGuid == stored.SourceGuid)
            {
                Debug.Log($"[SocketEditor] Exact match: source GUID equals '{m_Target.name}'.", m_Target);
                return;
            }

            RigMatchResult match = RigHasher.Validate(stored, m_RigRoot);

            if (!match.StructuralMatch)
            {
                string bones = match.MissingBones.Length > 0
                    ? $" Missing bones: {string.Join(", ", match.MissingBones)}"
                    : string.Empty;
                Debug.LogError($"[SocketEditor] Rig does NOT match '{m_Target.name}'. Different rig.{bones}", m_Target);
                return;
            }

            Debug.Log($"[SocketEditor] Rig matched (different GUID but same skeleton).", m_Target);
        }

        private Transform ResolveBoneTransform(SocketDefinition socket, Animator animator, Transform rigRoot)
        {
            if (animator != null && animator.isHuman && socket.HumanBoneHint != HumanBodyBones.LastBone)
            {
                Transform bone = animator.GetBoneTransform(socket.HumanBoneHint);
                if (bone != null)
                    return bone;
            }

            return FindByName(rigRoot, socket.ParentBoneName);
        }

        private bool TryGetSocketMatrix(SocketDefinition socket, out Matrix4x4 matrix)
        {
            matrix = default;
            Transform bone = ResolveBoneTransform(socket, m_PreviewAnimator, m_PreviewRigRoot);
            if (bone == null)
                return false;

            matrix = bone.localToWorldMatrix * Matrix4x4.TRS(socket.LocalPosition, socket.LocalRotation, Vector3.one);
            return true;
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
