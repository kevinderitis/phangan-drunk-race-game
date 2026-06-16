using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    public int playerID = 1;
    public float baseSpeed = 5f;
    public float strafeSpeed = 8f;
    public float jumpForce = 8f;
    public float boostMultiplier = 2f;
    public float boostDuration = 2f;
    public float roadHalfWidth = 2.5f;
    [Range(0, 1)] public float offRoadSlowFactor = 0.4f;
    public bool canMove;

    public System.Action<int> OnFinished;

    private float currentSpeed;
    private float boostTimer;
    private bool boosting;
    private bool grounded;
    private Rigidbody rb;
    private LocalWebSocketServer wsServer;
    private DrunkEffect drunk = new DrunkEffect();
    private KeyCode keyLeft, keyRight, keyJump, keyBoost;

    public int DrunkLevel => drunk.Level;
    public bool IsBoosting => boosting;
    public float RaceProgress { get; set; }
    public bool HasFinished { get; set; }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        currentSpeed = baseSpeed;

        if (playerID == 1)
        {
            keyLeft = KeyCode.A; keyRight = KeyCode.D;
            keyJump = KeyCode.W; keyBoost = KeyCode.LeftShift;
        }
        else
        {
            keyLeft = KeyCode.LeftArrow; keyRight = KeyCode.RightArrow;
            keyJump = KeyCode.UpArrow; keyBoost = KeyCode.RightShift;
        }

        wsServer = FindObjectOfType<LocalWebSocketServer>();
    }

    void Update()
    {
        if (!canMove || HasFinished) return;

        float h = 0;
        bool jump = false;
        bool boost = false;

        if (Input.GetKey(keyLeft)) h -= 1;
        if (Input.GetKey(keyRight)) h += 1;
        if (Input.GetKeyDown(keyJump)) jump = true;
        if (Input.GetKey(keyBoost)) boost = true;

        if (wsServer != null)
        {
            wsServer.GetPlayerInput(playerID, out bool l, out bool r, out bool j, out bool b);
            if (l) h -= 1;
            if (r) h += 1;
            if (j) jump = true;
            if (b) boost = true;
        }

        Vector3 input = new Vector3(h, 0, 1f);
        input = drunk.ModifyInput(input, Time.deltaTime);

        if (boost && !boosting)
        {
            boosting = true;
            boostTimer = boostDuration;
            currentSpeed = baseSpeed * boostMultiplier;
        }

        if (boosting)
        {
            boostTimer -= Time.deltaTime;
            if (boostTimer <= 0)
            {
                boosting = false;
                currentSpeed = baseSpeed;
            }
        }

        if (jump && grounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            grounded = false;
        }

        float speedMult = Mathf.Abs(rb.position.x) > roadHalfWidth ? offRoadSlowFactor : 1f;
        Vector3 move = (Vector3.forward * currentSpeed * speedMult * input.z + Vector3.right * input.x * strafeSpeed) * Time.deltaTime;
        rb.MovePosition(rb.position + move);
        RaceProgress = rb.position.z;
    }

    void OnCollisionStay(Collision col) { grounded = true; }

    void OnTriggerEnter(Collider other)
    {
        var obs = other.GetComponent<Obstacle>();
        if (obs != null)
        {
            currentSpeed = baseSpeed * obs.slowFactor;
            Invoke(nameof(RestoreSpeed), obs.slowDuration);
            Destroy(other.gameObject);
            return;
        }

        var pu = other.GetComponent<PowerUp>();
        if (pu != null)
        {
            pu.Apply(this);
            Destroy(other.gameObject);
            return;
        }

        if (other.GetComponent<FinishLine>() != null)
        {
            if (!HasFinished)
            {
                HasFinished = true;
                canMove = false;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                OnFinished?.Invoke(playerID);
            }
        }
    }

    void RestoreSpeed() { if (!boosting) currentSpeed = baseSpeed; }

    public void AddDrunk(int amt) { drunk.Add(amt); }
    public void ClearDrunk() { drunk.Clear(); }
    public void Boost(float mult, float dur)
    {
        boosting = true;
        boostTimer = dur;
        currentSpeed = baseSpeed * mult;
    }
}
