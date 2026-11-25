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

        public const string MainScenePath = "Assets/Scenes/Cafe.unity";

        public static void Build()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Cafe_NPC already exists; open it instead of overwriting it.");
            EnsureCharacterPrefabs(); // may open its own scene, so run it before opening Cafe_NPC
            if (!AssetDatabase.CopyAsset(MainScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy Cafe.unity.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Install(scene, testScene: true);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CAFE_NPC_READY: booth tables, till queue, outside walkway and automatic doors.");
        }

        // Adds (or rebuilds) the customers in the real café scene, keeping its VR rig. Safe to run again.
        [MenuItem("MilkIt/NPCs/Add customers to main cafe (Cafe.unity)")]
        public static void InstallInMainScene()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureCharacterPrefabs();
            var scene = EditorSceneManager.OpenScene(MainScenePath);
            foreach (var old in scene.GetRootGameObjects())
                if (old.name == RootName) UnityEngine.Object.DestroyImmediate(old);
            var system = Install(scene, testScene: false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"CAFE_MAIN_READY: {system.tables.Length} tables, {system.seats.Length} seats, {system.drinks.Length} drinks. Flip the OPEN sign on the door to start.");
        }

        const string RootName = "NPC Integration";

        // testScene: observation camera, XR rig off, grey walkway, no click-to-order (used by Cafe_NPC and the
        // route validation). Main scene: VR rig stays, customers use the real sidewalk and wait to be clicked.
        static CafeCustomerSystem Install(UnityEngine.SceneManagement.Scene scene, bool testScene)
        {
            var building = GameObject.Find("Building").transform;
            var root = new GameObject(RootName);
            if (testScene) root.AddComponent<PrototypeDesktopMode>();
            var system = root.AddComponent<CafeCustomerSystem>();
            system.customerPrefab = AssetDatabase.LoadAssetAtPath<CustomerController>("Assets/NPCs/Prefabs/Customer.prefab");
            // The main scene's sidewalk runs from the door out to the street, so customers arrive from its far end.
            system.entrance = Marker("Customer spawn outside", testScene ? new Vector3(1.8f, 0, 13.5f) : new Vector3(1.9f, 0, 22.5f), root.transform);
            system.exit = Marker("Customer exit outside", testScene ? new Vector3(0.3f, 0, 13.5f) : new Vector3(0.6f, 0, 22.5f), root.transform);
            system.entryWaypoint = Marker("Inside entrance", new Vector3(1.1f, 0, 8.2f), root.transform);
            ConfigureQueue(system, root.transform);
            ConfigureTables(system, building, root.transform);
            ConfigureCharacters(system);
            system.patienceIndicatorPrefab = BuildPatienceIndicator();
            ConfigureMenu(system);
            system.orderBubblePrefab = BuildOrderBubble();
            system.requireOrderClick = !testScene;
            system.showDebugPanel = true;
            system.fullDebugPanel = testScene; // the main scene only shows the Serve buttons
            var service = root.AddComponent<CafeService>();
            root.AddComponent<NpcDesktopClicker>().cafe = system;

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
                if (panel.GetComponent<BoxCollider>() != null) continue;
                var bounds = panel.GetComponent<MeshFilter>().sharedMesh.bounds;
                var collider = panel.gameObject.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
            BuildOpenSign(service, left, root.transform);

            // Conservative furniture volumes give reliable paths around imported decorative meshes.
            var sources = new List<NavMeshBuildSource>();
            sources.Add(Box(new Bounds(new Vector3(0.95f, -0.1f, 4.9f), new Vector3(11, 0.2f, 10)), 0));
            var outsideBounds = testScene
                ? new Bounds(new Vector3(1.1f, -0.1f, 12), new Vector3(3.5f, 0.2f, 5))
                : new Bounds(new Vector3(1.27f, -0.1f, 17.2f), new Vector3(3.5f, 0.2f, 14.6f)); // the sidewalk
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
                new Bounds(new Vector3(1, 1, 12), new Vector3(16, 8, 30)), Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("Cafe NavMesh bake failed.");
            var navPath = testScene ? "Assets/NPCs/Scenes/Cafe_NPC_NavMesh.asset" : "Assets/NPCs/Scenes/Cafe_Main_NavMesh.asset";
            AssetDatabase.CreateAsset(data, navPath);
            root.AddComponent<CafeNavigation>().bakedData = data;

            Camera xrCamera = null;
            foreach (var originalRoot in scene.GetRootGameObjects())
                if (originalRoot.name.StartsWith("XR Origin"))
                {
                    xrCamera = originalRoot.GetComponentInChildren<Camera>(true);
                    door.playerHead = xrCamera != null ? xrCamera.transform : null;
                    if (testScene) originalRoot.SetActive(false);
                }
            if (!testScene) return system;

            var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apron.name = "Entrance walkway";
            apron.transform.SetParent(root.transform);
            apron.transform.position = outsideBounds.center;
            apron.transform.localScale = outsideBounds.size;
            apron.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/NPCs/Materials/Floor.mat");
            // This test scene uses an observation camera. The real Cafe scene stays in VR.
            building.Find("Ceiling").gameObject.SetActive(false);
            var camera = new GameObject("NPC Observation Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(1, 18, 7), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 9;
            camera.gameObject.AddComponent<AudioListener>();
            return system;
        }

        // Drinks from the coffee proportions poster, with prices and the poster's picture of each.
        static readonly (string name, string file, int price)[] Menu =
        {
            ("Espresso", "Espresso", 3), ("Espresso Macchiato", "EspressoMacchiato", 3),
            ("Americano", "Americano", 3), ("Cortado", "Cortado", 4),
            ("Flat White", "FlatWhite", 4), ("Cappuccino", "Cappuccino", 4),
            ("Caffè Latte", "CaffeLatte", 5), ("Caffè Mocha", "CaffeMocha", 5),
        };

        const string PosterPath = "Assets/Textures/Posters/CoffeeProportions.png";
        const string IconFolder = "Assets/NPCs/Orders/Icons";

        static void ConfigureMenu(CafeCustomerSystem system)
        {
            system.drinks = new string[Menu.Length];
            system.drinkPrices = new int[Menu.Length];
            for (var i = 0; i < Menu.Length; i++) { system.drinks[i] = Menu[i].name; system.drinkPrices[i] = Menu[i].price; }
            system.drinkIcons = BuildDrinkIcons();
        }

        // Cuts each drink's card out of the poster (4 x 2 grid) into its own small texture.
        static Texture2D[] BuildDrinkIcons()
        {
            if (!File.Exists(PosterPath)) { Debug.LogWarning("Coffee poster not found; orders show text only."); return new Texture2D[0]; }
            var poster = new Texture2D(2, 2);
            poster.LoadImage(File.ReadAllBytes(PosterPath));
            // Card edges measured on the poster at 2000 px wide. Only the cup part of each card is kept (below
            // the title, down to the cup-type label); the name is shown as real 3D text instead.
            var scale = poster.width / 2000f;
            float[] columns = { 67, 540, 1013, 1487 };
            float[] rows = { 172 + 110, 772 + 110 };
            const float cardWidth = 446, cardHeight = 425;
            const int outWidth = 512, outHeight = 488;
            EnsureFolder(IconFolder);
            var pixels = poster.GetPixels();
            var icons = new Texture2D[Menu.Length];
            for (var i = 0; i < Menu.Length; i++)
            {
                var left = columns[i % 4] * scale;
                var top = rows[i / 4] * scale;
                var icon = new Texture2D(outWidth, outHeight, TextureFormat.RGB24, false);
                for (var y = 0; y < outHeight; y++)
                for (var x = 0; x < outWidth; x++)
                {
                    // Average the block of poster pixels behind each output pixel (a box filter), so the
                    // downscale keeps text and outlines crisp instead of picking single pixels.
                    var x0 = left + (float)x / outWidth * cardWidth * scale;
                    var x1 = left + (float)(x + 1) / outWidth * cardWidth * scale;
                    var y0 = poster.height - (top + (1f - (float)y / outHeight) * cardHeight * scale);
                    var y1 = poster.height - (top + (1f - (float)(y + 1) / outHeight) * cardHeight * scale);
                    var sum = Color.clear;
                    var count = 0;
                    for (var sy = Mathf.FloorToInt(y0); sy < Mathf.CeilToInt(y1); sy++)
                    for (var sx = Mathf.FloorToInt(x0); sx < Mathf.CeilToInt(x1); sx++)
                    {
                        sum += pixels[Mathf.Clamp(sy, 0, poster.height - 1) * poster.width + Mathf.Clamp(sx, 0, poster.width - 1)];
                        count++;
                    }
                    icon.SetPixel(x, y, count > 0 ? sum / count : Color.black);
                }
                var path = $"{IconFolder}/{Menu[i].file}.png";
                File.WriteAllBytes(path, icon.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(icon);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.npotScale = TextureImporterNPOTScale.None; // keep the card's proportions (no resize to 512x512)
                importer.SaveAndReimport();
                icons[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            UnityEngine.Object.DestroyImmediate(poster);
            return icons;
        }

        const string BubblePath = "Assets/NPCs/Prefabs/OrderBubble.prefab";

        static CustomerOrderBubble BuildOrderBubble()
        {
            var root = new GameObject("Order bubble");
            var bubble = root.AddComponent<CustomerOrderBubble>();
            var panel = new GameObject("Panel").transform;
            panel.SetParent(root.transform, false);
            // Dark card like the poster's, with a light border and tail.
            var light = Unlit("BubbleBackground", new Color(0.96f, 0.95f, 0.92f));
            Block("Border", panel, new Vector3(0, 0.27f, 0.012f), new Vector3(0.47f, 0.5f, 1f), PrimitiveType.Quad, light);
            Block("Tail", panel, new Vector3(0, 0.02f, 0.012f), new Vector3(0.07f, 0.07f, 1f), PrimitiveType.Quad, light)
                .transform.localRotation = Quaternion.Euler(0, 0, 45);
            Block("Card", panel, new Vector3(0, 0.27f, 0.006f), new Vector3(0.45f, 0.48f, 1f), PrimitiveType.Quad, Unlit("BubbleCard", new Color(0.12f, 0.14f, 0.18f)));
            var image = Block("Drink", panel, new Vector3(0, 0.225f, 0), new Vector3(0.38f, 0.362f, 1f), PrimitiveType.Quad, Unlit("BubbleImage", Color.white));
            var nameLabel = Text("Drink name", panel, new Vector3(0, 0.455f, -0.005f), 0.0068f, TextAnchor.MiddleCenter, Color.white);
            var label = Text("Question", panel, new Vector3(0, 0.27f, -0.005f), 0.02f, TextAnchor.MiddleCenter, Color.white);
            bubble.panel = panel.gameObject;
            bubble.image = image.GetComponent<Renderer>();
            bubble.label = label;
            bubble.nameLabel = nameLabel;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BubblePath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<CustomerOrderBubble>();
        }

        // OPEN / CLOSED sign hanging on the inside of the left entrance door at eye height.
        static void BuildOpenSign(CafeService service, Transform doorPanel, Transform root)
        {
            var panelBounds = doorPanel.GetComponent<Renderer>().bounds;
            var sign = new GameObject("Open-closed sign");
            sign.transform.SetParent(root, true);
            sign.transform.position = new Vector3(panelBounds.center.x, 1.45f, panelBounds.min.z - 0.03f);
            var collider = sign.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.48f, 0.26f, 0.04f);
            sign.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var board = new GameObject("Board").transform;
            board.SetParent(sign.transform, false);
            Block("Plate", board, Vector3.zero, new Vector3(0.46f, 0.24f, 0.015f), PrimitiveType.Cube, Unlit("SignPlate", new Color(0.97f, 0.95f, 0.88f)));
            Block("Border", board, Vector3.zero, new Vector3(0.48f, 0.26f, 0.012f), PrimitiveType.Cube, Unlit("SignBorder", new Color(0.25f, 0.18f, 0.12f)));
            Block("String L", sign.transform, new Vector3(-0.1f, 0.2f, 0), new Vector3(0.006f, 0.16f, 0.006f), PrimitiveType.Cube, Unlit("SignBorder", new Color(0.25f, 0.18f, 0.12f)))
                .transform.localRotation = Quaternion.Euler(0, 0, -32);
            Block("String R", sign.transform, new Vector3(0.1f, 0.2f, 0), new Vector3(0.006f, 0.16f, 0.006f), PrimitiveType.Cube, Unlit("SignBorder", new Color(0.25f, 0.18f, 0.12f)))
                .transform.localRotation = Quaternion.Euler(0, 0, 32);
            var faces = new TextMesh[2];
            for (var i = 0; i < 2; i++)
            {
                var face = new GameObject(i == 0 ? "Text inside" : "Text outside");
                face.transform.SetParent(board, false);
                face.transform.localPosition = new Vector3(0, 0, i == 0 ? -0.009f : 0.009f);
                face.transform.localRotation = Quaternion.Euler(0, i == 0 ? 0 : 180, 0);
                var text = face.AddComponent<TextMesh>();
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.font = font;
                face.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                text.fontSize = 100;
                text.characterSize = 0.0095f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontStyle = FontStyle.Bold;
                faces[i] = text;
            }
            var component = sign.AddComponent<CafeOpenSign>();
            component.service = service;
            component.board = board;
            component.insideFace = faces[0];
            component.outsideFace = faces[1];
            component.attachTo = doorPanel;
            // Same as the component sets at runtime, so the sign also reads right in the editor.
            faces[0].text = "OPEN"; faces[0].color = component.openColor;
            faces[1].text = "CLOSED"; faces[1].color = component.closedColor;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
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
            system.patienceIndicatorPrefab = BuildPatienceIndicator();
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

        const string IndicatorPath = "Assets/NPCs/Prefabs/PatienceIndicator.prefab";

        // Marker above a customer's head: cup icon + patience bar, and a text label for "!", seconds and payments.
        public static CustomerPatienceIndicator BuildPatienceIndicator()
        {
            var root = new GameObject("Patience indicator");
            var indicator = root.AddComponent<CustomerPatienceIndicator>();
            var waiting = new GameObject("Waiting").transform;
            waiting.SetParent(root.transform, false);
            // The ordered drink's poster picture, left of the bar (set per customer).
            var drinkIcon = Block("Drink", waiting, new Vector3(-0.4f, 0, 0), new Vector3(0.21f, 0.2f, 1f), PrimitiveType.Quad, Unlit("BubbleImage", Color.white));
            drinkIcon.SetActive(false);
            var drinkName = Text("Drink name", waiting, new Vector3(-0.24f, -0.065f, -0.01f), 0.0065f, TextAnchor.UpperLeft, Color.white);
            Block("Bar back", waiting, new Vector3(0.05f, 0, 0.005f), new Vector3(0.58f, 0.1f, 0.02f), PrimitiveType.Cube, Unlit("IndicatorBack", new Color(0.12f, 0.12f, 0.14f)));
            var pivot = new GameObject("Fill pivot").transform;
            pivot.SetParent(waiting, false);
            pivot.localPosition = new Vector3(-0.21f, 0, -0.01f);
            var fill = Block("Fill", pivot, new Vector3(0.26f, 0, 0), new Vector3(0.52f, 0.065f, 0.02f), PrimitiveType.Cube, Unlit("IndicatorFill", Color.white));

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0.05f, 0.1f, -0.02f);
            var label = labelObject.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.fontSize = 96;
            label.characterSize = 0.022f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.fontStyle = FontStyle.Bold;
            label.text = "";

            indicator.waitingGroup = waiting.gameObject;
            indicator.fillPivot = pivot;
            indicator.fillRenderer = fill.GetComponent<Renderer>();
            indicator.drinkIcon = drinkIcon.GetComponent<Renderer>();
            indicator.drinkName = drinkName;
            indicator.label = label;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, IndicatorPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<CustomerPatienceIndicator>();
        }

        static TextMesh Text(string name, Transform parent, Vector3 position, float characterSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.fontSize = 100;
            text.characterSize = characterSize;
            text.anchor = anchor;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.text = "";
            return text;
        }

        static GameObject Block(string name, Transform parent, Vector3 position, Vector3 scale, PrimitiveType shape, Material material)
        {
            var block = GameObject.CreatePrimitive(shape);
            block.name = name;
            UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            var renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return block;
        }

        static Material Unlit(string name, Color color)
        {
            var path = $"Assets/NPCs/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
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
