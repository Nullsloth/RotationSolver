﻿using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using XIVAutoAttack.Actions;

namespace XIVAutoAttack.Combos.CustomCombo
{
    public abstract partial class CustomCombo
    {
        internal static BattleChara EnemyTarget;
        internal static EnemyLocation ShouldLocation { get; set; } = EnemyLocation.None;
        internal bool TryInvoke(uint actionID, uint lastComboActionID, float comboTime, byte level, out IAction newAction)
        {

            newAction = null;
            if (!IsEnabled)
            {
                return false;
            }
            if (ActionID.ID != actionID)
            {
                return false;
            }

            newAction = Invoke(actionID, lastComboActionID, comboTime);

            //没获得对象
            if (newAction == null) return false;

            //和之前一样
            if (actionID == newAction.ID) return false;

            return true;
        }

        private IAction Invoke(uint actionID, uint lastComboActionID, float comboTime)
        {
            byte abilityRemain = TargetHelper.AbilityRemainCount;

            //防AOE
            var helpDefenseAOE = Service.Configuration.AutoDefenseForTank && TargetHelper.IsHostileAOE;

            //防单体
            bool helpDefenseSingle = false;
            //是个骑士或者奶妈
            if ((Role)XIVAutoAttackPlugin.AllJobs.First(job => job.RowId == JobID).Role == Role.治疗 || Service.ClientState.LocalPlayer.ClassJob.Id == 19)
            {
                if (Service.Configuration.AutoDefenseForTank && TargetHelper.PartyTanks.Any((tank) =>
                {
                    var attackingTankObj = TargetHelper.HostileTargets.Where(t => t.TargetObjectId == tank.ObjectId);

                    if (attackingTankObj.Count() != 1) return false;

                    return TargetHelper.IsHostileTank;
                })) helpDefenseSingle = true;
            }

            IAction act = GCD(lastComboActionID, abilityRemain, helpDefenseAOE, helpDefenseSingle);
            //Sayout!
            if (act != null && act is BaseAction GCDaction)
            {
                if (GCDaction.EnermyLocation != EnemyLocation.None)
                {
                    if (CheckAction(GCDaction.ID))
                    {
                        string location = GCDaction.EnermyLocation.ToName();
                        if (Service.Configuration.SayingLocation) Speak(location);
                        if (Service.Configuration.TextLocation) Service.ToastGui.ShowQuest(" " + location, new Dalamud.Game.Gui.Toast.QuestToastOptions()
                        {
                            IconId = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>().GetRow(
                                Service.IconReplacer.OriginalHook(GCDaction.ID)).Icon,
                        });
                        EnemyTarget = GCDaction.Target;
                        ShouldLocation = GCDaction.EnermyLocation;
                    }
                }
                else
                {
                    ShouldLocation = EnemyLocation.None;
                }

                switch (abilityRemain)
                {
                    case 0:
                        return GCDaction;
                    default:
                        if (Ability(abilityRemain, GCDaction, out IAction ability, helpDefenseAOE, helpDefenseSingle)) return ability;
                        return GCDaction;
                }
            }
            else if (act == null)
            {
                ShouldLocation = EnemyLocation.None;
                if (Ability(abilityRemain, GeneralActions.Addle, out IAction ability, helpDefenseAOE, helpDefenseSingle)) return ability;
                return null;
            }
            else
            {
                ShouldLocation = EnemyLocation.None;
            }
            return act;
        }

        uint _lastSayingGCDAction;
        DateTime lastTime;
        private bool CheckAction(uint actionID)
        {
            //return false;
            if ((_lastSayingGCDAction != actionID || DateTime.Now - lastTime > new TimeSpan(0,0,3)) && IconReplacer.AutoAttack)
            {
                _lastSayingGCDAction = actionID;
                lastTime = DateTime.Now;
                return true;
            }
            else return false;
        }

