using UnityEngine;

public class OnDropMover : MonoBehaviour
{
    [SerializeField] private DropTarget dropTarget;
    [SerializeField] private float posSpeed;
    [SerializeField] private float rotSpeed;
    [SerializeField] private Vector3 originalPos;
    [SerializeField] private Quaternion originalRot;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 targetPos;
    [SerializeField] private Quaternion targetRot;
    [SerializeField] private GameObject objectToEnable;
    [SerializeField] private bool goingUp = false;
    [SerializeField] private bool goingDown = false;

    void Start()
    {
        dropTarget.onStartDown += OnDrop;
        dropTarget.onStartUp += OnUp;
        originalPos = transform.position;
        originalRot = transform.rotation;
    }

    private void Update()
    {
        if (goingUp)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetTransform.position, Time.deltaTime * posSpeed);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetTransform.rotation, Time.deltaTime * rotSpeed);
            if (transform.position == targetTransform.position && transform.rotation == targetTransform.rotation)
            {
                goingUp = false;
            }
        }
        else if (goingDown)
        {
            transform.position = Vector3.MoveTowards(transform.position, originalPos, Time.deltaTime * posSpeed);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, originalRot, Time.deltaTime * rotSpeed);
            if (transform.position == originalPos && transform.rotation == originalRot)
            {
                goingDown = false;
            }
        }

    }

    void OnDrop()
    {
        goingUp = true;
        goingDown = false;
        objectToEnable.SetActive(true);
    }

    void OnUp()
    {
        goingUp = false;
        goingDown = true;
        objectToEnable.SetActive(false);
    }
}
