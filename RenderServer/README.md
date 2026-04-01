# Premium Ludo Render Server

This is a simple Render-ready backend for the Unity client in this project.

## Endpoints

- `POST /api/ludo/rooms/create`
- `POST /api/ludo/rooms/join`
- `POST /api/ludo/rooms/:roomCode/start`
- `POST /api/ludo/rooms/:roomCode/leave`
- `POST /api/ludo/rooms/:roomCode/chat`
- `POST /api/ludo/rooms/:roomCode/turn`
- `GET /api/ludo/rooms/:roomCode/poll`

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
6. Leave the service public unless you are adding your own auth layer.
7. Copy the deployed base URL after the first successful deploy.

## Unity Client Hookup

Update `DefaultServerBaseUrl` in:

- `/Users/jigar/Desktop/Ludo/Assets/Scripts/LudoOnlineService.cs`

Set it to your Render deployment URL, for example:

`https://your-service-name.onrender.com/api/ludo`

## Notes

- This server already supports room create/join, chat, turn sync, and polling.
- Chat sender names come from the `PlayerName` provided by the Unity client when creating or joining.
- This server keeps room state in memory only.
- Restarting the service clears active rooms.
- You do not need a separate chat service right now; chat is already part of this backend.
- It is a lightweight starting point that you can later extend with auth, persistence, reconnect handling, and stronger validation.
