using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagnetScavenger
{
    /// <summary>Original salvage-box presentation. Core alone owns cells, connections and ray queries.</summary>
    public sealed class MagnetSceneView : MonoBehaviour
    {
        public const float CellSize = .66f;
        private const float FloorTop = .045f;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Dictionary<int, ItemVisual> itemViews = new Dictionary<int, ItemVisual>();
        private Camera camera;
        private Font font;
        private Transform childRoot, itemsRoot, previewRoot, storageRoot, magnetRoot, magnetFacing, endMarker;
        private TextMesh rootLabel, endLabel, storageLabel;
        private string lastPreviewSignature, lastStorageSignature;

        private sealed class CellPiece
        {
            public Transform Root, ChainFrame, PendingFrame, SelectedRing;
        }

        private sealed class ItemVisual
        {
            public Transform Root, Content, Entry, Exit, Tag;
            public TextMesh Label;
            public CellPiece[] Pieces;
            public readonly List<Transform> Links = new List<Transform>();
            public string PoseSignature;
        }

        public Transform SceneRoot { get { return childRoot; } }

        public void Build(Camera gameCamera)
        {
            if (gameCamera == null) throw new ArgumentNullException(nameof(gameCamera));
            if (childRoot != null) return;
            camera = gameCamera; font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            childRoot = new GameObject("MagnetSalvage_OriginalArt").transform;
            BuildPalette(); ConfigureCameraAndLight(); BuildTableAndGrid(); BuildDepotAndStorage();
            itemsRoot = Empty("ActualUniqueItems", childRoot, Vector3.zero);
            previewRoot = Empty("CoreAttractionPreview", childRoot, Vector3.zero);
            BuildMagnet();
        }

        public Vector3 CellPosition(int x, int y)
        {
            return new Vector3((x - (Rules.Width - 1) * .5f) * CellSize, FloorTop,
                (y - (Rules.Height - 1) * .5f) * CellSize);
        }

        public Vector3 CellScreenPoint(int x, int y)
        {
            return camera == null ? Vector3.zero : camera.WorldToScreenPoint(CellPosition(x, y) + Vector3.up * .13f);
        }

        public bool TryPick(Vector2 screenPoint, out int x, out int y)
        {
            x = y = -1;
            if (camera == null || !camera.pixelRect.Contains(screenPoint)) return false;
            RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPoint), 50f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                MagnetCellPick pick = hit.collider.GetComponent<MagnetCellPick>();
                if (pick == null) continue;
                x = pick.X; y = pick.Y; return true;
            }
            return false;
        }

        public void Render(Session session, int selectedItemId)
        {
            if (session == null || childRoot == null) return;
            RoundSnapshot state = session.State;
            magnetRoot.position = CellPosition(state.RootX, state.RootY);
            magnetFacing.localRotation = Quaternion.Euler(0, (int)state.Facing * 90, 0);
            rootLabel.text = state.EndX == state.RootX && state.EndY == state.RootY ? "磁·末" : "磁头";
            rootLabel.transform.rotation = camera.transform.rotation;
            endMarker.position = CellPosition(state.EndX, state.EndY) + Vector3.up * .48f;
            endMarker.rotation = Quaternion.Euler(0, (int)state.EndDirection * 90, 0);
            bool endAway = state.EndX != state.RootX || state.EndY != state.RootY;
            endMarker.gameObject.SetActive(endAway); endLabel.gameObject.SetActive(endAway);
            endLabel.transform.position = CellPosition(state.EndX, state.EndY) + new Vector3(0, .67f, 0);
            endLabel.transform.rotation = camera.transform.rotation;

            var present = new HashSet<int>();
            foreach (ItemState item in state.Items)
            {
                if (item.Location == ItemLocation.Storage || item.Location == ItemLocation.Sold) continue;
                ItemVisual view;
                if (!itemViews.TryGetValue(item.Id, out view)) { view = CreateItemVisual(item.Id); itemViews.Add(item.Id, view); }
                present.Add(item.Id); view.Root.gameObject.SetActive(true);
                string pose = ItemSignature(item);
                if (view.PoseSignature != pose) { BuildItemPose(view, item); view.PoseSignature = pose; }
                view.Tag.rotation = camera.transform.rotation;
                foreach (CellPiece piece in view.Pieces)
                {
                    piece.ChainFrame.gameObject.SetActive(item.Location == ItemLocation.Chain);
                    piece.PendingFrame.gameObject.SetActive(item.Location == ItemLocation.Pending);
                    piece.SelectedRing.gameObject.SetActive(item.Id == selectedItemId);
                }
            }
            foreach (var entry in itemViews) entry.Value.Root.gameObject.SetActive(present.Contains(entry.Key));
            RenderStorage(state);
            RenderPreview(session.PreviewAttract());
        }

        private static string ItemSignature(ItemState item)
        {
            var key = new StringBuilder(); key.Append((int)item.Kind).Append(':').Append((int)item.Location).Append(':').Append((int)item.Facing);
            key.Append('/').Append(item.EntryX).Append(',').Append(item.EntryY).Append('/').Append(item.ExitX).Append(',').Append(item.ExitY).Append(',').Append((int)item.ExitDirection);
            foreach (GridPoint p in item.Cells) key.Append(';').Append(p.X).Append(',').Append(p.Y);
            return key.ToString();
        }

        private ItemVisual CreateItemVisual(int id)
        {
            var view = new ItemVisual();
            view.Root = Empty("ActualItem_" + id, itemsRoot, Vector3.zero);
            view.Content = Empty("ActualOccupiedCellGeometry", view.Root, Vector3.zero);
            view.Tag = Empty("ReadableItemTag", view.Root, Vector3.zero);
            RoundedBox("LightTagBack", view.Tag, Vector3.zero, new Vector3(.74f, .24f, .024f), .009f, "cream");
            view.Label = Text("StableItemId", "", view.Tag, new Vector3(0, 0, -.018f), .19f, "ink").GetComponent<TextMesh>();
            return view;
        }

        private void BuildItemPose(ItemVisual view, ItemState item)
        {
            ClearChildren(view.Content); view.Links.Clear(); view.Pieces = new CellPiece[item.Cells.Length];
            for (int i = 0; i < item.Cells.Length; i++)
            {
                GridPoint cell = item.Cells[i];
                var piece = new CellPiece();
                piece.Root = Empty("ActualCell_" + i, view.Content, CellPosition(cell.X, cell.Y));
                Transform shape = Empty("IronKindShape", piece.Root, Vector3.zero);
                shape.localRotation = Quaternion.Euler(0, (int)item.Facing * 90, 0);
                BuildIronCell(shape, item.Kind);
                piece.ChainFrame = Frame("ChainOutline", piece.Root, Vector3.up * .014f, .60f, .023f, false, "chain");
                piece.PendingFrame = Frame("PendingCorners", piece.Root, Vector3.up * .018f, .61f, .033f, true, "pending");
                piece.SelectedRing = OvalBand("SelectedItemRing", piece.Root, .308f, .308f, .022f, .014f, "selected");
                piece.SelectedRing.localPosition = Vector3.up * .027f; view.Pieces[i] = piece;
            }
            for (int i = 0; i < item.Cells.Length; i++)
                for (int j = i + 1; j < item.Cells.Length; j++)
                {
                    GridPoint a = item.Cells[i], b = item.Cells[j];
                    if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) != 1) continue;
                    Vector3 from = CellPosition(a.X, a.Y), to = CellPosition(b.X, b.Y), delta = to - from;
                    string color = IronColor(item.Kind);
                    view.Links.Add(Cylinder("WithinActualItem", view.Content, (from + to) * .5f + Vector3.up * .090f,
                        item.Kind == ItemKind.HeavyBlock ? .14f : .059f, .25f, color, delta.normalized));
                }
            view.Tag.position = CellPosition(item.EntryX, item.EntryY) + new Vector3(0, .47f, -.47f);
            view.Label.text = TypeChar(item.Kind) + "#" + item.Id + (item.Location == ItemLocation.Pending ? " 待" : "");
            view.Entry = OvalBand("ActualEntry", view.Content, .090f, .090f, .035f, .022f, "entry");
            view.Entry.position = CellPosition(item.EntryX, item.EntryY) + new Vector3(-.18f, .38f, .18f);
            view.Exit = Empty("ActualExitDirection", view.Content, CellPosition(item.ExitX, item.ExitY) + Vector3.up * .385f);
            view.Exit.rotation = Quaternion.Euler(0, (int)item.ExitDirection * 90, 0);
            Arrow("ExitChevron", view.Exit, new Vector3(0, 0, .18f), "exit");
        }

        private void BuildIronCell(Transform root, ItemKind kind)
        {
            string color = IronColor(kind);
            if (kind == ItemKind.HeavyBlock)
            {
                RoundedBox("HeavyIronBlock", root, new Vector3(0, .17f, 0), new Vector3(.51f, .31f, .51f), .060f, color);
                RoundedBox("HeavyBand", root, new Vector3(0, .333f, 0), new Vector3(.08f, .015f, .49f), .005f, "metalLight");
            }
            else if (kind == ItemKind.ShortPlate)
            {
                RoundedBox("ShortIronPlate", root, new Vector3(0, .08f, 0), new Vector3(.47f, .13f, .46f), .053f, color);
                foreach (float x in new[] { -.13f, .13f }) Cylinder("PlateRivet", root, new Vector3(x, .154f, 0), .027f, .021f, "metalLight", Vector3.up);
            }
            else
            {
                RoundedBox(kind == ItemKind.Elbow ? "ElbowPipeCell" : "LongRailCell", root, new Vector3(0, .10f, 0),
                    new Vector3(.28f, .16f, .54f), .059f, color);
                RoundedBox("IronTopSeam", root, new Vector3(0, .189f, 0), new Vector3(.07f, .018f, .45f), .005f, "metalLight");
            }
        }

        private void RenderStorage(RoundSnapshot state)
        {
            var key = new StringBuilder();
            foreach (int id in state.StorageIds) key.Append(id).Append(',');
            if (lastStorageSignature != key.ToString())
            {
                ClearChildren(storageRoot);
                for (int i = 0; i < state.StorageIds.Length; i++)
                    foreach (ItemState item in state.Items)
                        if (item.Id == state.StorageIds[i])
                        {
                            Transform icon = Empty("StorageItem_" + item.Id, storageRoot, new Vector3(0, 0, -.78f + i * .43f));
                            Transform shape = Empty("OffGridItemIcon", icon, Vector3.zero); shape.localScale = Vector3.one * .52f;
                            BuildIronCell(shape, item.Kind);
                            Text("StoredStableId", TypeChar(item.Kind) + "#" + item.Id, icon, new Vector3(0, .28f, -.10f), .17f, "cream").rotation = camera.transform.rotation;
                            break;
                        }
                lastStorageSignature = key.ToString();
            }
            storageLabel.text = "暂存 " + state.StorageIds.Length;
            storageLabel.transform.rotation = camera.transform.rotation;
        }

        private void RenderPreview(AttractPreview preview)
        {
            var key = new StringBuilder(); key.Append(preview.ItemId).Append(':').Append(preview.Legal).Append('/').Append(preview.BlockX).Append(',').Append(preview.BlockY);
            foreach (GridPoint p in preview.RayCells) key.Append(';').Append(p.X).Append(',').Append(p.Y);
            key.Append('|'); foreach (GridPoint p in preview.CandidateCells) key.Append(';').Append(p.X).Append(',').Append(p.Y);
            if (lastPreviewSignature == key.ToString()) return;
            ClearChildren(previewRoot);
            for (int i = 0; i < preview.RayCells.Length; i++)
            {
                GridPoint cell = preview.RayCells[i];
                if (!Rules.InBounds(cell.X, cell.Y)) continue;
                Vector3 p = CellPosition(cell.X, cell.Y) + Vector3.up * .035f;
                Cylinder("CoreRayDot", previewRoot, p, .037f, .014f, "ray", Vector3.up);
                if (i > 0)
                {
                    GridPoint prev = preview.RayCells[i - 1];
                    if (Rules.InBounds(prev.X, prev.Y)) BuildLine("CoreRaySegment", previewRoot,
                        CellPosition(prev.X, prev.Y) + Vector3.up * .034f, p, .020f, "ray");
                }
            }
            if (preview.ItemId > 0 && Rules.InBounds(preview.FirstX, preview.FirstY))
                Frame("CoreFirstIntersectedCell", previewRoot, CellPosition(preview.FirstX, preview.FirstY) + Vector3.up * .045f, .632f, .036f, false, "ray");
            foreach (GridPoint cell in preview.CandidateCells)
                if (Rules.InBounds(cell.X, cell.Y))
                    Frame("CoreAttachmentCandidate", previewRoot, CellPosition(cell.X, cell.Y) + Vector3.up * .050f, .57f, .016f, true,
                        preview.Legal ? "candidate" : "blocked");
            if (Rules.InBounds(preview.BlockX, preview.BlockY))
            {
                Transform cross = Empty("CoreBlockedCell", previewRoot, CellPosition(preview.BlockX, preview.BlockY) + Vector3.up * .52f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform arm = RoundedBox("BlockedCross", cross, Vector3.zero, new Vector3(.35f, .026f, .048f), .010f, "blocked");
                    arm.localRotation = Quaternion.Euler(0, side * 45, 0);
                }
            }
            lastPreviewSignature = key.ToString();
        }

        private void BuildMagnet()
        {
            magnetRoot = Empty("CoreRootMagnet", childRoot, Vector3.zero);
            magnetFacing = Empty("RootFacing", magnetRoot, Vector3.zero);
            RoundedBox("MagnetBridge", magnetFacing, new Vector3(0, .17f, -.16f), new Vector3(.47f, .25f, .16f), .050f, "magnet");
            for (int side = -1; side <= 1; side += 2)
            {
                RoundedBox("MagnetArm", magnetFacing, new Vector3(side * .16f, .17f, .025f), new Vector3(.15f, .25f, .38f), .049f, "magnet");
                RoundedBox("MagnetPole", magnetFacing, new Vector3(side * .16f, .17f, .224f), new Vector3(.15f, .25f, .07f), .017f, side < 0 ? "poleRed" : "cream");
            }
            rootLabel = Text("RootMagnetLabel", "磁·末", magnetRoot, new Vector3(0, .68f, 0), .20f, "ink").GetComponent<TextMesh>();
            endMarker = Empty("OnlyCommittedEnd", childRoot, Vector3.zero);
            Arrow("OnlyEndArrow", endMarker, Vector3.zero, "exit");
            endLabel = Text("OnlyEndLabel", "末端", childRoot, Vector3.zero, .19f, "ink").GetComponent<TextMesh>();
        }

        private void BuildTableAndGrid()
        {
            RoundedBox("SalvageWorkbench", childRoot, new Vector3(0, -.14f, 0), new Vector3(11.5f, .28f, 7.60f), .22f, "desk");
            RoundedBox("BlueSteelCrateBase", childRoot, new Vector3(0, -.055f, 0), new Vector3(8.50f, .11f, 5.95f), .21f, "tray");
            foreach (int side in new[] { -1, 1 })
            {
                RoundedBox("CrateLongRim", childRoot, new Vector3(0, .05f, side * 2.84f), new Vector3(8.43f, .16f, .15f), .052f, "tray");
                RoundedBox("CrateShortRim", childRoot, new Vector3(side * 4.16f, .05f, 0), new Vector3(.15f, .16f, 5.82f), .052f, "tray");
            }
            for (int y = 0; y < Rules.Height; y++)
                for (int x = 0; x < Rules.Width; x++)
                {
                    Vector3 p = CellPosition(x, y);
                    bool depot = Rules.IsDepot(x, y);
                    RoundedBox("ScrapCell_" + x + "_" + y, childRoot, p - Vector3.up * .0225f, new Vector3(.636f, .045f, .636f), .040f,
                        depot ? "depotFloor" : (x + y) % 2 == 0 ? "tileA" : "tileB");
                    if (depot) BuildDepotEdges(x, y);
                    if (Rules.IsBlocked(x, y))
                    {
                        Transform wall = Empty("CoreObstacle_" + x + "_" + y, childRoot, p);
                        RoundedBox("LowCrateBlock", wall, new Vector3(0, .25f, 0), new Vector3(.52f, .49f, .52f), .050f, "obstacle");
                        RoundedBox("BlockTopBand", wall, new Vector3(0, .502f, 0), new Vector3(.51f, .022f, .08f), .005f, "cream");
                        Text("BlockLabel", "墙", wall, new Vector3(0, .63f, 0), .19f, "cream").rotation = camera.transform.rotation;
                    }
                    var proxy = new GameObject("PickScrapCell_" + Rules.CellId(x, y)); proxy.transform.SetParent(childRoot, false);
                    proxy.transform.position = p + Vector3.up * .12f;
                    proxy.AddComponent<BoxCollider>().size = new Vector3(.62f, .28f, .62f);
                    MagnetCellPick pick = proxy.AddComponent<MagnetCellPick>(); pick.X = x; pick.Y = y;
                }
            for (int x = 0; x < Rules.Width; x++)
                Text("Column_" + x, (x + 1).ToString(), childRoot, new Vector3(CellPosition(x, 0).x, .10f, -3.15f), .21f, "ink").rotation = camera.transform.rotation;
            for (int y = 0; y < Rules.Height; y++)
                Text("Row_" + y, (y + 1).ToString(), childRoot, new Vector3(4.39f, .15f, CellPosition(0, y).z), .21f, "ink").rotation = camera.transform.rotation;
            RoundedBox("WorkshopNameplate", childRoot, new Vector3(0, .35f, 3.14f), new Vector3(4.6f, .44f, .12f), .03f, "tray");
            Text("WorkshopName", "磁 铁 拾 荒 者", childRoot, new Vector3(0, .37f, 3.065f), .30f, "cream");
        }

        private void BuildDepotEdges(int x, int y)
        {
            Vector3 p = CellPosition(x, y) + Vector3.up * .007f;
            for (int i = 0; i < 4; i++)
            {
                int dx = i == 0 ? -1 : i == 1 ? 1 : 0, dy = i == 2 ? -1 : i == 3 ? 1 : 0;
                if (Rules.IsDepot(x + dx, y + dy)) continue;
                Vector3 offset = new Vector3(dx * .312f, 0, dy * .312f);
                RoundedBox("DepotBoundary", childRoot, p + offset,
                    dx == 0 ? new Vector3(.622f, .015f, .028f) : new Vector3(.028f, .015f, .622f), .006f, "entry");
            }
        }

        private void BuildDepotAndStorage()
        {
            Transform depot = Empty("DepotServiceStand", childRoot, new Vector3(-5.02f, .01f, -.92f));
            RoundedBox("DepotStand", depot, new Vector3(0, .20f, 0), new Vector3(.88f, .39f, 1.20f), .085f, "wood");
            RoundedBox("DepotWorktop", depot, new Vector3(0, .418f, 0), new Vector3(.99f, .07f, 1.31f), .069f, "cream");
            Cylinder("DepotSortingBin", depot, new Vector3(0, .53f, 0), .26f, .15f, "tray", Vector3.up);
            Text("DepotTitle", "回收区", childRoot, new Vector3(-5.02f, 1.30f, -.92f), .23f, "ink").rotation = camera.transform.rotation;
            Text("DepotActions", "出售·拆装", childRoot, new Vector3(-5.02f, 1.02f, -.92f), .18f, "ink").rotation = camera.transform.rotation;
            Transform tray = Empty("StorageTray", childRoot, new Vector3(5.06f, .01f, 0));
            RoundedBox("StorageTrayBase", tray, new Vector3(0, .10f, 0), new Vector3(1.02f, .20f, 2.45f), .080f, "wood");
            RoundedBox("StorageTrayInset", tray, new Vector3(0, .211f, 0), new Vector3(.88f, .020f, 2.29f), .045f, "tray");
            storageRoot = Empty("ActualStoredItemIds", tray, new Vector3(0, .24f, 0));
            storageLabel = Text("StorageCount", "暂存 0", childRoot, new Vector3(5.06f, .80f, 1.02f), .21f, "ink").GetComponent<TextMesh>();
            Text("PortLegend", "黄圈：入口  /  蓝箭：出口", childRoot, new Vector3(0, .16f, -3.59f), .19f, "ink").rotation = camera.transform.rotation;
        }

        private void BuildPalette()
        {
            AddMaterial("desk", new Color(.72f, .65f, .51f)); AddMaterial("wood", new Color(.50f, .38f, .26f));
            AddMaterial("cream", new Color(.99f, .96f, .83f)); AddMaterial("tray", new Color(.20f, .36f, .44f));
            AddMaterial("tileA", new Color(.80f, .81f, .71f)); AddMaterial("tileB", new Color(.72f, .75f, .67f));
            AddMaterial("depotFloor", new Color(.91f, .81f, .52f)); AddMaterial("ink", new Color(.12f, .20f, .24f));
            AddMaterial("short", new Color(.52f, .64f, .67f), .34f); AddMaterial("rod", new Color(.35f, .46f, .55f), .38f);
            AddMaterial("elbow", new Color(.34f, .57f, .51f), .33f); AddMaterial("heavy", new Color(.53f, .39f, .30f), .37f);
            AddMaterial("metalLight", new Color(.83f, .85f, .76f), .36f); AddMaterial("magnet", new Color(.98f, .72f, .22f), .14f);
            AddMaterial("poleRed", new Color(.80f, .34f, .28f)); AddMaterial("entry", new Color(1, .73f, .18f), 0, true);
            AddMaterial("exit", new Color(.18f, .72f, .80f), 0, true); AddMaterial("chain", new Color(.14f, .65f, .72f), 0, true);
            AddMaterial("pending", new Color(.95f, .46f, .19f), 0, true); AddMaterial("selected", new Color(.47f, .81f, .24f), 0, true);
            AddMaterial("ray", new Color(1, .98f, .86f), 0, true); AddMaterial("candidate", new Color(.98f, .76f, .28f), 0, true);
            AddMaterial("blocked", new Color(.89f, .22f, .20f), 0, true); AddMaterial("obstacle", new Color(.34f, .41f, .45f));
        }

        private void AddMaterial(string id, Color color, float metal = 0, bool glow = false)
        {
            var material = new Material(Shader.Find("Standard")) { name = "MagnetOriginal_" + id, color = color };
            material.SetFloat("_Metallic", metal); material.SetFloat("_Glossiness", .27f);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .10f); }
            materials.Add(id, material);
        }

        private void ConfigureCameraAndLight()
        {
            float aspect = camera.pixelRect.height > 1 ? camera.pixelRect.width / camera.pixelRect.height : camera.aspect;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(3.70f, 5.95f / Mathf.Max(.6f, aspect));
            camera.transform.position = new Vector3(.30f, 10.2f, -10.2f); camera.transform.LookAt(new Vector3(0, .25f, .10f));
            camera.nearClipPlane = .1f; camera.farClipPlane = 50f; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f, .24f, .28f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.67f, .70f, .72f);
            Transform lightRoot = Empty("WorkbenchKeyLight", childRoot, Vector3.zero);
            Light light = lightRoot.gameObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.color = new Color(1, .95f, .86f); light.intensity = .85f; light.shadows = LightShadows.Soft;
            lightRoot.rotation = Quaternion.Euler(50, -27, 0);
        }

        private Transform Frame(string name, Transform parent, Vector3 p, float size, float width, bool corners, string color)
        {
            Transform root = Empty(name, parent, p);
            float half = size * .5f, shortLength = size * .25f;
            foreach (int side in new[] { -1, 1 })
            {
                if (!corners)
                {
                    RoundedBox("FrameHorizontal", root, new Vector3(0, 0, side * half), new Vector3(size + width, .015f, width), .004f, color);
                    RoundedBox("FrameVertical", root, new Vector3(side * half, 0, 0), new Vector3(width, .015f, size + width), .004f, color);
                }
                else foreach (int other in new[] { -1, 1 })
                {
                    RoundedBox("CornerHorizontal", root, new Vector3(other * (half - shortLength * .5f), 0, side * half), new Vector3(shortLength, .015f, width), .004f, color);
                    RoundedBox("CornerVertical", root, new Vector3(side * half, 0, other * (half - shortLength * .5f)), new Vector3(width, .015f, shortLength), .004f, color);
                }
            }
            return root;
        }

        private void BuildLine(string name, Transform parent, Vector3 a, Vector3 b, float width, string color)
        {
            Vector3 delta = b - a, normal = new Vector3(-delta.z, 0, delta.x).normalized * width * .5f;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Quad(vertices, triangles, a - normal, a + normal, b + normal, b - normal);
            MeshObject(name, parent, vertices, triangles, color);
        }

        private static string TypeChar(ItemKind kind) { return kind == ItemKind.LongRod ? "杆" : kind == ItemKind.Elbow ? "弯" : kind == ItemKind.HeavyBlock ? "重" : "短"; }
        private static string IronColor(ItemKind kind) { return kind == ItemKind.LongRod ? "rod" : kind == ItemKind.Elbow ? "elbow" : kind == ItemKind.HeavyBlock ? "heavy" : "short"; }

        public void Dispose()
        {
            if (childRoot != null) UnityEngine.Object.Destroy(childRoot.gameObject);
            foreach (Material material in materials.Values) UnityEngine.Object.Destroy(material);
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            materials.Clear(); meshes.Clear(); itemViews.Clear(); childRoot = null;
            lastPreviewSignature = lastStorageSignature = null;
        }

        private void OnDestroy() { Dispose(); }

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
            var mesh = new Mesh { name = "MagnetMesh_" + name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
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

    public sealed class MagnetCellPick : MonoBehaviour { public int X, Y; }
}
