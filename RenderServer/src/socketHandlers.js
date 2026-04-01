const { buildSnapshot, buildSeatState } = require("./gameLogic");

function emitError(socket, message, code = "server_error") {
  socket.emit("errorMessage", {
    Code: code,
    Error: String(message || "Unknown multiplayer error."),
  });
}

function guard(socket, handler) {
  return async (payload) => {
    try {
      await handler(payload || {});
    }
    catch (error) {
      console.error("Socket handler failed:", error);
      emitError(socket, error.message);
    }
  };
}

function buildSeatEvent(room, player, extras = {}) {
  return {
    Success: true,
    RoomCode: room.roomCode,
    RoomSequence: room.roomSequence,
    PlayerCount: room.started ? room.activeColors.length : room.playerCount,
    ActiveColors: room.activeColors.slice(),
    Seat: player ? buildSeatState(player) : null,
    Color: player ? player.color : "",
    Connected: !!(player && player.connected),
    Snapshot: buildSnapshot(room),
    ...extras,
  };
}

function registerSocketHandlers(io, roomManager) {
  io.on("connection", (socket) => {
    socket.on("createRoom", guard(socket, async (payload) => {
      const { room, player } = await roomManager.createRoom(payload, socket.id);
      await socket.join(room.roomCode);

      socket.emit("roomCreated", {
        Success: true,
        PlayerId: player.playerId,
        AssignedColor: player.color,
        Snapshot: buildSnapshot(room),
      });

      io.to(room.roomCode).emit("playerJoined", buildSeatEvent(room, player, {
        PlayerId: player.playerId,
        AssignedColor: player.color,
      }));
    }));

    socket.on("joinRoom", guard(socket, async (payload) => {
      const { room, player } = await roomManager.joinRoom(payload, socket.id);
      await socket.join(room.roomCode);
      io.to(room.roomCode).emit("playerJoined", buildSeatEvent(room, player, {
        PlayerId: player.playerId,
        AssignedColor: player.color,
      }));
    }));

    socket.on("recoverSession", guard(socket, async (payload) => {
      const { room, player } = await roomManager.recoverSession(payload, socket.id);
      await socket.join(room.roomCode);
      socket.emit("gameStateUpdate", {
        Snapshot: buildSnapshot(room),
      });
      socket.emit("playerJoined", buildSeatEvent(room, player, {
        PlayerId: player.playerId,
        AssignedColor: player.color,
      }));
      socket.to(room.roomCode).emit("playerJoined", buildSeatEvent(room, player));
    }));

    socket.on("startGame", guard(socket, async (payload) => {
      const { room } = await roomManager.startGame(payload, socket.id);
      io.to(room.roomCode).emit("gameStarted", {
        Success: true,
        Snapshot: buildSnapshot(room),
      });
    }));

    socket.on("sendChat", guard(socket, async (payload) => {
      const { room, message } = await roomManager.appendChat(payload, socket.id);
      io.to(room.roomCode).emit("chatMessage", {
        Message: message,
      });
    }));

    socket.on("playTurn", guard(socket, async (payload) => {
      const { room, action } = await roomManager.playTurn(payload, socket.id);
      io.to(room.roomCode).emit("turnPlayed", {
        Action: action,
      });
    }));

    socket.on("leaveRoom", guard(socket, async (payload) => {
      const result = await roomManager.leaveRoom(payload, socket.id);
      if (!result || result.deleted || !result.room) {
        return;
      }

      await socket.leave(result.room.roomCode);
      io.to(result.room.roomCode).emit("playerLeft", buildSeatEvent(result.room, result.player));
    }));

    socket.on("disconnect", () => {
      roomManager.markDisconnected(socket.id)
        .then((result) => {
          if (!result || !result.room) {
            return;
          }

          socket.to(result.room.roomCode).emit("playerLeft", buildSeatEvent(result.room, result.player));
        })
        .catch((error) => {
          console.error("Disconnect handling failed:", error);
        });
    });
  });
}

module.exports = {
  registerSocketHandlers,
};
