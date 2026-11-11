using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Fluids
{
    /// <summary>A spill on a surface: a puddle of liquid or a pile of grains. Created and pooled by FluidSpillManager.</summary>
    public class FluidPuddle : MonoBehaviour
    {
        [Tooltip("Never fades; stays until cleaned up.")]
        public bool permanent;
        public UnityEvent onCleaned = new UnityEvent();

        readonly List<FluidLayer> layers = new List<FluidLayer>();
        FluidSpillManager manager;
        MeshRenderer visual;
        Vector3 point, normal;
        float volume, lastAddTime, radius, thickness;

        public IReadOnlyList<FluidLayer> Layers => layers;
        /// <summary>ml.</summary>
        public float Volume => volume;
        public float Radius => radius;

        internal void Init(FluidSpillManager owner, Vector3 worldPoint, Vector3 worldNormal)
        {
            manager = owner;
            point = worldPoint;
            normal = worldNormal;
            permanent = owner.permanent;
            layers.Clear();
            volume = 0f;
            radius = 0f;
            if (visual == null) visual = FluidVisuals.CreateCylinder("Visual", transform);
            transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.up, normal));
            gameObject.SetActive(true);
        }

        internal void Add(FluidType type, float ml)
        {
            FluidMix.Add(layers, type, ml);
            volume += ml;
            lastAddTime = Time.time;
            Color color = FluidMix.Blend(layers, out FluidType dominant);
            thickness = dominant != null && dominant.IsGranular ? manager.granularThickness : manager.liquidThickness;
            FluidVisuals.Apply(visual, color, dominant);
        }

        /// <summary>Grows towards its size, fades when old. Returns false once it has faded away.</summary>
        internal bool Tick(float dt)
        {
            float target = Mathf.Min(Mathf.Sqrt(volume * 1e-6f / (Mathf.PI * thickness)), manager.maxRadius);
            float fade = 0f;
            if (!permanent)
            {
                float age = Time.time - lastAddTime - manager.lifetime;
                if (age > 0f) fade = manager.fadeDuration > 0f ? age / manager.fadeDuration : 1f;
                if (fade >= 1f) return false;
            }
            radius = Mathf.MoveTowards(radius, target * (1f - fade), manager.spreadSpeed * dt);
            Transform t = visual.transform;
            t.localPosition = new Vector3(0f, thickness * 0.5f, 0f);
            t.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
            return true;
        }

        /// <summary>Removes the whole puddle (a mop, a cloth, a cleaning event).</summary>
        public void CleanUp()
        {
            if (manager != null) manager.Remove(this, true);
        }

        /// <summary>Removes part of the puddle. Returns the ml removed; the puddle disappears when nothing is left.</summary>
        public float Clean(float ml)
        {
            ml = Mathf.Min(ml, volume);
            if (ml <= 0f) return 0f;
            float keep = 1f - ml / volume;
            for (int i = 0; i < layers.Count; i++) layers[i] = new FluidLayer(layers[i].type, layers[i].amount * keep);
            volume -= ml;
            if (volume < 0.5f) CleanUp();
            return ml;
        }
    }
}
