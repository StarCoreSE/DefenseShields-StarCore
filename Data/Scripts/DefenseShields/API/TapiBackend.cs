using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using System.Collections.Generic;
using System.Collections.Immutable;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static VRage.Game.MyObjectBuilder_SessionComponentMission;
using static VRage.Game.MyObjectBuilder_BehaviorTreeDecoratorNode;

namespace DefenseShields
{
    internal class ApiBackend
    {
        private static readonly MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>> SegmentPool = new MyConcurrentPool<List<MyLineSegmentOverlapResult<MyEntity>>>(10);

        internal readonly ImmutableDictionary<string, Delegate> TamperSafeModDict;
        internal readonly ImmutableDictionary<string, Delegate> TamperSafePbDict;

        internal readonly Dictionary<string, Delegate> ModApiMethods = new Dictionary<string, Delegate>()
        {
            ["RayAttackShield"] = new Func<IMyTerminalBlock, RayD, long, float, bool, bool, Vector3D?>(TAPI_RayAttackShield),
            ["LineAttackShield"] = new Func<IMyTerminalBlock, LineD, long, float, bool, bool, Vector3D?>(TAPI_LineAttackShield),
            ["IntersectEntToShieldFast"] = new Func<List<MyEntity>, RayD, bool, bool, long, float, MyTuple<bool, float>>(TAPI_IntersectEntToShieldFast),
            ["PointAttackShield"] = new Func<IMyTerminalBlock, Vector3D, long, float, bool, bool, bool, bool>(TAPI_PointAttackShield),
            ["PointAttackShieldExt"] = new Func<IMyTerminalBlock, Vector3D, long, float, bool, bool, bool, float?>(TAPI_PointAttackShieldExt),
            ["PointAttackShieldCon"] = new Func<IMyTerminalBlock, Vector3D, long, float, float, bool, bool, bool, float?>(TAPI_PointAttackShieldCon),
            ["PointAttackShieldHeat"] = new Func<IMyTerminalBlock, Vector3D, long, float, float, bool, bool, bool, float, float?>(TAPI_PointAttackShieldHeat),
            ["SetShieldHeat"] = new Action<IMyTerminalBlock, int>(TAPI_SetShieldHeat),
            ["SetSkipLos"] = new Action<IMyTerminalBlock>(TAPI_SetSkipLos),
            ["OverLoadShield"] = new Action<IMyTerminalBlock>(TAPI_OverLoadShield),
            ["SetCharge"] = new Action<IMyTerminalBlock, float>(TAPI_SetCharge),
            ["RayIntersectShield"] = new Func<IMyTerminalBlock, RayD, Vector3D?>(TAPI_RayIntersectShield),
            ["LineIntersectShield"] = new Func<IMyTerminalBlock, LineD, Vector3D?>(TAPI_LineIntersectShield),
            ["PointInShield"] = new Func<IMyTerminalBlock, Vector3D, bool>(TAPI_PointInShield),
            ["GetShieldPercent"] = new Func<IMyTerminalBlock, float>(TAPI_GetShieldPercent),
            ["GetShieldHeat"] = new Func<IMyTerminalBlock, int>(TAPI_GetShieldHeatLevel),
            ["GetChargeRate"] = new Func<IMyTerminalBlock, float>(TAPI_GetChargeRate),
            ["HpToChargeRatio"] = new Func<IMyTerminalBlock, int>(TAPI_HpToChargeRatio),
            ["GetMaxCharge"] = new Func<IMyTerminalBlock, float>(TAPI_GetMaxCharge),
            ["GetCharge"] = new Func<IMyTerminalBlock, float>(TAPI_GetCharge),
            ["GetPowerUsed"] = new Func<IMyTerminalBlock, float>(TAPI_GetPowerUsed),
            ["GetPowerCap"] = new Func<IMyTerminalBlock, float>(TAPI_GetPowerCap),
            ["GetMaxHpCap"] = new Func<IMyTerminalBlock, float>(TAPI_GetMaxHpCap),
            ["IsShieldUp"] = new Func<IMyTerminalBlock, bool>(TAPI_IsShieldUp),
            ["ShieldStatus"] = new Func<IMyTerminalBlock, string>(TAPI_ShieldStatus),
            ["EntityBypass"] = new Func<IMyTerminalBlock, IMyEntity, bool, bool>(TAPI_EntityBypass),
            ["GridHasShield"] = new Func<IMyCubeGrid, bool>(TAPI_GridHasShield),
            ["GridShieldOnline"] = new Func<IMyCubeGrid, bool>(TAPI_GridShieldOnline),
            ["ProtectedByShield"] = new Func<IMyEntity, bool>(TAPI_ProtectedByShield),
            ["GetShieldBlock"] = new Func<IMyEntity, IMyTerminalBlock>(TAPI_GetShieldBlock),
            ["MatchEntToShieldFast"] = new Func<IMyEntity, bool, IMyTerminalBlock>(TAPI_MatchEntToShieldFast),
            ["MatchEntToShieldFastExt"] = new Func<MyEntity, bool, MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>>?>(TAPI_MatchEntToShieldFastExt),
            ["MatchEntToShieldFastDetails"] = new Func<MyEntity, bool, MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>, MyTuple<bool, bool, float, float>>?>(TAPI_MatchEntToShieldFastDetails),
            ["ClosestShieldInLine"] = new Func<LineD, bool, MyTuple<float?, IMyTerminalBlock>>(TAPI_ClosestShieldInLine),
            ["IsShieldBlock"] = new Func<IMyTerminalBlock, bool>(TAPI_IsShieldBlock),
            ["GetClosestShield"] = new Func<Vector3D, IMyTerminalBlock>(TAPI_GetClosestShield),
            ["GetDistanceToShield"] = new Func<IMyTerminalBlock, Vector3D, double>(TAPI_GetDistanceToShield),
            ["GetClosestShieldPoint"] = new Func<IMyTerminalBlock, Vector3D, Vector3D?>(TAPI_GetClosestShieldPoint),
            ["GetShieldInfo"] = new Func<MyEntity, MyTuple<bool, bool, float, float, float, int>>(TAPI_GetShieldInfo),
            ["GetModulationInfo"] = new Func<MyEntity, MyTuple<bool, bool, float, float>>(TAPI_GetModulationInfo),
            ["GetFaceInfo"] = new Func<IMyTerminalBlock, Vector3D, bool, MyTuple<bool, int, int, float, float>>(TAPI_GetFaceInfo),
            ["GetFaceInfoAndPenChance"] = new Func<IMyTerminalBlock, Vector3D, bool, MyTuple<bool, int, int, float, float, float>>(TAPI_GetFaceInfoAndPenChance),
            ["GetFacesFast"] = new Func<MyEntity, MyTuple<bool, Vector3I>>(TAPI_GetFacesFast),
            ["AddAttacker"] = new Action<long>(TAPI_AddAttacker),
            ["IsBlockProtected"] = new Func<IMySlimBlock, bool>(TAPI_IsBlockProtected),
            ["GetLastAttackers"] = new Action<MyEntity, ICollection<MyTuple<long, float, uint>>>(TAPI_GetLastAttackers),
            ["IsFortified"] = new Func<IMyTerminalBlock, bool>(TAPI_IsFortified),
        };