        internal static void Speak(string text, bool wait = false)
        {
            ExecuteCommand(
                $@"Add-Type -AssemblyName System.speech; 
                $speak = New-Object System.Speech.Synthesis.SpeechSynthesizer; 
                $speak.Volume = ""{Service.Configuration.VoiceVolume}"";
                $speak.Speak(""{text}"");");

            void ExecuteCommand(string command)
            {
                string path = Path.GetTempPath() + Guid.NewGuid() + ".ps1";

                // make sure to be using System.Text
                using (StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8))
                {
                    sw.Write(command);

                    ProcessStartInfo start = new ProcessStartInfo()
                    {
                        FileName = @"C:\Windows\System32\windowspowershell\v1.0\powershell.exe",
                        LoadUserProfile = false,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Arguments = $"-executionpolicy bypass -File {path}",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process process = Process.Start(start);

                    if (wait)
                        process.WaitForExit();
                }
            }
        }

        private IAction GCD(uint lastComboActionID, byte abilityRemain, bool helpDefenseAOE, bool helpDefenseSingle)
        {
            if (EmergercyGCD(lastComboActionID, out IAction act)) return act;

            if (EsunaRaise(out act, abilityRemain, false)) return act;
            if (IconReplacer.Move && MoveGCD(lastComboActionID, out act)) return act;
            if (TargetHelper.HPNotFull)
            {
                if ((IconReplacer.HealArea || CanHealAreaSpell) && !HealAreaAbility(1, out _)
                    && HealAreaGCD(lastComboActionID, out act)) return act;
                if ((IconReplacer.HealSingle || CanHealSingleSpell) && !HealSingleAbility(1, out _)
                    && HealSingleGCD(lastComboActionID, out act)) return act;
            }
            if (IconReplacer.DefenseArea && DefenseAreaGCD(abilityRemain, out act)) return act;
            if (IconReplacer.DefenseSingle && DefenseSingleGCD(abilityRemain, out act)) return act;

            //自动防御
            if (helpDefenseAOE && DefenseAreaGCD(abilityRemain, out act)) return act;
            if (helpDefenseSingle && DefenseSingleGCD(abilityRemain, out act)) return act;

            if (GeneralGCD(lastComboActionID, out var action)) return action;

            //硬拉或者开始奶人
            if ((HaveSwift || !GeneralActions.Swiftcast.IsCoolDown) && EsunaRaise(out act, abilityRemain, true)) return act;
            if (TargetHelper.HPNotFull && HaveTargetAngle)
            {
                if (CanHealAreaSpell && HealAreaGCD(lastComboActionID, out act)) return act;
                if (CanHealSingleSpell && HealSingleGCD(lastComboActionID, out act)) return act;
            }
            if (Service.Configuration.RaisePlayerByCasting && EsunaRaise(out act, abilityRemain, true)) return act;

            return null;
        }

        private bool EsunaRaise(out IAction act, byte actabilityRemain, bool mustUse)
        {
            if (Raise == null)
            {
                act = null;
                return false;
            }
            //有某些非常危险的状态。
            if (IconReplacer.EsunaOrShield && TargetHelper.WeakenPeople.Length > 0 || TargetHelper.DyingPeople.Length > 0)
            {
                if ((Role)XIVAutoAttackPlugin.AllJobs.First(job => job.RowId == JobID).Role == Role.治疗
                    && GeneralActions.Esuna.ShouldUseAction(out act, mustUse: true)) return true;

            }

            //有人死了，看看能不能救。
            if (Service.Configuration.RaiseAll ? TargetHelper.DeathPeopleAll.Length > 0 : TargetHelper.DeathPeopleParty.Length > 0)
            {
                if (Service.ClientState.LocalPlayer.ClassJob.Id == 35)
                {
                    if (HaveSwift && Raise.ShouldUseAction(out act)) return true;
                }
                else if (IconReplacer.RaiseOrShirk || HaveSwift || !GeneralActions.Swiftcast.IsCoolDown && actabilityRemain > 0 || mustUse)
                {
                    if (Raise.ShouldUseAction(out _))
                    {
                        if (mustUse && GeneralActions.Swiftcast.ShouldUseAction(out act)) return true;
                        act = Raise;
                        return true;
                    }
                }
            }
            act = null;
            return false;
        }


        /// <summary>
        /// 一些非常紧急的GCD战技，优先级最高
        /// </summary>
        /// <param name="lastComboActionID"></param>
        /// <param name="act"></param>
        /// <returns></returns>
        private protected virtual bool EmergercyGCD(uint lastComboActionID, out IAction act)
        {
            act = null; return false;
        }
        /// <summary>
        /// 常规GCD技能
        /// </summary>
        /// <param name="lastComboActionID"></param>
        /// <param name="act"></param>
        /// <returns></returns>
        private protected abstract bool GeneralGCD(uint lastComboActionID, out IAction act);

        private protected virtual bool MoveGCD(uint lastComboActionID, out IAction act)
        {
            act = null; return false;
        }

        /// <summary>
        /// 单体治疗GCD
        /// </summary>
        /// <param name="lastComboActionID"></param>
        /// <param name="act"></param>
        /// <returns></returns>
        private protected virtual bool HealSingleGCD(uint lastComboActionID, out IAction act)
        {
            act = null; return false;
        }

        /// <summary>
        /// 范围治疗GCD
        /// </summary>
        /// <param name="level"></param>
        /// <param name="lastComboActionID"></param>
        /// <param name="act"></param>
        /// <returns></returns>
        private protected virtual bool HealAreaGCD(uint lastComboActionID, out IAction act)
        {
            act = null; return false;
        }

        private protected virtual bool DefenseSingleGCD(uint lastComboActionID, out IAction act)
        {
            act = null; return false;
        }
        private protected virtual bool DefenseAreaGCD(uint lastComboActionID, out IAction act)
        {
            act = null; return false;
        }
    }
}