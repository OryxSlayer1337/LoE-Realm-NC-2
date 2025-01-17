﻿#region

using LoESoft.Core.config;
using LoESoft.Core.database;
using LoESoft.GameServer.logic;
using LoESoft.GameServer.logic.skills.Pets;
using LoESoft.GameServer.networking;
using LoESoft.GameServer.networking.incoming;
using LoESoft.GameServer.networking.outgoing;
using LoESoft.GameServer.realm.terrain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static LoESoft.GameServer.networking.Client;

#endregion

namespace LoESoft.GameServer.realm.entity.player
{
    internal interface IPlayer
    {
        void Damage(int dmg, Entity chr, bool NoDef, bool manaDrain = false);

        bool IsVisibleToEnemy();
    }

    public static class ComparableExtension
    {
        public static bool InRange<T>(this T value, T from, T to) where T : IComparable<T> => value.CompareTo(from) >= 1 && value.CompareTo(to) <= -1;
    }

    public partial class Player : Character, IContainer, IPlayer
    {
        public Player(Client client) : base(client.Character.ObjectType, client.Random)
        {
            try
            {
                if (client.Account.Admin == true)
                    Admin = 1;
                Achievements = new List<string>();
                ActualTask = null;
                MonsterCaches = new List<MonsterCache>();
                Task = null;
                AccountType = client.Account.AccountType;
                AccountPerks = new AccountTypePerks(AccountType);
                AccountLifetime = client.Account.AccountLifetime;
                IsVip = AccountLifetime != DateTime.MinValue;
                Client = client;
                StatsManager = new StatsManager(this, client.Random.CurrentSeed);
                Name = client.Account.Name;
                AccountId = client.Account.AccountId;
                FameCounter = new FameCounter(this);
                Tokens = client.Account.FortuneTokens;
                HpPotionPrice = 5;
                MpPotionPrice = 5;
                Level = client.Character.Level == 0 ? 1 : client.Character.Level;
                Experience = client.Character.Experience;
                ExperienceGoal = GetExperience(Level + 1, ExpType.Level);
                FakeExperience = client.Character.FakeExperience;
                IsFakeEnabled = client.Character.IsFakeEnabled;
                AttackLevel = client.Character.AttackLevel;

                if (AttackLevel < 10)
                    AttackLevel += 10;

                AttackExperience = client.Character.AttackExperience;
                AttackGoalExperience = GetExperience(AttackLevel + 1 - 10, ExpType.Stat);
                DefenseLevel = client.Character.DefenseLevel;

                if (DefenseLevel < 10)
                    DefenseLevel += 10;

                DefenseExperience = client.Character.DefenseExperience;
                DefenseGoalExperience = GetExperience(DefenseLevel + 1 - 10, ExpType.Stat);
                Bless1 = client.Character.Bless1;
                Bless2 = client.Character.Bless2;
                Bless3 = client.Character.Bless3;
                Bless4 = client.Character.Bless4;
                Bless5 = client.Character.Bless5;
                EnablePetAttack = client.Character.EnablePetAttack;
                Stars = AccountType >= (int)Core.config.AccountType.DEVELOPER ? 70 : GetStars();
                ChatColors = new ChatColor(this);
                Texture1 = client.Character.Tex1;
                Texture2 = client.Character.Tex2;
                Credits = client.Account.Credits;
                NameChosen = client.Account.NameChosen;
                CurrentFame = client.Account.Fame;
                Fame = client.Character.Fame;
                PetHealing = null;
                PetAttack = null;
                if (client.Character.Pet != 0)
                {
                    PetHealing = new List<List<int>>();
                    PetAttack = new List<int>();
                    PetID = client.Character.Pet;
                    var HPData = PetHPHealing.MinMaxBonus(Resolve((ushort)PetID).ObjectDesc.HPTier, Stars);
                    var MPData = PetMPHealing.MinMaxBonus(Resolve((ushort)PetID).ObjectDesc.MPTier, Stars);
                    PetHealing.Add(new List<int> { HPData.Item1, HPData.Item2, (int)((HPData.Item3 - 1) * 100) });
                    PetHealing.Add(new List<int> { MPData.Item1, MPData.Item2, (int)((MPData.Item3 - 1) * 100) });
                    PetAttack.Add(7750 - Stars * 100);
                    PetAttack.Add(30 + Stars);
                    PetAttack.Add(Resolve((ushort)PetID).ObjectDesc.Projectiles[0].MinDamage);
                    PetAttack.Add(Resolve((ushort)PetID).ObjectDesc.Projectiles[0].MaxDamage);
                }
                LootDropBoostTimeLeft = client.Character.LootDropTimer;
                lootDropBoostFreeTimer = LootDropBoost;
                LootTierBoostTimeLeft = client.Character.LootTierTimer;
                lootTierBoostFreeTimer = LootTierBoost;
                FameGoal = (AccountType >= (int)Core.config.AccountType.MOD) ? 0d : GetFameGoal(FameCounter.ClassStats[ObjectType].BestFame);
                Glowing = AccountType == (int)Core.config.AccountType.VIP;
                var guild = GameServer.Manager.Database.GetGuild(client.Account.GuildId);
                if (guild != null)
                {
                    Guild = GameServer.Manager.Database.GetGuild(client.Account.GuildId).Name;
                    GuildRank = client.Account.GuildRank;
                }
                else
                {
                    Guild = "";
                    GuildRank = -1;
                }
                HP = client.Character.HP <= 0 ? (int)ObjectDesc.MaxHP : client.Character.HP;
                MP = client.Character.MP;
                ConditionEffects = 0;
                OxygenBar = 100;
                HasBackpack = client.Character.HasBackpack == true;
                PlayerSkin = Client.Account.OwnedSkins.Contains(Client.Character.Skin) ? Client.Character.Skin : 0;
                HealthPotions = client.Character.HealthPotions < 0 ? 0 : client.Character.HealthPotions;
                MagicPotions = client.Character.MagicPotions < 0 ? 0 : client.Character.MagicPotions;

                try
                {
                    Locked = GameServer.Manager.Database.GetLockeds(client.Account);
                    Ignored = GameServer.Manager.Database.GetIgnoreds(client.Account);
                }
                catch (Exception) { }

                if (HasBackpack)
                {
                    Item[] inv =
                        client.Character.Items.Select(
                            _ =>
                                _ == -1
                                    ? null
                                    : (GameServer.Manager.GameData.Items.ContainsKey((ushort)_) ? GameServer.Manager.GameData.Items[(ushort)_] : null))
                            .ToArray();
                    Item[] backpack =
                        client.Character.Backpack.Select(
                            _ =>
                                _ == -1
                                    ? null
                                    : (GameServer.Manager.GameData.Items.ContainsKey((ushort)_) ? GameServer.Manager.GameData.Items[(ushort)_] : null))
                            .ToArray();

                    Inventory = inv.Concat(backpack).ToArray();
                    XElement xElement = GameServer.Manager.GameData.ObjectTypeToElement[ObjectType].Element("SlotTypes");
                    if (xElement != null)
                    {
                        int[] slotTypes =
                            Utils.FromCommaSepString32(
                                xElement.Value);
                        Array.Resize(ref slotTypes, 20);
                        SlotTypes = slotTypes;
                    }
                }
                else
                {
                    Inventory =
                            client.Character.Items.Select(
                                _ =>
                                    _ == -1
                                        ? null
                                        : (GameServer.Manager.GameData.Items.ContainsKey((ushort)_) ? GameServer.Manager.GameData.Items[(ushort)_] : null))
                                .ToArray();
                    XElement xElement = GameServer.Manager.GameData.ObjectTypeToElement[ObjectType].Element("SlotTypes");
                    if (xElement != null)
                        SlotTypes =
                            Utils.FromCommaSepString32(
                                xElement.Value);
                }

                var (hp, mp) = GetStats[GetClassType[ObjectType]];

                Stats = (int[])client.Character.Stats.Clone();
                Stats[0] = Level * hp + 100; // hp
                Stats[1] = Level * mp + 100; // mp
                Stats[2] = 0; // old attack
                Stats[3] = 0; // old defense
                Stats[4] = Level / 2 + 10; // spd
                Stats[5] = Level / 2 + 10; // vit
                Stats[6] = Level / 2 + 10; // wis
                Stats[7] = Level / 1 + 10; // dex

                for (var i = 0; i < SlotTypes.Length; i++)
                    if (SlotTypes[i] == 0)
                        SlotTypes[i] = 10;

                if (Client.Account.AccountType >= (int)Core.config.AccountType.DEVELOPER)
                    return;

                for (var i = 0; i < 4; i++)
                    if (Inventory[i]?.SlotType != SlotTypes[i])
                        Inventory[i] = null;
            }
            catch (Exception) { }
        }

