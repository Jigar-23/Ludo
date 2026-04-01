const express = require("express");
const cors = require("cors");

const app = express();
const port = process.env.PORT || 10000;

app.use(cors());
app.use(express.json({ limit: "256kb" }));

const clockwiseColors = ["Red", "Green", "Yellow", "Blue"];
const rooms = new Map();

function normalizeColor(value) {
  const normalized = String(value || "").trim().toLowerCase();
  const match = clockwiseColors.find((color) => color.toLowerCase() === normalized);
  return match || null;
}

function normalizeName(value) {
  const trimmed = String(value || "").trim();
  if (!trimmed) {
    return "Player";
  }

  return trimmed.slice(0, 18);
}

function buildSnapshot(room) {
  return {
    RoomCode: room.roomCode,
    PlayerCount: room.playerCount,
    ActiveColors: room.activeColors.slice(),
    Seats: room.activeColors.map((color) => {
      const seat = room.seats.get(color);
      return {
        Color: color,
        DisplayName: seat ? seat.displayName : "",
        IsHost: seat ? seat.isHost : false,
        Connected: seat ? seat.connected : false,
      };
    }),
    Started: room.started,
    RoomSequence: room.roomSequence,
  };
}

function success(snapshot, extras = {}) {
  return {
    Success: true,
    AssignedColor: "",
    Snapshot: snapshot,
    ...extras,
  };
}

function failure(message) {
  return {
    Success: false,
    Error: message,
  };
}

function generateRoomCode() {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  for (let i = 0; i < 6; i += 1) {
    code += alphabet[Math.floor(Math.random() * alphabet.length)];
  }

  return code;
}

function getUniqueRoomCode() {
  let attempts = 0;
  while (attempts < 20) {
    const code = generateRoomCode();
    if (!rooms.has(code)) {
      return code;
    }

    attempts += 1;
  }

  return `ROOM${Date.now().toString().slice(-4)}`;
}

function getRoomOrFail(roomCode) {
  const normalized = String(roomCode || "").trim().toUpperCase();
  if (!normalized || !rooms.has(normalized)) {
    return [null, normalized, failure("Room not found.")];
  }

  return [rooms.get(normalized), normalized, null];
}

function incrementRoomSequence(room) {
  room.roomSequence += 1;
}

function appendChat(room, sender, message, color) {
  room.chatSequence += 1;
  room.chatMessages.push({
    Sender: sender,
    Message: message,
    Color: color,
    Sequence: room.chatSequence,
    SentAtUtc: new Date().toISOString(),
  });
}

function appendTurn(room, payload) {
  room.turnSequence += 1;
  room.turnActions.push({
    Color: payload.color,
    Roll: payload.roll,
    TokenIndex: payload.tokenIndex,
    NoMove: !!payload.noMove,
    Sequence: room.turnSequence,
  });
}

app.get("/", (_req, res) => {
  res.json({ ok: true, service: "premium-ludo-render-server" });
});

app.post("/api/ludo/rooms/create", (req, res) => {
  const playerCount = Math.max(2, Math.min(4, Number(req.body?.PlayerCount || req.body?.playerCount || 2)));
  const localColor = normalizeColor(req.body?.LocalColor || req.body?.localColor);
  const activeColors = Array.isArray(req.body?.activeColors)
    ? req.body.activeColors.map(normalizeColor).filter(Boolean)
    : Array.isArray(req.body?.ActiveColors)
    ? req.body.ActiveColors.map(normalizeColor).filter(Boolean)
    : [];

  if (!localColor) {
    return res.status(400).json(failure("A valid localColor is required."));
  }

  const uniqueColors = [];
  for (const color of activeColors) {
    if (!uniqueColors.includes(color)) {
      uniqueColors.push(color);
    }
  }

  if (!uniqueColors.includes(localColor)) {
    uniqueColors.unshift(localColor);
  }

  if (uniqueColors.length < playerCount) {
    for (const fallbackColor of clockwiseColors) {
      if (!uniqueColors.includes(fallbackColor)) {
        uniqueColors.push(fallbackColor);
      }

      if (uniqueColors.length >= playerCount) {
        break;
      }
    }
  }

  const roomCode = getUniqueRoomCode();
  const room = {
    roomCode,
    playerCount,
    activeColors: uniqueColors.slice(0, playerCount),
    seats: new Map(),
    started: false,
    roomSequence: 1,
    chatSequence: 0,
    turnSequence: 0,
    chatMessages: [],
    turnActions: [],
  };

  room.seats.set(localColor, {
    color: localColor,
    displayName: normalizeName(req.body?.PlayerName || req.body?.playerName),
    isHost: true,
    connected: true,
  });

  rooms.set(roomCode, room);
  return res.json(success(buildSnapshot(room), { AssignedColor: localColor }));
});

