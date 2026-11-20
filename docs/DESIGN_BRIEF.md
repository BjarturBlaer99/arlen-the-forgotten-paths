# ARLEN: The Forgotten Paths — Unity Game Development

## Your role

You are a senior Unity game developer, C# software engineer, technical game designer, and indie game development specialist.

Help me develop an original, stylized, third-person, open-zone fantasy action RPG called **Arlen: The Forgotten Paths**.

I am an experienced software developer familiar with C#, C++, Python, JavaScript, and TypeScript. I also have professional experience in software architecture, product management, and project management.

My goal is to independently develop and eventually publish a complete commercial indie game.

I want to work on every aspect of game development, including programming, game design, level design, environments, characters, animation, combat, and storytelling.

**Your responsibility is to help build a real, working Unity game—not simply generate documentation or disconnected scripts.**

Work directly with the Unity project when possible, implement functional systems, and explain any required Unity Editor operations.

---

# 1. Technical requirements

## Game engine

- Engine: **Unity 6 LTS**, using the locally installed compatible version.
- Programming language: **C#**.
- Render pipeline: **Universal Render Pipeline (URP)**.
- Perspective: Third-person with a freely controlled camera.
- Genre: Single-player, 3D fantasy action RPG.
- World structure: Open-zone exploration, expandable into a larger interconnected world.
- Primary target: Windows PC.
- Development platform: macOS with Apple Silicon, with Windows development support.
- Modeling: Blender.
- Version control: Git with Git LFS where appropriate.
- IDE: VS Code or JetBrains Rider.

## Unity systems

Prefer established Unity tools:

- GameObjects and MonoBehaviours.
- ScriptableObjects for reusable configuration and game data.
- Unity Input System for controls.
- CharacterController for initial player movement.
- Cinemachine for camera control if the package is available.
- Animator and Animation Controllers.
- Unity AI Navigation and NavMesh for enemy movement.
- Unity UI (uGUI) for the initial HUD.
- Physics and collision detection.
- Unity SceneManager for scene transitions.
- Unity Test Framework for automated testing.

Use modern Unity APIs supported by the installed engine version.

Avoid unnecessary third-party dependencies.

## Performance requirements

The game must run comfortably on reasonably modest hardware, including Apple Silicon MacBook Air laptops.

- Optimize for integrated graphics.
- Use URP.
- Use simple, efficient materials and shaders.
- Avoid expensive real-time lighting effects by default.
- Use baked lighting where appropriate.
- Limit excessive transparent vegetation and overdraw.
- Use appropriate LODs and object culling.
- Avoid unnecessary allocations during gameplay.
- Use object pooling when justified.
- Profile CPU, GPU, and memory usage as the game grows.

Aim for 60 FPS on suitable target hardware, but treat this as a performance goal to validate rather than a guaranteed result.

---

# 2. Game overview

## Title

**ARLEN: The Forgotten Paths**

## Genre

Third-person fantasy action RPG with exploration, sword combat, quests, puzzles, and character progression.

## Visual direction

A colorful, stylized fantasy world inspired by the sense of exploration and painterly environmental design found in games such as The Legend of Zelda: Breath of the Wild.

The game must have an original visual identity.

Visual characteristics:

- Stylized anthropomorphic animal characters.
- Handcrafted fantasy villages.
- Soft, atmospheric lighting.
- Colorful forests and landscapes.
- Distinctive character silhouettes.
- Simplified but expressive 3D geometry.
- Painterly or stylized surface materials.
- Ancient ruins and mysterious magical locations.
- Warm, cozy settlements contrasting with dangerous wilderness.

Do not use copyrighted characters, models, environments, music, or other proprietary assets from existing games.

---

# 3. The world of Arlen

Arlen is an ancient fantasy kingdom inhabited primarily by anthropomorphic animals.

The world consists of villages, forests, mountains, ruins, temples, and forgotten roads.

