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
            system.queuePositions = new[]
            {
                Marker("Order at till", new Vector3(3.27f, 0, 3.5f), root.transform),
                Marker("Queue 2", new Vector3(3.27f, 0, 4.8f), root.transform),
                Marker("Queue 3", new Vector3(3.27f, 0, 6.1f), root.transform)
            };
            foreach (var point in system.queuePositions) point.rotation = Quaternion.Euler(0, 180, 0);
            system.seats = new CustomerSeat[3];
            string[] booths = { "booth (3)", "booth (2)", "booth (1)" };
            for (int i = 0; i < booths.Length; i++)
            {
                var booth = building.Find("Booths/" + booths[i]);
                var seat = new GameObject("Customer seat " + (i + 1)).AddComponent<CustomerSeat>();
                seat.transform.SetParent(root.transform);
                seat.approach = Marker("Walk beside booth", new Vector3(-2.1f, 0, booth.position.z - 0.45f), seat.transform);
                seat.sittingPose = Marker("Sit on bench", new Vector3(-3.1f, 1.1f, booth.position.z - 0.48f), seat.transform);
                system.seats[i] = seat;
            }

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
            Debug.Log("CAFE_NPC_READY: three booth seats, till queue, outside walkway and automatic doors.");
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
