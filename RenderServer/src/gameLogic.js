const {
  CLOCKWISE_COLORS,
  TOKENS_PER_COLOR,
  COMMON_PATH_LENGTH,
  FINAL_PROGRESS,
  START_PATH_INDEX_BY_COLOR,
  SAFE_PATH_INDEXES,
} = require("./constants");

function normalizeColor(value) {
  const normalized = String(value || "").trim().toLowerCase();
  return CLOCKWISE_COLORS.find((color) => color.toLowerCase() === normalized) || null;
}

function normalizeName(value) {
  const trimmed = String(value || "").trim();
  if (!trimmed) {
    return "Player";
  }

  return trimmed.slice(0, 18);
}

function createInitialTokenStates() {
  const tokenStates = {};
  for (const color of CLOCKWISE_COLORS) {
    tokenStates[color] = new Array(TOKENS_PER_COLOR).fill(-1);
  }

  return tokenStates;
}

function buildSeatState(player) {
  return {
    PlayerId: player.playerId,
    Color: player.color,
    DisplayName: player.displayName,
    IsHost: !!player.isHost,
    Connected: !!player.connected,
  };
}

function buildSnapshot(room) {
  return {
    RoomCode: room.roomCode,
    RoomId: room.roomId,
    PlayerCount: room.started ? room.activeColors.length : room.playerCount,
    ActiveColors: room.activeColors.slice(),
    Seats: room.activeColors.map((color) => {
      const seat = room.players.find((player) => player.color === color);
      return seat ? buildSeatState(seat) : { Color: color, DisplayName: "", IsHost: false, Connected: false };
    }),
    Started: !!room.started,
    RoomSequence: room.roomSequence,
    StateVersion: room.stateVersion,
    CurrentTurnColor: room.currentTurnColor || "",
    WinnerColor: room.winnerColor || "",
    TokenStates: room.activeColors.map((color) => ({
      Color: color,
      Progress: (room.tokenStates[color] || []).slice(),
    })),
  };
}

function getMovableTokenIndexes(room, color, roll) {
  const progressList = room.tokenStates[color] || [];
  const movable = [];
  for (let tokenIndex = 0; tokenIndex < progressList.length; tokenIndex += 1) {
    if (canMove(progressList[tokenIndex], roll)) {
      movable.push(tokenIndex);
    }
  }

  return movable;
}

function canMove(progress, roll) {
  if (progress >= FINAL_PROGRESS) {
    return false;
  }

  if (progress < 0) {
    return roll === 6;
  }

  return progress + roll <= FINAL_PROGRESS;
}

function getBoardIndex(color, progress) {
  if (progress < 0 || progress >= COMMON_PATH_LENGTH) {
    return null;
  }

  return (START_PATH_INDEX_BY_COLOR[color] + progress) % COMMON_PATH_LENGTH;
}

function isSafeLanding(color, progress) {
  const boardIndex = getBoardIndex(color, progress);
  return boardIndex !== null && SAFE_PATH_INDEXES.has(boardIndex);
}

function findCapturedToken(room, attackerColor, landingProgress) {
  if (landingProgress < 0 || landingProgress >= COMMON_PATH_LENGTH || isSafeLanding(attackerColor, landingProgress)) {
    return null;
  }

  const landingIndex = getBoardIndex(attackerColor, landingProgress);
  for (const color of room.activeColors) {
    if (color === attackerColor) {
      continue;
    }

    const progressList = room.tokenStates[color] || [];
    for (let tokenIndex = 0; tokenIndex < progressList.length; tokenIndex += 1) {
      const progress = progressList[tokenIndex];
      if (progress < 0 || progress >= COMMON_PATH_LENGTH) {
        continue;
      }

      if (getBoardIndex(color, progress) === landingIndex) {
        return { color, tokenIndex };
      }
    }
  }

  return null;
}

function getNextTurnColor(room, currentColor) {
  if (!room.activeColors.length) {
    return "";
  }

  const currentIndex = room.activeColors.indexOf(currentColor);
  const nextIndex = currentIndex >= 0 ? (currentIndex + 1) % room.activeColors.length : 0;
  return room.activeColors[nextIndex];
}

