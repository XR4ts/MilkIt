using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs
{
    public enum CustomerState { Entering, Queueing, Ordering, WalkingToSeat, WaitingForDrink, Drinking, WaitingForParty, Leaving }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CustomerController : MonoBehaviour
    {
        public Transform body;
        public CustomerState State { get; private set; }
        public int OrderId { get; private set; }
        public string Drink { get; private set; }
        public CustomerSeat Seat => seat;
        public CustomerParty Party { get; private set; }
        public bool DoneDrinking { get; private set; }
        public CustomerAnimator Character { get; private set; }
        // Where the debug label is drawn.
        public Vector3 LabelPosition => Character != null ? transform.position + Vector3.up * 2.1f
            : body != null ? body.position + Vector3.up * 1.2f : transform.position;

        CafeCustomerSystem cafe;
        CustomerSeat seat;
        NavMeshAgent agent;
        Vector3 standingScale;
        bool finished;
        bool moving;
        float moveStarted;
        int moveFrame;
        float startDelay;
        Vector3 destination;

        const float SeatWalkSpeed = 0.7f;
        const float SipInterval = 3.2f;

        // Replaces the placeholder capsule with a character visual (a generated Humanoid prefab).
        public void AttachCharacter(GameObject characterPrefab, float walkSpeed)
        {
            var visual = Instantiate(characterPrefab, transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            var animator = visual.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning($"{characterPrefab.name} has no Humanoid Animator; keeping the placeholder.", this);
                Destroy(visual);
                return;
            }
            if (body != null) body.gameObject.SetActive(false);
            agent = GetComponent<NavMeshAgent>();
            agent.speed = walkSpeed;
            Character = gameObject.AddComponent<CustomerAnimator>();
            Character.Init(animator, agent);
        }

        public void Begin(CafeCustomerSystem owner, CustomerSeat reservedSeat, int orderId, string drink,
            CustomerParty party = null, float delay = 0f)
        {
            cafe = owner;
            seat = reservedSeat;
            OrderId = orderId;
            Drink = drink;
            Party = party;
            startDelay = delay;
            agent = GetComponent<NavMeshAgent>();
            if (body != null) standingScale = body.localScale;
            gameObject.name = $"Customer {orderId} ({drink})";
            State = CustomerState.Entering;
            StartCoroutine(Visit());
        }

        IEnumerator Visit()
        {
            // Party members set off slightly apart so they do not spawn into each other.
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
            if (cafe.entryWaypoint != null)
            {
                if (!MoveTo(cafe.entryWaypoint.position)) yield break;
                while (!Arrived())
                {
                    if (MoveFailed()) yield break;
                    yield return null;
                }
            }
            State = CustomerState.Queueing;
            while (!finished)
            {
                var target = cafe.QueueTarget(this);
                if (target == null) { Abort("lost its queue position"); yield break; }
                if (!moving || Vector3.Distance(destination, target.position) > 0.05f)
                    if (!MoveTo(target.position)) yield break;
                if (MoveFailed()) yield break;
                if (cafe.IsFirst(this) && Arrived()) break;
                yield return null;
            }
            agent.isStopped = true;
            transform.rotation = cafe.QueueTarget(this).rotation;
            State = CustomerState.Ordering;
            if (Character != null) Character.Talking = true;
            yield return new WaitForSeconds(cafe.orderSeconds);
            if (Character != null) Character.Talking = false;
            State = CustomerState.WalkingToSeat;
            cafe.PlaceOrder(this);
            if (cafe.tillExit != null)
            {
                if (!MoveTo(cafe.tillExit.position)) yield break;
                while (!Arrived())
                {
                    if (MoveFailed()) yield break;
                    yield return null;
                }
            }
            if (!MoveTo(seat.approach.position)) yield break;
            while (!Arrived())
            {
                if (MoveFailed()) yield break;
                yield return null;
            }

            agent.enabled = false;
            if (Character != null) yield return SitDown();
            else
            {
                // Placeholder seating: only move the visible body. The root stays at the walkable approach.
                body.SetPositionAndRotation(seat.sittingPose.position, seat.sittingPose.rotation);
                body.localScale = new Vector3(standingScale.x, standingScale.y * 0.65f, standingScale.z);
            }

            State = CustomerState.WaitingForDrink;
            while (State == CustomerState.WaitingForDrink)
            {
                if (Character != null) SeatedPose();
                yield return null;
            }

            if (Character != null)
            {
                Character.ShowCup(true);
                for (var t = 0f; t < cafe.drinkingSeconds; t += SipInterval)
                {
                    Character.Sip();
                    yield return new WaitForSeconds(Mathf.Min(SipInterval, cafe.drinkingSeconds - t));
                }
                Character.ShowCup(false);
            }
            else yield return new WaitForSeconds(cafe.drinkingSeconds);
            DoneDrinking = true;

            // Friends leave together.
            State = CustomerState.WaitingForParty;
            while (Party != null && !Party.AllDoneDrinking)
            {
                if (Character != null) SeatedPose();
                yield return null;
            }

            if (Character != null) yield return StandUp();
            else
            {
                body.localPosition = Vector3.up;
                body.localRotation = Quaternion.identity;
                body.localScale = standingScale;
            }
            seat.Release(this);
            agent.enabled = true;
            agent.Warp(seat.approach.position);
            State = CustomerState.Leaving;
            if (!MoveTo(cafe.exit.position)) yield break;
            // Friends leave side by side, so anywhere close to the exit counts.
            while (!Arrived(1f))
            {
                if (MoveFailed()) yield break;
                yield return null;
            }
            finished = true;
            cafe.Remove(this, true);
            Destroy(gameObject);
        }

        // Seated customers chat when someone else from their party is at the table.
        void SeatedPose()
        {
            var talk = Party != null && Party.SeatedCount > 1;
            var wanted = talk ? CustomerAnimator.SitTalk : CustomerAnimator.SitIdle;
            var info = Character.Animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != wanted && !Character.Animator.IsInTransition(0) &&
                (info.shortNameHash == CustomerAnimator.SitIdle || info.shortNameHash == CustomerAnimator.SitTalk))
                Character.Play(wanted, 0.4f);
        }

        public bool IsSeated => State == CustomerState.WaitingForDrink || State == CustomerState.Drinking ||
                                State == CustomerState.WaitingForParty;

        Vector3 SeatPoint => new Vector3(seat.sittingPose.position.x, seat.approach.position.y, seat.sittingPose.position.z);

        IEnumerator SitDown()
        {
            // Step from the aisle into the booth, turn to the table, then sit.
            yield return WalkKinematic(SeatPoint);
            yield return Turn(seat.sittingPose.rotation);
            yield return Character.PlayAndWait(CustomerAnimator.SitDown);
        }

        IEnumerator StandUp()
        {
            yield return Character.PlayAndWait(CustomerAnimator.StandUp);
            Character.Locomotion = true;
            yield return WalkKinematic(seat.approach.position);
        }

        IEnumerator WalkKinematic(Vector3 target)
        {
            Character.Locomotion = true;
            var flat = target - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f) yield return Turn(Quaternion.LookRotation(flat));
            Character.KinematicSpeed = SeatWalkSpeed;
            while ((transform.position - target).sqrMagnitude > 0.0004f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, SeatWalkSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = target;
            Character.KinematicSpeed = 0f;
        }

        IEnumerator Turn(Quaternion target)
        {
            while (Quaternion.Angle(transform.rotation, target) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 360f * Time.deltaTime);
                yield return null;
            }
            transform.rotation = target;
        }

        bool MoveTo(Vector3 target)
        {
            if (!agent.enabled || !agent.isOnNavMesh || !agent.SetDestination(target))
            {
                Abort("could not start a navigation path");
                return false;
            }
            agent.isStopped = false;
            destination = target;
            moveStarted = Time.time;
            moveFrame = Time.frameCount;
            moving = true;
            return true;
        }

        void Update()
        {
            if (finished || cafe == null || cafe.entranceDoor == null || agent == null ||
                !agent.enabled || !agent.isOnNavMesh || State == CustomerState.Ordering) return;
            agent.isStopped = cafe.entranceDoor.ShouldWait(transform.position);
        }

        bool Arrived(float tolerance = 0.15f) => Time.frameCount > moveFrame && !agent.pathPending &&
            agent.pathStatus == NavMeshPathStatus.PathComplete &&
            Vector3.Distance(transform.position, destination) <= agent.stoppingDistance + tolerance;

        bool MoveFailed()
        {
            if (Time.frameCount <= moveFrame || agent.pathPending) return TimedOut();
            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Abort("has an unreachable destination");
                return true;
            }
            return TimedOut();
        }

        bool TimedOut()
        {
            // A customer waiting at its queue marker is allowed to wait indefinitely.
            if (Time.time - moveStarted < 45f || Arrived(State == CustomerState.Leaving ? 1f : 0.15f)) return false;
            Abort("was blocked for 45 seconds");
            return true;
        }

        public bool TryReceiveDrink(string drink)
        {
            if (State != CustomerState.WaitingForDrink || drink != Drink) return false;
            State = CustomerState.Drinking;
            return true;
        }

        void Abort(string reason)
        {
            Debug.LogWarning($"{name} {reason}; releasing its queue position and seat.", this);
            finished = true;
            if (seat != null) seat.Release(this);
            if (cafe != null) cafe.Remove(this, false);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (seat != null) seat.Release(this);
            if (cafe != null) cafe.Remove(this, false);
            Party?.Members.Remove(this);
        }
    }
}
