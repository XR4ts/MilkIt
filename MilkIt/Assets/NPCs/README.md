# Customer prototype

## Real café integration

Open **MilkIt > NPCs > Open cafe with customers**, or **Assets/NPCs/Scenes/Cafe_NPC.unity**, then press Play. Pick a **Party size** (1–4) and click **Spawn**. Customers start on the outside walkway, pass through automatic sliding doors, queue at the real till, order, and sit at one of the seven booths.

**MilkIt > NPCs > Update cafe tables and characters** upgrades an existing Cafe_NPC scene in place (it replaces the original three single seats): every booth becomes a table with four seats (two per bench, facing each other), the queue gets five spots, a "Leave till" waypoint is added and customers use the character visuals. Re-running it rebuilds only those objects.

- **Characters:** customers use the generated Humanoid prefabs (`characterPrefabs` on the Customer System, filled by the update menu); the capsule is hidden. Members of one party always get different outfits. They walk/idle from their NavMesh speed, talk while ordering, step into the booth, sit down, sip with a cup while drinking, stand up and leave. With no character prefabs assigned, the capsule behaviour is unchanged (used by NPC_Test).
- **Parties:** a party queues together, each member orders their own drink (one order ID each), the party sits at one table, chats while seated (SitTalk) and leaves together once everyone has finished. Wall-side seats go to the first arrivals so nobody slides past a seated friend.
- **Seat choice:** an empty table first; otherwise, with **Share tables** on, free seats at a partly used table.
- **Flow:** after ordering, customers leave the till westward along the counter, so they never meet the queue head-on in the narrow gap between the condiment bar and the queue rail.

This scene is a copy of Cafe.unity. It has an overhead observation camera, the ceiling hidden, and the original XR rig inactive so you can inspect the entire route. The original Cafe.unity is untouched. Drink delivery still uses the debug buttons.

The NavMesh is baked into `Cafe_NPC_NavMesh.asset` from the floor, entrance walkway, wall geometry, and conservative furniture bounds. The movable doors are excluded from that bake; `CafeSlidingDoor` opens on approach and the customer controller waits near the threshold until they are open. Doors stay open while a customer is nearby and close after a delay. The optional player-head reference also opens them when its VR rig is active.

**MilkIt > NPCs > Validate cafe customer route** runs three visits and stops Play mode afterward. It checks all marker paths, unique seat reservations, six open-door crossings, closing after departure, and seat release. A compact result is written to the system temporary folder as `milkit-cafe-validation.txt`.

Booth seat positions are derived from each booth's transform. Moving the building or furniture requires re-running the update menu and rebaking the navigation data. The setup builder creates the integration scene only when it does not exist.

## Character animation preview

**MilkIt > NPCs > Open character animation preview** generates (first time, or when assets are out of date) and opens `Assets/NPCs/Scenes/NPC_CharacterPreview.unity`. Press Play and use the buttons on one man and one woman. **Rebuild character preview assets** regenerates everything, including the scene.

- Models: 12 Humanoid outfits from the packs' "Humanoid Rig(s)" folders (`Men/` and `Women/*_Humanoid.fbx`): men Hoodie, Casual, Suit, Worker, Punk, Beach; women Casual, Formal, Suit, Worker, Punk, Adventurer. Heights vary (men 1.74–1.84 m, women 1.64–1.72 m). To add another outfit, copy its FBX as `<Outfit>_Humanoid.fbx` and add one line to `CharacterPreviewSetup.Characters`. The animated `Casual_Hoodie.fbx` / `Casual.fbx` variants are not valid Humanoids and are no longer used.
- Animations: Quaternius Universal Animation Library, free Standard version (`Characters/Quaternius/Animations/UAL1_Standard.fbx`, CC0), retargeted through Humanoid. All root movement is baked into the pose, so characters stay where code places them, facing +Z.
- States: Idle, Walk, Talk, Interact, PickUp, SitDown (continues into SitIdle), SitIdle, SitTalk, StandUp (continues into Idle).
- Drink: a generated right-arm clip (`Generated/Drink.anim`) on the `Arms` layer with a right-arm mask, so it plays on top of SitIdle/SitTalk and returns to the base pose on its own. A placeholder cup in the right hand is visible only while drinking.
- Sitting places the hips about 0.45 m high and 0.25–0.3 m behind the character's root; the preview seats are 45 cm cubes.

Generated controllers, prefabs, materials, mask and drink clip live in `Characters/Quaternius/Generated/`. The preview is not connected to the customer system yet.

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
- `TrySpawnParty(int size)` spawns 1–4 customers who share a table; `TrySpawnCustomer()` is a party of one.
- The current string drink IDs are placeholders. Agree on shared recipe IDs with the teammate implementing drink preparation before integrating.
- Unreachable routes or movement blocked for 45 seconds warn in the Console, remove the affected customer, and release its queue position/seat. Waiting at a queue marker or waiting for a drink has no patience timeout yet. Party members wait for each other before leaving.

Keep approaches beside chairs, outside furniture collision volumes. The real café integration above uses its own baked NavMesh instead of `PrototypeNavigation`. The customer logic deliberately does not depend on the VR player or coffee machine.

## Validation

`CustomerPrototypeSmokeTest.Run` is an explicit batch-mode integration check for an isolated Unity project. It generates the test room, exercises four complete visits, rejects invalid/duplicate drinks and over-capacity spawns, checks seat uniqueness, and tests cancellation cleanup and seat reuse. It exits that test editor with a success/failure code; it must not be invoked in the interactive work project.

The editor builder's `Build` method regenerates the test assets and opens a new empty scene. It is used only in the isolated validation project; normal use is the **Open customer test scene** menu.
