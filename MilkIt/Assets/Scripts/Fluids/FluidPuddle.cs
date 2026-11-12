using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Fluids
{
    public class FluidPuddle : MonoBehaviour
    {
        [Tooltip("This puddle never fades, even while the manager's fade timer is on.")]
        public bool permanent;
        public UnityEvent onCleaned = new UnityEvent();

        readonly List<FluidLayer> layers = new List<FluidLayer>();
        FluidSpillManager manager;
        MeshRenderer visual;
        Vector3 point, normal;
        float volume, age, scrub, radius, thickness;

        public IReadOnlyList<FluidLayer> Layers => layers;
        public float Volume => volume;
        public float Radius => radius;

        internal void Init(FluidSpillManager owner, Vector3 worldPoint, Vector3 worldNormal)
        {
            manager = owner;
            point = worldPoint;
            normal = worldNormal;
            permanent = false;
            age = 0f;
            scrub = 0f;
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
            age = 0f;
            Color color = FluidMix.Blend(layers, out FluidType dominant);
            thickness = dominant != null && dominant.IsGranular ? manager.granularThickness : manager.liquidThickness;
            FluidVisuals.Apply(visual, color, dominant);
        }

        internal bool Tick(float dt, bool fadeEnabled)
        {
            float target = Mathf.Min(Mathf.Sqrt(volume * 1e-6f / (Mathf.PI * thickness)), manager.maxRadius);
            float fade = 0f;
            if (fadeEnabled && !permanent)
            {
                age += dt;
                float over = age - manager.lifetime;
                if (over > 0f) fade = manager.fadeDuration > 0f ? over / manager.fadeDuration : 1f;
                if (fade >= 1f) return false;
            }
            target *= (1f - fade) * (1f - 0.6f * scrub);
            radius = Mathf.MoveTowards(radius, target, manager.spreadSpeed * dt);
            Transform t = visual.transform;
            t.localPosition = new Vector3(0f, thickness * 0.5f, 0f);
            t.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
            return true;
        }

        public float ScrubProgress => scrub;

        public bool Scrub(float progress)
        {
            scrub += Mathf.Max(progress, 0f);
            if (scrub < 1f) return false;
            CleanUp();
            return true;
        }

        public void CleanUp()
        {
            if (manager != null) manager.Remove(this, true);
        }

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