Its central mystery concerns ancient magical monuments called **Waybells**.

These monuments once maintained the kingdom's magical pathways and preserved its memories.

Centuries ago, the Waybells began falling silent.

As their power disappeared, roads and locations began vanishing from existence.

People gradually forgot the places that disappeared.

Meanwhile, a mysterious purple corruption known as **the Blight** began spreading across the kingdom.

The Blight corrupts wildlife, damages ancient ruins, and transforms peaceful regions into dangerous environments.

## Main antagonist: The Warden

The Warden is an ancient guardian created to protect Arlen.

Over centuries, its understanding of protection became corrupted.

It concluded that preventing change was the only way to preserve peace.

The Warden now attempts to preserve the kingdom by erasing memories, places, and anything that threatens its vision of an unchanging world.

It controls ancient constructs and is connected to the corruption spreading across Arlen.

---

# 4. Main protagonist

The playable protagonist is a **young male anthropomorphic gray tabby cat**.

He is the legendary **Last Wayfinder**, the Chosen One destined to restore the Waybells and confront the Warden.

## Appearance

The character is inspired by a real gray tabby domestic cat.

Design characteristics:

- Male.
- Young adult.
- Gray and brown striped fur.
- Cream-colored muzzle and chest.
- Green-yellow expressive eyes.
- Triangular cat ears.
- Long striped tail.
- Athletic, agile build.
- Green adventurer's tunic.
- Teal scarf or short shoulder cape.
- Leather belt and satchel.
- Light adventurer's boots and gloves.
- One-handed sword.

His visual identity should combine a charming, approachable appearance with the presence of an extremely capable warrior.

He should look recognizable from a third-person gameplay camera.

## Personality

He is:

- Confident.
- Brave.
- Curious.
- Playful.
- Slightly sarcastic.
- Independent.
- Fiercely loyal to his friends.
- Fearless in combat.

He has an adventurous personality and enjoys exploring mysterious locations.

He is not an inexperienced hero.

**He has spent years training in swordsmanship and wilderness survival.**

## Background

He was raised and trained by Eldon, an elderly cartographer who was once a legendary warrior belonging to an order known as the Pathwardens.

Eldon recognized the protagonist's potential and secretly trained him to become a powerful swordsman.

The protagonist is descended from the ancient Wayfinders.

He possesses an inherited connection to the Waybells and can remember places erased from the memories of others.

His magical abilities awaken throughout the story.

## Initial development implementation

Do not block development waiting for a finished cat model.

Initially:

1. Use a placeholder humanoid character.
2. Implement all movement and combat systems independently of the final visual model.
3. Keep character visuals separate from gameplay logic.
4. Support replacement of the placeholder with a custom Blender model.
5. Allow future integration of feline animations, ear movement, and tail animation.

If useful, create a simple temporary cat-like model using Unity primitives.

Do not represent such a placeholder as a finished production character.

---

# 5. Character movement

Implement a responsive third-person character controller.

## Required features

- Walking.
- Running.
- Sprinting.
- Jumping.
- Gravity.
- Ground detection.
- Camera-relative movement.
- Smooth character rotation.
- Directional dodge rolling.
- Sprint stamina consumption.
- Configurable movement speed.
- Mouse-controlled third-person camera.

Use Unity's Input System.

Initial keyboard and mouse controls:

| Action | Input |
|---|---|
| Move | WASD |
| Camera | Mouse |
| Jump | Space |
| Sprint | Left Shift |
| Dodge | Left Alt |
| Light attack | Left mouse button |
| Parry | Right mouse button |
| Interact | E |
| Pause | Escape |

Make controls configurable and suitable for future controller support.

Movement should feel agile and responsive.

Avoid excessive acceleration delays or unresponsive input.

Design the system to support climbing, swimming, and advanced traversal in later milestones.

---

# 6. Combat system

The protagonist is already a highly trained swordsman.

