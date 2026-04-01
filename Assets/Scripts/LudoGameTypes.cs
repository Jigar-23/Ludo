using System;
using System.Collections.Generic;
using UnityEngine;

namespace PremiumLudo
{
    public enum LudoPlayerId
    {
        Human = 0,
        AI = 1,
    }

    public enum LudoTokenColor
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Yellow = 3,
    }

    public enum LudoGameMode
    {
        Local = 0,
        Computer = 1,
        Online = 2,
    }

    public enum LudoParticipantControl
    {
        HumanLocal = 0,
        AI = 1,
        Remote = 2,
    }

    public enum LudoOnlineEntryMode
    {
        CreateAndJoin = 0,
        Join = 1,
    }

    public enum LudoTurnPhase
    {
        Booting = 0,
        AwaitingHumanRoll = 1,
        AwaitingHumanMove = 2,
        AwaitingAI = 3,
        AwaitingRemote = 4,
        WaitingForRoom = 5,
        Resolving = 6,
        GameOver = 7,
    }

    [Serializable]
    public sealed class LudoParticipantConfig
    {
        public LudoTokenColor Color;
        public LudoParticipantControl Control;
        public string DisplayName;
        public bool IsLocal;

        public LudoParticipantConfig Clone()
        {
            return new LudoParticipantConfig
            {
                Color = Color,
                Control = Control,
                DisplayName = DisplayName,
                IsLocal = IsLocal,
            };
        }
    }

    [Serializable]
    public sealed class LudoSessionConfig
    {
        public LudoGameMode Mode;
        public string LocalPlayerName = "Player";
        public string RoomCode = string.Empty;
        public string RoomId = string.Empty;
        public bool IsHost;
        public readonly List<LudoParticipantConfig> Participants = new List<LudoParticipantConfig>(4);

        public int PlayerCount
        {
            get { return Participants.Count; }
        }

        public bool UsesNetwork
        {
            get { return Mode == LudoGameMode.Online; }
        }

        public LudoParticipantConfig GetParticipant(LudoTokenColor color)
        {
            for (int i = 0; i < Participants.Count; i++)
            {
                if (Participants[i] != null && Participants[i].Color == color)
                {
                    return Participants[i];
                }
            }

            return null;
        }

        public bool IsColorActive(LudoTokenColor color)
        {
            return GetParticipant(color) != null;
        }

        public LudoParticipantConfig GetLocalParticipant()
        {
            for (int i = 0; i < Participants.Count; i++)
            {
                if (Participants[i] != null && Participants[i].IsLocal)
                {
                    return Participants[i];
                }
            }

            return null;
        }

        public List<LudoTokenColor> BuildTurnOrder()
        {
            List<LudoTokenColor> turnOrder = new List<LudoTokenColor>(Participants.Count);
            IReadOnlyList<LudoTokenColor> clockwiseColors = LudoBoardGeometry.ClockwiseColors;
            for (int i = 0; i < clockwiseColors.Count; i++)
            {
                if (IsColorActive(clockwiseColors[i]))
                {
                    turnOrder.Add(clockwiseColors[i]);
                }
            }

            return turnOrder;
        }

        public LudoSessionConfig Clone()
        {
            LudoSessionConfig clone = new LudoSessionConfig
            {
                Mode = Mode,
                LocalPlayerName = LocalPlayerName,
                RoomCode = RoomCode,
                RoomId = RoomId,
                IsHost = IsHost,
            };

            for (int i = 0; i < Participants.Count; i++)
            {
                if (Participants[i] != null)
                {
                    clone.Participants.Add(Participants[i].Clone());
                }
            }

            return clone;
        }
    }

    public sealed class LudoTokenState
    {
        public LudoTokenColor Owner;
        public int TokenIndex;
        public int Progress = -1;

        public bool IsHome
        {
            get { return Progress < 0; }
        }

        public bool HasFinished
        {
            get { return Progress >= LudoBoardGeometry.FinalProgress; }
        }

        public bool IsOnCommonPath
        {
            get { return Progress >= 0 && Progress < LudoBoardGeometry.CommonPathLength; }
        }

        public Vector2Int GetBoardCoordinate()
        {
            if (IsHome)
            {
                return LudoBoardGeometry.GetHomeCoordinate(Owner);
            }

            if (HasFinished)
            {
                return LudoBoardGeometry.GoalCoordinate;
            }

            return LudoBoardGeometry.GetRouteCoordinate(Owner, Progress);
        }
    }

    [Serializable]
    public sealed class LudoChatMessage
    {
        public string Sender;
        public string Message;
        public string Color;
        public long Sequence;
        public string SentAtUtc;
    }

    [Serializable]
    public sealed class LudoTurnActionMessage
    {
        public string Color;
        public int Roll;
        public int TokenIndex;
        public bool NoMove;
        public long Sequence;
    }

    [Serializable]
    public sealed class LudoOnlineSeatState
    {
        public string Color;
        public string DisplayName;
        public bool IsHost;
        public bool Connected;
    }

    [Serializable]
    public sealed class LudoRoomSnapshot
    {
        public string RoomCode;
        public int PlayerCount;
        public string[] ActiveColors;
        public LudoOnlineSeatState[] Seats;
        public bool Started;
        public long RoomSequence;
    }

    [Serializable]
    public sealed class LudoRoomOperationResponse
    {
        public bool Success;
        public string Error;
        public string AssignedColor;
        public LudoRoomSnapshot Snapshot;
    }

    [Serializable]
    public sealed class LudoRoomPollResponse
    {
        public bool Success;
        public string Error;
        public LudoRoomSnapshot Snapshot;
        public LudoChatMessage[] ChatMessages;
        public LudoTurnActionMessage[] TurnActions;
    }
}
