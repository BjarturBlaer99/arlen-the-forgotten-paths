using System;
using Arlen.Combat;
using UnityEngine;

namespace Arlen.AI
{
    public enum EnemyState
    {
        Idle,
        Alert,
        Chase,
        Attack,
        Stagger,
        Return,
        Dead,
    }

    /// <summary>
    /// Enemy brain: a small state machine that decides what to do and delegates to the
    /// sensor (perception), motor (navigation) and attack (execution) components. Presentation
    /// listens to its events and never feeds back into decisions.
    ///
    /// Idle (wander near home) → Alert (spotted target) → Chase → Attack ⇄ Chase
    /// Any → Stagger (poise broken or parried) → Chase · Chase → Return (leash/lost) → Idle · Any → Dead
    /// </summary>
    [RequireComponent(typeof(HealthComponent), typeof(EnemySensor), typeof(EnemyMotor))]
    [RequireComponent(typeof(EnemyMeleeAttack))]
    [DisallowMultipleComponent]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] EnemyStats stats;
        [SerializeField] Team team = Team.Enemy;
        [Tooltip("Colliders disabled on death so the player can walk over the corpse.")]
        [SerializeField] Collider[] disableOnDeath;
        [Tooltip("Hide the enemy when the corpse timer runs out (a spawner can bring it back).")]
        [SerializeField] bool deactivateWhenCorpseExpires = true;

        HealthComponent health;
        EnemySensor sensor;
        EnemyMotor motor;
        EnemyMeleeAttack attack;

        Vector3 homePosition;
        Quaternion homeRotation;
        float stateTimer;
        float stateDuration;
        float attackCooldown;
        float poiseDamage;
        float poiseTimer;
        bool wanderMoving;

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public EnemyStats Stats => stats;
        public Team Team => team;
        public Targetable Target => sensor != null && sensor.HasTarget ? sensor.CurrentTarget : null;
        public float StateTime => stateTimer;
        public bool LastStaggerFromParry { get; private set; }
        public Vector3 HomePosition => homePosition;

        /// <summary>(previous, next)</summary>
        public event Action<EnemyState, EnemyState> StateChanged;
        /// <summary>Raised when staggered; true if caused by a parry.</summary>
        public event Action<bool> Staggered;
        public event Action Died;
        public event Action CorpseExpired;

        /// <summary>Assign stats from code before the object is activated (tests, spawners).</summary>
        public void SetStats(EnemyStats value) => stats = value;

        void Awake()
        {
            health = GetComponent<HealthComponent>();
            sensor = GetComponent<EnemySensor>();
            motor = GetComponent<EnemyMotor>();
            attack = GetComponent<EnemyMeleeAttack>();

            if (stats == null)
            {
                Debug.LogError($"{nameof(EnemyController)} on '{name}' has no {nameof(EnemyStats)} assigned.", this);
                enabled = false;
                return;
            }

            health.SetMaxHealth(stats.maxHealth, keepRatio: true);
            sensor.Initialize(stats, team);
            motor.Initialize(stats);
            attack.Initialize(stats, motor, team);
            if (TryGetComponent(out Targetable targetable)) targetable.Configure(team);

            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        void OnEnable()
        {
            if (stats == null) return;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            attack.Parried += OnParried;
            attack.Finished += OnAttackFinished;
            sensor.TargetAcquired += OnTargetAcquired;
        }

        void OnDisable()
        {
            if (stats == null) return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            attack.Parried -= OnParried;
            attack.Finished -= OnAttackFinished;
            sensor.TargetAcquired -= OnTargetAcquired;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTimer += dt;
            if (attackCooldown > 0f) attackCooldown -= dt;
            if (poiseTimer > 0f)
            {
                poiseTimer -= dt;
                if (poiseTimer <= 0f) poiseDamage = 0f;
            }

            switch (State)
            {
                case EnemyState.Idle: UpdateIdle(); break;
                case EnemyState.Alert: UpdateAlert(); break;
                case EnemyState.Chase: UpdateChase(); break;
                case EnemyState.Stagger: if (stateTimer >= stateDuration) Enter(Target != null ? EnemyState.Chase : EnemyState.Return); break;
                case EnemyState.Return: UpdateReturn(); break;
                // Attack: driven by EnemyMeleeAttack until Finished/Parried. Dead: nothing.
            }
        }

        // ───────────── States ─────────────

        void UpdateIdle()
        {
            if (stats.wanderRadius <= 0f) return;

            if (wanderMoving)
            {
                if (motor.HasArrived || stateTimer > 8f)
                {
                    wanderMoving = false;
                    stateTimer = 0f;
                    stateDuration = UnityEngine.Random.Range(stats.wanderPause.x, stats.wanderPause.y);
                }
            }
            else if (stateTimer >= stateDuration)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * stats.wanderRadius;
                motor.MoveTo(homePosition + new Vector3(offset.x, 0f, offset.y), stats.wanderSpeed);
                wanderMoving = true;
                stateTimer = 0f;
            }
        }

        void UpdateAlert()
        {
            var target = Target;
            if (target == null) { Enter(EnemyState.Return); return; }
            motor.FaceTowards(target.transform.position, stats.turnSpeed);
            if (stateTimer >= stats.alertDelay) Enter(EnemyState.Chase);
        }

        void UpdateChase()
        {
            var target = Target;
            if (target == null || PlanarDistance(homePosition, transform.position) > stats.leashRadius)
            {
                sensor.Forget();
                Enter(EnemyState.Return);
                return;
            }

            Vector3 targetPos = target.transform.position;
            float distance = PlanarDistance(transform.position, targetPos);

            if (distance <= stats.attackRange)
            {
                motor.Stop();
                motor.FaceTowards(targetPos, stats.turnSpeed);
                if (attackCooldown <= 0f && motor.AngleTo(targetPos) < 35f)
                {
                    Enter(EnemyState.Attack);
                    attack.Begin(target);
                }
            }
            else
            {
                motor.MoveTo(targetPos, stats.chaseSpeed, stats.attackRange * 0.75f);
            }
        }

        void UpdateReturn()
        {
            motor.MoveTo(homePosition, stats.wanderSpeed * 1.5f);
            if (motor.HasArrived || stateTimer > 20f)
            {
                health.Revive(); // classic leash reset: back home at full health
                sensor.Suppressed = false;
                Enter(EnemyState.Idle);
            }
        }

        void Enter(EnemyState next)
        {
            var previous = State;
            if (previous == EnemyState.Attack && next != EnemyState.Attack) attack.Cancel();

            State = next;
            stateTimer = 0f;
            stateDuration = 0f;
            wanderMoving = false;
            sensor.Suppressed = next == EnemyState.Return;

            if (next == EnemyState.Idle)
                stateDuration = UnityEngine.Random.Range(stats.wanderPause.x, stats.wanderPause.y);
            if (next == EnemyState.Alert || next == EnemyState.Stagger || next == EnemyState.Dead)
                motor.Stop();

            StateChanged?.Invoke(previous, next);
        }

        // ───────────── Events ─────────────

        void OnTargetAcquired(Targetable _)
        {
            if (State == EnemyState.Idle) Enter(EnemyState.Alert);
        }

        void OnDamaged(DamageInfo info)
        {
            if (State == EnemyState.Dead) return;

            // Hit from behind or while unaware: turn on the attacker immediately.
            if (State == EnemyState.Idle || State == EnemyState.Alert || State == EnemyState.Return)
            {
                var attacker = info.Source != null ? info.Source.GetComponentInParent<Targetable>() : null;
                if (attacker != null && Targetable.AreHostile(team, attacker.Team))
                {
                    sensor.Suppressed = false;
                    sensor.SetTarget(attacker);
                    if (State != EnemyState.Stagger) Enter(EnemyState.Chase);
                }
            }

            poiseDamage += info.Amount;
            poiseTimer = stats.poiseRegenDelay;
            if (poiseDamage >= stats.poise && info.Amount < health.Current)
            {
                poiseDamage = 0f;
                Stagger(stats.hitStaggerDuration, fromParry: false);
            }
        }

        void OnParried() => Stagger(stats.parryStaggerDuration, fromParry: true);

        void Stagger(float duration, bool fromParry)
        {
            if (State == EnemyState.Dead) return;
            LastStaggerFromParry = fromParry;
            Enter(EnemyState.Stagger);
            stateDuration = duration;
            // A short reprieve after recovering so the enemy doesn't instantly swing back.
            attackCooldown = Mathf.Max(attackCooldown, 0.4f);
            Staggered?.Invoke(fromParry);
        }

        void OnAttackFinished()
        {
            attackCooldown = UnityEngine.Random.Range(stats.attackCooldown.x, stats.attackCooldown.y);
            if (State == EnemyState.Attack) Enter(EnemyState.Chase);
        }

        void OnDied()
        {
            Enter(EnemyState.Dead);
            sensor.Forget();
            motor.SetNavigationEnabled(false);
            SetCollidersEnabled(false);
            Died?.Invoke();
            if (team == Team.Enemy) Core.WorldSignals.Raise(Core.SignalKind.Defeated, stats.Id);
            Invoke(nameof(ExpireCorpse), stats.corpseDuration);
        }

        void ExpireCorpse()
        {
            CorpseExpired?.Invoke();
            if (deactivateWhenCorpseExpires && State == EnemyState.Dead) gameObject.SetActive(false);
        }

        /// <summary>Revives the enemy at its home position (used by spawners and tests).</summary>
        public void ResetToHome()
        {
            CancelInvoke(nameof(ExpireCorpse));
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            SetCollidersEnabled(true);
            motor.SetNavigationEnabled(true);
            motor.Teleport(homePosition, homeRotation);
            health.Revive();
            poiseDamage = 0f;
            attackCooldown = 0f;
            sensor.Forget();
            Enter(EnemyState.Idle);
        }

        void SetCollidersEnabled(bool value)
        {
            if (disableOnDeath == null) return;
            foreach (var c in disableOnDeath)
                if (c != null) c.enabled = value;
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void OnDrawGizmosSelected()
        {
            if (stats == null) return;
            Vector3 home = Application.isPlaying ? homePosition : transform.position;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
            Gizmos.DrawWireSphere(home, stats.leashRadius);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, stats.attackRange);
        }
    }
}
