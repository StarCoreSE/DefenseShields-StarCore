using System;
using System.Collections.Generic;
using DefenseShields.Support;
using GjkShapes;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Noise.Patterns;
using VRage.Utils;
using VRageMath;
using static DefenseShields.DefenseShields;
using static VRage.Game.MyObjectBuilder_BlockNavigationDefinition;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        private void LosCheck()
        {
            LosCheckTick = uint.MaxValue;
            ShieldComp.CheckEmitters = true;
            FitChanged = true;
            _adjustShape = true;
        }

        private void Debug()
        {
            var name = Shield.CustomName;
            if (name.Length == 5 && name == "DEBUG")
            {
                if (_tick <= 1800) Shield.CustomName = "DEBUGAUTODISABLED";
                else UserDebug();
            }
        }

        private void UserDebug()
        {
            var active = Session.Instance.ActiveShields.ContainsKey(this);
            var message = $"User({MyAPIGateway.Multiplayer.Players.TryGetSteamId(Shield.OwnerId)}) Debugging\n" +
                          $"On:{DsState.State.Online} - Sus:{DsState.State.Suspended} - Act:{active}\n" +
                          $"Sleep:{Asleep} - Tick/Woke:{_tick}/{LastWokenTick}\n" +
                          $"Mode:{DsState.State.Mode} - Waking:{DsState.State.Waking}\n" +
                          $"Low:{DsState.State.Lowered} - Sl:{DsState.State.Sleeping}\n" +
                          $"Failed:{!NotFailed} - PNull:{MyResourceDist == null}\n" +
                          $"NoP:{DsState.State.NoPower} - PSys:{MyResourceDist?.SourcesEnabled}\n" +
                          $"Access:{DsState.State.ControllerGridAccess} - EmitterLos:{DsState.State.EmitterLos}\n" +
                          $"ProtectedEnts:{ProtectedEntCache.Count} - ProtectMyGrid:{Session.Instance.GlobalProtect.ContainsKey(MyGrid)}\n" +
                          $"ShieldMode:{ShieldMode} - pFail:{_powerFail}\n" +
                          $"Sink:{_sink.CurrentInputByType(GId)} - PFS:{_powerNeeded}/{ShieldMaxPower}\n" +
                          $"AvailPoW:{ShieldAvailablePower} - MTPoW:{_shieldMaintaintPower}\n" +
                          $"Pow:{_power} HP:{DsState.State.Charge}: {ShieldMaxCharge}";

            if (!_isDedicated) MyAPIGateway.Utilities.ShowNotification(message, 28800);
            else Log.Line(message);
        }

        private readonly List<IMyCubeGrid> _tempSubGridList = new List<IMyCubeGrid>();


        private bool SubGridUpdateSkip()
        {
            _tempSubGridList.Clear();
            MyAPIGateway.GridGroups.GetGroup(MyGrid, GridLinkTypeEnum.Physical, _tempSubGridList);

            var newCount = _tempSubGridList.Count;
            var sameCount = newCount == _linkedGridCount;
            var oneAndSame = newCount == 1 && sameCount;

            if (oneAndSame && ShieldComp.LinkedGrids.ContainsKey(MyGrid))
                return true;

            if (sameCount) {

                for (int i = 0; i < _tempSubGridList.Count; i++) {
                    if (!ShieldComp.LinkedGrids.ContainsKey((MyCubeGrid)_tempSubGridList[i]))
                        return false;
                }
            }
            else return false;

            return true;
        }

        private void UpdateSubGrids()
        {
            var subUpdate = _subUpdate;
            _subUpdate = false;
            
            if (_subUpdatedTick  == _tick || SubGridUpdateSkip())
                return;

            if (!_checkResourceDist && subUpdate)
                _checkResourceDist = true;
            
            _subUpdatedTick = _tick;
            ShieldComp.LinkedGrids.Clear();

            foreach (var s in ShieldComp.SubGrids.Keys) 
                Session.Instance.IdToBus.Remove(s.EntityId);

            ShieldComp.SubGrids.Clear();

            for (int i = 0; i < _tempSubGridList.Count; i++) {

                var sub = _tempSubGridList[i];
                if (sub == null) continue;
                sub.Flags |= (EntityFlags)(1 << 31);

                if (MyGrid.IsSameConstructAs(sub)) {
                    ShieldComp.SubGrids[(MyCubeGrid)sub] = byte.MaxValue;
                    Session.Instance.IdToBus[sub.EntityId] = ShieldComp;
                }

                ShieldComp.LinkedGrids.TryAdd((MyCubeGrid)sub, byte.MaxValue);
            }

            _linkedGridCount = ShieldComp.LinkedGrids.Count;
            _blockChanged = true;
            _subTick = _tick;
        }

        private void BlockMonitor()
        {
            if (_blockChanged)
            {
                _blockEvent = true;
                _shapeEvent = true;
                LosCheckTick = _tick + 1800;

                if (_isServer && _delayedCapTick == uint.MaxValue)
                    _delayedCapTick = _tick + 600;

                if (_blockAdded) _shapeTick = _tick + 300;
                else _shapeTick = _tick + 1800;
            }

            if (_functionalAdded || _functionalRemoved)
            {
                _functionalAdded = false;
                _functionalRemoved = false;
            }

            _blockChanged = false;
            _blockAdded = false;
        }

        private void BlockChanged(bool backGround)
        {
            if (_blockEvent)
            {
                if (DsState.State.Sleeping || DsState.State.Suspended) return;

                _blockEvent = false;
                _funcTick = _tick + 60;
            }
        }


        private void GridOwnsController()
        {
            if (MyGrid.BigOwners.Count == 0)
            {
                DsState.State.ControllerGridAccess = false;
                return;
            }

            _gridOwnerId = MyGrid.BigOwners[0];
            _controllerOwnerId = MyCube.OwnerId;

            if (_controllerOwnerId == 0) MyCube.ChangeOwner(_gridOwnerId, MyOwnershipShareModeEnum.Faction);

            var controlToGridRelataion = MyCube.GetUserRelationToOwner(_gridOwnerId);
            DsState.State.InFaction = controlToGridRelataion == MyRelationsBetweenPlayerAndBlock.FactionShare;
            DsState.State.IsOwner = controlToGridRelataion == MyRelationsBetweenPlayerAndBlock.Owner;

            if (controlToGridRelataion != MyRelationsBetweenPlayerAndBlock.Owner && controlToGridRelataion != MyRelationsBetweenPlayerAndBlock.FactionShare)
            {
                if (DsState.State.ControllerGridAccess)
                {
                    DsState.State.ControllerGridAccess = false;
                    Shield.RefreshCustomInfo();
                    if (Session.Enforced.Debug == 4) Log.Line($"GridOwner: controller is not owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
                }
                DsState.State.ControllerGridAccess = false;
                return;
            }

            if (!DsState.State.ControllerGridAccess)
            {
                DsState.State.ControllerGridAccess = true;
                Shield.RefreshCustomInfo();
                if (Session.Enforced.Debug == 4) Log.Line($"GridOwner: controller is owned: {ShieldMode} - ShieldId [{Shield.EntityId}]");
            }
            DsState.State.ControllerGridAccess = true;
        }

        private bool SubGridSlaveControllerLink()
        {
            var notTime = !_tick60 && _subTick + 10 < _tick;
            if (notTime && _slavedToGrid != null) return true;
            if (IsStatic || (notTime && !_firstLoop)) return false;

            var mySize = MyGrid.PositionComp.LocalAABB.Size.Volume;
            var myEntityId = MyGrid.EntityId;
            foreach (var grid in ShieldComp.LinkedGrids.Keys)
            {
                if (grid == MyGrid) continue;
                ShieldGridComponent shieldComponent;
                if (grid.Components.TryGet(out shieldComponent) && shieldComponent?.DefenseShields != null && shieldComponent.DefenseShields.MyCube.IsWorking) {

                    var ds = shieldComponent.DefenseShields;
                    var otherSize = ds.MyGrid.PositionComp.LocalAABB.Size.Volume;
                    var otherEntityId = ds.MyGrid.EntityId;
                    if ((!IsStatic && ds.IsStatic) || mySize < otherSize || MyUtils.IsEqual(mySize, otherSize) && myEntityId < otherEntityId)
                    {
                        _slavedToGrid = ds.MyGrid;
                        if (_slavedToGrid != null)
                        {
                            if (_isServer && !IsStatic && !ds.IsStatic && DsState.State.Charge > 0 && _slavedToGrid.GridSizeEnum == MyGrid.GridSizeEnum)
                                ChargeMgr.SetCharge(0, ShieldChargeMgr.ChargeMode.Zero);
                            return true;
                        }
                    }
                }
            }

            if (_slavedToGrid != null) {

                if (_slavedToGrid.IsInSameLogicalGroupAs(MyGrid))
                {
                    ResetEntityTick = _tick + 1800;
                }
            }
            _slavedToGrid = null;
            return false;
        }

        private readonly List<MyEntity> _fieldBlockerEntities = new List<MyEntity>();
        private readonly List<Triangle3d> _triangles = new List<Triangle3d>();
        private uint _lastFieldCheckTick;
        private bool _lastFieldCheckState;
        private bool FieldShapeBlocked()
        {
            _triangles.Clear();
            Icosphere.ReturnLowPhysicsTris(DetectMatrixOutside, 2, _triangles);
            //DsDebugDraw.DrawTris(_triangles);
            if (_lastFieldCheckState && Session.Instance.Tick - _lastFieldCheckTick < 120)
                return true;

            _lastFieldCheckTick = Session.Instance.Tick;

            _fieldBlockerEntities.Clear();
            var pruneSphere = new BoundingSphereD(WorldEllipsoidCenter, BoundingRange);
            var ignoreVoxels = Session.Enforced.DisableVoxelSupport == 1 || ShieldComp.Modulator == null || ShieldComp.Modulator.ModSet.Settings.ModulateVoxels;
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, _fieldBlockerEntities);
            for (int i = _fieldBlockerEntities.Count - 1; i >= 0; i--)
            {
                var ent = _fieldBlockerEntities[i];
                var grid = ent as MyCubeGrid;
                var voxel = !ignoreVoxels ? ent as MyVoxelBase : null;
                if (grid == null && voxel == null || grid != null && (grid.IsStatic ||  grid.IsInSameLogicalGroupAs(MyGrid) || !GridEnemy(grid)) || voxel != null && !GridIsMobile)
                    _fieldBlockerEntities.RemoveAtFast(i);
            }

            if (_fieldBlockerEntities.Count > 0)
            {
                var blocks = new List<IMySlimBlock>();


                bool gridIntersect = false;
                bool voxelIntersect = false;
                foreach (var entity in _fieldBlockerEntities)
                {
                    if (voxelIntersect || gridIntersect)
                        break;

                    var voxel = entity as MyVoxelBase;
                    var grid = entity as MyCubeGrid;
                    if (voxel != null)
                    {
                        if (voxel.RootVoxel == null || voxel != voxel.RootVoxel) continue;
                        if (CustomCollision.VoxelContact(ShieldComp.PhysicsOutsideLow, voxel))
                            voxelIntersect = true;
                    }
                    else if (grid != null)
                    {
                        blocks.Clear();
                        GetBlocksInsideSphereFastBasic(grid, ref pruneSphere, blocks);
                        var gQuaternion = Quaternion.CreateFromRotationMatrix(grid.PositionComp.WorldMatrixRef);
                        //Dsutil1.Sw.Restart();

                        for (int x = 0; x < blocks.Count; x++)
                        {
                            var block = blocks[x];
                            Vector3D center;
                            Vector3D halfExtents;
                            if (block.FatBlock != null)
                            {
                                halfExtents = block.FatBlock.LocalAABB.HalfExtents;
                                center = block.FatBlock.WorldAABB.Center;
                            }
                            else
                            {
                                Vector3 halfExt;
                                block.ComputeScaledHalfExtents(out halfExt);
                                halfExtents = halfExt;
                                block.ComputeWorldCenter(out center);
                            }
                            
                            var blockObb = new MyOrientedBoundingBoxD(center, halfExtents, gQuaternion);
                            var boxSphereRadiusSquared = halfExtents.LengthSquared();
                            _obbEdges[0] = blockObb.Orientation.Forward * (float)blockObb.HalfExtent.Z;
                            _obbEdges[1] = blockObb.Orientation.Up * (float)blockObb.HalfExtent.Y;
                            _obbEdges[2] = blockObb.Orientation.Right * (float)blockObb.HalfExtent.X;

                            for (int i = 0; i < _triangles.Count; i++)
                            {
                                var tri = _triangles[i];

                                Vector3D triangleCenter = (tri.V0 + tri.V1 + tri.V2) / 3;
                                double triangleRadiusSquared = Math.Max(Math.Max((tri.V0 - triangleCenter).LengthSquared(), (tri.V1 - triangleCenter).LengthSquared()), (tri.V2 - triangleCenter).LengthSquared());
                                double distanceBetweenCentersSquared = (center - triangleCenter).LengthSquared();

                                var spheresTouch = distanceBetweenCentersSquared <= (boxSphereRadiusSquared + triangleRadiusSquared);

                                if (spheresTouch)
                                {
                                    _triEdges[0] = tri.V1 - tri.V0;
                                    _triEdges[1] = tri.V2 - tri.V1;
                                    _triEdges[2] = tri.V0 - tri.V2;

                                    if (CustomCollision.CheckObbTriIntersection(blockObb, tri, _obbEdges, _triEdges))
                                    {
                                        //DsDebugDraw.DrawBox(blockObb, Color.Red);
                                        gridIntersect = true;
                                        break;
                                    }
                                }
                            }

                            if (gridIntersect)
                                break;
                        }
                        //Dsutil1.StopWatchReport("test", -1);
                    }
                }

                if (gridIntersect || voxelIntersect)
                {
                    if (voxelIntersect)
                        Shield.Enabled = false;

                    if (!_lastFieldCheckState)
                    {
                        DsState.State.FieldBlocked = true;
                        _sendMessage = true;
                    }

                    if (Session.Enforced.Debug == 3) Log.Line($"Field blocked: - ShieldId [{Shield.EntityId}]");
                    _lastFieldCheckState = true;

                    return _lastFieldCheckState;
                }

            }
            
            DsState.State.FieldBlocked = false;
            _lastFieldCheckState = false;
            return _lastFieldCheckState;
        }

        private void FailureDurations()
        {


            if (_overLoadLoop == 0 || _reModulationLoop == 0)
            {
                if (DsState.State.Online || !WarmedUp)
                {
                    if (_overLoadLoop != -1)
                    {
                        DsState.State.Overload = true;
                        _sendMessage = true;
                    }

                    if (_reModulationLoop != -1)
                    {
                        DsState.State.Remodulate = true;
                        _sendMessage = true;
                    }
                }
            }

            if (_reModulationLoop > -1)
            {
                _reModulationLoop++;
                if (_reModulationLoop == ReModulationCount)
                {
                    DsState.State.Remodulate = false;
                    _reModulationLoop = -1;
                }
            }

            if (_overLoadLoop > -1)
            {
                _overLoadLoop++;
                if (_overLoadLoop == Session.Enforced.OverloadTime - 1) ShieldComp.CheckEmitters = true;
                if (_overLoadLoop == Session.Enforced.OverloadTime)
                {
                    if (!DsState.State.EmitterLos)
                    {
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                    }
                    else
                    {
                        DsState.State.Overload = false;
                        _overLoadLoop = -1;
                        ChargeMgr.SetCharge(ShieldMaxCharge * 0.35f, ShieldChargeMgr.ChargeMode.Set);
                    }
                }
            }
        }
    }
}
