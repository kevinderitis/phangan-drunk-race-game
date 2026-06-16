using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public float slowFactor = 0.3f;
    public float slowDuration = 1f;

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
    }
}
