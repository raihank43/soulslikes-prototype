using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Soulslike.Combat;

namespace Soulslike.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyAI : MonoBehaviour
    {
        public enum State { Idle, Aggro, Chase, Attack, Hurt, Dead, Returning }

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyAttack attack;
        [SerializeField] private Transform player;

        [Header("Ranges")]
        [SerializeField] private float aggroRange = 12f;
        [SerializeField] private float loseAggroRange = 25f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float runDistanceThreshold = 6f;

        [Header("Speeds")]
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float runSpeed = 2.5f;

        [Header("Tuning")]
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float hurtDuration = 0.4f;
        [SerializeField] private float repathInterval = 0.2f;
        [SerializeField] private float idleVariantMin = 4f;
        [SerializeField] private float idleVariantMax = 7f;

        private static readonly int SpeedHash        = Animator.StringToHash("Speed");
        private static readonly int IdleVariantHash  = Animator.StringToHash("IdleVariant");
        private static readonly int RoarTriggerHash  = Animator.StringToHash("RoarTrigger");
        private static readonly int PunchTriggerHash = Animator.StringToHash("PunchTrigger");
        private static readonly int HurtTriggerHash  = Animator.StringToHash("HurtTrigger");
        private static readonly int DeadTriggerHash  = Animator.StringToHash("DeadTrigger");

        private NavMeshAgent agent;
        private EnemyHealth health;
        private State state;
        private Vector3 spawnPos;
        private Quaternion spawnRot;
        private float lastAttackTime = -999f;
        private float nextRepathTime;
        private float nextIdleVariantTime;
        private float chaseMoveAllowedTime;
        private Coroutine activeRoutine;

        public State CurrentState => state;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (attack == null) attack = GetComponentInChildren<EnemyAttack>();
            spawnPos = transform.position;
            spawnRot = transform.rotation;
        }

        private void Start()
        {
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }
            EnterIdle();
            health.HealthChanged += OnHealthChanged;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (state == State.Dead) return;

            switch (state)
            {
                case State.Idle:      TickIdle();      break;
                case State.Chase:     TickChase();     break;
                case State.Attack:    TickAttack();    break;
                case State.Returning: TickReturning(); break;
            }
        }

        private float DistanceToPlayer()
        {
            if (player == null) return float.PositiveInfinity;
            return Vector3.Distance(transform.position, player.position);
        }

        private void EnterIdle()
        {
            state = State.Idle;
            agent.isStopped = true;
            agent.ResetPath();
            SetSpeedParam(0f);
            nextIdleVariantTime = Time.time + Random.Range(idleVariantMin, idleVariantMax);
        }

        private void TickIdle()
        {
            // Idle variant cycling
            if (Time.time >= nextIdleVariantTime)
            {
                int next = animator != null ? 1 - animator.GetInteger(IdleVariantHash) : 0;
                if (animator != null) animator.SetInteger(IdleVariantHash, next);
                nextIdleVariantTime = Time.time + Random.Range(idleVariantMin, idleVariantMax);
            }

            float dist = DistanceToPlayer();
            if (dist <= aggroRange) EnterAggro();
        }

        private void EnterAggro()
        {
            state = State.Aggro;
            agent.isStopped = true;
            if (animator != null) animator.SetTrigger(RoarTriggerHash);
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(AggroRoutine());
        }

        private IEnumerator AggroRoutine()
        {
            // Wait for animator to enter Roar state (the Any→Roar transition takes a frame or two)
            float enterTimeout = Time.time + 0.5f;
            while (Time.time < enterTimeout)
            {
                if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Roar")) break;
                yield return null;
            }
            // Wait until Roar fully exits (clip end + transition out)
            float exitTimeout = Time.time + 8f;
            while (Time.time < exitTimeout)
            {
                if (animator == null) break;
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName("Roar") && !animator.IsInTransition(0)) break;
                yield return null;
            }
            EnterChase();
        }

        private void EnterChase()
        {
            state = State.Chase;
            agent.isStopped = true;
            agent.speed = walkSpeed;
            // Hold movement briefly so the animator finishes its transition into Locomotion
            // before the body starts translating. Avoids glide-out-of-Roar.
            chaseMoveAllowedTime = Time.time + 0.2f;
        }

        private void TickChase()
        {
            float dist = DistanceToPlayer();
            if (dist > loseAggroRange) { EnterReturning(); return; }
            if (dist <= attackRange && Time.time - lastAttackTime >= attackCooldown) { EnterAttack(); return; }

            if (Time.time < chaseMoveAllowedTime)
            {
                agent.isStopped = true;
                SetSpeedParam(0f);
                return;
            }

            agent.isStopped = false;
            agent.speed = (dist > runDistanceThreshold) ? runSpeed : walkSpeed;

            if (Time.time >= nextRepathTime && player != null)
            {
                agent.SetDestination(player.position);
                nextRepathTime = Time.time + repathInterval;
            }

            float speedParam = 0f;
            if (agent.desiredVelocity.sqrMagnitude > 0.01f)
            {
                speedParam = (agent.speed >= runSpeed - 0.01f) ? 1f : 0.5f;
            }
            SetSpeedParam(speedParam);
        }

        private void EnterAttack()
        {
            state = State.Attack;
            agent.isStopped = true;
            agent.ResetPath();
            SetSpeedParam(0f);
            SnapFacePlayer(); // commit strike direction at swing-start (soulslike convention)
            if (attack != null) attack.BeginAttack();
            if (animator != null) animator.SetTrigger(PunchTriggerHash);
            lastAttackTime = Time.time;
        }

        private void SnapFacePlayer()
        {
            if (player == null) return;
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private void TickAttack()
        {
            // Mutant commits to its strike direction at EnterAttack — no per-frame tracking
            // while swinging (avoids body rotating mid-punch which makes feet skid against
            // the locked-in-place punch pose). The player has to actually dodge.
            if (attack != null && attack.IsAttackComplete)
            {
                EnterChase();
            }
        }

        private void EnterReturning()
        {
            state = State.Returning;
            agent.isStopped = false;
            agent.speed = walkSpeed;
            agent.SetDestination(spawnPos);
        }

        private void TickReturning()
        {
            if (DistanceToPlayer() <= aggroRange)
            {
                EnterAggro();
                return;
            }

            float v = agent.velocity.magnitude;
            SetSpeedParam(v > 0.1f ? 0.5f : 0f);

            if (!agent.pathPending && agent.remainingDistance <= 0.3f)
            {
                transform.rotation = spawnRot;
                EnterIdle();
            }
        }

        private void OnHealthChanged(int current, int max)
        {
            if (current <= 0 || state == State.Dead) return;
            // Hurt only fires on actual damage taken (not on the initial Awake invoke)
            if (current >= max) return;
            EnterHurt();
        }

        private void EnterHurt()
        {
            // Don't interrupt own attack/dying with stun-spam unless still alive
            state = State.Hurt;
            agent.isStopped = true;
            if (attack != null) attack.ForceCancel();
            if (animator != null) animator.SetTrigger(HurtTriggerHash);
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(HurtRoutine());
        }

        private IEnumerator HurtRoutine()
        {
            yield return new WaitForSeconds(hurtDuration);
            if (state == State.Hurt) EnterChase();
        }

        private void OnDied()
        {
            if (state == State.Dead) return;
            state = State.Dead;
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            agent.isStopped = true;
            agent.enabled = false;
            if (attack != null) attack.ForceCancel();
            if (animator != null)
            {
                animator.applyRootMotion = true;
                animator.SetTrigger(DeadTriggerHash);
            }
            this.enabled = false;
        }

        private void FacePlayer()
        {
            if (player == null) return;
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 540f * Time.deltaTime);
        }

        private void SetSpeedParam(float v)
        {
            if (animator != null) animator.SetFloat(SpeedHash, v, 0.05f, Time.deltaTime);
        }
    }
}
