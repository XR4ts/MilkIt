using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilkIt.NPCs.Editor
{
    public static class CafeLayoutReport
    {
        [MenuItem("MilkIt/NPCs/Inspect original cafe layout")]
        public static void Inspect()
        {
            if (EditorApplication.isPlaying) return;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Cafe.unity", OpenSceneMode.Additive);
            try
            {
                var report = new StringBuilder();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var item in root.GetComponentsInChildren<Transform>(true))
                    {
                        string path = item.name;
                        for (var parent = item.parent; parent != null; parent = parent.parent)
                            path = parent.name + "/" + path;
                        report.AppendLine($"{path} | pos={item.position:F3} rot={item.eulerAngles:F1} scale={item.lossyScale:F3} active={item.gameObject.activeInHierarchy}");
                        var renderer = item.GetComponent<Renderer>();
                        if (renderer != null) report.AppendLine($"  BOUNDS center={renderer.bounds.center:F3} size={renderer.bounds.size:F3}");
                        foreach (var collider in item.GetComponents<Collider>())
                            report.AppendLine($"  COLLIDER {collider.GetType().Name} center={collider.bounds.center:F3} size={collider.bounds.size:F3} trigger={collider.isTrigger}");
                    }
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "milkit-cafe-layout.txt"), report.ToString());
                Debug.Log("Cafe layout written to " + Path.Combine(Path.GetTempPath(), "milkit-cafe-layout.txt"));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
