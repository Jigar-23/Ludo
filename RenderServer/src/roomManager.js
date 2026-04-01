const crypto = require("crypto");
const { CLOCKWISE_COLORS, DISCONNECT_GRACE_MS } = require("./constants");
const { normalizeColor, normalizeName, createInitialTokenStates, playTurn } = require("./gameLogic");

class RoomManager {
  constructor(store) {
    this.store = store;
    this.rooms = new Map();
  }

  async createRoom(payload, socketId) {
    const playerCount = Math.max(2, Math.min(4, Number(payload?.PlayerCount ?? payload?.playerCount ?? 2)));
    const localColor = normalizeColor(payload?.LocalColor ?? payload?.localColor);
    if (!localColor) {
      throw new Error("A valid local color is required.");
    }

    const requestedColors = Array.isArray(payload?.ActiveColors)
      ? payload.ActiveColors
      : Array.isArray(payload?.activeColors)
        ? payload.activeColors
        : [];

    const activeColors = [];
    for (const value of requestedColors) {
      const normalized = normalizeColor(value);
      if (normalized && !activeColors.includes(normalized)) {
        activeColors.push(normalized);
      }
    }

    if (!activeColors.includes(localColor)) {
      activeColors.unshift(localColor);
    }

    for (const fallbackColor of CLOCKWISE_COLORS) {
      if (activeColors.length >= playerCount) {
        break;
      }

      if (!activeColors.includes(fallbackColor)) {
        activeColors.push(fallbackColor);
      }
    }

    const room = {
      roomId: crypto.randomUUID(),
      roomCode: await this.generateRoomCode(),
      desiredPlayerCount: playerCount,
      playerCount,
      activeColors: activeColors.slice(0, playerCount),
      players: [],
      started: false,
      roomSequence: 1,
      stateVersion: 0,
      currentTurnColor: "",
      winnerColor: "",
      tokenStates: createInitialTokenStates(),
      chatSequence: 0,
      turnSequence: 0,
      lastActivityAt: Date.now(),
    };

    const hostPlayer = {
      playerId: crypto.randomUUID(),
      displayName: normalizeName(payload?.PlayerName ?? payload?.playerName),
      color: localColor,
      isHost: true,
      connected: true,
      socketId,
      lastSeenAt: Date.now(),
      disconnectDeadlineAt: 0,
    };

    room.players.push(hostPlayer);
    await this.persistRoom(room);
    return { room, player: hostPlayer };
  }

  async joinRoom(payload, socketId) {
    const roomCode = String(payload?.RoomCode ?? payload?.roomCode ?? "").trim().toUpperCase();
    const room = await this.getRoomOrThrow(roomCode);
    if (room.started) {
      throw new Error("That room has already started.");
    }

    const preferredColor = normalizeColor(payload?.PreferredColor ?? payload?.preferredColor);
    let seatColor = preferredColor;
    if (!seatColor || !room.activeColors.includes(seatColor) || room.players.some((player) => player.color === seatColor)) {
      seatColor = room.activeColors.find((color) => !room.players.some((player) => player.color === color)) || null;
    }

    if (!seatColor) {
      throw new Error("That room is full.");
    }

    const player = {
      playerId: crypto.randomUUID(),
      displayName: normalizeName(payload?.PlayerName ?? payload?.playerName),
      color: seatColor,
      isHost: false,
      connected: true,
      socketId,
      lastSeenAt: Date.now(),
      disconnectDeadlineAt: 0,
    };

    room.players.push(player);
    room.roomSequence += 1;
    await this.persistRoom(room);
    return { room, player };
  }

  async recoverSession(payload, socketId) {
    const playerId = String(payload?.PlayerId ?? payload?.playerId ?? "").trim();
    if (!playerId) {
      throw new Error("Missing playerId for recovery.");
    }

    let roomCode = String(payload?.RoomCode ?? payload?.roomCode ?? "").trim().toUpperCase();
    if (!roomCode && this.store.enabled) {
      roomCode = String((await this.store.findRoomCodeForPlayer(playerId)) || "").trim().toUpperCase();
    }

    const room = await this.getRoomOrThrow(roomCode);
    const player = room.players.find((entry) => entry.playerId === playerId);
    if (!player) {
      throw new Error("That player session could not be restored.");
    }

    if (!player.connected && player.disconnectDeadlineAt > 0 && Date.now() > player.disconnectDeadlineAt) {
      throw new Error("That player session expired. Join the room again.");
    }

    player.connected = true;
    player.socketId = socketId;
    player.lastSeenAt = Date.now();
    player.disconnectDeadlineAt = 0;
    room.roomSequence += 1;
    await this.persistRoom(room);
    return { room, player };
  }

  async startGame(payload, socketId) {
    const { room, player } = await this.requirePlayer(payload, socketId);
    if (!player.isHost) {
      throw new Error("Only the host can start the game.");
    }

    const connectedColors = CLOCKWISE_COLORS.filter((color) =>
      room.activeColors.includes(color) && room.players.some((entry) => entry.color === color && entry.connected));

    if (connectedColors.length < 2) {
      throw new Error("At least 2 connected players are required to start.");
    }

    room.activeColors = connectedColors;
    room.playerCount = connectedColors.length;
    room.started = true;
    room.roomSequence += 1;
    room.stateVersion += 1;
    room.currentTurnColor = connectedColors[0];
    room.winnerColor = "";
    room.lastActivityAt = Date.now();
    await this.persistRoom(room);
    return { room, player };
  }