        public override void Move(float x, float y)
        {
            if (Pet != null)
            {
                if (Dist(this, Pet) > 20f)
                {
                    Pet.Move(X, Y);
                    UpdateCount++;
                }
            }

            base.Move(x, y);
        }

        private void AnnounceDeath(string killer)
        {
            var playerDesc = GameServer.Manager.GameData.ObjectDescs[ObjectType];
            var notification = $"{Name} died at level {Level} to {killer} as {playerDesc.ObjectId.ToLower()} with {Fame} fame base.";

            if (Fame >= 2000 && !Client.Account.Admin)
                foreach (var client in GameServer.Manager.GetManager.Clients.Values)
                    client.Player?.GazerDM(notification);
            else
                foreach (var i in Owner.Players.Values)
                    i.GazerDM(notification);
        }

        public void Death(string killer, ObjectDesc desc = null, Entity entity = null, string displayid = null, string objectid = null)
        {
            if (dying)
                return;

            dying = true;

            if (Owner.Name == "Arena")
            {
                Client.SendMessage(new ARENA_DEATH
                {
                    RestartPrice = 100
                });

                HP = (int)ObjectDesc.MaxHP;

                ApplyConditionEffect(new ConditionEffect
                {
                    Effect = ConditionEffectIndex.Paused,
                    DurationMS = -1
                });
                return;
            }

            if (Client.State == ProtocolState.Disconnected)
                return;

            var blessings = CountBlessings();

            if (blessings == 5 || Level <= 30)
            {
                Bless1 = false;
                Bless2 = false;
                Bless3 = false;
                Bless4 = false;
                Bless5 = false;

                SaveToCharacter();

                var playerDesc = GameServer.Manager.GameData.ObjectDescs[ObjectType];
                var notification = $"({playerDesc.ObjectId} - Lvl: {Level}) {Name} escaped from death!";

                if (Fame >= 2000)
                    foreach (var client in GameServer.Manager.GetManager.Clients.Values.Where(c => c.Player != this))
                        client.Player?.GazerDM(notification);
                else
                    foreach (var i in Owner.Players.Values.Where(p => p != this))
                        i.GazerDM(notification);

                GameServer.Manager.TryDisconnect(Client, DisconnectReason.CHARACTER_IS_DEAD);
                return;
            }

            GenerateGravestone();

            if (desc != null)
                killer = desc.DisplayId ?? desc.ObjectId;
            else
                killer = displayid ?? objectid;

            AnnounceDeath(killer);

            var rng = new Random();
            var lvl = Level;
            var exp = Experience;
            var attlvl = AttackLevel - 10;
            var attexp = AttackExperience;
            var deflvl = DefenseLevel - 10;
            var defexp = DefenseExperience;
            var fame = Fame;
            var fexp = FakeExperience;
            var exploss = Math.Floor(exp * .2);
            var attexploss = Math.Floor(attexp * .2);
            var defexploss = Math.Floor(defexp * .2);
            var newexp = exp - exploss;
            var newattexp = attexp - attexploss;
            var newdefexp = defexp - defexploss;
            var newlvl = GetLevel(newexp, ExpType.Level);
            var newattlvl = GetLevel(newattexp, ExpType.Stat) + 10;
            var newdeflvl = GetLevel(newdefexp, ExpType.Stat) + 10;
            var items = Inventory;
            var (hp, mp) = GetStats[GetClassType[ObjectType]];

            if (newlvl < 1)
            {
                newlvl = 1;
                newexp = GetLevel(1, ExpType.Level);
            }

            if (newattlvl < 10)
            {
                newattlvl = 10;
                newattexp = GetLevel(1, ExpType.Stat);
            }

            if (newdeflvl < 10)
            {
                newdeflvl = 10;
                newdefexp = GetLevel(1, ExpType.Stat);
            }

            if (lvl > newlvl)
                newlvl = Level;
            else
                newlvl = lvl;

            if (attlvl > newattlvl)
                newattlvl = AttackLevel;
            else
                newattlvl = attlvl;

            if (deflvl > newdeflvl)
                newdeflvl = DefenseLevel;
            else
                newdeflvl = deflvl;

            if (rng.Next(0, 100) <= 100 - 20 * blessings)
                Client.Character.Pet = 0;

            if (rng.Next(0, 100) <= 100 - 20 * blessings)
            {
                var max = rng.Next(1, 3);

                for (var i = rng.Next(0, items.Length - 1); i < items.Length; i++)
                {
                    items[i] = null;

                    max--;

                    if (max == 0)
                        break;
                }
            }

            Stats[0] = Level * hp + 100; // hp
            Stats[1] = Level * mp + 100; // mp
            Stats[2] = 0; // old attack
            Stats[3] = 0; // old defense
            Stats[4] = Level / 2 + 10; // spd
            Stats[5] = Level / 2 + 10; // vit
            Stats[6] = Level / 2 + 10; // wis
            Stats[7] = Level / 1 + 10; // dex
            Fame = 0;
            Level = newlvl;
            Experience = newexp;
            FakeExperience = 0;
            IsFakeEnabled = true;
            AttackLevel = newattlvl;
            AttackExperience = newattexp;
            DefenseLevel = newdeflvl;
            DefenseExperience = newdefexp;
            Inventory = items;
            Bless1 = false;
            Bless2 = false;
            Bless3 = false;
            Bless4 = false;
            Bless5 = false;

            GameServer.Manager.TryDisconnect(Client, DisconnectReason.CHARACTER_IS_DEAD);
            return;
        }

