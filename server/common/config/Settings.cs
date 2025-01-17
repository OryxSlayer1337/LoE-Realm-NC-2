﻿using System;
using System.Collections.Generic;

namespace LoESoft.Core.config
{
    public partial class Settings
    {
        public enum ServerMode
        {
            Local,
            Production
        }

        public static readonly double EVENT_RATE = 6;
        public static readonly DateTime EVENT_OVER = new DateTime(2019, 10, 20, 23, 59, 59);

        public static readonly string EVENT_MESSAGE = $"[Server Time: {DateTime.Now.ToString("MM/dd/yyyy hh:mm tt")}] The server is hosting an event with " +
            $"+{(GetEventRate() - (GetEventRate() != 1 ? 1 : 0)) * 100}% EXP, stats EXP and loot drop rate. Enjoy it until " +
            $"{EVENT_OVER.ToString("MM/dd/yyyy hh:mm tt")} UTC!";

        public static readonly ServerMode SERVER_MODE = ServerMode.Production;
        public static readonly bool ENABLE_RESTART_SYSTEM = SERVER_MODE == ServerMode.Production;
        public static readonly int RESTART_DELAY_MINUTES = 60;
        public static readonly int RESTART_APPENGINE_DELAY_MINUTES = 120;
        public static readonly DateTimeKind DateTimeKind = DateTimeKind.Utc;

        public static double GetEventRate() => DateTime.UtcNow > EVENT_OVER ? 1 : EVENT_RATE;

        public static class STARTUP
        {
            public static readonly int GOLD = 40;
            public static readonly int FAME = 0;
            public static readonly int TOTAL_FAME = 0;
            public static readonly int TOKENS = 0;
            public static readonly int EMPIRES_COIN = 0;
            public static readonly int MAX_CHAR_SLOTS = 2;
            public static readonly int IS_AGE_VERIFIED = 1;
            public static readonly bool VERIFIED = true;
        }

        public static readonly List<GameVersion> GAME_VERSIONS = new List<GameVersion>
        {
            new GameVersion(Version: "3.2.12", Allowed: true)
        };
    }
}