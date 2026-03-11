using UnityEngine;
using System.Collections.Generic;

public class Bomb : MonoBehaviour
{
    [SerializeField] private List<BoardComponent> componentsToHit = new List<BoardComponent>();

    public void Explode()
    {
        foreach(BoardComponent boardComponent in componentsToHit)
        {
            boardComponent.AddScore();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        BoardComponent boardComponent = other.GetComponent<BoardComponent>();
        if (boardComponent && !componentsToHit.Contains(boardComponent))
        {
            componentsToHit.Add(boardComponent);
        }
    }
}
