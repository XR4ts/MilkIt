# Fluid system

Pouring, scooping, filling and spilling for MilkIt, without particles or fluid simulation.
Everything is volume bookkeeping in millilitres plus a few scaled cylinder meshes. The only
physics call is one downward raycast per stream that is actually pouring.

All scripts are in the `MilkIt.Fluids` namespace.

## How the pieces fit

```
FluidSource ──┐                      ┌──> FluidContainer (cup, tank, drain)
              ├──> FluidStream ──────┤
FluidContainer┘   (falls straight    └──> FluidSpillManager ──> FluidPuddle
(tilted or         down)                  (when it lands on a surface)
 overflowing)

FluidScoopZone ──> fills any scoopable FluidContainer dipped into it
FluidRag ────────> scrubs FluidPuddles away
```

- A **source** or a **tilted container** pushes volume into its **stream** each frame.
- The **stream** looks straight down. If an upward-facing container opening is below it, the
  volume goes into that container. Otherwise it lands on the first fixed surface below and
  becomes a **puddle**.
- A container that receives more than it can hold, or a material it does not accept, sends
  the excess out over its rim through its own stream.

Units: volumes are ml, flow rates are ml per second, sizes are metres.

## Scripts

### `FluidType.cs`
ScriptableObject describing one material (Water, Coffee, Milk, CoffeeGrain).

- `displayName`, `color`
- `state`: `Liquid` or `Granular`. Granular streams are drawn thicker and spill into thicker piles.
- `material`: optional material for its streams, fill level and puddles; tinted with `color`.

Create one with `Assets > Create > MilkIt > Fluid Type`.

### `FluidLayer.cs`
- `FluidLayer`: a struct holding a `FluidType` and an amount in ml.
- `FluidMix`: static helpers on lists of layers: total volume, add (merging into an existing
  layer of the same type), and the volume-weighted blend colour plus the dominant material.

### `FluidContainer.cs`
Anything that holds fluid: a cup, a machine's water tank, a sink drain, a trash can.

**Contents**
- `capacity` in ml.
- `Layers`: one entry per material, in the order each material was *first* poured in.
  Pouring coffee, then milk, then more coffee gives `0: coffee, 1: milk`.
- Queries: `Total`, `FillFraction`, `IsFull`, `IsEmpty`, `GetAmount(type)`,
  `GetFractionOfCapacity(type)`, `GetFractionOfContents(type)`.
- Changes: `Receive(type, ml)` (spills what does not fit), `Add(type, ml)` (never spills,
  returns the amount added), `Remove(ml)` (takes every material proportionally),
  `Remove(type, ml)`, `Empty()`.

**Shape**
- `opening`: a transform at the centre of the rim with +Y pointing out of the container.
- `openingRadius` and `depth`: the inside is treated as a cylinder. Select the object to see
  the cyan gizmo.

**Receiving**
- `canReceive`: streams can land in it.
- `acceptedTypes`: empty means everything; anything else poured in is spilled.
- `isDrain`: swallows everything and never fills (sink, trash).
- `scoopable`: fills when dipped into a `FluidScoopZone`.

**Pouring**
- `canPour`: pours when tilted. It pours whenever the level liquid surface would rise above
  the lowest point of the rim, and drains from that point until the surface is back at the
  rim. A full cup spills at a slight tilt, a nearly empty one must be almost upside down.
- `maxPourRate`: ml per second through the opening.

**Visual**
- `showFill`: the liquid inside the container, in the blend colour. Its surface stays level
  with the world while the container tilts, up to the point where it touches the rim.

**Events**
- `onContentsChanged`, `onFull`, `onEmptied`, `onOverflow`, `onDrained(type, ml)`, and the C#
  event `ContentsChanged`.

### `FluidStream.cs`
The pour itself. Added automatically to sources and containers the first time they pour; you
do not place it by hand.

- Emitters call `SetOrigin` and `Add` each frame; the stream delivers in `LateUpdate`.
- Target: the highest upward-facing container opening directly below the origin. If something
  blocks the way, or there is no opening, the volume becomes a spill where the stream lands.
- Fluid only settles on fixed, roughly level surfaces. It runs off anything else and lands on
  the fixed surface below: objects with a Rigidbody, the player rig (controllers, hands),
  containers, and faces steeper than about 60 degrees.
- Visual: one cylinder from the origin down to the landing point. Its thickness comes from
  the flow rate divided by `flowSpeed`, so pouring more makes it thicker.
- `obstacles`: layer mask for what the stream can land on. `maxDistance`: how far it falls.

### `FluidSource.cs`
Produces fluid at a spout: a tap, an espresso group head, a syrup pump. Infinite by default.

- `fluidType`, `flowRate`, `spout`, `startOn`.
- Continuous: `StartFlow()`, `StopFlow()`, `Toggle()`.
- Per signal: `Dispense()` produces `doseAmount` ml and stops; `DispenseAmount(ml)` for a
  custom amount.
