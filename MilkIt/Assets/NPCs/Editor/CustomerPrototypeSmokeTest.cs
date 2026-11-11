using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilkIt.NPCs.Editor
{
    // Explicitly invoked, isolated batch-mode integration test. Never runs on normal asset import.
    [InitializeOnLoad]
    public static class CustomerPrototypeSmokeTest
    {
        const string Pending = "MilkIt.NPC.SmokeTest";
        static CafeCustomerSystem system;
        static double started;
        static int stage;
        static int placed;
        static bool invalidDrinkChecked;
        static readonly HashSet<int> orderIds = new HashSet<int>();

        static CustomerPrototypeSmokeTest()
        {
            if (Application.isBatchMode && SessionState.GetBool(Pending, false))
                EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run NPC validation only in an isolated batch-mode Unity project.");
            CustomerPrototypeBuilder.Build();
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (system == null)
                {
                    system = UnityEngine.Object.FindFirstObjectByType<CafeCustomerSystem>();
                    if (system == null) throw new Exception("Customer system missing.");
                    started = EditorApplication.timeSinceStartup;
                    system.orderSeconds = 0.1f;
                    system.drinkingSeconds = 0.1f;
                    system.OrderPlaced += (id, drink) =>
                    {
                        if (!orderIds.Add(id)) throw new Exception("Duplicate order event.");
                        placed++;
                    };
                    for (int i = 0; i < 3; i++)
                        Require(system.TrySpawnCustomer(), "Could not spawn three customers.");
                    Require(!system.TrySpawnCustomer(), "Capacity limit failed.");
                    Require(!system.TryDeliverDrink(-1, "Latte"), "Unknown order accepted.");
                    stage = 1;
                }
                Require(EditorApplication.timeSinceStartup - started < 100, "Timed out: customer loop stalled.");
                var occupied = new HashSet<CustomerSeat>();
                foreach (var customer in system.Customers)
                {
                    if (customer.Seat.Occupant == customer)
                        Require(occupied.Add(customer.Seat), "Customers share a reserved seat.");
                    if (customer.State != CustomerState.WaitingForDrink) continue;
                    Require(!system.TryDeliverDrink(customer.OrderId, "Wrong drink"), "Wrong drink accepted.");
                    Require(system.TryDeliverDrink(customer.OrderId, customer.Drink), "Matching drink rejected.");
                    Require(!system.TryDeliverDrink(customer.OrderId, customer.Drink), "Duplicate delivery accepted.");
                    invalidDrinkChecked = true;
                }
                if (stage == 1 && system.CompletedVisits == 3)
                {
                    Require(system.Customers.Count == 0 && placed == 3 && invalidDrinkChecked, "First cycle incomplete.");
                    foreach (var seat in system.seats) Require(seat.Occupant == null, "Seat not released.");
                    Require(system.TrySpawnCustomer(), "Cannot reuse a released seat.");
                    UnityEngine.Object.DestroyImmediate(system.Customers[0].gameObject);
                    Require(system.Customers.Count == 0, "Deleted customer remained registered.");
                    Require(system.TrySpawnCustomer(), "Deleted customer leaked its seat or queue slot.");
                    stage = 2;
                }
                if (stage == 2 && system.CompletedVisits == 4)
                {
                    Require(placed == 4, "Repeated visit did not place exactly one order.");
                    Finish(0, "NPC_SMOKE_PASS: four visits, queue capacity, unique seats, wrong/duplicate delivery, cancellation and seat reuse.");
                }
            }
            catch (Exception error) { Finish(1, "NPC_SMOKE_FAIL: " + error); }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        static void Finish(int code, string message)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            if (code == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }
    }
}
