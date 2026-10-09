using System;
using System.Collections.Generic;
using RingToss.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RingToss.Presentation
{
    // Scene decoration and selection never decide a hit or change a transaction.
    public sealed class StallSceneView
    {
        public Camera Camera { get; private set; }
        private readonly Transform[] slotRoots = new Transform[6];
        private readonly string[] signatures = new string[6];
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly Font font;
        private readonly Transform ring, leftHand, rightHand;
        private readonly LineRenderer trail;
        private readonly Mesh ringMesh;
        private readonly Material ringMaterial;
        private readonly Transform aimMarker;

        public StallSceneView(Font uiFont)
        {
            font = uiFont;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 35;
            QualitySettings.pixelLightCount = 8;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.18f, .24f, .36f);
            RenderSettings.ambientEquatorColor = new Color(.17f, .19f, .26f);
            RenderSettings.ambientGroundColor = new Color(.10f, .09f, .14f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.08f, .13f, .21f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .017f;
            var cam = new GameObject("Player shoulder camera");
            Camera = cam.AddComponent<Camera>();
            Camera.transform.position = new Vector3(3.2f, 4.2f, -5.8f);
            Camera.transform.LookAt(new Vector3(0, .3f, 4.5f));
            Camera.fieldOfView = 44;
            Camera.nearClipPlane = .1f;
            Camera.farClipPlane = 100;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(.065f, .1f, .18f);
            cam.AddComponent<AudioListener>();
            var sun = new GameObject("Warm evening key").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1, .79f, .57f); sun.intensity = .75f;
            sun.transform.rotation = Quaternion.Euler(45, -35, 0); sun.shadows = LightShadows.Soft;
            sun.shadowBias = .025f;
            BuildMarket();
            ringMesh = MakeRing((float)Rules.InnerRadius, .037f);
            ringMaterial = Mat("jade_ring", new Color(.12f, .85f, .67f), .35f);
            ring = MeshObject("Thrown ring", ringMesh, ringMaterial, Vector3.zero).transform;
            var line = new GameObject("Last throw trajectory");
            trail = line.AddComponent<LineRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(.5f, 1, .8f, .75f); trail.endColor = new Color(.5f, 1, .8f, .2f);
            trail.startWidth = .018f; trail.endWidth = .008f; trail.useWorldSpace = true;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            var marker = MeshObject("Aim landing guide", MakeRing(.1f, .012f), Mat("aim", new Color(1, .79f, .28f)), new Vector3(0, .05f, 4));
            aimMarker = marker.transform;
            var sleeve = Mat("sleeve", new Color(.22f, .32f, .42f));
            var skin = Mat("hands", new Color(.84f, .56f, .36f));
            leftHand = Primitive("Left hand", PrimitiveType.Sphere, new Vector3(-.27f, .76f, -.05f), new Vector3(.14f, .1f, .2f), skin).transform;
            rightHand = Primitive("Right hand", PrimitiveType.Sphere, new Vector3(.28f, .76f, -.05f), new Vector3(.14f, .1f, .2f), skin).transform;
            Beam("Left sleeve", new Vector3(-.5f, .47f, -.8f), leftHand.position, .14f, sleeve);
            Beam("Right sleeve", new Vector3(.5f, .47f, -.8f), rightHand.position, .14f, sleeve);
            for (int i = 0; i < 6; i++)
            {
                var p = Rules3D.SlotPosition(i);
                slotRoots[i] = new GameObject("Slot " + (i + 1)).transform;
                slotRoots[i].position = new Vector3((float)p.X, 0, (float)p.Z);
            }
        }

        private Material Mat(string id, Color color, float metallic = 0)
        {
            if (materials.TryGetValue(id, out var cached)) return cached;
            var m = new Material(Shader.Find("Standard")) { name = id, color = color };
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", metallic > 0 ? .4f : .16f);
            materials.Add(id, m); return m;
        }
        private GameObject Primitive(string id, PrimitiveType type, Vector3 p, Vector3 scale, Material m, Transform parent = null)
        {
            var o = GameObject.CreatePrimitive(type); o.name = id; o.transform.SetParent(parent, false);
            o.transform.localPosition = p; o.transform.localScale = scale;
            o.GetComponent<Renderer>().sharedMaterial = m;
            UnityEngine.Object.Destroy(o.GetComponent<Collider>());
            return o;
        }
        private GameObject MeshObject(string id, Mesh mesh, Material m, Vector3 p, Transform parent = null)
        {
            var o = new GameObject(id); o.transform.SetParent(parent, false); o.transform.localPosition = p;
            o.AddComponent<MeshFilter>().sharedMesh = mesh; o.AddComponent<MeshRenderer>().sharedMaterial = m; return o;
        }
        private void Beam(string id, Vector3 from, Vector3 to, float width, Material m, Transform parent = null)
        {
            var o = Primitive(id, PrimitiveType.Cylinder, (from + to) * .5f, new Vector3(width, (to - from).magnitude / 2, width), m, parent);
            o.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }
        private void Text(string text, Vector3 p, float size, Color color, Transform parent = null)
        {
            var o = new GameObject("World label " + text); o.transform.SetParent(parent, false); o.transform.localPosition = p;
            var t = o.AddComponent<TextMesh>(); t.text = text; t.font = font; t.fontSize = 64; t.characterSize = size;
            t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = color;
            o.GetComponent<Renderer>().sharedMaterial = font.material;
            o.transform.rotation = Quaternion.LookRotation(o.transform.position - Camera.transform.position);
        }
        private void BuildMarket()
        {
            var stone = Mat("stone", new Color(.29f, .34f, .38f));
            var cloth = Mat("cloth", new Color(.68f, .3f, .2f));
            var trim = Mat("cream", new Color(.92f, .79f, .56f));
            var timber = Mat("timber", new Color(.3f, .16f, .1f));
            var metal = Mat("iron", new Color(.13f, .19f, .22f), .45f);
            Primitive("Market paving", PrimitiveType.Cube, new Vector3(5, -.13f, 5), new Vector3(22, .2f, 24), stone);
            for (int z = -2; z <= 11; z++)
                for (int x = -3; x <= 7; x++)
                    Primitive("Paving slab", PrimitiveType.Cube, new Vector3(x * 1.5f + (z % 2 == 0 ? 0 : .75f), -.019f, z * 1.2f),
                        new Vector3(1.46f, .02f, 1.16f), Mat("slab" + ((x + z + 30) % 3), Color.Lerp(new Color(.27f, .3f, .33f), stone.color, ((x + z + 30) % 3) / 3f)));
            Primitive("Display rug", PrimitiveType.Cube, new Vector3(0, .008f, 4.5f), new Vector3(5.6f, .045f, 5.4f), cloth);
            for (int x = -1; x <= 1; x++)
                Primitive("Rug stripe", PrimitiveType.Cube, new Vector3(x * 1.65f, .035f, 4.5f), new Vector3(.025f, .009f, 5.15f), trim);
            Primitive("Throwing line", PrimitiveType.Cube, new Vector3(0, .03f, .36f), new Vector3(3, .04f, .08f), trim);
            Primitive("Stall counter", PrimitiveType.Cube, new Vector3(0, .65f, 8), new Vector3(5.8f, 1.3f, .9f), timber);
            Primitive("Counter cloth", PrimitiveType.Cube, new Vector3(0, 1.32f, 8), new Vector3(5.95f, .06f, 1), trim);
            for (int i = 0; i < 6; i++)
            {
                string id = i % 3 == 0 ? "toy_duck" : i % 3 == 1 ? "ceramic_cup" : "radio";
                var model = Resources.Load<GameObject>("Models/" + id);
                if (model == null) continue;
                var decoration = UnityEngine.Object.Instantiate(model);
                decoration.name = "Counter prize " + id;
                decoration.transform.position = new Vector3(-2.35f + i * .75f, 1.36f, 7.8f);
                decoration.transform.rotation = Quaternion.Euler(0, 180, 0) * decoration.transform.rotation;
            }
            for (int x = -1; x <= 1; x += 2)
            {
                Primitive("Awning pole", PrimitiveType.Cylinder, new Vector3(x * 3.05f, 1.65f, 7.65f), new Vector3(.085f, 1.65f, .085f), metal);
                Primitive("Awning back pole", PrimitiveType.Cylinder, new Vector3(x * 3.05f, 1.65f, 9.2f), new Vector3(.085f, 1.65f, .085f), metal);
            }
            for (int i = 0; i < 12; i++)
            {
                var strip = Primitive("Striped canvas", PrimitiveType.Cube, new Vector3(-2.8f + i * .51f, 3.2f, 8.5f), new Vector3(.51f, .065f, 2.2f), i % 2 == 0 ? cloth : trim);
                strip.transform.localRotation = Quaternion.Euler(-7, 0, 0);
                Primitive("Canvas valance", PrimitiveType.Cube, new Vector3(-2.8f + i * .51f, 3.01f, 7.41f), new Vector3(.5f, .3f, .05f), i % 2 == 0 ? cloth : trim);
            }
            Primitive("Stall wooden sign", PrimitiveType.Cube, new Vector3(0, 2.66f, 7.32f), new Vector3(3.1f, .46f, .075f), timber);
            Text("桥脚 · 套圈改造摊", new Vector3(0, 2.66f, 7.26f), .045f, trim.color);
            for (int i = 0; i < 5; i++)
            {
                float x = -2.5f + 1.25f * i;
                var lamp = Mat("lantern" + i, new Color(1, .43f + i * .025f, .13f));
                lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", new Color(1, .22f, .06f) * .6f);
                Primitive("Paper lantern", PrimitiveType.Sphere, new Vector3(x, 2.57f, 7.38f), new Vector3(.28f, .38f, .28f), lamp);
                Beam("Lantern cord", new Vector3(x, 2.76f, 7.38f), new Vector3(x, 3.01f, 7.38f), .014f, metal);
                if (i % 2 == 0)
                {
                    var light = new GameObject("Stall warm lamp").AddComponent<Light>(); light.type = LightType.Point;
                    light.transform.position = new Vector3(x, 2.45f, 6.9f); light.color = new Color(1, .66f, .3f);
                    light.range = 6; light.intensity = 2.2f;
                }
            }
            // Riverfront gives the booth a place rather than an isolated test grid.
            Primitive("River", PrimitiveType.Cube, new Vector3(-9, -.42f, 6), new Vector3(6, .1f, 30), Mat("river", new Color(.065f, .25f, .34f), .45f));
            for (int i = 0; i < 10; i++)
            {
                float z = -2 + i * 1.5f;
                Beam("Quay post", new Vector3(-5.7f, 0, z), new Vector3(-5.7f, 1, z), .08f, timber);
                Beam("Quay rail", new Vector3(-5.7f, .85f, z), new Vector3(-5.7f, .85f, z + 1.5f), .065f, timber);
            }
            for (int i = 0; i < 5; i++)
            {
                float x = -7 + i * 3.5f;
                var building = Mat("building" + i, new Color(.13f + i * .008f, .18f, .25f));
                Primitive("Night market building", PrimitiveType.Cube, new Vector3(x, 2.5f, 16), new Vector3(3.25f, 5 + i % 2, 3), building);
                for (int w = 0; w < 2; w++)
                    Primitive("Lit window", PrimitiveType.Cube, new Vector3(x - .8f + w * 1.6f, 3.1f, 14.48f), new Vector3(.5f, .65f, .02f), trim);
            }
            for (int i = 0; i < 4; i++)
                Primitive("Stock crate", PrimitiveType.Cube, new Vector3(3.8f + (i % 2) * .6f, .23f + (i / 2) * .46f, 7.7f), new Vector3(.55f, .45f, .58f), timber);
            // A simple vendor silhouette is scene dressing, with no game state.
            Primitive("Vendor apron", PrimitiveType.Capsule, new Vector3(.9f, 1.65f, 8.65f), new Vector3(.4f, .4f, .32f), Mat("apron", new Color(.15f, .31f, .29f)));
            Primitive("Vendor head", PrimitiveType.Sphere, new Vector3(.9f, 2.18f, 8.65f), new Vector3(.3f, .35f, .3f), Mat("vendor_skin", new Color(.7f, .43f, .3f)));
        }

        public void Render(IReadOnlyList<SlotObject> objects, Vector3 ringPosition, IList<Vector3> points, int selected, bool flying, double azimuth, double elevation, double power)
        {
            for (int i = 0; i < 6; i++)
            {
                var item = objects[i];
                string signature = item == null ? "empty:" + (i == selected) : item.Id + ":" + item.Occupancy + ":" + item.Enabled + ":" + item.AngleDegrees + ":" + item.Durability + ":" + (i == selected);
                if (signature == signatures[i]) continue;
                signatures[i] = signature;
                foreach (Transform c in slotRoots[i]) UnityEngine.Object.Destroy(c.gameObject);
                var root = slotRoots[i];
                Primitive("Display cushion", PrimitiveType.Cylinder, new Vector3(0, .08f, 0), new Vector3(.95f, .08f, .95f),
                    Mat(i == selected ? "selected" : "base", i == selected ? new Color(.84f, .61f, .3f) : new Color(.54f, .37f, .28f)), root);
                if (item == null) continue;
                string id = item.Kind == ObjectKind.Fan ? "fan" : "rebound_board";
                bool prize = item.Occupancy == Occupancy.Prize;
                var prefab = Resources.Load<GameObject>("Models/" + id);
                if (prefab != null)
                {
                    var prop = UnityEngine.Object.Instantiate(prefab, root); prop.name = id;
                    prop.transform.localPosition = new Vector3(0, .16f, 0);
                    var importedRotation = prop.transform.localRotation;
                    prop.transform.localRotation = Quaternion.Euler(0, 180, 0) * importedRotation;
                    if (!prize && item.Kind == ObjectKind.Board)
                    {
                        prop.transform.localPosition = new Vector3(0, (float)Rules3D.AcceptHeight, 0);
                        // FBX is bottom-centred; pivot the actual visual plane at the Core centre.
                        var pivot = new GameObject("Rule board pivot").transform;
                        pivot.SetParent(root, false); pivot.localPosition = new Vector3(0, (float)Rules3D.AcceptHeight, 0);
                        prop.transform.SetParent(pivot, false);
                        prop.transform.localPosition = Vector3.zero; prop.transform.localRotation = importedRotation;
                        // Source Blender width is X, height is Z before the imported root conversion.
                        prop.transform.localScale = new Vector3(1f / .39f, 1, 1f / .36f);
                        var renderers = prop.GetComponentsInChildren<Renderer>();
                        var panel = Array.Find(renderers, r => r.name == "Board_Panel");
                        if (panel != null)
                        {
                            prop.transform.position -= panel.bounds.center - pivot.position;
                        }
                        pivot.localRotation = Quaternion.Euler(90 - (float)item.AngleDegrees, 0, 0);
                        var edge = Mat("board_edge", new Color(.3f, 1, .7f));
                        Beam("Rule top edge", new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0), .025f, edge, pivot);
                        Beam("Rule bottom edge", new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), .025f, edge, pivot);
                        Beam("Rule left edge", new Vector3(-.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), .025f, edge, pivot);
                        Beam("Rule right edge", new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), .025f, edge, pivot);
                    }
                    if (!prize && item.Kind == ObjectKind.Fan)
                    {
                        // Raising the fan is an explicit deployment, not a hidden collision shift.
                        prop.transform.localPosition = new Vector3(0, 1.6f - .375f, 0);
                        Beam("Expanded fan stand", new Vector3(0, .16f, 0), new Vector3(0, 1.25f, 0), .09f, Mat("fan_stand", new Color(.14f, .28f, .33f)), root);
                        Primitive("Rule support", PrimitiveType.Sphere, new Vector3(0, .9f, 0), Vector3.one * .2f, Mat("support", new Color(.18f, .63f, .54f)), root);
                    }
                    if (!prize && !item.Enabled)
                        foreach (var r in prop.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Mat("disabled", new Color(.3f, .33f, .34f));
                }
                else Primitive("Pending visual prop", PrimitiveType.Cylinder, new Vector3(0, .34f, 0), new Vector3(.3f, .2f, .3f), Mat("placeholder", new Color(.2f, .5f, .5f)), root);
                if (prize)
                {
                    Primitive("Catch post", PrimitiveType.Cylinder, new Vector3(0, (float)Rules3D.AcceptHeight / 2, 0),
                        new Vector3((float)Rules3D.PostRadius * 2, (float)Rules3D.AcceptHeight / 2, (float)Rules3D.PostRadius * 2),
                        Mat("post", new Color(.85f, .7f, .43f), .3f), root);
                    MeshObject("Acceptance marker", MakeRing(.24f, .009f), Mat("target", new Color(1, .78f, .32f)), new Vector3(0, (float)Rules3D.AcceptHeight, 0), root);
                }
                else if (item.Kind == ObjectKind.Fan)
                {
                    var direction = new Vector3(0, (float)Math.Sin(item.AngleDegrees * Math.PI / 180), (float)Math.Cos(item.AngleDegrees * Math.PI / 180));
                    Beam("Fan direction", new Vector3(0, 1.6f, 0), new Vector3(0, 1.6f, 0) + direction * .65f, .025f, Mat("function", new Color(.22f, .84f, .64f)), root);
                    if (i == selected)
                    {
                        var zone = Mat("fan_zone", new Color(.27f, .94f, .68f));
                        foreach (float x in new[] { -.6f, .6f })
                            foreach (float z in new[] { -1.2f, 1.2f })
                                Beam("Wind box upright", new Vector3(x, .8f, z), new Vector3(x, 2.4f, z), .012f, zone, root);
                        foreach (float y in new[] { .8f, 2.4f })
                        {
                            foreach (float x in new[] { -.6f, .6f }) Beam("Wind box depth", new Vector3(x, y, -1.2f), new Vector3(x, y, 1.2f), .012f, zone, root);
                            foreach (float z in new[] { -1.2f, 1.2f }) Beam("Wind box width", new Vector3(-.6f, y, z), new Vector3(.6f, y, z), .012f, zone, root);
                        }
                    }
                }
                Text((i + 1) + " · " + (prize ? item.PrizeValue + "币" : "耐久" + item.Durability), new Vector3(0, .17f, -.62f), .028f, new Color(1, .92f, .73f), root);
                var proxy = new GameObject("Visual selection"); proxy.transform.SetParent(root, false);
                proxy.transform.localPosition = new Vector3(0, .4f, 0);
                proxy.AddComponent<BoxCollider>().size = new Vector3(.85f, .85f, .85f);
                proxy.AddComponent<StallSlotMarker>().Index = i;
            }
            ring.position = ringPosition;
            ring.rotation = flying ? Quaternion.Euler((float)(Time.time * 90), 0, 0) : Quaternion.identity;
            leftHand.gameObject.SetActive(!flying); rightHand.gameObject.SetActive(!flying);
            trail.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) trail.SetPosition(i, points[i]);
            // Landing guide only shows direction, not a predicted reward.
            float a = (float)azimuth * Mathf.Deg2Rad;
            aimMarker.position = new Vector3(Mathf.Sin(a) * 2.3f, .07f, Mathf.Cos(a) * 2.3f);
        }
        public int Pick(Vector2 screen)
        {
            if (!Physics.Raycast(Camera.ScreenPointToRay(screen), out var hit, 100)) return -1;
            var marker = hit.collider.GetComponent<StallSlotMarker>(); return marker == null ? -1 : marker.Index;
        }
        private static Mesh MakeRing(float radius, float tube)
        {
            const int major = 64, minor = 8;
            var vertices = new Vector3[major * minor]; var normals = new Vector3[vertices.Length];
            var triangles = new int[major * minor * 6];
            for (int i = 0; i < major; i++)
                for (int j = 0; j < minor; j++)
                {
                    float a = i * Mathf.PI * 2 / major, b = j * Mathf.PI * 2 / minor;
                    var n = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    int k = i * minor + j; vertices[k] = new Vector3(Mathf.Cos(a) * (radius + tube), 0, Mathf.Sin(a) * (radius + tube)) + n * tube;
                    normals[k] = n;
                    int next = (i + 1) % major * minor + j, nj = i * minor + (j + 1) % minor, both = (i + 1) % major * minor + (j + 1) % minor;
                    int t = k * 6; triangles[t] = k; triangles[t + 1] = nj; triangles[t + 2] = next;
                    triangles[t + 3] = nj; triangles[t + 4] = both; triangles[t + 5] = next;
                }
            var mesh = new Mesh { name = "Original procedural ring", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds(); return mesh;
        }
    }
    public sealed class StallSlotMarker : MonoBehaviour { public int Index; }
}