        public int GetStatBase(int index, ObjectDesc desc)
        {
            switch (index)
            {
                case 2: return desc.ATTBase;
                case 3: return desc.DEFBase;
                case 4: return desc.SPDBase;
                case 5: return desc.VITBase;
                case 6: return desc.WISBase;
                case 7: return desc.DEXBase;
                default: return 0;
            }
        }

        public int CountBlessings()
        {
            var blessings = 0;

            if (Bless1) blessings++;
            if (Bless2) blessings++;
            if (Bless3) blessings++;
            if (Bless4) blessings++;
            if (Bless5) blessings++;

            return blessings;
        }

        public int GetBlessingPrice() => Level * 50;

        public override void Init(World owner)
        {
            MaxHackEntries = 0;
            visibleTiles = new Dictionary<IntPoint, bool>();
            WorldInstance = owner;

            var rand = new Random();

            int x, y;

            do
            {
                x = rand.Next(0, owner.Map.Width);
                y = rand.Next(0, owner.Map.Height);
            } while (owner.Map[x, y].Region != TileRegion.Spawn);

            var newposition = owner.RemovePositionFromReconnect(AccountId);

            if (newposition.Item1 != 0 && newposition.Item2 != 0)
                Move((int)newposition.Item1 + 0.5f, (int)newposition.Item2 + 0.5f);
            else
                Move(x + 0.5f, y + 0.5f);

            tiles = new byte[owner.Map.Width, owner.Map.Height];

            SetNewbiePeriod();

            base.Init(owner);

            var gifts = Client.Account.Gifts.ToList();

            if (owner.Id == (int)WorldID.NEXUS_ID || owner.Name == "Vault")
            {
                Client.SendMessage(new GLOBAL_NOTIFICATION
                {
                    Type = 0,
                    Text = gifts.Count > 0 ? "giftChestOccupied" : "giftChestEmpty"
                });
            }

            if (Client.Character.Pet != 0)
            {
                HatchlingPet = false;
                HatchlingNotification = false;
                Pet = Resolve((ushort)PetID);
                Pet.Move(x, y);
                Pet.SetPlayerOwner(this);
                Owner.EnterWorld(Pet);
                Pet.IsPet = true;
            }

            SendAccountList(Locked, ACCOUNTLIST.LOCKED_LIST_ID);
            SendAccountList(Ignored, ACCOUNTLIST.IGNORED_LIST_ID);

            CheckSetTypeSkin();

            if (Settings.SERVER_MODE == Settings.ServerMode.Local)
                if ((AccountType)AccountType == Core.config.AccountType.ADMIN)
                    ApplyConditionEffect((ConditionEffect)new ConditionEffect
                    {
                        Effect = ConditionEffectIndex.Invulnerable,
                        DurationMS = -1
                    });

            ApplyConditionEffect(AccountPerks.SetAccountTypeIcon());

            Achievements = ImportAchivementCache();
            ActualTask = ImportTaskCache();
            MonsterCaches = ImportMonsterCaches();

            if (ActualTask != null)
                Task = GameTask.Tasks[ActualTask];

            if (Client.Account.Credits < 0)
            {
                SendInfo("[Patch: negative currency] Gold set to 0.");
                GameServer.Manager.Database.UpdateCredit(Client.Account, Math.Abs(Client.Account.Credits));
            }

            if (Client.Account.Fame < 0)
            {
                SendInfo("[Patch: negative currency] Fame set to 0.");
                GameServer.Manager.Database.UpdateFame(Client.Account, Math.Abs(Client.Account.Fame));
            }
        }

