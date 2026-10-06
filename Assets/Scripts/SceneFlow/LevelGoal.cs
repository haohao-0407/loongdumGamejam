using System;
using UnityEngine;
using UnityEngine.Events;

namespace Loongdum.SceneFlow
{
    /// <summary>One scene-local goal: the player reunites with the vision source.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelGoal : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private VisionSource visionSource;
        [Tooltip("Maximum reunion distance in world units.")]
        [SerializeField, Min(0.01f)] private float reunionDistance = 1.1f;
        [Tooltip("Measure X/Z distance, allowing the two bodies to keep their individual heights.")]
        [SerializeField] private bool ignoreHeight = true;
        [Tooltip("Player control components to suspend during loading and after completion.")]
        [SerializeField] private Behaviour[] playerControls = Array.Empty<Behaviour>();
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        private bool[] initialControlStates;
        private bool prepared;

        public Transform Player => player;
        public VisionSource Source => visionSource;
        public float ReunionDistance => reunionDistance;
        public bool IsCompleted { get; private set; }
        public bool IsConfigured => player != null && visionSource != null &&
            player.gameObject.scene == gameObject.scene && visionSource.gameObject.scene == gameObject.scene;
        public event Action<LevelGoal> Completed;

        private void LateUpdate()
        {
            GameSceneManager manager = GameSceneManager.Instance;
            if (manager == null || manager.ActiveGoal != this || manager.State != SceneFlowState.Playing)
                return;
            CheckForCompletion();
        }

        internal void PrepareForRun()
        {
            // Also reset runtime fields when entering Play with domain/scene reload disabled.
            if (prepared) SetControlsEnabled(true);
            initialControlStates = new bool[playerControls.Length];
            for (int i = 0; i < playerControls.Length; i++)
                initialControlStates[i] = playerControls[i] != null && playerControls[i].enabled;
            prepared = true;
            IsCompleted = false;
        }

        public bool CheckForCompletion()
        {
            GameSceneManager manager = GameSceneManager.Instance;
            if (IsCompleted || !IsConfigured || manager == null || manager.ActiveGoal != this ||
                manager.State != SceneFlowState.Playing)
                return false;

            Vector3 offset = player.position - visionSource.transform.position;
            if (ignoreHeight) offset.y = 0f;
            if (offset.sqrMagnitude > reunionDistance * reunionDistance) return false;

            IsCompleted = true;
            SetControlsEnabled(false);
            Completed?.Invoke(this);
            onCompleted.Invoke();
            return true;
        }

        internal void SetControlsEnabled(bool allowed)
        {
            if (!prepared) return;
            for (int i = 0; i < playerControls.Length; i++)
            {
                Behaviour control = playerControls[i];
                if (control != null && control != this)
                    control.enabled = allowed && initialControlStates[i];
            }
        }

        private void OnDisable()
        {
            // Restore only the components that were enabled before this goal took ownership.
            if (prepared) SetControlsEnabled(true);
        }

#if UNITY_EDITOR
        public void Configure(Transform playerTransform, VisionSource source, Behaviour[] controls)
        {
            player = playerTransform;
            visionSource = source;
            playerControls = controls ?? Array.Empty<Behaviour>();
        }

        private void OnDrawGizmosSelected()
        {
            if (visionSource == null) return;
            Gizmos.color = new Color(0.74f, 0.87f, 0.55f, 0.8f);
            Vector3 center = visionSource.transform.position;
            if (ignoreHeight && player != null) center.y = player.position.y;
            Gizmos.DrawWireSphere(center, reunionDistance);
        }
#endif
    }
}
