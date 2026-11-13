using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs.Editor
{
    public static class CafeNpcSetup
    {
        public const string ScenePath = "Assets/NPCs/Scenes/Cafe_NPC.unity";

        [MenuItem("MilkIt/NPCs/Open cafe with customers")]
        public static void Open()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Build();
        }

        public static void Build()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Cafe_NPC already exists; open it instead of overwriting it.");
            EnsureCharacterPrefabs(); // may open its own scene, so run it before opening Cafe_NPC
            if (!AssetDatabase.CopyAsset("Assets/Scenes/Cafe.unity", ScenePath))
                throw new InvalidOperationException("Could not copy Cafe.unity.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var building = GameObject.Find("Building").transform;
            var root = new GameObject("NPC Integration");
            root.AddComponent<PrototypeDesktopMode>();
            var system = root.AddComponent<CafeCustomerSystem>();
            system.customerPrefab = AssetDatabase.LoadAssetAtPath<CustomerController>("Assets/NPCs/Prefabs/Customer.prefab");
            system.entrance = Marker("Customer spawn outside", new Vector3(1.8f, 0, 13.5f), root.transform);
            system.exit = Marker("Customer exit outside", new Vector3(0.3f, 0, 13.5f), root.transform);
            system.entryWaypoint = Marker("Inside entrance", new Vector3(1.1f, 0, 8.2f), root.transform);
            ConfigureQueue(system, root.transform);
            ConfigureTables(system, building, root.transform);
            ConfigureCharacters(system);

            var left = building.Find("WallFront/Entrance/Left_Door (1)");
            var right = building.Find("WallFront/Entrance/Right_Door (1)");
            var door = new GameObject("Automatic sliding entrance").AddComponent<CafeSlidingDoor>();
            door.transform.SetParent(root.transform);
            door.transform.position = new Vector3(1.112f, 0, 9.775f);
            door.customers = system;
            door.leftPanel = left;
            door.rightPanel = right;
            door.leftOpenOffset = left.parent.InverseTransformVector(Vector3.right * 1.82f);
            door.rightOpenOffset = right.parent.InverseTransformVector(Vector3.left * 1.82f);
            system.entranceDoor = door;
            foreach (var panel in new[] { left, right })
            {
                var bounds = panel.GetComponent<MeshFilter>().sharedMesh.bounds;
                var collider = panel.gameObject.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;
            }

            // Conservative furniture volumes give reliable paths around imported decorative meshes.
            var sources = new List<NavMeshBuildSource>();
            sources.Add(Box(new Bounds(new Vector3(0.95f, -0.1f, 4.9f), new Vector3(11, 0.2f, 10)), 0));
            var outsideBounds = new Bounds(new Vector3(1.1f, -0.1f, 12), new Vector3(3.5f, 0.2f, 5));
            sources.Add(Box(outsideBounds, 0));
            foreach (var renderer in building.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.transform.IsChildOf(building.Find("WallFront")) &&
                    !renderer.transform.IsChildOf(building.Find("WallBack")) &&
                    !renderer.transform.IsChildOf(building.Find("WallLeft")) &&
                    !renderer.transform.IsChildOf(building.Find("WallLeft (1)"))) continue;
                if (renderer.transform == left || renderer.transform == right) continue;
                if (renderer.bounds.min.y > 1.8f) continue;
                sources.Add(Box(renderer.bounds, 1));
            }
            foreach (Transform booth in building.Find("Booths")) sources.Add(Box(RenderBounds(booth), 1));
            foreach (Transform item in building.Find("shop and objects model"))
                if (item.position.y < 0.1f) sources.Add(Box(RenderBounds(item), 1));

            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.3f;
            settings.agentHeight = 1.8f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.06f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(new Vector3(1, 1, 7), new Vector3(16, 8, 20)), Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("Cafe NavMesh bake failed.");
            AssetDatabase.CreateAsset(data, "Assets/NPCs/Scenes/Cafe_NPC_NavMesh.asset");
            root.AddComponent<CafeNavigation>().bakedData = data;
            var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apron.name = "Entrance walkway";
            apron.transform.SetParent(root.transform);
            apron.transform.position = outsideBounds.center;
            apron.transform.localScale = outsideBounds.size;
            apron.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/NPCs/Materials/Floor.mat");

            // This integration scene uses an observation camera. The source Cafe scene stays in VR.
            foreach (var originalRoot in scene.GetRootGameObjects())
                if (originalRoot.name.StartsWith("XR Origin"))
                {
                    door.playerHead = originalRoot.GetComponentInChildren<Camera>(true).transform;
                    originalRoot.SetActive(false);
                }
            building.Find("Ceiling").gameObject.SetActive(false);
            var camera = new GameObject("NPC Observation Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(1, 18, 7), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 9;
            camera.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CAFE_NPC_READY: booth tables, till queue, outside walkway and automatic doors.");
        }

        // Upgrades an existing Cafe_NPC scene in place: all booths become 4-seat tables, the queue gets
        // room for a party of four, and customers use the generated character visuals.
        [MenuItem("MilkIt/NPCs/Update cafe tables and characters")]
        public static void UpdateTablesAndCharacters()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) { Build(); return; }
            EnsureCharacterPrefabs();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var system = UnityEngine.Object.FindFirstObjectByType<CafeCustomerSystem>()
                         ?? throw new InvalidOperationException("Cafe_NPC has no CafeCustomerSystem.");
            var building = GameObject.Find("Building").transform;
            ConfigureQueue(system, system.transform);
            ConfigureTables(system, building, system.transform);
            ConfigureCharacters(system);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"CAFE_NPC_UPDATED: {system.tables.Length} tables, {system.seats.Length} seats, {system.queuePositions.Length} queue spots.");
        }

        static readonly Vector3[] QueueSpots =
        {
            new Vector3(3.27f, 0, 3.5f), new Vector3(3.27f, 0, 4.8f), new Vector3(3.2f, 0, 6.1f),
            new Vector3(2.85f, 0, 7.3f), new Vector3(2.3f, 0, 8.4f),
        };

        static void ConfigureQueue(CafeCustomerSystem system, Transform root)
        {
            var names = new[] { "Order at till", "Queue 2", "Queue 3", "Queue 4", "Queue 5" };
            var markers = new Transform[QueueSpots.Length];
            for (var i = 0; i < markers.Length; i++)
            {
                var existing = system.queuePositions != null && i < system.queuePositions.Length ? system.queuePositions[i] : null;
                markers[i] = existing != null ? existing : Marker(names[i], QueueSpots[i], root);
                markers[i].position = QueueSpots[i];
                markers[i].rotation = Quaternion.Euler(0, 180, 0);
            }
            system.queuePositions = markers;
            // One-way flow: leave the till west along the counter, while the queue arrives from the north
            // through the narrow gap between the condiment bar and the queue rail.
            if (system.tillExit == null) system.tillExit = Marker("Leave till", Vector3.zero, root);
            system.tillExit.position = new Vector3(0.3f, 0, 3.7f);
        }

        // Booth geometry (metres, from the booth's centre). Benches sit at either end along Z and face the
        // table; the sitting clip puts the hips HipsBehindRoot behind the character root.
        const float BenchOffset = 0.5f, HipsBehindRoot = 0.3f, SeatSideOffset = 0.33f, AisleOffset = 1.14f;

        static void ConfigureTables(CafeCustomerSystem system, Transform building, Transform root)
        {
            // Remove the original three single seats and any previous table setup.
            foreach (var old in root.GetComponentsInChildren<CustomerSeat>(true))
                if (old != null && old.transform.parent == root) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var previous = root.Find("Customer tables");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var tablesRoot = new GameObject("Customer tables").transform;
            tablesRoot.SetParent(root);
            var tables = new List<CafeTable>();
            var seats = new List<CustomerSeat>();
            foreach (Transform booth in building.Find("Booths"))
            {
                var centre = booth.position;
                var aisle = centre.x < 1f ? 1f : -1f; // left-wall booths open toward +X
                var table = new GameObject("Table " + booth.name).AddComponent<CafeTable>();
                table.transform.SetParent(tablesRoot);
                table.transform.position = new Vector3(centre.x, 0, centre.z);
                var outer = new CustomerSeat[2];
                var inner = new CustomerSeat[2];
                for (var bench = 0; bench < 2; bench++)
                {
                    var side = bench == 0 ? -1f : 1f; // bench 0 is on the -Z end and faces +Z
                    var facing = Quaternion.Euler(0, bench == 0 ? 0 : 180, 0);
                    var rootZ = centre.z + side * (BenchOffset - HipsBehindRoot);
                    var approach = new Vector3(centre.x + aisle * AisleOffset, 0, centre.z + side * 0.45f);
                    outer[bench] = Seat(table, $"Bench {bench + 1} aisle", approach,
                        new Vector3(centre.x + aisle * SeatSideOffset, 0, rootZ), facing);
                    inner[bench] = Seat(table, $"Bench {bench + 1} wall", approach,
                        new Vector3(centre.x - aisle * SeatSideOffset, 0, rootZ), facing);
                    inner[bench].blockedBy = outer[bench];
                }
                // Aisle seats first: pairs face each other; larger parties also use the wall seats.
                table.seats = new[] { outer[0], outer[1], inner[0], inner[1] };
                tables.Add(table);
                seats.AddRange(table.seats);
            }
            system.tables = tables.ToArray();
            system.seats = seats.ToArray();
        }

        static CustomerSeat Seat(CafeTable table, string name, Vector3 approach, Vector3 sit, Quaternion facing)
        {
            var seat = new GameObject(name).AddComponent<CustomerSeat>();
            seat.transform.SetParent(table.transform);
            seat.table = table;
            seat.approach = Marker("Approach", approach, seat.transform);
            seat.sittingPose = Marker("Sitting pose", sit, seat.transform);
            seat.sittingPose.rotation = facing;
            return seat;
        }

        static void EnsureCharacterPrefabs()
        {
            foreach (var c in CharacterPreviewSetup.Characters)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPreviewSetup.PrefabPath(c));
                var animator = prefab != null ? prefab.GetComponentInChildren<Animator>() : null;
                if (animator == null || !animator.isHuman) { CharacterPreviewSetup.Build(false); return; }
            }
        }

        static void ConfigureCharacters(CafeCustomerSystem system)
        {
            var prefabs = new List<GameObject>();
            foreach (var c in CharacterPreviewSetup.Characters)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPreviewSetup.PrefabPath(c));
                if (prefab != null) prefabs.Add(prefab);
            }
            system.characterPrefabs = prefabs.ToArray();
        }

        static NavMeshBuildSource Box(Bounds bounds, int area) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box,
            transform = Matrix4x4.Translate(bounds.center), size = bounds.size, area = area
        };

        static Bounds RenderBounds(Transform parent)
        {
            var renderers = parent.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No geometry in " + parent.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static Transform Marker(string name, Vector3 position, Transform parent)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent);
            point.position = position;
            return point;
        }
    }
}
