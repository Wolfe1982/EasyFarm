
/*///////////////////////////////////////////////////////////////////
<EasyFarm, general farming utility for FFXI.>
Copyright (C) <2013>  <Zerolimits>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
*/
///////////////////////////////////////////////////////////////////

using FFACETools;
using System.Threading;
using ZeroLimits.FarmingTool;
using ZeroLimits.XITool;
using System.Linq;
using System.Collections.Generic;
using System;
using ZeroLimits.XITool.Classes;
using EasyFarm.ViewModels;
using EasyFarm.UserSettings;

namespace EasyFarm.Components
{
    /// <summary>
    /// A class for defeating monsters.
    /// </summary>
    public class AttackComponent : BaseComponent
    {
        public AttackComponent(FFACE fface) : base(fface) { }

        public static bool fightStarted = false;

        private static Unit _targetUnit = Unit.CreateUnit(0);

        /// <summary>
        /// Who we are trying to kill currently
        /// </summary>
        public static Unit TargetUnit
        {
            get { return _targetUnit; }
            set { _targetUnit = value; }
        }

        public override bool CheckComponent()
        {
            // If the target is not valid. 
            if (!ftools.UnitService.IsValid(TargetUnit)) return false;

            // We're dead. 
            if (FFACE.Player.Status.Equals(Status.Dead1 | Status.Dead2)) return false;

            // If we're injured  
            if (new RestComponent(FFACE).CheckComponent())
            {
                if (FFACE.Player.Status != Status.Fighting) return false;
                if (!ftools.UnitService.HasAggro) return false;
            }

            // Should we attack?
            return true;
        }

        public override void EnterComponent()
        {
            ftools.RestingService.EndResting();
        }

        public override void RunComponent()
        {
            // Face the target
            FFACE.Navigator.FaceHeading(TargetUnit.ID);

            // Check correct target
            if (TargetUnit.ID != FFACE.Target.ID)
                ftools.CombatService.Disengage();

            // Target the target
            if (TargetUnit.ID != FFACE.Target.ID)
                ftools.CombatService.TargetUnit(TargetUnit);

            ///////////////////////////////////////////////////////////////////
            // Buff Player
            ///////////////////////////////////////////////////////////////////

            // Cast only when there is a move ready. 
            if (!fightStarted && !TargetUnit.IsDead)
            {
                var UsableStartingMoves = Config.Instance.ActionInfo.StartList
                    .Where(x => ActionFilters.AbilityFilter(FFACE)(x))
                    .ToList();

                ftools.AbilityExecutor.EnsureSpellsCast(UsableStartingMoves);
            }

            ///////////////////////////////////////////////////////////////////
            // Engage Enemy. 
            ///////////////////////////////////////////////////////////////////

            // Attempt to engage the target up to three times. 
            int engageCount = 0;
            while (!FFACE.Player.Status.Equals(Status.Fighting) && engageCount++ < 3)
            {
                ftools.CombatService.Engage();
            }

            ///////////////////////////////////////////////////////////////////
            // Pull Enemy. 
            ///////////////////////////////////////////////////////////////////

            // Cast only when there is a move ready. 

            // Pull the target casting each spell once until the target is claimed
            if (!fightStarted && !TargetUnit.Status.Equals(Status.Fighting) && !TargetUnit.IsDead)
            {
                var UsablePullingMoves = Config.Instance.ActionInfo.PullList
                    .Where(x => ActionFilters.AbilityFilter(FFACE)(x))
                    .ToList();

                ftools.AbilityExecutor.ExecuteActions(UsablePullingMoves);
            }

            // set to true so that we do not cast starting spells again. 
            fightStarted = true;

            ///////////////////////////////////////////////////////////////////
            // Move to Enemy. 
            ///////////////////////////////////////////////////////////////////

            // Move to the target
            ftools.CombatService.MoveToUnit(TargetUnit, Config.Instance.MiscSettings.MeleeDistance);

            ///////////////////////////////////////////////////////////////////
            // Battle Enemy. 
            ///////////////////////////////////////////////////////////////////

            // Check engaged
            // FIXED: no longer return on not engage but don't execute 
            // these moves instead. Fixes the bot not attacking things 
            // from move than 30 yalms problem. 
            if (FFACE.Player.Status.Equals(Status.Fighting))
            {
                // Weaponskill
                if (ShouldWeaponSkill)
                {
                    // Not sure if weapon skills or job abilities endure the same penalties that 
                    // spell do in regards to wait times. So I'm using zero's here. 
                    ftools.AbilityExecutor.CastLatency = 0;
                    ftools.AbilityExecutor.GlobalCooldown = 0;

                    ftools.AbilityExecutor.UseAbility(Config.Instance.WeaponSkill.Ability);

                    ftools.AbilityExecutor.SetDefaults();
                }

                var UsableBattleMoves = Config.Instance.ActionInfo.BattleList
                    .Where(x => ActionFilters.AbilityFilter(FFACE)(x))
                    .ToList();

                // Cast all battle moves
                ftools.AbilityExecutor.ExecuteActions(UsableBattleMoves);
            }
        }

        public override void ExitComponent() { }

        /// <summary>
        /// Can we perform our weaponskill on the target unit?
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public bool ShouldWeaponSkill
        {
            get
            {
                return ActionFilters.WeaponSkillFilter(FFACE)
                    (Config.Instance.WeaponSkill, TargetUnit);
            }
        }
    }
}
