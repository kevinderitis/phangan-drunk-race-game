using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class GameRoot : MonoBehaviour
{
    public enum State { Lobby, Countdown, Racing, Results }
    public State state = State.Lobby;

    private LocalHttpServer httpServer;
    private LocalWebSocketServer wsServer;
    private GameObject sceneRoot;
    private PlayerController[] players = new PlayerController[3];
    private float countdownTimer;
    private float raceTimer;
    private int winner;

    private Text ipText, p1Status, p2Status;
    private Text timerText, cdText;
    private Text p1Label, p2Label, p1Drunk, p2Drunk;
    private Text p1FinishText, p2FinishText;
    private Text winText;
    private Button startBtn, rematchBtn, lobbyBtn;
    private RawImage qrImage;
    private float[] finishTimes = new float[3];

    void Start()
    {
        if (Camera.main == null)
        {
            var g = new GameObject("MainCamera");
            var c = g.AddComponent<Camera>();
            c.tag = "MainCamera";
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.4f, 0.6f, 0.85f);
        }

        if (FindObjectOfType<Light>() == null)
        {
            var lg = new GameObject("Directional Light");
            var l = lg.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.95f, 0.82f);
            l.intensity = 1.2f;
            lg.transform.rotation = Quaternion.Euler(50, -30, 0);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.5f);

        DontDestroyOnLoad(gameObject);
        httpServer = gameObject.AddComponent<LocalHttpServer>();
        wsServer = gameObject.AddComponent<LocalWebSocketServer>();
        wsServer.OnPlayerConnected += OnPlayerChange;
        wsServer.OnPlayerDisconnected += OnPlayerChange;
        EnterLobby();
    }

    void OnPlayerChange(int pid)
    {
        if (state == State.Lobby) UpdateLobbyUI();
    }

    void ClearScene()
    {
        if (sceneRoot != null) Destroy(sceneRoot);
        sceneRoot = new GameObject("SceneRoot");
        sceneRoot.transform.SetParent(transform);
    }

    Transform P => sceneRoot.transform;

    // ---- UI Builders ----

    Canvas MakeCanvas()
    {
        GameObject g = new GameObject("Canvas");
        g.transform.SetParent(P, false);
        Canvas c = g.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        g.AddComponent<CanvasScaler>();
        g.AddComponent<GraphicRaycaster>();
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
        return c;
    }

    Font GetFont()
    {
        try { Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if (f != null) return f; } catch { }
        try { Font f = Resources.GetBuiltinResource<Font>("Arial.ttf"); if (f != null) return f; } catch { }
        return null;
    }

    Text MakeText(Transform parent, string txt, int size, Color col, Vector2 pos, Vector2 sd)
    {
        var g = new GameObject("T");
        g.transform.SetParent(parent, false);
        var t = g.AddComponent<Text>();
        t.text = txt; t.fontSize = size; t.color = col;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = GetFont();
        var rt = g.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = sd;
        return t;
    }

    Button MakeBtn(Transform parent, string txt, Vector2 pos, Vector2 sd, System.Action cb)
    {
        var g = new GameObject("B");
        g.transform.SetParent(parent, false);
        var btn = g.AddComponent<Button>();
        g.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.35f);

        var lg = new GameObject("L");
        lg.transform.SetParent(g.transform, false);
        var l = lg.AddComponent<Text>();
        l.text = txt; l.fontSize = 22; l.color = Color.white;
        l.alignment = TextAnchor.MiddleCenter; l.font = GetFont();
        var lr = lg.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.sizeDelta = Vector2.zero;

        btn.onClick.AddListener(() => cb());
        var br = g.GetComponent<RectTransform>();
        br.anchoredPosition = pos; br.sizeDelta = sd;
        return btn;
    }

    // ---- Lobby ----

    void EnterLobby()
    {
        ClearScene();
        state = State.Lobby;
        var c = MakeCanvas();
        var p = c.transform;

        MakeText(p, "HOSTEL DRUNK RACE", 36, new Color(1, 0.42f, 0.21f), new Vector2(0, 210), new Vector2(500, 60));
        MakeText(p, "Koh Phangan", 18, new Color(0.52f, 0.37f, 0.97f), new Vector2(0, 175), new Vector2(300, 30));

        string ip = LocalIpProvider.GetLocalIP();
        string url = "http://" + ip + ":8080";
        ipText = MakeText(p, url, 18, Color.white, new Vector2(0, 95), new Vector2(500, 30));
        MakeText(p, "Scan QR or open in browser", 14, Color.gray, new Vector2(0, 68), new Vector2(400, 24));

        var qrG = new GameObject("QRCode");
        qrG.transform.SetParent(p, false);
        qrImage = qrG.AddComponent<RawImage>();
        qrImage.color = Color.white;
        var qrRt = qrG.GetComponent<RectTransform>();
        qrRt.anchoredPosition = new Vector2(0, -45);
        qrRt.sizeDelta = new Vector2(130, 130);
        StartCoroutine(LoadQRCode(url));

        p1Status = MakeText(p, "Player 1: Waiting...", 20, Color.gray, new Vector2(0, -120), new Vector2(400, 26));
        p2Status = MakeText(p, "Player 2: Waiting...", 20, Color.gray, new Vector2(0, -150), new Vector2(400, 26));

        startBtn = MakeBtn(p, "START RACE", new Vector2(0, -210), new Vector2(240, 60), EnterRace);
        startBtn.interactable = false;

        UpdateLobbyUI();
    }

    IEnumerator LoadQRCode(string url)
    {
        string apiUrl = "https://api.qrserver.com/v1/create-qr-code/?size=256x256&data=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(url);
        using (var uwr = UnityEngine.Networking.UnityWebRequest.Get(apiUrl))
        {
            uwr.timeout = 5;
            yield return uwr.SendWebRequest();
            if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                byte[] bytes = uwr.downloadHandler.data;
                if (bytes != null && bytes.Length > 0)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    UnityEngine.ImageConversion.LoadImage(tex, bytes);
                    if (qrImage != null) qrImage.texture = tex;
                }
            }
        }
    }

    void UpdateLobbyUI()
    {
        string p1Name = wsServer != null ? wsServer.GetPlayerName(1) : null;
        string p2Name = wsServer != null ? wsServer.GetPlayerName(2) : null;
        bool p1 = !string.IsNullOrEmpty(p1Name);
        bool p2 = !string.IsNullOrEmpty(p2Name);
        if (p1Status != null) p1Status.text = "Player 1: " + (p1 ? "<color=#69db7c>" + p1Name + "</color>" : "Waiting...");
        if (p2Status != null) p2Status.text = "Player 2: " + (p2 ? "<color=#69db7c>" + p2Name + "</color>" : "Waiting...");
        if (startBtn != null) startBtn.interactable = p1 && p2;
    }

    // ---- Race ----

    void EnterRace()
    {
        ClearScene();
        state = State.Countdown;
        countdownTimer = 4f;
        raceTimer = 60f;
        winner = 0;
        players = new PlayerController[3];
        finishTimes[1] = finishTimes[2] = -1f;

        BuildTrack();
        CreatePlayer(1, new Vector3(-1.5f, 0.5f, 0f), new Color(0.3f, 0.6f, 1f));
        CreatePlayer(2, new Vector3(1.5f, 0.5f, 0f), new Color(1f, 0.3f, 0.3f));
        MakeCamera("P1Cam", players[1].transform, new Rect(0, 0.5f, 1, 0.5f));
        MakeCamera("P2Cam", players[2].transform, new Rect(0, 0, 1, 0.5f));

        var c = MakeCanvas();
        var p = c.transform;
        cdText = MakeText(p, "", 80, Color.white, Vector2.zero, new Vector2(200, 100));
        timerText = MakeText(p, "", 28, Color.white, new Vector2(0, 240), new Vector2(200, 40));
        p1Label = MakeText(p, "P1", 18, new Color(0.3f, 0.6f, 1f), new Vector2(-200, 120), new Vector2(100, 24));
        p2Label = MakeText(p, "P2", 18, new Color(1f, 0.3f, 0.3f), new Vector2(-200, -120), new Vector2(100, 24));
        p1Drunk = MakeText(p, "Drunk: .....", 16, Color.white, new Vector2(-200, 90), new Vector2(150, 20));
        p2Drunk = MakeText(p, "Drunk: .....", 16, Color.white, new Vector2(-200, -150), new Vector2(150, 20));
        p1FinishText = MakeText(p, "", 20, new Color(0.3f, 0.6f, 1f), new Vector2(0, 60), new Vector2(300, 24));
        p2FinishText = MakeText(p, "", 20, new Color(1f, 0.3f, 0.3f), new Vector2(0, -200), new Vector2(300, 24));
    }

    void BuildTrack()
    {
        var ground = CreatePrim(PrimitiveType.Plane, new Vector3(0, -0.5f, 50), Vector3.one * 20, new Color(0.85f, 0.78f, 0.58f));
        ground.transform.localScale = new Vector3(5, 1, 10);

        CreatePrim(PrimitiveType.Cube, new Vector3(0, -0.4f, 50), new Vector3(5, 0.1f, 100), new Color(0.18f, 0.18f, 0.2f));

        for (int z = 0; z < 100; z += 5)
            CreatePrim(PrimitiveType.Cube, new Vector3(0, -0.3f, z + 1.5f), new Vector3(0.08f, 0.05f, 2f), Color.white);

        for (int z = 0; z < 100; z += 3)
        {
            CreatePrim(PrimitiveType.Cube, new Vector3(-2.3f, -0.3f, z), new Vector3(0.04f, 0.05f, 2.8f), new Color(1f, 0.84f, 0));
            CreatePrim(PrimitiveType.Cube, new Vector3(2.3f, -0.3f, z), new Vector3(0.04f, 0.05f, 2.8f), new Color(1f, 0.84f, 0));
        }

        CreatePrim(PrimitiveType.Cube, new Vector3(-4.8f, 0.5f, 50), new Vector3(0.3f, 1.5f, 100), new Color(0.55f, 0.55f, 0.58f));
        CreatePrim(PrimitiveType.Cube, new Vector3(4.8f, 0.5f, 50), new Vector3(0.3f, 1.5f, 100), new Color(0.55f, 0.55f, 0.58f));

        for (int z = 10; z < 100; z += 8)
        {
            CreatePrim(PrimitiveType.Cube, new Vector3(-4.8f, 1.8f, z), new Vector3(0.4f, 0.8f, 0.8f), new Color(0.9f, 0.9f, 0.1f));
            CreatePrim(PrimitiveType.Cube, new Vector3(4.8f, 1.8f, z), new Vector3(0.4f, 0.8f, 0.8f), new Color(0.9f, 0.9f, 0.1f));
        }

        CreatePrim(PrimitiveType.Cube, new Vector3(-2.5f, 2f, 90), new Vector3(0.3f, 3.5f, 0.3f), new Color(0.9f, 0.1f, 0.1f));
        CreatePrim(PrimitiveType.Cube, new Vector3(2.5f, 2f, 90), new Vector3(0.3f, 3.5f, 0.3f), new Color(0.9f, 0.1f, 0.1f));
        CreatePrim(PrimitiveType.Cube, new Vector3(0, 4f, 90), new Vector3(5.3f, 0.3f, 0.3f), new Color(0.9f, 0.1f, 0.1f));

        var fin = CreatePrim(PrimitiveType.Cube, new Vector3(0, 0.1f, 90), new Vector3(4.8f, 0.2f, 0.5f), Color.white);
        for (int x = -2; x <= 2; x++)
            CreatePrim(PrimitiveType.Cube, new Vector3(x * 1.2f, 0.1f, 90.3f), new Vector3(0.6f, 0.2f, 0.1f), Color.black);
        fin.AddComponent<FinishLine>();

        for (int i = 0; i < 8; i++)
            CreatePrim(PrimitiveType.Cube, RandPos(), Vector3.one * 0.5f, new Color(0.8f, 0.2f, 0.2f)).AddComponent<Obstacle>();

        var pTypes = new[] { PowerUpType.BeerBucket, PowerUpType.Coffee, PowerUpType.TukTukBoost, PowerUpType.SangsomBucket };
        for (int i = 0; i < 5; i++)
        {
            var pu = CreatePrim(PrimitiveType.Sphere, RandPos(), Vector3.one * 0.6f, Color.green);
            pu.AddComponent<PowerUp>().type = pTypes[UnityEngine.Random.Range(0, pTypes.Length)];
        }

        for (int i = 0; i < 8; i++)
        {
            float z = UnityEngine.Random.Range(0f, 95f);
            float x = (i % 2 == 0) ? UnityEngine.Random.Range(-5f, -3.2f) : UnityEngine.Random.Range(3.2f, 5f);
            var t = CreatePrim(PrimitiveType.Cylinder, new Vector3(x, 1, z), new Vector3(0.15f, 1.2f, 0.15f), new Color(0.45f, 0.3f, 0.08f));
            CreatePrim(PrimitiveType.Sphere, new Vector3(x, 2.8f, z), new Vector3(0.8f, 0.5f, 0.8f), new Color(0.15f, 0.6f, 0.08f));
        }
    }

    GameObject CreatePrim(PrimitiveType type, Vector3 pos, Vector3 scale, Color col)
    {
        var g = GameObject.CreatePrimitive(type);
        g.transform.SetParent(P);
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().material.color = col;
        return g;
    }

    Vector3 RandPos()
    {
        return new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0.5f, UnityEngine.Random.Range(10f, 80f));
    }

    void CreatePlayer(int id, Vector3 pos, Color color)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        g.transform.SetParent(P);
        g.transform.position = pos;
        g.transform.localScale = new Vector3(0.7f, 1, 0.7f);
        g.GetComponent<Renderer>().material.color = color;

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(head.GetComponent<SphereCollider>());
        head.transform.SetParent(g.transform);
        head.transform.localPosition = new Vector3(0, 1.1f, 0);
        head.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        head.GetComponent<Renderer>().material.color = color;

        var rb = g.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var pc = g.AddComponent<PlayerController>();
        pc.playerID = id;
        pc.canMove = false;
        pc.OnFinished += (pid) =>
        {
            if (finishTimes[pid] < 0) finishTimes[pid] = 60f - raceTimer;
            if (winner == 0) winner = pid;
        };
        players[id] = pc;
    }

    void MakeCamera(string name, Transform target, Rect viewport)
    {
        var g = new GameObject(name);
        g.transform.SetParent(P);
        var cam = g.AddComponent<Camera>();
        cam.rect = viewport;
        cam.depth = 0;
        g.AddComponent<CameraFollow>().target = target;
    }

    // ---- Results ----

    void EnterResults()
    {
        ClearScene();
        state = State.Results;

        if (winner == 0)
        {
            float p1p = players[1]?.RaceProgress ?? 0;
            float p2p = players[2]?.RaceProgress ?? 0;
            winner = p1p >= p2p ? 1 : 2;
        }

        var c = MakeCanvas();
        var p = c.transform;

        winText = MakeText(p, "Player " + winner + " Wins!", 42, new Color(1, 0.84f, 0), new Vector2(0, 130), new Vector2(500, 60));

        string p1Line = finishTimes[1] >= 0
            ? "Player 1: " + finishTimes[1].ToString("F1") + "s"
            : "Player 1: DNF";
        string p2Line = finishTimes[2] >= 0
            ? "Player 2: " + finishTimes[2].ToString("F1") + "s"
            : "Player 2: DNF";

        MakeText(p, p1Line, 22, new Color(0.3f, 0.6f, 1f), new Vector2(0, 55), new Vector2(300, 28));
        MakeText(p, p2Line, 22, new Color(1f, 0.3f, 0.3f), new Vector2(0, 20), new Vector2(300, 28));

        MakeBtn(p, "REMATCH", new Vector2(0, -80), new Vector2(240, 60), EnterRace);
        MakeBtn(p, "LOBBY", new Vector2(0, -160), new Vector2(240, 60), EnterLobby);
    }

    // ---- Update ----

    void Update()
    {
        if (state == State.Lobby)
        {
            UpdateLobbyUI();
        }
        else if (state == State.Countdown)
        {
            countdownTimer -= Time.deltaTime;
            if (cdText != null)
            {
                if (countdownTimer > 1f)
                    cdText.text = Mathf.CeilToInt(countdownTimer).ToString();
                else if (countdownTimer > 0f)
                    cdText.text = "GO!";
                else
                    cdText.text = "";
            }

            if (countdownTimer <= 0f)
            {
                state = State.Racing;
                if (players[1] != null) players[1].canMove = true;
                if (players[2] != null) players[2].canMove = true;
            }
        }
        else if (state == State.Racing)
        {
            raceTimer -= Time.deltaTime;
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(raceTimer).ToString();
                timerText.color = raceTimer < 10f ? Color.red : Color.white;
            }
            if (p1Drunk != null) p1Drunk.text = "Drunk: " + Bar(players[1]);
            if (p2Drunk != null) p2Drunk.text = "Drunk: " + Bar(players[2]);

            if (p1FinishText != null)
            {
                if (players[1] != null && players[1].HasFinished && finishTimes[1] >= 0)
                    p1FinishText.text = "Finished! " + finishTimes[1].ToString("F1") + "s";
                else
                    p1FinishText.text = "";
            }
            if (p2FinishText != null)
            {
                if (players[2] != null && players[2].HasFinished && finishTimes[2] >= 0)
                    p2FinishText.text = "Finished! " + finishTimes[2].ToString("F1") + "s";
                else
                    p2FinishText.text = "";
            }

            bool allFinished = (players[1]?.HasFinished ?? false) && (players[2]?.HasFinished ?? false);
            if (allFinished || raceTimer <= 0f)
                EnterResults();
        }
    }

    string Bar(PlayerController p)
    {
        if (p == null) return ".....";
        return new string('|', p.DrunkLevel + 1).PadRight(5, '.');
    }
}

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 2, -3);

    void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
            transform.LookAt(target);
        }
    }
}
