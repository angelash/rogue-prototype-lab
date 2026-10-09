using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace SnakeHatchery
{
    /// <summary>Original tabletop hatchery art. All ownership, occupation and routes come from Core.</summary>
    public sealed class SnakeSceneView : MonoBehaviour
    {
        public const float CellSize = .84f;
        private const float FloorTop = .17f;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Dictionary<int, SegmentVisual> segments = new Dictionary<int, SegmentVisual>();
        private readonly CellVisual[] cells = new CellVisual[Rules.Width * Rules.Height];
        private Camera camera;
        private Font font;
        private Transform childRoot, bodiesRoot, linksRoot, pathsRoot, cutMarker, cutLabel, inventoryRoot;
        private TextMesh inventoryCount;
        private GridPoint[] draftRoute = new GridPoint[0];
        private string lastLinkSignature, lastPathSignature, lastInventorySignature;

        private sealed class SegmentVisual
        {
            public Transform Root, Model, Head, Badge;
            public TextMesh TypeLabel, IdentityLabel;
            public Renderer IdentityRim;
            public SegmentKind Kind;
            public bool Built;
        }

        private sealed class CellVisual
        {
            public Transform Root, Items;
            public TextMesh Label;
            public string Signature;
        }

        public Transform SceneRoot { get { return childRoot; } }

        public void Build(Camera gameCamera)
        {
            if (gameCamera == null) throw new ArgumentNullException(nameof(gameCamera));
            if (childRoot != null) return;
            camera = gameCamera;
            font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            childRoot = new GameObject("SnakeHatchery_OriginalArt").transform;
            BuildPalette(); ConfigureCameraAndLight(); BuildTableAndTray(); BuildGrid(); BuildDockAndInventory();
            bodiesRoot = Empty("ActualOwnedSegments", childRoot, Vector3.zero);
            linksRoot = Empty("ActualBodyOrderLinks", childRoot, Vector3.zero);
            pathsRoot = Empty("PublicRoutePreview", childRoot, Vector3.zero);
            cutMarker = Empty("CandidateCutBoundary", childRoot, Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform blade = RoundedBox("CutFork", cutMarker, new Vector3(0, .04f, 0), new Vector3(.32f, .027f, .045f), .01f, "cut");
                blade.localRotation = Quaternion.Euler(0, side * 42, 0);
                Transform loop = OvalBand("CutHandle", cutMarker, .048f, .048f, .020f, .022f, "cut");
                loop.localPosition = new Vector3(side * .105f, .04f, -.095f);
            }
            cutLabel = Text("CutCandidateLabel", "切", childRoot, Vector3.zero, .21f, "ink");
            cutMarker.gameObject.SetActive(false); cutLabel.gameObject.SetActive(false);
        }

        public Vector3 CellPosition(int x, int y)
        {
            if (!Rules.InBounds(x, y)) throw new ArgumentOutOfRangeException("cell");
            return new Vector3((x - (Rules.Width - 1) * .5f) * CellSize, FloorTop,
                (y - (Rules.Height - 1) * .5f) * CellSize);
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
                SnakeCellPick pick = hit.collider.GetComponent<SnakeCellPick>();
                if (pick == null) continue;
                x = pick.X; y = pick.Y; return true;
            }
            return false;
        }

        public void SetDraftRoute(GridPoint[] route)
        {
            int count = route == null ? 0 : route.Length;
            bool same = count == draftRoute.Length;
            if (same)
                for (int i = 0; i < count; i++)
                    if (route[i].X != draftRoute[i].X || route[i].Y != draftRoute[i].Y) { same = false; break; }
            if (same) return;
            draftRoute = new GridPoint[count];
            for (int i = 0; i < count; i++) draftRoute[i] = new GridPoint(route[i].X, route[i].Y);
            lastPathSignature = null;
        }

        public void Render(Session session, int selectedCut, int selectedClone)
        {
            if (session == null || childRoot == null) return;
            RoundSnapshot state = session.State;
            var seen = new HashSet<int>();
            var linkKey = new StringBuilder();
            RenderSnake(state.Main, selectedClone, seen, linkKey);
            foreach (SnakeState clone in state.Clones) RenderSnake(clone, selectedClone, seen, linkKey);
            foreach (var entry in segments) entry.Value.Root.gameObject.SetActive(seen.Contains(entry.Key));
            string linkSignature = linkKey.ToString();
            if (lastLinkSignature != linkSignature)
            {
                ClearChildren(linksRoot);
                BuildLinks(state.Main);
                foreach (SnakeState clone in state.Clones) BuildLinks(clone);
                lastLinkSignature = linkSignature;
            }
            foreach (CellState cell in state.Cells)
            {
                var ground = new List<ModuleState>();
                foreach (ModuleState module in state.Modules)
                    if (module.Location == ModuleLocation.Ground && module.X == cell.X && module.Y == cell.Y) ground.Add(module);
                ground.Sort((a, b) => a.Id.CompareTo(b.Id));
                var key = new StringBuilder(); key.Append(cell.Blocked).Append(':').Append(cell.Cargo);
                foreach (ModuleState module in ground) key.Append('/').Append(module.Id).Append(':').Append((int)module.Kind);
                CellVisual view = cells[cell.Id];
                if (view.Signature != key.ToString())
                {
                    ClearChildren(view.Items);
                    if (cell.Blocked) BuildRockBarrier(view.Items);
                    else
                    {
                        if (cell.Cargo > 0) BuildCargo(view.Items, cell.Cargo);
                        for (int i = 0; i < Mathf.Min(ground.Count, 3); i++)
                        {
                            Transform token = Empty("GroundModule_" + ground[i].Id, view.Items,
                                new Vector3(-.19f + i * .19f, .065f, .10f));
                            token.localScale = Vector3.one * .50f;
                            BuildModuleShape(token, ground[i].Kind);
                        }
                    }
                    view.Label.text = cell.Blocked ? "墙" :
                        (cell.Cargo > 0 ? "货×" + cell.Cargo : "") +
                        (ground.Count > 0 ? (cell.Cargo > 0 ? "\n" : "") + "模×" + ground.Count : "");
                    view.Label.transform.localPosition = new Vector3(0, cell.Blocked ? .34f : .12f, -.34f);
                    view.Signature = key.ToString();
                }
                view.Label.transform.rotation = camera.transform.rotation;
            }
            var inventoryKey = new StringBuilder();
            foreach (int id in state.InventoryModuleIds) inventoryKey.Append(id).Append(',');
            if (lastInventorySignature != inventoryKey.ToString())
            {
                ClearChildren(inventoryRoot);
                for (int i = 0; i < state.InventoryModuleIds.Length; i++)
                    foreach (ModuleState module in state.Modules)
                        if (module.Id == state.InventoryModuleIds[i])
                        {
                            Transform token = Empty("StoredModule_" + module.Id, inventoryRoot, new Vector3(-.15f + i * .30f, 0, 0));
                            token.localScale = Vector3.one * .48f; BuildModuleShape(token, module.Kind); break;
                        }
                lastInventorySignature = inventoryKey.ToString();
            }
            inventoryCount.text = "盒 " + state.InventoryModuleIds.Length;
            inventoryCount.transform.rotation = camera.transform.rotation;
            RenderCut(state.Main, selectedCut);
            RenderPaths(state, selectedClone);
        }

        private void RenderSnake(SnakeState snake, int selectedClone, HashSet<int> seen, StringBuilder linkKey)
        {
            linkKey.Append(snake.Id).Append('=');
            for (int i = 0; i < snake.Segments.Length; i++)
            {
                SegmentState segment = snake.Segments[i];
                SegmentVisual view;
                if (!segments.TryGetValue(segment.ModuleId, out view))
                {
                    view = BuildSegmentVisual(segment.ModuleId); segments.Add(segment.ModuleId, view);
                }
                if (!view.Built || view.Kind != segment.Kind)
                {
                    ClearChildren(view.Model); BuildModuleShape(view.Model, segment.Kind);
                    view.Kind = segment.Kind; view.Built = true;
                }
                view.Root.gameObject.SetActive(true); view.Root.position = CellPosition(segment.X, segment.Y);
                view.Head.gameObject.SetActive(i == 0); view.Badge.gameObject.SetActive(i == 0);
                view.Head.localRotation = Quaternion.Euler(0, (int)snake.Direction * 90, 0);
                view.TypeLabel.text = TypeChar(segment.Kind) + (i + 1);
                view.TypeLabel.color = materials[segment.Kind == SegmentKind.Warehouse ? "ink" : "cream"].color;
                view.TypeLabel.transform.rotation = camera.transform.rotation;
                view.Badge.rotation = camera.transform.rotation;
                view.IdentityLabel.text = snake.IsMain ? "主" : "分" + snake.Id;
                string identity = snake.IsMain ? "cream" : snake.Id % 2 == 1 ? "cloneA" : "cloneB";
                view.IdentityRim.sharedMaterial = materials[snake.Id == selectedClone ? "selected" : identity];
                seen.Add(segment.ModuleId);
                linkKey.Append(segment.ModuleId).Append('@').Append(segment.X).Append(',').Append(segment.Y).Append(';');
            }
        }

        private void BuildLinks(SnakeState snake)
        {
            for (int i = 1; i < snake.Segments.Length; i++)
            {
                Vector3 a = CellPosition(snake.Segments[i - 1].X, snake.Segments[i - 1].Y);
                Vector3 b = CellPosition(snake.Segments[i].X, snake.Segments[i].Y);
                Vector3 delta = b - a;
                Cylinder("OrderLink_" + snake.Id + "_" + i, linksRoot, (a + b) * .5f + Vector3.up * .125f,
                    .098f, Mathf.Max(.06f, delta.magnitude - .43f), "link", delta.normalized);
            }
        }

        private void RenderCut(SnakeState main, int keepSegments)
        {
            bool show = keepSegments >= Rules.MinMainSegments && keepSegments < main.Segments.Length;
            cutMarker.gameObject.SetActive(show); cutLabel.gameObject.SetActive(show);
            if (!show) return;
            SegmentState a = main.Segments[keepSegments - 1], b = main.Segments[keepSegments];
            Vector3 p = CellPosition(a.X, a.Y), q = CellPosition(b.X, b.Y);
            Vector3 delta = (q - p).normalized;
            cutMarker.position = (p + q) * .5f + Vector3.up * .32f;
            cutMarker.rotation = Quaternion.Euler(0, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 0);
            cutLabel.position = (p + q) * .5f + new Vector3(-delta.z, 0, delta.x) * .44f + Vector3.up * .15f;
            cutLabel.rotation = camera.transform.rotation;
        }

        private void RenderPaths(RoundSnapshot state, int selectedClone)
        {
            var key = new StringBuilder(); key.Append(selectedClone).Append(':');
            AddRouteKey(key, draftRoute);
            foreach (SnakeState clone in state.Clones)
            {
                key.Append('|').Append(clone.Id).Append(':').Append(clone.RouteIndex);
                AddRouteKey(key, clone.Route);
            }
            if (lastPathSignature == key.ToString()) return;
            ClearChildren(pathsRoot);
            foreach (SnakeState clone in state.Clones)
            {
                string color = clone.Id % 2 == 1 ? "cloneA" : "cloneB";
                BuildRoute(clone.Route, color, false, clone.Id == selectedClone ? .028f : .016f);
                if (clone.RouteIndex >= 0 && clone.RouteIndex < clone.Route.Length)
                {
                    GridPoint point = clone.Route[clone.RouteIndex];
                    if (Rules.InBounds(point.X, point.Y))
                    {
                        Transform ring = OvalBand("CoreRouteIndex_" + clone.Id, pathsRoot, .075f, .075f, .026f, .012f, "selected");
                        ring.position = RoutePoint(point) + Vector3.up * .010f;
                    }
                }
            }
            BuildRoute(draftRoute, "draft", true, .023f);
            lastPathSignature = key.ToString();
        }

        private static void AddRouteKey(StringBuilder key, GridPoint[] route)
        {
            foreach (GridPoint point in route) key.Append(point.X).Append(',').Append(point.Y).Append(';');
        }

        private Vector3 RoutePoint(GridPoint point)
        {
            return CellPosition(point.X, point.Y) + new Vector3(.30f, .018f, .30f);
        }

        private void BuildRoute(GridPoint[] route, string color, bool dashed, float width)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < route.Length; i++)
            {
                if (!Rules.InBounds(route[i].X, route[i].Y)) continue;
                Vector3 p = RoutePoint(route[i]);
                Cylinder("PublicRouteNode", pathsRoot, p, .040f, .010f, color, Vector3.up);
                if (i == 0 || !Rules.InBounds(route[i - 1].X, route[i - 1].Y)) continue;
                Vector3 a = RoutePoint(route[i - 1]), delta = p - a;
                Vector3 normal = new Vector3(-delta.z, 0, delta.x).normalized * width * .5f;
                int steps = dashed ? 4 : 1;
                for (int part = 0; part < steps; part++)
                {
                    Vector3 from = a + delta * (part / (float)steps);
                    Vector3 to = a + delta * ((part + (dashed ? .60f : 1)) / steps);
                    Quad(vertices, triangles, from - normal, from + normal, to + normal, to - normal);
                }
            }
            if (vertices.Count > 0) MeshObject("PublicRouteLine", pathsRoot, vertices, triangles, color);
        }

        public void Dispose()
        {
            if (childRoot != null) UnityEngine.Object.Destroy(childRoot.gameObject);
            foreach (Material material in materials.Values) UnityEngine.Object.Destroy(material);
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            materials.Clear(); meshes.Clear(); segments.Clear(); Array.Clear(cells, 0, cells.Length);
            childRoot = null; lastLinkSignature = lastPathSignature = lastInventorySignature = null;
            draftRoute = new GridPoint[0];
        }

        private void OnDestroy() { Dispose(); }
        private static string TypeChar(SegmentKind kind) { return kind == SegmentKind.Collector ? "采" : kind == SegmentKind.Warehouse ? "仓" : "基"; }

        private void ConfigureCameraAndLight()
        {
            float aspect = camera.pixelRect.height > 1 ? camera.pixelRect.width / camera.pixelRect.height : camera.aspect;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(3.35f, 5.15f / Mathf.Max(.6f, aspect));
            camera.transform.position = new Vector3(.30f, 8.8f, -9.0f);
            camera.transform.LookAt(new Vector3(0, .25f, .20f));
            camera.nearClipPlane = .1f; camera.farClipPlane = 50f;
            camera.backgroundColor = new Color(.21f, .28f, .29f); camera.clearFlags = CameraClearFlags.SolidColor;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.64f, .69f, .66f);
            Transform sun = Empty("TabletopSoftKey", childRoot, Vector3.zero);
            Light light = sun.gameObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = .78f; light.color = new Color(1, .95f, .84f); light.shadows = LightShadows.Soft;
            sun.rotation = Quaternion.Euler(50, -28, 0);
        }

        private void BuildPalette()
        {
            AddMaterial("desk", new Color(.48f, .33f, .24f));
            AddMaterial("wood", new Color(.76f, .57f, .35f));
            AddMaterial("cream", new Color(.98f, .96f, .84f));
            AddMaterial("tray", new Color(.70f, .82f, .71f));
            AddMaterial("soilA", new Color(.62f, .73f, .52f));
            AddMaterial("soilB", new Color(.55f, .66f, .46f));
            AddMaterial("basic", new Color(.22f, .57f, .38f));
            AddMaterial("collector", new Color(.20f, .59f, .72f));
            AddMaterial("warehouse", new Color(.91f, .63f, .26f));
            AddMaterial("link", new Color(.26f, .43f, .28f));
            AddMaterial("ink", new Color(.13f, .22f, .21f));
            AddMaterial("rock", new Color(.56f, .62f, .54f));
            AddMaterial("rockLight", new Color(.74f, .78f, .68f));
            AddMaterial("cargo", new Color(.95f, .48f, .32f));
            AddMaterial("leaf", new Color(.30f, .58f, .33f));
            AddMaterial("cloneA", new Color(.65f, .40f, .74f));
            AddMaterial("cloneB", new Color(.32f, .43f, .71f));
            AddMaterial("selected", new Color(.25f, .74f, .74f), 0, true);
            AddMaterial("draft", new Color(.98f, .75f, .26f), 0, true);
            AddMaterial("cut", new Color(.96f, .44f, .39f));
            AddMaterial("dock", new Color(.29f, .58f, .54f));
        }

        private void AddMaterial(string id, Color color, float metal = 0, bool glow = false)
        {
            var material = new Material(Shader.Find("Standard")) { name = "SnakeOriginal_" + id, color = color };
            material.SetFloat("_Metallic", metal); material.SetFloat("_Glossiness", .22f);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .10f); }
            materials.Add(id, material);
        }

        private void BuildTableAndTray()
        {
            RoundedBox("WarmTabletop", childRoot, new Vector3(0, -.10f, 0), new Vector3(10.1f, .20f, 7.1f), .25f, "desk");
            RoundedBox("HatcheryTrayBase", childRoot, new Vector3(0, .07f, 0), new Vector3(7.18f, .14f, 5.50f), .24f, "cream");
            foreach (int side in new[] { -1, 1 })
            {
                RoundedBox("TrayLongRim", childRoot, new Vector3(0, .18f, side * 2.66f), new Vector3(7.13f, .18f, .17f), .065f, "tray");
                RoundedBox("TrayShortRim", childRoot, new Vector3(side * 3.49f, .18f, 0), new Vector3(.17f, .18f, 5.38f), .065f, "tray");
            }
            RoundedBox("HatcheryNameplate", childRoot, new Vector3(0, .29f, 3.05f), new Vector3(4.0f, .40f, .11f), .025f, "dock");
            Text("HatcheryTitle", "贪 吃 蛇 孵 化 场", childRoot, new Vector3(0, .30f, 2.98f), .29f, "cream");
            BuildPlanter(new Vector3(-4.20f, .04f, 2.39f));
            BuildPlanter(new Vector3(4.20f, .04f, 2.39f));
            for (int i = 0; i < 8; i++)
                RoundedBox("TableWoodJoint", childRoot, new Vector3(-4.47f + i * 1.28f, .006f, 0), new Vector3(.017f, .010f, 6.8f), .004f, "wood");
        }

        private void BuildGrid()
        {
            for (int y = 0; y < Rules.Height; y++)
                for (int x = 0; x < Rules.Width; x++)
                {
                    int id = Rules.CellId(x, y);
                    var view = new CellVisual();
                    view.Root = Empty("HatchCell_" + (x + 1) + "_" + (y + 1), childRoot, CellPosition(x, y));
                    RoundedBox("MossPad", view.Root, new Vector3(0, -.025f, 0), new Vector3(.806f, .05f, .806f), .052f,
                        (x + y) % 2 == 0 ? "soilA" : "soilB");
                    view.Items = Empty("ActualGroundCargoAndModules", view.Root, Vector3.zero);
                    view.Label = Text("GroundStateLabel", "", view.Root, new Vector3(0, .12f, -.34f), .20f, "ink").GetComponent<TextMesh>();
                    cells[id] = view;
                    var proxy = new GameObject("PickHatchCell_" + id); proxy.transform.SetParent(childRoot, false);
                    proxy.transform.position = CellPosition(x, y) + Vector3.up * .12f;
                    proxy.AddComponent<BoxCollider>().size = new Vector3(.78f, .28f, .78f);
                    SnakeCellPick pick = proxy.AddComponent<SnakeCellPick>(); pick.X = x; pick.Y = y;
                }
            for (int x = 0; x < Rules.Width; x++)
                Text("Column_" + x, (x + 1).ToString(), childRoot, new Vector3(CellPosition(x, 0).x, .18f, -2.96f), .23f, "cream").rotation = camera.transform.rotation;
            for (int y = 0; y < Rules.Height; y++)
                Text("Row_" + y, (y + 1).ToString(), childRoot, new Vector3(3.68f, .18f, CellPosition(0, y).z), .23f, "cream").rotation = camera.transform.rotation;
        }

        private void BuildDockAndInventory()
        {
            Vector3 dock = CellPosition(Rules.DockX, Rules.DockY);
            Transform frame = OvalBand("ActualDeliveryAndReclaimDock", childRoot, .34f, .34f, .035f, .014f, "cream");
            frame.position = dock + Vector3.up * .008f;
            Text("DockCellMark", "交", childRoot, dock + new Vector3(-.35f, .06f, -.31f), .20f, "cream").rotation = camera.transform.rotation;
            Transform station = Empty("DeliveryAndReclaimStand", childRoot, new Vector3(-4.19f, .015f, dock.z));
            RoundedBox("DockStandBase", station, new Vector3(0, .17f, 0), new Vector3(.89f, .33f, .72f), .10f, "wood");
            RoundedBox("DockStandTop", station, new Vector3(0, .355f, 0), new Vector3(.97f, .07f, .77f), .065f, "cream");
            for (int i = 0; i < 3; i++) Cylinder("StorageCanister", station, new Vector3(-.26f + i * .26f, .49f, 0), .079f, .22f, "dock", Vector3.up);
            Text("DockStandLabel", "货物交付", childRoot, new Vector3(-4.19f, 1.10f, dock.z), .21f, "cream").rotation = camera.transform.rotation;
            Transform box = Empty("ModuleInventoryBox", childRoot, new Vector3(4.22f, .02f, dock.z));
            RoundedBox("ModuleBoxBase", box, new Vector3(0, .105f, 0), new Vector3(.75f, .21f, .72f), .085f, "wood");
            RoundedBox("ModuleBoxInset", box, new Vector3(0, .217f, 0), new Vector3(.65f, .020f, .59f), .05f, "ink");
            inventoryRoot = Empty("ActualStoredModules", box, new Vector3(0, .23f, 0));
            inventoryCount = Text("ModuleBoxCount", "盒 0", childRoot, new Vector3(4.22f, .49f, dock.z - .25f), .22f, "cream").GetComponent<TextMesh>();
        }

        private SegmentVisual BuildSegmentVisual(int moduleId)
        {
            var view = new SegmentVisual();
            view.Root = Empty("OwnedModule_" + moduleId, bodiesRoot, Vector3.zero);
            view.Model = Empty("ModuleKindShape", view.Root, Vector3.zero);
            view.TypeLabel = Text("CurrentTypeAndOrder", "", view.Root, new Vector3(0, .34f, -.20f), .22f, "cream").GetComponent<TextMesh>();
            view.Head = Empty("HeadIdentityFeatures", view.Root, Vector3.zero);
            Sphere("Snout", view.Head, new Vector3(0, .155f, .29f), new Vector3(.28f, .16f, .20f), "cream");
            foreach (float x in new[] { -.13f, .13f })
            {
                Sphere("EyeWhite", view.Head, new Vector3(x, .265f, .205f), new Vector3(.125f, .11f, .092f), "cream");
                Sphere("EyePupil", view.Head, new Vector3(x, .27f, .248f), new Vector3(.047f, .055f, .024f), "ink");
            }
            Transform identityRim = OvalBand("EntityIdentityRim", view.Head, .336f, .303f, .024f, .020f, "cream");
            identityRim.localPosition = new Vector3(0, .018f, 0); view.IdentityRim = identityRim.GetComponent<Renderer>();
            view.Badge = Empty("StableEntityBadge", view.Root, new Vector3(0, .59f, 0));
            RoundedBox("IdentityBadgeBack", view.Badge, Vector3.zero, new Vector3(.47f, .23f, .025f), .01f, "cream");
            view.IdentityLabel = Text("EntityStableId", "主", view.Badge, new Vector3(0, 0, -.025f), .20f, "ink").GetComponent<TextMesh>();
            return view;
        }

        private void BuildModuleShape(Transform parent, SegmentKind kind)
        {
            string color = kind == SegmentKind.Collector ? "collector" : kind == SegmentKind.Warehouse ? "warehouse" : "basic";
            Sphere("SoftModuleBody", parent, new Vector3(0, .145f, 0), new Vector3(.63f, .30f, .58f), color);
            if (kind == SegmentKind.Basic)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform leaf = Sphere("BasicLeafCap", parent, new Vector3(side * .075f, .304f, .035f), new Vector3(.16f, .045f, .27f), "leaf");
                    leaf.localRotation = Quaternion.Euler(0, side * 28, 0);
                }
            }
            else if (kind == SegmentKind.Collector)
            {
                for (int i = 0; i < 3; i++)
                {
                    Cylinder("CollectorForkStem", parent, new Vector3(-.125f + i * .125f, .31f, .075f), .021f, .17f, "cream", Vector3.up);
                    Sphere("CollectorForkTip", parent, new Vector3(-.125f + i * .125f, .40f, .075f), new Vector3(.080f, .055f, .080f), "collector");
                }
            }
            else
            {
                RoundedBox("WarehouseBoxCap", parent, new Vector3(0, .32f, .045f), new Vector3(.31f, .14f, .28f), .043f, "wood");
                RoundedBox("WarehouseLatch", parent, new Vector3(0, .355f, -.101f), new Vector3(.08f, .050f, .013f), .005f, "cream");
            }
        }

        private void BuildCargo(Transform parent, int quantity)
        {
            RoundedBox("CargoSeedBed", parent, new Vector3(0, .035f, .045f), new Vector3(.52f, .05f, .41f), .10f, "wood");
            for (int i = 0; i < Mathf.Min(quantity, 4); i++)
            {
                Vector3 p = new Vector3(-.12f + i % 2 * .24f, .115f, -.065f + i / 2 * .20f);
                Sphere("CargoFruit", parent, p, new Vector3(.17f, .15f, .17f), "cargo");
                Cylinder("FruitStem", parent, p + Vector3.up * .094f, .012f, .06f, "leaf", Vector3.up);
            }
        }

        private void BuildRockBarrier(Transform parent)
        {
            Sphere("ChannelRock", parent, new Vector3(0, .13f, 0), new Vector3(.66f, .26f, .64f), "rock");
            Sphere("ChannelRockChip", parent, new Vector3(.18f, .21f, .09f), new Vector3(.26f, .13f, .28f), "rockLight");
            Sphere("BarrierMoss", parent, new Vector3(-.13f, .235f, .08f), new Vector3(.27f, .025f, .20f), "leaf");
        }

        private void BuildPlanter(Vector3 p)
        {
            Cylinder("DesktopPlanter", childRoot, p + Vector3.up * .17f, .22f, .34f, "cream", Vector3.up);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                Transform leaf = Sphere("DesktopLeaf", childRoot, p + new Vector3(Mathf.Cos(angle) * .14f, .54f, Mathf.Sin(angle) * .14f),
                    new Vector3(.16f, .41f, .15f), "leaf");
                leaf.localRotation = Quaternion.Euler(15 * Mathf.Sin(angle), i * 72, 20 * Mathf.Cos(angle));
            }
        }

        // Original project mesh/TextMesh recipes follow; no imported art or gameplay colliders.
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
            var mesh = new Mesh { name = "SnakeMesh_" + name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
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

    public sealed class SnakeCellPick : MonoBehaviour
    {
        public int X, Y;
    }
}
