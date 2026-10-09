using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RecyclingCleaners
{
    /// <summary>Original room art. Discrete Core state owns dirt, inventory and tool coverage.</summary>
    public sealed class CleanerSceneView : MonoBehaviour
    {
        public const float CellSize = .90f;
        private const float FloorTop = .035f;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly CellVisual[] cells = new CellVisual[Rules.Size * Rules.Size];
        private Camera camera;
        private Font font;
        private Transform childRoot, cart, narrowHead, wideHead, bucketContents, filterLamp, detergentGauge;
        private Renderer filterLampRenderer;
        private int lastPlayerX = -1, lastPlayerY = -1, lastOrdinary = -1, lastConvertible = -1;
        private Vector3 cartFrom, cartTo;
        private float cartMoveTime;
        private bool cartInitialized;

        private sealed class CellVisual
        {
            public Transform Root, DirtRoot, Selection, Coverage, CleanMark;
            public Renderer Floor;
            public TextMesh RemainingText;
            public int Remaining = -1;
            public DirtKind Dirt;
            public MaterialKind Material;
            public bool Blocked;
        }

        public Transform SceneRoot { get { return childRoot; } }

        public void Build(Camera gameCamera)
        {
            if (gameCamera == null) throw new ArgumentNullException(nameof(gameCamera));
            if (childRoot != null) return;
            camera = gameCamera;
            font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            childRoot = new GameObject("CleanerRoom_OriginalArt").transform;
            BuildPalette();
            ConfigureCameraAndLight();
            BuildRoom();
            BuildGrid();
            BuildSupplyCounter();
            BuildCart();
        }

        public Vector3 CellPosition(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Rules.Size || y >= Rules.Size)
                throw new ArgumentOutOfRangeException("cell");
            float half = (Rules.Size - 1) * .5f;
            return new Vector3((x - half) * CellSize, FloorTop, (y - half) * CellSize);
        }

        public Vector3 CellScreenPoint(int x, int y)
        {
            return camera == null ? Vector3.zero : camera.WorldToScreenPoint(CellPosition(x, y) + Vector3.up * .12f);
        }

        public bool TryPick(Vector2 screenPoint, out int x, out int y)
        {
            x = y = -1;
            if (camera == null || !camera.pixelRect.Contains(screenPoint)) return false;
            RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPoint), 50f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                CleanerCellPick pick = hit.collider.GetComponent<CleanerCellPick>();
                if (pick == null) continue;
                x = pick.X; y = pick.Y; return true;
            }
            return false;
        }

        public void Render(Session session, int targetX, int targetY)
        {
            if (session == null || childRoot == null) return;
            RoundSnapshot state = session.State;
            foreach (CellState cell in state.Cells)
            {
                CellVisual view = cells[cell.Id];
                if (view.Remaining != cell.Remaining || view.Dirt != cell.Dirt ||
                    view.Material != cell.Material || view.Blocked != cell.Blocked)
                {
                    ClearChildren(view.DirtRoot);
                    if (cell.Blocked) BuildObstacle(view.DirtRoot);
                    else if (cell.Remaining > 0) BuildDirt(view.DirtRoot, cell);
                    view.Remaining = cell.Remaining; view.Dirt = cell.Dirt;
                    view.Material = cell.Material; view.Blocked = cell.Blocked;
                    string floor = cell.Blocked ? "blockedTile" : cell.Remaining > 0
                        ? ((cell.X + cell.Y) % 2 == 0 ? "dirtyTileA" : "dirtyTileB")
                        : ((cell.X + cell.Y) % 2 == 0 ? "tileA" : "tileB");
                    view.Floor.sharedMaterial = materials[floor];
                    view.RemainingText.text = cell.Blocked ? "障碍" : cell.Remaining > 0
                        ? DirtLabel(cell) + " " + cell.Remaining : cell.InitialAmount > 0 ? "净" : "";
                    view.RemainingText.transform.localPosition = cell.Blocked
                        ? new Vector3(0, .44f, -.31f) : new Vector3(0, .15f, -.31f);
                    view.CleanMark.gameObject.SetActive(!cell.Blocked && cell.InitialAmount > 0 && cell.Remaining == 0);
                }
                view.RemainingText.transform.rotation = camera.transform.rotation;
                view.Selection.gameObject.SetActive(cell.X == targetX && cell.Y == targetY);
                view.Coverage.gameObject.SetActive(false);
            }
            if (targetX >= 0 && targetY >= 0 && targetX < Rules.Size && targetY < Rules.Size)
            {
                foreach (int id in session.PreviewCoverage(targetX, targetY))
                    if (id >= 0 && id < cells.Length) cells[id].Coverage.gameObject.SetActive(true);
            }
            Vector3 desired = CellPosition(state.PlayerX, state.PlayerY);
            if (!cartInitialized)
            {
                cartFrom = cartTo = desired; cart.position = desired; cartInitialized = true;
            }
            else if (lastPlayerX != state.PlayerX || lastPlayerY != state.PlayerY)
            {
                cartFrom = cart.position; cartTo = desired; cartMoveTime = Time.unscaledTime;
            }
            cart.position = Vector3.Lerp(cartFrom, cartTo,
                Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - cartMoveTime) / .20f)));
            lastPlayerX = state.PlayerX; lastPlayerY = state.PlayerY;
            narrowHead.gameObject.SetActive(state.Brush == BrushKind.Narrow);
            wideHead.gameObject.SetActive(state.Brush == BrushKind.Wide);
            filterLampRenderer.sharedMaterial = materials[state.FilterEnabled && state.FilterChargesRemaining > 0 ? "mint" : "ink"];
            detergentGauge.localScale = new Vector3(1, Mathf.Clamp(state.Detergent / (float)Rules.DetergentCapacity, .04f, 1), 1);
            if (lastOrdinary != state.Ordinary || lastConvertible != state.Convertible)
            {
                ClearChildren(bucketContents);
                for (int i = 0; i < Mathf.Min(state.Ordinary, 4); i++)
                    RoundedBox("StoredSquarePiece", bucketContents, new Vector3(-.08f, .022f * i, 0),
                        new Vector3(.11f, .024f, .12f), .008f, "debris").localRotation = Quaternion.Euler(0, i * 29, 0);
                for (int i = 0; i < Mathf.Min(state.Convertible, 4); i++)
                {
                    Transform ring = OvalBand("StoredRingPiece", bucketContents, .048f, .048f, .020f, .015f, "silver");
                    ring.localPosition = new Vector3(.075f, .020f * i, .012f);
                }
                lastOrdinary = state.Ordinary; lastConvertible = state.Convertible;
            }
        }

        public void Dispose()
        {
            if (childRoot != null) UnityEngine.Object.Destroy(childRoot.gameObject);
            foreach (Material material in materials.Values) UnityEngine.Object.Destroy(material);
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            materials.Clear(); meshes.Clear(); childRoot = null;
            Array.Clear(cells, 0, cells.Length); cartInitialized = false;
            lastPlayerX = lastPlayerY = lastOrdinary = lastConvertible = -1;
        }

        private void OnDestroy() { Dispose(); }

        private static string DirtLabel(CellState cell)
        {
            if (cell.Dirt == DirtKind.Water) return "水";
            if (cell.Dirt == DirtKind.Stubborn) return "渍";
            return cell.Material == MaterialKind.Convertible ? "环" : "片";
        }

        private void ConfigureCameraAndLight()
        {
            float aspect = camera.pixelRect.height > 1 ? camera.pixelRect.width / camera.pixelRect.height : camera.aspect;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(3.65f, 4.65f / Mathf.Max(.6f, aspect));
            camera.transform.position = new Vector3(.35f, 9.0f, -8.8f);
            camera.transform.LookAt(new Vector3(0, .60f, .40f));
            camera.nearClipPlane = .1f; camera.farClipPlane = 50f;
            camera.backgroundColor = new Color(.24f, .38f, .44f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.72f, .78f, .80f);
            Transform sun = Empty("MorningRoomKeyLight", childRoot, Vector3.zero);
            Light light = sun.gameObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = .90f;
            light.color = new Color(1, .93f, .80f); light.shadows = LightShadows.Soft;
            sun.rotation = Quaternion.Euler(48, -27, 0);
        }

        private void BuildPalette()
        {
            AddMaterial("wood", new Color(.68f, .44f, .26f));
            AddMaterial("woodLight", new Color(.84f, .65f, .42f));
            AddMaterial("wall", new Color(.89f, .91f, .83f));
            AddMaterial("tileA", new Color(.97f, .95f, .85f));
            AddMaterial("tileB", new Color(.91f, .86f, .73f));
            AddMaterial("dirtyTileA", new Color(.81f, .81f, .72f));
            AddMaterial("dirtyTileB", new Color(.76f, .73f, .63f));
            AddMaterial("blockedTile", new Color(.63f, .67f, .65f));
            AddMaterial("blue", new Color(.16f, .49f, .68f));
            AddMaterial("blueLight", new Color(.45f, .76f, .83f));
            AddMaterial("cream", new Color(.99f, .97f, .88f));
            AddMaterial("ink", new Color(.12f, .22f, .26f));
            AddMaterial("debris", new Color(.73f, .76f, .73f));
            AddMaterial("silver", new Color(.68f, .73f, .75f), .18f);
            AddMaterial("water", new Color(.25f, .62f, .77f), .05f);
            AddMaterial("waterShine", new Color(.70f, .91f, .92f));
            AddMaterial("stain", new Color(.77f, .47f, .19f));
            AddMaterial("stainDark", new Color(.39f, .27f, .14f));
            AddMaterial("mint", new Color(.26f, .68f, .49f), 0, true);
            AddMaterial("target", new Color(.05f, .45f, .60f), 0, true);
            AddMaterial("coverage", new Color(.98f, .63f, .16f), 0, true);
        }

        private void AddMaterial(string id, Color color, float metal = 0, bool glow = false)
        {
            var material = new Material(Shader.Find("Standard")) { name = "CleanerOriginal_" + id, color = color };
            material.SetFloat("_Metallic", metal); material.SetFloat("_Glossiness", .24f);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .10f); }
            materials.Add(id, material);
        }

        private void BuildRoom()
        {
            RoundedBox("RoomWoodFoundation", childRoot, new Vector3(0, -.12f, 0), new Vector3(8.2f, .24f, 7.1f), .20f, "wood");
            RoundedBox("FloorUnderGrid", childRoot, new Vector3(0, -.008f, 0), new Vector3(5.58f, .06f, 5.58f), .07f, "woodLight");
            RoundedBox("CreamBackWall", childRoot, new Vector3(0, 1.24f, 3.45f), new Vector3(8.14f, 2.48f, .15f), .035f, "wall");
            RoundedBox("BackSkirting", childRoot, new Vector3(0, .14f, 3.32f), new Vector3(8.12f, .20f, .12f), .025f, "woodLight");
            foreach (int side in new[] { -1, 1 })
                RoundedBox("SideSkirting", childRoot, new Vector3(side * 4.01f, .10f, -.07f), new Vector3(.12f, .16f, 6.75f), .025f, "woodLight");
            RoundedBox("WindowBluePane", childRoot, new Vector3(-1.56f, 1.45f, 3.35f), new Vector3(1.80f, 1.22f, .035f), .01f, "blueLight");
            foreach (float x in new[] { -2.52f, -1.56f, -.60f })
                RoundedBox("WindowUpright", childRoot, new Vector3(x, 1.45f, 3.28f), new Vector3(.07f, 1.40f, .095f), .018f, "cream");
            foreach (float y in new[] { .75f, 1.45f, 2.15f })
                RoundedBox("WindowCrossbar", childRoot, new Vector3(-1.56f, y, 3.28f), new Vector3(1.98f, .075f, .095f), .018f, "cream");
            RoundedBox("WindowSill", childRoot, new Vector3(-1.56f, .73f, 3.16f), new Vector3(2.12f, .11f, .32f), .035f, "woodLight");
            RoundedBox("TeamSign", childRoot, new Vector3(1.52f, 2.02f, 3.28f), new Vector3(2.58f, .54f, .10f), .035f, "blue");
            Text("TeamName", "回 收 保 洁 队", childRoot, new Vector3(1.52f, 2.03f, 3.20f), .27f, "cream");
            Text("MaterialLegend", "方片：普通料  圆环：滤芯料", childRoot, new Vector3(1.5f, 1.53f, 3.25f), .17f, "ink");
            BuildPlant(new Vector3(3.45f, .05f, 2.62f));
            for (int i = 0; i < 7; i++)
                RoundedBox("OuterFloorBoardJoint", childRoot, new Vector3(-3.77f + i * 1.20f, .005f, 0), new Vector3(.018f, .012f, 6.65f), .004f, "wood");
        }

        private void BuildGrid()
        {
            for (int y = 0; y < Rules.Size; y++)
                for (int x = 0; x < Rules.Size; x++)
                {
                    int id = y * Rules.Size + x;
                    Vector3 center = CellPosition(x, y);
                    var view = new CellVisual();
                    view.Root = Empty("Cell_" + (x + 1) + "_" + (y + 1), childRoot, center);
                    view.Floor = RoundedBox("CeramicFloorTile", view.Root, new Vector3(0, -.024f, 0),
                        new Vector3(.872f, .048f, .872f), .036f, (x + y) % 2 == 0 ? "tileA" : "tileB").GetComponent<Renderer>();
                    view.DirtRoot = Empty("ConfirmedDirt", view.Root, Vector3.zero);
                    view.RemainingText = Text("DirtRemaining", "", view.Root, new Vector3(0, .15f, -.31f), .21f, "ink").GetComponent<TextMesh>();
                    view.RemainingText.transform.rotation = camera.transform.rotation;
                    view.Selection = Frame("SelectedTarget", view.Root, .76f, .76f, .026f, false, "target");
                    view.Selection.localPosition = new Vector3(0, .015f, 0);
                    view.Coverage = Frame("BrushCoverageCorners", view.Root, .85f, .85f, .026f, true, "coverage");
                    view.Coverage.localPosition = new Vector3(0, .020f, 0);
                    view.Selection.gameObject.SetActive(false); view.Coverage.gameObject.SetActive(false);
                    view.CleanMark = CheckMark(view.Root);
                    view.CleanMark.gameObject.SetActive(false);
                    cells[id] = view;
                    var proxy = new GameObject("PickCell_" + id);
                    proxy.transform.SetParent(childRoot, false); proxy.transform.position = center + Vector3.up * .12f;
                    proxy.AddComponent<BoxCollider>().size = new Vector3(.84f, .28f, .84f);
                    CleanerCellPick pick = proxy.AddComponent<CleanerCellPick>(); pick.X = x; pick.Y = y;
                }
            for (int i = 0; i < Rules.Size; i++)
            {
                Vector3 x = CellPosition(i, 0), y = CellPosition(0, i);
                Text("Column_" + i, (i + 1).ToString(), childRoot, new Vector3(x.x, .13f, -2.88f), .23f, "ink").rotation = camera.transform.rotation;
                Text("Row_" + i, (i + 1).ToString(), childRoot, new Vector3(-2.82f, .13f, y.z), .23f, "ink").rotation = camera.transform.rotation;
            }
        }

        private void BuildSupplyCounter()
        {
            float supplyZ = CellPosition(Rules.SupplyX, Rules.SupplyY).z;
            Transform station = Empty("SupplyAndRecyclingCounter", childRoot, new Vector3(-3.48f, .01f, supplyZ));
            RoundedBox("BlueCounterCase", station, new Vector3(0, .34f, 0), new Vector3(1.01f, .62f, .76f), .09f, "blue");
            RoundedBox("CreamCounterTop", station, new Vector3(0, .68f, 0), new Vector3(1.07f, .09f, .81f), .07f, "cream");
            RoundedBox("SquareMaterialPort", station, new Vector3(-.24f, .34f, -.397f), new Vector3(.22f, .21f, .016f), .025f, "ink");
            Cylinder("RoundMaterialPort", station, new Vector3(.25f, .34f, -.412f), .105f, .025f, "ink", Vector3.forward);
            Text("SupplyCounterLabel", "回收 / 滤芯", childRoot, new Vector3(-3.48f, 1.03f, supplyZ), .20f, "ink").rotation = camera.transform.rotation;
            Cylinder("FilterSpare", station, new Vector3(-.23f, .85f, 0), .07f, .25f, "blueLight", Vector3.up);
            Cylinder("SoapBottle", station, new Vector3(.23f, .87f, 0), .075f, .26f, "coverage", Vector3.up);
            RoundedBox("SoapCap", station, new Vector3(.23f, 1.02f, 0), new Vector3(.10f, .06f, .10f), .018f, "cream");
            RoundedBox("ServicePointPad", childRoot, CellPosition(0, 0) + new Vector3(0, .004f, 0), new Vector3(.73f, .012f, .73f), .09f, "blueLight");
        }

        private void BuildCart()
        {
            cart = Empty("CleanerToolCart", childRoot, Vector3.zero);
            foreach (float x in new[] { -.25f, .25f })
                foreach (float z in new[] { -.19f, .19f })
                    Cylinder("CartWheel", cart, new Vector3(x, .095f, z), .082f, .065f, "ink", Vector3.right);
            RoundedBox("BlueCartCasing", cart, new Vector3(0, .26f, .02f), new Vector3(.48f, .30f, .46f), .085f, "blue");
            RoundedBox("WhiteCartShoulder", cart, new Vector3(0, .43f, .025f), new Vector3(.47f, .075f, .45f), .06f, "cream");
            Cylinder("OpenBucketWall", cart, new Vector3(-.065f, .55f, .045f), .153f, .21f, "blueLight", Vector3.up);
            Cylinder("BucketInterior", cart, new Vector3(-.065f, .66f, .045f), .127f, .018f, "ink", Vector3.up);
            Transform bucketRim = OvalBand("BucketWhiteRim", cart, .148f, .148f, .034f, .020f, "cream");
            bucketRim.localPosition = new Vector3(-.065f, .675f, .045f);
            bucketContents = Empty("ConfirmedBucketContents", cart, new Vector3(-.065f, .682f, .045f));
            foreach (float x in new[] { -.18f, .18f })
                Cylinder("CartHandleUpright", cart, new Vector3(x, .55f, .235f), .018f, .43f, "silver", Vector3.up);
            Cylinder("CartPushHandle", cart, new Vector3(0, .77f, .235f), .026f, .40f, "ink", Vector3.right);
            Cylinder("FilterPod", cart, new Vector3(.185f, .52f, .05f), .049f, .22f, "cream", Vector3.up);
            filterLamp = Cylinder("FilterStatusLamp", cart, new Vector3(.185f, .65f, .05f), .040f, .036f, "mint", Vector3.up);
            filterLampRenderer = filterLamp.GetComponent<Renderer>();
            detergentGauge = RoundedBox("DetergentGauge", cart, new Vector3(.255f, .31f, -.055f), new Vector3(.028f, .22f, .14f), .008f, "coverage");
            narrowHead = BuildHead(cart, false); wideHead = BuildHead(cart, true);
        }

        private Transform BuildHead(Transform parent, bool wide)
        {
            Transform head = Empty(wide ? "WideBrush" : "NarrowBrush", parent, new Vector3(0, .065f, -.35f));
            float width = wide ? .68f : .32f;
            RoundedBox("WhiteBrushHousing", head, new Vector3(0, .045f, 0), new Vector3(width, .07f, .17f), .045f, "cream");
            RoundedBox("BlueBrushFace", head, new Vector3(0, .08f, 0), new Vector3(width - .07f, .022f, .13f), .025f, "blue");
            for (int i = 0; i < (wide ? 7 : 3); i++)
                RoundedBox("BrushBristle", head, new Vector3(-width * .40f + i * width * .80f / (wide ? 6 : 2), .018f, -.045f),
                    new Vector3(.035f, .036f, .072f), .008f, "ink");
            return head;
        }

        private void BuildDirt(Transform parent, CellState cell)
        {
            int quantity = Mathf.Min(cell.Remaining, 6);
            if (cell.Dirt == DirtKind.Debris)
            {
                for (int i = 0; i < quantity; i++)
                {
                    Vector3 p = new Vector3(-.19f + (i % 3) * .185f, .040f + (i / 3) * .018f, -.035f + (i / 3) * .16f);
                    if (cell.Material == MaterialKind.Ordinary)
                    {
                        Transform shard = RoundedBox("SquareScrap", parent, p, new Vector3(.14f, .028f, .14f), .009f, "debris");
                        shard.localRotation = Quaternion.Euler(0, (cell.Id * 17 + i * 41) % 180, 0);
                    }
                    else
                    {
                        Transform ring = OvalBand("RingScrap", parent, .068f, .068f, .025f, .026f, "silver");
                        ring.localPosition = p;
                    }
                }
            }
            else if (cell.Dirt == DirtKind.Water)
            {
                float amount = Mathf.Clamp(cell.Remaining / (float)Mathf.Max(1, cell.InitialAmount), .25f, 1);
                Sphere("WaterLobeA", parent, new Vector3(-.10f, .018f, .035f), new Vector3(.40f * amount, .026f, .38f * amount), "water");
                Sphere("WaterLobeB", parent, new Vector3(.15f, .020f, .09f), new Vector3(.31f * amount, .027f, .29f * amount), "water");
                RoundedBox("PuddleReflection", parent, new Vector3(-.08f, .035f, .02f), new Vector3(.17f * amount, .006f, .024f), .006f, "waterShine");
            }
            else if (cell.Dirt == DirtKind.Stubborn)
            {
                float amount = Mathf.Clamp(cell.Remaining / (float)Mathf.Max(1, cell.InitialAmount), .28f, 1);
                Sphere("WarmStainA", parent, new Vector3(-.095f, .026f, .045f), new Vector3(.41f * amount, .040f, .37f * amount), "stain");
                Sphere("WarmStainB", parent, new Vector3(.14f, .023f, .05f), new Vector3(.29f * amount, .036f, .31f * amount), "stain");
                for (int i = 0; i < Mathf.Min(cell.Remaining, 3); i++)
                {
                    Transform scratch = RoundedBox("StainCrustScratch", parent, new Vector3(-.17f + i * .145f, .049f, .065f),
                        new Vector3(.025f, .009f, .21f * amount), .008f, "stainDark");
                    scratch.localRotation = Quaternion.Euler(0, 27, 0);
                }
            }
        }

        private void BuildObstacle(Transform parent)
        {
            RoundedBox("FixedStorageCrate", parent, new Vector3(0, .17f, .035f), new Vector3(.62f, .32f, .60f), .055f, "wood");
            for (int i = 0; i < 3; i++)
                RoundedBox("CrateTopSlat", parent, new Vector3(-.18f + i * .18f, .344f, .035f), new Vector3(.13f, .025f, .58f), .008f, "woodLight");
            RoundedBox("CrateBrace", parent, new Vector3(0, .18f, -.278f), new Vector3(.52f, .046f, .022f), .008f, "cream");
        }

        private void BuildPlant(Vector3 position)
        {
            Cylinder("CornerPlantPot", childRoot, position + Vector3.up * .18f, .20f, .35f, "cream", Vector3.up);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                Transform leaf = Sphere("MorningPlantLeaf", childRoot,
                    position + new Vector3(Mathf.Cos(angle) * .13f, .57f, Mathf.Sin(angle) * .13f),
                    new Vector3(.19f, .46f, .12f), "mint");
                leaf.localRotation = Quaternion.Euler(12 * Mathf.Sin(angle), i * 72, 18 * Mathf.Cos(angle));
            }
        }

        private Transform Frame(string name, Transform parent, float width, float depth, float stroke, bool cornersOnly, string material)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            float x = width * .5f, z = depth * .5f;
            foreach (int side in new[] { -1, 1 })
            {
                if (cornersOnly)
                {
                    foreach (int end in new[] { -1, 1 })
                    {
                        FlatRect(vertices, triangles, end * (x - .065f), side * z, .16f, stroke);
                        FlatRect(vertices, triangles, side * x, end * (z - .065f), stroke, .16f);
                    }
                }
                else
                {
                    FlatRect(vertices, triangles, 0, side * z, width + stroke, stroke);
                    FlatRect(vertices, triangles, side * x, 0, stroke, depth + stroke);
                }
            }
            return MeshObject(name, parent, vertices, triangles, material);
        }

        private static void FlatRect(List<Vector3> vertices, List<int> triangles, float x, float z, float width, float depth)
        {
            float hx = width * .5f, hz = depth * .5f;
            Quad(vertices, triangles, new Vector3(x - hx, 0, z - hz), new Vector3(x - hx, 0, z + hz),
                new Vector3(x + hx, 0, z + hz), new Vector3(x + hx, 0, z - hz));
        }

        private Transform CheckMark(Transform parent)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Quad(vertices, triangles, new Vector3(.22f, .023f, .22f), new Vector3(.185f, .023f, .25f),
                new Vector3(.245f, .023f, .30f), new Vector3(.27f, .023f, .265f));
            Quad(vertices, triangles, new Vector3(.245f, .023f, .30f), new Vector3(.28f, .023f, .315f),
                new Vector3(.385f, .023f, .20f), new Vector3(.35f, .023f, .17f));
            return MeshObject("CleanCheckMark", parent, vertices, triangles, "mint");
        }

        // Shared original mesh-building recipes follow; no imported art or gameplay colliders.
        private Transform Empty(string name, Transform parent, Vector3 position)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = position; return obj.transform;
        }

        private Transform Sphere(string name, Transform parent, Vector3 p, Vector3 size, string material)
        {
            const int around = 12, vertical = 8;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int y = 0; y <= vertical; y++)
            {
                float elevation = Mathf.PI * y / vertical;
                for (int x = 0; x <= around; x++)
                {
                    float a = Mathf.PI * 2 * x / around;
                    vertices.Add(new Vector3(Mathf.Sin(elevation) * Mathf.Cos(a) * .5f,
                        Mathf.Cos(elevation) * .5f, Mathf.Sin(elevation) * Mathf.Sin(a) * .5f));
                }
            }
            for (int y = 0; y < vertical; y++)
                for (int x = 0; x < around; x++)
                {
                    int a = y * (around + 1) + x, b = a + around + 1;
                    if (y > 0) { triangles.Add(a); triangles.Add(a + 1); triangles.Add(b); }
                    if (y < vertical - 1) { triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b); }
                }
            Transform obj = MeshObject(name, parent, vertices, triangles, material);
            obj.localPosition = p; obj.localScale = size; return obj;
        }

        private Transform Cylinder(string name, Transform parent, Vector3 p, float radius, float length, string material, Vector3 axis)
        {
            Transform obj = Primitive(name, PrimitiveType.Cylinder, parent, p, new Vector3(radius * 2, length * .5f, radius * 2), material);
            obj.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized); return obj;
        }

        private Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 p, Vector3 size, string material)
        {
            GameObject obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = p; obj.transform.localScale = size;
            Collider collision = obj.GetComponent<Collider>();
            if (collision != null) { collision.enabled = false; UnityEngine.Object.Destroy(collision); }
            obj.GetComponent<Renderer>().sharedMaterial = materials[material]; return obj.transform;
        }

        private Transform Text(string name, string value, Transform parent, Vector3 p, float size, string material)
        {
            Transform obj = Empty(name, parent, p);
            TextMesh text = obj.gameObject.AddComponent<TextMesh>();
            text.text = value; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            // TextMesh geometry grows with raster fontSize. Keep the requested approximate
            // world glyph height independent of the 64px atlas resolution (10px = 1 unit).
            text.fontSize = 64; text.characterSize = size * 10f / text.fontSize;
            text.color = materials[material].color;
            if (font != null) { text.font = font; obj.GetComponent<Renderer>().sharedMaterial = font.material; }
            return obj;
        }

        private Transform RoundedBox(string name, Transform parent, Vector3 p, Vector3 size, float radius, string material)
        {
            radius = Mathf.Min(radius, Mathf.Min(size.x, size.z) * .48f);
            float bevel = Mathf.Min(.018f, Mathf.Min(size.y * .25f, Mathf.Min(size.x, size.z) * .20f));
            var loops = new List<Vector3[]>();
            loops.Add(RoundedOutline(size.x - bevel * 2, size.z - bevel * 2, Mathf.Max(.001f, radius - bevel), -size.y * .5f));
            loops.Add(RoundedOutline(size.x, size.z, radius, -size.y * .5f + bevel));
            loops.Add(RoundedOutline(size.x, size.z, radius, size.y * .5f - bevel));
            loops.Add(RoundedOutline(size.x - bevel * 2, size.z - bevel * 2, Mathf.Max(.001f, radius - bevel), size.y * .5f));
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int layer = 0; layer < loops.Count - 1; layer++)
                for (int i = 0; i < loops[0].Length; i++)
                {
                    int j = (i + 1) % loops[0].Length;
                    Quad(vertices, triangles, loops[layer][i], loops[layer + 1][i], loops[layer + 1][j], loops[layer][j]);
                }
            for (int i = 0; i < loops[0].Length; i++)
            {
                int j = (i + 1) % loops[0].Length;
                Triangle(vertices, triangles, new Vector3(0, size.y * .5f, 0), loops[3][j], loops[3][i]);
                Triangle(vertices, triangles, new Vector3(0, -size.y * .5f, 0), loops[0][i], loops[0][j]);
            }
            Transform obj = MeshObject(name, parent, vertices, triangles, material); obj.localPosition = p; return obj;
        }

        private static Vector3[] RoundedOutline(float width, float depth, float radius, float y)
        {
            var result = new List<Vector3>();
            for (int corner = 0; corner < 4; corner++)
            {
                float centerX = corner == 0 || corner == 3 ? width * .5f - radius : -width * .5f + radius;
                float centerZ = corner < 2 ? depth * .5f - radius : -depth * .5f + radius;
                for (int step = 0; step <= 3; step++)
                {
                    float angle = (corner * 90f + step * 30f) * Mathf.Deg2Rad;
                    result.Add(new Vector3(centerX + Mathf.Cos(angle) * radius, y, centerZ + Mathf.Sin(angle) * radius));
                }
            }
            return result.ToArray();
        }

        private Transform OvalBand(string name, Transform parent, float rx, float rz, float width, float height, string material)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64, b = (i + 1) * Mathf.PI * 2 / 64;
                Vector3 pa = new Vector3(Mathf.Cos(a) * rx, 0, Mathf.Sin(a) * rz);
                Vector3 pb = new Vector3(Mathf.Cos(b) * rx, 0, Mathf.Sin(b) * rz);
                Vector3 na = new Vector3(Mathf.Cos(a) / rx, 0, Mathf.Sin(a) / rz).normalized * width * .5f;
                Vector3 nb = new Vector3(Mathf.Cos(b) / rx, 0, Mathf.Sin(b) / rz).normalized * width * .5f;
                Vector3 up = Vector3.up * height * .5f;
                Quad(vertices, triangles, pa + na + up, pa - na + up, pb - nb + up, pb + nb + up);
                Quad(vertices, triangles, pa + na - up, pb + nb - up, pb - nb - up, pa - na - up);
                Quad(vertices, triangles, pa + na - up, pa + na + up, pb + nb + up, pb + nb - up);
                Quad(vertices, triangles, pa - na - up, pb - nb - up, pb - nb + up, pa - na + up);
            }
            return MeshObject(name, parent, vertices, triangles, material);
        }

        private Transform Arrow(string name, Transform parent, Vector3 p, string material)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Triangle(vertices, triangles, new Vector3(-.09f, 0, -.08f), new Vector3(0, 0, .13f), new Vector3(.09f, 0, -.08f));
            Transform obj = MeshObject(name, parent, vertices, triangles, material); obj.localPosition = p; return obj;
        }

        private Transform MeshObject(string name, Transform parent, List<Vector3> vertices, List<int> triangles, string material)
        {
            var mesh = new Mesh { name = "CleanerMesh_" + name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            Transform obj = Empty(name, parent, Vector3.zero); obj.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.gameObject.AddComponent<MeshRenderer>().sharedMaterial = materials[material]; return obj;
        }

        private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private static void Triangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        private void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i); child.gameObject.SetActive(false);
                foreach (MeshFilter filter in child.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && meshes.Remove(filter.sharedMesh)) UnityEngine.Object.Destroy(filter.sharedMesh);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    public sealed class CleanerCellPick : MonoBehaviour
    {
        public int X, Y;
    }
}
