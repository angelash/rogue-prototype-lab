using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SushiWorkshop
{
    /// <summary>Original parameterized kitchen art. Read-only view; all pick proxies have fixed slot anchors.</summary>
    public sealed class SushiSceneView : MonoBehaviour
    {
        public const float RingRadiusX = 2.45f;
        public const float RingRadiusZ = 1.55f;
        public const float TrackHeight = 1.02f;
        private const float PlateHeight = 1.095f;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Dictionary<int, PlateVisual> plates = new Dictionary<int, PlateVisual>();
        private readonly Transform[] stationRoots = new Transform[6];
        private readonly StationKind[] displayedStations = new StationKind[6];
        private readonly Transform[] selectionRims = new Transform[6];
        private Transform childRoot;
        private Transform cleanStack, dirtyStack, customer, orderSample;
        private TextMesh cleanCount, dirtyCount, orderText;
        private Font font;
        private Camera camera;
        private OrderKind displayedOrder;
        private bool orderBuilt;

        private sealed class PlateVisual
        {
            public Transform Root, FoodRoot, Flag;
            public TextMesh Caption, FlagText;
            public FoodState Food;
            public int Amount, Slot = -1;
            public PlateLocation Location;
            public bool Initialized;
            public float AngleFrom, AngleTo, MoveTime;
        }

        public Transform SceneRoot { get { return childRoot; } }

        public void Build(Camera gameCamera)
        {
            if (gameCamera == null) throw new ArgumentNullException(nameof(gameCamera));
            if (childRoot != null) return;
            camera = gameCamera;
            font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            childRoot = new GameObject("SushiKitchen_OriginalArt").transform;
            childRoot.position = Vector3.zero;
            ConfigureCameraAndLight();
            BuildPalette();
            BuildRoom();
            BuildCounterAndTrack();
            BuildInventoryAndCustomer();
            for (int i = 0; i < 6; i++)
            {
                Transform anchor = Empty("Station_" + (i + 1), childRoot, StationPosition(i));
                stationRoots[i] = anchor;
                displayedStations[i] = (StationKind)(-1);
                Vector3 p = SlotPosition(i);
                selectionRims[i] = OvalBand("SelectedSlot_" + i, childRoot, .395f, .395f, .032f, .012f, "selection");
                selectionRims[i].position = p + Vector3.up * .012f;
                selectionRims[i].gameObject.SetActive(false);
                float labelAngle = SlotAngle(i) * Mathf.Deg2Rad;
                Transform number = Text("SlotNumber_" + i, (i + 1).ToString(), childRoot,
                    new Vector3(Mathf.Cos(labelAngle) * (RingRadiusX + .50f), 1.17f,
                        Mathf.Sin(labelAngle) * (RingRadiusZ + .50f)), .21f, "ink");
                number.rotation = camera.transform.rotation;
                var proxy = new GameObject("PickSlot_" + i);
                proxy.transform.SetParent(childRoot, false);
                proxy.transform.position = p + Vector3.up * .18f;
                var collider = proxy.AddComponent<BoxCollider>();
                collider.size = new Vector3(.82f, .5f, .82f);
                proxy.AddComponent<SushiSlotPick>().Index = i;
            }
        }

        public Vector3 SlotPosition(int slot)
        {
            if (slot < 0 || slot >= 6) throw new ArgumentOutOfRangeException(nameof(slot));
            float a = SlotAngle(slot) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * RingRadiusX, PlateHeight, Mathf.Sin(a) * RingRadiusZ);
        }

        public Vector3 SlotScreenPoint(int slot)
        {
            return camera == null ? Vector3.zero : camera.WorldToScreenPoint(SlotPosition(slot) + Vector3.up * .37f);
        }

        public int RaycastSlot(Vector2 screenPoint)
        {
            if (camera == null) return -1;
            RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPoint), 50f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                SushiSlotPick pick = hit.collider.GetComponent<SushiSlotPick>();
                if (pick != null) return pick.Index;
            }
            return -1;
        }

        public void Render(Session session, int selectedSlot = -1)
        {
            if (session == null || childRoot == null) return;
            RoundSnapshot state = session.State;
            for (int i = 0; i < 6; i++)
            {
                selectionRims[i].gameObject.SetActive(i == selectedSlot);
                if (displayedStations[i] != state.Stations[i])
                {
                    ClearChildren(stationRoots[i]);
                    BuildStation(stationRoots[i], state.Stations[i]);
                    displayedStations[i] = state.Stations[i];
                }
            }
            int stagedIndex = 0;
            foreach (PlateState plate in state.Plates)
            {
                PlateVisual visual;
                if (!plates.TryGetValue(plate.Id, out visual))
                {
                    visual = BuildPlate(plate.Id);
                    plates.Add(plate.Id, visual);
                }
                bool visible = plate.Location == PlateLocation.Ring || plate.Location == PlateLocation.Staged;
                visual.Root.gameObject.SetActive(visible);
                if (!visible) { visual.Initialized = false; continue; }
                if (!visual.Initialized || visual.Food != plate.Food || visual.Amount != plate.AmountUnits)
                {
                    ClearChildren(visual.FoodRoot);
                    BuildFood(visual.FoodRoot, plate.Food, plate.AmountUnits);
                    visual.Food = plate.Food;
                    visual.Amount = plate.AmountUnits;
                    visual.Caption.text = FoodName(plate.Food) + (plate.AmountUnits == 1 ? " · 半份" : " · 整份");
                }
                if (plate.Location == PlateLocation.Ring)
                {
                    if (!visual.Initialized || visual.Location != PlateLocation.Ring)
                    {
                        visual.AngleFrom = visual.AngleTo = SlotAngle(plate.Slot);
                        visual.MoveTime = -10;
                    }
                    else if (visual.Slot != plate.Slot)
                    {
                        visual.AngleFrom = CurrentPlateAngle(visual);
                        visual.AngleTo = visual.AngleFrom + Mathf.DeltaAngle(visual.AngleFrom, SlotAngle(plate.Slot));
                        visual.MoveTime = Time.unscaledTime;
                    }
                    float angle = CurrentPlateAngle(visual) * Mathf.Deg2Rad;
                    visual.Root.position = new Vector3(Mathf.Cos(angle) * RingRadiusX, PlateHeight, Mathf.Sin(angle) * RingRadiusZ);
                }
                else
                {
                    visual.Root.position = new Vector3(-2.55f + stagedIndex * .40f, PlateHeight, -2.43f);
                    visual.Root.localScale = Vector3.one * .75f;
                    stagedIndex++;
                }
                if (plate.Location == PlateLocation.Ring) visual.Root.localScale = Vector3.one;
                visual.Flag.GetComponent<Renderer>().sharedMaterial = materials[plate.Deliverable ? "selection" : "gold"];
                visual.FlagText.text = plate.Deliverable ? "交" : "留";
                visual.Caption.color = plate.BaseSaleRequested ? new Color(.43f, .20f, .08f) : materials["ink"].color;
                visual.Caption.text = FoodName(plate.Food) + (plate.AmountUnits == 1 ? " · 半份" : " · 整份") + (plate.BaseSaleRequested ? " / 出售" : "");
                visual.Caption.transform.rotation = camera.transform.rotation;
                visual.FlagText.transform.rotation = camera.transform.rotation;
                visual.Slot = plate.Slot;
                visual.Location = plate.Location;
                visual.Initialized = true;
            }
            cleanCount.text = "净盘 " + state.CleanPlateCount;
            dirtyCount.text = "待洗 " + state.DirtyPlateCount;
            cleanStack.localScale = new Vector3(1, Mathf.Max(.18f, state.CleanPlateCount * .16f), 1);
            dirtyStack.localScale = new Vector3(1, Mathf.Max(.18f, state.DirtyPlateCount * .16f), 1);
            cleanStack.gameObject.SetActive(state.CleanPlateCount > 0);
            dirtyStack.gameObject.SetActive(state.DirtyPlateCount > 0);
            if (!orderBuilt || displayedOrder != state.Order.Kind)
            {
                ClearChildren(orderSample);
                BuildFood(orderSample, state.Order.RequiredFood, state.Order.RequiredAmountUnits);
                displayedOrder = state.Order.Kind;
                orderBuilt = true;
            }
            string title = state.Order.Kind == OrderKind.WholeHeated ? "整份熟卷" : "快取小份";
            orderText.text = title + "\n" + state.Order.DeliveredQuantity + "/" + state.Order.RequiredQuantity + "  等待 " + state.Order.RemainingWait;
            orderText.transform.rotation = camera.transform.rotation;
            customer.localRotation = Quaternion.Euler(0, 8f * Mathf.Sin(Time.unscaledTime * .55f), 0);
        }

        public void Dispose()
        {
            if (childRoot != null) UnityEngine.Object.Destroy(childRoot.gameObject);
            foreach (Material material in materials.Values) UnityEngine.Object.Destroy(material);
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            materials.Clear(); meshes.Clear(); plates.Clear(); childRoot = null;
        }

        public static string StationName(StationKind kind)
        {
            return Rules.StationName(kind);
        }

        public static string FoodName(FoodState food)
        {
            return Rules.FoodName(food);
        }

        private static float SlotAngle(int slot) { return -90f - slot * 60f; }
        private static float CurrentPlateAngle(PlateVisual p)
        {
            return Mathf.LerpAngle(p.AngleFrom, p.AngleTo, Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - p.MoveTime) / .28f)));
        }
        private Vector3 StationPosition(int slot)
        {
            float a = SlotAngle(slot) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * 1.32f, 1.00f, Mathf.Sin(a) * .71f);
        }

        private void ConfigureCameraAndLight()
        {
            camera.orthographic = true;
            float viewAspect = camera.pixelRect.height > 1 ? camera.pixelRect.width / camera.pixelRect.height : camera.aspect;
            camera.orthographicSize = Mathf.Max(3.7f, 5.15f / Mathf.Max(.6f, viewAspect));
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 60;
            camera.transform.position = new Vector3(.3f, 8.3f, -9.3f);
            camera.transform.LookAt(new Vector3(0, .65f, .45f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.11f, .19f, .18f);
            camera.allowHDR = true;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.63f, .69f, .70f) * .68f;
            RenderSettings.ambientEquatorColor = new Color(.45f, .41f, .34f) * .7f;
            RenderSettings.ambientGroundColor = new Color(.15f, .18f, .17f);
            Transform key = Empty("Kitchen_WarmKey", childRoot, new Vector3(0, 6, -2));
            var light = key.gameObject.AddComponent<Light>();
            light.type = LightType.Directional; light.color = new Color(1, .88f, .72f); light.intensity = 1.1f;
            light.shadows = LightShadows.Soft; key.rotation = Quaternion.Euler(48, -32, 0);
            QualitySettings.shadowDistance = 30;
            Transform lamp = Empty("Kitchen_CounterLight", childRoot, new Vector3(-2.2f, 3.8f, 1.8f));
            light = lamp.gameObject.AddComponent<Light>(); light.type = LightType.Point;
            light.color = new Color(1, .68f, .34f); light.intensity = .8f; light.range = 7;
        }

        private void BuildPalette()
        {
            AddMaterial("wood", new Color(.57f, .32f, .16f));
            AddMaterial("woodTop", new Color(.77f, .55f, .32f));
            AddMaterial("cream", new Color(.91f, .88f, .74f));
            AddMaterial("rice", new Color(.98f, .95f, .82f));
            AddMaterial("track", new Color(.045f, .23f, .19f));
            AddMaterial("ink", new Color(.065f, .13f, .13f));
            AddMaterial("nori", new Color(.09f, .18f, .075f));
            AddMaterial("salmon", new Color(.94f, .36f, .25f));
            AddMaterial("cucumber", new Color(.36f, .57f, .12f));
            AddMaterial("gold", new Color(.93f, .61f, .17f));
            AddMaterial("grilled", new Color(.45f, .23f, .085f));
            AddMaterial("steel", new Color(.56f, .66f, .64f), .45f);
            AddMaterial("wall", new Color(.78f, .81f, .69f));
            AddMaterial("floor", new Color(.33f, .40f, .35f));
            AddMaterial("customer", new Color(.40f, .62f, .62f));
            AddMaterial("skin", new Color(.85f, .60f, .40f));
            AddMaterial("selection", new Color(.28f, .82f, .59f), 0, true);
            AddMaterial("lamp", new Color(1, .72f, .34f), 0, true);
        }

        private void AddMaterial(string id, Color color, float metal = 0, bool glow = false)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            var material = new Material(shader) { name = "Sushi_" + id, color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", metal > 0 ? .35f : .19f);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .22f); }
            materials.Add(id, material);
        }

        private void BuildRoom()
        {
            RoundedBox("KitchenFloor", childRoot, new Vector3(0, -.12f, .4f), new Vector3(10.8f, .2f, 8.2f), .10f, "floor");
            for (int i = -5; i <= 5; i++)
                RoundedBox("FloorJoint", childRoot, new Vector3(i, -.012f, .4f), new Vector3(.015f, .009f, 8), .004f, "track");
            RoundedBox("BackWall", childRoot, new Vector3(0, 1.8f, 3.52f), new Vector3(9.9f, 3.6f, .16f), .08f, "wall");
            RoundedBox("WallWoodRail", childRoot, new Vector3(0, 1.02f, 3.40f), new Vector3(9.8f, .09f, .08f), .025f, "wood");
            RoundedBox("SignFrame", childRoot, new Vector3(0, 2.83f, 3.33f), new Vector3(3.8f, .73f, .18f), .13f, "wood");
            RoundedBox("SignInlay", childRoot, new Vector3(0, 2.83f, 3.22f), new Vector3(3.55f, .53f, .05f), .09f, "track");
            Transform sign = Text("KitchenName", "回 转 寿 司 工 坊", childRoot, new Vector3(0, 2.84f, 3.17f), .28f, "cream");
            sign.rotation = Quaternion.identity;
            for (int i = -3; i <= 3; i++)
            {
                Transform curtain = RoundedBox("NorenCurtain", childRoot, new Vector3(i * .65f, 2.04f, 3.20f), new Vector3(.59f, .75f, .045f), .025f, "track");
                Cylinder("NorenBadge", curtain, new Vector3(0, .04f, -.036f), .10f, .012f, "cream", Vector3.forward);
            }
            for (int x = -1; x <= 1; x += 2)
            {
                Cylinder("PendantCable", childRoot, new Vector3(x * 2.8f, 3.30f, .35f), .012f, .48f, "ink", Vector3.up);
                Cylinder("PendantShade", childRoot, new Vector3(x * 2.8f, 3.05f, .35f), .29f, .13f, "track", Vector3.up);
                Sphere("PendantGlow", childRoot, new Vector3(x * 2.8f, 2.97f, .35f), new Vector3(.25f, .07f, .25f), "lamp");
            }
            BuildPlant(new Vector3(-4.1f, .45f, 2.15f));
            BuildPlant(new Vector3(4.1f, .45f, 2.15f));
        }

        private void BuildCounterAndTrack()
        {
            RoundedBox("IslandBase", childRoot, new Vector3(0, .40f, 0), new Vector3(7.3f, .8f, 5.6f), .78f, "wood");
            RoundedBox("IslandTop", childRoot, new Vector3(0, .91f, 0), new Vector3(7.65f, .22f, 5.85f), .86f, "woodTop");
            for (int i = -5; i <= 5; i++)
                RoundedBox("WoodGrainLine", childRoot, new Vector3(i * .57f, 1.024f, 0), new Vector3(.013f, .007f, 4.7f), .003f, "wood");
            Transform track = OvalBand("SingleLoopConveyor", childRoot, RingRadiusX, RingRadiusZ, .68f, .075f, "track");
            track.position = new Vector3(0, TrackHeight, 0);
            Transform rim = OvalBand("OuterBrassRail", childRoot, RingRadiusX + .38f, RingRadiusZ + .38f, .036f, .022f, "gold");
            rim.position = new Vector3(0, TrackHeight + .045f, 0);
            rim = OvalBand("InnerBrassRail", childRoot, RingRadiusX - .38f, RingRadiusZ - .38f, .025f, .019f, "gold");
            rim.position = new Vector3(0, TrackHeight + .042f, 0);
            for (int i = 0; i < 24; i++)
            {
                float angle = (-90f - i * 15f) * Mathf.Deg2Rad;
                Vector3 position = new Vector3(Mathf.Cos(angle) * RingRadiusX, TrackHeight + .047f, Mathf.Sin(angle) * RingRadiusZ);
                Transform slat = RoundedBox("ConveyorSeam", childRoot, position, new Vector3(.56f, .005f, .012f), .003f, "steel");
                Vector3 tangent = new Vector3(-Mathf.Sin(angle) * RingRadiusX, 0, Mathf.Cos(angle) * RingRadiusZ);
                slat.rotation = Quaternion.LookRotation(tangent);
            }
            for (int i = 0; i < 6; i++)
            {
                float angle = (-120f - i * 60f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(angle) * RingRadiusX, TrackHeight + .059f, Mathf.Sin(angle) * RingRadiusZ);
                Vector3 direction = new Vector3(Mathf.Sin(angle) * RingRadiusX, 0, -Mathf.Cos(angle) * RingRadiusZ).normalized;
                Transform arrow = Arrow("BeltDirection", childRoot, p, "gold");
                arrow.rotation = Quaternion.LookRotation(direction);
            }
            RoundedBox("FeedTray", childRoot, new Vector3(-2.04f, 1.062f, -2.43f), new Vector3(1.65f, .055f, .58f), .10f, "cream");
            Text("FeedLabel", "切分暂存 · 下步入环", childRoot, new Vector3(-2.10f, 1.30f, -2.73f), .13f, "ink").rotation = camera.transform.rotation;
        }

        private void BuildInventoryAndCustomer()
        {
            RoundedBox("WashBasin", childRoot, new Vector3(-3.2f, 1.075f, .28f), new Vector3(.65f, .14f, 1.02f), .10f, "steel");
            RoundedBox("BasinWater", childRoot, new Vector3(-3.2f, 1.151f, .28f), new Vector3(.49f, .017f, .81f), .08f, "customer");
            cleanStack = Empty("CleanPlateStack", childRoot, new Vector3(-3.22f, 1.07f, 1.37f));
            dirtyStack = Empty("DirtyPlateStack", childRoot, new Vector3(-3.22f, 1.07f, -.82f));
            for (int i = 0; i < 6; i++)
            {
                Cylinder("CleanPlate", cleanStack, new Vector3(0, i * .055f, 0), .27f, .045f, "cream", Vector3.up);
                Cylinder("DirtyPlate", dirtyStack, new Vector3(0, i * .055f, 0), .27f, .045f, "cream", Vector3.up);
                Sphere("DirtyPlateStain", dirtyStack, new Vector3(.03f, i * .055f + .025f, 0), new Vector3(.15f, .008f, .12f), "grilled");
            }
            cleanCount = Text("CleanInventoryLabel", "净盘", childRoot, new Vector3(-3.30f, 1.58f, 1.35f), .18f, "ink").GetComponent<TextMesh>();
            dirtyCount = Text("DirtyInventoryLabel", "待洗", childRoot, new Vector3(-3.30f, 1.51f, -.87f), .18f, "ink").GetComponent<TextMesh>();
            cleanCount.transform.rotation = dirtyCount.transform.rotation = camera.transform.rotation;
            Cylinder("CustomerStool", childRoot, new Vector3(4.15f, .53f, -.78f), .40f, .11f, "salmon", Vector3.up);
            Cylinder("StoolLeg", childRoot, new Vector3(4.15f, .26f, -.78f), .09f, .48f, "wood", Vector3.up);
            customer = Empty("WaitingCustomer", childRoot, new Vector3(4.12f, .64f, -.80f));
            Sphere("CustomerApron", customer, new Vector3(0, .38f, 0), new Vector3(.42f, .63f, .32f), "customer");
            Sphere("CustomerHead", customer, new Vector3(0, .95f, -.02f), new Vector3(.40f, .43f, .36f), "skin");
            Sphere("CustomerHair", customer, new Vector3(0, 1.11f, .02f), new Vector3(.43f, .20f, .37f), "ink");
            for (int i = -1; i <= 1; i += 2)
            {
                Sphere("CustomerEye", customer, new Vector3(i * .085f, .97f, -.185f), new Vector3(.029f, .035f, .015f), "ink");
                Sphere("CustomerCheek", customer, new Vector3(i * .13f, .90f, -.178f), new Vector3(.050f, .025f, .016f), "salmon");
                Cylinder("CustomerSleeve", customer, new Vector3(i * .28f, .43f, -.13f), .10f, .39f, "customer", new Vector3(i * .35f, -.65f, -.6f));
                Sphere("CustomerHand", customer, new Vector3(i * .29f, .27f, -.27f), new Vector3(.12f, .10f, .12f), "skin");
            }
            RoundedBox("OrderSampleBoard", childRoot, new Vector3(3.18f, 1.08f, -.7f), new Vector3(.57f, .09f, .77f), .10f, "cream");
            orderSample = Empty("RequestedDish", childRoot, new Vector3(3.18f, 1.145f, -.7f));
            orderSample.localScale = Vector3.one * .70f;
            orderText = Text("CustomerOrder", "当前订单", childRoot, new Vector3(4.08f, 2.21f, -.80f), .17f, "cream").GetComponent<TextMesh>();
            orderText.transform.rotation = camera.transform.rotation;
            Transform delivery = Arrow("DeliveryPoint", childRoot, new Vector3(2.90f, 1.16f, -.76f), "selection");
            delivery.rotation = Quaternion.Euler(0, 90, 0);
        }

        private void BuildStation(Transform parent, StationKind kind)
        {
            RoundedBox("StationMat", parent, new Vector3(0, .035f, 0), new Vector3(.84f, .06f, .58f), .12f, kind == StationKind.None ? "cream" : "wood");
            switch (kind)
            {
                case StationKind.Filling:
                    Cylinder("IngredientBowl", parent, new Vector3(-.19f, .14f, .02f), .14f, .17f, "cream", Vector3.up);
                    Sphere("GreenIngredients", parent, new Vector3(-.19f, .235f, .02f), new Vector3(.20f, .05f, .19f), "cucumber");
                    RoundedBox("SalmonTray", parent, new Vector3(.20f, .075f, -.02f), new Vector3(.26f, .035f, .39f), .04f, "steel");
                    for (int i = 0; i < 3; i++) RoundedBox("IngredientStrip", parent, new Vector3(.20f, .102f, -.12f + i * .105f), new Vector3(.21f, .038f, .07f), .02f, "salmon");
                    break;
                case StationKind.Rolling:
                    for (int i = 0; i < 9; i++) Cylinder("BambooMatRib", parent, new Vector3(-.33f + i * .082f, .075f, 0), .018f, .43f, "gold", Vector3.forward);
                    Cylinder("BambooRoller", parent, new Vector3(0, .16f, .16f), .075f, .66f, "woodTop", Vector3.right);
                    RoundedBox("SeaweedSheet", parent, new Vector3(0, .10f, -.085f), new Vector3(.46f, .015f, .24f), .015f, "nori");
                    break;
                case StationKind.Heating:
                    RoundedBox("OvenBody", parent, new Vector3(0, .18f, 0), new Vector3(.63f, .28f, .43f), .095f, "salmon");
                    RoundedBox("OvenFront", parent, new Vector3(0, .18f, -.226f), new Vector3(.40f, .16f, .018f), .035f, "ink");
                    RoundedBox("OvenGlow", parent, new Vector3(0, .145f, -.240f), new Vector3(.30f, .045f, .012f), .015f, "lamp");
                    for (int i = 0; i < 5; i++) Cylinder("GrillBar", parent, new Vector3(-.24f + i * .12f, .335f, 0), .015f, .33f, "steel", Vector3.forward);
                    break;
                case StationKind.Cutting:
                    RoundedBox("ChoppingBoard", parent, new Vector3(0, .088f, 0), new Vector3(.71f, .045f, .44f), .065f, "woodTop");
                    Transform knife = RoundedBox("KnifeBlade", parent, new Vector3(.08f, .24f, .06f), new Vector3(.33f, .15f, .019f), .011f, "steel");
                    knife.localRotation = Quaternion.Euler(0, 0, -18);
                    Transform handle = RoundedBox("KnifeHandle", parent, new Vector3(-.16f, .32f, .06f), new Vector3(.19f, .065f, .048f), .02f, "ink");
                    handle.localRotation = Quaternion.Euler(0, 0, -18);
                    Cylinder("CutSample", parent, new Vector3(.12f, .155f, -.13f), .055f, .095f, "nori", Vector3.forward);
                    break;
            }
            // Keep front-row labels below their own tool instead of projecting over the
            // back-row models; back-row labels sit above and behind their own tool.
            Vector3 captionPosition = parent.localPosition.z <= 0
                ? new Vector3(0, .12f, -.34f) : new Vector3(0, .47f, .25f);
            Transform caption = Text("StationCaption", StationName(kind), parent, captionPosition, .18f, parent.localPosition.z <= 0 ? "ink" : "cream");
            caption.rotation = camera.transform.rotation;
        }

        private PlateVisual BuildPlate(int id)
        {
            var p = new PlateVisual();
            p.Root = Empty("Plate_" + id, childRoot, Vector3.zero);
            Cylinder("CeramicDish", p.Root, new Vector3(0, .022f, 0), .345f, .042f, "cream", Vector3.up);
            Transform rim = OvalBand("CeramicRim", p.Root, .319f, .319f, .022f, .022f, "track");
            rim.localPosition = new Vector3(0, .05f, 0);
            p.FoodRoot = Empty("DishFood", p.Root, new Vector3(0, .053f, 0));
            p.Flag = Cylinder("DeliveryFlag", p.Root, new Vector3(.29f, .115f, -.23f), .083f, .022f, "selection", Vector3.forward);
            p.FlagText = Text("DeliveryMarker", "交", p.Root, new Vector3(.29f, .115f, -.25f), .12f, "ink").GetComponent<TextMesh>();
            p.Caption = Text("DishCaption", "", p.Root, new Vector3(0, .46f, 0), .145f, "ink").GetComponent<TextMesh>();
            return p;
        }

        private void BuildFood(Transform parent, FoodState food, int amount)
        {
            bool small = amount == 1 || food == FoodState.SmallRoll || food == FoodState.SmallHeatedRoll;
            bool heated = food == FoodState.HeatedRoll || food == FoodState.SmallHeatedRoll;
            if (food == FoodState.RiceBase || food == FoodState.FilledRice)
            {
                RoundedBox("PressedRice", parent, new Vector3(0, .055f, 0), new Vector3(.43f, .11f, .28f), .065f, "rice");
                for (int i = 0; i < 6; i++)
                    Sphere("RiceGrain", parent, new Vector3(-.15f + i * .06f, .115f, -.065f), new Vector3(.04f, .018f, .07f), "cream");
                if (food == FoodState.FilledRice)
                {
                    RoundedBox("UnrolledNori", parent, new Vector3(0, .008f, 0), new Vector3(.50f, .015f, .32f), .012f, "nori");
                    RoundedBox("SalmonFilling", parent, new Vector3(0, .145f, -.045f), new Vector3(.39f, .045f, .06f), .025f, "salmon");
                    RoundedBox("CucumberFilling", parent, new Vector3(0, .144f, .045f), new Vector3(.39f, .04f, .055f), .022f, "cucumber");
                }
                return;
            }
            float length = small ? .215f : .445f;
            Cylinder("NoriRoll", parent, new Vector3(0, .119f, 0), .115f, length, heated ? "grilled" : "nori", Vector3.forward);
            foreach (int direction in new[] { -1, 1 })
            {
                Cylinder("RiceCrossSection", parent, new Vector3(0, .119f, direction * (length * .5f + .002f)), .099f, .010f, "rice", Vector3.forward);
                Cylinder("SalmonCenter", parent, new Vector3(-.025f, .12f, direction * (length * .5f + .008f)), .034f, .014f, "salmon", Vector3.forward);
                Cylinder("CucumberCenter", parent, new Vector3(.035f, .135f, direction * (length * .5f + .009f)), .026f, .015f, "cucumber", Vector3.forward);
            }
            if (heated)
            {
                for (int i = 0; i < 3; i++)
                    RoundedBox("ToastStripe", parent, new Vector3(0, .23f, -.075f + i * .075f), new Vector3(.16f, .018f, .021f), .007f, "gold");
                for (int i = 0; i < 2; i++)
                {
                    Transform steam = Cylinder("HeatWisp", parent, new Vector3(-.045f + i * .09f, .30f, 0), .009f, .085f, "cream", new Vector3(i == 0 ? -.2f : .2f, 1, 0));
                    steam.localScale *= .75f;
                }
            }
            if (small) RoundedBox("HalfPortionAccent", parent, new Vector3(.20f, .045f, 0), new Vector3(.055f, .015f, .19f), .012f, "gold");
        }

        private void BuildPlant(Vector3 position)
        {
            Cylinder("PlantPot", childRoot, position, .22f, .43f, "salmon", Vector3.up);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7;
                Transform leaf = Sphere("KitchenLeaf", childRoot, position + new Vector3(Mathf.Cos(a) * .14f, .45f, Mathf.Sin(a) * .14f), new Vector3(.13f, .56f, .20f), "cucumber");
                leaf.rotation = Quaternion.Euler(18 * Mathf.Sin(a), i * 51, 18 * Mathf.Cos(a));
            }
        }

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
            var mesh = new Mesh { name = "SushiMesh_" + name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
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

    public sealed class SushiSlotPick : MonoBehaviour
    {
        public int Index;
    }
}
