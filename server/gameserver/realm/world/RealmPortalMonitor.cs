﻿#region

using LoESoft.GameServer.realm.entity;
using LoESoft.GameServer.realm.terrain;
using LoESoft.GameServer.realm.world;
using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace LoESoft.GameServer.realm
{
    public class RealmPortalMonitor
    {
        private readonly Nexus nexus;
        private readonly Random rand = new Random();
        private readonly object worldLock = new object();
        public Dictionary<World, Portal> portals = new Dictionary<World, Portal>();

        public RealmPortalMonitor()
        {
            nexus = GameServer.Manager.Worlds[(int)WorldID.NEXUS_ID] as Nexus;

            lock (worldLock)
                foreach (var i in GameServer.Manager.Worlds)
                    if (i.Value is GameWorld)
                        WorldAdded(i.Value);
        }

        private Position GetRandPosition()
        {
            int x, y;

            do
            {
                x = rand.Next(0, nexus.Map.Width);
                y = rand.Next(0, nexus.Map.Height);
            } while (
                portals.Values.Any(_ => _.X == x && _.Y == y) ||
                nexus.Map[x, y].Region != TileRegion.Realm_Portals);

            return new Position { X = x, Y = y };
        }

        public bool AddPortal(int worldId, World world, Portal portal = null, Position? position = null, bool announce = true)
        {
            if (announce)
                foreach (var w in GameServer.Manager.Worlds.Values)
                    foreach (var p in w.Players.Values)
                        p.SendInfo(
                            $"A portal to {(w == world ? "this land" : world.GetDisplayName())} has opened up{(w is Nexus ? "" : " in Nexus")}.");
            return true;
        }

        public void WorldAdded(World world)
        {
            lock (worldLock)
            {
                var pos = GetRandPosition();
                var portal = new Portal(0x0712, null)
                {
                    Size = 80,
                    WorldInstance = world,
                    Name = world.Name
                };
                portal.Move(pos.X + 0.5f, pos.Y + 0.5f);
                nexus.EnterWorld(portal);
                portals.Add(world, portal);
            }
        }

        public void WorldRemoved(World world)
        {
            lock (worldLock)
            {
                if (portals.ContainsKey(world))
                {
                    var portal = portals[world];
                    nexus.LeaveWorld(portal);
                    RealmManager.Realms.Add(portal.PortalName);
                    RealmManager.CurrentRealmNames.Remove(portal.PortalName);
                    portals.Remove(world);
                }
            }
        }

        public void WorldClosed(World world)
        {
            lock (worldLock)
            {
                var portal = portals[world];
                nexus.LeaveWorld(portal);
                portals.Remove(world);
            }
        }

        public void WorldOpened(World world)
        {
            lock (worldLock)
            {
                var pos = GetRandPosition();
                var portal = new Portal(0x71c, null)
                {
                    Size = 150,
                    WorldInstance = world,
                    Name = world.Name
                };
                portal.Move(pos.X, pos.Y);
                nexus.EnterWorld(portal);
                portals.Add(world, portal);
            }
        }

        public World GetRandomRealm()
        {
            lock (worldLock)
            {
                var worlds = portals.Keys.ToArray();

                if (worlds.Length == 0)
                    return GameServer.Manager.Worlds[(int)WorldID.NEXUS_ID];

                return worlds[Environment.TickCount % worlds.Length];
            }
        }
    }
}