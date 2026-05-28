using UnityEngine;

public class ModuleSpawnPoint : MonoBehaviour
{
    [SerializeField] private Vector3 nextModuleOffset;
    [SerializeField] private Vector3 baseModulePosition;

    private void Awake()
    {
        ServiceLocator.Register<ModuleSpawnPoint>(this);
        if (baseModulePosition == Vector3.zero)
        {
            baseModulePosition = transform.position;
        }
    }

    public GameObject SpawnModule(GameObject modulePrefab, int index)
    {
        Vector3 position = baseModulePosition + nextModuleOffset * index;
        return Instantiate(modulePrefab, position, Quaternion.identity, transform);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<ModuleSpawnPoint>();
    }
}
