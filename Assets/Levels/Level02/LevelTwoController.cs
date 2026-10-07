using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Loongdum.Levels
{
    [DisallowMultipleComponent]
    public sealed class LevelTwoController : MonoBehaviour
    {
        /// <summary>One cell that carries something. Cells the diagram draws as plain floor get no
        /// entry at all, so the list holds the level's bodies rather than its grid.</summary>
        [Serializable]
        public sealed class CellVisual
        {
            public Vector2Int cell;
            public GameObject barrier;
            /// <summary>The wall this cell owns. Only the two cells the mirror is swapped between carry
            /// one that the model can take away or hand over.</summary>
            public GameObject wall;
            public Transform leverHandle;
            public Transform leverBHandle;
            public Transform plate;
            public Renderer[] renderers;
            /// <summary>The body's own collider, sized to the cell wherever the rules say the cell
            /// blocks. A cell never carries two: the duplicated blanket collider is gone.</summary>
            public Collider collider;
        }

        public const float CellSize = 3f;
        /// <summary>The first level places its player capsule at this height; the lower body keeps it.</summary>
        [SerializeField] private float lowerBodyHeight = 1.41f;
        [SerializeField] private TextAsset layout;
        [SerializeField] private CellVisual[] cells;
        [SerializeField] private Transform lowerBody;
        [SerializeField] private Camera levelCamera;
        /// <summary>The orange mirror's frame. The near lever trades it with the wall above the far
        /// lever, so it hangs off the portal pair rather than off a cell and is driven here.</summary>
        [SerializeField] private Transform portalExit;
        [SerializeField] private Vector3 portalHomeLocal;
        [SerializeField] private Vector3 portalWallLocal;
        /// <summary>The first level's vision source, standing on the fixed upper body. It paints the
        /// lit disc and the darkness around it over the whole camera, as it does in the first level.</summary>
        [SerializeField] private Transform visionSource;

        [Header("音效（留空则不发声）")]
        [Tooltip("拨动拉杆成功时播放，例如「SFX_UI_BottonClick」。")]
        [SerializeField] private AudioClip interactClip;
        [SerializeField, Range(0f, 1f)] private float interactVolume = 1f;

        private Renderer[] portalRenderers;
        private VisionSource vision;
        private LevelTwoModel model;
        private Font font;
        private GUIStyle heading, text, small, centered;
        private bool designerView;

        /// <summary>The orange mirror's own renderers. The mirror is saved with the scene; the list
        /// gathered off it is not, so a scene that is loaded rather than just built has the mirror and
        /// nothing cached off it.</summary>
        private Renderer[] PortalRenderers
        {
            get
            {
                if (portalRenderers == null && portalExit != null)
                    portalRenderers = portalExit.GetComponentsInChildren<Renderer>(true);
                return portalRenderers;
            }
        }

        /// <summary>The vision source component. The reference is saved with the scene; the component
        /// looked up off it is not, so a scene that is loaded rather than just built has the source and
        /// nothing cached off it.</summary>
        private VisionSource Vision
        {
            get
            {
                if (vision == null && visionSource != null) vision = visionSource.GetComponent<VisionSource>();
                return vision;
            }
        }

        public LevelTwoModel Model => model;
        public CellVisual[] Cells => cells;
        public Transform LowerBody => lowerBody;
        public Transform VisionSourceTransform => visionSource;
        public bool VisionSourceEnabled => Vision != null && Vision.enabled;
        public bool DesignerView => designerView;
        public Camera LevelCamera => levelCamera;
        public Transform PortalExitTransform => portalExit;

        public static Vector3 CellPosition(Vector2Int cell) =>
            new Vector3((cell.x - 5) * CellSize, 0f, (5 - cell.y) * CellSize);

        /// <summary>The cell a point on the board stands in. The board was measured out in cells, but
        /// nothing walks cell by cell: the first level's movement module carries the body wherever the
        /// keyboard points, and the rules read back which cell that turned out to be.</summary>
        public static Vector2Int CellAt(Vector3 position) => new Vector2Int(
            Mathf.RoundToInt(position.x / CellSize) + 5,
            5 - Mathf.RoundToInt(position.z / CellSize));

        public void Configure(TextAsset data, CellVisual[] visuals, Transform lower, Camera camera)
        {
            layout = data;
            cells = visuals;
            lowerBody = lower;
            levelCamera = camera;
        }

        public void ConfigureVision(Transform source)
        {
            visionSource = source;
            vision = null;
        }

        public void ConfigurePortal(Transform exit, Vector3 homeLocal, Vector3 wallLocal)
        {
            portalExit = exit;
            portalHomeLocal = homeLocal;
            portalWallLocal = wallLocal;
            portalRenderers = null;
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
            PlaceLowerBody();
            RefreshPresentation();
        }

        /// <summary>Puts the lower body on the cell the rules hold, without walking it there. The level
        /// starts and restarts with this, and every check compares against it. How high the body stands
        /// is left alone: that is the first level's gravity to decide.</summary>
        public void PlaceLowerBody()
        {
            if (lowerBody == null) return;
            var controller = lowerBody.GetComponent<CharacterController>();
            // A CharacterController keeps its own copy of where it is, so moving the transform under
            // it needs the controller switched off and on again, or its next Move walks from the old
            // spot and drags the body back there.
            if (controller != null) controller.enabled = false;
            Vector3 placed = CellPosition(model.Lower) + Vector3.up * lowerBodyHeight;
            placed.y = lowerBody.position.y;
            lowerBody.position = placed;
            if (controller != null) controller.enabled = true;
        }

        /// <summary>Reads the cell the lower body has walked into and hands it to the rules. The body
        /// is never moved here: it walks itself, on the first level's movement module, and this only
        /// notices where it got to.</summary>
        private void FollowLowerBody()
        {
            if (model == null || lowerBody == null) return;
            Vector2Int cell = CellAt(lowerBody.position);
            if (cell == model.Lower) return;
            model.SetLower(cell);
            RefreshPresentation();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) { Restart(); return; }
                if ((Application.isEditor || Debug.isDebugBuild) && keyboard.f1Key.wasPressedThisFrame)
                {
                    designerView = !designerView;
                    RefreshPresentation();
                }
                if (keyboard.fKey.wasPressedThisFrame) Interact();
            }
            FollowLowerBody();
        }

        /// <summary>Walks the rules one cell and stands the body on the cell they land on. The scene
        /// itself is walked by the first level's movement module; this is the entry the checks use to
        /// travel the same route without a keyboard.</summary>
        public bool Step(Vector2Int direction)
        {
            bool moved = model.TryMove(direction);
            PlaceLowerBody();
            RefreshPresentation();
            return moved;
        }

        public bool Interact()
        {
            bool changed = model.TryInteract();
            if (changed)
                AudioOneShot.Play(interactClip, lowerBody != null ? lowerBody.gameObject : gameObject, interactVolume);
            RefreshPresentation();
            return changed;
        }

        public void Restart()
        {
            model.Reset();
            designerView = false;
            PlaceLowerBody();
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            // Only the cells that carry a body are walked: plain floor is the first level's terrain and
            // has nothing to switch, so it has no entry in the first place.
            foreach (CellVisual visual in cells)
            {
                Vector2Int cell = visual.cell;
                char tile = model.Tile(cell);
                bool visible = designerView || model.IsVisible(cell);
                foreach (Renderer renderer in visual.renderers) renderer.enabled = visible;
                if (visual.barrier != null) visual.barrier.SetActive(!model.IsOpen(tile));
                // The two cells the orange mirror trades between carry a wall each, and the model
                // says which of them is a wall right now. Every other cell's wall never changes.
                if (visual.wall != null && visual.wall.activeSelf != (tile == '#'))
                    visual.wall.SetActive(tile == '#');
                if (visual.collider != null) visual.collider.enabled = model.BlocksMovement(cell);
                if (visual.leverHandle != null)
                    visual.leverHandle.localRotation = Quaternion.Euler(0f, 0f, model.LeverThrown ? -32f : 32f);
                if (visual.leverBHandle != null)
                    visual.leverBHandle.localRotation = Quaternion.Euler(0f, 0f, model.LeverBThrown ? -32f : 32f);
                if (visual.plate != null)
                {
                    // The plate is its own body on the board, so it keeps the position of its cell and
                    // only sinks where it stands.
                    Vector3 platePosition = visual.plate.localPosition;
                    platePosition.y = model.PlatePressed ? .03f : .12f;
                    visual.plate.localPosition = platePosition;
                }
            }
            if (portalExit != null)
            {
                portalExit.localPosition = model.LeverThrown ? portalWallLocal : portalHomeLocal;
                bool mirrorVisible = designerView || model.IsVisible(model.PortalExit);
                foreach (Renderer renderer in PortalRenderers) renderer.enabled = mirrorVisible;
            }
            // The designer's view switches the lamp off as well as switching every cell on: with the
            // mask gone the whole board renders lit, which is what the editor's Scene view already
            // shows, since the first level's source only paints over a game camera.
            if (Vision != null) Vision.enabled = !designerView;
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
                "WASD  移动      F  拨动拉杆      R  重来", small);
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
