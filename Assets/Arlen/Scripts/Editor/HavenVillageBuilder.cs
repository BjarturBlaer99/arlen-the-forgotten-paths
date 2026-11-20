using System;
using System.Collections.Generic;
using Arlen.AI;
using Arlen.DialogueSystem;
using Arlen.Interaction;
using Arlen.Items;
using Arlen.NPC;
using Arlen.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static Arlen.Editor.PrototypeSceneBuilder;
using Object = UnityEngine.Object;

namespace Arlen.Editor
{
    /// <summary>
    /// Milestone 3: generates the Haven Village greybox — the hero's cabin (enterable, furnished,
    /// interactive), the village square with bakery, smithy and cottages, two NPCs with dialogue, and a
    /// forest path north into Mosswood where the road has vanished.
    ///
    /// Everything reusable is saved as a prefab (Prefabs/Environment, Prefabs/Characters) so placeholder
    /// geometry can be swapped for Blender assets without touching the scene layout. Existing prefabs and
    /// dialogue assets are reused (never overwritten) unless rebuilt explicitly from the menu.
    /// </summary>
    public static class HavenVillageBuilder
    {
        public const string ScenePath = Root + "/Scenes/Haven_Village.unity";
        const string NavMeshPath = Root + "/Scenes/Haven_Village_NavMesh.asset";
        const string EnvPrefabs = Root + "/Prefabs/Environment";
        const string CharPrefabs = Root + "/Prefabs/Characters";
        const string DialogueFolder = Root + "/Data/Dialogue";

        const float CabinZ = -34f;
        const float StreamZ = 24f;
        static readonly Vector3 ShrinePos = new Vector3(18f, 0f, 80f);
        static readonly Vector3 ShrinePathStart = new Vector3(1.5f, 0f, 73f);
        static readonly Vector3 ShrinePathEnd = new Vector3(13f, 0f, 79f);

        static RpgContentBuilder.Content items;
        static readonly List<Renderer> RoadShimmer = new List<Renderer>();

        static bool rebuildPrefabs;

        // ───────────────────────────── Menu ─────────────────────────────