        public void Teleport(RealmTime time, TELEPORT packet)
        {
            var obj = Client.Player.Owner.GetEntity(packet.ObjectId);
            try
            {
                if (obj == null)
                    return;
                if (!TPCooledDown())
                {
                    SendError("Player.teleportCoolDown");
                    return;
                }
                if (obj.HasConditionEffect(ConditionEffectIndex.Invisible))
                {
                    SendError("server.no_teleport_to_invisible");
                    return;
                }
                if (obj.HasConditionEffect(ConditionEffectIndex.Paused))
                {
                    SendError("server.no_teleport_to_paused");
                    return;
                }
                if (obj is Player player && !player.NameChosen)
                {
                    SendError("server.teleport_needs_name");
                    return;
                }
                if (obj.Id == Id)
                {
                    SendError("server.teleport_to_self");
                    return;
                }
                if (!Owner.AllowTeleport)
                {
                    SendError(GetLanguageString("server.no_teleport_in_realm", new KeyValuePair<string, object>("realm", Owner.Name)));
                    return;
                }

                SetTPDisabledPeriod();
                Move(obj.X, obj.Y);
                FameCounter.Teleport();
                SetNewbiePeriod();
                UpdateCount++;
            }
            catch (Exception)
            {
                SendError("player.cannotTeleportTo");
                return;
            }

            Owner.BroadcastMessage(new GOTO
            {
                ObjectId = Id,
                Position = new Position
                {
                    X = X,
                    Y = Y
                }
            }, null);
            Owner.BroadcastMessage(new SHOWEFFECT
            {
                EffectType = EffectType.Teleport,
                TargetId = Id,
                PosA = new Position
                {
                    X = X,
                    Y = Y
                },
                Color = new ARGB(0xFFFFFFFF)
            }, null);

            foreach (var plr in Owner.Players.Values)
                plr.AwaitGotoAck(time.TotalElapsedMs);
        }