        private readonly Dictionary<string, Delegate> _terminalPbApiMethods = new Dictionary<string, Delegate>()
        {
            ["RayIntersectShield"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, RayD, Vector3D?>(TAPI_RayIntersectShield),
            ["LineIntersectShield"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, LineD, Vector3D?>(TAPI_LineIntersectShield),
            ["PointInShield"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, Vector3D, bool>(TAPI_PointInShield),
            ["GetShieldPercent"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetShieldPercent),
            ["GetShieldHeat"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int>(TAPI_GetShieldHeatLevel),
            ["GetChargeRate"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetChargeRate),
            ["HpToChargeRatio"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int>(TAPI_HpToChargeRatio),
            ["GetMaxCharge"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetMaxCharge),
            ["GetCharge"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetCharge),
            ["GetPowerUsed"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetPowerUsed),
            ["GetPowerCap"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetPowerCap),
            ["GetMaxHpCap"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(TAPI_GetMaxHpCap),
            ["IsShieldUp"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool>(TAPI_IsShieldUp),
            ["ShieldStatus"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, string>(TAPI_ShieldStatus),
            ["EntityBypass"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, VRage.Game.ModAPI.Ingame.IMyEntity, bool, bool>(TAPI_EntityBypass),
            ["EntityBypassPb"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, bool, bool>(TAPI_EntityBypass),
            ["GridHasShield"] = new Func<VRage.Game.ModAPI.Ingame.IMyCubeGrid, bool>(TAPI_GridHasShield),
            ["GridShieldOnline"] = new Func<VRage.Game.ModAPI.Ingame.IMyCubeGrid, bool>(TAPI_GridShieldOnline),
            ["ProtectedByShield"] = new Func<VRage.Game.ModAPI.Ingame.IMyEntity, bool>(TAPI_ProtectedByShield),
            ["GetShieldBlock"] = new Func<VRage.Game.ModAPI.Ingame.IMyEntity, Sandbox.ModAPI.Ingame.IMyTerminalBlock>(TAPI_GetShieldBlock),
            ["IsShieldBlock"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool>(TAPI_IsShieldBlock),
            ["GetClosestShield"] = new Func<Vector3D, Sandbox.ModAPI.Ingame.IMyTerminalBlock>(TAPI_GetClosestShieldPb), // need to switch to entityId
            ["GetDistanceToShield"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, Vector3D, double>(TAPI_GetDistanceToShield),
            ["GetClosestShieldPoint"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, Vector3D, Vector3D?>(TAPI_GetClosestShieldPoint),
        };


        internal readonly Dictionary<string, Delegate> Retired = new Dictionary<string, Delegate>()
        {
        };

        public ApiBackend()
        {
            var builderMod = ImmutableDictionary.CreateBuilder<string, Delegate>();

            var builderPb = ImmutableDictionary.CreateBuilder<string, Delegate>();

            foreach (var pair in ModApiMethods)
            {
                builderMod.Add(pair.Key, pair.Value);
            }

            foreach (var pair in _terminalPbApiMethods)
            {
                builderPb.Add(pair.Key, pair.Value);
            }

            TamperSafeModDict = builderMod.ToImmutable();

            TamperSafePbDict = builderPb.ToImmutable();
        }

        internal bool DetectedTampering(IReadOnlyDictionary<string, Delegate> extDict = null)
        {
            var dictToCheck = extDict ?? ModApiMethods;
            foreach (var pair in dictToCheck)
            {
                if (pair.Value != TamperSafeModDict[pair.Key])
                {
                    Log.Line($"API tampering detected, shutting down: {pair.Key}");
                    Session.Instance.ShutDown = true;
                    return true;
                }
            }

            return false;
        }

        internal void Init()
        {
            var mod = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("DefenseSystemsAPI");
            mod.Getter = (b) => Retired;
            MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(mod);

            var pb = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, IMyTerminalBlock>("DefenseSystemsPbAPI");
            pb.Getter = (b) => TamperSafePbDict;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
        }

        // ModApi only methods below
        private static Vector3D? TAPI_RayAttackShield(IMyTerminalBlock block, RayD ray, long attackerId, float damage, bool energy, bool drawParticle)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return null;

            var intersectDist = CustomCollision.IntersectEllipsoid(ref logic.DetectMatrixOutsideInv, logic.DetectMatrixOutside, ref ray);

            if (!intersectDist.HasValue) return null;
            var ellipsoid = intersectDist ?? 0;
            var pos = ray.Position + (ray.Direction * ellipsoid);

            var result = TAPI_DamageShield(logic, pos, energy, damage, drawParticle);
            UpdateLastAttackers(logic, attackerId, result);

            return pos;
        }

        private static Vector3D? TAPI_LineAttackShield(IMyTerminalBlock block, LineD line, long attackerId, float damage, bool energy, bool drawParticle)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return null;

            var ray = new RayD(line.From, line.Direction);
            var intersectDist = CustomCollision.IntersectEllipsoid(ref logic.DetectMatrixOutsideInv, logic.DetectMatrixOutside, ref ray);

            if (!intersectDist.HasValue) return null;
            var ellipsoid = intersectDist ?? 0;
            if (ellipsoid > line.Length) return null;

            var pos = ray.Position + (ray.Direction * ellipsoid);

            var result = TAPI_DamageShield(logic, pos, energy, damage, drawParticle);
            UpdateLastAttackers(logic, attackerId, result);

            return pos;
        }

        private static bool TAPI_PointAttackShield(IMyTerminalBlock block, Vector3D pos, long attackerId, float damage, bool energy, bool drawParticle, bool posMustBeInside = false)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return false;
            if (posMustBeInside)
                if (!CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv)) return false;

            var result = TAPI_DamageShield(logic, pos, energy, damage, drawParticle);
            UpdateLastAttackers(logic, attackerId, result);

            return true;
        }

        private static float? TAPI_PointAttackShieldExt(IMyTerminalBlock block, Vector3D pos, long attackerId, float damage, bool energy, bool drawParticle, bool posMustBeInside = false)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return null;
            if (posMustBeInside)
                if (!CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv)) return null;

            var result = TAPI_DamageShield(logic, pos, energy, damage, drawParticle);
            UpdateLastAttackers(logic, attackerId, result);
            return result;
        }

        private static float TAPI_DamageShield(DefenseShields logic, Vector3D pos, bool energy, float damage, bool drawParticle)
        {
            if (energy)
                damage *= logic.DsState.State.ModulateKinetic;
            else
                damage *= logic.DsState.State.ModulateEnergy;

            logic.ChargeMgr.DoDamage(damage, damage, energy, pos, drawParticle, true);
            return damage;
        }

        private static void UpdateLastAttackers(DefenseShields ds, long attackerId, float damage)
        {
            var index = ds.LastIndex;
            var newAttacker = ds.LastAttackerId != attackerId && !ds.AttackerLookupCache.TryGetValue(attackerId, out index);

            if (!newAttacker) {
                ds.AttackerDamage[index] += damage;
                ds.AttackerTimes[index] = Session.Instance.Tick;
            }
            else {

                ds.LastAttackerId = attackerId;

                if (++ds.LastIndex < 10) {
                    ds.AttackerLookupCache[ds.LastAttackerId] = ds.LastIndex;
                    ds.AttackerDamage[ds.LastIndex] = damage;
                    ds.AttackerTimes[ds.LastIndex] = Session.Instance.Tick;

                }
                else {
                    ds.LastIndex = 0;
                    ds.AttackerLookupCache[ds.LastAttackerId] = ds.LastIndex;
                    ds.AttackerDamage[ds.LastIndex] = damage;
                    ds.AttackerTimes[ds.LastIndex] = Session.Instance.Tick;
                }

                if (ds.AttackerLast.Count >= 10)
                    ds.AttackerLookupCache.Remove(ds.AttackerLast.Dequeue());

                ds.AttackerLast.Enqueue(attackerId);
            }
        }

        private static bool TAPI_IsFortified(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return false;

            var fortify = logic.DsSet.Settings.FortifyShield && logic.DsState.State.Enhancer;
            return logic.DsState.State.Online && !logic.DsState.State.Lowered && fortify;
        }


        private static MyTuple<bool, Vector3I> TAPI_GetFacesFast(MyEntity entity)
        {
            ShieldGridComponent c;
            if (entity != null  && Session.Instance.IdToBus.TryGetValue(entity.EntityId, out c) && c?.DefenseShields != null && c.DefenseShields.DsSet.Settings.SideShunting)
            {
                return new MyTuple<bool, Vector3I>(true, c.DefenseShields.ShieldRedirectState);
            }
            return new MyTuple<bool, Vector3I>();
        }

        private static MyTuple<bool, int, int, float, float> TAPI_GetFaceInfo(IMyTerminalBlock block, Vector3D pos, bool posMustBeInside = false)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null)
                return new MyTuple<bool, int, int, float, float>();


            if (posMustBeInside && !CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv))
                return new MyTuple<bool, int, int, float, float>();

            var result = TAPI_GetFaceInfoAndPenChance(block, pos, posMustBeInside);
            return new MyTuple<bool, int, int, float, float>(result.Item1, result.Item2, result.Item3, result.Item4, result.Item5);
        }


        private static MyTuple<bool, int, int, float, float, float> TAPI_GetFaceInfoAndPenChance(IMyTerminalBlock block, Vector3D pos, bool posMustBeInside = false)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;

            if (Session.Instance.Api.ModApiMethods["GetFaceInfoAndPenChance"] != Session.Instance.Api.TamperSafeModDict["GetFaceInfoAndPenChance"])
            {
                if (!Session.Instance.ShutDown)
                    Log.Line($"detected api tampering");

                Session.Instance.ShutDown = true;
                return new MyTuple<bool, int, int, float, float, float>();
            }

            if (logic == null)
                return new MyTuple<bool, int, int, float, float, float>();

            if (posMustBeInside && !CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv))
                return new MyTuple<bool, int, int, float, float, float>();

            var sides = logic.DsState.State.ShieldSides;
            var penChance = 0f;
            var penStart = logic.DsSet.Settings.AutoManage ? 40f : 20f;
            var penStartThreshold = 100f - penStart;

            bool sideOffline = false;
            double minDistance = double.MaxValue;
            int shuntedFaceHit = -1;
            for (int i = 0; i < 6; i++)
            {
                var faceDirection = logic.DetectMatrixOutside.Forward;
                switch (i)
                {
                    case 1:
                        faceDirection = -logic.DetectMatrixOutside.Forward;
                        break;
                    case 2:
                        faceDirection = logic.DetectMatrixOutside.Left;
                        break;
                    case 3:
                        faceDirection = -logic.DetectMatrixOutside.Left;
                        break;
                    case 4:
                        faceDirection = logic.DetectMatrixOutside.Up;
                        break;
                    case 5:
                        faceDirection = -logic.DetectMatrixOutside.Up;
                        break;
                }

                var faceCenter = logic.WorldEllipsoidCenter + faceDirection;
                var facePlane = new PlaneD(faceCenter, Vector3D.Normalize(faceDirection));
                var distanceToFace = Math.Abs(facePlane.DistanceToPoint(pos));
                //DsDebugDraw.DrawLine(faceCenter + faceDirection, faceCenter - faceDirection, Vector4.One, 5f);
                //Log.Line($"{distanceToFace} - {Vector3D.Distance(faceCenter, logic.WorldEllipsoidCenter)}- {logic.RealSideStates[(Session.ShieldSides)i].Side.ToString()}[{(Session.ShieldSides)i}]");
                if (distanceToFace < minDistance)
                {
                    minDistance = distanceToFace;
                    var realFace = logic.RealSideStates[(Session.ShieldSides)i];
                    var faceId = (int) realFace.Side;
                    sideOffline = !sides[faceId].Online;
                    shuntedFaceHit = realFace.Redirected ? faceId : -1;
                }
            }


            //var shuntName = shuntedFaceHit >= 0 ? ((Session.ShieldSides) shuntedFaceHit).ToString() : "None";

            //MyAPIGateway.Utilities.ShowNotification($"dmg: {shuntName} - {realFace.Side.ToString()} - {sides[(int)realFace.Side].Charge}", 1000);

            //if (((IMyCubeGrid)logic.MyGrid).ControlSystem.IsControlled)
            //    Log.Line($"Api:{faceName}");

            if (sideOffline)
                return new MyTuple<bool, int, int, float, float, float>(false, 0, 0, 1f, 0, 1f);

            var hitShuntedSide = shuntedFaceHit != -1;
            var shuntedFaces = Math.Abs(logic.ShieldRedirectState.X) + Math.Abs(logic.ShieldRedirectState.Y) + Math.Abs(logic.ShieldRedirectState.Z);
            var shuntMod = !hitShuntedSide ? 1 - (shuntedFaces * Session.ShieldShuntBonus) : logic.DsSet.Settings.AutoManage ? 1 - Session.ShieldShuntBonus : 1f;
            var preventBypassMod = MathHelper.Clamp(shuntedFaces * Session.ShieldBypassBonus, 0f, 1f);

            var reinforcedPercent = hitShuntedSide ? logic.DsState.State.ShieldPercent + (shuntedFaces * 8) : logic.DsState.State.ShieldPercent;
            var heatedEnforcedPercent = reinforcedPercent / (1 + (logic.DsState.State.Heat * 0.005));

            if (heatedEnforcedPercent < penStartThreshold)
            {
                double x = MathHelperD.Clamp(heatedEnforcedPercent + penStart, 0, 100);
                double a = 0.0001;
                double b = -0.02;
                double c = 1.0;

                penChance = (float) ((a * Math.Pow(x, 2)) + (b * x) + c);
            }

            if (logic.DsSet.Settings.SideShunting)
            {
                return new MyTuple<bool, int, int, float, float, float>(hitShuntedSide, shuntedFaceHit, shuntedFaces, shuntMod, preventBypassMod, penChance);
            }

            var penScaler = logic.DsState.State.ShieldPercent / (1 + (logic.DsState.State.Heat * 0.005f));
            if (penScaler < penStartThreshold)
            {
                double x = MathHelperD.Clamp(penScaler + penStart, 0, 100);
                double a = 0.0001;
                double b = -0.02;
                double c = 1.0;

                penChance = (float)((a * Math.Pow(x, 2)) + (b * x) + c);
            }

            return new MyTuple<bool, int, int, float, float, float>(false, 0, 0, logic.DsSet.Settings.AutoManage ? 1 - Session.ShieldShuntBonus : 1f, 0, penChance);
        }

        private static float? TAPI_PointAttackShieldCon(IMyTerminalBlock block, Vector3D pos, long attackerId, float damage, float secondaryDamage, bool energy, bool drawParticle, bool posMustBeInside = false) //inlined for performance
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null || posMustBeInside && !CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv))
                return null;

