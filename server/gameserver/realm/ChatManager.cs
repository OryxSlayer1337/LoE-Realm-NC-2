﻿#region

using LoESoft.GameServer.networking.outgoing;
using LoESoft.GameServer.realm.entity.player;
using System;
using System.Collections.Generic;

#endregion

namespace LoESoft.GameServer.realm
{
    public class ChatManager
    {
        private struct Message
        {
            public char Type;
            public string Inst;

#pragma warning disable CS0649 // Field 'ChatManager.Message.ObjId' is never assigned to, and will always have its default value 0
            public int ObjId;
#pragma warning restore CS0649 // Field 'ChatManager.Message.ObjId' is never assigned to, and will always have its default value 0
#pragma warning disable CS0649 // Field 'ChatManager.Message.Stars' is never assigned to, and will always have its default value 0
            public int Stars;
#pragma warning restore CS0649 // Field 'ChatManager.Message.Stars' is never assigned to, and will always have its default value 0
#pragma warning disable CS0649 // Field 'ChatManager.Message.From' is never assigned to, and will always have its default value null
            public string From;
#pragma warning restore CS0649 // Field 'ChatManager.Message.From' is never assigned to, and will always have its default value null

#pragma warning disable CS0649 // Field 'ChatManager.Message.To' is never assigned to, and will always have its default value null
            public string To;
#pragma warning restore CS0649 // Field 'ChatManager.Message.To' is never assigned to, and will always have its default value null
            public string Text;
        }

        private RealmManager manager;

        public ChatManager(RealmManager manager)
        {
            this.manager = manager;
        }

        public static Dictionary<string, Tuple<DateTime, string>> ChatDataCache = new Dictionary<string, Tuple<DateTime, string>>(); // store only latest player message

        public void Say(Player player, string chatText)
        {
            if (!player.NameChosen)
                return;

            if (!ChatDataCache.ContainsKey(player.Name))
                ChatDataCache.Add(player.Name, Tuple.Create(DateTime.Now, chatText));
            else
                ChatDataCache[player.Name] = Tuple.Create(DateTime.Now, chatText);

            var color = new ChatColor(player.Stars, player.AccountType);

            player.Owner.BroadcastMessage(new TEXT
            {
                Name = player.Name,
                ObjectId = player.Id,
                Stars = player.Stars,
                Admin = player.Client.Account.Admin ? 1 : 0,
                BubbleTime = 5,
                Recipient = "",
                Text = chatText,
                CleanText = chatText,
                NameColor = color.GetColor(),
                TextColor = 0x123456
            }, null);
        }

        public void Oryx(World world, string text)
        {
            TEXT _text = new TEXT
            {
                Name = "#Oryx the Mad God",
                Text = text,
                BubbleTime = 0,
                Stars = -1,
                NameColor = 0x123456,
                TextColor = 0x123456
            };

            world.BroadcastMessage(_text, null);
        }

        public void Guild(Player player, string text, bool announce = false)
        {
            if (announce)
            {
                foreach (ClientData cData in GameServer.Manager.ClientManager.Values)
                    if (cData.Client != null)
                        cData.Client.Player.SendInfo(text.ToSafeText());
            }
            else
            {
                TEXT _text = new TEXT
                {
                    BubbleTime = 10,
                    CleanText = "",
                    Name = player.Name,
                    ObjectId = player.Id,
                    Recipient = "*Guild*",
                    Stars = player.Stars,
                    NameColor = 0x123456,
                    TextColor = 0x123456,
                    Text = text.ToSafeText()
                };

                player.Client.SendMessage(_text);
            }
        }
    }
}