using UnityEngine;

public class OnDropAbduction : MonoBehaviour
{
    [SerializeField] private DropTarget dropTarget;
    [SerializeField] private Abductor abductor;
    public bool gettingAbducted = false;

    void Start()
    {
        dropTarget.OnFullyDown += StartAbduction;
    }

    private void Update()
    {
        if (gettingAbducted)
        {
            dropTarget.resetTimer += Time.deltaTime;
        }
    }

    void StartAbduction()
    {
        abductor.StartAbduction();
        gettingAbducted = true;
    }
}