            var pendingDamage = logic.ChargeMgr.Absorb > 0 ? logic.ChargeMgr.Absorb : 0;
            var primaryDamage = damage;
            var shieldHp = logic.DsState.State.Charge * DefenseShields.ConvToHp;

            if (energy)
                primaryDamage *= logic.DsState.State.ModulateKinetic;
            else
                primaryDamage *= logic.DsState.State.ModulateEnergy;

            var hpRemaining = Math.Max(-(primaryDamage - (shieldHp - pendingDamage)), -damage);

            if (hpRemaining > 0) {
                damage += secondaryDamage;
                if (energy)
                    damage *= logic.DsState.State.ModulateKinetic;
                else
                    damage *= logic.DsState.State.ModulateEnergy;
                hpRemaining = 0;
            }
            else
                damage = primaryDamage;

            var index = logic.LastIndex;
            var newAttacker = logic.LastAttackerId != attackerId && !logic.AttackerLookupCache.TryGetValue(attackerId, out index);

            if (!newAttacker) {
                logic.AttackerDamage[index] += damage;
                logic.AttackerTimes[index] = Session.Instance.Tick;
            }
            else {
                logic.LastAttackerId = attackerId;

                if (++logic.LastIndex < 100) {
                    logic.AttackerLookupCache[logic.LastAttackerId] = logic.LastIndex;
                    logic.AttackerDamage[logic.LastIndex] = damage;
                    logic.AttackerTimes[logic.LastIndex] = Session.Instance.Tick;

                }
                else {
                    logic.LastIndex = 0;
                    logic.AttackerLookupCache[logic.LastAttackerId] = logic.LastIndex;
                    logic.AttackerDamage[logic.LastIndex] = damage;
                    logic.AttackerTimes[logic.LastIndex] = Session.Instance.Tick;
                }

                if (logic.AttackerLast.Count >= 100)
                    logic.AttackerLookupCache.Remove(logic.AttackerLast.Dequeue());

                logic.AttackerLast.Enqueue(attackerId);
            }

