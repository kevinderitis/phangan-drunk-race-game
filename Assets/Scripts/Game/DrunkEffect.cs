using UnityEngine;

[System.Serializable]
public class DrunkEffect
{
    public int Level { get; private set; }
    private float timer;

    public void Add(int amount)
    {
        Level = Mathf.Clamp(Level + amount, 0, 4);
    }

    public void Clear()
    {
        Level = 0;
        timer = 0;
    }

    public Vector3 ModifyInput(Vector3 input, float dt)
    {
        if (Level == 0) return input;

        timer += dt;

        if (Level >= 1)
            input.x += Mathf.Sin(timer * 3f + Mathf.Sin(timer * 1.2f)) * 0.3f;

        if (Level >= 2)
            input.x += Mathf.Sin(timer * 1.5f) * 0.5f;

        if (Level >= 3 && Mathf.Sin(timer * 0.7f) > 0.7f)
            input.x *= -1f;

        if (Level >= 4)
        {
            input.x += Mathf.Sin(timer * 5f) * 0.8f;
            input.z = Mathf.Lerp(input.z, input.z * 0.7f, Mathf.PingPong(timer * 0.5f, 1));
        }

        return input;
    }
}