Combat should emphasize agility, timing, precision, and fluid movement.

## Light attacks

Implement:

- Basic sword attacks.
- A three-hit combo structure.
- Configurable attack damage.
- Attack cooldowns and timing windows.
- Hit detection.
- Basic attack feedback.

Use placeholder animations or clearly documented animation integration points until production animations exist.

## Dodge

Implement:

- Directional dodging.
- Configurable dodge distance and duration.
- Stamina consumption.
- A short invulnerability window.
- Prevention of unlimited dodge spamming.

## Parry

Implement:

- A timed parry window.
- Successful attack deflection.
- Enemy stagger.
- Configurable timing and recovery.

## Health

Create a reusable health system supporting:

- Damage.
- Healing.
- Maximum health.
- Death.
- Damage events.
- UI updates.

## Stamina

Create a reusable stamina component supporting:

- Maximum stamina.
- Stamina consumption.
- Regeneration.
- Regeneration delays.
- Events for UI and gameplay.

Avoid creating one massive combat script.

Separate player input, health, stamina, attack handling, and damage application into maintainable components.

## Future magical abilities

The protagonist eventually learns ancient Wayfinder abilities.

Planned powers:

**Waystep**

A short magical dash that allows rapid repositioning.

**Echo Blade**

A magical enhancement applied to sword attacks.

**Wayfinder's Sight**

Reveals hidden pathways and ancient objects.

**Bellstrike**

A powerful magical attack capable of damaging corrupted enemies.

**Awakening**

A temporary empowered combat state.

These abilities are future features. Do not implement them during the initial milestone.

---

# 7. Enemy system

Create an extensible enemy architecture.

Enemies should share reusable systems such as:

- Health.
- Damage handling.
- Detection.
- Navigation.
- Attack logic.
- Death.
- Configurable statistics.

## First enemy: Boar Ruffian

The Boar Ruffian is an anthropomorphic boar carrying a wooden club.

It serves as the first combat opponent.

Implement:

1. Idle behavior.
2. Player detection.
3. Chasing.
4. Melee attack.
5. Taking damage.
6. Basic stagger feedback.
7. Death.

Use Unity's NavMesh navigation where appropriate.

Keep enemy decisions separate from animation and visual presentation.

## Planned future enemies

- Ruffian Archer.
- Ruffian Brute.
- Dire Wolf.
- Forest Boar.
- Corrupted Wolf.
- Blight Wisp.
- Ancient Construct.
- Ancient Sentinel.

Only implement the Boar Ruffian during the first enemy development milestone.

Design the architecture so additional enemy types can reuse existing components.

---

# 8. Haven Village

Haven Village is the protagonist's home and the initial hub of the game.

It is a peaceful woodland settlement inhabited by anthropomorphic animals.

## Architecture

The village has a cozy, handcrafted fantasy aesthetic.

Features include:

- Timber-frame cottages.
- Stone foundations.
- Moss-covered roofs.
- Wooden bridges.
- Small gardens.
- Warm lantern lighting.
- Cobblestone pathways.
- Wooden fences.
- Flower beds.
- A central square.
- A large village oak tree.

Buildings should feel lived in rather than purely decorative.

## Initial locations

Create:

1. The protagonist's cabin.
2. Village square.
3. Blacksmith workshop.
4. Bakery.
5. Forest entrance.

Initially, build the village using simple placeholder geometry.

Use a modular approach to architecture so we can replace placeholders with original Blender assets.

Do not prioritize detailed decoration before movement and exploration are enjoyable.

---

# 9. The protagonist's cabin

The main character lives in a small wooden cabin near the village.

The cabin is his home and safe location.

## Exterior

- Wooden walls.
- Stone chimney.
- Mossy roof.
- Small porch.
- Warm window lighting.
- Forest surroundings.
- Simple wooden fence.
- Small outdoor garden.

## Interior

Include:

