# Premium Ludo Render Server

This is the Socket.IO + Redis-ready backend for the Unity client in this project.

## Transport

- `Socket.IO` over `/socket.io`
- Health route: `GET /`
- Optional health route: `GET /api/ludo`

## Socket Events

Client to server:

- `createRoom`
- `joinRoom`
- `recoverSession`
- `startGame`
- `playTurn`
- `sendChat`
- `leaveRoom`

Server to client:

- `roomCreated`
- `playerJoined`
- `playerLeft`
- `gameStarted`
- `turnPlayed`
- `chatMessage`
- `gameStateUpdate`
- `errorMessage`

## Local Run

```bash
cd RenderServer
npm install
npm start
```

The server listens on `PORT` or `10000`.

## Render Deploy

1. Create one new `Web Service` on Render for this backend.
2. Point it to the `RenderServer` folder.
3. Runtime: `Node`
4. Build command: `npm install`
5. Start command: `npm start`
6. Add environment variable `REDIS_URL` pointing to your Redis instance.
7. Leave the service public unless you are adding your own auth layer.
8. Copy the deployed base URL after the first successful deploy.

## Unity Client Hookup

Update `DefaultServerBaseUrl` in:

- `/Users/jigar/Desktop/Ludo/Assets/Scripts/LudoOnlineService.cs`

Set it to your Render deployment URL, for example:

`https://your-service-name.onrender.com`

## Notes

- This server is server-authoritative for turn validation.
- Chat sender names come from the `PlayerName` provided by the Unity client when creating or joining.
- The host can start once at least 2 players are connected, even if the room was originally created for 3 or 4.
- When the host starts early, the match uses only the currently connected colors.
- Room state is kept in memory for hot runtime access and persisted to Redis for reconnect recovery.
- If `REDIS_URL` is missing, the service falls back to in-memory only.
- Restarting the service without Redis will clear active rooms.
- You do not need a separate chat service; chat is already part of this backend.
- The Unity client now uses event-driven sockets instead of HTTP polling.
- Reconnect recovery uses `playerId` + `roomCode`, so the client must keep those values alive for the match.
