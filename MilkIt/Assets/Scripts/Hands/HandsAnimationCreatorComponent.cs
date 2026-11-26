using UnityEngine;
using UnityEngine.InputSystem;

public class HandsAnimationCreatorComponent : MonoBehaviour
    {
        public AnimationClip targetAnimationClip = null!;

        public Transform handsRoot = null!;

        public bool bakeLocalPosition = true;
        public bool bakeLocalRotation = true;

        public Key bakeKey = Key.Space;

        public void Update()
        {
            if (Keyboard.current[bakeKey].wasPressedThisFrame)
            {
                BakePose();
            }
        }

        [ContextMenu(nameof(BakePose))]
        void BakePose()
        {
#if !UNITY_EDITOR
            Debug.LogWarning("Pose baking works only in UnityEditor");
            return;
#endif

            if (targetAnimationClip == null)
            {
                Debug.LogError($"{nameof(targetAnimationClip)} is not set.");
                return;
            }

            if (handsRoot == null)
            {
                Debug.LogError($"{nameof(handsRoot)} is not set.");
                return;
            }

            targetAnimationClip.ClearCurves();

            AddCurvesRecursive(targetAnimationClip, handsRoot, string.Empty, bakeLocalRotation, bakeLocalPosition);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(targetAnimationClip); // Marks the clip as "dirty" (modified)
            UnityEditor.AssetDatabase.SaveAssets(); // Saves all modified assets (including the clip)
#endif

            Debug.Log($"Baked Pose:  {targetAnimationClip.name}");
        }

        private void AddCurvesRecursive(AnimationClip clip, Transform currentTransform, string parentRelativePath,
            bool addLocalRotation, bool addLocalPosition)
        {
            var currentRelativePath = parentRelativePath + currentTransform.name;

            if (addLocalRotation)
            {
                SetLocalRotationCurveToClip(clip, currentRelativePath, currentTransform.localRotation);
            }

            if (addLocalPosition)
            {
                SetLocalPositionCurveToClip(clip, currentRelativePath, currentTransform.localPosition);
            }

            for (int i = 0, count = currentTransform.childCount; i < count; i++)
            {
                var child = currentTransform.GetChild(i);
                AddCurvesRecursive(clip, child, currentRelativePath + "/", addLocalRotation, addLocalPosition);
            }
        }


        private void SetLocalRotationCurveToClip(AnimationClip clip, string relativeTransformPath,
            Quaternion localRotation)
        {
            var animationCurveX = CreateCurveFromValue(localRotation.x);
            clip.SetCurve(relativeTransformPath, typeof(Transform), "localRotation.x", animationCurveX);

            var animationCurveY = CreateCurveFromValue(localRotation.y);
            clip.SetCurve(relativeTransformPath, typeof(Transform), "localRotation.y", animationCurveY);

            var animationCurveZ = CreateCurveFromValue(localRotation.z);
            clip.SetCurve(relativeTransformPath, typeof(Transform), "localRotation.z", animationCurveZ);

            var animationCurveW = CreateCurveFromValue(localRotation.w);
            clip.SetCurve(relativeTransformPath, typeof(Transform), "localRotation.w", animationCurveW);
        }

        private void SetLocalPositionCurveToClip(AnimationClip clip, string relativeTransformPath,
            Vector3 localPosition)
        {
            var animationCurveX = CreateCurveFromValue(localPosition.x);
            clip.SetCurve(relativeTransformPath, typeof(Transform), "m_localPosition.x", animationCurveX);

            var animationCurveY = CreateCurveFromValue(localPosition.y);
            clip.SetCurve(relativeTransformPath, typeof(Transform), "m_localPosition.y", animationCurveY);

            var animationCurveZ = CreateCurveFromValue(localPosition.z);
            clip.SetCurve(relativeTransformPath, typeof(Transform), "m_localPosition.z", animationCurveZ);
        }

        private AnimationCurve CreateCurveFromValue(float value)
        {
            var keyframe = new Keyframe(0, value);
            var keyframes = new Keyframe[]
            {
                keyframe,
            };
            return new AnimationCurve(keyframes);
        }
    }