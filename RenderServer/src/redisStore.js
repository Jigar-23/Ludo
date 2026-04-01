const { createClient } = require("redis");
const { createAdapter } = require("@socket.io/redis-adapter");
const { ROOM_TTL_MS } = require("./constants");

function roomKey(roomCode) {
  return `ludo:room:${String(roomCode || "").toUpperCase()}`;
}

function playerKey(playerId) {
  return `ludo:player:${String(playerId || "")}`;
}

function createNoopStore() {
  return {
    enabled: false,
    async saveRoom() {},
    async loadRoom() {
      return null;
    },
    async findRoomCodeForPlayer() {
      return null;
    },
    async deletePlayerMapping() {},
    async deleteRoom() {},
    async close() {},
  };
}

async function createRedisStore(io) {
  const redisUrl = String(process.env.REDIS_URL || "").trim();
  if (!redisUrl) {
    return createNoopStore();
  }

  try {
    const publisher = createClient({ url: redisUrl });
    const subscriber = publisher.duplicate();
    const dataClient = publisher.duplicate();
    await Promise.all([publisher.connect(), subscriber.connect(), dataClient.connect()]);
    io.adapter(createAdapter(publisher, subscriber));

    return {
      enabled: true,
      async saveRoom(room) {
        if (!room || !room.roomCode) {
          return;
        }

        await dataClient.set(roomKey(room.roomCode), JSON.stringify(room), { PX: ROOM_TTL_MS });
        const players = Array.isArray(room.players) ? room.players : [];
        await Promise.all(players.map((player) => dataClient.set(playerKey(player.playerId), room.roomCode, { PX: ROOM_TTL_MS })));
      },
      async loadRoom(roomCode) {
        const raw = await dataClient.get(roomKey(roomCode));
        return raw ? JSON.parse(raw) : null;
      },
      async findRoomCodeForPlayer(playerId) {
        return dataClient.get(playerKey(playerId));
      },
      async deletePlayerMapping(playerId) {
        await dataClient.del(playerKey(playerId));
      },
      async deleteRoom(roomCode, playerIds = []) {
        const deletions = [dataClient.del(roomKey(roomCode))];
        for (const playerId of playerIds) {
          deletions.push(dataClient.del(playerKey(playerId)));
        }

        await Promise.all(deletions);
      },
      async close() {
        await Promise.allSettled([publisher.quit(), subscriber.quit(), dataClient.quit()]);
      },
    };
  }
  catch (error) {
    console.error("Redis initialization failed, falling back to in-memory only:", error);
    return createNoopStore();
  }
}

module.exports = {
  createRedisStore,
};