            logic.ChargeMgr.DoDamage(damage, damage, energy, pos, drawParticle, true);

            return hpRemaining;
        }

        private static float? TAPI_PointAttackShieldHeat(IMyTerminalBlock block, Vector3D pos, long attackerId, float damage, float secondaryDamage, bool energy, bool drawParticle, bool posMustBeInside = false, float heatScaler = 1) //inlined for performance
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null || posMustBeInside && !CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv))
                return null;

            var pendingDamage = logic.ChargeMgr.Absorb > 0 ? logic.ChargeMgr.Absorb : 0;
            var primaryDamage = damage;
            var shieldHp = logic.DsState.State.Charge * DefenseShields.ConvToHp;

            if (energy)
                primaryDamage *= logic.DsState.State.ModulateKinetic;
            else
                primaryDamage *= logic.DsState.State.ModulateEnergy;

            var hpRemaining = Math.Max(-(primaryDamage - (shieldHp - pendingDamage)), -damage);

            if (hpRemaining > 0)
            {
                damage += secondaryDamage;
                if (energy)
                    damage *= logic.DsState.State.ModulateKinetic;
                else
                    damage *= logic.DsState.State.ModulateEnergy;

                hpRemaining = 0;
            }
            else
                damage = primaryDamage;

            var index = logic.LastIndex;
            var newAttacker = logic.LastAttackerId != attackerId && !logic.AttackerLookupCache.TryGetValue(attackerId, out index);

            if (!newAttacker)
            {
                logic.AttackerDamage[index] += damage;
                logic.AttackerTimes[index] = Session.Instance.Tick;
            }
            else
            {
                logic.LastAttackerId = attackerId;

                if (++logic.LastIndex < 100)
                {
                    logic.AttackerLookupCache[logic.LastAttackerId] = logic.LastIndex;
                    logic.AttackerDamage[logic.LastIndex] = damage;
                    logic.AttackerTimes[logic.LastIndex] = Session.Instance.Tick;

                }
                else
                {
                    logic.LastIndex = 0;
                    logic.AttackerLookupCache[logic.LastAttackerId] = logic.LastIndex;
                    logic.AttackerDamage[logic.LastIndex] = damage;
                    logic.AttackerTimes[logic.LastIndex] = Session.Instance.Tick;
                }

                if (logic.AttackerLast.Count >= 100)
                    logic.AttackerLookupCache.Remove(logic.AttackerLast.Dequeue());

                logic.AttackerLast.Enqueue(attackerId);
            }

            logic.ChargeMgr.DoDamage(damage, damage, energy, pos, drawParticle, true, heatScaler);

            return hpRemaining;
        }

        private static void TAPI_SetSkipLos(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic?.ShieldComp == null) return;

            logic.ShieldComp.SkipLos = true;
            logic.ShieldComp.CheckEmitters = true;
        }

        private static void TAPI_SetShieldHeat(IMyTerminalBlock block, int value)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null || !Session.Instance.IsServer) return;

            logic.DsState.State.Heat = value;
        }

        private static void TAPI_OverLoadShield(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null || !Session.Instance.IsServer) return;

            logic.ChargeMgr.SetCharge(0, DefenseShields.ShieldChargeMgr.ChargeMode.Overload);
        }


        private static void TAPI_SetCharge(IMyTerminalBlock block, float value)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null || !Session.Instance.IsServer) return;

            logic.ChargeMgr.SetCharge(value, DefenseShields.ShieldChargeMgr.ChargeMode.Set);
        }

        // ModApi and PB methods below.
        private static Vector3D? TAPI_RayIntersectShield(IMyTerminalBlock block, RayD ray)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return null;

            var intersectDist = CustomCollision.IntersectEllipsoid(ref logic.DetectMatrixOutsideInv, logic.DetectMatrixOutside, ref ray);

            if (!intersectDist.HasValue) return null;
            var ellipsoid = intersectDist ?? 0;
            return ray.Position + (ray.Direction * ellipsoid);
        }

        private static Vector3D? TAPI_LineIntersectShield(IMyTerminalBlock block, LineD line)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return null;
            var ray = new RayD(line.From, line.Direction);

            var intersectDist = CustomCollision.IntersectEllipsoid(ref logic.DetectMatrixOutsideInv, logic.DetectMatrixOutside, ref ray);

            if (!intersectDist.HasValue) return null;
            var ellipsoid = intersectDist ?? 0;
            if (ellipsoid > line.Length) return null;
            return ray.Position + (ray.Direction * ellipsoid);
        }

        private static bool TAPI_PointInShield(IMyTerminalBlock block, Vector3D pos)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return false;

            var pointInShield = CustomCollision.PointInShield(pos, logic.DetectMatrixOutsideInv);
            return pointInShield;
        }

        private static float TAPI_GetShieldPercent(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.DsState.State.ShieldPercent;
        }

        private static int TAPI_GetShieldHeatLevel(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.DsState.State.Heat;
        }

        private static int TAPI_HpToChargeRatio(IMyTerminalBlock block)
        {
            return DefenseShields.ConvToHp;
        }

        private static float TAPI_GetChargeRate(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.ShieldChargeRate * DefenseShields.ConvToDec;
        }

        private static float TAPI_GetMaxCharge(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.ShieldMaxCharge;
        }

        private static float TAPI_GetCharge(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.DsState.State.Charge;
        }

        private static float TAPI_GetPowerUsed(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.ShieldCurrentPower;
        }

        private static float TAPI_GetPowerCap(IMyTerminalBlock block)
        {
            return float.MinValue;
        }

        private static float TAPI_GetMaxHpCap(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            return logic.ShieldMaxCharge * DefenseShields.ConvToDec;
        }

        private static bool TAPI_IsShieldUp(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return false;

            return logic.DsState.State.Online && !logic.DsState.State.Lowered;
        }

        private static string TAPI_ShieldStatus(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return string.Empty;

            return logic.GetShieldStatus();
        }

        private static bool TAPI_EntityBypass(IMyTerminalBlock block, IMyEntity entity, bool remove)
        {
            var ent = (MyEntity)entity;
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null || ent == null) return false;

            var success = remove ? logic.EntityBypass.Remove(ent) : logic.EntityBypass.Add(ent);

            return success;
        }

        private static bool TAPI_GridHasShield(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            MyProtectors protectors;
            var myGrid = (MyCubeGrid)grid;

            if (Session.Instance.GlobalProtect.TryGetValue(myGrid, out protectors)) {

                foreach (var s in protectors.Shields.Keys)
                    if (s?.ShieldComp != null && s.ShieldComp.SubGrids.ContainsKey(myGrid)) return true;
            }
            return false;
        }

        private static bool TAPI_GridShieldOnline(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            MyProtectors protectors;
            var myGrid = (MyCubeGrid)grid;
            if (Session.Instance.GlobalProtect.TryGetValue(myGrid, out protectors))
            {
                foreach (var s in protectors.Shields.Keys)
                {
                    if (s?.ShieldComp != null && s.ShieldComp.SubGrids.ContainsKey(myGrid) && s.DsState.State.Online && !s.DsState.State.Lowered) return true;
                }
            }
            return false;
        }

        private static bool TAPI_ProtectedByShield(IMyEntity entity)
        {
            if (entity == null) return false;

            MyProtectors protectors;
            var ent = (MyEntity)entity;
            if (Session.Instance.GlobalProtect.TryGetValue(ent, out protectors))
            {
                if (protectors?.Shields == null) return false;

                foreach (var s in protectors.Shields.Keys)
                {
                    if (s?.DsState?.State == null) continue;
                    if (s.DsState.State.Online && !s.DsState.State.Lowered) return true;
                }
            }
            return false;
        }

        private static IMyTerminalBlock TAPI_GetShieldBlock(IMyEntity entity)
        {
            var ent = entity as MyEntity;
            if (ent == null) return null;

            MyProtectors protectors;
            if (Session.Instance.GlobalProtect.TryGetValue(ent, out protectors))
            {
                DefenseShields firstShield = null;
                var grid = ent as MyCubeGrid;
                foreach (var s in protectors.Shields.Keys)
                {
                    if (s == null) continue;

                    if (firstShield == null) firstShield = s;

                    if (grid != null && s.ShieldComp?.SubGrids != null && s.ShieldComp.SubGrids.ContainsKey(grid)) 
                       return s.MyCube as IMyTerminalBlock;
                }
                if (firstShield != null) return firstShield.MyCube as IMyTerminalBlock;
            }
            return null;
        }

        private static IMyTerminalBlock TAPI_MatchEntToShieldFast(IMyEntity entity, bool onlyIfOnline)
        {
            if (entity == null) return null;
            ShieldGridComponent c;
            if (Session.Instance.IdToBus.TryGetValue(entity.EntityId, out c) && c?.DefenseShields != null)
            {
                using (c.DefenseShields?.MyCube?.Pin())
                {
                    if (c.DefenseShields?.MyCube == null || c.DefenseShields.MyCube.MarkedForClose || onlyIfOnline && (!c.DefenseShields.DsState.State.Online || c.DefenseShields.DsState.State.Lowered) || c.DefenseShields.ReInforcedShield) return null;
                    return c.DefenseShields.Shield;
                }
            }

            return null;
        }

        private static MyTuple<bool, float> TAPI_IntersectEntToShieldFast(List<MyEntity> entities, RayD ray, bool onlyIfOnline, bool enenmyOnly = false, long requesterId = 0, float maxLengthSqr = float.MaxValue)
        {
            if (enenmyOnly)
                if (requesterId == 0)
                    return new MyTuple<bool, float>(false, 0);

            float closestOtherDist = float.MaxValue;
            float closestFriendDist = float.MaxValue;
            bool closestOther = false;
            bool closestFriend = false;

            for (int i = 0; i < entities.Count; i++) {

                var entity = entities[i];
                ShieldGridComponent c;
                if (entity != null && Session.Instance.IdToBus.TryGetValue(entity.EntityId, out c) && c?.DefenseShields?.DsState?.State != null && c.DefenseShields.MyCube != null) {
                    
                    var s = c.DefenseShields;
                    if (onlyIfOnline && (!s.DsState.State.Online || s.DsState.State.Lowered) || s.ReInforcedShield)
                        continue;


                    var normSphere = new BoundingSphereD(Vector3.Zero, 1f);
                    var kRay = new RayD(Vector3D.Zero, Vector3D.Forward);

                    var ellipsoidMatrixInv = s.DetectMatrixOutsideInv;
                    Vector3D krayPos;
                    Vector3D.Transform(ref ray.Position, ref ellipsoidMatrixInv, out krayPos);

                    Vector3D nDir;
                    Vector3D.TransformNormal(ref ray.Direction, ref ellipsoidMatrixInv, out nDir);

                    Vector3D krayDir;
                    Vector3D.Normalize(ref nDir, out krayDir);

                    kRay.Direction = krayDir;
                    kRay.Position = krayPos;
                    var nullDist = normSphere.Intersects(kRay);

                    if (!nullDist.HasValue)
                        continue;

                    var hitPos = krayPos + (krayDir * -nullDist.Value);

                    var ellipsoidMatrix = s.DetectMatrixOutside;
                    Vector3D worldHitPos;
                    Vector3D.Transform(ref hitPos, ref ellipsoidMatrix, out worldHitPos);
                    var intersectDist = Vector3.DistanceSquared(worldHitPos, ray.Position);
                    if (intersectDist <= 0 || intersectDist > maxLengthSqr)
                        continue;

                    var firstOrLast = enenmyOnly && (!closestFriend || intersectDist < closestFriendDist);
                    var notEnemyCheck = false;
                    if (firstOrLast)
                    {
                        var relationship = MyIDModule.GetRelationPlayerBlock(requesterId, s.MyCube.OwnerId);
                        var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare && relationship != MyRelationsBetweenPlayerAndBlock.Friends;
                        notEnemyCheck = !enemy;
                    }

                    if (notEnemyCheck) {
                        closestFriendDist = intersectDist;
                        closestFriend = true;
                    }
                    else {
                        closestOtherDist = intersectDist;
                        closestOther = true;
                    }
                }
            }

            if (!enenmyOnly && closestOther || closestOther && !closestFriend)
            {
                return new MyTuple<bool, float>(true, closestOtherDist);
            }

            if (closestFriend && !closestOther || closestFriendDist < closestOtherDist)
            {
                return new MyTuple<bool, float>(false, closestFriendDist);
            }

            if (!closestOther)
            {
                return new MyTuple<bool, float>(false, 0);
            }

            return new MyTuple<bool, float>(true, closestOtherDist);
        }

        private static MyTuple<bool, bool, float, float, float, int> TAPI_GetShieldInfo(MyEntity entity)
        {
            var info = new MyTuple<bool, bool, float, float, float, int>();

            if (entity == null) return info;
            ShieldGridComponent c;
            if (Session.Instance.IdToBus.TryGetValue(entity.EntityId, out c) && c?.DefenseShields != null)
            {
                var s = c.DefenseShields;
                info.Item1 = true;
                var state = s.DsState.State;
                if (state.Online)
                {
                    info.Item2 = true;
                    info.Item3 = state.Charge;
                    info.Item4 = s.ShieldMaxCharge;
                    info.Item5 = state.ShieldPercent;
                    info.Item6 = state.Heat;
                }
            }

            return info;
        }

        private static MyTuple<bool, bool, float, float> TAPI_GetModulationInfo(MyEntity entity)
        {
            var info = new MyTuple<bool, bool, float, float>();

            if (entity == null) return info;
            ShieldGridComponent c;
            if (Session.Instance.IdToBus.TryGetValue(entity.EntityId, out c) && c?.DefenseShields != null)
            {
                var s = c.DefenseShields;
                var state = s.DsState.State;
                info.Item1 = state.Enhancer && c.Modulator?.ModSet != null && c.Modulator.ModSet.Settings.ReInforceEnabled;
                info.Item2 = state.Enhancer && c.Modulator?.ModSet != null && c.Modulator.ModSet.Settings.EmpEnabled;
                info.Item3 = state.ModulateKinetic;
                info.Item4 = state.ModulateEnergy;
            }

            return info;
        }

        private static MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>>? TAPI_MatchEntToShieldFastExt(MyEntity entity, bool onlyIfOnline)
        {
            if (entity == null) return null;
            ShieldGridComponent c;
            if (Session.Instance.IdToBus.TryGetValue(entity.EntityId, out c) && c?.DefenseShields != null)
            {
                if (onlyIfOnline && (!c.DefenseShields.DsState.State.Online || c.DefenseShields.DsState.State.Lowered)) return null;
                var s = c.DefenseShields;
                var state = s.DsState.State;
                var info = new MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>>
                {
                    Item1 = s.Shield,
                    Item2 =
                    {
                        Item1 = !c.DefenseShields.ReInforcedShield,
                        Item2 = s.DsSet.Settings.SideShunting,
                        Item3 = state.Charge,
                        Item4 = s.ShieldMaxCharge,
                        Item5 = state.ShieldPercent,
                        Item6 = state.Heat,
                    },
                    Item3 = { Item1 = s.DetectMatrixOutsideInv, Item2 = s.DetectMatrixOutside }
                };
                return info;
            }
            return null;
        }

        private static MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>, MyTuple<bool, bool, float, float>>? TAPI_MatchEntToShieldFastDetails(MyEntity entity, bool onlyIfOnline)
        {
            if (entity == null) return null;
            ShieldGridComponent c;
            if (Session.Instance.IdToBus.TryGetValue(entity.EntityId, out c) && c?.DefenseShields != null)
            {
                if (onlyIfOnline && (!c.DefenseShields.DsState.State.Online || c.DefenseShields.DsState.State.Lowered)) return null;
                var s = c.DefenseShields;
                var state = s.DsState.State;
                var penStart = s.DsSet.Settings.AutoManage ? 40f : 20f;
                var penStartThreshold = 100f - penStart;
                var penScaler = s.DsState.State.ShieldPercent / (1 + (s.DsState.State.Heat * 0.005f));
                var shuntedCount = Math.Abs(s.ShieldRedirectState.X) + Math.Abs(s.ShieldRedirectState.Y) + Math.Abs(s.ShieldRedirectState.Z);
                var info = new MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>, MyTuple<bool, bool, float, float>>
                {
                    Item1 = s.Shield,
                    Item2 =
                    {
                        Item1 = !c.DefenseShields.ReInforcedShield,
                        Item2 = s.DsSet.Settings.SideShunting && shuntedCount > 0,
                        Item3 = state.Charge,
                        Item4 = s.ShieldMaxCharge,
                        Item5 = state.ShieldPercent,
                        Item6 = state.Heat,
                    },
                    Item3 = { Item1 = s.DetectMatrixOutsideInv, Item2 = s.DetectMatrixOutside },

                    Item4 = { Item1 = s.DsSet.Settings.AutoManage, Item2 = s.ChargeMgr.SidesOnline != 6, Item3 = penScaler, Item4 = penStartThreshold }
                };
                return info;
            }
            return null;
        }

        private static MyTuple<float?, IMyTerminalBlock> TAPI_ClosestShieldInLine(LineD line, bool onlyIfOnline)
        {
            var segment = SegmentPool.Get();
            MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref line, segment, MyEntityQueryType.Dynamic);
            var ray = new RayD(line.From, line.Direction);

            var closest = float.MaxValue;
            IMyTerminalBlock closestShield = null;
            for (int i = 0; i < segment.Count; i++)
            {
                var ent = segment[i].Element;
                if (ent == null || ent.Physics != null && !ent.Physics.IsPhantom) continue;
                ShieldGridComponent c;
                if (Session.Instance.IdToBus.TryGetValue(ent.EntityId, out c) && c.DefenseShields != null)
                {
                    if (onlyIfOnline && (!c.DefenseShields.DsState.State.Online || c.DefenseShields.DsState.State.Lowered)) continue;
                    var s = c.DefenseShields;
                    var intersectDist = CustomCollision.IntersectEllipsoid(ref s.DetectMatrixOutsideInv, s.DetectMatrixOutside, ref ray);
                    if (!intersectDist.HasValue) continue;
                    var ellipsoid = intersectDist ?? 0;
                    if (ellipsoid > line.Length || ellipsoid > closest || CustomCollision.PointInShield(ray.Position, s.DetectMatrixOutsideInv)) continue;
                    closest = ellipsoid;
                    closestShield = s.Shield;
                }
            }
            segment.Clear();
            SegmentPool.Return(segment);
            var response = new MyTuple<float?, IMyTerminalBlock>();
            if (closestShield == null)
            {
                response.Item1 = null;
                response.Item2 = null;
                return response;
            }
            response.Item1 = closest;
            response.Item2 = closestShield;
            return response;
        }

        private static bool TAPI_IsShieldBlock(IMyTerminalBlock block)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>();
            return logic != null;
        }

        private static IMyTerminalBlock TAPI_GetClosestShield(Vector3D pos)
        {
            MyCubeBlock cloestSBlock = null;
            var closestDist = double.MaxValue;
            foreach (var s in Session.Instance.ActiveShields.Keys)
            {
                if (Vector3D.DistanceSquared(s.WorldEllipsoidCenter, pos) > Session.Instance.SyncDistSqr) continue;

                var sInv = s.DetectMatrixOutsideInv;
                var sMat = s.DetectMatrixOutside;
                var sDist = CustomCollision.EllipsoidDistanceToPos(ref sInv, ref sMat, ref pos);
                if (sDist > 0 && sDist < closestDist)
                {
                    cloestSBlock = s.MyCube;
                    closestDist = sDist;
                }
            }
            return cloestSBlock as IMyTerminalBlock;
        }

        private static IMyTerminalBlock TAPI_GetClosestShieldPb(Vector3D pos)
        {
            return null;
        }

        private static double TAPI_GetDistanceToShield(IMyTerminalBlock block, Vector3D pos)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return -1;

            var sInv = logic.DetectMatrixOutsideInv;
            var sMat = logic.DetectMatrixOutside;
            return CustomCollision.EllipsoidDistanceToPos(ref sInv, ref sMat, ref pos);
        }

        private static Vector3D? TAPI_GetClosestShieldPoint(IMyTerminalBlock block, Vector3D pos)
        {
            var logic = block?.GameLogic?.GetAs<DefenseShields>()?.ShieldComp?.DefenseShields;
            if (logic == null) return null;

            Vector3D? closestShieldPoint = CustomCollision.ClosestEllipsoidPointToPos(ref logic.DetectMatrixOutsideInv, logic.DetectMatrixOutside, ref pos);

            return closestShieldPoint;
        }

        private static void TAPI_AddAttacker(long attacker)
        {
            Session.Instance.ManagedAttackers[attacker] = byte.MaxValue;
        }

        private static bool TAPI_IsBlockProtected(IMySlimBlock block)
        {

            if (block == null) return false;
            var grid = (MyCubeGrid)block.CubeGrid;
            ShieldGridComponent c;
            if (Session.Instance.IdToBus.TryGetValue(grid.EntityId, out c) && c?.DefenseShields != null)
            {
                var ds = c.DefenseShields;
                var pointInShield = Vector3D.Transform(grid.GridIntegerToWorld(block.Position), ref ds.DetectMatrixOutsideInv).LengthSquared() <= 1;
                return pointInShield;
            }
            return false;
        }

        private static void TAPI_GetLastAttackers(MyEntity entity, ICollection<MyTuple<long, float, uint>> collection)
        {
            collection.Clear();
            if (entity == null) return;
            ShieldGridComponent c;
            if (Session.Instance.IdToBus.TryGetValue(entity.EntityId, out c) && c?.DefenseShields != null) {

                var s = c.DefenseShields;
                foreach (var pair in s.AttackerLookupCache)
                    collection.Add(new MyTuple<long, float, uint>(pair.Key, s.AttackerDamage[pair.Value], s.AttackerTimes[pair.Value]));
            }
        }

        // PB overloads
        private static Vector3D? TAPI_RayIntersectShield(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, RayD arg2) => TAPI_RayIntersectShield(arg1 as IMyTerminalBlock, arg2);
        private static Vector3D? TAPI_LineIntersectShield(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, LineD arg2) => TAPI_LineIntersectShield(arg1 as IMyTerminalBlock, arg2);
        private static bool TAPI_PointInShield(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, Vector3D arg2) => TAPI_PointInShield(arg1 as IMyTerminalBlock, arg2);
        private static float TAPI_GetShieldPercent(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetShieldPercent(arg as IMyTerminalBlock);
        private static int TAPI_GetShieldHeatLevel(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetShieldHeatLevel(arg as IMyTerminalBlock);
        private static float TAPI_GetChargeRate(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetChargeRate(arg as IMyTerminalBlock);
        private static int TAPI_HpToChargeRatio(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_HpToChargeRatio(arg as IMyTerminalBlock);
        private static float TAPI_GetMaxCharge(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetMaxCharge(arg as IMyTerminalBlock);
        private static float TAPI_GetCharge(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetCharge(arg as IMyTerminalBlock);
        private static float TAPI_GetPowerUsed(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetPowerUsed(arg as IMyTerminalBlock);
        private static float TAPI_GetPowerCap(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetPowerCap(arg as IMyTerminalBlock);
        private static bool TAPI_IsShieldBlock(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_IsShieldBlock(arg as IMyTerminalBlock);
        private static float TAPI_GetMaxHpCap(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_GetMaxHpCap(arg as IMyTerminalBlock);
        private static string TAPI_ShieldStatus(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_ShieldStatus(arg as IMyTerminalBlock);
        private static bool TAPI_EntityBypass(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, VRage.Game.ModAPI.Ingame.IMyEntity arg2, bool arg3) =>TAPI_EntityBypass(arg1 as IMyTerminalBlock, arg2 as IMyEntity, arg3);
        private static bool TAPI_EntityBypass(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, long arg2, bool arg3) => TAPI_EntityBypass(arg1 as IMyTerminalBlock, MyEntities.GetEntityById(arg2), arg3);

        private static bool TAPI_GridHasShield(VRage.Game.ModAPI.Ingame.IMyCubeGrid arg) => TAPI_GridHasShield(arg as IMyCubeGrid);
        private static bool TAPI_GridShieldOnline(VRage.Game.ModAPI.Ingame.IMyCubeGrid arg) => TAPI_GridShieldOnline(arg as IMyCubeGrid);
        private static bool TAPI_ProtectedByShield(VRage.Game.ModAPI.Ingame.IMyEntity arg) => TAPI_ProtectedByShield(arg as IMyEntity);
        private static bool TAPI_IsShieldUp(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg) => TAPI_IsShieldUp(arg as IMyTerminalBlock);
        private static Sandbox.ModAPI.Ingame.IMyTerminalBlock TAPI_GetShieldBlock(VRage.Game.ModAPI.Ingame.IMyEntity arg) => TAPI_GetShieldBlock(arg as IMyEntity);
        private static double TAPI_GetDistanceToShield(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, Vector3D arg2) => TAPI_GetDistanceToShield(arg1 as IMyTerminalBlock, arg2);
        private static Vector3D? TAPI_GetClosestShieldPoint(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, Vector3D arg2) => TAPI_GetClosestShieldPoint(arg1 as IMyTerminalBlock, arg2);

    }
}
