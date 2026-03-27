using System;
using System.Collections.Generic;
using UnityEngine;

namespace schrader
{
    public enum TeamResult
    {
        Unknown = 0,
        Red = 1,
        Blue = 2
    }

    public class RankedParticipant
    {
        public ulong clientId;
        public string playerId;
        public string displayName;
        public TeamResult team;
        public bool isDummy;
    }

    public class MmrEntry
    {
        public int mmr = 350;
        public int wins = 0;
        public int losses = 0;
        public string lastUpdated = null;
    }

    public class MmrFile
    {
        public int version = 1;
        public Dictionary<string, MmrEntry> players = new Dictionary<string, MmrEntry>();
    }
}
