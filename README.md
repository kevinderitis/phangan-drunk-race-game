# Hostel Drunk Race

Local 1v1 party racing game for Android TV. Phones = controllers.

## Requirements

- **Unity 2022.3 LTS** (free, from unity.com)
- Android SDK (for APK build)

## Setup (30 seconds)

1. **Open the project in Unity.** Wait for scripts to compile.
2. **Save the empty scene:** `File → Save As → Assets/Scenes/LobbyScene.unity`.
3. **Add GameRoot:** `GameObject → Create Empty`, name it `GameRoot`, attach the `GameRoot.cs` script (drag from Project panel).
4. **Add scene to Build:** `File → Build Settings → Add Open Scenes`.
5. **Press Play.**

That's it. `GameRoot.cs` auto-generates the entire game at runtime — lobby UI, race track, players, cameras, results screen.

### How to Test with Keyboard

Works immediately:

- **Player 1**: A / D / W / Left Shift
- **Player 2**: ← → ↑ / Right Shift

Both players share the screen. Controls are split-screen cameras.

### How to Test with Browser Tabs

1. Run the game (Play).
2. Look at the lobby screen for the IP (e.g. `192.168.1.100:8080`).
3. Open **two browser tabs** at `http://IP:8080`.
4. Tab 1 → tap **Player 1**. Tab 2 → tap **Player 2**.
5. Back in Unity, click **START RACE**.

### How to Test with Phones

1. Same as browser tabs, but on phones connected to the same WiFi.
2. Open the URL on each phone.

Everything is auto-generated. No scene setup needed beyond the initial empty scene.

## Network

| Service | Port | What |
|---------|------|------|
| HTTP | 8080 | Controller web page |
| WebSocket | 8081 | Player input |

Servers start automatically with `GameRoot`.

## Controls

| Action | P1 Keyboard | P2 Keyboard | Phone |
|--------|-------------|-------------|-------|
| LEFT | A | ← | LEFT button |
| RIGHT | D | → | RIGHT button |
| JUMP | W | ↑ | JUMP button |
| BOOST | Left Shift | Right Shift | BOOST button |

## Drunk Effects

| Level | Effect |
|-------|--------|
| 1 | Wobble |
| 2 | Steering drift |
| 3 | Random input inversion |
| 4 | Strong wobble + slow |

## Power-Ups

| Item | Effect |
|------|--------|
| Beer Bucket | +speed, +1 drunk |
| Sangsom Bucket | +speed, +2 drunk |
| Coffee | Clear drunk |
| Tuk-Tuk Boost | Speed ×2 for 2s |

## Project Structure

```
Assets/
  Scripts/
    GameRoot.cs              ← Entry point. Attach to any GameObject.
    Networking/
      LocalHttpServer.cs       HTTP server
      LocalWebSocketServer.cs  WebSocket + player input
      LocalIpProvider.cs       IP detection
    Game/
      PlayerController.cs     Movement, drunk, collisions
      Obstacle.cs             Obstacle marker
      PowerUp.cs              PowerUp marker + types
      DrunkEffect.cs          Drunk math
      FinishLine.cs           Finish trigger
  StreamingAssets/
    ControllerPage/
      index.html              Phone controller page
```

## How to Build APK

```bash
# Build in Unity: File → Build Settings → Android → Build
# Then install on TV:
adb connect TV_IP:5555
adb install -r HostelDrunkRace.apk
```

## No Cloud

Everything runs locally. Zero external services.