        private int QuestPriority(ObjectDesc enemy)
        {
            int score = 0;

            try
            {
                if (enemy.Oryx)
                    score += 100000;

                if (enemy.Cube)
                    score += 2500;

                if (enemy.God)
                    score += 500;

                if (enemy.Hero)
                    score += 1250;

                if (enemy.Encounter)
                    score += 5000;

                if (enemy.Quest)
                    score += 250;

                if (Realm.AllRealmEvents.Contains(enemy.ObjectId))
                    score += 1000000;
                else
                {
                    score += enemy.MaxHitPoints;
                    score += enemy.Defense * enemy.Level;
                }

                if (enemy.ObjectId == "Undertaker the Great Juggernaut")
                    score += 5000000;
            }
            catch { }

            return score;
        }

        private void HandleQuest(RealmTime time)
        {
            if (time.TickCount % 5 != 0)
                return;

            var newQuestId = -1;
            var questId = Quest == null ? -1 : Quest.Id;
            var candidates = new HashSet<Enemy>();

            foreach (var i in Owner.Quests.Values
                .OrderBy(j => MathsUtils.DistSqr(j.X, j.Y, X, Y))
                .Where(k => k.ObjectDesc != null && k.ObjectDesc.Quest))
            {
                if (!RealmManager.QuestPortraits.TryGetValue(i.ObjectDesc.ObjectId, out int questLevel))
                    continue;

                if (Level < questLevel)
                    continue;

                if (!RealmManager.QuestPortraits.ContainsKey(i.ObjectDesc.ObjectId))
                    continue;

                candidates.Add(i);
            }

            if (candidates.Count != 0)
            {
                var newQuest = candidates.OrderByDescending(i => QuestPriority(i.ObjectDesc)).Take(3).ToList()[0];

                newQuestId = newQuest.Id;
                Quest = newQuest;
            }

            if (newQuestId == questId)
                return;

            Client.SendMessage(new QUESTOBJID
            {
                ObjectId = newQuestId
            });
        }