function hasCompletedAllTokens(room, color) {
  const progressList = room.tokenStates[color] || [];
  return progressList.length > 0 && progressList.every((progress) => progress >= FINAL_PROGRESS);
}

function playTurn(room, player, payload) {
  if (!room.started) {
    throw new Error("The game has not started yet.");
  }

  if (room.winnerColor) {
    throw new Error("That match has already ended.");
  }

  if (!player || player.color !== room.currentTurnColor) {
    throw new Error("It is not your turn.");
  }

  const roll = Number(payload?.Roll ?? payload?.roll ?? 0);
  if (roll < 1 || roll > 6) {
    throw new Error("Roll must be between 1 and 6.");
  }

  const movableTokenIndexes = getMovableTokenIndexes(room, player.color, roll);
  const declaredNoMove = !!(payload?.NoMove ?? payload?.noMove);

  if (declaredNoMove) {
    if (movableTokenIndexes.length > 0) {
      throw new Error("A movable token exists for that roll.");
    }

    room.turnSequence += 1;
    room.stateVersion += 1;
    room.lastActivityAt = Date.now();
    room.currentTurnColor = getNextTurnColor(room, player.color);
    return {
      PlayerId: player.playerId,
      Color: player.color,
      Roll: roll,
      TokenIndex: -1,
      NoMove: true,
      Sequence: room.turnSequence,
      StateVersion: room.stateVersion,
      NextTurnColor: room.currentTurnColor,
      WinnerColor: "",
      BonusTurn: false,
      Captured: false,
      CapturedColor: "",
      CapturedTokenIndex: -1,
      Completed: false,
    };
  }

  if (movableTokenIndexes.length === 0) {
    throw new Error("No token can move for that roll.");
  }

  const tokenIndex = Number(payload?.TokenIndex ?? payload?.tokenIndex ?? -1);
  if (tokenIndex < 0 || !movableTokenIndexes.includes(tokenIndex)) {
    throw new Error("That token cannot move for the selected roll.");
  }

  room.turnSequence += 1;
  room.stateVersion += 1;
  room.lastActivityAt = Date.now();

  const progressList = room.tokenStates[player.color];
  const previousProgress = progressList[tokenIndex];
  const finalProgress = previousProgress < 0 ? 0 : previousProgress + roll;
  progressList[tokenIndex] = finalProgress;

  let captured = false;
  let capturedColor = "";
  let capturedTokenIndex = -1;
  const capturedToken = findCapturedToken(room, player.color, finalProgress);
  if (capturedToken) {
    room.tokenStates[capturedToken.color][capturedToken.tokenIndex] = -1;
    captured = true;
    capturedColor = capturedToken.color;
    capturedTokenIndex = capturedToken.tokenIndex;
  }

  const completed = previousProgress < FINAL_PROGRESS && finalProgress >= FINAL_PROGRESS;
  const winnerColor = hasCompletedAllTokens(room, player.color) ? player.color : "";
  room.winnerColor = winnerColor;

  const bonusTurn = !winnerColor && (roll === 6 || captured);
  room.currentTurnColor = winnerColor ? "" : (bonusTurn ? player.color : getNextTurnColor(room, player.color));

  return {
    PlayerId: player.playerId,
    Color: player.color,
    Roll: roll,
    TokenIndex: tokenIndex,
    NoMove: false,
    Sequence: room.turnSequence,
    StateVersion: room.stateVersion,
    NextTurnColor: room.currentTurnColor,
    WinnerColor: winnerColor,
    BonusTurn: bonusTurn,
    Captured: captured,
    CapturedColor: capturedColor,
    CapturedTokenIndex: capturedTokenIndex,
    Completed: completed,
  };
}

module.exports = {
  normalizeColor,
  normalizeName,
  createInitialTokenStates,
  buildSeatState,
  buildSnapshot,
  getMovableTokenIndexes,
  playTurn,
};
