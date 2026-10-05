using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerCollisionSmoke : MonoBehaviour
{
    [SerializeField] private GameObject smokePrefab;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.05f;
    [Tooltip("This layer is rendered after the vision mask by PC_Renderer's Global Visible Smoke pass.")]
    [SerializeField] private string visibleLayer = "TransparentFX";

    private HashSet<Collider> previousContacts = new HashSet<Collider>();
    private HashSet<Collider> currentContacts = new HashSet<Collider>();

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isActiveAndEnabled || smokePrefab == null)
        {
            return;
        }

        // One puff when contact begins; holding against a wall does not emit every frame.
        if (currentContacts.Add(hit.collider) && !previousContacts.Contains(hit.collider))
        {
            SpawnSmoke(hit.point, hit.normal);
        }
    }

    private void LateUpdate()
    {
        HashSet<Collider> reusable = previousContacts;
        previousContacts = currentContacts;
        currentContacts = reusable;
        currentContacts.Clear();
    }

    private void OnDisable()
    {
        previousContacts.Clear();
        currentContacts.Clear();
    }

    private void SpawnSmoke(Vector3 point, Vector3 normal)
    {
        GameObject smoke = Instantiate(smokePrefab, point + normal * surfaceOffset,
            smokePrefab.transform.rotation);

        int layer = LayerMask.NameToLayer(visibleLayer);
        if (layer >= 0)
        {
            foreach (Transform child in smoke.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        ParticleSystem particles = smoke.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            Destroy(smoke);
            return;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (ParticleSystem system in smoke.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        }

        ParticleSystem.MainModule rootMain = particles.main;
        rootMain.stopAction = ParticleSystemStopAction.Destroy;
        particles.Play(true);
    }
}
