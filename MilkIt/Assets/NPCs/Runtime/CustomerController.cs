using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs
{
    public enum CustomerState { Entering, Queueing, Ordering, WalkingToSeat, WaitingForDrink, Drinking, Leaving }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CustomerController : MonoBehaviour
    {
        public Transform body;
        public CustomerState State { get; private set; }
        public int OrderId { get; private set; }
        public string Drink { get; private set; }
        public CustomerSeat Seat => seat;
        CafeCustomerSystem cafe;
        CustomerSeat seat;
        NavMeshAgent agent;
        Vector3 standingScale;
        bool finished;
        bool moving;
        float moveStarted;
        int moveFrame;
        Vector3 destination;

        public void Begin(CafeCustomerSystem owner, CustomerSeat reservedSeat, int orderId, string drink)
        {
            cafe = owner;
            seat = reservedSeat;
            OrderId = orderId;
            Drink = drink;
            agent = GetComponent<NavMeshAgent>();
            standingScale = body.localScale;
            gameObject.name = $"Customer {orderId} ({drink})";
            State = CustomerState.Entering;
            StartCoroutine(Visit());
        }

        IEnumerator Visit()
        {
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
            yield return new WaitForSeconds(cafe.orderSeconds);
            State = CustomerState.WalkingToSeat;
            cafe.PlaceOrder(this);
            if (!MoveTo(seat.approach.position)) yield break;
            while (!Arrived())
            {
                if (MoveFailed()) yield break;
                yield return null;
            }

            agent.enabled = false;
            // Placeholder seating: only move the visible body. The root stays at the walkable approach.
            body.SetPositionAndRotation(seat.sittingPose.position, seat.sittingPose.rotation);
            body.localScale = new Vector3(standingScale.x, standingScale.y * 0.65f, standingScale.z);
            State = CustomerState.WaitingForDrink;
            while (State == CustomerState.WaitingForDrink) yield return null;
            yield return new WaitForSeconds(cafe.drinkingSeconds);

            body.localPosition = Vector3.up;
            body.localRotation = Quaternion.identity;
            body.localScale = standingScale;
            seat.Release(this);
            agent.enabled = true;
            State = CustomerState.Leaving;
            if (!MoveTo(cafe.exit.position)) yield break;
            while (!Arrived())
            {
                if (MoveFailed()) yield break;
                yield return null;
            }
            finished = true;
            cafe.Remove(this, true);
            Destroy(gameObject);
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

        bool Arrived() => Time.frameCount > moveFrame && !agent.pathPending &&
            agent.pathStatus == NavMeshPathStatus.PathComplete &&
            Vector3.Distance(transform.position, destination) <= agent.stoppingDistance + 0.15f;

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
            if (Time.time - moveStarted < 45f || Arrived()) return false;
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
        }
    }
}
