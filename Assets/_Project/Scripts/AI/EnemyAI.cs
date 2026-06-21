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
        [SerializeField] private MutantRootMotionForwarder rootMotionForwarder;
        [SerializeField] private Transform player;

        [Header("Lunge Amplifiers")]
        [SerializeField] private float swipeMotionMultiplier = 1.5f;
        [SerializeField] private float jumpAttackClipTravel = 1.0f; // estimated baked travel of the JumpAttack clip in meters
        [SerializeField] private float jumpAttackLandClearance = 1.0f; // meters short of player to aim for
        [SerializeField] private float jumpAttackMaxMultiplier = 4.0f;

        [Header("Ranges")]
        [SerializeField] private float aggroRange = 12f;
        [SerializeField] private float loseAggroRange = 25f;
        [SerializeField] private float runDistanceThreshold = 6f;

        [Header("Attack Bands")]
        [SerializeField] private float punchRange = 1.6f;
        [SerializeField] private float swipeRange = 2.8f;
        [SerializeField] private float jumpAttackMinRange = 3.5f;
        [SerializeField] private float jumpAttackMaxRange = 6.0f;

        [Header("Speeds")]
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float runSpeed = 2.5f;

        [Header("Tuning")]
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float jumpAttackCooldown = 5.0f;
        [SerializeField] private float hurtDuration = 0.4f;
        [SerializeField] private float repathInterval = 0.2f;
        [SerializeField] private float idleVariantMin = 4f;
        [SerializeField] private float idleVariantMax = 7f;

        [Header("Combo & Tells")]
        [SerializeField, Range(0f, 1f)] private float comboChance = 0.5f;
        [SerializeField] private int comboMaxLength = 2;
        [SerializeField] private float windupMin = 0.0f;
        [SerializeField] private float windupMax = 0.25f;
        [SerializeField] private float attackSpeedMin = 0.92f;
        [SerializeField] private float attackSpeedMax = 1.08f;

        private static readonly int SpeedHash        = Animator.StringToHash("Speed");
        private static readonly int IdleVariantHash  = Animator.StringToHash("IdleVariant");
        private static readonly int RoarTriggerHash  = Animator.StringToHash("RoarTrigger");
        private static readonly int HurtTriggerHash  = Animator.StringToHash("HurtTrigger");
        private static readonly int DeadTriggerHash  = Animator.StringToHash("DeadTrigger");
        private static readonly int AttackSpeedHash  = Animator.StringToHash("AttackSpeed");
        private static readonly int AttackingTagHashEnemy = Animator.StringToHash("Attacking");

        private NavMeshAgent agent;
        private EnemyHealth health;
        private State state;
        private Vector3 spawnPos;
        private Quaternion spawnRot;
        private float lastAttackTime = -999f;
        private float lastJumpAttackTime = -999f;
        private float nextRepathTime;
        private float nextIdleVariantTime;
        private float chaseMoveAllowedTime;
        private Coroutine activeRoutine;
        private Coroutine attackRoutine;
        private int chainCount;
        private EnemyAttackType? lastAttackType;

        public State CurrentState => state;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (attack == null) attack = GetComponentInChildren<EnemyAttack>();
            if (rootMotionForwarder == null) rootMotionForwarder = GetComponentInChildren<MutantRootMotionForwarder>();
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
            if (Time.time - lastAttackTime >= attackCooldown && PickAttackForRange(dist).HasValue)
            {
                EnterAttack();
                return;
            }

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

        private EnemyAttackType? PickAttackForRange(float dist)
        {
            // Each band picks weighted-random across the moves valid for that distance.
            // No-repeat: bias against the last attack type when the band has alternatives.
            // Far band has a real "chase closer" option so JumpAttack isn't spammed.
            if (dist <= punchRange)
            {
                return WeightedPick(
                    (EnemyAttackType.Punch, 0.70f),
                    (EnemyAttackType.Swipe, 0.30f));
            }
            if (dist <= swipeRange)
            {
                return WeightedPick(
                    (EnemyAttackType.Swipe, 0.55f),
                    (EnemyAttackType.Punch, 0.45f));
            }
            if (dist >= jumpAttackMinRange && dist <= jumpAttackMaxRange)
            {
                // JumpAttack is gated by its own longer cooldown so the AI can't spam-leap
                // at a player who hovers in the jump band. While on cooldown, the AI returns
                // null → drops back to Chase so it actually closes the gap.
                if (Time.time - lastJumpAttackTime < jumpAttackCooldown) return null;
                if (Random.value < 0.50f) return EnemyAttackType.JumpAttack;
                return null;
            }
            return null;
        }

        private EnemyAttackType WeightedPick(params (EnemyAttackType type, float weight)[] choices)
        {
            // Halve the weight of last-used attack to bias against repeats.
            float total = 0f;
            float[] adjusted = new float[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                float w = choices[i].weight;
                if (lastAttackType.HasValue && choices[i].type == lastAttackType.Value) w *= 0.5f;
                adjusted[i] = w;
                total += w;
            }
            float roll = Random.value * total;
            float cumulative = 0f;
            for (int i = 0; i < choices.Length; i++)
            {
                cumulative += adjusted[i];
                if (roll <= cumulative) return choices[i].type;
            }
            return choices[choices.Length - 1].type;
        }

        private void EnterAttack()
        {
            state = State.Attack;
            chainCount = 0;
            agent.isStopped = true;
            agent.ResetPath();
            SetSpeedParam(0f);
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            while (true)
            {
                float dist = DistanceToPlayer();
                var pick = PickAttackForRange(dist);
                if (!pick.HasValue) break;

                SnapFacePlayer(); // commit strike direction at swing-start
                float windup = Random.Range(windupMin, windupMax);
                if (windup > 0f) yield return new WaitForSeconds(windup);
                // Hurt/death may have interrupted us during the windup
                if (state != State.Attack) yield break;

                float speedMult = Random.Range(attackSpeedMin, attackSpeedMax);
                if (animator != null) animator.SetFloat(AttackSpeedHash, speedMult);
                if (rootMotionForwarder != null)
                {
                    float mult = 1f;
                    if (pick.Value == EnemyAttackType.Swipe) mult = swipeMotionMultiplier;
                    else if (pick.Value == EnemyAttackType.JumpAttack)
                    {
                        // Aim to land jumpAttackLandClearance short of the player so the
                        // leap doesn't sail past. Player can still dodge by moving during
                        // the swing — the multiplier is set at strike-commit.
                        float distAtStart = DistanceToPlayer();
                        float targetTravel = Mathf.Max(0.5f, distAtStart - jumpAttackLandClearance);
                        mult = Mathf.Clamp(targetTravel / Mathf.Max(0.1f, jumpAttackClipTravel), 1f, jumpAttackMaxMultiplier);
                    }
                    rootMotionForwarder.motionMultiplier = mult;
                }
                if (attack != null) attack.BeginAttack(pick.Value);
                lastAttackTime = Time.time;
                lastAttackType = pick.Value;
                if (pick.Value == EnemyAttackType.JumpAttack) lastJumpAttackTime = Time.time;

                // Wait an absolute swing duration scaled by AttackSpeed. We don't poll the
                // animator state because Unity inflates FBX clip lengths inconsistently
                // (Punch stays at 1.1s but Swipe/JumpAttack get 2-3x stretching), which
                // makes normalized-time thresholds unreliable across attacks.
                float duration = EnemyAttack.DurationFor(pick.Value) / Mathf.Max(0.01f, speedMult);
                float endTime = Time.time + duration;
                while (state == State.Attack && Time.time < endTime) yield return null;
                if (attack != null) attack.AttackComplete();
                if (state != State.Attack) yield break;

                chainCount++;
                if (chainCount >= comboMaxLength) break;
                if (Random.value > comboChance) break;
                // Chain — re-evaluate range on next loop iteration
            }
            if (animator != null) animator.SetFloat(AttackSpeedHash, 1.0f);
            if (rootMotionForwarder != null) rootMotionForwarder.motionMultiplier = 1f;
            attackRoutine = null;
            if (state == State.Attack) EnterChase();
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
            // AttackRoutine drives the whole attack sequence; nothing per-frame needed here.
        }

        private void EnterReturning()
        {
            state = State.Returning;
            lastAttackType = null;
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
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            if (rootMotionForwarder != null) rootMotionForwarder.motionMultiplier = 1f;
            if (animator != null)
            {
                animator.SetFloat(AttackSpeedHash, 1.0f);
                animator.SetTrigger(HurtTriggerHash);
            }
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
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            if (rootMotionForwarder != null) rootMotionForwarder.motionMultiplier = 1f;
            agent.isStopped = true;
            agent.enabled = false;
            if (attack != null) attack.ForceCancel();
            if (animator != null)
            {
                animator.SetFloat(AttackSpeedHash, 1.0f);
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
