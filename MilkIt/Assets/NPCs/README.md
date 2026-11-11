# Customer prototype

## Real café integration

Open **MilkIt > NPCs > Open cafe with customers**, or **Assets/NPCs/Scenes/Cafe_NPC.unity**, then press Play. Use **Spawn customer** and the serving buttons as in the test room. Customers start on the outside walkway, pass through automatic sliding doors, queue at the real till, and use three booths along the left wall.

This scene is a copy of Cafe.unity. It has an overhead observation camera, the ceiling hidden, and the original XR rig inactive so you can inspect the entire route. The original Cafe.unity is untouched. Character and sitting animation remain placeholders, and drink delivery still uses the debug buttons.

The NavMesh is baked into `Cafe_NPC_NavMesh.asset` from the floor, entrance walkway, wall geometry, and conservative furniture bounds. The movable doors are excluded from that bake; `CafeSlidingDoor` opens on approach and the customer controller waits near the threshold until they are open. Doors stay open while a customer is nearby and close after a delay. The optional player-head reference also opens them when its VR rig is active.

**MilkIt > NPCs > Validate cafe customer route** runs three visits and stops Play mode afterward. It checks all marker paths, unique seat reservations, six open-door crossings, closing after departure, and seat release. A compact result is written to the system temporary folder as `milkit-cafe-validation.txt`.

The initial integration uses three specific booth seats. Moving the building or furniture requires updating the route markers and rebaking the navigation data. The setup builder creates the integration scene only when it does not exist; it will not overwrite your edits.

## Character animation preview

**MilkIt > NPCs > Open character animation preview** generates (first time only) and opens `Assets/NPCs/Scenes/NPC_CharacterPreview.unity`. Press Play and use the buttons to preview all 24 Quaternius clips on one man (`Men/Casual_Hoodie.fbx`) and one woman (`Women/Casual.fbx`). **Rebuild character preview assets** regenerates everything, including the scene.

The setup imports both models as Generic rigs, names clips without the `CharacterArmature|` prefix, loops idle/walk/run clips, remaps materials to editable URP Lit copies, and writes animator controllers and visual prefabs to `Characters/Quaternius/Generated/`. Each prefab root is 1.75 m tall, feet at y = 0, facing +Z; root motion is off and Walk plays in place. One-shot clips return to Idle. The packs contain no sitting animation. This preview is not connected to the customer system yet. Models are CC0 (see `License.txt`).

## Original test room

Open **Assets/NPCs/Scenes/NPC_Test.unity** (or **MilkIt > NPCs > Open customer test scene**) and press Play. This is a standalone test room with an overhead camera; it does not require a headset or the XR simulator.

`PrototypeDesktopMode` temporarily suspends any automatically created XR Interaction Simulator instance in this test scene, because it has no VR rig. It restores the instance when disabled and does not change the project's XR settings.

1. Click **Spawn customer** up to three times.
2. Watch the status panel: customers queue, order one at a time, then walk to separate reserved chairs.
3. Click **Serve [drink] to #[order]** when a customer is waiting at a table.
4. After drinking, the customer releases the seat and walks to the exit.
5. Spawn another customer to reuse the seat. **Automatically serve waiting customers** runs the same loop without clicking each delivery.

Use the Scene view during Play to inspect movement from another angle. The test scene is separate from Cafe.unity and is not added to build settings. The coloured capsules are placeholders; the seating pose is a visual approximation, not a sit animation. The test room has an open entrance; sliding-door behaviour is not implemented yet.

## Scene controls

Select **Customer System** to inspect the entrance, exit, ordered queue markers, seats, prefab, drink list, ordering duration and drinking duration. Each **Seat** has a walkable **Approach** marker and a **Sitting pose** for the visible character body. Queue slot zero is the counter. Customers reserve a seat when admitted. If every seat is occupied/reserved, admission stops until one is released. Spawning is manual in this first version.

`PrototypeNavigation` builds a small NavMesh from the test room's floor and furniture boxes when Play begins, using Unity's built-in navigation API. It removes only its own NavMesh when the scene stops. No new package is required for this test room.

## Team integration

- `CafeCustomerSystem.OrderPlaced(int orderId, string drink)` fires once at the counter. An order ID identifies a visit, not just a recipe.
- Call `TryDeliverDrink(orderId, drink)` after delivery to the matching customer's table. It returns false for an unknown order, wrong drink, duplicate delivery, or a customer who is not yet seated and waiting.
- `CustomerLeft(int orderId)` reports a completed visit. `CompletedVisits` counts those visits.
- The current string drink IDs are placeholders. Agree on shared recipe IDs with the teammate implementing drink preparation before integrating.
- Unreachable routes or movement blocked for 45 seconds warn in the Console, remove the affected customer, and release its queue position/seat. Waiting at a queue marker or waiting for a drink has no patience timeout yet.

Keep approaches beside chairs, outside furniture collision volumes. The real café integration above uses its own baked NavMesh instead of `PrototypeNavigation`. The customer logic deliberately does not depend on the VR player or coffee machine.

## Validation

`CustomerPrototypeSmokeTest.Run` is an explicit batch-mode integration check for an isolated Unity project. It generates the test room, exercises four complete visits, rejects invalid/duplicate drinks and over-capacity spawns, checks seat uniqueness, and tests cancellation cleanup and seat reuse. It exits that test editor with a success/failure code; it must not be invoked in the interactive work project.

The editor builder's `Build` method regenerates the test assets and opens a new empty scene. It is used only in the isolated validation project; normal use is the **Open customer test scene** menu.
