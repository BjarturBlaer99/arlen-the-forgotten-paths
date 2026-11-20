using System.Collections.Generic;
using Arlen.CameraSystem;
using Arlen.Combat;
using Arlen.Core;
using Arlen.Player;
using Arlen.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arlen.Editor
{
    /// <summary>
    /// Generates the Milestone 1 prototype: placeholder materials, a placeholder cat player prefab and a
    /// greybox training-ground scene with HUD and pause menu. Safe to rerun: existing materials and the
    /// player prefab are reused, and an existing scene is never overwritten without confirmation.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        public const string Root = "Assets/Arlen";
        public const string ScenePath = Root + "/Scenes/Prototype_TrainingGround.unity";
        public const string PlayerPrefabPath = Root + "/Prefabs/Characters/Player_PlaceholderCat.prefab";
        public const string InputActionsPath = Root + "/Input/ArlenControls.inputactions";
        const string MaterialsFolder = Root + "/Art/Materials";

        static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        // ───────────────────────────── Menu commands ─────────────────────────────

        [MenuItem("Tools/Arlen/Create Prototype Scene", priority = 0)]
        public static void CreatePrototypeSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetExists(ScenePath) && !EditorUtility.DisplayDialog("Arlen",
                    $"'{ScenePath}' already exists.\n\nOverwrite it with a freshly generated scene? Any manual edits to it will be lost.",
                    "Overwrite", "Cancel"))
                return;

            BuildScene();
        }

        [MenuItem("Tools/Arlen/Rebuild Player Prefab", priority = 1)]
        public static void RebuildPlayerPrefabMenu()
        {
            if (AssetExists(PlayerPrefabPath) && !EditorUtility.DisplayDialog("Arlen",
                    $"Regenerate '{PlayerPrefabPath}'? Manual edits to the prefab will be lost.\n" +
                    "Scenes using it keep their instance and pick up the new version.", "Rebuild", "Cancel"))
                return;
            BuildPlayerPrefab();
            Debug.Log($"[Arlen] Rebuilt {PlayerPrefabPath}");
        }

        [MenuItem("Tools/Arlen/Open Prototype Scene", priority = 2)]
        public static void OpenPrototypeScene()
        {
            if (!AssetExists(ScenePath))
            {
                EditorUtility.DisplayDialog("Arlen", "The prototype scene doesn't exist yet. Use Tools > Arlen > Create Prototype Scene.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>
        /// Command-line entry point:
        /// Unity -batchmode -projectPath . -executeMethod Arlen.Editor.PrototypeSceneBuilder.BuildFromCommandLine -quit
        /// Refuses to overwrite an existing scene unless "-arlenOverwrite" is passed.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            bool overwrite = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-arlenOverwrite") >= 0;
            if (AssetExists(ScenePath) && !overwrite)
            {
                Debug.LogWarning($"[Arlen] {ScenePath} already exists; not overwriting (pass -arlenOverwrite to force).");
                return;
            }
            BuildScene();
        }

        // ───────────────────────────── Scene ─────────────────────────────

        static void BuildScene()
        {
            MaterialCache.Clear();
            EnsureFolder(Root + "/Scenes");

            GameObject playerPrefab = AssetExists(PlayerPrefabPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath)
                : BuildPlayerPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            var environment = new GameObject("Environment").transform;
            BuildGround(environment);
            BuildTrainingProps(environment);
            BuildScenery(environment);

            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            Set(player.GetComponent<PlayerRespawn>(), "spawnPoint", spawn);

            var cam = BuildCamera(player);
            Set(player.GetComponent<ArlenPlayerController>(), "cameraTransform", cam.transform);

            BuildUI(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arlen] Created {ScenePath}. Press Play to test.");
        }

        static void SetupLighting()
        {
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            // Cheap, painterly ambient: tri-colour gradient instead of baked/realtime GI.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.68f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.56f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.21f);
            RenderSettings.sun = light;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.79f, 0.86f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 150f;
        }

        static void BuildGround(Transform parent)
        {
            Prim(PrimitiveType.Cube, "Ground", parent, new Vector3(0f, -0.5f, 0f), new Vector3(120f, 1f, 120f), Mat("Grass", new Color(0.42f, 0.62f, 0.30f)), collider: true);
            Prim(PrimitiveType.Cube, "DirtPath", parent, new Vector3(0f, 0.005f, 10f), new Vector3(3f, 0.01f, 40f), Mat("Dirt", new Color(0.66f, 0.55f, 0.38f)), collider: false);

            // Perimeter walls keep the player inside the test area.
            var wallMat = Mat("Stone", new Color(0.55f, 0.55f, 0.52f));
            Prim(PrimitiveType.Cube, "Wall_N", parent, new Vector3(0f, 1f, 50f), new Vector3(100f, 2f, 1f), wallMat, collider: true);
            Prim(PrimitiveType.Cube, "Wall_S", parent, new Vector3(0f, 1f, -50f), new Vector3(100f, 2f, 1f), wallMat, collider: true);
            Prim(PrimitiveType.Cube, "Wall_E", parent, new Vector3(50f, 1f, 0f), new Vector3(1f, 2f, 100f), wallMat, collider: true);
            Prim(PrimitiveType.Cube, "Wall_W", parent, new Vector3(-50f, 1f, 0f), new Vector3(1f, 2f, 100f), wallMat, collider: true);
        }

        static void BuildTrainingProps(Transform parent)
        {
            // Sparring dummies
            var dummies = new GameObject("TrainingDummies").transform;
            dummies.SetParent(parent);
            BuildDummy(dummies, new Vector3(-3f, 0f, 8f));
            BuildDummy(dummies, new Vector3(0f, 0f, 9.5f));
            BuildDummy(dummies, new Vector3(3f, 0f, 8f));

            // Jump course: each step is 0.5 m higher than the last (jump height is 1.35 m).
            var course = new GameObject("JumpCourse").transform;
            course.SetParent(parent);
            var wood = Mat("WoodPlanks", new Color(0.55f, 0.38f, 0.24f));
            for (int i = 0; i < 5; i++)
            {
                float h = 0.5f * (i + 1);
                Prim(PrimitiveType.Cube, $"Step_{i + 1}", course, new Vector3(10f + i * 2.6f, h * 0.5f, 4f + i * 1.5f), new Vector3(2.4f, h, 2.4f), wood, collider: true);
            }
            Prim(PrimitiveType.Cube, "Ramp", course, new Vector3(-12f, 1.1f, 6f), new Vector3(4f, 0.3f, 10f), wood, collider: true, euler: new Vector3(-14f, 0f, 0f));
            Prim(PrimitiveType.Cube, "RampTop", course, new Vector3(-12f, 2.15f, 13.3f), new Vector3(4f, 0.3f, 5f), wood, collider: true);

            // Camera collision test: pillars and an archway.
            var stone = Mat("Stone", Color.gray);
            var ruins = new GameObject("Ruins").transform;
            ruins.SetParent(parent);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, $"Pillar_{i + 1}", ruins, new Vector3(-6f + i * 4f, 3f, -12f), new Vector3(1.2f, 3f, 1.2f), stone, collider: true);
            Prim(PrimitiveType.Cube, "Lintel", ruins, new Vector3(0f, 6.3f, -12f), new Vector3(14f, 0.6f, 1.6f), stone, collider: true);

            // Blight pool: periodic damage.
            var blight = new GameObject("BlightPool");
            blight.transform.SetParent(parent);
            blight.transform.position = new Vector3(-10f, 0f, -4f);
            var blightVol = blight.AddComponent<HealthVolume>();
            SetValue(blightVol, "mode", (int)HealthVolume.Mode.Damage);
            SetValue(blightVol, "amountPerTick", 6f);
            SetValue(blightVol, "size", new Vector3(5f, 2f, 5f));
            Prim(PrimitiveType.Cylinder, "BlightGoo", blight.transform, new Vector3(0f, 0.02f, 0f), new Vector3(5.5f, 0.02f, 5.5f),
                Mat("Blight", new Color(0.45f, 0.15f, 0.6f), emission: new Color(0.35f, 0.05f, 0.5f)), collider: false);

            // Healing spring: periodic heal.
            var spring = new GameObject("HealingSpring");
            spring.transform.SetParent(parent);
            spring.transform.position = new Vector3(10f, 0f, -6f);
            var springVol = spring.AddComponent<HealthVolume>();
            SetValue(springVol, "mode", (int)HealthVolume.Mode.Heal);
            SetValue(springVol, "amountPerTick", 10f);
            SetValue(springVol, "size", new Vector3(4f, 2f, 4f));
            Prim(PrimitiveType.Cylinder, "SpringWater", spring.transform, new Vector3(0f, 0.02f, 0f), new Vector3(4f, 0.02f, 4f),
                Mat("SpringWater", new Color(0.35f, 0.75f, 0.85f), emission: new Color(0.05f, 0.25f, 0.3f)), collider: false);

            // Pulsing hazard: practise dodge i-frames.
            var pulse = new GameObject("PulsingHazard");
            pulse.transform.SetParent(parent);
            pulse.transform.position = new Vector3(0f, 0.5f, 22f);
            var hazard = pulse.AddComponent<PulsingHazard>();
            var ring = Prim(PrimitiveType.Cylinder, "Indicator", pulse.transform, new Vector3(0f, -0.47f, 0f), new Vector3(6f, 0.02f, 6f),
                Mat("Hazard", new Color(0.35f, 0.15f, 0.45f)), collider: false);
            Set(hazard, "indicator", ring.GetComponent<Renderer>());
            Prim(PrimitiveType.Sphere, "Core", pulse.transform, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.6f,
                Mat("Blight", Color.magenta), collider: false);
        }

        static void BuildDummy(Transform parent, Vector3 position)
        {
            var root = new GameObject("TrainingDummy");
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.4f;

            var health = root.AddComponent<HealthComponent>();
            SetValue(health, "maxHealth", 60f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var straw = Mat("Straw", new Color(0.86f, 0.74f, 0.42f));
            var wood = Mat("WoodDark", new Color(0.38f, 0.26f, 0.16f));
            Prim(PrimitiveType.Cylinder, "Post", visual, new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 0.6f, 0.12f), wood, collider: false);
            Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 1.25f, 0f), new Vector3(0.6f, 0.5f, 0.5f), straw, collider: false);
            Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.9f, 0f), Vector3.one * 0.4f, straw, collider: false);
            Prim(PrimitiveType.Cube, "Arms", visual, new Vector3(0f, 1.45f, 0f), new Vector3(1.4f, 0.1f, 0.1f), wood, collider: false);
            Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.8f, 0.05f, 0.8f), wood, collider: false);

            var dummy = root.AddComponent<TrainingDummy>();
            Set(dummy, "visualRoot", visual);
            var flash = root.AddComponent<DamageFlash>();
            Set(flash, "health", health);
            SetArray(flash, "renderers", visual.GetComponentsInChildren<Renderer>());
        }

        static void BuildScenery(Transform parent)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(parent);
            var bark = Mat("Bark", new Color(0.42f, 0.30f, 0.20f));
            var leaves = new[]
            {
                Mat("Leaves_A", new Color(0.30f, 0.55f, 0.28f)),
                Mat("Leaves_B", new Color(0.45f, 0.65f, 0.25f)),
                Mat("Leaves_Autumn", new Color(0.85f, 0.55f, 0.25f)),
            };
            var rock = Mat("Rock", new Color(0.5f, 0.5f, 0.48f));
            var flowerMats = new[]
            {
                Mat("Flower_Pink", new Color(0.95f, 0.55f, 0.7f)),
                Mat("Flower_Yellow", new Color(0.98f, 0.85f, 0.3f)),
                Mat("Flower_Blue", new Color(0.45f, 0.6f, 0.95f)),
            };

            var rng = new System.Random(1234); // deterministic layout
            float R(float min, float max) => (float)(min + rng.NextDouble() * (max - min));

            for (int i = 0; i < 40; i++)
            {
                Vector3 p = RandomPerimeterPoint(rng, 22f, 46f);
                var tree = new GameObject($"Tree_{i:00}").transform;
                tree.SetParent(scenery);
                tree.position = p;
                float h = R(2.5f, 5f);
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.5f, h * 0.5f, 0.5f), bark, collider: true);
                var leaf = leaves[rng.Next(leaves.Length)];
                float s = R(2.5f, 4f);
                Prim(PrimitiveType.Sphere, "Canopy", tree, new Vector3(0f, h + s * 0.3f, 0f), new Vector3(s, s * 0.85f, s), leaf, collider: false);
                Prim(PrimitiveType.Sphere, "Canopy2", tree, new Vector3(R(-0.6f, 0.6f), h + s * 0.7f, R(-0.6f, 0.6f)), Vector3.one * s * 0.65f, leaf, collider: false);
            }

            for (int i = 0; i < 18; i++)
            {
                Vector3 p = RandomPerimeterPoint(rng, 14f, 44f);
                float s = R(0.6f, 2f);
                Prim(PrimitiveType.Sphere, $"Rock_{i:00}", scenery, p + Vector3.up * s * 0.2f, new Vector3(s * R(1f, 1.6f), s * 0.7f, s), rock,
                    collider: true, euler: new Vector3(0f, R(0f, 360f), R(-10f, 10f)));
            }

            for (int i = 0; i < 60; i++)
            {
                Vector3 p = RandomPerimeterPoint(rng, 6f, 40f);
                Prim(PrimitiveType.Sphere, $"Flower_{i:00}", scenery, p + Vector3.up * 0.12f, Vector3.one * R(0.15f, 0.3f),
                    flowerMats[rng.Next(flowerMats.Length)], collider: false);
            }
        }

        static Vector3 RandomPerimeterPoint(System.Random rng, float minRadius, float maxRadius)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float r = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());
            return new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }

        static Camera BuildCamera(GameObject player)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            go.AddComponent<AudioListener>();
            go.transform.position = player.transform.position + new Vector3(0f, 2.5f, -5f);

            var orbit = go.AddComponent<ThirdPersonCameraController>();
            Set(orbit, "target", player.transform);
            Set(orbit, "input", player.GetComponent<PlayerInputReader>());
            return cam;
        }

        // ───────────────────────────── Player prefab ─────────────────────────────

        static GameObject BuildPlayerPrefab()
        {
            EnsureFolder(Root + "/Prefabs/Characters");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null) Debug.LogError($"[Arlen] Input actions not found at {InputActionsPath}.");

            var root = new GameObject("Player_PlaceholderCat");
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.stepOffset = 0.35f;
                cc.slopeLimit = 50f;
                cc.skinWidth = 0.05f;
                cc.minMoveDistance = 0f;

                var input = root.AddComponent<PlayerInputReader>();
                Set(input, "actions", actions);
                root.AddComponent<PlayerActionState>();
                var health = root.AddComponent<HealthComponent>();
                root.AddComponent<StaminaComponent>();
                root.AddComponent<ArlenPlayerController>();
                var dodge = root.AddComponent<PlayerDodge>();
                var combat = root.AddComponent<PlayerMeleeCombat>();
                var respawn = root.AddComponent<PlayerRespawn>();
                SetArray(respawn, "disableWhileDead", new Object[] { dodge, combat });
                root.AddComponent<PlayerAnimatorBridge>();

                var visual = BuildCatVisual(root.transform, out var body, out var tail, out var sword);
                var anim = visual.gameObject.AddComponent<PlaceholderCatAnimator>();
                Set(anim, "body", body);
                Set(anim, "tail", tail);
                Set(anim, "swordPivot", sword);

                var flash = visual.gameObject.AddComponent<DamageFlash>();
                Set(flash, "health", health);

                return PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Primitive-based stand-in for the gray tabby Wayfinder. Not production art: it exists so
        /// silhouette, scale and camera framing can be judged while systems are built.
        /// </summary>
        static Transform BuildCatVisual(Transform parent, out Transform body, out Transform tail, out Transform swordPivot)
        {
            var fur = Mat("Cat_FurGray", new Color(0.52f, 0.50f, 0.47f));
            var stripe = Mat("Cat_FurStripe", new Color(0.30f, 0.27f, 0.24f));
            var cream = Mat("Cat_Cream", new Color(0.93f, 0.87f, 0.74f));
            var eye = Mat("Cat_Eye", new Color(0.70f, 0.82f, 0.25f));
            var pupil = Mat("Cat_Pupil", new Color(0.05f, 0.05f, 0.05f));
            var pink = Mat("Cat_Pink", new Color(0.85f, 0.55f, 0.55f));
            var tunic = Mat("Cat_Tunic", new Color(0.22f, 0.50f, 0.26f));
            var scarf = Mat("Cat_Scarf", new Color(0.12f, 0.58f, 0.58f));
            var leather = Mat("Cat_Leather", new Color(0.42f, 0.27f, 0.16f));
            var steel = Mat("Sword_Steel", new Color(0.82f, 0.85f, 0.9f));
            var brass = Mat("Sword_Brass", new Color(0.85f, 0.68f, 0.3f));

            var visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);

            body = new GameObject("Body").transform;
            body.SetParent(visual, false);
            body.localPosition = new Vector3(0f, 0.95f, 0f);

            // Torso & outfit
            Prim(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.42f, 0.4f), fur, false);
            Prim(PrimitiveType.Sphere, "Chest", body, new Vector3(0f, 0.18f, 0.13f), new Vector3(0.3f, 0.3f, 0.16f), cream, false);
            Prim(PrimitiveType.Cylinder, "Tunic", body, new Vector3(0f, -0.12f, 0f), new Vector3(0.56f, 0.26f, 0.46f), tunic, false);
            Prim(PrimitiveType.Cylinder, "Belt", body, new Vector3(0f, -0.2f, 0f), new Vector3(0.58f, 0.035f, 0.48f), leather, false);
            Prim(PrimitiveType.Cube, "Satchel", body, new Vector3(-0.3f, -0.3f, -0.02f), new Vector3(0.1f, 0.16f, 0.22f), leather, false);
            Prim(PrimitiveType.Cylinder, "Scarf", body, new Vector3(0f, 0.38f, 0f), new Vector3(0.42f, 0.06f, 0.4f), scarf, false);
            Prim(PrimitiveType.Cube, "Cape", body, new Vector3(0f, 0.2f, -0.21f), new Vector3(0.44f, 0.4f, 0.03f), scarf, false, new Vector3(8f, 0f, 0f));

            // Head
            var head = new GameObject("Head").transform;
            head.SetParent(body, false);
            head.localPosition = new Vector3(0f, 0.66f, 0.02f);
            Prim(PrimitiveType.Sphere, "Skull", head, Vector3.zero, new Vector3(0.48f, 0.43f, 0.43f), fur, false);
            Prim(PrimitiveType.Sphere, "Muzzle", head, new Vector3(0f, -0.08f, 0.18f), new Vector3(0.22f, 0.14f, 0.12f), cream, false);
            Prim(PrimitiveType.Sphere, "Nose", head, new Vector3(0f, -0.04f, 0.245f), new Vector3(0.05f, 0.035f, 0.03f), pink, false);
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                Prim(PrimitiveType.Sphere, $"Eye_{s}", head, new Vector3(0.095f * side, 0.04f, 0.185f), new Vector3(0.085f, 0.1f, 0.04f), eye, false);
                Prim(PrimitiveType.Sphere, $"Pupil_{s}", head, new Vector3(0.095f * side, 0.04f, 0.2f), new Vector3(0.025f, 0.075f, 0.02f), pupil, false);
                Prim(PrimitiveType.Cube, $"Ear_{s}", head, new Vector3(0.13f * side, 0.2f, -0.01f), new Vector3(0.14f, 0.14f, 0.045f), fur, false, new Vector3(0f, 0f, 45f + 12f * side));
                Prim(PrimitiveType.Cube, $"EarInner_{s}", head, new Vector3(0.13f * side, 0.2f, 0.012f), new Vector3(0.08f, 0.08f, 0.02f), pink, false, new Vector3(0f, 0f, 45f + 12f * side));
            }
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cube, $"ForeheadStripe_{i}", head, new Vector3((i - 1) * 0.06f, 0.15f, 0.15f), new Vector3(0.025f, 0.09f, 0.02f), stripe, false, new Vector3(-35f, 0f, 0f));

            // Limbs
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                Prim(PrimitiveType.Capsule, $"Arm_{s}", body, new Vector3(0.31f * side, 0.08f, 0f), new Vector3(0.13f, 0.27f, 0.13f), fur, false, new Vector3(0f, 0f, 8f * side));
                Prim(PrimitiveType.Cube, $"ArmStripe_{s}", body, new Vector3(0.32f * side, 0.12f, 0f), new Vector3(0.135f, 0.03f, 0.135f), stripe, false);
                Prim(PrimitiveType.Sphere, $"Glove_{s}", body, new Vector3(0.35f * side, -0.2f, 0.03f), Vector3.one * 0.13f, leather, false);
                Prim(PrimitiveType.Capsule, $"Leg_{s}", body, new Vector3(0.12f * side, -0.55f, 0f), new Vector3(0.16f, 0.27f, 0.16f), fur, false);
                Prim(PrimitiveType.Cube, $"Boot_{s}", body, new Vector3(0.12f * side, -0.86f, 0.04f), new Vector3(0.17f, 0.18f, 0.26f), leather, false);
            }

            // Tail: striped segments curving up behind the character.
            tail = new GameObject("Tail").transform;
            tail.SetParent(body, false);
            tail.localPosition = new Vector3(0f, -0.25f, -0.2f);
            tail.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            for (int i = 0; i < 6; i++)
            {
                float z = -0.08f - i * 0.11f;
                float y = 0.012f * i * i;
                Prim(PrimitiveType.Sphere, $"TailSeg_{i}", tail, new Vector3(0f, y, z), new Vector3(0.09f, 0.09f, 0.14f), i % 2 == 0 ? fur : stripe, false);
            }

            // Sword in the right hand. Pivot at the shoulder so swings arc around it.
            swordPivot = new GameObject("SwordPivot").transform;
            swordPivot.SetParent(body, false);
            swordPivot.localPosition = new Vector3(0.36f, 0.25f, 0.02f);
            swordPivot.localRotation = Quaternion.Euler(55f, 15f, 0f);
            Prim(PrimitiveType.Cube, "Grip", swordPivot, new Vector3(0f, 0f, 0.45f), new Vector3(0.04f, 0.04f, 0.16f), leather, false);
            Prim(PrimitiveType.Cube, "Guard", swordPivot, new Vector3(0f, 0f, 0.54f), new Vector3(0.22f, 0.04f, 0.04f), brass, false);
            Prim(PrimitiveType.Cube, "Blade", swordPivot, new Vector3(0f, 0f, 0.96f), new Vector3(0.06f, 0.015f, 0.8f), steel, false);

            return visual;
        }

        // ───────────────────────────── UI ─────────────────────────────

        static void BuildUI(GameObject player)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var healthBar = BuildBar(canvasGo.transform, "HealthBar", new Vector2(40f, -40f), new Vector2(440f, 22f),
                new Color(0.86f, 0.24f, 0.26f), new Color(1f, 0.85f, 0.45f, 0.9f));
            var staminaBar = BuildBar(canvasGo.transform, "StaminaBar", new Vector2(40f, -70f), new Vector2(340f, 14f),
                new Color(0.55f, 0.85f, 0.35f), new Color(0.9f, 0.95f, 0.6f, 0.6f));

            var hint = MakeText(canvasGo.transform, "ControlsHint", font, 20, TextAnchor.LowerLeft,
                "WASD move · Mouse look · Shift sprint · Ctrl walk · Space jump · Alt dodge · LMB attack (3-hit combo) · Esc pause");
            Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(1400f, 30f));
            hint.color = new Color(1f, 1f, 1f, 0.8f);

            var defeated = MakeText(canvasGo.transform, "DefeatedMessage", font, 48, TextAnchor.MiddleCenter, "You have fallen…\nThe path calls you back.");
            Anchor(defeated.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 200f));
            defeated.gameObject.SetActive(false);

            var hud = canvasGo.AddComponent<PlayerHUD>();
            Set(hud, "health", player.GetComponent<HealthComponent>());
            Set(hud, "stamina", player.GetComponent<StaminaComponent>());
            Set(hud, "healthBar", healthBar);
            Set(hud, "staminaBar", staminaBar);
            Set(hud, "defeatedMessage", defeated.gameObject);

            // Pause menu
            var pauseRoot = new GameObject("PauseMenu", typeof(RectTransform), typeof(Image));
            pauseRoot.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)pauseRoot.transform);
            pauseRoot.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.1f, 0.7f);

            var title = MakeText(pauseRoot.transform, "Title", font, 64, TextAnchor.MiddleCenter, "ARLEN\n<size=28>The Forgotten Paths — Paused</size>");
            title.supportRichText = true;
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(900f, 200f));

            var systems = new GameObject("GameSystems");
            var pause = systems.AddComponent<PauseController>();
            Set(pause, "input", player.GetComponent<PlayerInputReader>());
            Set(pause, "pauseMenuRoot", pauseRoot);

            var resume = MakeButton(pauseRoot.transform, "ResumeButton", font, "Resume", new Vector2(0f, -10f));
            UnityEventTools.AddPersistentListener(resume.onClick, pause.Resume);
            var quit = MakeButton(pauseRoot.transform, "QuitButton", font, "Quit", new Vector2(0f, -90f));
            UnityEventTools.AddPersistentListener(quit.onClick, pause.QuitGame);
            pauseRoot.SetActive(false);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static ResourceBar BuildBar(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor, Color trailColor)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            Anchor((RectTransform)root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var trail = new GameObject("Trail", typeof(RectTransform), typeof(Image));
            trail.transform.SetParent(root.transform, false);
            Stretch((RectTransform)trail.transform, 3f);
            trail.GetComponent<Image>().color = trailColor;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(root.transform, false);
            Stretch((RectTransform)fill.transform, 3f);
            fill.GetComponent<Image>().color = fillColor;

            var bar = root.AddComponent<ResourceBar>();
            Set(bar, "fill", fill.transform);
            Set(bar, "trail", trail.transform);
            Set(bar, "fillGraphic", fill.GetComponent<Image>());
            SetValue(bar, "normalColor", fillColor);
            return bar;
        }

        static Text MakeText(Transform parent, string name, Font font, int size, TextAnchor align, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.text = content;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            return t;
        }

        static Button MakeButton(Transform parent, string name, Font font, string label, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(320f, 64f));
            var img = go.GetComponent<Image>();
            img.color = new Color(0.16f, 0.42f, 0.4f, 0.95f);
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            var t = MakeText(go.transform, "Label", font, 30, TextAnchor.MiddleCenter, label);
            Stretch(t.rectTransform);
            return button;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, bool collider, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = localScale;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!collider)
            {
                // Small decorative parts don't need to cast shadows; saves draw calls on integrated GPUs.
                bool small = localScale.x < 0.3f && localScale.y < 0.3f && localScale.z < 0.3f;
                if (small) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            go.isStatic = false;
            return go;
        }

        /// <summary>Loads or creates a URP Lit material asset. Existing materials are reused (never recoloured).</summary>
        static Material Mat(string name, Color color, Color? emission = null)
        {
            if (MaterialCache.TryGetValue(name, out var cached) && cached != null) return cached;

            EnsureFolder(MaterialsFolder);
            string path = $"{MaterialsFolder}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogWarning("[Arlen] URP Lit shader not found; falling back to Standard. Is URP active?");
                    shader = Shader.Find("Standard");
                }
                mat = new Material(shader) { name = $"M_{name}" };
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_Color", color);
                mat.SetFloat("_Smoothness", 0.15f);
                mat.SetFloat("_Glossiness", 0.15f);
                if (emission.HasValue)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission.Value);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, path);
            }
            MaterialCache[name] = mat;
            return mat;
        }

        static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null) { Debug.LogError($"[Arlen] Property '{property}' not found on {target.GetType().Name}."); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null || !prop.isArray) { Debug.LogError($"[Arlen] Array '{property}' not found on {target.GetType().Name}."); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetValue(Object target, string property, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null) { Debug.LogError($"[Arlen] Property '{property}' not found on {target.GetType().Name}."); return; }
            switch (value)
            {
                case float f: prop.floatValue = f; break;
                case int i when prop.propertyType == SerializedPropertyType.Enum: prop.enumValueIndex = i; break;
                case int i: prop.intValue = i; break;
                case bool b: prop.boolValue = b; break;
                case Vector3 v: prop.vector3Value = v; break;
                case Color c: prop.colorValue = c; break;
                default: Debug.LogError($"[Arlen] Unsupported value type for '{property}'."); return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static bool AssetExists(string path) => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