        [MenuItem("Tools/Arlen/Create Haven Village Scene", priority = 20)]
        public static void CreateVillageMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetExistsAt(ScenePath) && !EditorUtility.DisplayDialog("Arlen",
                    $"'{ScenePath}' already exists.\n\nOverwrite it with a freshly generated scene? Manual edits to the scene will be lost (prefabs and dialogue assets are kept).",
                    "Overwrite", "Cancel"))
                return;
            Build(rebuild: false);
        }

        [MenuItem("Tools/Arlen/Rebuild Village Prefabs", priority = 21)]
        public static void RebuildPrefabsMenu()
        {
            if (!EditorUtility.DisplayDialog("Arlen",
                    "Regenerate all village building, prop and NPC prefabs? Manual edits to those prefabs will be lost. Dialogue assets are kept.\n\nThe Haven Village scene is regenerated afterwards.",
                    "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(rebuild: true);
        }

        [MenuItem("Tools/Arlen/Open Haven Village", priority = 22)]
        public static void OpenVillage()
        {
            if (!AssetExistsAt(ScenePath))
            {
                EditorUtility.DisplayDialog("Arlen", "Haven Village doesn't exist yet. Use Tools > Arlen > Create Haven Village Scene.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>
        /// Unity -batchmode -projectPath . -executeMethod Arlen.Editor.HavenVillageBuilder.BuildFromCommandLine -quit
        /// Pass -arlenOverwrite to replace an existing scene, -arlenRebuildPrefabs to regenerate prefabs.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            bool overwrite = Array.IndexOf(args, "-arlenOverwrite") >= 0;
            if (AssetExistsAt(ScenePath) && !overwrite)
            {
                Debug.LogWarning($"[Arlen] {ScenePath} already exists; not overwriting (pass -arlenOverwrite to force).");
                return;
            }
            Build(Array.IndexOf(args, "-arlenRebuildPrefabs") >= 0);
        }

        static bool AssetExistsAt(string path) => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));

        // ───────────────────────────── Scene ─────────────────────────────

        static void Build(bool rebuild)
        {
            rebuildPrefabs = rebuild;
            BuiltThisRun.Clear();
            MaterialCache.Clear();
            RoadShimmer.Clear();
            items = RpgContentBuilder.LoadItems(forceReload: true);
            var roadQuest = RpgContentBuilder.LoadRoadQuest();
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(EnvPrefabs);
            EnsureFolder(DialogueFolder);

            GameObject playerPrefab = PreparePlayerPrefab();
            GameObject boarPrefab = AssetExistsAt(BoarPrefabPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(BoarPrefabPath) : BuildBoarPrefab();
            LoadOrCreateBoarStats(); // fills in the quest id on existing stats assets
            var mabel = BuildMabelPrefab();
            var bram = BuildBramPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 170f;

            var environment = new GameObject("Environment").transform;
            BuildTerrain(environment);
            BuildCabinArea(environment);
            BuildVillage(environment);
            BuildStreamAndBridge(environment);
            BuildForest(environment);

            var spawn = new GameObject("PlayerSpawn").transform;
            // On the cabin porch, facing the village.
            spawn.SetPositionAndRotation(new Vector3(0f, 0.4f, CabinZ + 4.6f), Quaternion.identity);
            var hud = BuildPlayerRig(scene, playerPrefab, spawn);
            hud.Quests.Configure(new[] { roadQuest }, new[] { roadQuest }, 2.5f);

            BakeNavMesh(environment, NavMeshPath);
            PlaceVillagers(scene, mabel, bram);
            PlaceForestEnemy(scene, boarPrefab);
            BuildAreas(hud);
            PlaceWaybell(environment);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath); // inserted first: Haven Village is the default start scene
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arlen] Created {ScenePath}. Press Play to explore.");
        }

        static void BuildTerrain(Transform env)
        {
            var grass = Mat("Grass", new Color(0.42f, 0.62f, 0.30f));
            Prim(PrimitiveType.Cube, "Ground", env, new Vector3(0f, -0.5f, 20f), new Vector3(140f, 1f, 180f), grass, collider: true);

            // Invisible boundary; the tree ring outside it sells the edge visually.
            var bounds = new GameObject("Boundary").transform;
            bounds.SetParent(env);
            void Wall(string n, Vector3 p, Vector3 s)
            {
                var w = Prim(PrimitiveType.Cube, n, bounds, p, s, grass, collider: true);
                Object.DestroyImmediate(w.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(w.GetComponent<MeshFilter>());
            }
            Wall("Bound_W", new Vector3(-58f, 3f, 20f), new Vector3(1f, 6f, 170f));
            Wall("Bound_E", new Vector3(58f, 3f, 20f), new Vector3(1f, 6f, 170f));
            Wall("Bound_S", new Vector3(0f, 3f, -58f), new Vector3(118f, 6f, 1f));
            Wall("Bound_N", new Vector3(0f, 3f, 100f), new Vector3(118f, 6f, 1f));

            // Rolling hills at the edges (decorative, walkable bumps).
            var hill = Mat("GrassHill", new Color(0.38f, 0.58f, 0.28f));
            var rng = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var p = new Vector3(side * (48f + (float)rng.NextDouble() * 10f), -4f, -50f + i * 11f);
                float s = 14f + (float)rng.NextDouble() * 10f;
                Prim(PrimitiveType.Sphere, $"Hill_{i}", env, p, new Vector3(s, 10f, s * 1.3f), hill, collider: true);
            }
        }

        // ───────────────────────────── Cabin ─────────────────────────────

        static void BuildCabinArea(Transform env)
        {
            var cabin = Place(GetPrefab("Building_Cabin", BuildCabin), env, new Vector3(0f, 0f, CabinZ), 0f);
            ConfigureCabinStorage(cabin);
            var dirt = Mat("Dirt", new Color(0.66f, 0.55f, 0.38f));
            Prim(PrimitiveType.Cube, "Path_CabinToSquare", env, new Vector3(0f, 0.01f, -18f), new Vector3(3f, 0.02f, 22f), dirt, collider: false);

            // Garden & fence east of the cabin.
            var garden = new GameObject("CabinGarden").transform;
            garden.SetParent(env);
            garden.position = new Vector3(8f, 0f, CabinZ + 1f);
            var soil = Mat("Soil", new Color(0.36f, 0.25f, 0.17f));
            var leaf = Mat("Leaves_B", new Color(0.45f, 0.65f, 0.25f));
            var carrot = Mat("Flower_Orange", new Color(0.95f, 0.55f, 0.2f));
            for (int row = 0; row < 3; row++)
            {
                Prim(PrimitiveType.Cube, $"Bed_{row}", garden, new Vector3(0f, 0.12f, -2f + row * 2f), new Vector3(4.5f, 0.24f, 1.2f), soil, collider: false);
                for (int i = 0; i < 6; i++)
                    Prim(PrimitiveType.Sphere, $"Plant_{row}_{i}", garden, new Vector3(-1.9f + i * 0.75f, 0.35f, -2f + row * 2f), Vector3.one * 0.35f, row == 1 ? carrot : leaf, collider: false);
            }
            var fence = GetPrefab("Prop_FenceSegment", BuildFenceSegment);
            FenceLine(fence, garden, new Vector3(-3f, 0f, -3.5f), new Vector3(3f, 0f, -3.5f));
            FenceLine(fence, garden, new Vector3(3f, 0f, -3.5f), new Vector3(3f, 0f, 3.5f));
            FenceLine(fence, garden, new Vector3(-3f, 0f, 3.5f), new Vector3(3f, 0f, 3.5f));

            // Woodpile & lantern by the path.
            Place(GetPrefab("Prop_Lantern", BuildLantern), env, new Vector3(2.2f, 0f, CabinZ + 6f), 0f);
            var wood = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            for (int i = 0; i < 6; i++)
                Prim(PrimitiveType.Cylinder, $"Log_{i}", env, new Vector3(-5.3f, 0.2f + (i / 3) * 0.36f, CabinZ - 1f + (i % 3) * 0.4f - (i / 3) * 0.2f),
                    new Vector3(0.36f, 0.6f, 0.36f), wood, collider: i < 3, euler: new Vector3(0f, 0f, 90f));
        }

        /// <summary>
        /// Milestone 4 cabin wiring, done as overrides on the cabin instance so a hand-edited cabin prefab is
        /// kept: the chest gets real storage and the spare sword on the rack becomes a pickup.
        /// </summary>
        static void ConfigureCabinStorage(GameObject cabin)
        {
            var chest = cabin.GetComponentInChildren<StorageChest>();
            if (chest != null)
            {
                chest.ConfigureStorage("cabin_chest", "Cabin Chest",
                    new ItemAmount(items.TrailRation, 3), new ItemAmount(items.Whetstone, 1),
                    new ItemAmount(items.Rope, 1), new ItemAmount(items.OldCoins, 12));
                PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
            }
            else Debug.LogWarning("[Arlen] Cabin has no StorageChest; the chest won't store items.");

            var sword = cabin.transform.Find("Interior/EquipmentRack/SpareSword");
            if (sword != null)
            {
                var pickup = sword.gameObject.AddComponent<ItemPickup>();
                pickup.Configure("cabin_rack_sabre", items.PathwardenSabre, 1, null,
                    "You lift Eldon's old sabre from the rack. It still balances perfectly in your paw.",
                    "Equip it from your pack (Tab) whenever you're ready.");
                AddTrigger(sword.gameObject, new Vector3(0.6f, 1.4f, 0.6f));
            }
            else Debug.LogWarning("[Arlen] Cabin rack has no SpareSword; the Pathwarden Sabre pickup was skipped.");
        }

        static void AddTrigger(GameObject go, Vector3 worldSize)
        {
            var trig = go.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            var s = go.transform.lossyScale;
            trig.size = new Vector3(worldSize.x / Mathf.Max(0.01f, Mathf.Abs(s.x)), worldSize.y / Mathf.Max(0.01f, Mathf.Abs(s.y)), worldSize.z / Mathf.Max(0.01f, Mathf.Abs(s.z)));
            trig.center = Vector3.zero;
        }

        /// <summary>The hero's cabin: enterable, with furniture and interactions. Origin = floor centre, door faces +Z.</summary>
        static GameObject BuildCabin()
        {
            var root = new GameObject("Building_Cabin");
            var log = Mat("Cabin_Logs", new Color(0.55f, 0.37f, 0.22f));
            var logDark = Mat("Cabin_Beams", new Color(0.33f, 0.21f, 0.12f));
            var stone = Mat("Stone", new Color(0.55f, 0.55f, 0.52f));
            var floor = Mat("Cabin_Floor", new Color(0.62f, 0.45f, 0.28f));
            var moss = Mat("Roof_Moss", new Color(0.36f, 0.48f, 0.25f));
            var t = root.transform;

            const float w = 8f, d = 6f, h = 3.2f, th = 0.25f, floorTop = 0.32f;
            // Foundation, floor, porch
            Prim(PrimitiveType.Cube, "Foundation", t, new Vector3(0f, 0.15f, 0f), new Vector3(w + 0.3f, 0.3f, d + 0.3f), stone, true);
            Prim(PrimitiveType.Cube, "Floor", t, new Vector3(0f, floorTop - 0.05f, 0f), new Vector3(w - 0.2f, 0.1f, d - 0.2f), floor, true);
            Prim(PrimitiveType.Cube, "Porch", t, new Vector3(0f, floorTop - 0.1f, d * 0.5f + 1f), new Vector3(4f, 0.2f, 2f), floor, true);
            Prim(PrimitiveType.Cube, "PorchStep", t, new Vector3(0f, 0.08f, d * 0.5f + 2.3f), new Vector3(2f, 0.16f, 0.6f), stone, true);
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cylinder, $"PorchPost_{s}", t, new Vector3(1.8f * s, 1.5f, d * 0.5f + 1.85f), new Vector3(0.18f, 1.25f, 0.18f), logDark, true);
            var awning = Prim(PrimitiveType.Cube, "PorchAwning", t, new Vector3(0f, 2.85f, d * 0.5f + 1.1f), new Vector3(4.4f, 0.12f, 2.3f), moss, false, new Vector3(-12f, 0f, 0f));

            // Walls (log colour) with a doorway in the front wall.
            float wy = h * 0.5f;
            Prim(PrimitiveType.Cube, "Wall_Back", t, new Vector3(0f, wy, -d * 0.5f + th * 0.5f), new Vector3(w, h, th), log, true);
            Prim(PrimitiveType.Cube, "Wall_West", t, new Vector3(-w * 0.5f + th * 0.5f, wy, 0f), new Vector3(th, h, d), log, true);
            Prim(PrimitiveType.Cube, "Wall_East", t, new Vector3(w * 0.5f - th * 0.5f, wy, 0f), new Vector3(th, h, d), log, true);
            const float doorW = 1.4f, doorH = 2.4f;
            float seg = (w - doorW) * 0.5f;
            Prim(PrimitiveType.Cube, "Wall_FrontL", t, new Vector3(-(doorW * 0.5f + seg * 0.5f), wy, d * 0.5f - th * 0.5f), new Vector3(seg, h, th), log, true);
            Prim(PrimitiveType.Cube, "Wall_FrontR", t, new Vector3(doorW * 0.5f + seg * 0.5f, wy, d * 0.5f - th * 0.5f), new Vector3(seg, h, th), log, true);
            Prim(PrimitiveType.Cube, "Wall_Lintel", t, new Vector3(0f, (doorH + floorTop + h) * 0.5f, d * 0.5f - th * 0.5f), new Vector3(doorW, h - doorH - floorTop, th), log, true);

            // Log-cabin banding & corner posts
            for (int i = 1; i < 6; i++)
            {
                float y = i * 0.55f;
                Prim(PrimitiveType.Cube, $"Band_B{i}", t, new Vector3(0f, y, -d * 0.5f - 0.01f), new Vector3(w, 0.05f, 0.05f), logDark, false);
                Prim(PrimitiveType.Cube, $"Band_W{i}", t, new Vector3(-w * 0.5f - 0.01f, y, 0f), new Vector3(0.05f, 0.05f, d), logDark, false);
                Prim(PrimitiveType.Cube, $"Band_E{i}", t, new Vector3(w * 0.5f + 0.01f, y, 0f), new Vector3(0.05f, 0.05f, d), logDark, false);
            }
            for (int cx = -1; cx <= 1; cx += 2)
            for (int cz = -1; cz <= 1; cz += 2)
                Prim(PrimitiveType.Cube, $"Corner_{cx}_{cz}", t, new Vector3(cx * w * 0.5f, h * 0.5f, cz * d * 0.5f), new Vector3(0.32f, h + 0.1f, 0.32f), logDark, false);

            var roofRenderers = BuildGableRoof(t, w, d, h, moss);
            roofRenderers.Add(awning.GetComponent<Renderer>()); // also blocks the camera's view of the room

            // Chimney (west wall, rises through the roof) doubling as the fireplace inside.
            Prim(PrimitiveType.Cube, "Chimney", t, new Vector3(-w * 0.5f + 0.55f, 3.6f, -0.9f), new Vector3(1.1f, 7.2f, 1.2f), stone, true);
            var embers = Prim(PrimitiveType.Cube, "Hearth_Embers", t, new Vector3(-w * 0.5f + 1.12f, 0.75f, -0.9f), new Vector3(0.06f, 0.55f, 0.8f),
                Mat("FX_Embers", new Color(1f, 0.45f, 0.1f), emission: new Color(2.2f, 0.7f, 0.1f)), false);
            embers.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            AddExaminable(embers, "Examine", new Vector3(0.5f, 1.2f, 1.2f),
                "The embers are still warm. Eldon must have banked the fire for you.",
                "Home. It smells of woodsmoke and old maps.");

            // Windows: warm glow outside, framed inside.
            Window(t, "Window_East", new Vector3(w * 0.5f + 0.02f, 1.7f, -0.6f), 90f, 1.2f, 1f);
            Window(t, "Window_West", new Vector3(-w * 0.5f - 0.02f, 1.7f, 1.4f), -90f, 1f, 0.9f);
            Window(t, "Window_FrontL", new Vector3(-2.5f, 1.7f, d * 0.5f + 0.02f), 0f, 1.1f, 0.9f);
            var backWindow = Window(t, "Window_Back", new Vector3(0.6f, 1.75f, -d * 0.5f - 0.02f), 180f, 1.4f, 1f);
            var inside = Window(t, "Window_BackInside", new Vector3(0.6f, 1.75f, -d * 0.5f + th + 0.02f), 0f, 1.4f, 1f);
            AddExaminable(inside, "Look outside", new Vector3(1.6f, 1.2f, 1.5f),
                "Through the window, Mosswood sways in the breeze.",
                "Something about the treeline feels wrong tonight, like a word on the tip of your tongue.");
            _ = backWindow;

            // Door (opens), with a lantern beside it.
            BuildDoor(t, "FrontDoor", new Vector3(0f, floorTop, d * 0.5f - th * 0.5f), doorW, doorH - 0.05f, Mat("Door_Wood", new Color(0.45f, 0.28f, 0.16f)),
                locked: false, lockedMessage: null, solidLeaf: true);
            var lamp = Prim(PrimitiveType.Cube, "DoorLantern", t, new Vector3(1.05f, 2.2f, d * 0.5f + 0.15f), new Vector3(0.18f, 0.26f, 0.18f),
                Mat("Lantern_Glow", new Color(1f, 0.85f, 0.5f), emission: new Color(2f, 1.4f, 0.6f)), false);
            lamp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            BuildCabinInterior(t, w, d, floorTop);

            // Hide the roof while the player is inside so the camera can see the room.
            var zone = new GameObject("InteriorZone").AddComponent<InteriorZone>();
            zone.transform.SetParent(t, false);
            zone.SetSize(new Vector3(w - 0.4f, h, d - 0.4f));
            zone.SetRenderers(roofRenderers.ToArray());
            return root;
        }

        static void BuildCabinInterior(Transform t, float w, float d, float floorTop)
        {
            var wood = Mat("Furniture_Wood", new Color(0.5f, 0.33f, 0.2f));
            var woodDark = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            var cloth = Mat("Cloth_Teal", new Color(0.18f, 0.5f, 0.5f));
            var linen = Mat("Cloth_Linen", new Color(0.9f, 0.86f, 0.76f));
            var rug = Mat("Cloth_Rug", new Color(0.62f, 0.22f, 0.2f));
            var parchment = Mat("Parchment", new Color(0.92f, 0.84f, 0.62f));
            var steel = Mat("Sword_Steel", new Color(0.82f, 0.85f, 0.9f));
            var iron = Mat("Club_Iron", new Color(0.35f, 0.35f, 0.38f));
            float y0 = floorTop;
            var interior = new GameObject("Interior").transform;
            interior.SetParent(t, false);

            Prim(PrimitiveType.Cylinder, "Rug", interior, new Vector3(0.2f, y0 + 0.01f, 0f), new Vector3(3f, 0.01f, 2.2f), rug, false);

            // Bed (south-east): rest to recover.
            var bed = new GameObject("Bed").transform;
            bed.SetParent(interior, false);
            bed.localPosition = new Vector3(w * 0.5f - 1.05f, y0, -d * 0.5f + 1.35f);
            Prim(PrimitiveType.Cube, "Frame", bed, new Vector3(0f, 0.22f, 0f), new Vector3(1.4f, 0.44f, 2.2f), wood, true);
            Prim(PrimitiveType.Cube, "Mattress", bed, new Vector3(0f, 0.52f, 0f), new Vector3(1.3f, 0.18f, 2.05f), linen, false);
            Prim(PrimitiveType.Cube, "Blanket", bed, new Vector3(0f, 0.6f, 0.25f), new Vector3(1.34f, 0.08f, 1.4f), cloth, false);
            Prim(PrimitiveType.Cube, "Pillow", bed, new Vector3(0f, 0.66f, -0.75f), new Vector3(0.9f, 0.14f, 0.4f), linen, false);
            Prim(PrimitiveType.Cube, "Headboard", bed, new Vector3(0f, 0.6f, -1.08f), new Vector3(1.4f, 1.2f, 0.08f), woodDark, true);
            bed.gameObject.AddComponent<RestSpot>();

            // Desk + chair + map of Arlen (east wall).
            var desk = new GameObject("Desk").transform;
            desk.SetParent(interior, false);
            desk.localPosition = new Vector3(w * 0.5f - 0.75f, y0, 1.1f);
            Prim(PrimitiveType.Cube, "Top", desk, new Vector3(0f, 0.78f, 0f), new Vector3(0.9f, 0.06f, 1.7f), wood, true);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, $"Leg_{i}", desk, new Vector3(i % 2 == 0 ? -0.38f : 0.38f, 0.38f, i < 2 ? -0.78f : 0.78f), new Vector3(0.07f, 0.76f, 0.07f), woodDark, false);
            Prim(PrimitiveType.Cube, "Papers", desk, new Vector3(0f, 0.82f, 0.2f), new Vector3(0.4f, 0.01f, 0.55f), parchment, false, new Vector3(0f, 12f, 0f));
            Prim(PrimitiveType.Cylinder, "InkPot", desk, new Vector3(0.1f, 0.86f, -0.45f), new Vector3(0.08f, 0.05f, 0.08f), Mat("Ink_Blue", new Color(0.15f, 0.25f, 0.7f)), false);
            Prim(PrimitiveType.Cube, "Chair", desk, new Vector3(-0.85f, 0.45f, 0f), new Vector3(0.5f, 0.08f, 0.5f), wood, true);
            AddExaminable(desk.gameObject, "Examine", new Vector3(1f, 1.2f, 1.8f),
                "Half-finished letters, a brass compass, and a note in Eldon's tidy hand:",
                "\"Off to check something in the old survey records. Back before supper. Don't touch the blue ink.\"",
                "The note is dated three days ago.");

            var map = Prim(PrimitiveType.Cube, "MapOfArlen", interior, new Vector3(w * 0.5f - 0.27f, y0 + 1.75f, 1.1f), new Vector3(0.03f, 1.0f, 1.5f), parchment, false);
            Prim(PrimitiveType.Cube, "Map_Forest", map.transform, new Vector3(-0.6f, 0.15f, -0.15f), new Vector3(0.5f, 0.35f, 0.3f), Mat("Leaves_A", new Color(0.30f, 0.55f, 0.28f)), false);
            Prim(PrimitiveType.Cube, "Map_Mountains", map.transform, new Vector3(-0.6f, 0.25f, 0.25f), new Vector3(0.5f, 0.25f, 0.25f), Mat("Rock", new Color(0.5f, 0.5f, 0.48f)), false);
            Prim(PrimitiveType.Cube, "Map_Road", map.transform, new Vector3(-0.6f, -0.05f, 0f), new Vector3(0.5f, 0.6f, 0.02f), Mat("Map_Ink", new Color(0.5f, 0.35f, 0.25f)), false, new Vector3(0f, 0f, 0f));
            AddExaminable(map, "Read map", new Vector3(1.2f, 1.2f, 1.6f),
                "Eldon's great map of Arlen, annotated in his tiny, careful handwriting.",
                "Haven Village. Mosswood. The northern market. You inked the Mosswood Road yourself, years ago.",
                "Strange. The line of the road looks fainter than you remember. Almost as if it's fading.");

            // Bookshelf (west wall).
            var shelf = new GameObject("Bookshelf").transform;
            shelf.SetParent(interior, false);
            shelf.localPosition = new Vector3(-w * 0.5f + 0.5f, y0, 1.35f);
            Prim(PrimitiveType.Cube, "Case", shelf, new Vector3(0f, 1.1f, 0f), new Vector3(0.45f, 2.2f, 1.4f), woodDark, true);
            var bookColors = new[] { new Color(0.6f, 0.2f, 0.2f), new Color(0.2f, 0.35f, 0.6f), new Color(0.3f, 0.5f, 0.25f), new Color(0.75f, 0.6f, 0.3f) };
            for (int row = 0; row < 4; row++)
            for (int b = 0; b < 6; b++)
                Prim(PrimitiveType.Cube, $"Book_{row}_{b}", shelf, new Vector3(0.24f, 0.35f + row * 0.5f, -0.55f + b * 0.21f), new Vector3(0.04f, 0.34f, 0.17f),
                    Mat($"Book_{(row + b) % 4}", bookColors[(row + b) % 4]), false);
            AddExaminable(shelf.gameObject, "Browse", new Vector3(1.2f, 2f, 1.6f),
                "Travel journals, a field guide to Arlen's wildlife, and \"The Pathwardens: A History\".",
                "That last one is Eldon's favourite. Its spine is worn smooth from rereading.");

            // Kitchen corner (north-west).
            var kitchen = new GameObject("Kitchen").transform;
            kitchen.SetParent(interior, false);
            kitchen.localPosition = new Vector3(-w * 0.5f + 1.35f, y0, d * 0.5f - 0.6f);
            Prim(PrimitiveType.Cube, "Counter", kitchen, new Vector3(0f, 0.45f, 0f), new Vector3(1.9f, 0.9f, 0.6f), wood, true);
            Prim(PrimitiveType.Cylinder, "Pot", kitchen, new Vector3(-0.4f, 1.02f, 0f), new Vector3(0.36f, 0.14f, 0.36f), iron, false);
            Prim(PrimitiveType.Sphere, "Bread", kitchen, new Vector3(0.45f, 0.98f, 0f), new Vector3(0.35f, 0.16f, 0.22f), Mat("Bread", new Color(0.8f, 0.55f, 0.28f)), false);
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Sphere, $"Herbs_{i}", kitchen, new Vector3(-0.6f + i * 0.5f, 2.2f, 0.15f), new Vector3(0.18f, 0.3f, 0.18f), Mat("Leaves_A", Color.green), false);
            AddExaminable(kitchen.gameObject, "Examine", new Vector3(2f, 1.2f, 1.2f),
                "A loaf of Mabel's bread, a pot of yesterday's stew, and drying herbs from the garden.",
                "Eldon's cooking is legendary. Legendarily bad. The stew is yours.");

            // Storage chest (south wall).
            var chest = Place(GetPrefab("Prop_Chest", BuildChest), interior, new Vector3(-0.6f, y0, -d * 0.5f + 0.65f), 0f);
            _ = chest;

            // Equipment rack (south-west): spare weapons from past travels.
            var rack = new GameObject("EquipmentRack").transform;
            rack.SetParent(interior, false);
            rack.localPosition = new Vector3(-w * 0.5f + 1.7f, y0, -d * 0.5f + 0.45f);
            Prim(PrimitiveType.Cube, "Frame", rack, new Vector3(0f, 1f, 0f), new Vector3(1.6f, 0.08f, 0.3f), woodDark, true);
            Prim(PrimitiveType.Cube, "PostL", rack, new Vector3(-0.75f, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f), woodDark, false);
            Prim(PrimitiveType.Cube, "PostR", rack, new Vector3(0.75f, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f), woodDark, false);
            Prim(PrimitiveType.Cube, "SpareSword", rack, new Vector3(-0.35f, 1.15f, 0.08f), new Vector3(0.05f, 1.1f, 0.015f), steel, false, new Vector3(0f, 0f, 8f));
            Prim(PrimitiveType.Cylinder, "Buckler", rack, new Vector3(0.3f, 1.35f, 0.08f), new Vector3(0.6f, 0.03f, 0.6f), iron, false, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Bow", rack, new Vector3(0.6f, 1.05f, 0.1f), new Vector3(0.04f, 1.3f, 0.04f), wood, false, new Vector3(0f, 0f, -6f));
            AddExaminable(rack.gameObject, "Examine", new Vector3(1.8f, 2f, 1.2f),
                "Your spare sword, a dented buckler and a well-oiled bow.",
                "Each one has a story. Most of them end with you winning.");

            // Table & stool by the window.
            Prim(PrimitiveType.Cylinder, "Table", interior, new Vector3(0.9f, y0 + 0.7f, -0.6f), new Vector3(1f, 0.04f, 1f), wood, true);
            Prim(PrimitiveType.Cylinder, "TableLeg", interior, new Vector3(0.9f, y0 + 0.35f, -0.6f), new Vector3(0.12f, 0.35f, 0.12f), woodDark, false);
            Prim(PrimitiveType.Cylinder, "Candle", interior, new Vector3(0.9f, y0 + 0.82f, -0.6f), new Vector3(0.06f, 0.08f, 0.06f),
                Mat("Lantern_Glow", new Color(1f, 0.85f, 0.5f), emission: new Color(2f, 1.4f, 0.6f)), false);
        }

        // ───────────────────────────── Village ─────────────────────────────

        static void BuildVillage(Transform env)
        {
            var village = new GameObject("HavenVillage").transform;
            village.SetParent(env);
            var cobble = Mat("Cobblestone", new Color(0.62f, 0.6f, 0.55f));
            var dirt = Mat("Dirt", new Color(0.66f, 0.55f, 0.38f));

            Prim(PrimitiveType.Cylinder, "VillageSquare", village, new Vector3(0f, 0.01f, 0f), new Vector3(22f, 0.01f, 22f), cobble, false);
            Prim(PrimitiveType.Cube, "Path_West", village, new Vector3(-14f, 0.012f, 2f), new Vector3(10f, 0.02f, 3f), dirt, false);
            Prim(PrimitiveType.Cube, "Path_East", village, new Vector3(14f, 0.012f, 2f), new Vector3(10f, 0.02f, 3f), dirt, false);
            Prim(PrimitiveType.Cube, "Path_North", village, new Vector3(0f, 0.012f, 16f), new Vector3(3f, 0.02f, 12f), dirt, false);

            Place(GetPrefab("Prop_HavenOak", BuildHavenOak), village, Vector3.zero, 20f);
            Place(GetPrefab("Prop_Well", BuildWell), village, new Vector3(6.5f, 0f, -6f), 0f);

            var bench = GetPrefab("Prop_Bench", BuildBench);
            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Place(bench, village, dir * 5.2f, a + 180f);
            }
            var lantern = GetPrefab("Prop_Lantern", BuildLantern);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 30f;
                Place(lantern, village, Quaternion.Euler(0f, a, 0f) * Vector3.forward * 10.5f, a);
            }

            // Bakery (west) and smithy (east) face the square.
            var bakery = Place(GetPrefab("Building_Bakery", BuildBakery), village, new Vector3(-21f, 0f, 2f), 90f);
            var smithy = Place(GetPrefab("Building_Smithy", BuildSmithy), village, new Vector3(21f, 0f, 2f), -90f);
            _ = bakery; _ = smithy;

            // Cottages around the square. Eldon's house is the one with the cold teacup.
            var cottageA = GetPrefab("Building_Cottage_A", () => BuildCottage("Building_Cottage_A", 6f, 5f, 2.8f, new Color(0.93f, 0.88f, 0.76f), new Color(0.55f, 0.3f, 0.22f)));
            var cottageB = GetPrefab("Building_Cottage_B", () => BuildCottage("Building_Cottage_B", 7f, 5.5f, 3f, new Color(0.85f, 0.8f, 0.68f), new Color(0.36f, 0.48f, 0.25f)));
            var eldon = Place(cottageB, village, new Vector3(-17f, 0f, -14f), 90f);
            eldon.name = "Cottage_Eldon";
            SetDoorMessage(eldon, "Knock",
                "No answer. Eldon's door is locked, which it never is.\nA cold cup of tea sits on the windowsill.");
            var c2 = Place(cottageA, village, new Vector3(18f, 0f, -14f), -90f);
            SetDoorMessage(c2, "Knock", "No one's home. The Thistlewicks are probably at the mill.");
            var c3 = Place(cottageA, village, new Vector3(-12f, 0f, 15f), 160f);
            SetDoorMessage(c3, "Knock", "A sleepy voice calls: \"Come back after my nap!\"");
            var c4 = Place(cottageB, village, new Vector3(13f, 0f, 15f), 200f);
            SetDoorMessage(c4, "Knock", "Locked. A wreath of dried lavender hangs on the door.");

            // Props
            var barrel = GetPrefab("Prop_Barrel", BuildBarrel);
            var crate = GetPrefab("Prop_Crate", BuildCrate);
            Place(barrel, village, new Vector3(-15.5f, 0f, 6.2f), 0f);
            Place(barrel, village, new Vector3(-15.8f, 0f, 7.1f), 40f);
            Place(crate, village, new Vector3(16.5f, 0f, 6.8f), 15f);
            Place(crate, village, new Vector3(16.6f, 0.6f, 6.8f), 50f);
            Place(barrel, village, new Vector3(-7f, 0f, -10f), 0f);

            // Flower beds
            var flowerMats = new[]
            {
                Mat("Flower_Pink", new Color(0.95f, 0.55f, 0.7f)),
                Mat("Flower_Yellow", new Color(0.98f, 0.85f, 0.3f)),
                Mat("Flower_Blue", new Color(0.45f, 0.6f, 0.95f)),
            };
            var soil = Mat("Soil", new Color(0.36f, 0.25f, 0.17f));
            Vector3[] beds = { new Vector3(-6f, 0f, -9.5f), new Vector3(6f, 0f, 9.5f), new Vector3(-9.8f, 0f, 6f), new Vector3(9.8f, 0f, -5f) };
            for (int b = 0; b < beds.Length; b++)
            {
                Prim(PrimitiveType.Cube, $"FlowerBed_{b}", village, beds[b] + Vector3.up * 0.1f, new Vector3(2.4f, 0.2f, 1f), soil, true);
                for (int f = 0; f < 6; f++)
                    Prim(PrimitiveType.Sphere, $"Flower_{b}_{f}", village, beds[b] + new Vector3(-0.95f + f * 0.38f, 0.32f, (f % 2) * 0.3f - 0.15f), Vector3.one * 0.22f, flowerMats[(b + f) % 3], false);
            }

            // Trees around the village
            var rng = new System.Random(42);
            var trees = new[] { GetPrefab("Prop_Tree_Oak", BuildOakTree), GetPrefab("Prop_Tree_Birch", BuildBirchTree), GetPrefab("Prop_Tree_Pine", BuildPineTree) };
            int placed = 0;
            for (int i = 0; i < 400 && placed < 70; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 110 - 55), 0f, (float)(rng.NextDouble() * 75 - 55));
                if (!IsClearForTree(p)) continue;
                var tree = Place(trees[rng.Next(trees.Length)], village, p, (float)rng.NextDouble() * 360f);
                tree.transform.localScale = Vector3.one * (0.85f + (float)rng.NextDouble() * 0.5f);
                placed++;
            }
            var rock = GetPrefab("Prop_Rock", BuildRock);
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 100 - 50), 0f, (float)(rng.NextDouble() * 70 - 52));
                if (!IsClearForTree(p)) continue;
                var r = Place(rock, village, p, (float)rng.NextDouble() * 360f);
                r.transform.localScale = Vector3.one * (0.6f + (float)rng.NextDouble() * 1.2f);
            }
        }

        /// <summary>Keeps scattered trees off buildings, paths, the square and the stream.</summary>
        static bool IsClearForTree(Vector3 p)
        {
            if (new Vector2(p.x, p.z).magnitude < 15f) return false;                     // square + nearby houses
            if (Mathf.Abs(p.x) < 4f) return false;                                         // north-south path
            if (Mathf.Abs(p.z - 2f) < 7f && Mathf.Abs(p.x) < 30f) return false;            // east-west paths & shops
            if (Mathf.Abs(p.x) < 14f && p.z < CabinZ + 8f && p.z > CabinZ - 7f) return false; // cabin & garden
            if (Mathf.Abs(p.z - StreamZ) < 5f) return false;                               // stream banks
            if (Mathf.Abs(p.x) > 54f || p.z < -54f) return false;
            Vector3[] houses = { new Vector3(-17f, 0f, -14f), new Vector3(18f, 0f, -14f), new Vector3(-12f, 0f, 15f), new Vector3(13f, 0f, 15f) };
            foreach (var h in houses)
                if ((p - h).sqrMagnitude < 8f * 8f) return false;
            return true;
        }

        // ───────────────────────────── Stream, bridge, forest ─────────────────────────────

        static void BuildStreamAndBridge(Transform env)
        {
            var root = new GameObject("Stream").transform;
            root.SetParent(env);
            var water = Mat("Water", new Color(0.3f, 0.58f, 0.75f), emission: new Color(0.02f, 0.08f, 0.12f));
            Prim(PrimitiveType.Cube, "Water", root, new Vector3(0f, 0.02f, StreamZ), new Vector3(118f, 0.02f, 4f), water, false);
            var bank = Mat("Rock", new Color(0.5f, 0.5f, 0.48f));
            for (int i = 0; i < 30; i++)
            {
                float x = -56f + i * 3.9f;
                if (Mathf.Abs(x) < 3f) continue;
                Prim(PrimitiveType.Sphere, $"BankStone_N{i}", root, new Vector3(x, 0.05f, StreamZ + 2.1f), new Vector3(1.4f, 0.4f, 0.8f), bank, false);
                Prim(PrimitiveType.Sphere, $"BankStone_S{i}", root, new Vector3(x + 1.6f, 0.05f, StreamZ - 2.1f), new Vector3(1.3f, 0.4f, 0.8f), bank, false);
            }

            // Fences along both banks keep the player on the bridge (no swimming yet).
            var fence = GetPrefab("Prop_FenceSegment", BuildFenceSegment);
            foreach (float z in new[] { StreamZ - 2.6f, StreamZ + 2.6f })
            {
                FenceLine(fence, root, new Vector3(-57f, 0f, z), new Vector3(-2.2f, 0f, z));
                FenceLine(fence, root, new Vector3(2.2f, 0f, z), new Vector3(57f, 0f, z));
            }

            var wood = Mat("WoodPlanks", new Color(0.55f, 0.38f, 0.24f));
            var dark = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            var bridge = new GameObject("Bridge").transform;
            bridge.SetParent(root);
            bridge.position = new Vector3(0f, 0f, StreamZ);
            Prim(PrimitiveType.Cube, "Deck", bridge, new Vector3(0f, 0.14f, 0f), new Vector3(3.4f, 0.28f, 7f), wood, true);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cube, $"Rail_{s}", bridge, new Vector3(1.6f * s, 1.05f, 0f), new Vector3(0.12f, 0.1f, 7f), dark, true);
                for (int i = 0; i < 4; i++)
                    Prim(PrimitiveType.Cube, $"RailPost_{s}_{i}", bridge, new Vector3(1.6f * s, 0.6f, -3.3f + i * 2.2f), new Vector3(0.14f, 0.9f, 0.14f), dark, true);
            }
        }

        static void BuildForest(Transform env)
        {
            var forest = new GameObject("Mosswood").transform;
            forest.SetParent(env);
            var dirt = Mat("Dirt", new Color(0.66f, 0.55f, 0.38f));
            var mossFloor = Mat("Moss_Floor", new Color(0.3f, 0.47f, 0.25f));
            Prim(PrimitiveType.Cube, "Path_Forest", forest, new Vector3(0f, 0.012f, 56f), new Vector3(3f, 0.02f, 60f), dirt, false);
            Prim(PrimitiveType.Cube, "ForestFloor", forest, new Vector3(0f, 0.005f, 72f), new Vector3(112f, 0.01f, 54f), mossFloor, false);
            Prim(PrimitiveType.Cylinder, "Clearing", forest, new Vector3(-16f, 0.008f, 68f), new Vector3(18f, 0.01f, 18f), Mat("Grass", Color.green), false);
            Prim(PrimitiveType.Cube, "Path_Clearing", forest, new Vector3(-7f, 0.012f, 66f), new Vector3(12f, 0.02f, 2.4f), dirt, false, new Vector3(0f, 12f, 0f));
            // Faint side trail east to the Waybell shrine.
            Vector3 trail = ShrinePathEnd - ShrinePathStart;
            Prim(PrimitiveType.Cube, "Path_Shrine", forest, (ShrinePathStart + ShrinePathEnd) * 0.5f + Vector3.up * 0.011f, new Vector3(trail.magnitude, 0.02f, 2f), Mat("Dirt_Faint", new Color(0.56f, 0.5f, 0.36f)), false,
                new Vector3(0f, -Mathf.Atan2(trail.z, trail.x) * Mathf.Rad2Deg, 0f));
            Prim(PrimitiveType.Cylinder, "ShrineClearing", forest, ShrinePos + Vector3.up * 0.007f, new Vector3(18f, 0.01f, 18f), Mat("Grass", Color.green), false);

            // Signpost at the forest edge.
            var sign = new GameObject("Signpost_Mosswood").transform;
            sign.SetParent(forest);
            sign.position = new Vector3(2.6f, 0f, 38f);
            var dark = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            var plank = Mat("WoodPlanks", new Color(0.55f, 0.38f, 0.24f));
            Prim(PrimitiveType.Cube, "Post", sign, new Vector3(0f, 1.1f, 0f), new Vector3(0.15f, 2.2f, 0.15f), dark, true);
            Prim(PrimitiveType.Cube, "Board_North", sign, new Vector3(0.45f, 1.85f, 0f), new Vector3(1f, 0.3f, 0.06f), plank, false, new Vector3(0f, 0f, 6f));
            Prim(PrimitiveType.Cube, "Board_South", sign, new Vector3(-0.45f, 1.45f, 0f), new Vector3(1f, 0.3f, 0.06f), plank, false, new Vector3(0f, 0f, -4f));
            AddExaminable(sign.gameObject, "Read sign", new Vector3(1.5f, 2.4f, 1f),
                "MOSSWOOD. Haven Village, one mile south.",
                "Beneath it, an older carving: \"Market Road, north\". Half the letters have faded to nothing, as if the wood forgot them.");

            // Dense trees on both sides of the path.
            var rng = new System.Random(99);
            var trees = new[] { GetPrefab("Prop_Tree_Mossy", BuildMossyTree), GetPrefab("Prop_Tree_Pine", BuildPineTree), GetPrefab("Prop_Tree_Oak", BuildOakTree) };
            int placed = 0;
            for (int i = 0; i < 900 && placed < 150; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 110 - 55), 0f, 30f + (float)rng.NextDouble() * 68f);
                if (Mathf.Abs(p.x) < 3.2f && p.z < 90f) continue;                       // the path
                if ((p - new Vector3(-16f, 0f, 68f)).sqrMagnitude < 10f * 10f) continue; // clearing
                if (p.x < -1f && p.x > -12f && Mathf.Abs(p.z - (66f + (p.x + 7f) * 0.2f)) < 2.2f) continue; // side path
                if (p.z < 34f && Mathf.Abs(p.x) < 10f) continue;                         // keep the entrance open
                if ((p - new Vector3(2.6f, 0f, 38f)).sqrMagnitude < 4f) continue;
                if (IsNearShrine(p)) continue;
                var t = Place(trees[placed % 3 == 0 ? 1 : rng.Next(trees.Length)], forest, p, (float)rng.NextDouble() * 360f);
                t.transform.localScale = Vector3.one * (1f + (float)rng.NextDouble() * 0.6f);
                placed++;
            }

            var rock = GetPrefab("Prop_Rock", BuildRock);
            for (int i = 0; i < 18; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 100 - 50), 0f, 34f + (float)rng.NextDouble() * 60f);
                if (Mathf.Abs(p.x) < 4f || IsNearShrine(p)) continue;
                var r = Place(rock, forest, p, (float)rng.NextDouble() * 360f);
                r.transform.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 1.6f);
            }

            // Where the road vanished: a wall of brambles and a faint violet shimmer.
            var end = new GameObject("VanishedRoad").transform;
            end.SetParent(forest);
            end.position = new Vector3(0f, 0f, 90f);
            var bramble = Mat("Bramble", new Color(0.18f, 0.3f, 0.16f));
            var shimmer = Mat("Waymark_Shimmer", new Color(0.55f, 0.4f, 0.85f), emission: new Color(0.5f, 0.25f, 0.9f));
            for (int i = 0; i < 9; i++)
                Prim(PrimitiveType.Sphere, $"Bramble_{i}", end, new Vector3(-4.5f + i * 1.15f, 1.2f + (i % 2) * 0.4f, 1.5f + (i % 3) * 0.6f), new Vector3(2.4f, 2.6f, 2.2f), bramble, true);
            for (int i = 0; i < 7; i++)
            {
                var mote = Prim(PrimitiveType.Sphere, $"Shimmer_{i}", end, new Vector3(-2.4f + i * 0.8f, 0.6f + (i % 3) * 0.7f, 0.2f), Vector3.one * 0.12f, shimmer, false);
                mote.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                RoadShimmer.Add(mote.GetComponent<Renderer>());
            }
            // Road fragments that fade into nothing
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, $"RoadStone_{i}", end, new Vector3(((i % 2) - 0.5f) * 1.4f, 0.03f, -2f + i * 0.9f), new Vector3(0.9f - i * 0.15f, 0.06f, 0.7f - i * 0.12f),
                    Mat("Cobblestone", Color.gray), false, new Vector3(0f, i * 17f, 0f));
            var roadEnd = AddExaminable(end.gameObject, "Examine", new Vector3(8f, 3f, 2f),
                "The road should continue here. You walked it to the northern market only last spring.",
                "Now there is only thick bramble, as if the path had never existed. Old paving stones simply... stop.",
                "The air hums faintly, like a bell rung very far away. The sound seems to come from somewhere east.");
            roadEnd.SetId(RpgContentBuilder.VanishedRoadId);

            // Mossy milestone partway along the path.
            var stone = Prim(PrimitiveType.Cube, "Milestone", forest, new Vector3(-2.4f, 0.5f, 62f), new Vector3(0.5f, 1f, 0.35f), Mat("Stone", Color.gray), true, new Vector3(0f, 15f, -4f));
            Prim(PrimitiveType.Cube, "MilestoneMoss", stone.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.08f, 0.15f, 1.08f), Mat("Roof_Moss", Color.green), false);
            AddExaminable(stone, "Examine", new Vector3(1.5f, 2f, 1.5f),
                "An old Pathwarden milestone. The carved arrow points north, toward the brambles.",
                "\"Northmarket, 9 miles.\" Nobody has cleared the moss off it in years.");

            // Things to find along the way.
            var herb = GetPrefab("Pickup_Sunmoss", BuildSunmossPickup);
            PlacePickup(herb, forest, "sunmoss_forest_entrance", items.Sunmoss, 2, new Vector3(4.6f, 0f, 33f));
            PlacePickup(herb, forest, "sunmoss_road_milestone", items.Sunmoss, 1, new Vector3(-2.3f, 0f, 56.5f));
            PlacePickup(herb, forest, "sunmoss_shrine", items.Sunmoss, 2, ShrinePos + new Vector3(-4f, 0f, 5.5f));
            PlacePickup(GetPrefab("Pickup_Token", BuildTokenPickup), forest, "pathwarden_token_milestone", items.PathwardenToken, 1, new Vector3(-1.7f, 0f, 61.2f));
        }

        static bool IsNearShrine(Vector3 p)
        {
            if ((p - ShrinePos).sqrMagnitude < 11f * 11f) return true;
            Vector3 ab = ShrinePathEnd - ShrinePathStart;
            float t = Mathf.Clamp01(Vector3.Dot(p - ShrinePathStart, ab) / ab.sqrMagnitude);
            Vector3 closest = ShrinePathStart + ab * t;
            closest.y = p.y;
            return (p - closest).sqrMagnitude < 2.6f * 2.6f;
        }

        static void PlacePickup(GameObject prefab, Transform parent, string id, ItemDefinition item, int count, Vector3 position)
        {
            var go = Place(prefab, parent, Vector3.zero, 0f);
            go.transform.position = position;
            go.name = $"Pickup_{id}";
            var pickup = go.GetComponent<ItemPickup>();
            pickup.Configure(id, item, count);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pickup);
        }

        static GameObject BuildSunmossPickup()
        {
            var root = new GameObject("Pickup_Sunmoss");
            var moss = Mat("Pickup_Sunmoss", new Color(0.9f, 0.8f, 0.3f), emission: new Color(0.35f, 0.28f, 0.04f));
            var leaf = Mat("Leaves_B", new Color(0.45f, 0.65f, 0.25f));
            Prim(PrimitiveType.Sphere, "Base", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.5f, 0.12f, 0.5f), leaf, false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                Prim(PrimitiveType.Sphere, $"Tuft_{i}", root.transform, new Vector3(Mathf.Cos(a) * 0.14f, 0.16f + (i % 2) * 0.05f, Mathf.Sin(a) * 0.14f), Vector3.one * 0.18f, moss, false);
            }
            root.AddComponent<ItemPickup>();
            var trig = root.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 0.6f;
            trig.center = new Vector3(0f, 0.3f, 0f);
            return root;
        }

        static GameObject BuildTokenPickup()
        {
            var root = new GameObject("Pickup_Token");
            var brass = Mat("Pickup_Brass", new Color(0.85f, 0.68f, 0.3f), emission: new Color(0.4f, 0.28f, 0.05f));
            Prim(PrimitiveType.Cylinder, "Token", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.22f, 0.02f, 0.22f), brass, false, new Vector3(8f, 0f, 14f));
            Prim(PrimitiveType.Cube, "Glint", root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.05f, 0.01f, 0.16f), Mat("Waybell_Light", new Color(0.6f, 1f, 0.95f), emission: new Color(0.6f, 1.6f, 1.5f)), false, new Vector3(0f, 45f, 0f));
            root.AddComponent<ItemPickup>();
            var trig = root.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 0.6f;
            trig.center = new Vector3(0f, 0.3f, 0f);
            return root;
        }

        // ───────────────────────────── Waybell shrine ─────────────────────────────

        static void PlaceWaybell(Transform env)
        {
            var forest = env.Find("Mosswood");
            var shrine = Place(GetPrefab("Building_WaybellShrine", BuildWaybellShrine), forest != null ? forest : env, Vector3.zero, 0f);
            shrine.transform.position = ShrinePos;
            shrine.transform.rotation = Quaternion.Euler(0f, -100f, 0f); // bell faces the trail
            var bell = shrine.GetComponent<Waybell>();
            var so = new SerializedObject(bell);
            so.FindProperty("waybellId").stringValue = RpgContentBuilder.WaybellId;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetArray(bell, "resonators", RoadShimmer.ToArray());
        }

        /// <summary>Ring of broken pillars around a stone platform; the bell hangs from a stone frame. Origin = ground centre.</summary>
        static GameObject BuildWaybellShrine()
        {
            var root = new GameObject("Building_WaybellShrine");
            var t = root.transform;
            var stone = Mat("Stone_Ancient", new Color(0.6f, 0.62f, 0.6f));
            var moss = Mat("Roof_Moss", new Color(0.36f, 0.48f, 0.25f));
            var bronze = Mat("Waybell_Bronze", new Color(0.55f, 0.42f, 0.22f), emission: Color.black);
            var runeMat = Mat("Waybell_Runes", new Color(0.32f, 0.36f, 0.36f), emission: Color.black);
            var lightMat = Mat("Waybell_Light", new Color(0.6f, 1f, 0.95f), emission: new Color(0.6f, 1.6f, 1.5f));
            var glow = new List<Renderer>();

            // Low platform (0.3 m: within the player's step height).
            Prim(PrimitiveType.Cylinder, "Platform", t, new Vector3(0f, 0.15f, 0f), new Vector3(9f, 0.15f, 9f), stone, true);
            var circle = Prim(PrimitiveType.Cylinder, "RuneCircle", t, new Vector3(0f, 0.305f, 0f), new Vector3(6.4f, 0.005f, 6.4f), runeMat, false);
            circle.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            glow.Add(circle.GetComponent<Renderer>());
            Prim(PrimitiveType.Cylinder, "RuneCircleInner", t, new Vector3(0f, 0.31f, 0f), new Vector3(5.6f, 0.005f, 5.6f), stone, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var mark = Prim(PrimitiveType.Cube, $"Rune_{i}", t, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.316f, 3f), new Vector3(0.18f, 0.01f, 0.45f), runeMat, false, new Vector3(0f, a, 0f));
                glow.Add(mark.GetComponent<Renderer>());
            }

            // Broken pillars
            float[] heights = { 2.8f, 1.4f, 3.2f, 1.0f, 2.2f, 1.7f };
            for (int i = 0; i < heights.Length; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(a) * 3.9f, 0.3f + heights[i] * 0.5f, Mathf.Sin(a) * 3.9f);
                var pillar = Prim(PrimitiveType.Cylinder, $"Pillar_{i}", t, pos, new Vector3(0.6f, heights[i] * 0.5f, 0.6f), stone, true);
                Prim(PrimitiveType.Cylinder, "Moss", pillar.transform, new Vector3(0f, 1f, 0f), new Vector3(1.08f, 0.08f, 1.08f), moss, false);
            }

            // Frame and bell
            Prim(PrimitiveType.Cube, "FramePost_L", t, new Vector3(-1.35f, 2.05f, 0f), new Vector3(0.38f, 3.5f, 0.38f), stone, true);
            Prim(PrimitiveType.Cube, "FramePost_R", t, new Vector3(1.35f, 2.05f, 0f), new Vector3(0.38f, 3.5f, 0.38f), stone, true);
            Prim(PrimitiveType.Cube, "FrameBeam", t, new Vector3(0f, 3.95f, 0f), new Vector3(3.3f, 0.42f, 0.55f), stone, true);
            Prim(PrimitiveType.Cube, "FrameMoss", t, new Vector3(0f, 4.2f, 0f), new Vector3(3.4f, 0.1f, 0.6f), moss, false);
            var pivot = new GameObject("BellPivot").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(0f, 3.74f, 0f);
            glow.Add(Prim(PrimitiveType.Sphere, "BellCrown", pivot, new Vector3(0f, -0.25f, 0f), new Vector3(0.85f, 0.55f, 0.85f), bronze, false).GetComponent<Renderer>());
            glow.Add(Prim(PrimitiveType.Cylinder, "Bell", pivot, new Vector3(0f, -0.75f, 0f), new Vector3(1.0f, 0.55f, 1.0f), bronze, false).GetComponent<Renderer>());
            glow.Add(Prim(PrimitiveType.Cylinder, "BellLip", pivot, new Vector3(0f, -1.3f, 0f), new Vector3(1.3f, 0.06f, 1.3f), bronze, false).GetComponent<Renderer>());
            Prim(PrimitiveType.Sphere, "Clapper", pivot, new Vector3(0f, -1.38f, 0f), Vector3.one * 0.26f, Mat("Club_Iron", Color.gray), false);

            // Ring of light that rolls outward when the bell wakes.
            var ring = new GameObject("LightRing").transform;
            ring.SetParent(t, false);
            ring.localPosition = new Vector3(0f, 0.9f, 0f);
            for (int i = 0; i < 28; i++)
            {
                var mote = Prim(PrimitiveType.Sphere, $"Mote_{i}", ring, Vector3.zero, Vector3.one * 0.3f, lightMat, false);
                mote.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            var trig = root.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0f, 1.5f, 0f);
            trig.size = new Vector3(3.4f, 3f, 3.4f);

            var bell = root.AddComponent<Waybell>();
            bell.Configure("waybell", pivot, glow.ToArray(), ring, null);
            bell.SetText(new[]
            {
                "You lay a paw on the cold bronze. For a heartbeat, nothing.",
                "Then the Waybell sings: one deep note you feel in your bones more than hear.",
                "Light races through the runes beneath your feet. Somewhere to the west, the brambles on the old road shiver.",
                "For an instant you see it clearly: the Mosswood Road, paved and whole, running north. Then the forest closes over it again.",
                "The bell knew you. Somehow, it knew you.",
            }, new[]
            {
                "The Waybell hums softly, warm to the touch, as if it is remembering something.",
                "The road beyond the brambles is still lost. Whatever woke here is only the beginning.",
            });
            return root;
        }

        static void BuildAreas(HudRefs hud)
        {
            var areas = new GameObject("Areas").transform;
            var village = new GameObject("Area_HavenVillage").AddComponent<AreaZone>();
            village.transform.SetParent(areas);
            village.transform.position = new Vector3(0f, -1f, -14f);
            village.SetSize(new Vector3(116f, 12f, 72f));
            village.Configure("Haven Village", "A quiet woodland village", hud.Banner);
            village.SetId("haven_village");

            var forest = new GameObject("Area_Mosswood").AddComponent<AreaZone>();
            forest.transform.SetParent(areas);
            forest.transform.position = new Vector3(0f, -1f, 66f);
            forest.SetSize(new Vector3(116f, 12f, 66f));
            forest.Configure("Mosswood", "The forest road north", hud.Banner);
            forest.SetId("mosswood");

            var shrine = new GameObject("Area_WaybellShrine").AddComponent<AreaZone>();
            shrine.transform.SetParent(areas);
            shrine.transform.position = ShrinePos + Vector3.down;
            shrine.SetSize(new Vector3(17f, 12f, 17f));
            shrine.Configure("Forgotten Shrine", "Where a Waybell keeps its silence", hud.Banner);
            shrine.SetId(RpgContentBuilder.ShrineAreaId);
        }

        // ───────────────────────────── Characters ─────────────────────────────

        static GameObject BuildMabelPrefab()
        {
            var first = LoadOrCreateDialogue("Mabel_FirstMeeting", "Mabel",
                ("", "Well, look who's back! I was starting to think the wilds had finally swallowed you whole."),
                ("You", "Takes more than a few bandits and one very rude bear. Is that cinnamon I smell?"),
                ("", "Honey-cinnamon rolls, fresh this morning. One's yours if you promise to look in on Eldon."),
                ("", "He hasn't been by for his morning loaf in three days. Three! That old cartographer never misses."),
                ("You", "He's probably buried under a mountain of maps again. I'll check on him."),
                ("", "And mind the north side of the stream, dear. Bram says boar ruffians have been prowling about Mosswood."));
            var repeatA = LoadOrCreateDialogue("Mabel_Repeat_Rolls", "Mabel",
                ("", "Still warm, those rolls. Don't you let Bram tell you iron counts as a food group."));
            var repeatB = LoadOrCreateDialogue("Mabel_Repeat_Road", "Mabel",
                ("You", "Mabel, when did you last take the Mosswood Road up to the northern market?"),
                ("", "The Mosswood... road? Dear, there's no road through Mosswood. There never has been."),
                ("", "Are you feeling alright? You've gone pale as flour."),
                ("You", "...Right. Must be thinking of somewhere else."));

            return BuildNpcPrefab("NPC_Mabel", "Mabel", new CatLook
            {
                Prefix = "Mabel",
                Fur = new Color(0.95f, 0.93f, 0.88f), Stripe = new Color(0.9f, 0.55f, 0.22f),
                Cream = new Color(0.98f, 0.96f, 0.92f), Eye = new Color(0.85f, 0.6f, 0.2f),
                Outfit = new Color(0.82f, 0.5f, 0.55f), Accent = new Color(0.95f, 0.8f, 0.35f),
                Leather = new Color(0.5f, 0.35f, 0.25f),
                Apron = new Color(0.97f, 0.95f, 0.9f),
                Patches = new[] { new Color(0.9f, 0.55f, 0.22f), new Color(0.15f, 0.12f, 0.1f) },
                Scale = 0.95f,
            }, first, repeatA, repeatB);
        }

        static GameObject BuildBramPrefab()
        {
            var first = LoadOrCreateDialogue("Bram_FirstMeeting", "Bram",
                ("", "Hmph. You're back. And I suppose that blade of yours is chipped to ribbons again."),
                ("You", "Only slightly ribboned. Hello to you too, Bram."),
                ("", "Leave it with me later and I'll put an edge on it. Someone in this village has to keep you alive."),
                ("", "...Good to see you in one piece, kid. Don't tell Mabel I said that."));
            var repeatA = LoadOrCreateDialogue("Bram_Repeat_Upgrades", "Bram",
                ("", "Upgrades? Bring me good ore and honest coin and we'll talk."),
                ("", "Until then, keep your guard up. Watch their shoulders: a ruffian always rears back before he swings."));
            var repeatB = LoadOrCreateDialogue("Bram_Repeat_Noise", "Bram",
                ("", "Heard something big crashing about in Mosswood last night."),
                ("", "Probably a boar. ...Probably."));

            return BuildNpcPrefab("NPC_Bram", "Bram", new CatLook
            {
                Prefix = "Bram",
                Fur = new Color(0.86f, 0.5f, 0.22f), Stripe = new Color(0.58f, 0.28f, 0.1f),
                Cream = new Color(0.95f, 0.85f, 0.7f), Eye = new Color(0.85f, 0.55f, 0.15f),
                Outfit = new Color(0.3f, 0.3f, 0.32f), Accent = new Color(0.55f, 0.2f, 0.15f),
                Leather = new Color(0.32f, 0.2f, 0.11f),
                Apron = new Color(0.36f, 0.23f, 0.13f),
                Scale = 1.12f,
            }, first, repeatA, repeatB);
        }

        static GameObject BuildNpcPrefab(string prefabName, string displayName, CatLook look, DialogueData first, params DialogueData[] repeats)
        {
            EnsureFolder(CharPrefabs);
            string path = $"{CharPrefabs}/{prefabName}.prefab";
            if (!rebuildPrefabs && AssetExistsAt(path)) return AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var root = new GameObject(prefabName);
            try
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.9f * look.Scale, 0f);
                capsule.height = 1.8f * look.Scale;
                capsule.radius = 0.4f;
                var npc = root.AddComponent<NpcCharacter>();
                npc.Configure(displayName, first, repeats);

                var visual = BuildCatVisual(root.transform, look, out var body, out var tail, out _);
                var idle = visual.gameObject.AddComponent<NpcIdleMotion>();
                Set(idle, "npc", npc);
                Set(idle, "body", body);
                Set(idle, "tail", tail);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static DialogueData LoadOrCreateDialogue(string assetName, string speaker, params (string speaker, string text)[] lines)
        {
            string path = $"{DialogueFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (existing != null) return existing; // keep writers' edits

            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.defaultSpeaker = speaker;
            data.lines = new DialogueData.Line[lines.Length];
            for (int i = 0; i < lines.Length; i++) data.lines[i] = new DialogueData.Line(lines[i].speaker, lines[i].text);
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        static void PlaceVillagers(Scene scene, GameObject mabel, GameObject bram)
        {
            var group = new GameObject("Villagers").transform;
            var m = (GameObject)PrefabUtility.InstantiatePrefab(mabel, scene);
            m.transform.SetParent(group);
            // Beside her bread stall, facing the square.
            m.transform.SetPositionAndRotation(new Vector3(-15.8f, 0f, 1.9f), Quaternion.Euler(0f, 90f, 0f));
            var mabelNpc = m.GetComponent<NpcCharacter>();
            mabelNpc.ConfigureQuestContent(new[] { RpgContentBuilder.RoadTalk(RpgContentBuilder.MabelRoadDialogue()) }, new ItemAmount(items.HoneyRoll, 1));
            PrefabUtility.RecordPrefabInstancePropertyModifications(mabelNpc);

            var b = (GameObject)PrefabUtility.InstantiatePrefab(bram, scene);
            b.transform.SetParent(group);
            // By his anvil.
            b.transform.SetPositionAndRotation(new Vector3(18.4f, 0.16f, 4.8f), Quaternion.Euler(0f, -100f, 0f));
            var bramNpc = b.GetComponent<NpcCharacter>();
            bramNpc.ConfigureQuestContent(new[] { RpgContentBuilder.RoadTalk(RpgContentBuilder.BramRoadDialogue()) }, default);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bramNpc);
        }

        static void PlaceForestEnemy(Scene scene, GameObject boarPrefab)
        {
            var group = new GameObject("Enemies").transform;
            var boar = (GameObject)PrefabUtility.InstantiatePrefab(boarPrefab, scene);
            boar.transform.SetParent(group);
            boar.transform.SetPositionAndRotation(new Vector3(-16f, 0f, 69f), Quaternion.Euler(0f, 120f, 0f));
            var spawner = new GameObject("BoarRuffian_Respawner").AddComponent<PrototypeEnemySpawner>();
            spawner.transform.SetParent(group);
            Set(spawner, "enemy", boar.GetComponent<EnemyController>());
        }

        // ───────────────────────────── Buildings ─────────────────────────────

        /// <summary>Solid cottage (not enterable) with a knockable locked door. Origin = ground centre, door faces +Z.</summary>
        static GameObject BuildCottage(string name, float w, float d, float h, Color wallColor, Color roofColor)
        {
            var root = new GameObject(name);
            var t = root.transform;
            var plaster = Mat($"Plaster_{ColorKey(wallColor)}", wallColor);
            var beam = Mat("Cabin_Beams", new Color(0.33f, 0.21f, 0.12f));
            var stone = Mat("Stone", new Color(0.55f, 0.55f, 0.52f));
            var roof = Mat($"Roof_{ColorKey(roofColor)}", roofColor);

            Prim(PrimitiveType.Cube, "Foundation", t, new Vector3(0f, 0.2f, 0f), new Vector3(w + 0.3f, 0.4f, d + 0.3f), stone, true);
            Prim(PrimitiveType.Cube, "Walls", t, new Vector3(0f, h * 0.5f + 0.2f, 0f), new Vector3(w, h, d), plaster, true);
            // Timber framing
            for (int cx = -1; cx <= 1; cx += 2)
            for (int cz = -1; cz <= 1; cz += 2)
                Prim(PrimitiveType.Cube, $"Post_{cx}_{cz}", t, new Vector3(cx * w * 0.5f, h * 0.5f + 0.2f, cz * d * 0.5f), new Vector3(0.24f, h, 0.24f), beam, false);
            Prim(PrimitiveType.Cube, "Beam_Front", t, new Vector3(0f, h * 0.55f + 0.2f, d * 0.5f + 0.01f), new Vector3(w, 0.18f, 0.06f), beam, false);
            Prim(PrimitiveType.Cube, "Beam_Back", t, new Vector3(0f, h * 0.55f + 0.2f, -d * 0.5f - 0.01f), new Vector3(w, 0.18f, 0.06f), beam, false);
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, $"Brace_{s}", t, new Vector3(s * w * 0.3f, h * 0.3f + 0.2f, d * 0.5f + 0.01f), new Vector3(0.14f, h * 0.55f, 0.05f), beam, false, new Vector3(0f, 0f, 35f * s));

            BuildGableRoof(t, w, d, h + 0.2f, roof);
            Prim(PrimitiveType.Cube, "Chimney", t, new Vector3(w * 0.3f, h + 1.9f, -d * 0.2f), new Vector3(0.7f, 2.6f, 0.7f), stone, false);

            Window(t, "Window_FL", new Vector3(-w * 0.3f, h * 0.55f + 0.3f, d * 0.5f + 0.02f), 0f, 0.9f, 0.8f);
            Window(t, "Window_FR", new Vector3(w * 0.3f, h * 0.55f + 0.3f, d * 0.5f + 0.02f), 0f, 0.9f, 0.8f);
            Window(t, "Window_E", new Vector3(w * 0.5f + 0.02f, h * 0.55f + 0.3f, 0f), 90f, 0.9f, 0.8f);
            Window(t, "Window_W", new Vector3(-w * 0.5f - 0.02f, h * 0.55f + 0.3f, 0f), -90f, 0.9f, 0.8f);

            // Window boxes with flowers
            var box = Mat("WoodPlanks", new Color(0.55f, 0.38f, 0.24f));
            var flower = Mat("Flower_Pink", new Color(0.95f, 0.55f, 0.7f));
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cube, $"FlowerBox_{s}", t, new Vector3(s * w * 0.3f, h * 0.55f - 0.25f, d * 0.5f + 0.15f), new Vector3(1f, 0.2f, 0.25f), box, false);
                for (int f = 0; f < 4; f++)
                    Prim(PrimitiveType.Sphere, $"FB_Flower_{s}_{f}", t, new Vector3(s * w * 0.3f - 0.36f + f * 0.24f, h * 0.55f - 0.1f, d * 0.5f + 0.15f), Vector3.one * 0.16f, flower, false);
            }

            BuildDoor(t, "FrontDoor", new Vector3(0f, 0.4f, d * 0.5f), 1.2f, 2.1f, Mat("Door_Wood", new Color(0.45f, 0.28f, 0.16f)),
                locked: true, lockedMessage: "No one answers.", solidLeaf: false);
            Prim(PrimitiveType.Cube, "DoorStep", t, new Vector3(0f, 0.15f, d * 0.5f + 0.45f), new Vector3(1.6f, 0.3f, 0.7f), stone, true);
            return root;
        }

        static GameObject BuildBakery()
        {
            var root = BuildCottage("Building_Bakery", 8f, 6f, 3f, new Color(0.96f, 0.86f, 0.7f), new Color(0.7f, 0.32f, 0.22f));
            var t = root.transform;
            SetDoorMessage(root, "Knock", "Mabel's kitchen. Through the window, trays of dough are rising by the oven.");

            // Bread stall in front (toward the square, +Z).
            var wood = Mat("WoodPlanks", new Color(0.55f, 0.38f, 0.24f));
            var cloth = Mat("Awning_Stripe", new Color(0.85f, 0.35f, 0.3f));
            var bread = Mat("Bread", new Color(0.8f, 0.55f, 0.28f));
            var stall = new GameObject("BreadStall").transform;
            stall.SetParent(t, false);
            stall.localPosition = new Vector3(2.4f, 0f, 5.2f);
            Prim(PrimitiveType.Cube, "Counter", stall, new Vector3(0f, 0.5f, 0f), new Vector3(2.6f, 1f, 0.8f), wood, true);
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, $"Pole_{s}", stall, new Vector3(1.25f * s, 1.3f, -0.3f), new Vector3(0.1f, 2.6f, 0.1f), wood, false);
            Prim(PrimitiveType.Cube, "Awning", stall, new Vector3(0f, 2.55f, 0.05f), new Vector3(2.9f, 0.08f, 1.4f), cloth, false, new Vector3(14f, 0f, 0f));
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Sphere, $"Loaf_{i}", stall, new Vector3(-0.9f + i * 0.45f, 1.08f, 0.05f), new Vector3(0.36f, 0.18f, 0.22f), bread, false, new Vector3(0f, i * 25f, 0f));
            AddExaminable(stall.gameObject, "Smell bread", new Vector3(3f, 1.5f, 1.4f),
                "Honey-cinnamon rolls, seed loaves and a lopsided pie labelled \"for Bram, don't touch\".",
                "Your stomach growls loudly enough to embarrass you.");

            // Hanging sign
            Prim(PrimitiveType.Cube, "Sign", t, new Vector3(-2.6f, 2.4f, 3.3f), new Vector3(1.1f, 0.5f, 0.06f), Mat("Bread", Color.white), false);
            return root;
        }

        /// <summary>Open-fronted smithy: roof on posts, forge, anvil, rack. Origin = ground centre, open side faces +Z.</summary>
        static GameObject BuildSmithy()
        {
            var root = new GameObject("Building_Smithy");
            var t = root.transform;
            var stone = Mat("Stone", new Color(0.55f, 0.55f, 0.52f));
            var beam = Mat("Cabin_Beams", new Color(0.33f, 0.21f, 0.12f));
            var roof = Mat("Roof_Slate", new Color(0.35f, 0.38f, 0.45f));
            var iron = Mat("Club_Iron", new Color(0.35f, 0.35f, 0.38f));
            var coal = Mat("FX_Embers", new Color(1f, 0.45f, 0.1f), emission: new Color(2.2f, 0.7f, 0.1f));
            const float w = 8f, d = 6f, h = 3.2f;

            Prim(PrimitiveType.Cube, "Floor", t, new Vector3(0f, 0.08f, 0f), new Vector3(w, 0.16f, d), stone, true);
            Prim(PrimitiveType.Cube, "BackWall", t, new Vector3(0f, h * 0.5f, -d * 0.5f + 0.15f), new Vector3(w, h, 0.3f), stone, true);
            Prim(PrimitiveType.Cube, "SideWall_L", t, new Vector3(-w * 0.5f + 0.15f, h * 0.5f, -d * 0.25f), new Vector3(0.3f, h, d * 0.5f), stone, true);
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, $"FrontPost_{s}", t, new Vector3(s * (w * 0.5f - 0.2f), h * 0.5f, d * 0.5f - 0.2f), new Vector3(0.3f, h, 0.3f), beam, true);
            Prim(PrimitiveType.Cube, "Roof", t, new Vector3(0f, h + 0.5f, 0.1f), new Vector3(w + 0.8f, 0.2f, d + 1.2f), roof, false, new Vector3(-12f, 0f, 0f));

            // Forge with glowing coals and its own chimney
            Prim(PrimitiveType.Cube, "Forge", t, new Vector3(-2.2f, 0.6f, -1.8f), new Vector3(1.8f, 1.2f, 1.4f), stone, true);
            var coals = Prim(PrimitiveType.Cube, "Coals", t, new Vector3(-2.2f, 1.22f, -1.8f), new Vector3(1.3f, 0.06f, 1f), coal, false);
            coals.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Cube, "ForgeChimney", t, new Vector3(-2.2f, 3.2f, -2.2f), new Vector3(0.8f, 4.2f, 0.6f), stone, false);
            AddExaminable(t.Find("Forge").gameObject, "Examine", new Vector3(2.2f, 1.6f, 1.8f),
                "Bram's forge. Even banked, the coals throw off a fierce heat.",
                "A half-finished horseshoe cools on the edge. It's shaped suspiciously like a heart.");

            // Anvil on a stump
            Prim(PrimitiveType.Cylinder, "Stump", t, new Vector3(1.4f, 0.4f, 1.2f), new Vector3(0.7f, 0.4f, 0.7f), beam, true);
            Prim(PrimitiveType.Cube, "Anvil", t, new Vector3(1.4f, 0.95f, 1.2f), new Vector3(0.45f, 0.3f, 0.9f), iron, false);
            Prim(PrimitiveType.Cube, "AnvilHorn", t, new Vector3(1.4f, 1.0f, 1.75f), new Vector3(0.2f, 0.18f, 0.35f), iron, false);

            // Weapon rack against the back wall
            var rack = new GameObject("WeaponRack").transform;
            rack.SetParent(t, false);
            rack.localPosition = new Vector3(1.8f, 0.16f, -d * 0.5f + 0.5f);
            var steel = Mat("Sword_Steel", new Color(0.82f, 0.85f, 0.9f));
            Prim(PrimitiveType.Cube, "Bar", rack, new Vector3(0f, 1.2f, 0f), new Vector3(2.2f, 0.08f, 0.2f), beam, true);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cube, $"Blade_{i}", rack, new Vector3(-0.9f + i * 0.45f, 0.75f, 0.1f), new Vector3(0.05f, 1.2f, 0.015f), steel, false);
            AddExaminable(rack.gameObject, "Examine", new Vector3(2.4f, 2f, 1.2f),
                "Fine blades, every one. A tag on the last reads: \"NOT FOR SALE. Kid's next sword.\"");

            // Water barrel & tools
            Prim(PrimitiveType.Cylinder, "QuenchBarrel", t, new Vector3(-0.6f, 0.55f, -2.1f), new Vector3(0.8f, 0.45f, 0.8f), beam, true);
            Prim(PrimitiveType.Cylinder, "Water", t, new Vector3(-0.6f, 0.98f, -2.1f), new Vector3(0.7f, 0.02f, 0.7f), Mat("Water", Color.blue), false);
            return root;
        }

        /// <summary>
        /// Gable roof (ridge along X) for a box of width w (X) and depth d (Z) whose walls end at wallTop.
        /// 45° slopes; triangular gable ends are diamonds whose lower half hides inside the walls.
        /// </summary>
        static List<Renderer> BuildGableRoof(Transform t, float w, float d, float wallTop, Material mat)
        {
            var result = new List<Renderer>();
            float half = d * 0.5f + 0.35f;
            float length = half * 1.4142f + 0.15f;
            for (int s = -1; s <= 1; s += 2)
            {
                var slope = Prim(PrimitiveType.Cube, $"Roof_{(s > 0 ? "N" : "S")}", t, new Vector3(0f, wallTop + half * 0.5f - 0.1f, s * half * 0.5f),
                    new Vector3(w + 0.7f, 0.2f, length), mat, false, new Vector3(45f * s, 0f, 0f));
                result.Add(slope.GetComponent<Renderer>());
            }
            float side = d * 0.5f * 1.4142f;
            for (int s = -1; s <= 1; s += 2)
            {
                var gable = Prim(PrimitiveType.Cube, $"Gable_{s}", t, new Vector3(s * (w * 0.5f - 0.12f), wallTop, 0f), new Vector3(0.22f, side, side),
                    mat.name.Contains("Moss") ? Mat("Cabin_Logs", new Color(0.55f, 0.37f, 0.22f)) : Mat("Cabin_Beams", new Color(0.33f, 0.21f, 0.12f)), false, new Vector3(45f, 0f, 0f));
                result.Add(gable.GetComponent<Renderer>());
            }
            return result;
        }

        /// <summary>Door whose component sits at the bottom-centre of the doorway facing +Z; hinge on the -X edge.</summary>
        static Door BuildDoor(Transform parent, string name, Vector3 localPos, float width, float height, Material mat, bool locked, string lockedMessage, bool solidLeaf)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, height * 0.5f, 0f);
            trigger.size = new Vector3(width + 0.2f, height, 0.8f);

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
            var leaf = Prim(PrimitiveType.Cube, "Leaf", hinge, new Vector3(width * 0.5f, height * 0.5f, solidLeaf ? 0f : 0.05f), new Vector3(width - 0.04f, height, 0.1f), mat, solidLeaf);
            Prim(PrimitiveType.Sphere, "Knob", leaf.transform, new Vector3(0.38f, 0f, 0.7f), new Vector3(0.06f, 0.03f, 0.6f), Mat("Sword_Brass", new Color(0.85f, 0.68f, 0.3f)), false);
            for (int i = 0; i < 2; i++)
                Prim(PrimitiveType.Cube, $"Strap_{i}", hinge, new Vector3(width * 0.35f, height * (0.25f + i * 0.5f), solidLeaf ? 0.06f : 0.11f), new Vector3(width * 0.6f, 0.06f, 0.02f), Mat("Club_Iron", Color.gray), false);

            var door = root.AddComponent<Door>();
            Set(door, "hinge", hinge);
            SetValue(door, "locked", locked);
            if (lockedMessage != null)
            {
                var so = new SerializedObject(door);
                so.FindProperty("lockedMessage").stringValue = lockedMessage;
                so.FindProperty("lockedVerb").stringValue = "Knock";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return door;
        }

        static void SetDoorMessage(GameObject building, string verb, string message)
        {
            var door = building.GetComponentInChildren<Door>();
            if (door == null) return;
            var so = new SerializedObject(door);
            so.FindProperty("lockedVerb").stringValue = verb;
            so.FindProperty("lockedMessage").stringValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Window(Transform parent, string name, Vector3 localPos, float yaw, float w, float h)
        {
            var frame = Prim(PrimitiveType.Cube, name, parent, localPos, new Vector3(w + 0.14f, h + 0.14f, 0.05f), Mat("Cabin_Beams", new Color(0.33f, 0.21f, 0.12f)), false, new Vector3(0f, yaw, 0f));
            var pane = Prim(PrimitiveType.Cube, "Pane", frame.transform, new Vector3(0f, 0f, 0.15f), new Vector3(w / (w + 0.14f), h / (h + 0.14f), 1.2f),
                Mat("Window_Glow", new Color(1f, 0.86f, 0.55f), emission: new Color(0.9f, 0.6f, 0.25f)), false);
            Prim(PrimitiveType.Cube, "Mullion", frame.transform, new Vector3(0f, 0f, 0.3f), new Vector3(0.06f, 1f, 1.4f), Mat("Cabin_Beams", Color.black), false);
            pane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return frame;
        }

        static Examinable AddExaminable(GameObject go, string verb, Vector3 triggerSize, params string[] lines)
        {
            var ex = go.AddComponent<Examinable>();
            ex.SetText(verb, lines);
            // Trigger volume so objects without (or with small) colliders are easy to target.
            var trig = go.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            var s = go.transform.lossyScale;
            trig.size = new Vector3(triggerSize.x / Mathf.Max(0.01f, s.x), triggerSize.y / Mathf.Max(0.01f, s.y), triggerSize.z / Mathf.Max(0.01f, s.z));
            trig.center = Vector3.zero;
            return ex;
        }

        static string ColorKey(Color c) => $"{(int)(c.r * 100)}_{(int)(c.g * 100)}_{(int)(c.b * 100)}";

        // ───────────────────────────── Props ─────────────────────────────

        static GameObject BuildHavenOak()
        {
            var root = new GameObject("Prop_HavenOak");
            var bark = Mat("Bark", new Color(0.42f, 0.30f, 0.20f));
            var leaves = Mat("Leaves_Oak", new Color(0.33f, 0.56f, 0.26f));
            var t = root.transform;
            Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0f, 3f, 0f), new Vector3(2.2f, 3f, 2.2f), bark, true);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f;
                Prim(PrimitiveType.Cube, $"Root_{i}", t, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.25f, 1.3f), new Vector3(0.5f, 0.5f, 1.6f), bark, false, new Vector3(20f, a, 0f));
            }
            Prim(PrimitiveType.Sphere, "Canopy", t, new Vector3(0f, 8.5f, 0f), new Vector3(11f, 6f, 11f), leaves, false);
            Prim(PrimitiveType.Sphere, "Canopy_A", t, new Vector3(3.5f, 7.5f, 1f), new Vector3(6f, 4.5f, 6f), leaves, false);
            Prim(PrimitiveType.Sphere, "Canopy_B", t, new Vector3(-3f, 7.8f, -2f), new Vector3(6.5f, 4.5f, 6.5f), leaves, false);
            Prim(PrimitiveType.Sphere, "Canopy_C", t, new Vector3(-1f, 10.5f, 2f), new Vector3(6f, 4f, 6f), leaves, false);
            AddExaminable(root, "Examine", new Vector3(5f, 4f, 5f),
                "The Haven Oak. Older than the village itself.",
                "Generations of children have carved their initials into its roots. Yours are there too, next to a very wobbly drawing of a sword.");
            return root;
        }

        static GameObject BuildWell()
        {
            var root = new GameObject("Prop_Well");
            var t = root.transform;
            var stone = Mat("Stone", new Color(0.55f, 0.55f, 0.52f));
            var wood = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            Prim(PrimitiveType.Cylinder, "Ring", t, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.45f, 1.8f), stone, true);
            Prim(PrimitiveType.Cylinder, "Water", t, new Vector3(0f, 0.86f, 0f), new Vector3(1.4f, 0.02f, 1.4f), Mat("Water", Color.blue), false);
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, $"Post_{s}", t, new Vector3(0.85f * s, 1.4f, 0f), new Vector3(0.12f, 1.9f, 0.12f), wood, false);
            Prim(PrimitiveType.Cube, "Roof", t, new Vector3(0f, 2.5f, 0f), new Vector3(2.2f, 0.12f, 1.6f), Mat("Roof_Moss", Color.green), false);
            Prim(PrimitiveType.Cylinder, "Bucket", t, new Vector3(0.3f, 1.2f, 0f), new Vector3(0.25f, 0.15f, 0.25f), wood, false);
            AddExaminable(root, "Examine", new Vector3(3f, 2f, 3f),
                "The old village well. Locals swear the water tastes faintly of bells.");
            return root;
        }

        static GameObject BuildBench()
        {
            var root = new GameObject("Prop_Bench");
            var wood = Mat("WoodPlanks", new Color(0.55f, 0.38f, 0.24f));
            var dark = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            Prim(PrimitiveType.Cube, "Seat", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), wood, true);
            Prim(PrimitiveType.Cube, "Back", root.transform, new Vector3(0f, 0.8f, -0.22f), new Vector3(1.8f, 0.5f, 0.06f), wood, false);
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, $"Leg_{s}", root.transform, new Vector3(0.75f * s, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.45f), dark, false);
            return root;
        }

        static GameObject BuildLantern()
        {
            var root = new GameObject("Prop_Lantern");
            var dark = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            var glow = Mat("Lantern_Glow", new Color(1f, 0.85f, 0.5f), emission: new Color(2f, 1.4f, 0.6f));
            Prim(PrimitiveType.Cube, "Post", root.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.14f, 2.4f, 0.14f), dark, true);
            Prim(PrimitiveType.Cube, "Arm", root.transform, new Vector3(0f, 2.35f, 0.25f), new Vector3(0.08f, 0.08f, 0.5f), dark, false);
            var lamp = Prim(PrimitiveType.Cube, "Lamp", root.transform, new Vector3(0f, 2.1f, 0.45f), new Vector3(0.22f, 0.32f, 0.22f), glow, false);
            lamp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Cube, "Cap", root.transform, new Vector3(0f, 2.3f, 0.45f), new Vector3(0.3f, 0.06f, 0.3f), dark, false);
            return root;
        }

        static GameObject BuildFenceSegment()
        {
            // 2 m long along +X from the origin.
            var root = new GameObject("Prop_FenceSegment");
            var wood = Mat("WoodPlanks", new Color(0.55f, 0.38f, 0.24f));
            var dark = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            Prim(PrimitiveType.Cube, "Post", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.14f, 1.1f, 0.14f), dark, false);
            Prim(PrimitiveType.Cube, "RailTop", root.transform, new Vector3(1f, 0.85f, 0f), new Vector3(2f, 0.08f, 0.06f), wood, false);
            Prim(PrimitiveType.Cube, "RailLow", root.transform, new Vector3(1f, 0.45f, 0f), new Vector3(2f, 0.08f, 0.06f), wood, false);
            var block = root.AddComponent<BoxCollider>(); // one collider for the whole segment
            block.center = new Vector3(1f, 0.6f, 0f);
            block.size = new Vector3(2.05f, 1.2f, 0.2f);
            return root;
        }

        static void FenceLine(GameObject segment, Transform parent, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
            float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                var go = Place(segment, parent, Vector3.zero, 0f);
                go.transform.localPosition = from + dir * (i / (float)count);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = new Vector3(length / count / 2f, 1f, 1f);
            }
        }

        static GameObject BuildChest()
        {
            var root = new GameObject("Prop_Chest");
            var wood = Mat("Furniture_Wood", new Color(0.5f, 0.33f, 0.2f));
            var iron = Mat("Club_Iron", new Color(0.35f, 0.35f, 0.38f));
            var t = root.transform;
            Prim(PrimitiveType.Cube, "Body", t, new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 0.5f, 0.6f), wood, true);
            var lid = new GameObject("LidHinge").transform;
            lid.SetParent(t, false);
            lid.localPosition = new Vector3(0f, 0.5f, -0.3f);
            Prim(PrimitiveType.Cube, "Lid", lid, new Vector3(0f, 0.08f, 0.3f), new Vector3(1.12f, 0.16f, 0.62f), wood, false);
            Prim(PrimitiveType.Cube, "Lock", lid, new Vector3(0f, 0f, 0.62f), new Vector3(0.14f, 0.16f, 0.04f), iron, false);
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, $"Band_{s}", t, new Vector3(0.38f * s, 0.25f, 0f), new Vector3(0.06f, 0.52f, 0.62f), iron, false);
            var chest = root.AddComponent<StorageChest>();
            Set(chest, "lid", lid);
            var so = new SerializedObject(chest);
            var lines = so.FindProperty("openLines");
            lines.arraySize = 1;
            lines.GetArrayElementAtIndex(0).stringValue = "Spare rations, rope, a whetstone and a pouch of old coins. (Storage arrives with the inventory system.)";
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static GameObject BuildBarrel()
        {
            var root = new GameObject("Prop_Barrel");
            Prim(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.75f, 0.5f, 0.75f), Mat("Furniture_Wood", new Color(0.5f, 0.33f, 0.2f)), true);
            for (int i = 0; i < 2; i++)
                Prim(PrimitiveType.Cylinder, $"Hoop_{i}", root.transform, new Vector3(0f, 0.25f + i * 0.5f, 0f), new Vector3(0.78f, 0.03f, 0.78f), Mat("Club_Iron", Color.gray), false);
            return root;
        }

        static GameObject BuildCrate()
        {
            var root = new GameObject("Prop_Crate");
            Prim(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), Mat("WoodPlanks", new Color(0.55f, 0.38f, 0.24f)), true);
            Prim(PrimitiveType.Cube, "Slat", root.transform, new Vector3(0f, 0.3f, 0.305f), new Vector3(0.6f, 0.08f, 0.02f), Mat("WoodDark", Color.black), false, new Vector3(0f, 0f, 45f));
            return root;
        }

        static GameObject BuildRock()
        {
            var root = new GameObject("Prop_Rock");
            var rock = Mat("Rock", new Color(0.5f, 0.5f, 0.48f));
            Prim(PrimitiveType.Sphere, "Main", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 0.8f, 1.1f), rock, true, new Vector3(0f, 0f, 8f));
            Prim(PrimitiveType.Sphere, "Side", root.transform, new Vector3(0.6f, 0.15f, 0.3f), new Vector3(0.7f, 0.5f, 0.6f), rock, false);
            Prim(PrimitiveType.Sphere, "Moss", root.transform, new Vector3(-0.1f, 0.58f, 0f), new Vector3(0.9f, 0.18f, 0.7f), Mat("Roof_Moss", Color.green), false);
            return root;
        }

        static GameObject BuildTree(string name, Material bark, Material leaves, float height, float trunkWidth, Action<Transform, Material> canopy)
        {
            var root = new GameObject(name);
            var trunk = Prim(PrimitiveType.Cylinder, "Trunk", root.transform, new Vector3(0f, height * 0.5f, 0f), new Vector3(trunkWidth, height * 0.5f, trunkWidth), bark, false);
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.height = height;
            col.radius = trunkWidth * 0.5f;
            _ = trunk;
            canopy(root.transform, leaves);
            return root;
        }

        static GameObject BuildOakTree() => BuildTree("Prop_Tree_Oak", Mat("Bark", new Color(0.42f, 0.30f, 0.20f)), Mat("Leaves_A", new Color(0.30f, 0.55f, 0.28f)), 3.5f, 0.55f, (t, m) =>
        {
            Prim(PrimitiveType.Sphere, "Canopy", t, new Vector3(0f, 4.6f, 0f), new Vector3(3.8f, 3.2f, 3.8f), m, false);
            Prim(PrimitiveType.Sphere, "Canopy2", t, new Vector3(0.8f, 5.4f, 0.4f), new Vector3(2.6f, 2.2f, 2.6f), m, false);
        });

        static GameObject BuildBirchTree() => BuildTree("Prop_Tree_Birch", Mat("Bark_Birch", new Color(0.9f, 0.88f, 0.82f)), Mat("Leaves_B", new Color(0.45f, 0.65f, 0.25f)), 4.5f, 0.35f, (t, m) =>
        {
            Prim(PrimitiveType.Sphere, "Canopy", t, new Vector3(0f, 5.3f, 0f), new Vector3(2.6f, 3.4f, 2.6f), m, false);
            Prim(PrimitiveType.Sphere, "Canopy2", t, new Vector3(-0.4f, 4.4f, 0.3f), new Vector3(2f, 2f, 2f), Mat("Leaves_Autumn", new Color(0.85f, 0.55f, 0.25f)), false);
        });

        static GameObject BuildPineTree() => BuildTree("Prop_Tree_Pine", Mat("Bark", new Color(0.42f, 0.30f, 0.20f)), Mat("Leaves_Pine", new Color(0.18f, 0.38f, 0.24f)), 3f, 0.45f, (t, m) =>
        {
            for (int i = 0; i < 4; i++)
            {
                float s = 3.4f - i * 0.75f;
                Prim(PrimitiveType.Sphere, $"Tier_{i}", t, new Vector3(0f, 2.2f + i * 1.25f, 0f), new Vector3(s, 1.3f, s), m, false);
            }
        });

        static GameObject BuildMossyTree() => BuildTree("Prop_Tree_Mossy", Mat("Bark_Mossy", new Color(0.32f, 0.3f, 0.2f)), Mat("Leaves_Deep", new Color(0.2f, 0.42f, 0.22f)), 5f, 0.8f, (t, m) =>
        {
            Prim(PrimitiveType.Sphere, "Canopy", t, new Vector3(0f, 6.2f, 0f), new Vector3(5f, 3.6f, 5f), m, false);
            Prim(PrimitiveType.Sphere, "Canopy2", t, new Vector3(1.2f, 5.2f, -0.8f), new Vector3(3.2f, 2.4f, 3.2f), m, false);
            Prim(PrimitiveType.Sphere, "Canopy3", t, new Vector3(-1.4f, 5.6f, 1f), new Vector3(3f, 2.2f, 3f), m, false);
            Prim(PrimitiveType.Cylinder, "MossCollar", t, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.3f, 1.1f), Mat("Roof_Moss", Color.green), false);
        });

        // ───────────────────────────── Prefab plumbing ─────────────────────────────

        static GameObject GetPrefab(string name, Func<GameObject> build)
        {
            string path = $"{EnvPrefabs}/{name}.prefab";
            // Reuse existing prefabs (they may contain the user's edits) unless rebuilding; when rebuilding,
            // each prefab is regenerated once per run.
            if (AssetExistsAt(path) && (!rebuildPrefabs || BuiltThisRun.Contains(path)))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var go = build();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                BuiltThisRun.Add(path);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static readonly HashSet<string> BuiltThisRun = new HashSet<string>();

        static GameObject Place(GameObject prefab, Transform parent, Vector3 localPos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }
    }
}