        public void CalculateAttack()
        {
            AttackExperience += Settings.GetEventRate() * 5;

            if (AttackExperience >= AttackGoalExperience)
            {
                AttackLevel++;
                AttackGoalExperience = GetExperience(AttackLevel + 1 - 10, ExpType.Stat);
                SendHelp($"You advanced from attack level {AttackLevel - 1} to level {AttackLevel}.");
                UpdateCount++;
            }
        }

        public void CalculateDefense()
        {
            DefenseExperience += Settings.GetEventRate() * 5;

            if (DefenseExperience >= DefenseGoalExperience)
            {
                DefenseLevel++;
                DefenseGoalExperience = GetExperience(DefenseLevel + 1 - 10, ExpType.Stat);
                SendHelp($"You advanced from defense level {DefenseLevel - 1} to level {DefenseLevel}.");
                UpdateCount++;
            }
        }

        public void CalculateFame(bool notifyFame = true)
        {
            var newFame = 0d;
            newFame += Math.Max(0, Math.Min(20000, FakeExperience)) * 0.001;
            newFame += Math.Max(0, Math.Min(45200, FakeExperience) - 20000) * 0.002;
            newFame += Math.Max(0, Math.Min(80000, FakeExperience) - 45200) * 0.003;
            newFame += Math.Max(0, Math.Min(101200, FakeExperience) - 80000) * 0.002;
            newFame += Math.Max(0, FakeExperience - 101200) * 0.0005;
            newFame = Math.Floor(newFame);

            if (newFame == Fame)
                return;

            if (notifyFame)
                Owner.BroadcastMessage(new NOTIFICATION
                {
                    ObjectId = Id,
                    Color = new ARGB(0xFF8C00),
                    Text = "{\"key\":\"blank\",\"tokens\":{\"data\":\"+" + (newFame - Fame) + " Fame!\"}}",
                }, null);

            Fame = newFame;

            var stats = FameCounter.ClassStats[ObjectType];
            var newGoal = GetFameGoal(stats.BestFame > Fame ? stats.BestFame : Fame);

            if (newGoal > FameGoal && AccountType < (int)Core.config.AccountType.MOD)
            {
                Owner.BroadcastMessage(new NOTIFICATION
                {
                    ObjectId = Id,
                    Color = new ARGB(0xFF00FF00),
                    Text = "{\"key\":\"blank\",\"tokens\":{\"data\":\"Class Quest Complete!\"}}",
                }, null);
                Stars = GetStars();
            }

            FameGoal = (AccountType >= (int)Core.config.AccountType.MOD) ? 0 : newGoal;
            UpdateCount++;
        }

        private enum ClassType
        {
            Mage,
            Melee,
            Range
        }

        private Dictionary<ushort, ClassType> GetClassType = new Dictionary<ushort, ClassType>()
        {
            { 0x0300, ClassType.Range }, // rogue
            { 0x0307, ClassType.Range }, // archer
            { 0x030e, ClassType.Mage }, // wizard
            { 0x0310, ClassType.Mage }, // priest
            { 0x031d, ClassType.Melee }, // warrior
            { 0x031e, ClassType.Melee }, // knight
            { 0x031f, ClassType.Melee }, // paladin
            { 0x0320, ClassType.Range }, // assassin
            { 0x0321, ClassType.Mage }, // necromancer
            { 0x0322, ClassType.Range }, // huntress
            { 0x0323, ClassType.Mage }, // mystic
            { 0x0324, ClassType.Range }, // trickster
            { 0x0325, ClassType.Mage }, // sorcerer
            { 0x0326, ClassType.Melee } // ninja
        };

