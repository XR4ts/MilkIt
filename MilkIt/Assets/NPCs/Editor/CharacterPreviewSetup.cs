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
    // Builds reusable visual prefabs for the Quaternius characters and a standalone preview scene.
    // Does not touch the customer system, Cafe.unity or Cafe_NPC.unity.
    public static class CharacterPreviewSetup
    {
        public const string Root = "Assets/NPCs/Characters/Quaternius";
        public const string Generated = Root + "/Generated";
        public const string ScenePath = "Assets/NPCs/Scenes/NPC_CharacterPreview.unity";
        public const float TargetHeight = 1.75f;

        public struct CharacterSource
        {
            public string id, label, fbx;
            public float yawOffset; // rotates the model so the prefab's +Z is the character's front
        }

        public static readonly CharacterSource[] Characters =
        {
            new CharacterSource { id = "Man_CasualHoodie", label = "Man", fbx = Root + "/Men/Casual_Hoodie.fbx" },
            new CharacterSource { id = "Woman_Casual", label = "Woman", fbx = Root + "/Women/Casual.fbx" },
        };

        static readonly HashSet<string> Looping = new HashSet<string>
        {
            "Idle", "Idle_Neutral", "Idle_Gun", "Idle_Gun_Pointing", "Idle_Gun_Shoot", "Idle_Sword",
            "Walk", "Run", "Run_Back", "Run_Left", "Run_Right", "Run_Shoot",
        };

        // Shown first in the preview panel; all other clips follow alphabetically.
        static readonly string[] PreferredOrder = { "Idle", "Walk", "Interact", "Wave", "Idle_Neutral", "Run" };

        public static string PrefabPath(CharacterSource c) => $"{Generated}/{c.id}.prefab";
        static string ControllerPath(CharacterSource c) => $"{Generated}/{c.id}.controller";

        [MenuItem("MilkIt/NPCs/Open character animation preview")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var missing = !File.Exists(ScenePath) || Characters.Any(c => !File.Exists(PrefabPath(c)));
            if (missing) Build(true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("MilkIt/NPCs/Rebuild character preview assets")]
        public static void RebuildPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // Regenerates importer settings, materials, controllers and prefabs. The scene is
        // overwritten only when rebuildScene is true. Leaves a new empty scene open when it writes one.
        public static void Build(bool rebuildScene)
        {
            EnsureFolder(Generated);
            EnsureFolder(Generated + "/Materials");
            var states = new List<string>();
            foreach (var c in Characters)
            {
                var clips = ConfigureModel(c);
                var controller = BuildController(c, clips);
                BuildPrefab(c, controller);
                foreach (var clip in clips)
                    if (!states.Contains(clip.name)) states.Add(clip.name);
            }
            AssetDatabase.SaveAssets();
            if (rebuildScene || !File.Exists(ScenePath)) BuildScene(Order(states));
        }

        static List<AnimationClip> ConfigureModel(CharacterSource c)
        {
            var importer = AssetImporter.GetAtPath(c.fbx) as ModelImporter
                           ?? throw new InvalidOperationException($"Model not found: {c.fbx}");

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = ShortName(clip.name);
                clip.loopTime = Looping.Contains(clip.name);
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            RemapMaterials(c, importer);

            return AssetDatabase.LoadAllAssetsAtPath(c.fbx).OfType<AnimationClip>()
                .Where(a => !a.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
        }

        // Replaces embedded materials with editable URP Lit materials (colour and texture preserved).
        static void RemapMaterials(CharacterSource c, ModelImporter importer)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? throw new InvalidOperationException("URP Lit shader not found.");
            var remapped = importer.GetExternalObjectMap()
                .Where(p => p.Key.type == typeof(Material)).ToDictionary(p => p.Key.name, p => p.Value as Material);
            var embedded = AssetDatabase.LoadAllAssetsAtPath(c.fbx).OfType<Material>().ToList();
            var changed = false;

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
                changed = true;
            }

            foreach (var m in remapped.Values)
                if (m != null && m.shader != lit) { m.shader = lit; EditorUtility.SetDirty(m); }

            if (changed) importer.SaveAndReimport();
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

        static AnimatorController BuildController(CharacterSource c, List<AnimationClip> clips)
        {
            var path = ControllerPath(c);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states.ToArray()) machine.RemoveState(child.state);

            var byName = clips.ToDictionary(a => a.name);
            var states = new Dictionary<string, AnimatorState>();
            foreach (var name in Order(byName.Keys))
            {
                var state = machine.AddState(name);
                state.motion = byName[name];
                states[name] = state;
            }

            if (!states.TryGetValue("Idle", out var idle))
                throw new InvalidOperationException($"{c.fbx} has no Idle clip.");
            machine.defaultState = idle;

            // One-shot clips return to Idle so Interact/Wave can be re-triggered from the panel.
            foreach (var pair in states)
            {
                if (Looping.Contains(pair.Key) || pair.Key == "Death") continue;
                var back = pair.Value.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.15f;
                back.hasFixedDuration = true;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void BuildPrefab(CharacterSource c, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(c.fbx);
            var root = new GameObject(c.id);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, c.yawOffset, 0f);
            visual.transform.localScale = Vector3.one;

            var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Measure a throwaway copy in the Idle pose, then normalize height and put the feet at y = 0.
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            probe.transform.SetPositionAndRotation(Vector3.zero, visual.transform.localRotation);
            var idle = controller.animationClips.FirstOrDefault(a => a.name == "Idle");
            if (idle != null) idle.SampleAnimation(probe, 0f);
            var bounds = MeasureBounds(probe);
            UnityEngine.Object.DestroyImmediate(probe);
            if (bounds.size.y < 0.01f) throw new InvalidOperationException($"{c.id}: could not measure height.");
            var scale = TargetHeight / bounds.size.y;
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;

            foreach (var skinned in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                skinned.updateWhenOffscreen = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(c));
            UnityEngine.Object.DestroyImmediate(root);
        }

        public static Bounds MeasureBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var baked = new Mesh();
            var has = false;
            var result = new Bounds();
            foreach (var r in renderers)
            {
                IEnumerable<Vector3> points;
                if (r is SkinnedMeshRenderer s)
                {
                    // Baked vertices already include bone scale (these FBX nodes are scaled 100x).
                    s.BakeMesh(baked, false);
                    var m = Matrix4x4.TRS(s.transform.position, s.transform.rotation, Vector3.one);
                    points = baked.vertices.Select(v => m.MultiplyPoint3x4(v));
                }
                else if (r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null)
                    points = f.sharedMesh.vertices.Select(v => r.transform.TransformPoint(v));
                else continue;

                foreach (var p in points)
                {
                    if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                    else result.Encapsulate(p);
                }
            }
            UnityEngine.Object.DestroyImmediate(baked);
            return result;
        }

        static void BuildScene(List<string> states)
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
            camera.transform.position = new Vector3(0f, 1.2f, 4.6f);
            camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            floor.GetComponent<Renderer>().sharedMaterial = FloorMaterial();

            // 1.75 m reference pole for checking the normalized height.
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.name = "Height reference 1.75 m";
            pole.transform.position = new Vector3(0f, TargetHeight / 2f, -0.4f);
            pole.transform.localScale = new Vector3(0.03f, TargetHeight, 0.03f);
            UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());

            var preview = new GameObject("Character Preview");
            preview.AddComponent<PrototypeDesktopMode>();
            var component = preview.AddComponent<CharacterAnimationPreview>();
            var entries = new List<CharacterAnimationPreview.Character>();
            for (var i = 0; i < Characters.Length; i++)
            {
                var c = Characters[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(c));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = new Vector3(i == 0 ? 0.75f : -0.75f, 0f, 0f); // camera looks down -Z, so +X is screen-left
                entries.Add(new CharacterAnimationPreview.Character
                    { label = c.label, animator = instance.GetComponentInChildren<Animator>() });
            }
            component.characters = entries.ToArray();
            component.states = states.ToArray();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static Material FloorMaterial()
        {
            var path = Generated + "/Materials/PreviewFloor.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.85f, 0.82f, 0.76f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static List<string> Order(IEnumerable<string> names)
        {
            var all = names.ToList();
            var result = PreferredOrder.Where(all.Contains).ToList();
            result.AddRange(all.Where(n => !result.Contains(n)).OrderBy(n => n, StringComparer.Ordinal));
            return result;
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
