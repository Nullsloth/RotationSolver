using RotationSolver.Actions;
using RotationSolver.Configuration.RotationConfig;
using RotationSolver.Data;
using RotationSolver.Helpers;
using RotationSolver.Rotations.Basic;
using RotationSolver.Rotations.CustomRotation;
using System.Collections.Generic;

namespace RotationSolver.Rotations.RangedMagicial.RDM;

internal sealed class RDM_Default : RDM_Base
{
    public override string GameVersion => "6.0";

    public override string RotationName => "Default";

    public override SortedList<DescType, string> DescriptionDict => new()
    {
        {DescType.HealSingle, $"{Vercure}"},
        {DescType.DefenseArea, $"{MagickBarrier}"},
        {DescType.MoveAction, $"{CorpsAcorps}"},
    };

    static RDM_Default()
    {
        Acceleration.RotationCheck = b => InCombat;
    }

    private protected override IRotationConfigSet CreateConfiguration()
    {
        return base.CreateConfiguration()
            .SetBool("UseVercure", true, "Use Vercure for Dualcast");
    }

    private protected override bool EmergencyAbility(byte abilityRemain, IAction nextGCD, out IAction act)
    {
        act = null;
        //鼓励要放到魔回刺或者魔Z斩或魔划圆斩之后
        if (nextGCD.IsTheSameTo(true, Zwerchhau, Redoublement, Moulinet))
        {
            if (Service.Configuration.AutoBurst && Embolden.CanUse(out act, mustUse: true)) return true;
        }
        //开场爆发的时候释放。
        if (Service.Configuration.AutoBurst && GetRightValue(WhiteMana) && GetRightValue(BlackMana))
        {
            if (!canUseMagic(act) && Manafication.CanUse(out act)) return true;
            if (Embolden.CanUse(out act, mustUse: true)) return true;
        }
        //倍增要放到魔连攻击之后
        if (ManaStacks == 3 || Level < 68 && !nextGCD.IsTheSameTo(true, Zwerchhau, Riposte))
        {
            if (!canUseMagic(act) && Manafication.CanUse(out act)) return true;
        }

        act = null;
        return false;
    }

    private bool GetRightValue(byte value)
    {
        return value >= 6 && value <= 12;
    }

    private protected override bool AttackAbility(byte abilitiesRemaining, out IAction act)
    {
        act = null;
        if (InBurst)
        {
            if (!canUseMagic(act) && Manafication.CanUse(out act)) return true;
            if (Embolden.CanUse(out act, mustUse: true)) return true;
        }

        if (ManaStacks == 0 && (BlackMana < 50 || WhiteMana < 50) && !Manafication.WillHaveOneChargeGCD(1, 1))
        {
            //促进满了且预备buff没满就用。 
            if (abilitiesRemaining == 2 && Acceleration.CanUse(out act, emptyOrSkipCombo: true)
                && (!Player.HasStatus(true, StatusID.VerfireReady) || !Player.HasStatus(true, StatusID.VerstoneReady))) return true;

            //即刻咏唱
            if (!Player.HasStatus(true, StatusID.Acceleration)
                && Swiftcast.CanUse(out act, mustUse: true)
                && (!Player.HasStatus(true, StatusID.VerfireReady) || !Player.HasStatus(true, StatusID.VerstoneReady))) return true;
        }

        //攻击四个能力技。
        if (ContreSixte.CanUse(out act, mustUse: true)) return true;
        if (Fleche.CanUse(out act)) return true;
        //Empty: BaseAction.HaveStatusSelfFromSelf(1239)
        if (Engagement.CanUse(out act, emptyOrSkipCombo: true)) return true;

        if (CorpsAcorps.CanUse(out act, mustUse: true) && !IsMoving) return true;

        return false;
    }

