# Network Co-op Prototype

A first-person co-op game for 2 to 4 players, including the host. Players share a room where they can spawn, grab and throw physics cubes. Built with Unity 6000.3.23f1, Netcode for GameObjects 2.14.0 and Unity Transport.

## Run a session

1. Launch the Windows build. In the editor, open `Assets/Project/Scenes/0Boot.unity` and press Play.
2. Enter a name and port (for example, `7777`), then click **Host**.
3. On another PC, enter a name, the host's LAN IP and the same port. Click **Join**. In the lobby, the host can kick a player or click **Start Game**.

To run two copies on one PC, launch them with separate profiles and join `127.0.0.1`. Each profile keeps its own player ID between runs.

```powershell
.\TestWork.exe --profile=host
.\TestWork.exe --profile=guest
```

| Input | Action |
| --- | --- |
| WASD, mouse | Move and look |
| Q | Spawn a cube |
| E | Pick up the cube under the crosshair or drop the one you're holding |
| Left click | Throw the held cube |
| X | Delete the held cube |
| C | Change your color |
| Hold Tab | Show players and ping |
| Esc | Open or close the pause menu |

## How the match works

```text
Menu -> ConnectionService -> Unity Transport
Lobby and HUD <- NetworkSessionState <- NetworkSceneLoader
Game scene ready -> PlayerSpawner -> player prefab
PlayerItemInteractor -> host -> PhysicsItem
```

The host checks for a free slot, matching build and protocol versions, and a player ID that isn't already in the session. It waits for everyone to load the game scene. If someone disconnects during loading, the others can still start. The host sets the start time one second ahead on the network clock. Players who join mid-match see the current player colors and who is holding each cube. If the host leaves, clients return to the menu.

Each player moves locally so walking and turning respond immediately. The player's `NetworkTransform` sends movement updates from its owner, and the other clients interpolate between them. The network tick rate is 30 Hz.

The host spawns the cubes. When someone tries to pick one up, the host checks that it's free and within reach. If two players reach for the same cube, the first valid request wins. The holder gets ownership, but the host still moves the cube and sends its position and rotation to everyone. On a throw, the host checks the direction, takes ownership back and applies the impulse. This keeps clients from calculating different trajectories for the same cube. Dropping the cube or disconnecting also returns it to the host.

## Extending the prototype

To switch transports, select the new transport on the boot scene's `NetworkManager` and update the `UnityTransport` reference, component requirement and connection setup in `ConnectionService`.

To add another item with the same grab and throw behavior, duplicate the cube prefab, change its mesh and collider, and register it in `DefaultNetworkPrefabs`. Q currently spawns the single prefab assigned to `PlayerItemInteractor`. To offer both items, change that field to a list and have the host spawn the selected entry.

## Known limitation

The 12-second join timeout includes scene synchronization. A slow client can time out after the host has accepted it. The fix would be to stop the join timer on approval and time scene loading separately.

Approximate time spent on development and testing: 8 hours.
