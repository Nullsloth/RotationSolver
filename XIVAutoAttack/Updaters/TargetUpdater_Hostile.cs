﻿using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using XIVAutoAttack.Helpers;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace XIVAutoAttack.Updaters
{
    internal static partial class TargetUpdater
    {
        private static BattleChara[] AllTargets { get; set; } = new BattleChara[0];

        internal static BattleChara[] HostileTargets { get; private set; } = new BattleChara[0];

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static BattleChara[] TarOnMeTargets { get; private set; } = new BattleChara[0];

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static BattleChara[] CanInterruptTargets { get; private set; } = new BattleChara[0];

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static bool HaveHostileInRange { get; private set; } = false;

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static bool IsHostileAOE { get; private set; } = false;

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static bool IsHostileTank { get; private set; } = false;

        internal unsafe static void UpdateHostileTargets()
        {
            //能打的目标
            AllTargets = TargetFilter.GetTargetable(TargetFilter.GetObjectInRadius(Service.ObjectTable.ToArray(), 30).Where(obj =>
            {
                if (obj is BattleChara c && c.CurrentHp != 0)
                {
                    foreach (var status in c.StatusList)
                    {
                        if (Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>()
                        .GetRow(status.StatusId).Icon == 15024) return false;
                    }
                    if (obj.CanAttack()) return true;
                }
                return false;
            }).Cast<BattleChara>().ToArray());

            //Filter the fate objects.
            //if (Service.Configuration.ChangeTargetForFate && FateManager.Instance()->FateJoined > 0)
            //{
            //    //Get fate objects.
            //    var vector = FateManager.Instance()->Unk_Vector;
            //    uint[] fateIds = new uint[vector.Size()];
            //    for (ulong i = 0; i < vector.Size(); i++)
            //    {
            //        fateIds[i] = vector.Get(i).ObjectID;
            //    }

            //    AllTargets = AllTargets.Where(t => fateIds.Contains(t.ObjectId)).ToArray();
            //}

            uint[] ids = GetEnemies() ?? new uint[0];

            if (AllTargets != null && AllTargets.Length > 0)
            {
                HostileTargets = AllTargets.Where(t => t.TargetObject is PlayerCharacter || ids.Contains(t.ObjectId)).ToArray();

                switch (Service.Configuration.TargetToHostileType)
                {
                    case 0:
                        HostileTargets = AllTargets;
                        break;

                    default:
                    case 1:
                        if (HostileTargets.Length == 0)
                            HostileTargets = AllTargets;
                        break;

                    case 2:
                        break;
                }

                CanInterruptTargets = HostileTargets.Where(tar => tar.IsCasting && tar.IsCastInterruptible && tar.TotalCastTime >= 2
                && tar.CurrentCastTime >= Service.Configuration.InterruptibleTime).ToArray();

                TarOnMeTargets = HostileTargets.Where(tar => tar.TargetObjectId == Service.ClientState.LocalPlayer.ObjectId).ToArray();

                float radius = 25;
                switch (XIVAutoAttackPlugin.AllJobs.First(job => job.RowId == Service.ClientState.LocalPlayer.ClassJob.Id).Role)
                {
                    case (byte)Role.防护:
                    case (byte)Role.近战:
                        radius = 3;
                        break;
                }
                HaveHostileInRange = TargetFilter.GetObjectInRadius(HostileTargets, radius).Length > 0;
            }
            else
            {
                AllTargets = HostileTargets = CanInterruptTargets = new BattleChara[0];
                HaveHostileInRange = false;
            }

            if (HostileTargets.Length == 1)
            {
                var tar = HostileTargets[0];

                IsHostileTank = IsHostileCastingTank(tar);
                IsHostileAOE = IsHostileCastingArea(tar);
            }
        }

        private static unsafe uint[] GetEnemies()
        {
            if (!Service.Configuration.AddEnemyListToHostile) return new uint[0];

            var addonByName = Service.GameGui.GetAddonByName("_EnemyList", 1);
            if (addonByName != IntPtr.Zero)
            {
                var addon = (AddonEnemyList*)addonByName;
                var numArray = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder.NumberArrays[19];
                List<uint> list = new List<uint>(addon->EnemyCount);
                for (var i = 0; i < addon->EnemyCount; i++)
                {
                    list.Add((uint)numArray->IntArray[8 + i * 6]);
                }
                return list.ToArray();
            }
            return new uint[0];
        }

        private static bool IsHostileCastingTank(BattleChara h)
        {
            return IsHostileCastingBase(h, (act) =>
            {
                return h.CastTargetObjectId == h.TargetObjectId;
            });
        }

        private static bool IsHostileCastingArea(BattleChara h)
        {
            return IsHostileCastingBase(h, (act) =>
            {
                if (h.CastTargetObjectId == h.TargetObjectId) return false;
                if ((act.CastType == 1 || act.CastType == 2) &&
                    act.Range == 0 &&
                    act.EffectRange >= 40)
                    return true;
                return false;
            });
        }

        private static bool IsHostileCastingBase(BattleChara h, Func<Action, bool> check)
        {
            if (h.IsCasting)
            {
                if (h.IsCastInterruptible) return false;
                var last = h.TotalCastTime - h.CurrentCastTime;

                if (!(h.TotalCastTime > 2 && last < 6 && last > 0.5)) return false;

                var action = Service.DataManager.GetExcelSheet<Action>().GetRow(h.CastActionId);
                return check?.Invoke(action) ?? false;
            }
            return false;
        }
    }
}