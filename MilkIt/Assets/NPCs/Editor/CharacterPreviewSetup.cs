using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilkIt.NPCs.Editor
{
    // Builds reusable Humanoid visual prefabs for the Quaternius characters, driven by clips from the
    // Universal Animation Library, plus a standalone preview scene.
    // Does not touch the customer system, Cafe.unity or Cafe_NPC.unity.
    public static class CharacterPreviewSetup
    {
        public const string Root = "Assets/NPCs/Characters/Quaternius";
        public const string Generated = Root + "/Generated";
        public const string AnimationsFbx = Root + "/Animations/UAL1_Standard.fbx";
        public const string ScenePath = "Assets/NPCs/Scenes/NPC_CharacterPreview.unity";
        public const float TargetHeight = 1.75f;
        public const float SeatHeight = 0.45f;
        public const string ArmLayer = "Arms";
        public const string CupName = "Cup";

        public struct CharacterSource
        {
            public string id, label, fbx;
            public float height;
            public Vector3 cupPosition, cupEuler; // in the right hand's local axes, metres
        }

        // Cup grip found per rig; all outfits of a pack share that pack's skeleton.
        static readonly Vector3 ManCup = new Vector3(0.010f, 0.159f, -0.024f), ManCupEuler = new Vector3(310f, 10f, 240f);
        static readonly Vector3 WomanCup = new Vector3(0.011f, 0.137f, -0.030f), WomanCupEuler = new Vector3(307f, 3f, 246f);

        static CharacterSource Man(string id, string outfit, string label, float height) => new CharacterSource
            { id = id, label = label, fbx = $"{Root}/Men/{outfit}_Humanoid.fbx", height = height, cupPosition = ManCup, cupEuler = ManCupEuler };

        static CharacterSource Woman(string id, string outfit, string label, float height) => new CharacterSource
            { id = id, label = label, fbx = $"{Root}/Women/{outfit}_Humanoid.fbx", height = height, cupPosition = WomanCup, cupEuler = WomanCupEuler };

        // The "_Humanoid" rigs are the packs' retargeting variants; the animated variants are not
        // valid Unity Humanoids. Ids of the first two are kept so existing references stay valid.
        public static readonly CharacterSource[] Characters =
        {
            Man("Man_CasualHoodie", "Casual", "Hoodie", 1.78f),
            Man("Man_Casual2", "Casual2", "Casual", 1.74f),
            Man("Man_Suit", "Suit", "Suit", 1.82f),
            Man("Man_Worker", "Worker", "Worker", 1.80f),
            Man("Man_Punk", "Punk", "Punk", 1.76f),
            Man("Man_Beach", "Beach", "Beach", 1.84f),
            Woman("Woman_Casual", "Casual", "Casual", 1.68f),
            Woman("Woman_Formal", "Formal", "Formal", 1.70f),
            Woman("Woman_Suit", "Suit", "Suit", 1.72f),
            Woman("Woman_Worker", "Worker", "Worker", 1.66f),
            Woman("Woman_Punk", "Punk", "Punk", 1.64f),
            Woman("Woman_Adventurer", "Adventurer", "Adventurer", 1.70f),
        };

        // Animator state -> library clip. Order is the preview panel order.
        static readonly (string state, string clip)[] BaseStates =
        {
            ("Idle", "Idle_Loop"), ("Walk", "Walk_Loop"), ("Talk", "Idle_Talking_Loop"),
            ("Interact", "Interact"), ("PickUp", "PickUp_Table"),
            ("SitDown", "Sitting_Enter"), ("SitIdle", "Sitting_Idle_Loop"),
            ("SitTalk", "Sitting_Talking_Loop"), ("StandUp", "Sitting_Exit"),
        };

        // One-shot state -> state it continues into.
        static readonly Dictionary<string, string> FollowUps = new Dictionary<string, string>
        {
            { "Interact", "Idle" }, { "PickUp", "Idle" }, { "SitDown", "SitIdle" }, { "StandUp", "Idle" },
        };

        public static readonly string[] PreviewStates =
            BaseStates.Select(s => s.state).Concat(new[] { "Drink" }).ToArray();

        public static string PrefabPath(CharacterSource c) => $"{Generated}/{c.id}.prefab";
        static string ControllerPath(CharacterSource c) => $"{Generated}/{c.id}.controller";
        static string DrinkClipPath => Generated + "/Drink.anim";
        static string ArmMaskPath => Generated + "/RightArm.mask";

        [MenuItem("MilkIt/NPCs/Open character animation preview")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (NeedsBuild()) Build(true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("MilkIt/NPCs/Rebuild character preview assets")]
        public static void RebuildPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // True when assets are missing or were generated by the older Generic-rig version.
        static bool NeedsBuild()
        {
            if (!File.Exists(ScenePath)) return true;
            foreach (var c in Characters)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(c));
                if (controller == null || controller.layers.Length < 2 ||
                    controller.layers[0].stateMachine.states.All(s => s.state.name != "SitIdle")) return true;
            }
            return false;
        }

        // Regenerates importer settings, materials, clips, controllers and prefabs. The scene is
        // overwritten only when rebuildScene is true. Leaves a new empty scene open when it writes one.
        public static void Build(bool rebuildScene)
        {
            EnsureFolder(Generated);
            EnsureFolder(Generated + "/Materials");
            var clips = ConfigureAnimations();
            var drink = BuildDrinkClip();
            var mask = BuildArmMask();
            foreach (var c in Characters)
            {
                ConfigureModel(c);
                var controller = BuildController(c, clips, drink, mask);
                BuildPrefab(c, controller);
            }
            AssetDatabase.SaveAssets();
            if (rebuildScene || !File.Exists(ScenePath)) BuildScene();
        }

        static Dictionary<string, AnimationClip> ConfigureAnimations()
        {
            var importer = AssetImporter.GetAtPath(AnimationsFbx) as ModelImporter
                           ?? throw new InvalidOperationException($"Animation library not found: {AnimationsFbx}");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = ShortName(clip.name);
                clip.loopTime = clip.name.EndsWith("_Loop", StringComparison.Ordinal);
                // Bake all root movement into the pose: characters stay where the customer logic puts them.
                clip.lockRootRotation = clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = clip.keepOriginalPositionXZ = true;
                clip.rotationOffset = 180f; // library clips face -Z; prefabs face +Z
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            var all = AssetDatabase.LoadAllAssetsAtPath(AnimationsFbx).OfType<AnimationClip>()
                .Where(a => !a.name.StartsWith("__preview__", StringComparison.Ordinal))
                .GroupBy(a => a.name).ToDictionary(g => g.Key, g => g.First());
            var result = new Dictionary<string, AnimationClip>();
            foreach (var (state, clip) in BaseStates)
                result[state] = all.TryGetValue(clip, out var found) ? found
                    : throw new InvalidOperationException($"Animation library has no clip '{clip}'.");
            return result;
        }

        static void ConfigureModel(CharacterSource c)
        {
            var importer = AssetImporter.GetAtPath(c.fbx) as ModelImporter
                           ?? throw new InvalidOperationException($"Model not found: {c.fbx}");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(c.fbx).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman)
                throw new InvalidOperationException($"{c.fbx} did not import as a valid Humanoid.");
            RemapMaterials(c, importer);
        }

        // Replaces embedded materials with editable URP Lit materials (colour and texture preserved).
        static void RemapMaterials(CharacterSource c, ModelImporter importer)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? throw new InvalidOperationException("URP Lit shader not found.");
            var embedded = AssetDatabase.LoadAllAssetsAtPath(c.fbx).OfType<Material>().ToList();
            foreach (var source in embedded)
            {
                var path = $"{Generated}/Materials/{c.id}_{Sanitize(source.name)}.mat";
                var target = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (target == null)
                {
                    target = new Material(lit) { name = Path.GetFileNameWithoutExtension(path) };
                    AssetDatabase.CreateAsset(target, path);
                }
                CopyLook(source, target, lit);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), target);
            }
            foreach (var m in importer.GetExternalObjectMap().Values.OfType<Material>())
                if (m.shader != lit) { m.shader = lit; EditorUtility.SetDirty(m); }
            if (embedded.Count > 0) importer.SaveAndReimport();
        }

        static void CopyLook(Material source, Material target, Shader lit)
        {
            var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap")
                : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            target.shader = lit;
            color.a = 1f;
            target.SetColor("_BaseColor", color);
            target.SetTexture("_BaseMap", texture);
            target.SetFloat("_Metallic", 0f);
            target.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(target);
        }

        // Right-arm muscle keys: (muscle, resting value, cup-at-mouth value). Resting matches the library's
        // Sitting_Idle_Loop arm so the overlay blends in smoothly. The mouth pose was found by searching
        // muscle values that bring both characters' palms beside a cup at the mouth.
        static readonly (string muscle, float rest, float mouth)[] DrinkMuscles =
        {
            ("Right Shoulder Down-Up", -0.37f, 0.5f),
            ("Right Arm Down-Up", -0.45f, -0.5f),
            ("Right Arm Front-Back", 0.01f, -0.75f),
            ("Right Arm Twist In-Out", -0.32f, 0.5f),
            ("Right Forearm Stretch", 0.16f, -0.25f),
            ("Right Forearm Twist In-Out", -0.65f, 0f),
            ("Right Hand Down-Up", 0.46f, -0.5f),
            ("Right Hand In-Out", 0f, 1f),
        };

        // Lift, sip, lower (seconds).
        static readonly float[] DrinkTimes = { 0f, 0.8f, 1.8f, 2.6f };

        static AnimationClip BuildDrinkClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DrinkClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "Drink" };
                AssetDatabase.CreateAsset(clip, DrinkClipPath);
            }
            clip.ClearCurves();
            var known = new HashSet<string>(HumanTrait.MuscleName);
            foreach (var (muscle, rest, mouth) in DrinkMuscles)
            {
                if (!known.Contains(muscle)) throw new InvalidOperationException($"Unknown muscle '{muscle}'.");
                var values = new[] { rest, mouth, mouth, rest };
                var curve = new AnimationCurve(DrinkTimes.Select((t, i) => new Keyframe(t, values[i])).ToArray());
                for (var i = 0; i < curve.length; i++) AnimationUtility.SetKeyBroken(curve, i, false);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
            }
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static AvatarMask BuildArmMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ArmMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, ArmMaskPath);
            }
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.RightArm);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        static AnimatorController BuildController(CharacterSource c, Dictionary<string, AnimationClip> clips,
            AnimationClip drink, AvatarMask mask)
        {
            var path = ControllerPath(c);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            while (controller.layers.Length > 1) controller.RemoveLayer(controller.layers.Length - 1);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states.ToArray()) machine.RemoveState(child.state);

            var states = new Dictionary<string, AnimatorState>();
            foreach (var (state, _) in BaseStates)
            {
                states[state] = machine.AddState(state);
                states[state].motion = clips[state];
            }
            machine.defaultState = states["Idle"];
            foreach (var pair in FollowUps) AddExit(states[pair.Key], states[pair.Value], 0.2f);

            // Arm overlay: Empty passes the base pose through; Drink overrides only the right arm.
            controller.AddLayer(ArmLayer);
            var layers = controller.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 1f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;
            var arms = controller.layers[1].stateMachine;
            var empty = arms.AddState("Empty");
            arms.defaultState = empty;
            var drinkState = arms.AddState("Drink");
            drinkState.motion = drink;
            AddExit(drinkState, empty, 0.4f);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void AddExit(AnimatorState from, AnimatorState to, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.95f;
            t.duration = duration;
            t.hasFixedDuration = true;
        }

        static void BuildPrefab(CharacterSource c, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(c.fbx);
            var root = new GameObject(c.id);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Model";
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            visual.transform.localPosition = Vector3.zero;

            var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Normalize height from the bind pose and put the feet at y = 0.
            var bounds = MeasureBounds(visual);
            if (bounds.size.y < 0.01f) throw new InvalidOperationException($"{c.id}: could not measure height.");
            var scale = (c.height > 0f ? c.height : TargetHeight) / bounds.size.y;
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;

            AddCup(animator, c);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(c));
            UnityEngine.Object.DestroyImmediate(root);
        }

        // Placeholder cup in the right hand, hidden until a drink is shown.
        static void AddCup(Animator animator, CharacterSource c)
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand)
                       ?? throw new InvalidOperationException("No right hand bone.");
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cup.name = CupName;
            UnityEngine.Object.DestroyImmediate(cup.GetComponent<Collider>());
            cup.GetComponent<Renderer>().sharedMaterial = SimpleMaterial("Cup", new Color(0.95f, 0.93f, 0.88f));
            cup.transform.SetParent(hand, false);
            // Undo the hand's world scale so the cup is 8 cm wide and 10 cm tall.
            var s = hand.lossyScale;
            cup.transform.localScale = new Vector3(0.08f / s.x, 0.05f / s.y, 0.08f / s.z);
            cup.transform.localPosition = new Vector3(c.cupPosition.x / s.x, c.cupPosition.y / s.y, c.cupPosition.z / s.z);
            cup.transform.localRotation = Quaternion.Euler(c.cupEuler);
            cup.SetActive(false);
        }

        public static Bounds MeasureBounds(GameObject go)
        {
            var baked = new Mesh();
            var has = false;
            var result = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is SkinnedMeshRenderer s)) continue;
                // Baked vertices already include bone scale (these FBX nodes are scaled 100x).
                s.BakeMesh(baked, false);
                var m = Matrix4x4.TRS(s.transform.position, s.transform.rotation, Vector3.one);
                foreach (var v in baked.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                    else result.Encapsulate(p);
                }
            }
            UnityEngine.Object.DestroyImmediate(baked);
            return result;
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45f, 150f, 0f);

            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.72f, 0.80f);
            camera.fieldOfView = 40f;
            camera.transform.position = new Vector3(0f, 2.3f, 7.2f);
            camera.transform.LookAt(new Vector3(0f, 0.8f, -1.1f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(1f, 1f, 1f);
            floor.GetComponent<Renderer>().sharedMaterial = SimpleMaterial("PreviewFloor", new Color(0.85f, 0.82f, 0.76f));

            // 1.75 m reference pole for checking the normalized height.
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.name = "Height reference 1.75 m";
            pole.transform.position = new Vector3(-3.6f, TargetHeight / 2f, 0f);
            pole.transform.localScale = new Vector3(0.03f, TargetHeight, 0.03f);
            UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());

            var preview = new GameObject("Character Preview");
            preview.AddComponent<PrototypeDesktopMode>();
            var component = preview.AddComponent<CharacterAnimationPreview>();
            var entries = new List<CharacterAnimationPreview.Character>();
            for (var i = 0; i < Characters.Length; i++)
            {
                var c = Characters[i];
                // Two rows of six (men in front); camera looks down -Z, so +X is screen-left.
                var column = i % 6;
                var x = 2.75f - column * 1.1f;
                var z = i < 6 ? 0f : -2.2f;

                // Simple 45 cm seat behind each character for the sitting clips.
                var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seat.name = $"Seat ({c.id})";
                seat.transform.position = new Vector3(x, SeatHeight / 2f, z - 0.3f);
                seat.transform.localScale = new Vector3(0.5f, SeatHeight, 0.45f);
                seat.GetComponent<Renderer>().sharedMaterial = SimpleMaterial("PreviewSeat", new Color(0.45f, 0.32f, 0.22f));

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(c));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = new Vector3(x, 0f, z);
                var animator = instance.GetComponentInChildren<Animator>();
                entries.Add(new CharacterAnimationPreview.Character
                {
                    label = (i < 6 ? "M " : "W ") + c.label, animator = animator,
                    cup = animator.GetBoneTransform(HumanBodyBones.RightHand).Find(CupName).gameObject,
                });
            }
            component.characters = entries.ToArray();
            component.states = PreviewStates;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static Material SimpleMaterial(string name, Color color)
        {
            var path = $"{Generated}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static string ShortName(string clipName)
        {
            var bar = clipName.LastIndexOf('|');
            return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
        }

        static string Sanitize(string name) =>
            string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == '|' ? '_' : ch));

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