  async appendChat(payload, socketId) {
    const { room, player } = await this.requirePlayer(payload, socketId);
    const message = String(payload?.Message ?? payload?.message ?? "").trim();
    if (!message) {
      throw new Error("Chat messages cannot be empty.");
    }

    room.chatSequence += 1;
    room.lastActivityAt = Date.now();
    const chatMessage = {
      PlayerId: player.playerId,
      Sender: player.displayName,
      Message: message.slice(0, 180),
      Color: player.color,
      Sequence: room.chatSequence,
      SentAtUtc: new Date().toISOString(),
    };

    await this.persistRoom(room);
    return { room, player, message: chatMessage };
  }

  async playTurn(payload, socketId) {
    const { room, player } = await this.requirePlayer(payload, socketId);
    const action = playTurn(room, player, payload);
    await this.persistRoom(room);
    return { room, player, action };
  }

  async leaveRoom(payload, socketId) {
    const roomCode = String(payload?.RoomCode ?? payload?.roomCode ?? "").trim().toUpperCase();
    const playerId = String(payload?.PlayerId ?? payload?.playerId ?? "").trim();
    const room = await this.getRoom(roomCode);
    if (!room) {
      return null;
    }

    const playerIndex = room.players.findIndex((entry) => entry.playerId === playerId || entry.socketId === socketId);
    if (playerIndex < 0) {
      return null;
    }

    const [player] = room.players.splice(playerIndex, 1);
    if (this.store.enabled) {
      await this.store.deletePlayerMapping(player.playerId);
    }

    if (room.players.length === 0) {
      await this.deleteRoom(room);
      return { deleted: true, roomCode, player };
    }

    if (!room.players.some((entry) => entry.isHost)) {
      room.players[0].isHost = true;
    }

    if (!room.started) {
      room.roomSequence += 1;
    } else {
      room.roomSequence += 1;
      room.activeColors = room.activeColors.filter((color) => room.players.some((entry) => entry.color === color));
      room.playerCount = room.activeColors.length;
      if (room.currentTurnColor === player.color) {
        room.currentTurnColor = room.activeColors[0] || "";
      }
    }

    room.lastActivityAt = Date.now();
    await this.persistRoom(room);
    return { room, player, deleted: false };
  }

  async markDisconnected(socketId) {
    if (!socketId) {
      return null;
    }

    const room = this.findRoomBySocketId(socketId);
    if (!room) {
      return null;
    }

    const player = room.players.find((entry) => entry.socketId === socketId);
    if (!player || !player.connected) {
      return null;
    }

    player.connected = false;
    player.socketId = "";
    player.lastSeenAt = Date.now();
    player.disconnectDeadlineAt = Date.now() + DISCONNECT_GRACE_MS;
    room.roomSequence += 1;
    room.lastActivityAt = Date.now();
    await this.persistRoom(room);
    return { room, player };
  }

  async cleanupInactiveRooms(expireBefore) {
    const deadline = Number(expireBefore || 0);
    const deletions = [];
    for (const room of this.rooms.values()) {
      if (room.lastActivityAt <= deadline) {
        deletions.push(this.deleteRoom(room));
      }
    }

    await Promise.all(deletions);
  }

  async getRoom(roomCode) {
    const normalized = String(roomCode || "").trim().toUpperCase();
    if (!normalized) {
      return null;
    }

    if (this.rooms.has(normalized)) {
      return this.rooms.get(normalized);
    }

    if (!this.store.enabled) {
      return null;
    }

    const restoredRoom = await this.store.loadRoom(normalized);
    if (!restoredRoom) {
      return null;
    }

    this.rooms.set(normalized, restoredRoom);
    return restoredRoom;
  }

  async getRoomOrThrow(roomCode) {
    const room = await this.getRoom(roomCode);
    if (!room) {
      throw new Error("Room not found.");
    }

    return room;
  }

  async requirePlayer(payload, socketId = "") {
    const roomCode = String(payload?.RoomCode ?? payload?.roomCode ?? "").trim().toUpperCase();
    const playerId = String(payload?.PlayerId ?? payload?.playerId ?? "").trim();
    if (!roomCode || !playerId) {
      throw new Error("roomCode and playerId are required.");
    }

    const room = await this.getRoomOrThrow(roomCode);
    const player = room.players.find((entry) => entry.playerId === playerId);
    if (!player) {
      throw new Error("That player is not part of the room.");
    }

    if (!player.connected) {
      throw new Error("That player is currently disconnected.");
    }

    if (socketId && player.socketId && player.socketId !== socketId) {
      throw new Error("That player session is active on another connection.");
    }

    player.lastSeenAt = Date.now();
    player.disconnectDeadlineAt = 0;
    room.lastActivityAt = Date.now();
    return { room, player };
  }

  findRoomBySocketId(socketId) {
    for (const room of this.rooms.values()) {
      if (room.players.some((player) => player.socketId === socketId)) {
        return room;
      }
    }

    return null;
  }

  async persistRoom(room) {
    room.lastActivityAt = Date.now();
    this.rooms.set(room.roomCode, room);
    await this.store.saveRoom(room);
  }

  async deleteRoom(room) {
    if (!room) {
      return;
    }

    this.rooms.delete(room.roomCode);
    await this.store.deleteRoom(room.roomCode, room.players.map((player) => player.playerId));
  }

  async generateRoomCode() {
    const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    for (let attempt = 0; attempt < 24; attempt += 1) {
      let code = "";
      for (let index = 0; index < 6; index += 1) {
        code += alphabet[Math.floor(Math.random() * alphabet.length)];
      }

      if (!this.rooms.has(code)) {
        const existing = this.store.enabled ? await this.store.loadRoom(code) : null;
        if (!existing) {
          return code;
        }
      }
    }

    return `RM${Date.now().toString().slice(-4)}`;
  }
}

module.exports = {
  RoomManager,
};
