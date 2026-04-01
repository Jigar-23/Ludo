mergeInto(LibraryManager.library, {
  LudoSocketIoBridge_Connect: function (objectNamePtr, serverBaseUrlPtr, allowReconnect) {
    var bridge = window.PremiumLudoSocketBridge;
    if (!bridge) {
      bridge = window.PremiumLudoSocketBridge = createPremiumLudoSocketBridge();
    }

    bridge.connect(UTF8ToString(objectNamePtr), UTF8ToString(serverBaseUrlPtr), !!allowReconnect);
  },

  LudoSocketIoBridge_Disconnect: function () {
    var bridge = window.PremiumLudoSocketBridge;
    if (bridge) {
      bridge.disconnect();
    }
  },

  LudoSocketIoBridge_EmitJson: function (eventNamePtr, payloadJsonPtr) {
    var bridge = window.PremiumLudoSocketBridge;
    if (!bridge) {
      return;
    }

    bridge.emitJson(UTF8ToString(eventNamePtr), UTF8ToString(payloadJsonPtr));
  }
});

function createPremiumLudoSocketBridge() {
  return {
    objectName: "",
    serverBaseUrl: "",
    allowReconnect: true,
    socket: null,
    scriptPromise: null,

    notify: function (methodName, payload) {
      if (!this.objectName || typeof SendMessage !== "function") {
        return;
      }

      SendMessage(this.objectName, methodName, payload || "");
    },

    ensureLibrary: function () {
      var self = this;
      if (typeof window.io === "function") {
        return Promise.resolve();
      }

      if (self.scriptPromise) {
        return self.scriptPromise;
      }

      self.scriptPromise = new Promise(function (resolve, reject) {
        var existing = document.querySelector("script[data-premium-ludo-socketio]");
        if (existing) {
          existing.addEventListener("load", function () { resolve(); }, { once: true });
          existing.addEventListener("error", function () { reject(new Error("Socket.IO library failed to load.")); }, { once: true });
          return;
        }

        var script = document.createElement("script");
        script.src = "https://cdn.socket.io/4.8.1/socket.io.min.js";
        script.async = true;
        script.dataset.premiumLudoSocketio = "true";
        script.onload = function () { resolve(); };
        script.onerror = function () { reject(new Error("Socket.IO library failed to load.")); };
        document.head.appendChild(script);
      });

      return self.scriptPromise;
    },

    cleanupSocket: function () {
      if (!this.socket) {
        return;
      }

      try {
        this.socket.removeAllListeners();
        this.socket.disconnect();
      } catch (_error) {
      }

      this.socket = null;
    },

    connect: function (objectName, serverBaseUrl, allowReconnect) {
      var self = this;
      self.objectName = objectName || "";
      self.serverBaseUrl = serverBaseUrl || "";
      self.allowReconnect = !!allowReconnect;

      self.ensureLibrary()
        .then(function () {
          self.cleanupSocket();

          var socket = window.io(self.serverBaseUrl, {
            transports: ["websocket"],
            autoConnect: true,
            forceNew: true,
            reconnection: self.allowReconnect,
            reconnectionAttempts: Infinity,
            reconnectionDelay: 1000,
            reconnectionDelayMax: 8000,
            timeout: 15000
          });

          self.socket = socket;

          socket.on("connect", function () {
            self.notify("OnSocketConnected", "");
          });

          socket.on("disconnect", function (reason) {
            self.notify("OnSocketDisconnected", String(reason || ""));
          });

          socket.on("connect_error", function (error) {
            self.notify("OnSocketError", error && error.message ? error.message : "Socket connection failed.");
          });

          socket.on("error", function (error) {
            self.notify("OnSocketError", error && error.message ? error.message : "Socket error.");
          });

          socket.onAny(function (eventName, payload) {
            if (!eventName) {
              return;
            }

            var envelope = JSON.stringify({
              EventName: String(eventName),
              PayloadJson: JSON.stringify(payload === undefined ? {} : payload)
            });
            self.notify("OnSocketEvent", envelope);
          });
        })
        .catch(function (error) {
          self.notify("OnSocketError", error && error.message ? error.message : "Socket.IO library failed to load.");
        });
    },

    disconnect: function () {
      this.cleanupSocket();
    },

    emitJson: function (eventName, payloadJson) {
      if (!this.socket || !eventName) {
        return;
      }

      var payload = {};
      if (payloadJson) {
        try {
          payload = JSON.parse(payloadJson);
        } catch (_error) {
          this.notify("OnSocketError", "Failed to encode multiplayer request.");
          return;
        }
      }

      this.socket.emit(String(eventName), payload);
    }
  };
}
