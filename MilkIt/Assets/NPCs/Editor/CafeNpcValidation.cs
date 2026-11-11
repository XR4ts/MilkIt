using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace MilkIt.NPCs.Editor
{
    [InitializeOnLoad]
    public static class CafeNpcValidation
    {
        const string Pending = "MilkIt.CafeNpcValidation";
        static CafeCustomerSystem cafe;
        static double started, completedAt;
        static int orders, crossings;
        static float originalOrderTime, originalDrinkTime;
        static bool originalAutoServe;
        static readonly Dictionary<int, float> previousSides = new Dictionary<int, float>();

        static CafeNpcValidation()
        {
            if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
        }

        [MenuItem("MilkIt/NPCs/Validate cafe customer route")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            CafeNpcSetup.Open();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != CafeNpcSetup.ScenePath) return;
            SessionState.SetBool(Pending, true);
            cafe = null;
            orders = crossings = 0;
            started = EditorApplication.timeSinceStartup;
            completedAt = 0;
            previousSides.Clear();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (cafe == null)
                {
                    started = EditorApplication.timeSinceStartup;
                    cafe = UnityEngine.Object.FindFirstObjectByType<CafeCustomerSystem>();
                    Require(cafe != null && cafe.entranceDoor != null, "Cafe components missing.");
                    var targets = new List<Transform> { cafe.entryWaypoint, cafe.exit };
                    targets.AddRange(cafe.queuePositions);
                    foreach (var seat in cafe.seats) targets.Add(seat.approach);
                    foreach (var target in targets)
                    {
                        var path = new NavMeshPath();
                        Require(NavMesh.CalculatePath(cafe.entrance.position, target.position, NavMesh.AllAreas, path) &&
                            path.status == NavMeshPathStatus.PathComplete, "Unreachable marker: " + target.name);
                    }
                    originalOrderTime = cafe.orderSeconds;
                    originalDrinkTime = cafe.drinkingSeconds;
                    originalAutoServe = cafe.autoServe;
                    cafe.orderSeconds = cafe.drinkingSeconds = 1;
                    cafe.autoServe = true;
                    cafe.OrderPlaced += OnOrder;
                    for (int i = 0; i < 3; i++) Require(cafe.TrySpawnCustomer(), "Spawn failed.");
                }
                Require(EditorApplication.timeSinceStartup - started < 100, "Route timed out.");
                var seats = new HashSet<CustomerSeat>();
                foreach (var customer in cafe.Customers)
                {
                    if (customer.Seat.Occupant == customer)
                        Require(seats.Add(customer.Seat), "Seat reserved twice.");
                    float side = customer.transform.position.z - cafe.entranceDoor.transform.position.z;
                    if (previousSides.TryGetValue(customer.OrderId, out var previous) && side * previous < 0)
                    {
                        Require(cafe.entranceDoor.IsOpen, "Customer crossed a closed door.");
                        crossings++;
                    }
                    previousSides[customer.OrderId] = side;
                }
                if (cafe.CompletedVisits != 3) return;
                if (completedAt == 0) completedAt = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - completedAt < 3) return;
                Require(orders == 3 && crossings == 6, $"Expected 3 orders / 6 doorway crossings; got {orders} / {crossings}.");
                Require(cafe.Customers.Count == 0 && cafe.entranceDoor.OpenAmount < 0.01f, "Customers remain or doors did not close.");
                foreach (var seat in cafe.seats) Require(seat.Occupant == null, "Seat was not released.");
                Finish("PASS: all route markers reachable; 3 complete visits; 6 open-door crossings; doors close; seats released.");
            }
            catch (Exception error) { Finish("FAIL: " + error.Message); }
        }

        static void OnOrder(int id, string drink) => orders++;
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        static void Finish(string result)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            if (cafe != null)
            {
                cafe.OrderPlaced -= OnOrder;
                cafe.orderSeconds = originalOrderTime;
                cafe.drinkingSeconds = originalDrinkTime;
                cafe.autoServe = originalAutoServe;
            }
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "milkit-cafe-validation.txt"), result);
            if (result.StartsWith("PASS")) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.isPlaying = false;
        }
    }
}