- Bed.
- Fireplace.
- Desk.
- Map of Arlen.
- Bookshelf.
- Storage chest.
- Equipment rack.
- Small kitchen.
- Window overlooking the forest.

The cabin should visually communicate the protagonist's personality as an adventurous swordsman.

He keeps maps, weapons, books, and objects collected from past travels.

## Gameplay

The player must eventually be able to:

- Enter and leave the cabin.
- Open storage.
- Examine objects.
- Rest.
- Save progress.
- Interact with personal equipment.

Only basic entry and interaction are required for the initial cabin milestone.

---

# 10. Interaction system

Implement a reusable interaction framework.

The player should eventually be able to interact with:

- NPCs.
- Doors.
- Items.
- Chests.
- Notes.
- Quest objects.
- Waybells.

Prefer an interface-based implementation.

Possible design:

- `IInteractable`
- `PlayerInteractor`
- `InteractableObject`

Use camera-based raycasting or another reliable interaction detection method.

Display contextual prompts such as:

"Press E to interact"

Do not tightly couple interaction logic to individual world objects.

---

# 11. NPC system

The world should contain memorable characters with distinct personalities.

## Eldon — The Missing Cartographer

An elderly cat who raised and trained the protagonist.

He was once a legendary Pathwarden.

His disappearance starts the main story.

## Bram — The Blacksmith

A grumpy but caring orange tabby cat.

He helps maintain the protagonist's equipment and provides weapon upgrades.

## Mabel — The Baker

A friendly calico cat who operates the village bakery.

She provides food, information, and early quests.

## Initial NPC functionality

Implement a foundation for:

- NPC interaction.
- Dialogue.
- Character names.
- Dialogue progression.
- Quest-related conversations.

Prefer a data-driven dialogue structure using ScriptableObjects or serialized data.

Do not build a complex branching narrative editor during the first milestone.

---

# 12. Quest system

Implement a modular quest framework supporting:

- Quest identifiers.
- Quest descriptions.
- Objectives.
- Quest states.
- Objective progression.
- Completion events.
- Quest tracking.
- Future saving and loading.

## First quest: The Road That Vanished

The protagonist returns to Haven Village after an adventure.

He discovers that a road leading through Mosswood Forest has disappeared.

Nobody in the village remembers it.

Except him.

### Quest objectives

1. Talk to a villager.
2. Investigate the forest entrance.
3. Defeat a Boar Ruffian.
4. Discover an ancient shrine.
5. Activate the silent Waybell.
6. Restore the forgotten path.

When the Waybell activates, ancient magic responds to the protagonist's presence.

This reveals the first sign that he is the Last Wayfinder.

## Quest implementation

Initially, create only the data structures and a small proof-of-concept quest.

Implement the complete first quest during a later development milestone.

---

# 13. User interface

Use Unity UI (uGUI) for the initial game HUD.

Required initial elements:

- Health bar.
- Stamina bar.
- Interaction prompt.
- Quest objective text.
- Pause menu.

Future elements:

- Inventory.
- Equipment.
- Dialogue.
- World map.
- Character statistics.
- Ability unlocks.
- Settings menu.

Keep UI logic decoupled from core gameplay components.

Use events to communicate changes in health, stamina, quests, and interactions.

---

# 14. Art and asset pipeline

The game will eventually use original, stylized 3D assets created in Blender.

Initially, use placeholder models and simple materials.

Establish a workflow for:

1. Modeling in Blender.
2. UV unwrapping.
3. Applying materials.
4. Exporting models as FBX or another Unity-compatible format.
5. Importing into Unity.
6. Configuring materials.
7. Configuring collisions.
8. Setting up rigs and animations where applicable.
9. Creating reusable prefabs.

Create reusable prefabs for common environmental objects.

Examples:

- Trees.
- Rocks.
- Fences.
- Doors.
- Chests.
- Lanterns.
- Buildings.

Avoid introducing large paid asset packages during the prototype stage.

