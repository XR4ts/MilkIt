using System;
using UnityEngine;

namespace MilkIt.NPCs
{
    // Standalone preview only (NPC_CharacterPreview.unity). Not used by the customer system.
    public sealed class CharacterAnimationPreview : MonoBehaviour
    {
        [Serializable]
        public struct Character
        {
            public string label;
            public Animator animator;
            public GameObject cup;
        }

        const int ArmLayer = 1;
        static readonly int DrinkHash = Animator.StringToHash("Drink");

        public Character[] characters = Array.Empty<Character>();
        public string[] states = { "Idle", "Walk", "SitDown", "SitIdle", "Drink", "StandUp" };
        public float crossFade = 0.15f;
        public float turnSpeed = 45f;

        int target; // 0 = all, then one per character
        bool turning;
        Vector2 scroll;
        string[] targetLabels;

        void Start()
        {
            targetLabels = new string[characters.Length + 1];
            targetLabels[0] = "All";
            for (var i = 0; i < characters.Length; i++) targetLabels[i + 1] = characters[i].label;
            Play("Idle");
        }

        void Update()
        {
            foreach (var c in characters)
            {
                if (c.animator == null || c.cup == null || c.animator.layerCount <= ArmLayer) continue;
                var drinking = c.animator.GetCurrentAnimatorStateInfo(ArmLayer).shortNameHash == DrinkHash ||
                               c.animator.GetNextAnimatorStateInfo(ArmLayer).shortNameHash == DrinkHash;
                if (c.cup.activeSelf != drinking) c.cup.SetActive(drinking);
            }
            if (!turning) return;
            foreach (var c in characters)
                if (c.animator != null) c.animator.transform.parent.Rotate(0f, turnSpeed * Time.deltaTime, 0f);
        }

        public void Play(string state)
        {
            for (var i = 0; i < characters.Length; i++)
            {
                if (target != 0 && target != i + 1) continue;
                var animator = characters[i].animator;
                if (animator == null) continue;
                var hash = Animator.StringToHash(state);
                // Drink plays on the right-arm overlay layer on top of whatever the body is doing.
                var layer = hash == DrinkHash && animator.layerCount > ArmLayer ? ArmLayer : 0;
                if (!animator.HasState(layer, hash))
                {
                    Debug.LogWarning($"{characters[i].label}: no animation state '{state}'.", animator);
                    continue;
                }
                animator.CrossFadeInFixedTime(hash, layer == ArmLayer ? 0.3f : crossFade, layer, 0f);
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("Character animation preview");
            if (targetLabels != null) target = GUILayout.SelectionGrid(target, targetLabels, 3);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var state in states)
                if (GUILayout.Button(state)) Play(state);
            GUILayout.EndScrollView();
            turning = GUILayout.Toggle(turning, "Turntable");
            if (GUILayout.Button("Face camera"))
                foreach (var c in characters)
                    if (c.animator != null) c.animator.transform.parent.localRotation = Quaternion.identity;
            foreach (var c in characters)
            {
                if (c.animator == null) continue;
                var info = c.animator.GetCurrentAnimatorStateInfo(0);
                var current = Array.Find(states, s => info.IsName(s)) ?? "?";
                if (c.animator.layerCount > ArmLayer &&
                    c.animator.GetCurrentAnimatorStateInfo(ArmLayer).shortNameHash == DrinkHash) current += " + Drink";
                GUILayout.Label($"{c.label}: {current}");
            }
            GUILayout.EndArea();
        }
    }
}
