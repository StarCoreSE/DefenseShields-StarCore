using System;
using DefenseShields.Support;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;
using static VRage.Game.MyObjectBuilder_ControllerSchemaDefinition;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        internal class ShieldChargeMgr
        {
            internal const float ConvToWatts = 0.01f;
            private const uint SideDownInterval = 900;

            internal readonly RunningAverageCalculator NormalAverage = new RunningAverageCalculator(1800);

            internal DefenseShields Controller;
            internal int SidesOnline;
            internal float Absorb;
            internal float AbsorbHeat;
            internal float ImpactSize = 9f;
            internal float EnergyDamage;
            internal float KineticDamage;
            internal float ModEnergyDamage;
            internal float ModKineticDamage;
            internal float RawEnergyDamage;
            internal float RawKineticDamage;
            internal float AverageNormDamage;
            internal uint LastDamageTick;
            internal uint LastDamageResetTick;
            internal HitType HitType;
            internal bool HitWave;
            internal bool WebDamage;
            internal Vector3D WorldImpactPosition = Vector3D.NegativeInfinity;

            public enum ChargeMode
            {
                Set,
                Charge,
                Discharge,
                Overload,
                OverCharge,
                Zero,
            }


            internal void DoDamage(float damage, float impactSize, bool energy, Vector3D position, bool hitWave, bool webDamage, float heatScaler = 1, bool setRender = true)
            {
                Session.ShieldSides face;
                GetFace(position, out face);
                var sideInfo = Controller.DsState.State.ShieldSides[(int)face];

                if (setRender && sideInfo.Online) {
                    HitWave = hitWave;
                    WorldImpactPosition = position;
                    ImpactSize = impactSize;
                    HitType = energy ? HitType.Energy : HitType.Kinetic;
                }

                WebDamage = webDamage;
                Absorb += damage;
                AbsorbHeat += (damage * heatScaler);
                if (Session.Instance.Tick - LastDamageResetTick > 600)
                {
                    LastDamageResetTick = Session.Instance.Tick;
                    ModKineticDamage = 0;
                    RawKineticDamage = 0;
                    ModEnergyDamage = 0;
                    RawEnergyDamage = 0;
                }
                if (energy)
                {
                    EnergyDamage += damage;
                    ModEnergyDamage += damage;
                    RawEnergyDamage += (damage * Controller.DsState.State.ModulateKinetic);
                }
                else
                {
                    KineticDamage += damage;
                    ModKineticDamage += damage;
                    RawKineticDamage += (damage * Controller.DsState.State.ModulateEnergy);
                }

                sideInfo.Absorb += damage;
            }

            internal void SetCharge(float amount, ChargeMode type)
            {
                var state = Controller.DsState.State;
                var relativeThreshold = 0.05f * Controller.ShieldChargeBase; // 5% threshold
                var previousCharge = state.Charge;

                if (type == ChargeMode.Overload)
                {
                    state.Charge = -(Controller.ShieldMaxCharge * 2);
                    ChargeSide(type);
                }
                else if (type == ChargeMode.OverCharge)
                {
                    state.Charge = amount;
                    ChargeSide(type);
                }
                else if (type == ChargeMode.Zero)
                {
                    state.Charge = 0;
                    ChargeSide(type);
                }
                else if (type == ChargeMode.Set)
                {
                    state.Charge = amount;
                    ChargeSide(type);
                }
                else if (type == ChargeMode.Charge)
                {
                    state.Charge += amount;
                }
                else
                {
                    state.Charge -= amount;

                    if (!state.ReInforce)
                        ChargeSide(type);
                }

                if (Math.Abs(state.Charge - previousCharge) > relativeThreshold)
                    Controller.StateChangeRequest = true;
            }

            internal void ChargeSide(ChargeMode type)
            {
                var sides = Controller.DsState.State.ShieldSides;
                var maxSides = sides.Length;
                var maxSideCharge = Controller.ShieldChargeBase / maxSides;
                var relativeThreshold = 0.05f * maxSideCharge; // 5% threshold

                switch (type)
                {
                    case ChargeMode.Set:
                        if (Controller.DsState.State.Charge <= 0)
                            Clear();
                        else if (Session.Instance.IsServer)
                        {
                            for (int i = 0; i < maxSides; i++)
                            {
                                var sideId = (Session.ShieldSides) i;
                                var side = sides[(int) sideId];

                                side.Charge = maxSideCharge;
                                side.Online = true;
                            }

                            SidesOnline = 6;
                        }
                        break;
                    case ChargeMode.Charge:
                        var heatSinkActive = Controller.DsSet.Settings.SinkHeatCount > Controller.HeatSinkCount;
                        var chargeEfficiency = heatSinkActive ? 2.5f : 0.5f;
                        var reducer = (3 + Controller.ExpChargeReduction) / 4;
                        var chargeBuffer = Controller.ShieldPeakRate / 3;
                        var chargeRate = Controller.ExpChargeReduction > 0 && !Controller.DsSet.Settings.AutoManage ? chargeBuffer / reducer : chargeBuffer;
                        var amount = chargeRate * (maxSides * chargeEfficiency);
                        int activeSides = Controller.DsSet.Settings.SideShunting ? maxSides - Controller.ShuntedSideCount() : maxSides;
                        SidesOnline = 0;
                        for (int i = 0; i < maxSides; i++)
                        {
                            var sideId = (Session.ShieldSides)i;
                            var side = sides[(int) sideId];

                            if (side.Online)
                                ++SidesOnline;

                            if (maxSides != 6 && Controller.IsSideShunted(sideId))
                                continue;

                            var chargePerSide = amount / activeSides;
                            var previousCharge = side.Charge;

                            if (side.Charge + chargePerSide > maxSideCharge)
                            {
                                side.Charge = maxSideCharge;
                            }
                            else
                            {
                                side.Charge += chargePerSide;
                            }

                            if (Session.Instance.IsServer && !side.Online && Session.Instance.Tick >= side.NextOnline)
                            {
                                side.Online = true;
                                Controller.StateChangeRequest = true;
                            }

                            if (Math.Abs(side.Charge - previousCharge) > relativeThreshold )
                                Controller.StateChangeRequest = true;
                        }

                        break;
                    case ChargeMode.Discharge:

                        SidesOnline = 0;

                        for (int i = 0; i < maxSides; i++)
                        {
                            var side = sides[i];


                            if (!side.Online)
                            {
                                side.Absorb = 0;
                                continue;
                            }

                            var previousCharge = side.Charge;
                            side.Charge -= (side.Absorb * ConvToWatts);
                            side.Absorb = 0;

                            if (Session.Instance.IsServer && side.Charge <= 0)
                            {
                                side.Online = false;
                                side.NextOnline = Session.Instance.Tick + SideDownInterval;
                                side.Charge = 0;
                                Controller.StateChangeRequest = true;
                            }

                            if (side.Online)
                                ++SidesOnline;

                            if (Math.Abs(side.Charge - previousCharge) > relativeThreshold)
                                Controller.StateChangeRequest = true;
                        }
                        break;
                    case ChargeMode.Overload:
                    case ChargeMode.Zero:
                        Clear();
                        break;
                    default:
                        break;
                }
            }

            internal void ReportSideStatus()
            {
                var sides = Controller.DsState.State.ShieldSides;
                int totalSides = sides.Length;
                for (int i = 0; i < totalSides; i++)
                {
                    bool offline;
                    MyAPIGateway.Utilities.ShowNotification(((Session.ShieldSides)i).ToString() + ": " + SideHealthRatio((Session.ShieldSides)i, out offline).ToString(), 16);
                }
            }

            internal float SideHealthRatio(Session.ShieldSides shieldSide, out bool offline)
            {
                var sides = Controller.DsState.State.ShieldSides;
                int totalSides = sides.Length;
                var maxHealth = Controller.ShieldChargeBase / totalSides;
                var side = sides[(int) shieldSide];
                offline = !side.Online;
                var currentHealth = (float)MathHelperD.Clamp(side.Charge, 0, maxHealth);

                var ratioToFull = currentHealth / maxHealth;
                return ratioToFull;
            }

            private void GetFace(Vector3D pos, out Session.ShieldSides closestFaceHit)
            {
                closestFaceHit = Session.ShieldSides.Forward;
                double minDistance = double.MaxValue;
                for (int i = 0; i < 6; i++)
                {
                    var faceDirection = Controller.DetectMatrixOutside.Forward;
                    switch (i)
                    {
                        case 1:
                            faceDirection = -Controller.DetectMatrixOutside.Forward;
                            break;
                        case 2:
                            faceDirection = Controller.DetectMatrixOutside.Left;
                            break;
                        case 3:
                            faceDirection = -Controller.DetectMatrixOutside.Left;
                            break;
                        case 4:
                            faceDirection = Controller.DetectMatrixOutside.Up;
                            break;
                        case 5:
                            faceDirection = -Controller.DetectMatrixOutside.Up;
                            break;
                    }

                    var faceCenter = Controller.WorldEllipsoidCenter + faceDirection;
                    var facePlane = new PlaneD(faceCenter, Vector3D.Normalize(faceDirection));
                    var distanceToFace = Math.Abs(facePlane.DistanceToPoint(pos));
                    if (distanceToFace < minDistance)
                    {
                        minDistance = distanceToFace;
                        closestFaceHit = Controller.RealSideStates[(Session.ShieldSides)i].Side;
                    }
                }
            }

            internal void ClearDamageTypeInfo()
            {
                NormalAverage.Clear();
                KineticDamage = 0;
                EnergyDamage = 0;
                RawKineticDamage = 0;
                RawEnergyDamage = 0;
                ModKineticDamage = 0;
                ModEnergyDamage = 0;
                AverageNormDamage = 0;
                Absorb = 0f;
            }

            internal void Clear()
            {
                ClearDamageTypeInfo();

                
                var sides = Controller.DsState.State.ShieldSides;
                var maxSides = sides.Length;
                for (int i = 0; i < maxSides; i++)
                {
                    var sideId = (Session.ShieldSides)i;
                    var side = sides[(int)sideId];

                    if (Session.Instance.IsServer)
                    {
                        side.Online = false;
                        side.NextOnline = Session.Instance.Tick;
                    }
                    side.Charge = 0;
                    side.Absorb = 0;
                }
                SidesOnline = 0;
                Controller.StateChangeRequest = true;
            }
        }

    }
}
