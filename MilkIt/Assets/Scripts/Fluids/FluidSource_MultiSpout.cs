using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MilkIt.Fluids
{
    public class FluidSource_MultiSpout : FluidSource
    {
        public List<Transform> spouts = new List<Transform>();
        protected override Transform Spout =>
            spouts != null && spouts.Count > 0 ? spouts[0] : null;
        
        List<FluidStream> streams = new List<FluidStream>();
        protected override void Update()
        {
            if (!IsFlowing || fluidType == null) return;

            float amount = Mathf.Min(flowRate * flowScale * Time.deltaTime, remaining);
            if (supply != null && supplyPerOutput > 0f)
            {
                float available = supplyType != null ? supply.GetAmount(supplyType) : supply.Total;
                amount = Mathf.Min(amount, available / supplyPerOutput);
                if (amount <= 0.0001f)
                {
                    onSupplyEmpty.Invoke();
                    StopFlow();
                    return;
                }
                if (supplyType != null) supply.Remove(supplyType, amount * supplyPerOutput);
                else supply.Remove(amount * supplyPerOutput);
            }
            if (amount <= 0f) return;

            if (spouts.Count == 0) spouts[0] = transform;

            for (int i = 0; i < spouts.Count; i++){
                if (streams.Count <= i)
                {
                    streams.Add(FluidStream.Create(gameObject, null));
                }
                streams[i].SetOrigin(spouts[i].position);
                streams[i].Add(fluidType, amount / spouts.Count);
            }

            remaining -= amount;
            if (remaining <= 0.0001f)
            {
                StopFlow();
                onDoseCompleted.Invoke();
            }
        }
    }
}
