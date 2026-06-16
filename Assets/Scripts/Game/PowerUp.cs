using UnityEngine;

public enum PowerUpType
{
    BeerBucket,
    SangsomBucket,
    Coffee,
    TukTukBoost,
}

public class PowerUp : MonoBehaviour
{
    public PowerUpType type = PowerUpType.BeerBucket;

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
    }

    public void Apply(PlayerController player)
    {
        switch (type)
        {
            case PowerUpType.BeerBucket:
                player.AddDrunk(1);
                break;
            case PowerUpType.SangsomBucket:
                player.AddDrunk(2);
                break;
            case PowerUpType.Coffee:
                player.ClearDrunk();
                break;
            case PowerUpType.TukTukBoost:
                player.Boost(2f, 2f);
                break;
        }
    }
}
