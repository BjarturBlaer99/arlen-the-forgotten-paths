using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arlen.Player
{
    /// <summary>
    /// The only player script that talks to the Input System. Gameplay components read its state
    /// (Move, Look, Sprint) or consume buffered button presses (Jump, Dodge, Attack), so they never
    /// depend on specific devices or bindings. Rebinding/gamepad support lives entirely in the actions asset.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] string actionMapName = "Player";
        [Tooltip("A button press is remembered this long, so pressing slightly early still triggers the action.")]
        [SerializeField, Min(0f)] float inputBufferTime = 0.15f;

        InputActionMap map;
        InputAction move, look, jump, sprint, walk, dodge, attack, parry, interact, pause;

        float jumpPressedAt = float.NegativeInfinity;
        float dodgePressedAt = float.NegativeInfinity;
        float attackPressedAt = float.NegativeInfinity;

        /// <summary>Camera-independent move input, magnitude clamped to 1.</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Raw look input. Pixels for mouse (see <see cref="LookIsPointerDelta"/>), -1..1 for sticks.</summary>
        public Vector2 Look { get; private set; }
        /// <summary>True when Look came from a mouse/pen delta, which must not be scaled by deltaTime.</summary>
        public bool LookIsPointerDelta { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool WalkToggled { get; private set; }

        /// <summary>When blocked (pause menu, cutscenes) all gameplay input reads as neutral. Pause still works.</summary>
        public bool GameplayBlocked { get; set; }

        public event Action PausePressed;
        /// <summary>Reserved for Milestone 2 (parry) and 3 (interaction).</summary>
        public event Action ParryPressed;
        public event Action InteractPressed;

        public InputActionAsset Actions => actions;

        /// <summary>Assign the actions asset from code (tests, runtime spawning). Call before the component is enabled.</summary>
        public void SetActions(InputActionAsset asset)
        {
            if (map != null) Debug.LogWarning("PlayerInputReader.SetActions called after initialization; ignoring.", this);
            else actions = asset;
        }

        void Awake() => TryInitialize();

        bool TryInitialize()
        {
            if (map != null) return true;
            if (actions == null)
            {
                Debug.LogError($"{nameof(PlayerInputReader)} on '{name}' has no InputActionAsset assigned. " +
                               "Assign Assets/Arlen/Input/ArlenControls.inputactions.", this);
                return false;
            }

            map = actions.FindActionMap(actionMapName, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogError($"Action map '{actionMapName}' not found in '{actions.name}'.", this);
                return false;
            }

            move = Find("Move");
            look = Find("Look");
            jump = Find("Jump");
            sprint = Find("Sprint");
            walk = Find("Walk");
            dodge = Find("Dodge");
            attack = Find("Attack");
            parry = Find("Parry");
            interact = Find("Interact");
            pause = Find("Pause");
            return true;
        }

        InputAction Find(string actionName)
        {
            var action = map.FindAction(actionName, throwIfNotFound: false);
            if (action == null) Debug.LogError($"Input action '{actionName}' is missing from map '{map.name}'.", this);
            return action;
        }

        void OnEnable()
        {
            if (!TryInitialize()) { enabled = false; return; }

            Subscribe(jump, OnJump);
            Subscribe(dodge, OnDodge);
            Subscribe(attack, OnAttack);
            Subscribe(walk, OnWalk);
            Subscribe(parry, OnParry);
            Subscribe(interact, OnInteract);
            Subscribe(pause, OnPause);
            map.Enable();
        }

        void OnDisable()
        {
            if (map == null) return;
            Unsubscribe(jump, OnJump);
            Unsubscribe(dodge, OnDodge);
            Unsubscribe(attack, OnAttack);
            Unsubscribe(walk, OnWalk);
            Unsubscribe(parry, OnParry);
            Unsubscribe(interact, OnInteract);
            Unsubscribe(pause, OnPause);
            map.Disable();
            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
        }

        static void Subscribe(InputAction a, Action<InputAction.CallbackContext> cb) { if (a != null) a.performed += cb; }
        static void Unsubscribe(InputAction a, Action<InputAction.CallbackContext> cb) { if (a != null) a.performed -= cb; }

        void Update()
        {
            if (GameplayBlocked)
            {
                Move = Vector2.zero;
                Look = Vector2.zero;
                SprintHeld = false;
                return;
            }

            Move = move != null ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            Look = look != null ? look.ReadValue<Vector2>() : Vector2.zero;
            LookIsPointerDelta = look?.activeControl?.device is Pointer;
            SprintHeld = sprint != null && sprint.IsPressed();
        }

        void OnJump(InputAction.CallbackContext _) { if (!GameplayBlocked) jumpPressedAt = Time.time; }
        void OnDodge(InputAction.CallbackContext _) { if (!GameplayBlocked) dodgePressedAt = Time.time; }
        void OnAttack(InputAction.CallbackContext _) { if (!GameplayBlocked) attackPressedAt = Time.time; }
        void OnWalk(InputAction.CallbackContext _) { if (!GameplayBlocked) WalkToggled = !WalkToggled; }
        void OnParry(InputAction.CallbackContext _) { if (!GameplayBlocked) ParryPressed?.Invoke(); }
        void OnInteract(InputAction.CallbackContext _) { if (!GameplayBlocked) InteractPressed?.Invoke(); }
        void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();

        /// <summary>Returns true (once) if Jump was pressed within the buffer window.</summary>
        public bool ConsumeJump() => Consume(ref jumpPressedAt);
        public bool ConsumeDodge() => Consume(ref dodgePressedAt);
        public bool ConsumeAttack() => Consume(ref attackPressedAt);

        /// <summary>Drops any buffered presses (e.g. after respawn or unpausing).</summary>
        public void ClearBufferedInput()
        {
            jumpPressedAt = dodgePressedAt = attackPressedAt = float.NegativeInfinity;
        }

        bool Consume(ref float pressedAt)
        {
            if (GameplayBlocked || Time.time - pressedAt > inputBufferTime) return false;
            pressedAt = float.NegativeInfinity;
            return true;
        }
    }
}