    private protected override bool GeneralGCD(out IAction act)
    {
        act = null;
        if (ManaStacks == 3) return false;

        #region 常规输出
        if (!Verthunder2.CanUse(out _))
        {
            if (Verfire.CanUse(out act)) return true;
            if (Verstone.CanUse(out act)) return true;
        }

        //试试看散碎
        if (Scatter.CanUse(out act)) return true;
        //平衡魔元
        if (WhiteMana < BlackMana)
        {
            if (Veraero2.CanUse(out act)) return true;
            if (Veraero.CanUse(out act)) return true;
        }
        else
        {
            if (Verthunder2.CanUse(out act)) return true;
            if (Verthunder.CanUse(out act)) return true;
        }
        if (Jolt.CanUse(out act)) return true;
        #endregion

        //震荡刷火炎和飞石


        //赤治疗，加即刻，有连续咏唱或者即刻的话就不放了
        if (Configs.GetBool("UseVercure") && Vercure.CanUse(out act)
            ) return true;

        return false;
    }


    private protected override bool DefenceAreaAbility(byte abilitiesRemaining, out IAction act)
    {
        //混乱
        if (Addle.CanUse(out act)) return true;
        if (MagickBarrier.CanUse(out act, mustUse: true)) return true;
        return false;
    }

    private protected override bool EmergencyGCD(out IAction act)
    {
        byte level = Level;
        #region 远程三连
        //如果魔元结晶满了。
        if (ManaStacks == 3)
        {
            if (BlackMana > WhiteMana && level >= 70)
            {
                if (Verholy.CanUse(out act, mustUse: true)) return true;
            }
            if (Verflare.CanUse(out act, mustUse: true)) return true;
        }

        //焦热
        if (Scorch.CanUse(out act, mustUse: true)) return true;

        //决断
        if (Resolution.CanUse(out act, mustUse: true)) return true;
        #endregion

        #region 近战三连


        if (IsLastGCD(true, Moulinet) && Moulinet.CanUse(out act, mustUse: true)) return true;
        if (Zwerchhau.CanUse(out act)) return true;
        if (Redoublement.CanUse(out act)) return true;

        //如果倍增好了，或者魔元满了，或者正在爆发，或者处于开场爆发状态，就马上用！
        bool mustStart = Player.HasStatus(true, StatusID.Manafication) ||
                         BlackMana == 100 || WhiteMana == 100 || !Embolden.IsCoolingDown;

        //在魔法元没有溢出的情况下，要求较小的魔元不带触发，也可以强制要求跳过判断。
        if (!mustStart)
        {
            if (BlackMana == WhiteMana) return false;

            //要求较小的魔元不带触发，也可以强制要求跳过判断。
            if (WhiteMana < BlackMana)
            {
                if (Player.HasStatus(true, StatusID.VerstoneReady))
                {
                    return false;
                }
            }
            if (WhiteMana > BlackMana)
            {
                if (Player.HasStatus(true, StatusID.VerfireReady))
                {
                    return false;
                }
            }

            //看看有没有即刻相关的技能。
            if (Player.HasStatus(true, Vercure.StatusProvide))
            {
                return false;
            }

            //如果倍增的时间快到了，但还是没好。
            if (Embolden.WillHaveOneChargeGCD(10))
            {
                return false;
            }
        }
        #endregion

        if (Player.HasStatus(true, StatusID.Dualcast)) return false;

        #region 开启爆发
        //要来可以使用近战三连了。
        if (Moulinet.CanUse(out act))
        {
            if (BlackMana >= 60 && WhiteMana >= 60) return true;
        }
        else
        {
            if (BlackMana >= 50 && WhiteMana >= 50 && Riposte.CanUse(out act)) return true;
        }
        if (ManaStacks > 0 && Riposte.CanUse(out act)) return true;
        #endregion

        return false;
    }

    //判定焦热决断能不能使用
    private bool canUseMagic(IAction act)
    {
        //return IsLastAction(true, Scorch) || IsLastAction(true, Resolution) || IsLastAction(true, Verholy) || IsLastAction(true, Verflare);
        return Scorch.CanUse(out act) || Resolution.CanUse(out act);
    }
}

