using UnityEngine;

public class FinishLine : MonoBehaviour
{
    void Start()
    {
        var col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
    }
}
