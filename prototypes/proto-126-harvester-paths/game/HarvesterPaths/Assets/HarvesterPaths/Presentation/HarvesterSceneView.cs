using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvesterPaths
{
    /// <summary>Original miniature farm. Core owns tiles, shared cargo, coverage and every cost.</summary>
    public sealed class HarvesterSceneView : MonoBehaviour
    {
        public const float CellSize = .72f;
        private const float FloorTop = .045f;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly TileVisual[] tiles = new TileVisual[Rules.Width * Rules.Height];
        private Camera camera;
        private Font font;
        private Transform childRoot, vehicleRoot, vehicleFacing, vehicleModel, cargoRoot, previewRoot, fuelFill;
        private TextMesh cargoCount, moduleLabel, depotStock;
        private ModuleKind shownModule;
        private bool vehicleBuilt;
        private string lastCargoSignature, lastPreviewSignature;

        private sealed class TileVisual
        {
            public Transform Root, Content;
            public Renderer Floor;
            public string Signature;
        }

        public Transform SceneRoot { get { return childRoot; } }

        public void Build(Camera gameCamera)
        {
            if (gameCamera == null) throw new ArgumentNullException(nameof(gameCamera));
            if (childRoot != null) return;
            camera = gameCamera; font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            childRoot = new GameObject("HarvesterFarm_OriginalArt").transform;
            BuildPalette(); ConfigureCameraAndLight(); BuildField(); BuildDepot(); BuildModuleSamples();
            vehicleRoot = Empty("ActualVehicleCell", childRoot, Vector3.zero);
            vehicleFacing = Empty("ActualVehicleFacing", vehicleRoot, Vector3.zero);
            vehicleModel = Empty("ActualSelectedModule", vehicleFacing, Vector3.zero);
            moduleLabel = Caption("VehicleModuleLabel", "基础机", vehicleRoot, new Vector3(0, 1.04f, 0), .18f, .74f).GetComponentInChildren<TextMesh>();
            previewRoot = Empty("CoreCoverageAndTarget", childRoot, Vector3.zero);
        }

        public Vector3 CellPosition(int x, int y)
        {
            return new Vector3((x - (Rules.Width - 1) * .5f) * CellSize, FloorTop,
                (y - (Rules.Height - 1) * .5f) * CellSize);
        }

        public Vector3 CellScreenPoint(int x, int y)
        {
            return camera == null ? Vector3.zero : camera.WorldToScreenPoint(CellPosition(x, y) + Vector3.up * .14f);
        }

        public bool TryPick(Vector2 screenPoint, out int x, out int y)
        {
            x = y = -1;
            if (camera == null || !camera.pixelRect.Contains(screenPoint)) return false;
            RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPoint), 50f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                HarvesterCellPick pick = hit.collider.GetComponent<HarvesterCellPick>();
                if (pick == null) continue;
                x = pick.X; y = pick.Y; return true;
            }
            return false;
        }

        public void Render(Session session, int selectedX, int selectedY)
        {
            if (session == null || childRoot == null) return;
            RoundSnapshot state = session.State;
            foreach (TileState tile in state.Tiles)
            {
                TileVisual view = tiles[tile.Id];
                string signature = (int)tile.Kind + ":" + tile.Harvested + ":" + tile.Paved;
                if (view.Signature == signature) continue;
                ClearChildren(view.Content);
                view.Floor.sharedMaterial = materials[tile.Kind == TileKind.Mud ? "mud" : tile.Harvested || tile.Paved ? "harvested" :
                    (tile.X + tile.Y) % 2 == 0 ? "groundA" : "groundB"];
                if (tile.Kind == TileKind.Crop) BuildCrop(view.Content);
                else if (tile.Kind == TileKind.Mud) BuildMud(view.Content);
                else if (tile.Paved) BuildStrawRoad(view.Content);
                else if (tile.Harvested) BuildStubble(view.Content);
                view.Signature = signature;
            }
            VehicleState vehicle = state.Vehicle;
            if (!vehicleBuilt || shownModule != vehicle.Module)
            {
                ClearChildren(vehicleModel); BuildVehicle(vehicle.Module); shownModule = vehicle.Module; vehicleBuilt = true;
                lastCargoSignature = null;
            }
            vehicleRoot.position = CellPosition(vehicle.X, vehicle.Y);
            vehicleFacing.localRotation = Quaternion.Euler(0, (int)vehicle.Facing * 90, 0);
            moduleLabel.text = Rules.ModuleName(vehicle.Module); moduleLabel.transform.parent.rotation = camera.transform.rotation;
            float fuel = Mathf.Clamp01(vehicle.Fuel / (float)Rules.MaxFuel);
            fuelFill.localScale = new Vector3(1, Mathf.Max(.001f, fuel), 1);
            fuelFill.localPosition = new Vector3(.25f, .315f + .115f * fuel, .10f);
            RenderCargo(vehicle);
            depotStock.text = "库存秸 " + state.StoredStraw;
            depotStock.transform.parent.rotation = camera.transform.rotation;
            RenderPreview(session.PreviewHarvest(), selectedX, selectedY);
        }

        private void BuildField()
        {
            RoundedBox("FarmDioramaBase", childRoot, new Vector3(0, -.14f, 0), new Vector3(11.10f, .28f, 9.35f), .22f, "wood");
            RoundedBox("FieldUnderlay", childRoot, new Vector3(0, -.055f, 0), new Vector3(7.62f, .11f, 7.62f), .18f, "soil");
            for (int y = 0; y < Rules.Height; y++)
                for (int x = 0; x < Rules.Width; x++)
                {
                    int id = Rules.CellId(x, y); var view = new TileVisual();
                    view.Root = Empty("FieldCell_" + (x + 1) + "_" + (y + 1), childRoot, CellPosition(x, y));
                    view.Floor = RoundedBox("ActualTerrain", view.Root, new Vector3(0, -.0225f, 0), new Vector3(.696f, .045f, .696f), .043f, "groundA").GetComponent<Renderer>();
                    view.Content = Empty("ActualTileContents", view.Root, Vector3.zero); tiles[id] = view;
                    var proxy = new GameObject("PickFieldCell_" + id); proxy.transform.SetParent(childRoot, false);
                    proxy.transform.position = CellPosition(x, y) + Vector3.up * .12f;
                    proxy.AddComponent<BoxCollider>().size = new Vector3(.68f, .28f, .68f);
                    HarvesterCellPick pick = proxy.AddComponent<HarvesterCellPick>(); pick.X = x; pick.Y = y;
                }
            for (int x = 0; x < Rules.Width; x++)
                Text("Column_" + x, (x + 1).ToString(), childRoot, new Vector3(CellPosition(x, 0).x, .10f, -3.97f), .22f, "cream").rotation = camera.transform.rotation;
            for (int y = 0; y < Rules.Height; y++)
                Text("Row_" + y, (y + 1).ToString(), childRoot, new Vector3(4.07f, .10f, CellPosition(0, y).z), .22f, "cream").rotation = camera.transform.rotation;
            Text("FieldLegend", "蓝框：切幅  /  金角：本次作物  /  白圈：铺路点", childRoot, new Vector3(0, .13f, -4.44f), .18f, "cream").rotation = camera.transform.rotation;
        }

        private void BuildCrop(Transform root)
        {
            for (int i = 0; i < 5; i++)
            {
                float x = -.19f + i % 3 * .19f, z = -.10f + i / 3 * .22f;
                float height = .37f + (i % 2) * .045f;
                Cylinder("WheatStem", root, new Vector3(x, height * .5f, z), .012f, height, "straw", Vector3.up);
                Sphere("WheatHead", root, new Vector3(x, height + .045f, z), new Vector3(.10f, .17f, .075f), "grain");
                Transform leaf = Sphere("WheatLeaf", root, new Vector3(x + .042f, .20f, z), new Vector3(.11f, .035f, .19f), "leaf");
                leaf.localRotation = Quaternion.Euler(0, i * 47, 32);
            }
        }

        private void BuildMud(Transform root)
        {
            Sphere("MudWetPatch", root, new Vector3(-.08f, .016f, .035f), new Vector3(.49f, .032f, .42f), "mudDark");
            Sphere("MudSmallPatch", root, new Vector3(.16f, .013f, -.16f), new Vector3(.29f, .026f, .27f), "mudDark");
            foreach (float x in new[] { -.17f, .17f })
                RoundedBox("MudRut", root, new Vector3(x, .035f, .03f), new Vector3(.043f, .015f, .53f), .005f, "mudLight");
            Caption("MudLabel", "泥", root, new Vector3(0, .082f, -.27f), .16f, .22f).rotation = camera.transform.rotation;
        }

        private void BuildStubble(Transform root)
        {
            for (int i = 0; i < 4; i++)
                RoundedBox("HarvestedSoilFurrow", root, new Vector3(-.23f + i * .155f, .016f, 0), new Vector3(.055f, .018f, .57f), .007f, "soil");
            for (int i = 0; i < 3; i++) Cylinder("ActualCutStubble", root, new Vector3(-.18f + i * .18f, .04f, -.12f), .024f, .065f, "straw", Vector3.up);
        }

        private void BuildStrawRoad(Transform root)
        {
            for (int i = 0; i < 7; i++)
                RoundedBox("CommittedStrawPaving", root, new Vector3(0, .027f, -.27f + i * .09f), new Vector3(.62f, .040f, .055f), .012f, i % 2 == 0 ? "straw" : "grain");
            Caption("PavedLabel", "路", root, new Vector3(0, .10f, -.26f), .16f, .22f).rotation = camera.transform.rotation;
        }

        private void BuildVehicle(ModuleKind module)
        {
            RoundedBox("BlueHarvesterChassis", vehicleModel, new Vector3(0, .255f, 0), new Vector3(.44f, .26f, .52f), .066f, "vehicle");
            RoundedBox("CreamCab", vehicleModel, new Vector3(0, .485f, .12f), new Vector3(.31f, .27f, .24f), .050f, "cream");
            RoundedBox("BlueWindshield", vehicleModel, new Vector3(0, .51f, .249f), new Vector3(.24f, .14f, .020f), .012f, "glass");
            Arrow("FacingArrow", vehicleModel, new Vector3(0, .645f, .11f), "coverage");
            BuildRunningGear(vehicleModel, module == ModuleKind.Roller);
            BuildHeader(vehicleModel, module == ModuleKind.WideHead ? 1.86f : .55f, module == ModuleKind.WideHead ? 11 : 5);
            float boxWidth = module == ModuleKind.CargoBox ? .48f : .38f;
            float boxHeight = module == ModuleKind.CargoBox ? .25f : .14f;
            Transform box = Empty("OneSharedCargoBox", vehicleModel, new Vector3(0, .405f, -.155f));
            RoundedBox("CargoBoxFloor", box, Vector3.zero, new Vector3(boxWidth, .035f, .28f), .022f, "cargoDark");
            foreach (int side in new[] { -1, 1 })
            {
                RoundedBox("CargoSideRim", box, new Vector3(side * boxWidth * .5f, boxHeight * .5f, 0), new Vector3(.032f, boxHeight, .31f), .011f, "cream");
                RoundedBox("CargoEndRim", box, new Vector3(0, boxHeight * .5f, side * .145f), new Vector3(boxWidth + .02f, boxHeight, .028f), .010f, "cream");
            }
            cargoRoot = Empty("ActualGrainAndStrawUnits", box, new Vector3(0, .05f, 0));
            cargoCount = Caption("SharedCargoCount", "箱0/6", vehicleModel, new Vector3(0, .25f, -.80f), .17f, .68f).GetComponentInChildren<TextMesh>();
            RoundedBox("FuelGaugeBack", vehicleModel, new Vector3(.25f, .43f, .10f), new Vector3(.04f, .26f, .06f), .008f, "cream");
            fuelFill = RoundedBox("ActualFuelFill", vehicleModel, new Vector3(.25f, .43f, .10f), new Vector3(.045f, .23f, .035f), .007f, "fuel");
        }

        private void BuildRunningGear(Transform root, bool roller)
        {
            foreach (int side in new[] { -1, 1 })
            {
                if (roller)
                {
                    RoundedBox("RollerTrackCase", root, new Vector3(side * .27f, .16f, 0), new Vector3(.15f, .27f, .61f), .055f, "rubber");
                    for (int i = 0; i < 6; i++)
                        RoundedBox("RollerTrackBar", root, new Vector3(side * .27f, .299f, -.24f + i * .096f), new Vector3(.16f, .017f, .035f), .005f, "track");
                }
                else foreach (float z in new[] { -.19f, .19f })
                {
                    Cylinder("FieldTire", root, new Vector3(side * .26f, .15f, z), .13f, .095f, "rubber", Vector3.right);
                    Cylinder("TireHub", root, new Vector3(side * .314f, .15f, z), .063f, .015f, "cream", Vector3.right);
                }
            }
        }

        private void BuildHeader(Transform root, float width, int teeth)
        {
            RoundedBox("RealHeaderWidth", root, new Vector3(0, .22f, .49f), new Vector3(width, .16f, .20f), .050f, "header");
            Cylinder("HeaderReel", root, new Vector3(0, .30f, .50f), .058f, width - .04f, "cream", Vector3.right);
            for (int i = 0; i < teeth; i++)
                RoundedBox("HeaderTooth", root, new Vector3(-width * .43f + i * width * .86f / (teeth - 1), .16f, .626f), new Vector3(.033f, .029f, .11f), .007f, "metal");
        }

        private void RenderCargo(VehicleState vehicle)
        {
            string signature = vehicle.Grain + ":" + vehicle.Straw + ":" + vehicle.Capacity;
            if (lastCargoSignature != signature)
            {
                ClearChildren(cargoRoot);
                int columns = Mathf.CeilToInt(vehicle.Capacity / 2f);
                float width = vehicle.Module == ModuleKind.CargoBox ? .38f : .29f;
                for (int i = 0; i < vehicle.CargoUsed; i++)
                {
                    Vector3 p = new Vector3((i % columns - (columns - 1) * .5f) * width / columns, .025f, -.055f + i / columns * .11f);
                    if (i < vehicle.Grain) Sphere("ActualGrainUnit", cargoRoot, p, new Vector3(.062f, .07f, .062f), "grain");
                    else for (int bundle = 0; bundle < 3; bundle++)
                        Cylinder("ActualStrawUnit", cargoRoot, p + new Vector3(0, bundle * .013f, 0), .010f, .076f, "straw", Vector3.right);
                }
                lastCargoSignature = signature;
            }
            cargoCount.text = "箱" + vehicle.CargoUsed + "/" + vehicle.Capacity;
            cargoCount.transform.parent.position = vehicleRoot.position + new Vector3(0, .25f, -.80f);
            cargoCount.transform.parent.rotation = camera.transform.rotation;
        }

        private void RenderPreview(HarvestPreview harvest, int selectedX, int selectedY)
        {
            var key = new StringBuilder(); key.Append(selectedX).Append(',').Append(selectedY).Append(':').Append(harvest.Legal);
            foreach (GridPoint p in harvest.CoverageCells) key.Append('/').Append(p.X).Append(',').Append(p.Y);
            key.Append('|'); foreach (GridPoint p in harvest.TargetCells) key.Append('/').Append(p.X).Append(',').Append(p.Y);
            if (lastPreviewSignature == key.ToString()) return;
            ClearChildren(previewRoot);
            foreach (GridPoint p in harvest.CoverageCells)
                if (Rules.InBounds(p.X, p.Y)) Frame("CoreFullCutWidth", previewRoot, CellPosition(p.X, p.Y) + Vector3.up * .020f, .65f, .023f, false, "coverage");
            foreach (GridPoint p in harvest.TargetCells)
                if (Rules.InBounds(p.X, p.Y)) Frame("CoreActualCrop", previewRoot, CellPosition(p.X, p.Y) + Vector3.up * .045f, .68f, .035f, true, "target");
            if (Rules.InBounds(selectedX, selectedY))
            {
                Transform selected = OvalBand("SelectedPavingCell", previewRoot, .323f, .323f, .024f, .014f, "cream");
                selected.position = CellPosition(selectedX, selectedY) + Vector3.up * .055f;
            }
            lastPreviewSignature = key.ToString();
        }

        private void BuildDepot()
        {
            Vector3 p = CellPosition(Rules.DepotX, Rules.DepotY);
            RoundedBox("ActualDepotFunctionalPad", childRoot, p + Vector3.up * .008f, new Vector3(.69f, .018f, .69f), .070f, "depot");
            Transform ring = OvalBand("DepotCircle", childRoot, .32f, .32f, .026f, .015f, "cream"); ring.position = p + Vector3.up * .026f;
            Caption("DepotCellLabel", "仓", childRoot, p + new Vector3(-.28f, .085f, -.28f), .16f, .22f).rotation = camera.transform.rotation;
            Transform barn = Empty("DepotBarnOutsideGrid", childRoot, new Vector3(-4.75f, .01f, p.z));
            RoundedBox("BarnCreamWalls", barn, new Vector3(0, .46f, 0), new Vector3(1.02f, .90f, 1.03f), .065f, "cream");
            RoundedBox("BarnBlueDoor", barn, new Vector3(0, .35f, -.53f), new Vector3(.47f, .63f, .030f), .022f, "depot");
            foreach (int side in new[] { -1, 1 })
            {
                Transform roof = RoundedBox("BarnSlopedRoof", barn, new Vector3(side * .27f, 1.01f, 0), new Vector3(.67f, .055f, 1.23f), .018f, "roof");
                roof.localRotation = Quaternion.Euler(0, 0, side * -26);
            }
            Caption("DepotBarnTitle", "粮仓", childRoot, new Vector3(-4.75f, 1.48f, p.z), .23f, .66f).rotation = camera.transform.rotation;
            depotStock = Caption("ActualDepotStraw", "库存秸 0", childRoot, new Vector3(-4.75f, 1.16f, p.z - .58f), .16f, .95f).GetComponentInChildren<TextMesh>();
            depotStock.transform.parent.rotation = camera.transform.rotation;
        }

        private void BuildModuleSamples()
        {
            var modules = new[] { ModuleKind.Roller, ModuleKind.WideHead, ModuleKind.CargoBox };
            for (int i = 0; i < modules.Length; i++)
            {
                Transform sample = Empty("OffGridModuleSample_" + modules[i], childRoot, new Vector3(-2.50f + i * 2.50f, .05f, 4.15f));
                RoundedBox("ModuleDisplayStand", sample, new Vector3(0, .045f, 0), new Vector3(1.20f, .09f, .54f), .064f, "depot");
                Transform model = Empty("ExplanatoryModuleShape", sample, new Vector3(0, .08f, 0)); model.localScale = Vector3.one * .42f;
                if (modules[i] == ModuleKind.Roller) BuildRunningGear(model, true);
                else if (modules[i] == ModuleKind.WideHead) { BuildHeader(model, 1.86f, 11); model.localPosition = new Vector3(0, .08f, -.22f); }
                else
                {
                    RoundedBox("SampleLargeBox", model, new Vector3(0, .16f, 0), new Vector3(.70f, .30f, .55f), .068f, "cream");
                    RoundedBox("SampleBoxOpening", model, new Vector3(0, .317f, 0), new Vector3(.56f, .020f, .41f), .045f, "cargoDark");
                }
                Caption("ModuleSampleLabel", Rules.ModuleName(modules[i]), childRoot,
                    new Vector3(-2.50f + i * 2.50f, .58f, 4.15f), .19f, .88f).rotation = camera.transform.rotation;
            }
        }

        private Transform Caption(string name, string value, Transform parent, Vector3 p, float glyph, float width)
        {
            Transform root = Empty(name, parent, p);
            RoundedBox("CreamCaptionBack", root, Vector3.zero, new Vector3(width, glyph * 1.30f, .024f), .009f, "cream");
            Text("DarkCaption", value, root, new Vector3(0, 0, -.018f), glyph, "ink"); return root;
        }

        private Transform Frame(string name, Transform parent, Vector3 p, float size, float width, bool corners, string color)
        {
            Transform root = Empty(name, parent, p); float half = size * .5f, shortLength = size * .25f;
            foreach (int side in new[] { -1, 1 })
            {
                if (!corners)
                {
                    RoundedBox("FrameHorizontal", root, new Vector3(0, 0, side * half), new Vector3(size + width, .014f, width), .004f, color);
                    RoundedBox("FrameVertical", root, new Vector3(side * half, 0, 0), new Vector3(width, .014f, size + width), .004f, color);
                }
                else foreach (int other in new[] { -1, 1 })
                {
                    RoundedBox("CornerHorizontal", root, new Vector3(other * (half - shortLength * .5f), 0, side * half), new Vector3(shortLength, .014f, width), .004f, color);
                    RoundedBox("CornerVertical", root, new Vector3(side * half, 0, other * (half - shortLength * .5f)), new Vector3(width, .014f, shortLength), .004f, color);
                }
            }
            return root;
        }

        private void BuildPalette()
        {
            AddMaterial("wood", new Color(.47f, .36f, .24f)); AddMaterial("soil", new Color(.48f, .36f, .23f));
            AddMaterial("groundA", new Color(.61f, .72f, .46f)); AddMaterial("groundB", new Color(.55f, .67f, .40f));
            AddMaterial("harvested", new Color(.73f, .61f, .39f)); AddMaterial("mud", new Color(.47f, .35f, .23f));
            AddMaterial("mudDark", new Color(.34f, .26f, .19f)); AddMaterial("mudLight", new Color(.64f, .47f, .28f));
            AddMaterial("grain", new Color(.96f, .72f, .25f)); AddMaterial("straw", new Color(.76f, .57f, .31f));
            AddMaterial("leaf", new Color(.44f, .63f, .26f)); AddMaterial("cream", new Color(.98f, .96f, .83f));
            AddMaterial("ink", new Color(.13f, .22f, .25f)); AddMaterial("vehicle", new Color(.22f, .58f, .65f));
            AddMaterial("glass", new Color(.22f, .40f, .48f)); AddMaterial("header", new Color(.25f, .49f, .50f));
            AddMaterial("rubber", new Color(.19f, .22f, .22f)); AddMaterial("track", new Color(.45f, .50f, .44f));
            AddMaterial("metal", new Color(.75f, .78f, .69f), .30f); AddMaterial("cargoDark", new Color(.29f, .36f, .32f));
            AddMaterial("depot", new Color(.24f, .43f, .56f)); AddMaterial("roof", new Color(.62f, .35f, .25f));
            AddMaterial("coverage", new Color(.16f, .66f, .83f), 0, true); AddMaterial("target", new Color(1, .77f, .26f), 0, true);
            AddMaterial("fuel", new Color(.40f, .73f, .34f));
        }

        private void AddMaterial(string id, Color color, float metal = 0, bool glow = false)
        {
            var material = new Material(Shader.Find("Standard")) { name = "HarvesterOriginal_" + id, color = color };
            material.SetFloat("_Metallic", metal); material.SetFloat("_Glossiness", .24f);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .10f); }
            materials.Add(id, material);
        }

        private void ConfigureCameraAndLight()
        {
            float aspect = camera.pixelRect.height > 1 ? camera.pixelRect.width / camera.pixelRect.height : camera.aspect;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(3.95f, 5.65f / Mathf.Max(.6f, aspect));
            camera.transform.position = new Vector3(.30f, 11.0f, -11.4f); camera.transform.LookAt(new Vector3(0, .25f, .35f));
            camera.nearClipPlane = .1f; camera.farClipPlane = 50f; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.20f, .28f, .28f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.70f, .73f, .68f);
            Transform lightRoot = Empty("FieldMorningKey", childRoot, Vector3.zero);
            Light light = lightRoot.gameObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.color = new Color(1, .96f, .82f); light.intensity = .85f; light.shadows = LightShadows.Soft;
            lightRoot.rotation = Quaternion.Euler(51, -25, 0);
        }

        public void Dispose()
        {
            if (childRoot != null) UnityEngine.Object.Destroy(childRoot.gameObject);
            foreach (Material material in materials.Values) UnityEngine.Object.Destroy(material);
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            materials.Clear(); meshes.Clear(); Array.Clear(tiles, 0, tiles.Length); childRoot = null;
            vehicleBuilt = false; lastCargoSignature = lastPreviewSignature = null;
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
            var mesh = new Mesh { name = "HarvesterMesh_" + name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
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

    public sealed class HarvesterCellPick : MonoBehaviour { public int X, Y; }
}