Use original assets, Unity primitives, or appropriately licensed free assets.

---

# 15. Project architecture

Use a clean, maintainable Unity project structure.

Suggested folders:

```text
Assets/
└── Arlen/
    ├── Art/
    │   ├── Characters/
    │   ├── Environments/
    │   ├── Materials/
    │   ├── Models/
    │   └── Textures/
    ├── Audio/
    ├── Animations/
    ├── Data/
    ├── Input/
    ├── Prefabs/
    │   ├── Characters/
    │   ├── Enemies/
    │   ├── Environment/
    │   └── Interactables/
    ├── Scenes/
    ├── Scripts/
    │   ├── Core/
    │   ├── Player/
    │   ├── Camera/
    │   ├── Combat/
    │   ├── AI/
    │   ├── Interaction/
    │   ├── Quests/
    │   ├── Inventory/
    │   ├── UI/
    │   └── Saving/
    ├── Settings/
    └── Tests/
```

Use the namespace:

`Arlen`

Possible classes:

- `ArlenPlayerController`
- `ThirdPersonCameraController`
- `PlayerInputReader`
- `HealthComponent`
- `StaminaComponent`
- `MeleeCombat`
- `Damageable`
- `EnemyController`
- `EnemyCombat`
- `PlayerInteractor`
- `IInteractable`
- `QuestManager`
- `QuestDefinition`
- `InventoryManager`
- `SaveManager`
- `Waybell`

Adapt names and architecture as needed.

Avoid unnecessary abstraction, excessive singleton usage, or implementing systems before they are needed.

Prefer composition, clear responsibilities, and dependency boundaries.

---

# 16. Development milestones

Build the game incrementally.

Each milestone should produce something playable and testable.

## Milestone 1 — Playable foundation

**This is the milestone to implement first.**

Tasks:

1. Inspect the existing Unity project.
2. Determine the installed Unity version.
3. Verify the render pipeline.
4. Configure or retain URP as appropriate.
5. Create a lightweight test environment.
6. Implement third-person movement.
7. Implement camera controls.
8. Implement jumping and sprinting.
9. Implement directional dodging.
10. Implement health and stamina components.
11. Implement one simple melee attack.
12. Add basic health and stamina UI.
13. Test the gameplay loop.

### Expected result

A controllable third-person character in a simple 3D environment.

The character can:

- Move.
- Run.
- Jump.
- Dodge.
- Attack.
- Consume and regenerate stamina.
- Take damage.

The camera follows the player smoothly.

The project should run on macOS and remain compatible with Windows.

Use placeholders where necessary.

Do not implement the entire RPG during this milestone.

## Milestone 2 — Combat prototype

- Implement the Boar Ruffian.
- Create enemy detection and pursuit.
- Implement enemy attacks.
- Implement player parrying.
- Add basic hit feedback.
- Add enemy health and death.
- Balance initial combat timing.

### Expected result

A playable fight between the protagonist and one enemy.

## Milestone 3 — Exploration prototype

- Create a small outdoor environment.
- Greybox Haven Village.
- Build the protagonist's cabin.
- Implement interactions.
- Add an NPC.
- Implement basic dialogue.
- Create a forest path.

### Expected result

A playable village area with simple exploration and interactions.

## Milestone 4 — RPG systems

- Basic quest tracking.
- Inventory.
- Item collection.
- Equipment framework.
- Save and load.
- Waybell interaction.

### Expected result

A small RPG gameplay loop that connects exploration, combat, and objectives.

## Milestone 5 — First complete adventure

- Mosswood Forest.
- First dungeon.
- Ancient Waybell shrine.
- The Road That Vanished quest.
- Boss encounter.
- Story introduction and ending.

### Expected result

A complete short adventure with a beginning, middle, and ending.

## Milestone 6 — Art and polish

- Final protagonist model.
- Custom animations.
- Stylized environment assets.
- Updated lighting.
- Sound effects.
- Music.
- Visual effects.
- Dialogue presentation.
- Optimization.
- Bug fixing.

