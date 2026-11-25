# Food

## `ItemDispenser.cs`
Keeps one item on a plate. When the item is carried further than `radius` (default 0.25 m)
from the spawn point, or destroyed, a new one drops onto the plate from `dropHeight` with a
small random tilt and settles. Select the dispenser to see the yellow radius gizmo.

Ready to use: drag `Assets/Food/Croissant Dispenser.prefab` into a scene.

## What an item is
A croissant (`Assets/Food/Croissant/Croissant.prefab`) is a grabbable Rigidbody with a
`FluidContainer` holding 1 ml of the `Croissant` fluid type. Pouring and receiving are off,
so nothing goes in or out. To check what something is, ask the container:
`GetAmount(croissantType) > 0`.

It has two LODs (7.5k and 750 triangles) and a separate collider: a 20-face convex hull
(`Croissant_Collider` in the FBX) with a few broad flat sides, so it lands on one and stays
there instead of rolling across hundreds of small facets. The `Pastry` physics material has no
bounce, and the Rigidbody's angular damping (1) stops it spinning once it lands.

## Adding another item (e.g. a muffin)
1. Duplicate `Croissant.prefab`, swap the meshes on `LOD0`/`LOD1` and the collider mesh.
   Use a simple convex hull of a few dozen faces for the collider, not the render mesh.
2. Create a fluid type for it (`Assets > Create > MilkIt > Fluid Type`) and put it in the
   container's only layer, amount 1.
3. Make a prefab variant of `Croissant Dispenser`, set `itemPrefab` to the new item, and raise
   `dropHeight` if the item is taller (a bit more than half its height).
