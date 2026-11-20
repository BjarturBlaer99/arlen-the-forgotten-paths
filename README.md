# ARLEN: The Forgotten Paths

Stylized third-person fantasy action RPG — Unity 6 (6000.6.10f1), URP, C#.

The full design brief (world, characters, systems, milestones, standards) is in
[docs/DESIGN_BRIEF.md](docs/DESIGN_BRIEF.md). Milestones 1–4 are done; Milestone 5 (first complete adventure) is next.

## Running the game

1. Open the folder in Unity Hub with editor **6000.6.10f1**.
2. Open a scene and press Play (click the Game view to capture the mouse):
   - **Haven Village** — `Assets/Arlen/Scenes/Haven_Village.unity` (**Tools ▸ Arlen ▸ Open Haven Village**).
     The playable slice: your cabin, the village, Mabel & Bram, Mosswood, the opening of "The Road That
     Vanished" and the first Waybell. Default build scene.
   - **Training Ground** — `Assets/Arlen/Scenes/Prototype_TrainingGround.unity` (**Tools ▸ Arlen ▸ Open Prototype Scene**).
     Movement/combat sandbox with dummies, hazards and the Boar Ruffian.

Missing scenes can be regenerated from **Tools ▸ Arlen** (it asks before overwriting). Generated prefabs and
dialogue assets are reused, so your edits to them survive a scene rebuild; **Rebuild Village Prefabs**
regenerates them deliberately.

| Action | Keyboard & mouse | Gamepad |
|---|---|---|
| Move | WASD / arrows | Left stick |
| Camera | Mouse | Right stick |
| Jump | Space | South |
| Sprint (hold) | Left Shift | Left stick press |
| Walk toggle | Left Ctrl | (stick tilt) |
| Dodge | Left Alt (Option on Mac) | East |
| Attack (3-hit combo) | Left mouse | West |
| Parry | Right mouse | LB |
| Interact / advance dialogue | E (LMB and Space also advance) | North |
| Pack (inventory) | Tab or I | Select |
| Pause / close a menu | Escape | Start |

Bindings live in `Assets/Arlen/Input/ArlenControls.inputactions`.

## Haven Village

- **Your cabin** (spawn on the porch): open the door, read Eldon's map and note on the desk, browse the
  bookshelf, take the **Pathwarden Sabre** from the equipment rack, open the **chest** (real storage:
  rations, rope, a whetstone and coins), look out the window, and **rest in the bed** to restore health
  and stamina and **save**. The roof hides while you're inside so the camera can see the room.
- **Village square**: the Haven Oak, the well, benches and lanterns. Knock on the cottages; one is Eldon's.
- **Mabel** (calico baker, west stall) and **Bram** (orange-tabby blacksmith, east smithy): first-meeting
  conversations (Mabel gives you a honey-cinnamon roll), a quest conversation about the missing road, then
  rotating follow-ups that hint at the main story.
- **Mosswood** (north, over the bridge): the signpost, a Pathwarden milestone (with a charm at its foot),
  Sunmoss to pick, a Boar Ruffian in a side clearing, and the place where the road simply... stops.
- **Forgotten Shrine** (east off the forest path): a silent **Waybell**. Ring it.
- Area banners announce Haven Village, Mosswood and the shrine as you enter them.

### The Road That Vanished (opening)

Starts a few seconds into a new game; the objective shows top-right. Ask Mabel or Bram about the road →
find where it ends in Mosswood → defeat the Boar Ruffian → follow the humming to the shrine → ring the
Waybell. Doing steps out of order is fine: anything you already did counts when its step comes up.
The rest of the quest (restoring the path) is Milestone 5.

### Pack, equipment and saving

- **Tab / I** opens the pack (the game pauses). Click (or gamepad A) to eat food or equip a weapon/charm;
  click the charm slot to take it off. Opening a chest shows chest and pack side by side; click to move items.
- **Resting in the cabin bed saves**: position, health, quests, pack, equipment, chest contents, picked-up
  items, who you've met and the Waybell. The save loads automatically when Haven Village starts.
  **Pause ▸ Load Last Rest** reloads it; **Pause ▸ New Game** erases it (after confirming).
- The save is `~/Library/Application Support/Arlen Studio/Arlen: The Forgotten Paths/arlen_save_0.json`
  on macOS (`Application.persistentDataPath`). Tests never touch it.