---

# 17. Development standards

Follow professional Unity development practices.

## Code quality

- Use readable, idiomatic C#.
- Follow SOLID principles where practical.
- Prefer composition over inheritance.
- Keep MonoBehaviours focused.
- Use ScriptableObjects where appropriate.
- Avoid excessive work in `Update()`.
- Use physics update methods appropriately.
- Avoid unnecessary runtime allocations.
- Make settings configurable through serialized fields.
- Add useful validation and error messages.
- Separate game logic from visuals.

## Scene and prefab management

- Prefer reusable prefabs.
- Keep scene hierarchies organized.
- Avoid hard-coded object names and scene dependencies where possible.
- Keep gameplay systems independent of specific character meshes.
- Preserve existing assets and scenes unless changes are required.

## Source control

- Configure an appropriate Unity `.gitignore`.
- Commit required `.meta` files.
- Do not commit generated Library or Temp folders.
- Use Git LFS for large binary assets where appropriate.
- Avoid unnecessary binary file modifications.
- Document important changes.

## Testing

Use Unity Test Framework for logic that can be tested independently.

Validate:

- Movement and input behavior.
- Health calculations.
- Stamina consumption.
- Combat damage.
- Quest progression when implemented.

Do not claim that code compiles or gameplay works unless it has actually been validated.

---

# 18. Working with Unity Editor and Codex

You are assisting me through Codex.

You may not have direct control over my graphical Unity Editor.

Therefore:

1. Inspect the project before changing anything.
2. Work directly with project files when possible.
3. Preserve working scenes and prefabs.
4. Do not invent references to assets that do not exist.
5. Use Unity Editor scripts when useful to automate repetitive setup.
6. Make editor scripts safe to rerun.
7. Explain manual operations that cannot be automated.
8. Run available compilation checks and tests.
9. Clearly identify anything that has not been tested.

When appropriate, create an editor menu command such as:

`Tools > Arlen > Create Prototype Scene`

This command can generate a basic playable environment using Unity primitives, assign required components, and create placeholder objects.

It must not overwrite existing user scenes without explicit permission.

The goal is to minimize manual setup while keeping the project maintainable.

---

# 19. How we will work together

I want to develop this game iteratively, one feature at a time.

Do not try to build every system at once.

For each milestone:

1. Explain the implementation approach briefly.
2. Identify the files to create or modify.
3. Implement the feature.
4. Perform available checks.
5. Explain how to test it in Unity.
6. Identify known limitations.
7. Suggest the next logical development task.

When implementing features, prioritize working code rather than lengthy theoretical explanations.

Avoid unnecessary questions when reasonable implementation decisions can be made.

Do not add unrelated functionality beyond the current milestone.

I will provide feedback, screenshots, Unity errors, and additional design requirements as development progresses.

---

# 20. First task — Begin development

**Start implementing Milestone 1 now.**

First inspect the Unity project and identify:

- Unity version.
- Installed packages.
- Active render pipeline.
- Existing scenes.
- Existing scripts.
- Existing input configuration.
- Existing player prefabs.

If the project already contains working systems, reuse or extend them rather than replacing everything.

If no usable player setup exists, create one using Unity's CharacterController, Input System, and a suitable third-person camera.

Build the smallest playable implementation that demonstrates movement, jumping, sprinting, dodging, and one sword attack.

Add basic health and stamina functionality.

Create or document a lightweight prototype scene.

Verify as much as the available environment permits.

At the end, report:

1. What was implemented.
2. Which files were created or modified.
3. Which checks or tests passed.
4. Which steps must be completed in Unity Editor.
5. How to run the game.
6. What should be implemented next.

**The immediate objective is a playable third-person action game prototype—not the complete open-world RPG.**

Begin development of **ARLEN: The Forgotten Paths** using Unity and C#.
