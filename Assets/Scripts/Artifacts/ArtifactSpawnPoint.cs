using UnityEngine;

public class ArtifactSpawnPoint : MonoBehaviour
{
    [SerializeField] private Vector3 nextArtifactOffset;
    [SerializeField] private Vector3 baseArtifactPosition;

    private void Awake()
    {
        ServiceLocator.Register<ArtifactSpawnPoint>(this);
        if (baseArtifactPosition == Vector3.zero)
        {
            baseArtifactPosition = transform.position;
        }
    }

    public GameObject SpawnArtifact(GameObject artifactPrefab, int index)
    {
        Vector3 position = baseArtifactPosition + nextArtifactOffset * index;
        return Instantiate(artifactPrefab, position, Quaternion.identity, transform);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<ArtifactSpawnPoint>();
    }
}
