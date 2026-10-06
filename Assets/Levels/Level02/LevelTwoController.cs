using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Loongdum.Levels
{
    [DisallowMultipleComponent]
    public sealed class LevelTwoController : MonoBehaviour
    {
        [Serializable]
        public sealed class CellVisual
        {
            public GameObject root;
            public Renderer floor;
            public GameObject barrier;
            public Transform leverHandle;
            public Renderer[] renderers;
            public Collider collider;
        }

        public const float CellSize = 1.6f;
        [SerializeField] private TextAsset layout;
        [SerializeField] private CellVisual[] cells;
        [SerializeField] private Transform lowerBody;
        [SerializeField] private Material darkFloor;
        [SerializeField] private Material litFloor;
        [SerializeField] private Material feltFloor;
        [SerializeField] private Camera levelCamera;
        [SerializeField] private VisionSource upperVision;
        private LevelTwoModel model;
        private Font font;
        private GUIStyle heading, text, small, centered;
        private float nextMoveTime;
        private Vector2Int previousDirection;
        private bool designerView;
        public LevelTwoModel Model => model;
        public CellVisual[] Cells => cells;
        public Transform LowerBody => lowerBody;
        public Camera LevelCamera => levelCamera;
        public VisionSource UpperVision => upperVision;

        public static Vector3 CellPosition(Vector2Int cell) =>
            new Vector3((cell.x - 5) * CellSize, 0f, (5 - cell.y) * CellSize);

        public void Configure(TextAsset data, CellVisual[] visuals, Transform lower,
            Material dark, Material light, Material felt, Camera camera, VisionSource vision = null)
        {
            layout = data;
            cells = visuals;
            lowerBody = lower;
            darkFloor = dark;
            litFloor = light;
            feltFloor = felt;
            levelCamera = camera;
            upperVision = vision;
        }

        private void OnEnable()
        {
            // This repository disables domain and scene reload on entering Play mode.
            // Re-entering the scene must still begin a fresh puzzle.
            if (Application.isPlaying) Initialize();
        }

        public void Initialize()
        {
            model = new LevelTwoModel(JsonUtility.FromJson<LevelTwoLayout>(layout.text).rows);
            designerView = false;
            nextMoveTime = 0f;
            previousDirection = Vector2Int.zero;
            RefreshPresentation();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) { Restart(); return; }
            if ((Application.isEditor || Debug.isDebugBuild) && keyboard.f1Key.wasPressedThisFrame)
            {
                designerView = !designerView;
                RefreshPresentation();
            }
            if (keyboard.eKey.wasPressedThisFrame) Interact();
            Vector2Int direction = ReadDirection(keyboard);
            if (direction == Vector2Int.zero) { previousDirection = direction; return; }
            bool newPress = direction != previousDirection;
            if (newPress || Time.unscaledTime >= nextMoveTime)
            {
                Move(direction);
                nextMoveTime = Time.unscaledTime + (newPress ? 0.28f : 0.14f);
            }
            previousDirection = direction;
        }

        private static Vector2Int ReadDirection(Keyboard keyboard)
        {
            // Never permit a diagonal step or a diagonal corner cut.
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) return Vector2Int.down;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) return Vector2Int.up;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) return Vector2Int.left;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) return Vector2Int.right;
            return Vector2Int.zero;
        }

        public bool Move(Vector2Int direction)
        {
            bool moved = model.TryMove(direction);
            RefreshPresentation();
            return moved;
        }

        public bool Interact()
        {
            bool changed = model.TryInteract();
            RefreshPresentation();
            return changed;
        }

        public void Restart()
        {
            model.Reset();
            designerView = false;
            previousDirection = Vector2Int.zero;
            nextMoveTime = 0f;
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            bool continuousVision = upperVision != null;
            if (continuousVision) upperVision.enabled = !designerView;
            for (int r = 0; r < model.Height; r++)
                for (int c = 0; c < model.Width; c++)
                {
                    Vector2Int cell = new Vector2Int(c, r);
                    CellVisual visual = cells[r * model.Width + c];
                    char tile = model.Tile(cell);
                    bool visible = continuousVision || designerView || model.IsVisible(cell);
                    foreach (Renderer renderer in visual.renderers) renderer.enabled = visible;
                    visual.floor.sharedMaterial = continuousVision ? litFloor
                        : model.IsLit(cell) ? litFloor
                        : model.IsFelt(cell) || cell == model.Lower ? feltFloor : darkFloor;
                    if (visual.barrier != null) visual.barrier.SetActive(!model.IsOpen(tile));
                    if (visual.collider != null) visual.collider.enabled = model.BlocksMovement(cell);
                    if (visual.leverHandle != null)
                        visual.leverHandle.localRotation = Quaternion.Euler(0f, 0f, model.LeverThrown ? -32f : 32f);
                }
            lowerBody.position = CellPosition(model.Lower);
        }

        private void OnGUI()
        {
            if (model == null) return;
            if (heading == null) CreateStyles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.Label(new Rect(34, 24, width - 68, 35), "第二关 · 够不着的拉杆", heading);
            GUI.Label(new Rect(36, 64, width - 72, 28), "下半身找上半身", small);
            GUI.Label(new Rect(36, height - 74, width - 72, 28), model.Feedback, text);
            GUI.Label(new Rect(36, height - 40, width - 72, 26),
                "WASD / 方向键  移动      E  拨动拉杆      R  重来", small);
            if (designerView)
                GUI.Label(new Rect(width - 285, 32, 250, 30), "设计者全图 · F1 返回", text);
            if (model.Won)
            {
                Color old = GUI.color;
                GUI.color = new Color(0.04f, 0.04f, 0.05f, 0.94f);
                GUI.DrawTexture(new Rect(width / 2 - 250, height / 2 - 72, 500, 144), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(width / 2 - 235, height / 2 - 50, 470, 48), "重新走到一起", centered);
                GUI.Label(new Rect(width / 2 - 235, height / 2 + 6, 470, 32),
                    "第二关完成 · " + model.MoveCount + " 步 · 按 R 重来", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            }
            GUI.matrix = previous;
        }

        private void CreateStyles()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 24);
            heading = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, fontStyle = FontStyle.Bold };
            heading.normal.textColor = new Color(0.94f, 0.88f, 0.72f);
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18 };
            text.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
            small = new GUIStyle(text) { fontSize = 15 };
            small.normal.textColor = new Color(0.65f, 0.67f, 0.7f);
            centered = new GUIStyle(heading) { alignment = TextAnchor.MiddleCenter };
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