Dialogue lives in `Assets/Arlen/Data/Dialogue/*.asset` (Create ▸ Arlen ▸ Dialogue). A line's speaker
can be empty (use the NPC's name), a name (e.g. "You"), or "-" for narration.
Items are `Assets/Arlen/Data/Items/*.asset` (Create ▸ Arlen ▸ Item / Equipment; new ones under that folder are
added to the item database on the next scene build) and quests `Assets/Arlen/Data/Quests/*.asset`
(Create ▸ Arlen ▸ Quest). Never change an item's or quest's `id` once saves exist.

## Training ground

- **Dummies** (north): 60 HP, wobble when hit, fall and reset.
- **Jump course** (east): steps 0.5 m apart; **ramp** (west).
- **Ruins** (south): pillars + lintel to test camera collision.
- **Blight pool** (purple, south-west): damage over time. **Healing spring** (cyan, south-east): heals.
- **Pulsing hazard** (far north): flickers then bursts — dodge through it to test i-frames.
- **Boar Ruffian** (north-west clearing): wanders until it spots you ("!"), chases, and swings its club
  after a telegraph (club raised, eyes glow). Parry just before the swing lands to stagger it for a
  counter-attack, or dodge through it. Breaking its poise (≈ a full combo) also staggers it. It
  leashes home if you run away and respawns a few seconds after dying.
  Tune it in `Assets/Arlen/Data/Enemies/BoarRuffian_Stats.asset`.

## Architecture (Milestones 1–4)

```
Scripts/
  Core/    HealthPool, StaminaPool, MovementMath (pure C#, unit-tested), PauseController,
           WorldSignals (world → quest event hub), ModalScreens (menus Escape closes before pausing)
  Player/  PlayerInputReader → only Input System consumer (buffered presses)
           PlayerActionState → arbitrates committed actions (dodge/attack) + cancel windows
           ArlenPlayerController → CharacterController locomotion; the only caller of Move()
           PlayerDodge, PlayerRespawn
           PlayerAnimatorBridge → Animator parameter contract for future rigged models
           PlaceholderCatAnimator → procedural motion for the primitive placeholder
  Combat/  IDamageable/DamageInfo/DamageResult, IDamageInterceptor, HealthComponent,
           StaminaComponent, PlayerMeleeCombat, PlayerParry, Targetable (teams + registry),
           MeleeAttackStep, DamageFlash, TrainingDummy, HealthVolume, PulsingHazard
  AI/      EnemyStats (ScriptableObject), EnemySensor (perception), EnemyMotor (NavMeshAgent),
           EnemyMeleeAttack (execution), EnemyController (state machine brain),
           BoarRuffianVisual (presentation only), NavMeshBootstrap, PrototypeEnemySpawner
  Feedback/ HitStop, CameraShake, ImpactFxPool (pooled), PlayerCombatFeedback
  Interaction/ IInteractable + InteractionScoring, PlayerInteractor (proximity + facing; hands out
           dialogue/fader/quest/inventory-screen services), Door, Examinable, StorageChest (storage), RestSpot
  Dialogue/ DialogueData (ScriptableObject), DialogueRunner (typewriter, input blocking), DialogueView
  NPC/     NpcCharacter (first meeting, quest conversations, repeats, gifts), NpcIdleMotion (placeholder)
  World/   PlayerZone → InteriorZone (roof hiding), AreaZone (location banners), Waybell
  Quests/  QuestDefinition (ScriptableObject), QuestLog (pure C# rules + facts), QuestCondition,
           QuestManager (scene service)
  Items/   ItemDefinition/EquipmentDefinition/ItemDatabase (ScriptableObjects), Inventory (pure C#),
           PlayerInventory, PlayerEquipment (stat modifiers), ItemPickup, EquipmentVisuals (presentation)
  Saving/  SaveData (plain JSON data), SaveFile (atomic write, versioning), ISaveable, SaveManager,
           PlayerSaveState
  Camera/  ThirdPersonCameraController (orbit + sphere-cast collision, no Cinemachine)
  UI/      PlayerHUD, ResourceBar, WorldHealthBar, InteractionPromptUI, ScreenFader, AreaBanner,
           QuestTrackerUI, ToastFeed + HudNotifications, InventoryScreen + ItemSlotView, SaveMenuButtons
  Editor/  PrototypeSceneBuilder (shared rig + training ground), HavenVillageBuilder (village,
           cabin, forest, shrine, NPCs, environment prefabs), RpgRigBuilder (M4 rig + HUD),
           RpgContentBuilder (items, quest, quest dialogue) — Tools ▸ Arlen menu, rerunnable
Tests/EditMode, Tests/PlayMode (simulated keyboard/mouse via InputTestFixture)
```

Input blocking is reference-counted (`PlayerInputReader.PushBlock/PopBlock`): pause, dialogue, resting,
the pack screen and the Waybell each hold a block; Interact/Attack/Jump still raise `SubmitPressed` to
advance dialogue.

Quest flow: world objects raise `WorldSignals.Raise(kind, id)` with their stable id (NPC, examinable,
enemy stats, area, Waybell, item). `QuestManager` feeds them to `QuestLog`, which advances the current
objective and remembers every signal as a fact, so a step done early completes when it becomes current.
Nothing in the world references quests; dialogue reacts to quests through `QuestCondition`.

Saving: `SaveManager` finds every `ISaveable` in the scene (inactive included) and lets each write its own
section of `SaveData`. Restoring happens on scene start (before other `Start`s); loading mid-game reloads
the scene, so world objects never need to "undo" state.

Damage flow: attacker → `IDamageable.ApplyDamage` → interceptors (parry) → health. The returned
`DamageResult` lets attackers react (a parried boar staggers).

New enemy types reuse Sensor/Motor/Attack/Controller with a new `EnemyStats` asset and visual;
only genuinely new behaviour (ranged attacks, packs) needs new components.

Visuals live under the player's `Visual` child; gameplay components never reference meshes.
To swap in a Blender model: replace `Visual`'s children, remove `PlaceholderCatAnimator`, add an
Animator using the parameters documented in `PlayerAnimatorBridge`.

## Command line

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.6.10f1/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml
$UNITY -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml
$UNITY -batchmode -nographics -quit -projectPath . -executeMethod Arlen.Editor.PrototypeSceneBuilder.BuildFromCommandLine
$UNITY -batchmode -nographics -quit -projectPath . -executeMethod Arlen.Editor.HavenVillageBuilder.BuildFromCommandLine
```

Git LFS patterns are in `.gitattributes`; install it with `brew install git-lfs && git lfs install`.
