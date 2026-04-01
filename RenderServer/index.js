const http = require("http");
const express = require("express");
const cors = require("cors");
const { createRedisStore } = require("./src/redisStore");
const { RoomManager } = require("./src/roomManager");
const { registerSocketHandlers } = require("./src/socketHandlers");
const { ROOM_TTL_MS } = require("./src/constants");

async function bootstrap() {
  const app = express();
  const port = Number(process.env.PORT || 10000);

  app.use(cors());
  app.use(express.json({ limit: "256kb" }));

  app.get("/", (_req, res) => {
    res.json({ ok: true, service: "premium-ludo-socket-server" });
  });

  app.get("/api/ludo", (_req, res) => {
    res.json({ ok: true, service: "premium-ludo-socket-server", transport: "socket.io" });
  });

  const server = http.createServer(app);
  const { Server } = require("socket.io");
  const io = new Server(server, {
    cors: {
      origin: "*",
      methods: ["GET", "POST"],
    },
    transports: ["websocket", "polling"],
  });

  const redisStore = await createRedisStore(io);
  const roomManager = new RoomManager(redisStore);
  registerSocketHandlers(io, roomManager);

  const cleanupHandle = setInterval(() => {
    roomManager.cleanupInactiveRooms(Date.now() - ROOM_TTL_MS).catch((error) => {
      console.error("Failed to clean inactive rooms:", error);
    });
  }, 10 * 60 * 1000);

  cleanupHandle.unref();

  server.listen(port, () => {
    console.log(`Premium Ludo Socket server listening on port ${port}`);
  });

  async function shutdown() {
    clearInterval(cleanupHandle);
    io.close();
    server.close();
    await redisStore.close();
  }

  process.on("SIGINT", () => {
    shutdown().finally(() => process.exit(0));
  });

  process.on("SIGTERM", () => {
    shutdown().finally(() => process.exit(0));
  });
}

bootstrap().catch((error) => {
  console.error("Failed to bootstrap Premium Ludo server:", error);
  process.exit(1);
});