        private Dictionary<ClassType, (int, int)> GetStats = new Dictionary<ClassType, (int, int)>()
        {
            { ClassType.Mage, (5, 30) },
            { ClassType.Melee, (15, 5) },
            { ClassType.Range, (10, 10) }
        };

        public bool CheckLevelUp(bool notifyFame = true)
        {
            if (!IsFakeEnabled)
            {
                IsFakeEnabled = true;
                FakeExperience = Experience;
            }

            if (Experience >= ExperienceGoal)
            {
                Level++;

                ExperienceGoal = GetExperience(Level + 1, ExpType.Level);

                var (hp, mp) = GetStats[GetClassType[ObjectType]];

                Stats[0] = Level * hp + 100; // hp
                Stats[1] = Level * mp + 100; // mp
                Stats[2] = 0; // old attack
                Stats[3] = 0; // old defense
                Stats[4] = Level / 2 + 10; // spd
                Stats[5] = Level / 2 + 10; // vit
                Stats[6] = Level / 2 + 10; // wis
                Stats[7] = Level / 1 + 10; // dex

                HP = Stats[0] + Boost[0];
                MP = Stats[1] + Boost[1];

                UpdateCount++;

                var playerDesc = GameServer.Manager.GameData.ObjectDescs[ObjectType];

                SendHelp($"You advanced from level {Level - 1} to level {Level}.");

                if (Level % 50 == 0 || Level == 30)
                {
                    foreach (var client in GameServer.Manager.GetManager.Clients.Values)
                        client.Player?.GazerDM($"Player {Name} achived level {Level} as {playerDesc.ObjectId}.");
                }

                Quest = null;
                return true;
            }

            CalculateFame(notifyFame);
            return false;
        }

        public bool CalculateExp(Enemy enemy, int exp)
        {
            if (enemy == Quest)
                Owner.BroadcastMessage(new NOTIFICATION
                {
                    ObjectId = Id,
                    Color = new ARGB(0xFF00FF00),
                    Text = "{\"key\":\"blank\",\"tokens\":{\"data\":\"Quest Complete!\"}}",
                }, null);

            if (exp > 0)
            {
                var getexpboost = GetExpBoost(exp, AccountType);

                Experience += getexpboost;
                FakeExperience += getexpboost;
                UpdateCount++;
            }

            return CheckLevelUp();
        }

        private int GetExpBoost(int exp, int accountType)
            => (int)Math.Ceiling((accountType == (int)Core.config.AccountType.VIP ? 1.5 : 1)
                    * exp * Settings.GetEventRate());

        internal Projectile PlayerShootProjectile(
            byte id,
            ProjectileDesc desc,
            ushort objType,
            long time,
            Position position,
            float angle
            )
        {
            ProjectileId = id;
            return CreateProjectile(desc, objType, (int)StatsManager.GetAttackDamage(desc.MinDamage, desc.MaxDamage), time, position, angle);
        }

        public override void Tick(RealmTime time)
        {
            if (Client == null)
                return;

            if (!KeepAlive(time) || Client.State == ProtocolState.Disconnected)
            {
                if (Owner != null)
                    Owner.LeaveWorld(this);
                else
                    WorldInstance.LeaveWorld(this);

                return;
            }

            if (Stats != null && Boost != null)
            {
                MaxHp = Stats[0] + Boost[0];
                MaxMp = Stats[1] + Boost[1];
            }

            if (Boost == null)
                CalculateBoost();

            if (!HasConditionEffect(ConditionEffects.Paused))
            {
                HandleRegen(time);

                HandleGround(time);

                FameCounter.Tick(time);
            }

            HandleTrade?.Tick(time);

            try
            {
                HandleQuest(time);
            }
            catch (NullReferenceException) { }

            HandleEffects(time);

            HandleBoosts();

            if (MP < 0)
                MP = 0;

            if (Owner != null)
            {
                HandleNewTick(time);

                HandleUpdate(time);
            }

            if (HP < 0 && !dying)
            {
                Death("Unknown");
                return;
            }

            base.Tick(time);
        }
    }
}