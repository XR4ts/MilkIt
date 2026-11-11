using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MilkIt.NPCs.Editor
{
    public static class CustomerPrototypeBuilder
    {
        public const string ScenePath = "Assets/NPCs/Scenes/NPC_Test.unity";

        [MenuItem("MilkIt/NPCs/Open customer test scene")]
        public static void Open()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Used in an isolated Unity project to generate and validate the delivered scene.
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.IO.Directory.CreateDirectory("Assets/NPCs/Scenes");
            System.IO.Directory.CreateDirectory("Assets/NPCs/Materials");
            System.IO.Directory.CreateDirectory("Assets/NPCs/Prefabs");
            AssetDatabase.Refresh();
            var floorMaterial = Material("Floor", new Color(0.13f, 0.20f, 0.23f));
            var furnitureMaterial = Material("Furniture", new Color(0.65f, 0.40f, 0.21f));
            var markerMaterial = Material("Markers", new Color(0.15f, 0.8f, 0.7f));
            var customerMaterial = Material("Customer", new Color(0.96f, 0.68f, 0.25f));
            var room = new GameObject("Test Cafe");
            room.AddComponent<PrototypeDesktopMode>();
            var floor = Box("Floor", new Vector3(0, -0.15f, 0), new Vector3(16, 0.3f, 14), floorMaterial, room.transform);
            var obstacles = new List<BoxCollider>();
            obstacles.Add(Box("Counter", new Vector3(-4, 0.6f, 3.8f), new Vector3(3, 1.2f, 1), furnitureMaterial, room.transform));
            var markers = new GameObject("Customer route markers").transform;
            var system = new GameObject("Customer System").AddComponent<CafeCustomerSystem>();
            system.entrance = Marker("Entrance", new Vector3(-4, 0, -5), markers);
            system.exit = Marker("Exit", new Vector3(-6, 0, -5), markers);
            system.queuePositions = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var position = new Vector3(-4, 0, 2.4f - i * 1.5f);
                system.queuePositions[i] = Marker(i == 0 ? "Order point" : $"Queue {i}", position, markers);
                Box($"Queue marker {i}", position + Vector3.up * 0.015f, new Vector3(0.65f, 0.02f, 0.65f), markerMaterial, markers);
            }
            system.seats = new CustomerSeat[3];
            for (int i = 0; i < 3; i++)
            {
                float z = -3f + i * 3.5f;
                obstacles.Add(Box($"Table {i + 1}", new Vector3(3.2f, 0.65f, z), new Vector3(1.4f, 1.3f, 1.4f), furnitureMaterial, room.transform));
                obstacles.Add(Box($"Chair {i + 1}", new Vector3(1.8f, 0.25f, z), new Vector3(0.7f, 0.5f, 0.7f), furnitureMaterial, room.transform));
                var seat = new GameObject($"Seat {i + 1}").AddComponent<CustomerSeat>();
                seat.transform.SetParent(markers);
                seat.approach = Marker("Approach", new Vector3(0.7f, 0, z), seat.transform);
                seat.sittingPose = Marker("Sitting pose", new Vector3(1.8f, 1.1f, z), seat.transform);
                seat.sittingPose.rotation = Quaternion.Euler(0, 90, 0);
                system.seats[i] = seat;
            }
            var nav = room.AddComponent<PrototypeNavigation>();
            nav.floor = floor;
            nav.obstacles = obstacles.ToArray();

            var customer = new GameObject("Customer");
            var agent = customer.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 2f;
            agent.speed = 2.2f;
            agent.acceleration = 10f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.12f;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Placeholder body";
            body.transform.SetParent(customer.transform);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(0.6f, 1, 0.6f);
            body.GetComponent<Renderer>().sharedMaterial = customerMaterial;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            customer.AddComponent<CustomerController>().body = body.transform;
            system.customerPrefab = PrefabUtility.SaveAsPrefabAsset(customer, "Assets/NPCs/Prefabs/Customer.prefab").GetComponent<CustomerController>();
            UnityEngine.Object.DestroyImmediate(customer);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(11, 16, -16);
            camera.transform.LookAt(new Vector3(-0.8f, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 10.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.09f, 0.12f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Daylight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = Color.white;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("NPC_PROTOTYPE_BUILT");
        }

        static Material Material(string name, Color color)
        {
            string path = $"Assets/NPCs/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // Built-in unlit shader also renders in URP; no imported art or package dependency.
                material = new Material(Shader.Find("Unlit/Color"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            return material;
        }

        static Transform Marker(string name, Vector3 position, Transform parent)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent);
            marker.position = position;
            return marker;
        }

        static BoxCollider Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box.GetComponent<BoxCollider>();
        }
    }
}
