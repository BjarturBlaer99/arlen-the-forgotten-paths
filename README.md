# ARLEN: The Forgotten Paths

Stylized third-person fantasy action RPG — Unity 6 (6000.6.10f1), URP, C#.

## Running the prototype

1. Open the folder in Unity Hub with editor **6000.6.10f1**.
2. Open `Assets/Arlen/Scenes/Prototype_TrainingGround.unity` (or **Tools ▸ Arlen ▸ Open Prototype Scene**).
3. Press Play. Click the Game view to capture the mouse.

If the scene is missing, run **Tools ▸ Arlen ▸ Create Prototype Scene** (it asks before overwriting).

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
| Interact | E — *reserved for Milestone 3* | North |
| Pause | Escape | Start |

Bindings live in `Assets/Arlen/Input/ArlenControls.inputactions`.

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

## Architecture (Milestones 1–2)

```
Scripts/
  Core/    HealthPool, StaminaPool, MovementMath (pure C#, unit-tested), PauseController
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
  Camera/  ThirdPersonCameraController (orbit + sphere-cast collision, no Cinemachine)
  UI/      PlayerHUD, ResourceBar, WorldHealthBar (event-driven)
  Editor/  PrototypeSceneBuilder (Tools ▸ Arlen menu, rerunnable)
Tests/EditMode, Tests/PlayMode (simulated keyboard/mouse via InputTestFixture)
```

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
```

Git LFS patterns are in `.gitattributes`; install it with `brew install git-lfs && git lfs install`.
