using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class IgnoreCollision : MonoBehaviour
{
    Collider[] collidersToIgnore;
    void Start()
    {
        var colliders = Physics.OverlapBox(transform.position, transform.localScale / 2);
        var children = GetComponentsInChildren<Collider>();
        var collidersToIgnore = colliders.Except(children).ToArray();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // Ensure the player has the tag "Player"
        {
            foreach (Collider col in collidersToIgnore)
            {
                Physics.IgnoreCollision(other, col, true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) // Restore collision when leaving the lift shaft
        {
            foreach (Collider col in collidersToIgnore)
            {
                Physics.IgnoreCollision(other, col, false);
            }
        }
    }
}