app.post("/api/ludo/rooms/join", (req, res) => {
  const [room] = getRoomOrFail(req.body?.RoomCode || req.body?.roomCode);
  if (!room) {
    return res.status(404).json(failure("Room not found."));
  }

  if (room.started) {
    return res.status(409).json(failure("That room has already started."));
  }

  const preferredColor = normalizeColor(req.body?.PreferredColor || req.body?.preferredColor);
  let seatColor = preferredColor;
  if (!seatColor || !room.activeColors.includes(seatColor) || room.seats.has(seatColor)) {
    seatColor = room.activeColors.find((color) => !room.seats.has(color)) || null;
  }

  if (!seatColor) {
    return res.status(409).json(failure("That room is full."));
  }

  room.seats.set(seatColor, {
    color: seatColor,
    displayName: normalizeName(req.body?.PlayerName || req.body?.playerName),
    isHost: false,
    connected: true,
  });
  incrementRoomSequence(room);

  return res.json(success(buildSnapshot(room), { AssignedColor: seatColor }));
});

app.post("/api/ludo/rooms/:roomCode/start", (req, res) => {
  const [room] = getRoomOrFail(req.params.roomCode);
  if (!room) {
    return res.status(404).json(failure("Room not found."));
  }

  const connectedCount = room.activeColors.filter((color) => room.seats.has(color)).length;
  if (connectedCount < room.playerCount) {
    return res.status(409).json(failure("Not all players have joined yet."));
  }

  room.started = true;
  incrementRoomSequence(room);
  return res.json(success(buildSnapshot(room)));
});

app.post("/api/ludo/rooms/:roomCode/leave", (req, res) => {
  const [room] = getRoomOrFail(req.params.roomCode);
  if (!room) {
    return res.status(404).json(failure("Room not found."));
  }

  const color = normalizeColor(req.body?.Color || req.body?.color);
  if (color && room.seats.has(color)) {
    room.seats.delete(color);
    incrementRoomSequence(room);
  }

  if (room.seats.size === 0) {
    rooms.delete(room.roomCode);
    return res.json(success(null));
  }

  return res.json(success(buildSnapshot(room)));
});

app.post("/api/ludo/rooms/:roomCode/chat", (req, res) => {
  const [room] = getRoomOrFail(req.params.roomCode);
  if (!room) {
    return res.status(404).json(failure("Room not found."));
  }

  const message = String(req.body?.Message || req.body?.message || "").trim();
  if (!message) {
    return res.status(400).json(failure("Chat messages cannot be empty."));
  }

  appendChat(
    room,
    normalizeName(req.body?.Sender || req.body?.sender),
    message.slice(0, 180),
    normalizeColor(req.body?.Color || req.body?.color) || "Blue"
  );

  return res.json(success(buildSnapshot(room)));
});

app.post("/api/ludo/rooms/:roomCode/turn", (req, res) => {
  const [room] = getRoomOrFail(req.params.roomCode);
  if (!room) {
    return res.status(404).json(failure("Room not found."));
  }

  const color = normalizeColor(req.body?.Color || req.body?.color);
  const roll = Number(req.body?.Roll || req.body?.roll || 0);
  if (!color || roll < 1 || roll > 6) {
    return res.status(400).json(failure("A valid turn payload is required."));
  }

  appendTurn(room, {
    color,
    roll,
    tokenIndex: Number((req.body?.TokenIndex ?? req.body?.tokenIndex) ?? -1),
    noMove: !!(req.body?.NoMove ?? req.body?.noMove),
  });

  return res.json(success(buildSnapshot(room)));
});

app.get("/api/ludo/rooms/:roomCode/poll", (req, res) => {
  const [room] = getRoomOrFail(req.params.roomCode);
  if (!room) {
    return res.status(404).json(failure("Room not found."));
  }

  const roomSequence = Number(req.query.roomSequence || req.query.RoomSequence || 0);
  const chatSequence = Number(req.query.chatSequence || req.query.ChatSequence || 0);
  const turnSequence = Number(req.query.turnSequence || req.query.TurnSequence || 0);

  const snapshot = buildSnapshot(room);
  const chatMessages = room.chatMessages.filter((entry) => entry.Sequence > chatSequence);
  const turnActions = room.turnActions.filter((entry) => entry.Sequence > turnSequence);

  return res.json(
    success(snapshot, {
      ChatMessages: chatMessages,
      TurnActions: turnActions,
    })
  );
});

app.listen(port, () => {
  console.log(`Premium Ludo Render server listening on port ${port}`);
});
