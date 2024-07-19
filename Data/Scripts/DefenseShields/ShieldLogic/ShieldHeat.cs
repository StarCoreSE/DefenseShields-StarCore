using System;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    using Sandbox.Game;
    using Support;
    using static VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GameDefinition;

    public partial class DefenseShields
    {

        private void UpdateHeatRate()
        {
            var heat = DsState.State.Heat;
            heat /= 10;

            if (heat >= 8) ShieldChargeRate = 0;
            else
            {
                ExpChargeReduction = ExpChargeReductions[heat];
                ShieldChargeRate /= ExpChargeReduction;
            }
        }

        private void StepDamageState()
        {
            if (_isServer)
            {
                Heating();

                if (DsSet.Settings.AutoManage && DsState.State.MaxHpReductionScaler < 0.94 && HeatSinkCount >= DsSet.Settings.SinkHeatCount && (DsState.State.Heat >= 90 || DsState.State.Heat >= 30 && GetPenChance() > 0)) {
                    SettingsUpdated = true;
                    SettingsChangeRequest = true;
                    DsSet.Settings.SinkHeatCount++;
                }
            }

            if (_tick30)
            {
                _runningDamage = _dpsAvg.Add((int)_damageReadOut);
                _runningHeal = _hpsAvg.Add((int)(ShieldChargeRate * ConvToHp));
                _damageReadOut = 0;

            }
        }

        private bool _heatSinkEffectTriggered = false;
        private int _heatSinkEffectTimer = 0;

        private void Heating()
        {
            if (ChargeMgr.AbsorbHeat > 0)
                _lastHeatTick = _tick;

            var oldMaxHpScaler = DsState.State.MaxHpReductionScaler;
            var heatSinkActive = DsSet.Settings.SinkHeatCount > HeatSinkCount;

            if ((heatSinkActive || _sinkCount != 0) && oldMaxHpScaler <= 0.95)
                DecreaseHeatLevel();
            else if (!heatSinkActive && oldMaxHpScaler > 0 && _tick - _lastHeatTick >= 600)
                IncreaseMaxHealth();

            HeatTick();

            var hp = ShieldMaxCharge * ConvToHp;
            var oldHeat = DsState.State.Heat;
            var rawHeatScale = DsSet.Settings.AutoManage ? Session.Enforced.HeatScaler * 2 : Session.Enforced.HeatScaler;

            var heatScale = (ShieldMode == ShieldType.Station || DsSet.Settings.FortifyShield) && DsState.State.Enhancer ? rawHeatScale * 2.75f : rawHeatScale;
            var thresholdAmount = heatScale * _heatScaleHp;
            var nextThreshold = hp * thresholdAmount * (_currentHeatStep + 1);

            var scaledOverHeat = OverHeat / _heatScaleTime;
            var scaledHeatingSteps = HeatingStep / _heatScaleTime;

            var lastStep = _currentHeatStep == 10;
            var overloadStep = _heatCycle == scaledOverHeat && lastStep;

            var nextCycle = _heatCycle == (_currentHeatStep * scaledHeatingSteps) + scaledOverHeat;

            var fastCoolDown = (heatSinkActive || DsState.State.Overload) && (_heatCycle % 200 == 0);

            float shuntingHeatFactor = CalculateShuntedHeatFactor();

            ChargeMgr.AbsorbHeat *= shuntingHeatFactor;

            var pastThreshold = ChargeMgr.AbsorbHeat > nextThreshold;
            var venting = lastStep && pastThreshold;
            var leftCritical = lastStep && _tick >= _heatVentingTick;

            var backTwoCycles = ((_currentHeatStep - 2) * scaledHeatingSteps) + scaledOverHeat + 1;

            if (overloadStep)
                Overload(hp, thresholdAmount, nextThreshold);
            else if (fastCoolDown)
                FastCoolDown(hp, thresholdAmount, scaledHeatingSteps, scaledOverHeat, backTwoCycles);
            else if (nextCycle && !lastStep)
                NextCycleAction(hp, nextThreshold, thresholdAmount, pastThreshold, scaledHeatingSteps, scaledOverHeat, backTwoCycles);
            else if (venting)
                Venting(nextThreshold);
            else if (leftCritical)
                NoLongerCritical(backTwoCycles, thresholdAmount, nextThreshold, hp);

            var fallbackTime = _heatCycle > (HeatingStep * 10) + OverHeat && _tick >= _heatVentingTick;
            if (fallbackTime)
                FallBack();

            if (oldHeat != DsState.State.Heat || !MyUtils.IsEqual(oldMaxHpScaler, DsState.State.MaxHpReductionScaler))
            {
                StateChangeRequest = true;
            }

            if (heatSinkActive)
            {
                if (!_heatSinkEffectTriggered || _heatSinkEffectTimer >= 180) // Check if it's not triggered or 1 second has passed
                {
                    MyVisualScriptLogicProvider.CreateParticleEffectAtEntity("HeatSinkParticle", MyGrid.Name);
                    MyVisualScriptLogicProvider.PlaySingleSoundAtEntity("HeatSinkSound", MyGrid.Name);
                    _heatSinkEffectTriggered = true;
                    _heatSinkEffectTimer = 0; // Reset the timer
                }
                else
                {
                    _heatSinkEffectTimer++; // Increment the timer
                }
            }
            else
            {
                _heatSinkEffectTriggered = false;
                _heatSinkEffectTimer = 0; // Reset the timer
            }
        }

        // Calculation taken from TapiBackend.cs line 969, counts shunts.
        // This is based off of the surface area of one segment
        // One segment = ( (4πr^2 / 6) / 4πr^2) * 100% = 16.666666... repeating...
        // Simplifying: (1 / 6) * 100% ≈ 16.67% = close enough

        private float CalculateShuntedHeatFactor()
        {
            if (!DsSet.Settings.SideShunting)
            {
                // Add debug notification when shunting is disabled
                //MyAPIGateway.Utilities.ShowNotification("Shunting Heat Factor: 1.00 (Disabled)", 100, "White");
                return 1f;
            }

            int shuntedCount = Math.Abs(ShieldRedirectState.X) + Math.Abs(ShieldRedirectState.Y) + Math.Abs(ShieldRedirectState.Z);
            float factor = 1f + (shuntedCount * 0.1667f);

            // Add debug notification
            //MyAPIGateway.Utilities.ShowNotification($"Shunting Heat Factor: {factor:F2}", 100, "White");

            return factor;
        }

        private void HeatTick()
        {
            var ewarProt = DsState.State.Enhancer && ShieldComp?.Modulator?.ModSet != null && ShieldComp.Modulator.ModSet.Settings.EmpEnabled && ShieldMode != ShieldType.Station;

            if (_tick30 && ChargeMgr.AbsorbHeat > 0 && _heatCycle == -1)
                _heatCycle = 0;
            else if (_heatCycle > -1)
                _heatCycle++;

            if (ewarProt && _heatCycle == 0) {
                _heatScaleHp = 0.1f;
                _heatScaleTime = 5;
            }
            else if (!ewarProt && _heatCycle == 0) {
                _heatScaleHp = 1f;
                _heatScaleTime = 1;
            }
        }
        
        private void NextCycleAction(float hp, float nextThreshold, float thresholdAmount, bool pastThreshold, int scaledHeatingSteps, int scaledOverHeat, int backTwoCycles)
        {
            var currentThreshold = hp * thresholdAmount * _currentHeatStep;
            var metThreshold = ChargeMgr.AbsorbHeat > currentThreshold;
            var underThreshold = !pastThreshold && !metThreshold;
            var backOneCycles = ((_currentHeatStep - 1) * scaledHeatingSteps) + scaledOverHeat + 1;

            if (_heatScaleTime == 5)
            {
                if (ChargeMgr.AbsorbHeat > 0)
                {
                    _fallbackCycle = 1;
                    ChargeMgr.AbsorbHeat = 0;
                }
                else _fallbackCycle++;
            }

            if (pastThreshold)
            {
                PastThreshold(hp, nextThreshold, thresholdAmount);
            }
            else if (metThreshold)
            {
                MetThreshold(backOneCycles, hp, nextThreshold, thresholdAmount);
            }
            else _heatCycle = backOneCycles;

            if (_fallbackCycle == FallBackStep || underThreshold)
            {
                DropHeat(backTwoCycles, currentThreshold);
            }
        }

        public void FastCoolDown(float hp, float thresholdAmount, int scaledHeatingSteps, int scaledOverHeat, int backTwoCycles)
        {
            var currentThreshold = hp * thresholdAmount * _currentHeatStep;
            var backOneCycles = ((_currentHeatStep - 1) * scaledHeatingSteps) + scaledOverHeat + 1;
            _heatCycle = backOneCycles;

            DropHeat(backTwoCycles, currentThreshold);
        }

        public void DecreaseHeatLevel()
        {
            ChargeMgr.AbsorbHeat = 0;
            var end = _tick30 && _sinkCount++ >= SinkCountTime;
            if (!end)
                return;

            _sinkCount = 0;
            HeatSinkCount = DsSet.Settings.SinkHeatCount;
            var hpLoss = DsSet.Settings.FortifyShield ? 0.1 : 0.05;
            DsState.State.MaxHpReductionScaler = (float)MathHelper.Clamp(Math.Round(DsState.State.MaxHpReductionScaler + hpLoss, 3), 0.05, 0.95);
        }

        public void IncreaseMaxHealth()
        {
            if (DsState.State.ShieldPercent >= 100)
                DsState.State.MaxHpReductionScaler = (float) MathHelper.Clamp(Math.Round(DsState.State.MaxHpReductionScaler - 0.05, 3), 0, 0.90);
        }

        private void Overload(float hp, float thresholdAmount, float nextThreshold)
        {
            var overload = ChargeMgr.AbsorbHeat > hp * thresholdAmount * 2;
            if (overload)
            {
                if (Session.Enforced.Debug == 3) Log.Line($"overh - stage:{_currentHeatStep + 1} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{ChargeMgr.AbsorbHeat} - threshold:{hp * thresholdAmount * 2}[{hp / hp * thresholdAmount * (_currentHeatStep + 1)}] - nThreshold:{hp * thresholdAmount * (_currentHeatStep + 2)} - ShieldId [{Shield.EntityId}]");
                _currentHeatStep = 1;
                DsState.State.Heat = _currentHeatStep * 10;
                ChargeMgr.AbsorbHeat = 0;
            }
            else
            {
                if (Session.Enforced.Debug == 3) Log.Line($"under - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:[-1] - heat:{ChargeMgr.AbsorbHeat} - threshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
                DsState.State.Heat = 0;
                _currentHeatStep = 0;
                _heatCycle = -1;
                ChargeMgr.AbsorbHeat = 0;
            }
        }

        private void Venting(float nextThreshold)
        {
            if (Session.Enforced.Debug == 4) Log.Line($"mainc - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{ChargeMgr.AbsorbHeat} - threshold:{nextThreshold} - ShieldId [{Shield.EntityId}]");
            _heatVentingTick = _tick + CoolingStep;
            ChargeMgr.AbsorbHeat = 0;
        }

        private void NoLongerCritical(int backTwoCycles, float thresholdAmount, float nextThreshold, float hp)
        {
            if (_currentHeatStep >= 10) _currentHeatStep--;
            if (Session.Enforced.Debug == 4) Log.Line($"leftc - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:{backTwoCycles} - heat:{ChargeMgr.AbsorbHeat} - threshold:{nextThreshold}[{hp / hp * thresholdAmount * (_currentHeatStep + 1)}] - nThreshold:{hp * thresholdAmount * (_currentHeatStep + 2)} - ShieldId [{Shield.EntityId}]");
            DsState.State.Heat = _currentHeatStep * 10;
            _heatCycle = backTwoCycles;
            _heatVentingTick = uint.MaxValue;
            ChargeMgr.AbsorbHeat = 0;
        }

        private void DropHeat(int backTwoCycles, float currentThreshold)
        {
            if (_currentHeatStep == 0)
            {
                DsState.State.Heat = 0;
                _currentHeatStep = 0;
                if (Session.Enforced.Debug == 4) Log.Line($"nohea - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:[-1] - heat:{ChargeMgr.AbsorbHeat} - ShieldId [{Shield.EntityId}]");
                _heatCycle = -1;
                ChargeMgr.AbsorbHeat = 0;
                _fallbackCycle = 0;
            }
            else
            {
                if (Session.Enforced.Debug == 4) Log.Line($"decto - stage:{_currentHeatStep - 1} - cycle:{_heatCycle} - resetCycle:{backTwoCycles} - heat:{ChargeMgr.AbsorbHeat} - threshold:{currentThreshold} - ShieldId [{Shield.EntityId}]");
                _currentHeatStep--;
                DsState.State.Heat = _currentHeatStep * 10;
                _heatCycle = backTwoCycles;
                ChargeMgr.AbsorbHeat = 0;
                _fallbackCycle = 0;
            }
        }

        private void MetThreshold(int backOneCycles, float hp, float nextThreshold, float thresholdAmount)
        {
            if (Session.Enforced.Debug == 4) Log.Line($"uncha - stage:{_currentHeatStep} - cycle:{_heatCycle} - resetCycle:{backOneCycles} - heat:{ChargeMgr.AbsorbHeat} - threshold:{nextThreshold} - nThreshold:{hp * thresholdAmount * (_currentHeatStep + 2)} - ShieldId [{Shield.EntityId}]");
            DsState.State.Heat = _currentHeatStep * 10;
            _heatCycle = backOneCycles;
            ChargeMgr.AbsorbHeat = 0;
        }

        private void PastThreshold(float hp, float nextThreshold, float thresholdAmount)
        {
            if (Session.Enforced.Debug == 4) Log.Line($"incre - stage:{_currentHeatStep + 1} - cycle:{_heatCycle} - resetCycle:xxxx - heat:{ChargeMgr.AbsorbHeat} - threshold:{nextThreshold}[{hp / hp * thresholdAmount * (_currentHeatStep + 1)}] - nThreshold:{hp * thresholdAmount * (_currentHeatStep + 2)} - ShieldId [{Shield.EntityId}]");
            _currentHeatStep++;
            DsState.State.Heat = _currentHeatStep * 10;
            ChargeMgr.AbsorbHeat = 0;
            if (_currentHeatStep == 10) _heatVentingTick = _tick + CoolingStep;
        }

        private void FallBack()
        {
            if (Session.Enforced.Debug == 4) Log.Line($"HeatCycle over limit, resetting: heatCycle:{_heatCycle} - fallCycle:{_fallbackCycle}");
            _heatCycle = -1;
            _fallbackCycle = 0;
        }

    }
}