- `SetFlowScale(0..1)`: for a tap handle or knob.
- Optional supply: set `supply` (a `FluidContainer`) and the source can only produce what the
  supply holds, using it up. `supplyType` picks which material is taken and `supplyPerOutput`
  sets the ratio. This is how the espresso machine turns tank water into coffee.
- Events: `onStarted`, `onStopped`, `onDoseCompleted`, `onSupplyEmpty`.

All the public methods take no or one simple argument, so they can be wired to XR
Interaction Toolkit events in the inspector.

### `FluidScoopZone.cs`
Makes a source scoopable: a box of material such as a bag of coffee grains. Infinite.

- `fluidType`, `fillRate`.
- `center` and `size`: the box in local space; its top face is the surface.
- Any container with `scoopable` on fills up while the centre of its opening is inside the
  box. It does not pour back out while submerged.

### `FluidPuddle.cs`
One spill on a surface: a puddle of liquid or a pile of grains. Created and pooled by the
spill manager.

- `Layers`, `Volume`, `Radius`.
- `permanent`: this puddle never fades, even while the manager's fade timer is on.
- `Scrub(progress)`: adds scrubbing progress (1 = cleaned). The puddle shrinks as it is
  scrubbed. `ScrubProgress` reads it back.
- `CleanUp()` removes it at once, `Clean(ml)` removes part of it.
- `onCleaned` event.

### `FluidSpillManager.cs`
Owns every puddle. One per scene; created automatically with default settings if missing.

- Spills landing close to an existing puddle join it; puddles grow with volume.
- `fadeEnabled`: the fade timer for all puddles. On (default): a puddle fades `lifetime`
  seconds (default 10) after the last drop landed on it. Off: puddles stay until cleaned.
  Tick the checkbox on the manager, or call `SetFadeEnabled(bool)` / `ToggleFade()`, at any
  time; it applies to existing puddles too, and their timers pause while it is off.
- `maxPuddles`: beyond this the oldest fading puddle is reused. With the timer off, new
  spills join the nearest puddle instead.
- Cleaning from code: `CleanAt(position, radius)`, `CleanAll()`.
- Events: `onPuddleCreated`, `onPuddleRemoved`, `onPuddleCleaned`.
- `Puddles`: the active list, for game logic.

### `FluidRag.cs`
A cloth that cleans spills. Put it on any grabbable object.

- Touching a puddle is not enough: the rag has to be moved along the surface over it.
  `scrubTime` (default 3) is the total seconds of rubbing needed to clean one puddle.
- `radius`: how far around its centre it wipes. `reach`: how far above the surface it may be.
- `minRubSpeed`: slower movement than this does not count as rubbing.
- `onPuddleCleaned` event.

### `FluidFillMesh.cs`
Internal helper for `FluidContainer`: builds the liquid shape (a cylinder cut by a level
surface) and does the volume maths for it, which also decides when a tilted container starts
to pour.

### `FluidVisuals.cs`
Internal helper shared by the fill level, streams and puddles: one cylinder mesh generated in
code, the material lookup (the type's material, or a URP Lit fallback), and colour tinting.

## Test scene

`Assets/Scenes/FluidTest.unity` is a scene for trying the system, with fluid types in
`Assets/Fluids/Types/` (Water, Coffee, Milk, CoffeeGrain) and materials in
`Assets/Fluids/Materials/`.

It is built from primitives: a grain bag (scoop zone), a pre-filled milk jug, a tap with a
toggle button over a sink drain, an espresso machine whose button gives one 40 ml shot from
the water inlet on top, three cups, a rag, and a trash can. The XR rig in this scene has far
grabbing switched off, so objects must be touched to be grabbed or pressed.

## Using it on real models

1. Add `FluidContainer` to the cup or machine part.
2. Add an empty child at the centre of the rim, +Y pointing out, and assign it as `opening`.
3. Set `openingRadius` and `depth` until the cyan gizmo matches the inside of the model.
4. Set `capacity`. For a cylinder: π × radius² × depth × 1,000,000 gives ml.
5. For a source, add `FluidSource`, assign a `spout` transform, and wire a button's event to
   `Toggle` or `Dispense`.

## Known limits

- Streams fall straight down; there is no arc.
- The liquid surface follows gravity instantly; it does not slosh.
- Fluid that runs off a movable object appears directly below where it landed, without a
  visible trickle down the object's side.
- Liquids are opaque.
- The inside of every container is approximated as a cylinder.
- A stream ignores the colliders of the object it comes from, so an espresso shot with no cup
  under it passes through the machine's own drip tray.
- An object only counts as movable if it has a Rigidbody or belongs to the player rig or a
  container. A prop moved by script without a Rigidbody still collects puddles.
- Recipe checking (comparing a container's layers against a recipe) is not implemented.

## Testing on Linux

Unity's OpenXR plugin has no Linux library, so Play mode cannot reach a headset from the Linux
editor. Use the XR Interaction Simulator in the editor, or make a Windows build and run it
through Proton with SteamVR.
