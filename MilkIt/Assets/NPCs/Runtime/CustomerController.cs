using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilkIt.NPCs
{
    public enum CustomerState { Entering, Queueing, Ordering, WalkingToSeat, WaitingForDrink, Drinking, WaitingForParty, Leaving, LeavingAngry, WaitingAtTill }

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
        // Patience while waiting for the drink: 1 = just sat down, 0 = gives up and leaves without paying.
        public float Patience01 { get; private set; } = 1f;
        public float PatienceSecondsLeft { get; private set; }
        public bool Frustrated { get; private set; }
        public bool GaveUp { get; private set; }
        public int AmountPaid { get; private set; }
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
        CustomerPatienceIndicator indicator;
        CustomerOrderBubble bubble;
        CustomerHandOffZone handOff;

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

        void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (body == null || !r.transform.IsChildOf(body) || Character == null) r.enabled = visible;
        }

        public void AttachIndicator(CustomerPatienceIndicator prefab)
        {
            if (prefab == null) return;
            indicator = Instantiate(prefab, transform.parent);
            var head = Character != null ? Character.Animator.GetBoneTransform(HumanBodyBones.Head) : null;
            indicator.Attach(head != null ? head : body != null ? body : transform);
            if (head == null) indicator.heightAboveHead = 1.3f;
        }

        public void AttachOrderBubble(CustomerOrderBubble prefab)
        {
            if (prefab == null) return;
            bubble = Instantiate(prefab, transform.parent);
            var head = Character != null ? Character.Animator.GetBoneTransform(HumanBodyBones.Head) : null;
            bubble.Attach(head != null ? head : body != null ? body : transform);
            if (head == null) { bubble.heightAtTill = 1.4f; bubble.heightSeated = 1.9f; }
        }

        // A solid collider so the player can select this customer with a controller ray, poke or mouse click.
        public void MakeSelectable()
        {
            var collider = gameObject.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.9f;
            collider.height = 1.8f;
            collider.radius = 0.3f;
            var interactable = gameObject.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => { if (cafe != null) cafe.TakeOrder(this); });
        }

        // Seated and waiting: an invisible zone over the customer and their half of the table, so the player can
        // hand them a drink (see CafeCustomerSystem.DrinkHandedOver).
        void SetHandOffZone(bool active)
        {
            if (handOff == null)
            {
                if (!active) return;
                var zone = new GameObject("Hand-off zone");
                zone.transform.SetParent(transform, false);
                zone.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                var box = zone.AddComponent<BoxCollider>();
                box.size = new Vector3(0.6f, 1.1f, 0.45f); // stops at the table's middle, so facing seats don't overlap
                handOff = zone.AddComponent<CustomerHandOffZone>();
                handOff.Init(this, cafe);
            }
            handOff.Enable(active);
        }

        // The player took this customer's order at the till.
        public bool TryTakeOrder()
        {
            if (State != CustomerState.WaitingAtTill) return false;
            State = CustomerState.Ordering;
            return true;
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
            // Customers wait their turn at the entrance hidden and without an agent, so they never overlap.
            if (startDelay > 0.01f)
            {
                var spawn = transform.position;
                agent.enabled = false;
                SetVisible(false);
                yield return new WaitForSeconds(startDelay);
                transform.position = spawn;
                agent.enabled = true;
                SetVisible(true);
            }
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
            if (cafe.requireOrderClick)
            {
                // Wait at the register until the player clicks them.
                State = CustomerState.WaitingAtTill;
                if (bubble != null) bubble.ShowWaitingToOrder();
                while (State == CustomerState.WaitingAtTill) yield return null;
            }
            State = CustomerState.Ordering;
            if (bubble != null) bubble.ShowOrder(Drink, cafe.IconOf(Drink), false);
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
            SetHandOffZone(true);
            // Seated: the drink picture moves next to the patience bar; the bubble is only for the till.
            var icon = cafe.IconOf(Drink);
            if (indicator != null)
            {
                indicator.SetDrinkIcon(icon, Drink);
                if (bubble != null) bubble.Hide();
            }
            else if (bubble != null) bubble.ShowOrder(Drink, icon, true);
            var patience = cafe.patienceSeconds;
            var waitStarted = Time.time;
            while (State == CustomerState.WaitingForDrink)
            {
                if (patience > 0f)
                {
                    PatienceSecondsLeft = Mathf.Max(0f, patience - (Time.time - waitStarted));
                    Patience01 = PatienceSecondsLeft / patience;
                    Frustrated = Patience01 <= cafe.frustratedAt;
                    if (PatienceSecondsLeft <= 0f) { State = CustomerState.LeavingAngry; break; }
                }
                if (indicator != null) indicator.ShowWaiting(Patience01, patience > 0f ? PatienceSecondsLeft : -1f, Frustrated);
                if (Character != null) SeatedPose();
                yield return null;
            }
            Frustrated = false;
            SetHandOffZone(false);
            if (indicator != null) indicator.Hide();
            if (bubble != null) bubble.Hide();

            if (State == CustomerState.LeavingAngry)
            {
                // Waited too long: leave alone, without paying. Friends carry on.
                GaveUp = true;
                DoneDrinking = true;
                cafe.GiveUp(this);
                if (indicator != null) indicator.ShowGaveUp();
                yield return LeaveCafe(false);
                yield break;
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
            // Pay only once the drink is finished.
            AmountPaid = cafe.Pay(this);
            if (indicator != null) indicator.ShowPaid(AmountPaid);
            DoneDrinking = true;

            // Friends leave together.
            State = CustomerState.WaitingForParty;
            while (Party != null && !Party.AllDoneDrinking)
            {
                if (Character != null) SeatedPose();
                yield return null;
            }

            State = CustomerState.Leaving;
            yield return LeaveCafe(true);
        }

        IEnumerator LeaveCafe(bool completed)
        {
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
            if (!MoveTo(cafe.exit.position)) yield break;
            // Friends leave side by side, so anywhere close to the exit counts.
            while (!Arrived(1f))
            {
                if (MoveFailed()) yield break;
                yield return null;
            }
            finished = true;
            cafe.Remove(this, completed);
            Destroy(gameObject);
        }

        // Seated customers chat when someone else from their party is at the table.
        void SeatedPose()
        {
            // Frustrated customers stop chatting.
            var talk = Party != null && Party.SeatedCount > 1 && !Frustrated;
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
                !agent.enabled || !agent.isOnNavMesh || State == CustomerState.Ordering ||
                State == CustomerState.WaitingAtTill) return;
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
            if (Time.time - moveStarted < 45f || Arrived(State == CustomerState.Leaving || State == CustomerState.LeavingAngry ? 1f : 0.15f)) return false;
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
            if (indicator != null) Destroy(indicator.gameObject);
            if (bubble != null) Destroy(bubble.gameObject);
            if (seat != null) seat.Release(this);
            if (cafe != null) cafe.Remove(this, false);
            Party?.Members.Remove(this);
        }
    }
}
