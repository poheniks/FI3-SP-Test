using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using SandBox.BoardGames;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Mod_Tools_Test
{
    /*TODO:
     * Make an actual map to test the effectiveness of AI logic on a non-flat map
     *  -I expect ranged cav to need a new behavior under its BehaviorComponent. Circular skirmish will not be possible in tight locations. Perhaps raycast from each flag on init() to determine if a circular skirmish is viable?
     * Write logic for melee cav formation to steal empty points. Since they are a singular formation & not tied to the main team-level formation distribution logic, this should be a simple logic check 
     * 
     * Split this into multiple .cs
     * Rename this from Mod_Tools_Test to something more appropriate
     * 
     */
    /*BUGS:
     * Ranged cav formations will inherit foot soldiers on occasion.
     * 
     * Null reference crash on ManageFormations() called in my class TacticCapturePoint: Speculate that this happens when Formation.QuerySystem returns false on all isInfantry, isRanged, isCav, isCavRanged. 
     * 
     * Probably more that I don't know about yet
     */
    /*IMPROVEMENTS:
     * (?) Add particle emitters to show who is capturing a point when the point is neutral
     * 
     */
    /*GAMEPLAY NOTES:
     * Current AI system is limited to attacking or defending a total 6 points. Do not recommend using more than 4-5 active points. Tested up to 6 active points without issue, but leaves many points empty & AI are not smart enough to capitalize on that
     *  -AI will assign a ranged & inf formation below 3 active points. Past 3 active points, AI will alternate infantry and ranged formations between points
     *  -Hardcoded(?) formation limit is 10. Where 2 formations are designated as special, presumably for assigning to siege weapons/usable objects. Where the remaining 8 formations are used for infantry, ranged, & cav.
     *  -A possible workaround to the 10 (realistically 8) formation limit is to create multiple enemy teams
     * 
     * Maps will need to prevent snow-balling by careful placement of points & points w/ spawners 
     * 
     * Formations can get confused when assaulting a flag & the flag status changes. This can lead to losing an easily-won point if the formations & surround formations kept committing to that point
     * 
     * Ranged cav will sometimes charge directly onto points
     * Ranged cav are not fast in getting into their circular-pattern skirmish from a standstill. They will sit in place and shoot (which is okay) until it is their turn to join the column formation. 
     * Melee cav suck - no surprise here. Their current logic is primarily charging hit-and-runs. They will attempt to sit on a point if they can easily cap it (no enemies present)
     * 
     */
    /*OTHER NOTES:
     * Adding more than one lane may significantly complicate AI logic. Multi-lane maps may need to simplify so there's only ever one active capture point
     * Have troop spawners upgrade over the duration of the map.
     * Siege weapons? They could easily designate choke points throughout the map
     * Have destructible buildings instead of flag points as the objective?
     * Have transportation objectives instead of flag points as the objective? Similar to TF2's payload gamemode. Attackers can push a battering ram or cart of explosives to destroy an objective
     * 
     */
    public class Mod_Tools_TestSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage("Mod Tools Test Rev0 Loaded"));   
        }

        public override void OnMissionBehaviourInitialize(Mission mission)
        {
            base.OnMissionBehaviourInitialize(mission);
            if (mission.HasMissionBehaviour<CustomBattleAgentLogic>())
            {
                ModMissionBehavior modMissionBehavior = new ModMissionBehavior();
                mission.AddMissionBehaviour(modMissionBehavior);

                InformationManager.DisplayMessage(new InformationMessage("Mod Mission Behavior Loaded"));
            }
        }
    }

    public class ModMissionBehavior : MissionLogic
    {
        bool init = false;
        private float tick = 0f;
        private float tickThreshold = 120f;

        IEnumerable<GameEntity> scriptedEntities;
        List<InvasionFlag> flagPoints = new List<InvasionFlag>();

        public IAgentOriginBase enemyOriginBase, allyOriginBase;

        public override void OnCreated()
        {
            base.OnCreated();
            Mission.Scene.TimeOfDay = 12f;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            tick += dt;
            if (tick >= tickThreshold)
            {
                tick = 0;
                occasionalTrigger();
            }

            if (!init && Agent.Main != null)
            {
                scriptedEntities = Mission.Scene.FindEntitiesWithTag("invasion");
                foreach (GameEntity entity in scriptedEntities)
                {
                    if (entity.HasScriptOfType<InvasionFlag>())
                    {
                        flagPoints.Add(entity.GetFirstScriptOfType<InvasionFlag>());
                    }
                }

                Agent.Main.SetInvulnerable(true);
                if (Agent.Main.HasMount) Agent.Main.MountAgent.SetInvulnerable(true);

                Mission.Teams.Add(Mission.PlayerTeam.Side, isPlayerGeneral: false, isPlayerSergeant: false);
                Team allyTeam = Mission.Current.PlayerAllyTeam;
                Team enemyTeam = Mission.Current.PlayerEnemyTeam;

                TeamAIGeneral normalAI = new TeamAIGeneral(Mission.Current, allyTeam, 10f, 1f);
                Mission.PlayerAllyTeam.AddTeamAI(normalAI);
                allyTeam.ClearTacticOptions();
                allyTeam.AddTacticOption(new TacticCapturePoint(allyTeam, flagPoints));

                enemyTeam.ClearTacticOptions();
                enemyTeam.AddTacticOption(new TacticCapturePoint(enemyTeam, flagPoints));

                string troopID = "basic_inf";
                BasicCharacterObject tempCharacterObject = MBObjectManager.Instance.GetObject<BasicCharacterObject>(troopID);
                CustomBattleCombatant baseBattleCombatant = new CustomBattleCombatant(tempCharacterObject.Name, tempCharacterObject.Culture, Banner.CreateRandomBanner());
                baseBattleCombatant.AddCharacter(tempCharacterObject, 2);
                CustomBattleTroopSupplier enemyTroopSupplier = new CustomBattleTroopSupplier(baseBattleCombatant, false);
                CustomBattleTroopSupplier allyTroopSupplier = new CustomBattleTroopSupplier(baseBattleCombatant, false);
                enemyOriginBase = enemyTroopSupplier.SupplyTroops(1).First();
                allyOriginBase = allyTroopSupplier.SupplyTroops(1).First();

                init = true;
            }
        }

        public override void OnAgentFleeing(Agent affectedAgent)
        {
            base.OnAgentFleeing(affectedAgent);
            affectedAgent.StopRetreating();
        }

        private void occasionalTrigger()
        {

        }
    }

    public class BehaviorCapturePoint : BehaviorComponent
    {
        public InvasionFlag point
        {
            get;
            set;
        }

        private WorldPosition pointPosition;
        private Vec3 thisPosition;

        private bool isEnemyAtPoint = false, isThisAtPoint = false;
        public bool isAttacking
        {
            get;
            private set;
        }

        private Formation _formation;

        public BehaviorCapturePoint(Formation formation)
            : base()
        {
            Constructor(formation); //the proper constructor for the BehaviorComponent class is internal... Need to use Reflection to assign formation & _navmeshTargetPenaltyTime to prevent null pointer crashes

            _formation = formation;
            _formation.MovementOrder = base.CurrentOrder;
            _formation.FormOrder = FormOrder.FormOrderWide;
            _formation.FiringOrder = FiringOrder.FiringOrderFireAtWill;

            CalculateCurrentOrder();
        }

        private void Constructor(Formation formation)
        {
            Type behaviorType = this.GetType();
            FieldInfo behaviorFormation = behaviorType.BaseType.GetField("formation", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo behaviorNavMeshTimer = behaviorType.BaseType.GetField("_navmeshlessTargetPenaltyTime", BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo behaviorCoherence = behaviorType.BaseType.GetProperty("BehaviorCoherence", BindingFlags.NonPublic | BindingFlags.Instance);

            behaviorFormation.SetValue(this, formation);
            
            Timer navmeshlessTimer = new Timer(MBCommon.GetTime(MBCommon.TimeType.Mission), 20f);
            behaviorNavMeshTimer.SetValue(this, navmeshlessTimer);

            behaviorCoherence.SetValue(this, 0.75f);
        }

        private void Defense(WorldPosition position)
        {
            isAttacking = false;
            _formation.MovementOrder = MovementOrder.MovementOrderMove(position);
            if (isThisAtPoint & _formation.QuerySystem.IsInfantryFormation & _formation.QuerySystem.HasShield) _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderShieldWall;
            else if (isThisAtPoint & _formation.QuerySystem.IsInfantryFormation & !_formation.QuerySystem.HasShield) _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
            else if (_formation.QuerySystem.IsRangedFormation) _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
            else _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
        }
        private void CavalryRangedOffense(Formation enemyFormation)
        {
            _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderColumn;
            _formation.FormOrder = FormOrder.FormOrderWide;
            _formation.FiringOrder = FiringOrder.FiringOrderFireAtWill;
            _formation.WeaponUsageOrder = WeaponUsageOrder.WeaponUsageOrderUseAny;

            Vec2 thisAvgPos = _formation.QuerySystem.AveragePosition;
            Vec2 thisLeadingPos = _formation.GetFirstUnit().Position.AsVec2;
            Vec3 centerPos = enemyFormation.QuerySystem.MedianPosition.GetGroundVec3();

            bool canEngageEnemyCav = false;
            if ((enemyFormation.QuerySystem.IsCavalryFormation | enemyFormation.QuerySystem.IsRangedCavalryFormation) & (enemyFormation.QuerySystem.MovementSpeed < _formation.QuerySystem.MovementSpeedMaximum * 0.5f) & ((thisAvgPos - centerPos.AsVec2).Length) < _formation.QuerySystem.MissileRange * 0.6f) canEngageEnemyCav = true;
            if (canEngageEnemyCav)
            {
                _formation.MovementOrder = MovementOrderChargeToTarget(enemyFormation);
                return;
            }

            if (isEnemyAtPoint) centerPos = pointPosition.GetGroundVec3();

            float enemySeparation = (enemyFormation.QuerySystem.MedianPosition.AsVec2 - enemyFormation.QuerySystem.AveragePosition).Length;
            float estRadius = enemySeparation + (_formation.QuerySystem.MissileRange * 0.4f);
            Vec2 normalDir = (centerPos.AsVec2 - thisLeadingPos).Normalized();
            Vec2 tangentDir = (normalDir).RightVec();
            float thisVelocity = _formation.QuerySystem.CurrentVelocity.Length;
            float thisTargetVelocity = _formation.QuerySystem.MovementSpeedMaximum;
            float projectedTangentDistance = 10f + (thisTargetVelocity / Math.Max(thisVelocity, 0.2f)) * 3f;
            Vec2 nearestCircularPosition = centerPos.AsVec2 + (-normalDir * estRadius);
            Vec2 projectedTangentPosition = nearestCircularPosition + (tangentDir * projectedTangentDistance);
            _formation.MovementOrder = MovementOrder.MovementOrderMove(projectedTangentPosition.ToVec3().ToWorldPosition());
        }
        private void CavalryOffense(Formation enemyFormation)
        {
            _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
            _formation.FormOrder = FormOrder.FormOrderDeep;

            bool isEnemyCav = false;
            if (enemyFormation.QuerySystem.IsCavalryFormation | enemyFormation.QuerySystem.IsRangedCavalryFormation) isEnemyCav = true;

            bool canStealFlag = false;
            if (!isEnemyAtPoint & point.flagMover != enemyFormation.Team.Side & point.flagOwner != _formation.Team.Side) canStealFlag = true;

            bool canEngageEnemyCav = false;
            if (isEnemyCav && (enemyFormation.QuerySystem.MovementSpeed * 1.1f < _formation.QuerySystem.MovementSpeedMaximum)) canEngageEnemyCav = true;

            if (canStealFlag)
            { 
                _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderSquare;
                _formation.MovementOrder = MovementOrder.MovementOrderMove(pointPosition);
            }
            else if (canEngageEnemyCav)
            {
                _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLoose;
                _formation.MovementOrder = MovementOrderChargeToTarget(enemyFormation);
            }
            else
            {
                Vec3 enemyPos = enemyFormation.QuerySystem.MedianPosition.GetGroundVec3();
                Vec3 dir = enemyPos - thisPosition;
                Vec3 dirNormal = dir.NormalizedCopy();
                float thisVelocity = _formation.QuerySystem.CurrentVelocity.Length;
                float thisTargetVelocity = _formation.QuerySystem.MovementSpeedMaximum;
                float chargeThroughDistance = (thisTargetVelocity/thisVelocity)*2f;
                Vec3 chargeToPosition = (enemyPos + (dirNormal * chargeThroughDistance));
                _formation.MovementOrder = MovementOrder.MovementOrderMove(chargeToPosition.ToWorldPosition());
            }
        }
        private void Offense(Formation enemyFormation)
        {
            isAttacking = true;
            if (_formation.QuerySystem.IsRangedFormation & isEnemyAtPoint)
            {
                float range = _formation.QuerySystem.MissileRange * 1f;
                Vec3 directionVec = (pointPosition.GetGroundVec3() - thisPosition).NormalizedCopy();
                WorldPosition skirmishPosition = ((directionVec * range) + pointPosition.GetGroundVec3()).ToWorldPosition();
                _formation.MovementOrder = MovementOrder.MovementOrderMove(skirmishPosition);
                _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
                _formation.FiringOrder = FiringOrder.FiringOrderFireAtWill;
                _formation.FacingOrder = FacingOrder.FacingOrderLookAtEnemy;
                _formation.WeaponUsageOrder = WeaponUsageOrder.WeaponUsageOrderUseAny;
            }
            else
            {
                _formation.MovementOrder = MovementOrderChargeToTarget(enemyFormation);
                _formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
            }
        }
        private void DecideAction()
        {
            if (point != null)
            {
                thisPosition = _formation.QuerySystem.MedianPosition.GetGroundVec3();
                pointPosition = point.GameEntity.GlobalPosition.ToWorldPosition();
                Formation nearestEnemyFormation = _formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation.Formation;

                if (_formation.QuerySystem.IsCavalryFormation)
                {
                    CavalryOffense(nearestEnemyFormation);
                    return;
                }

                if (_formation.QuerySystem.IsRangedCavalryFormation)
                {
                    CavalryRangedOffense(nearestEnemyFormation);
                    return;
                }

                Vec3 enemyPosition = nearestEnemyFormation.QuerySystem.MedianPosition.GetGroundVec3();
                Vec3 enemyDistanceToThis = (enemyPosition - _formation.QuerySystem.MedianPosition.GetGroundVec3());
                float enemyDistanceToPoint = (enemyPosition - pointPosition.GetGroundVec3()).Length;
                Vec2 enemyDirection = nearestEnemyFormation.QuerySystem.EstimatedDirection;
                Vec2 enemyVelocity = nearestEnemyFormation.QuerySystem.CurrentVelocity;
                Vec2 enemyVelDir = new Vec2((enemyDirection.X * enemyVelocity.X), (enemyDirection.Y * enemyVelocity.Y));
                float enemyArrivalTime = (enemyDistanceToThis.X / enemyVelDir.X) + (enemyDistanceToThis.Y / enemyVelDir.Y) + enemyDistanceToThis.Z;
                
                Vec3 thisDistanceToPoint = (thisPosition - pointPosition.GetGroundVec3());
                float thisEstVelocity = _formation.QuerySystem.MovementSpeedMaximum*0.8f;
                Vec2 thisCurVelocity = _formation.QuerySystem.CurrentVelocity;
                float thisArrivalTimeToPoint = Math.Abs(thisDistanceToPoint.X / thisEstVelocity) + Math.Abs(thisDistanceToPoint.Y / thisEstVelocity) + thisDistanceToPoint.Z;
                float thisArrivalTimeToEnemy = ((enemyPosition.X - thisPosition.X) / thisCurVelocity.X) + ((enemyPosition.Y - thisPosition.Y) / thisCurVelocity.Y) + (enemyPosition.Z - thisPosition.Z);

                float arrivalRatios, powerRatios;
                if (enemyArrivalTime + thisArrivalTimeToPoint == 0) arrivalRatios = 0.1f;
                else arrivalRatios = (thisArrivalTimeToPoint) / (enemyArrivalTime + thisArrivalTimeToPoint); //ratio >0.5 means enemy hits this formation before formation reaches point
                
                float enemyPower = nearestEnemyFormation.QuerySystem.FormationPower;
                float thisPower = _formation.QuerySystem.FormationPower;
                if (enemyPower + thisPower == 0) powerRatios = 0.1f;
                else powerRatios = (thisPower) / (enemyPower + thisPower);

                if (enemyDistanceToPoint <= 5f) isEnemyAtPoint = true;
                else isEnemyAtPoint = false;

                if (thisDistanceToPoint.Length <= 8f) isThisAtPoint = true;
                else isThisAtPoint = false;

                bool isThisMelee = false;
                if (_formation.QuerySystem.IsMeleeFormation) isThisMelee = true;

                bool isThisShielded = false;
                if (_formation.QuerySystem.HasShield) isThisShielded = true;

                bool isThisCapturing = false;
                if (isThisAtPoint & point.flagMover == _formation.Team.Side) isThisCapturing = true;

                bool isEnemyStealing = false;
                if (isEnemyAtPoint & point.flagOwner != nearestEnemyFormation.Team.Side & point.flagMover == nearestEnemyFormation.Team.Side ) isEnemyStealing = true;

                bool canSteal = false;
                if (!isEnemyAtPoint & point.flagMover != _formation.Team.Side) canSteal = true;

                bool isUnderSustainedRangedAttack = false;
                if (_formation.QuerySystem.IsUnderRangedAttack & isThisAtPoint & isThisMelee & !isThisShielded) isUnderSustainedRangedAttack = true;

                bool willMeetEnemyFirst = false;
                if (thisArrivalTimeToPoint - thisArrivalTimeToEnemy > 0) willMeetEnemyFirst = true;

                bool isEnemyOnTopOfUs = false;
                if ((Math.Abs((enemyPosition - thisPosition).Length) < 5f) & !isThisCapturing) isEnemyOnTopOfUs = true;

                bool canOverwhelmEnemy = false;
                if (enemyArrivalTime > 0 && arrivalRatios > 0.5f && powerRatios > 0.7f) canOverwhelmEnemy = true;

                if (isThisCapturing | isEnemyStealing | canSteal)
                {
                    Defense(pointPosition);
                }
                else if (isUnderSustainedRangedAttack | willMeetEnemyFirst | isEnemyOnTopOfUs | isEnemyAtPoint | canOverwhelmEnemy | isThisMelee & !isThisShielded)
                {
                    Offense(nearestEnemyFormation);
                }
                else
                {
                    Defense(pointPosition);
                }
            }
        }

        protected override void TickOccasionally()
        {
            base.TickOccasionally();
            CalculateCurrentOrder();
        }

        protected override void CalculateCurrentOrder()
        {

            base.CalculateCurrentOrder();
            base.CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;

            DecideAction();
        }

        protected override float GetAiWeight()
        {
            return 10f;
        }
        private MovementOrder MovementOrderChargeToTarget(Formation targetFormation)
        {
            Type movementOrderType = typeof(MovementOrder);
            MethodInfo newOrderChargeToTarget = movementOrderType.GetMethod("MovementOrderChargeToTarget", BindingFlags.NonPublic | BindingFlags.Static);

            return (MovementOrder)newOrderChargeToTarget.Invoke(null, new object[] { targetFormation });
        }
    }

    public class TacticCapturePoint : TacticComponent
    {
        private InvasionFlag mainTargetPoint;
        private List<InvasionFlag> capturePoints = new List<InvasionFlag>();
        private Dictionary<InvasionFlag, BattleSideEnum> eligiblePoints = new Dictionary<InvasionFlag, BattleSideEnum>();
        private Dictionary<InvasionFlag, float> enemyPointsPower = new Dictionary<InvasionFlag, float>();
        private Dictionary<Formation, KeyValuePair<InvasionFlag, BattleSideEnum>> masterFormationList = new Dictionary<Formation, KeyValuePair<InvasionFlag, BattleSideEnum>>();
        private List<Formation> cavalryFormationList = new List<Formation>();

        private bool updateAgain = false;
        private bool init = false;
        private bool hasCav = false;

        private int ticks;
        private const int tickThreshold = 30;
        private Team enemyTeam;

        public TacticCapturePoint(Team team, List<InvasionFlag> flagPoints)
            : base(team)
        {
            capturePoints = flagPoints;
        }

        protected override void OnApply()
        {
            base.OnApply();
        }

        protected override void TickOccasionally()
        {
            base.TickOccasionally();
            ticks++;
            if (ticks >= tickThreshold)
            {
                ticks = 0;
                IsTacticReapplyNeeded = true;
            }
            if (!init)
            {
                enemyTeam = Mission.Current.Teams.Where((Team f) => f.IsEnemyOf(team) && !f.IsPlayerTeam).FirstOrDefault();
                if (enemyTeam != null) init = true;
                else return;
            }
            GetEligibleCapturePoints(IsTacticReapplyNeeded);

            if (updateAgain)
            {
                updateAgain = false;
                SetTroopOrders();
            }
            if (IsTacticReapplyNeeded)
            {
                InformationManager.DisplayMessage(new InformationMessage("Tactic Reapplied"));
                IsTacticReapplyNeeded = false;
            }
        }

        private void SetTroopOrders()
        {
            foreach(KeyValuePair<Formation, KeyValuePair<InvasionFlag, BattleSideEnum>> list in masterFormationList)
            {
                Formation formation = list.Key;
                InvasionFlag point = list.Value.Key;

                if (formation != null && formation.AI != null)
                {
                    BehaviorCapturePoint behavior;
                    if (formation.AI.GetBehavior<BehaviorCapturePoint>() == null)
                    {
                        behavior = formation.AI.EnsureBehavior<BehaviorCapturePoint>();
                    }
                    behavior = formation.AI.GetBehavior<BehaviorCapturePoint>();
                    behavior.point = point;
                    formation.AI.ResetBehaviorWeights();
                    behavior.WeightFactor = 10f;
                }
                else
                {
                    updateAgain = true;
                    continue;
                }
            }
            if (hasCav)
            {
                foreach(Formation formation in cavalryFormationList)
                {
                    if (formation != null && formation.AI != null)
                    {
                        BehaviorCapturePoint behavior;
                        if (formation.AI.GetBehavior<BehaviorCapturePoint>() == null)
                        {
                            behavior = formation.AI.EnsureBehavior<BehaviorCapturePoint>();
                        }
                        behavior = formation.AI.GetBehavior<BehaviorCapturePoint>();
                        behavior.point = mainTargetPoint;
                        formation.AI.ResetBehaviorWeights();
                        behavior.WeightFactor = 10f;
                    }
                }
            }
        }

        private void CreateAndAssignFormations()
        {
            //IEnumerable<Formation> allFormationsNoCav = team.Formations.Where((Formation f) => !f.QuerySystem.IsCavalryFormation | !f.QuerySystem.IsRangedCavalryFormation);
            int numPoints = eligiblePoints.Count();
            int formationsNeeded = Math.Min(3,numPoints);

            hasCav = false;
            cavalryFormationList.Clear();

            if (masterFormationList.Count() != Math.Min(6,formationsNeeded*2))
            {
                InformationManager.DisplayMessage(new InformationMessage("Readjusting Formation Count"));
                //check for querysystem failure - see bugs
                IEnumerable<Formation> nullList;
                nullList = team.Formations.Where((Formation f) => !f.QuerySystem.IsInfantryFormation & !f.QuerySystem.IsRangedFormation & !f.QuerySystem.IsCavalryFormation & !f.QuerySystem.IsRangedCavalryFormation);
                if (nullList.Count() == 0) ManageFormationCounts(formationsNeeded, formationsNeeded, 1, 1);
                else InformationManager.DisplayMessage(new InformationMessage("Null formation(s) found: " + nullList.Count().ToString()));
            }

            bool skipCav = false;
            Dictionary<InvasionFlag, SortedList<float, Formation>> pointsNearestFormation = new Dictionary<InvasionFlag, SortedList<float, Formation>>();
            foreach (KeyValuePair<InvasionFlag, BattleSideEnum> point in eligiblePoints)
            {
                if (hasCav) skipCav = true;
                SortedList<float, Formation> formationList = new SortedList<float, Formation>();
                Random random = new Random();
                foreach (Formation formation in team.Formations)
                {
                    if (!skipCav && (formation.QuerySystem.IsCavalryFormation | formation.QuerySystem.IsRangedCavalryFormation))
                    {
                        hasCav = true;
                        cavalryFormationList.Add(formation);
                        continue;
                    }
                    Vec3 formationPos = formation.QuerySystem.MedianPosition.GetGroundVec3();
                    float distance = (point.Key.GameEntity.GlobalPosition - formationPos).Length;
                    distance += random.NextFloat();
                    formationList.Add(distance, formation);
                }
                pointsNearestFormation.Add(point.Key, formationList);
            }
            //master formation list is foot soldiers only - cav & cav archers always target the main capture point
            masterFormationList.Clear();
            int loopCounter = 0;
            int oddEven = 0;
            bool alternateAssignments = false, isOdd = false;
            while (pointsNearestFormation.Values.First() != null && pointsNearestFormation.Values.First().Count > 0)
            {
                loopCounter++;
                foreach (KeyValuePair<InvasionFlag, BattleSideEnum> point in eligiblePoints)
                {
                    if (eligiblePoints.Count > 3)   //start alternating formation assignments if there are more than 3 active points. Allows up to 6 active points to be defended/attacked
                    {
                        alternateAssignments = true;
                        oddEven++;
                        int remainder = 0;
                        isOdd = false;
                        Math.DivRem(oddEven, 2, out remainder);
                        if (remainder > 0) isOdd = true;
                    }
                    List<Formation> removeList = new List<Formation>();
                    SortedList<float, Formation> closestFormations = new SortedList<float, Formation>();
                    if (pointsNearestFormation.TryGetValue(point.Key, out closestFormations))
                    {
                        Dictionary<float, Formation> closestInfFormations = closestFormations.Where((KeyValuePair<float, Formation> f) => f.Value.QuerySystem.IsInfantryFormation).ToDictionary(p => p.Key, p => p.Value);
                        if (closestInfFormations.Count > 0 && ((alternateAssignments & isOdd) | !alternateAssignments))
                        {
                            KeyValuePair<float, Formation> infSet = closestInfFormations.MinBy((KeyValuePair<float, Formation> f) => f.Key);
                            Formation exactInfFormation = infSet.Value;
                            masterFormationList.Add(exactInfFormation, new KeyValuePair<InvasionFlag, BattleSideEnum>(point.Key, point.Value));
                            removeList.Add(exactInfFormation);
                        }
                        Dictionary<float, Formation> closestRangedFormations = closestFormations.Where((KeyValuePair<float, Formation> f) => f.Value.QuerySystem.IsRangedFormation).ToDictionary(p => p.Key, p => p.Value);
                        if (closestRangedFormations.Count > 0 && ((alternateAssignments & !isOdd) | !alternateAssignments))
                        {
                            KeyValuePair<float, Formation> rangedSet = closestRangedFormations.MinBy((KeyValuePair<float, Formation> f) => f.Key);
                            Formation exactRangedFormation = rangedSet.Value;
                            masterFormationList.Add(exactRangedFormation, new KeyValuePair<InvasionFlag, BattleSideEnum>(point.Key, point.Value));
                            removeList.Add(exactRangedFormation);
                        }
                    }
                    foreach (KeyValuePair<InvasionFlag, BattleSideEnum> point2 in eligiblePoints)
                    {
                        foreach (Formation formation in removeList)
                        {
                            int index = pointsNearestFormation[point2.Key].IndexOfValue(formation);
                            pointsNearestFormation[point2.Key].RemoveAt(index); //remove formations to prevent a double assignment. Double assignments will throw an exception anyways - when masterFormationList attempts to add the same Formation key twice
                        }
                    }
                }
                if (loopCounter >= 4) break;    //break to prevent main thread lockup if I forgot to account for a case where all formations aren't assigned 
            }
        }

        private void OrganizeFormations(float percentMainAttackFormations, float percentDefenseFormations)
        {
            percentMainAttackFormations = Math.Min(1f, percentMainAttackFormations);    //main attack formation targets a specific point, defined by a global var - usually the most enemy-dense
            percentDefenseFormations = Math.Min(1f, percentDefenseFormations);  //defense formations sit on already-captured points

            int numDefensePoints = eligiblePoints.Where((KeyValuePair<InvasionFlag, BattleSideEnum> f) => f.Value == team.Side).Count();

            if (percentMainAttackFormations + percentDefenseFormations > 1f)
            {
                percentMainAttackFormations = 0.8f;
                percentDefenseFormations = 0.2f;
            }

            int totalTroops = team.QuerySystem.MemberCount;
            int totalInfantry = (int)Math.Round((float)totalTroops * team.QuerySystem.InfantryRatio);
            int totalRanged = (int)Math.Round((float)totalTroops * team.QuerySystem.RangedRatio);

            int numInfantryInAttackFormation = (int)Math.Round((float)totalInfantry * percentMainAttackFormations); //main & attack refer to the same thing here... 
            int numRangedInAttackFormation = (int)Math.Round((float)totalRanged * percentMainAttackFormations);
            int numInfantryInDefenseFormations = (int)Math.Round((float)totalInfantry * percentDefenseFormations / numDefensePoints);
            int numRangedInDefenseFormations = (int)Math.Round((float)totalRanged * percentDefenseFormations / numDefensePoints);
            int numInfantryInOtherFormations = totalInfantry - numInfantryInAttackFormation - numInfantryInDefenseFormations;  //other formations target other enemy/neutral points different from the main point (defined as a global)
            int numRangedInOtherFormations = totalRanged - numRangedInAttackFormation - numRangedInDefenseFormations;

            //do ALL the linq... how resource intensive will this be for multiplayer??
            List<Formation> mainFormations = masterFormationList.Where((KeyValuePair<Formation, KeyValuePair<InvasionFlag, BattleSideEnum>> f) => f.Value.Key == mainTargetPoint).ToDictionary(p => p.Key, p => p.Value.Key).Keys.ToList();
            List<Formation> defenseFormations = masterFormationList.Where((KeyValuePair<Formation, KeyValuePair<InvasionFlag, BattleSideEnum>> f) => f.Value.Value == team.Side).ToDictionary(p => p.Key, p => p.Value.Key).Keys.ToList();
            List<Formation> otherFormations = masterFormationList.Keys.ToList().Except(mainFormations).ToList();
            otherFormations = otherFormations.Except(defenseFormations).ToList();

            Formation mainInfFormation = null, mainRangedFormation = null;
            List<Formation> defenseInfFormations = null, defenseRangedFormations = null;
            List<Formation> otherInfFormations = null, otherRangedFormations = null;

            bool hasMainInf = false, hasMainRanged = false, hasDefenseInf = false, hasDefenseRanged = false, hasOtherInf = false, hasOtherRanged = false;
            if (mainFormations.Count > 0)
            {
                mainInfFormation = mainFormations.Where((Formation f) => f.QuerySystem.IsInfantryFormation).FirstOrDefault();
                if (mainInfFormation != null && mainInfFormation.CountOfUnits > 0) hasMainInf = true;
                mainRangedFormation = mainFormations.Where((Formation f) => f.QuerySystem.IsRangedFormation).FirstOrDefault();
                if (mainRangedFormation != null && mainRangedFormation.CountOfUnits > 0) hasMainRanged = true;
            }
            if (defenseFormations.Count > 0)
            {
                defenseInfFormations = defenseFormations.Where((Formation f) => f.QuerySystem.IsMeleeFormation).ToList();
                if (defenseInfFormations != null && defenseInfFormations.Count > 0) hasDefenseInf = true;
                defenseRangedFormations = defenseFormations.Where((Formation f) => f.QuerySystem.IsRangedFormation).ToList();
                if (defenseRangedFormations != null && defenseRangedFormations.Count > 0) hasDefenseRanged = true;
            }
            if (otherFormations.Count > 0)
            {
                otherInfFormations = otherFormations.Where((Formation f) => f.QuerySystem.IsMeleeFormation).ToList();
                if (otherInfFormations != null && otherInfFormations.Count > 0) hasOtherInf = true;
                otherRangedFormations = otherFormations.Where((Formation f) => f.QuerySystem.IsRangedFormation).ToList();
                if (otherRangedFormations != null && otherRangedFormations.Count > 0) hasOtherRanged = true;
            }

            List<KeyValuePair<Formation, int>> transferFromList = new List<KeyValuePair<Formation, int>>();
            List<KeyValuePair<Formation, int>> transferFromList2 = new List<KeyValuePair<Formation, int>>();
            List<KeyValuePair<Formation, int>> transferToList = new List<KeyValuePair<Formation, int>>();
            bool isFulfilled = false;
            while (hasMainInf && mainInfFormation.CountOfUnits < numInfantryInAttackFormation)  //Use a while() loop so it can be broken early if the first TransferTroops satisfies the formation troop count
            {
                transferToList.Add(new KeyValuePair<Formation, int>(mainInfFormation, numInfantryInAttackFormation));

                if (hasDefenseInf)
                {
                    foreach (Formation formation in defenseInfFormations)
                    {
                        transferFromList.Add(new KeyValuePair<Formation, int>(formation, numInfantryInDefenseFormations));
                    }
                    isFulfilled = TransferTroops(transferFromList, transferToList);
                    if (isFulfilled) break;
                }
                if (hasOtherInf)
                {
                    transferFromList.Clear();
                    foreach (Formation formation in otherInfFormations)
                    {
                        transferFromList.Add(new KeyValuePair<Formation, int>(formation, numInfantryInOtherFormations));
                    }
                    TransferTroops(transferFromList, transferToList);
                }
                break;
            }
            while (hasMainRanged && mainRangedFormation.CountOfUnits < numRangedInAttackFormation)  //Use a while() loop so it can be broken early - same reason as above
            {
                transferToList.Add(new KeyValuePair<Formation, int>(mainRangedFormation, numRangedInAttackFormation));
                if (hasDefenseRanged)
                {
                    foreach (Formation formation in defenseRangedFormations)
                    {
                        transferFromList.Add(new KeyValuePair<Formation, int>(formation, numRangedInDefenseFormations));
                    }
                    isFulfilled = TransferTroops(transferFromList, transferToList);
                    if (isFulfilled) break;
                }
                if (hasOtherRanged)
                {
                    transferFromList.Clear();
                    foreach (Formation formation in otherRangedFormations)
                    {
                        transferFromList.Add(new KeyValuePair<Formation, int>(formation, numRangedInOtherFormations));
                    }
                    TransferTroops(transferFromList, transferToList);
                }
                break;
            }
            if (hasDefenseInf)
            {
                transferToList.Clear();
                transferFromList2.Clear();
                foreach (Formation formation in defenseInfFormations)
                {
                    transferToList.Add(new KeyValuePair<Formation, int>(formation, numInfantryInDefenseFormations));
                    
                }
                transferFromList2 = transferToList; //for transferring excess to other formations
                transferFromList.Clear();
                if (hasMainInf) transferFromList.Add(new KeyValuePair<Formation, int>(mainInfFormation, numInfantryInAttackFormation));
                TransferTroops(transferFromList, transferToList);

                if (hasOtherInf)
                {
                    transferToList.Clear();
                    foreach(Formation formation in otherInfFormations)
                    {
                        transferToList.Add(new KeyValuePair<Formation, int>(formation, numInfantryInOtherFormations));
                    }
                    transferFromList.Clear();
                    TransferTroops(transferFromList2, transferToList);
                }
            }
            if (hasDefenseRanged)
            {
                transferToList.Clear();
                transferFromList2.Clear();
                foreach (Formation formation in defenseRangedFormations)
                {
                    transferToList.Add(new KeyValuePair<Formation, int>(formation, numRangedInDefenseFormations));
                }
                transferFromList2 = transferToList; //for transferring excess to other formations
                transferFromList.Clear();
                if (hasMainRanged) transferFromList.Add(new KeyValuePair<Formation, int>(mainRangedFormation, numRangedInAttackFormation));
                TransferTroops(transferFromList, transferToList);

                if (hasOtherRanged)
                {
                    transferToList.Clear();
                    foreach (Formation formation in otherRangedFormations)
                    {
                        transferToList.Add(new KeyValuePair<Formation, int>(formation, numRangedInOtherFormations));
                    }
                    transferFromList.Clear();
                    TransferTroops(transferFromList2, transferToList);
                }
            }
        }
        private bool TransferTroops(List<KeyValuePair<Formation, int>> fromFormations, List<KeyValuePair<Formation, int>> toFormations)
        {
            foreach (KeyValuePair<Formation, int> toDataSet in toFormations)
            {
                Formation toFormation = toDataSet.Key;
                int toNumNeeded = toDataSet.Value;
                if (toFormation != null && toFormation.CountOfUnits < toNumNeeded)
                {
                    if (fromFormations != null && fromFormations.Count > 0)
                    {
                        int numTroopDeficit = toNumNeeded - toFormation.CountOfUnits;
                        foreach (KeyValuePair<Formation, int> fromDataSet in fromFormations)
                        {
                            Formation formation = fromDataSet.Key;
                            int fromNumNeeded = fromDataSet.Value;
                            if (formation.CountOfUnits > fromNumNeeded)
                            {
                                int numExtraTroops = formation.CountOfUnits - fromNumNeeded;
                                TransferUnits(formation, toFormation, Math.Min(numExtraTroops, numTroopDeficit));
                                numTroopDeficit = toNumNeeded - toFormation.CountOfUnits;
                            }
                            if (numTroopDeficit <= 0) return true;
                        }
                    }
                    else return false;
                }
                else return true;
            }
            return false;
        }

        private void CalculateCaptureLogic()
        {
            int numPoints = eligiblePoints.Count();
            float teamPower = team.QuerySystem.TeamPower;
            float enemyTeamPower = enemyTeam.QuerySystem.TeamPower;
            float teamInfantryPower = (team.QuerySystem.InfantryRatio * (float)team.QuerySystem.MemberCount) / teamPower;
            float teamRangedPower = (team.QuerySystem.RangedRatio * (float)team.QuerySystem.MemberCount) / teamPower;

            Formation defaultFormation = Formations.FirstOrDefault();
            float highestEnemyPower = 0;

            enemyPointsPower.Clear();
            int numControlledPoints = 0;
            int numEnemyPoints = 0;
            int numNeutralPoints = 0;
            foreach (KeyValuePair<InvasionFlag, BattleSideEnum> point in eligiblePoints)
            {
                if (point.Value == team.Side) numControlledPoints++;
                else if (point.Value == BattleSideEnum.None) numNeutralPoints++;
                else numEnemyPoints++;

                if (point.Value == team.Side) continue;

                if (defaultFormation != null)
                {
                    float enemyPower = defaultFormation.QuerySystem.GetLocalEnemyPower(point.Key.GameEntity.GlobalPosition.AsVec2);
                    enemyPointsPower.Add(point.Key, enemyPower);
                    if (highestEnemyPower < enemyPower)
                    {
                        highestEnemyPower = enemyPower;
                        mainTargetPoint = point.Key;
                    }
                }
                else continue;
            }
            CreateAndAssignFormations();

            float forceRatio = teamPower / (enemyTeamPower + teamPower);
            float assaultThreshold = 0.8f;
            float percentAttack = Math.Min((0.1f + forceRatio*1.25f), assaultThreshold);
            float percentBalanceAttack = Math.Min( (1f / (float)(numEnemyPoints+numNeutralPoints)), (1-forceRatio));

            bool desperateAssault = false, desperateDefense = false;
            if ((numControlledPoints <= 1 & numPoints > 1 & numEnemyPoints >= 1) & (forceRatio > 0.6f)) desperateAssault = true;
            else if ((numControlledPoints <= 1 & numPoints > 1 & numEnemyPoints >= 1) & (forceRatio < 0.45f)) desperateDefense = true;

            bool canOverwhelm = false;
            if ((numPoints - numControlledPoints == numEnemyPoints) & forceRatio > (1-forceRatio)) canOverwhelm = true;

            bool balancedAssault = false, balancedDefense = false;
            if ((numNeutralPoints >= numEnemyPoints) & forceRatio > 0.6f) balancedAssault = true;
            else if ((numNeutralPoints >= numEnemyPoints) & forceRatio < 0.45f) balancedDefense = true;

            if (desperateAssault | desperateDefense)
            {
                if (desperateAssault)
                {
                    OrganizeFormations(percentAttack, 1 - percentAttack);
                    InformationManager.DisplayMessage(new InformationMessage(team.ToString() + " Desperate Assault: " + percentAttack.ToString()));
                }
                else if (desperateDefense)
                {
                    InformationManager.DisplayMessage(new InformationMessage(team.ToString() + " Desperate Defense: " + percentAttack.ToString()));
                    if (enemyPointsPower.Count > 1)
                    {
                        mainTargetPoint = enemyPointsPower.MinBy((KeyValuePair<InvasionFlag, float> f) => f.Value).Key;
                        float enemyPowerAtTarget = defaultFormation.QuerySystem.GetLocalEnemyPower(mainTargetPoint.GameEntity.GlobalPosition.AsVec2);
                        float targetForceRatio = enemyPowerAtTarget / (enemyPowerAtTarget + teamPower);
                        percentAttack = Math.Min(forceRatio, targetForceRatio + 0.2f);
                        OrganizeFormations(1 - percentAttack, percentAttack);
                    }
                    else OrganizeFormations(1 - percentAttack, percentAttack);
                }
            }
            else if (canOverwhelm)
            {
                InformationManager.DisplayMessage(new InformationMessage(team.ToString() + " Overwhelm: " + percentAttack.ToString()));
                OrganizeFormations(percentAttack, 1 - percentAttack);
            }
            else if (balancedAssault)
            {
                InformationManager.DisplayMessage(new InformationMessage(team.ToString() + " Balanced Assault: " + percentBalanceAttack.ToString()));
                OrganizeFormations(Math.Min(percentBalanceAttack, 0.8f), 0.2f);
            }
            else if (balancedDefense)
            {
                InformationManager.DisplayMessage(new InformationMessage(team.ToString() + " Balanced Defense: " + percentBalanceAttack.ToString()));
                OrganizeFormations(Math.Min(0.6f, percentBalanceAttack), 0.4f);
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(team.ToString() + " Default Tactic: " + percentBalanceAttack.ToString()));
                OrganizeFormations(Math.Min(0.7f, percentBalanceAttack), 0.3f);
            }
            SetTroopOrders();
        }
        private void GetEligibleCapturePoints(bool forceUpdateFormations)
        {
            Dictionary<InvasionFlag, BattleSideEnum> newEligiblePoints = new Dictionary<InvasionFlag, BattleSideEnum>();
            switch (team.Side)
            {
                case BattleSideEnum.Defender:
                    foreach(InvasionFlag point in capturePoints)
                    {
                        if (!point.canDefendersCapture) continue;
                        else newEligiblePoints.Add(point, point.flagOwner);
                    }
                    break;

                case BattleSideEnum.Attacker:
                    foreach(InvasionFlag point in capturePoints)
                    {
                        if (!point.canAttackersCapture) continue;
                        else newEligiblePoints.Add(point, point.flagOwner);
                    }
                    break;
            }
            if (forceUpdateFormations)
            {
                eligiblePoints.Clear();
                eligiblePoints = newEligiblePoints;
                CalculateCaptureLogic();
                return;
            }

            foreach (KeyValuePair<InvasionFlag, BattleSideEnum> point in newEligiblePoints)
            {
                BattleSideEnum pointOwner;
                if (eligiblePoints.TryGetValue(point.Key, out pointOwner))
                {
                    if (pointOwner != point.Value)
                    {
                        eligiblePoints.Clear();
                        eligiblePoints = newEligiblePoints;
                        CalculateCaptureLogic();
                        break;
                    }
                    else continue;
                }
                else
                {
                    eligiblePoints.Clear();
                    eligiblePoints = newEligiblePoints;
                    CalculateCaptureLogic();
                    break;
                }
            }
        }
        private void TransferUnits(Formation formationFrom, Formation formationTo, int transferCount)
        {
            Type formationType = typeof(Formation);
            MethodInfo transferUnitsMethod = formationType.GetMethod("TransferUnits", BindingFlags.NonPublic | BindingFlags.Instance);

            transferUnitsMethod.Invoke(formationFrom, new object[] { formationTo, transferCount });
        }
    }

    public class InvasionSpawner : ScriptComponentBehaviour
    {
        //editor-accessible classes:
        public int SpawnCount = 10;
        public bool FriendlySpawns = false;
        public int SpawnClass = 0;
        //

        public int spawnerIndex
        {
            get;
            set;
        }

        private string troopID = "modTroopInf";

        private Mission mission = Mission.Current;

        private BasicCharacterObject baseCharacter;
        private ModMissionBehavior invasionMissionBehavior;
        private IAgentOriginBase agentOriginBase;

        private bool init, isOn, forceSpawn = false;
        private Team spawnTeam;
        private float tick = 0f;
        private const float initThreshold = 2f;
        private float spawnInterval;

        private List<Agent> agentList = new List<Agent>();
        private Dictionary<Agent, Agent> agentHorse = new Dictionary<Agent, Agent>();

        protected override void OnInit()
        {
            base.OnInit();
            switch (SpawnClass)
            {
                case 0: 
                    troopID = "basic_inf";
                    spawnInterval = 30f;
                    break;
                case 1: 
                    troopID = "basic_xbow";
                    spawnInterval = 30f;
                    break;
                case 2:
                    troopID = "basic_cav";
                    spawnInterval = 60f;
                    break;
                case 3:
                    troopID = "basic_cav_archer";
                    spawnInterval = 90f;
                    break;
                case 4:
                    troopID = "soldier_assault";
                    spawnInterval = 60f;
                    break;
                case 5:
                    troopID = "soldier_armored";
                    spawnInterval = 60f;
                    break;

                default:
                    troopID = "basic_inf";
                    spawnInterval = 30f;
                    break;
            }
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);
            tick += dt;
            if (tick >= spawnInterval)
            {
                OccasionalTick();
                tick = 0;
            }
            if (forceSpawn && agentList.Count > 0)
            {
                forceSpawn = false;
            }
            if (!init && tick > initThreshold)
            {
                invasionMissionBehavior = Mission.Current.GetMissionBehaviour<ModMissionBehavior>();
                baseCharacter = MBObjectManager.Instance.GetObject<BasicCharacterObject>(troopID);

                if (FriendlySpawns)
                {
                    spawnTeam = mission.PlayerAllyTeam;
                    agentOriginBase = invasionMissionBehavior.allyOriginBase;
                }
                else
                {
                    spawnTeam = mission.PlayerEnemyTeam;
                    agentOriginBase = invasionMissionBehavior.enemyOriginBase;
                }

                init = true;
                forceSpawn = true;
                OccasionalTick();
            }

        }

        private void OccasionalTick()
        {
            if (init && forceSpawn | init && isOn)
            {
                //This spawn system should really be written using IMissionTroopSupplier
                List<BasicCharacterObject> characterObjects = new List<BasicCharacterObject>();
                if (agentList.Count == 0)
                {
                    for (int i = 0; i < SpawnCount; i++)
                    {
                        characterObjects.Add(baseCharacter);
                    }
                }

                for (int i = 0; i < agentList.Count; i++)
                {
                    if (agentList[i].Health == 0)
                    {
                        Agent horse;
                        if (agentHorse.TryGetValue(agentList[i], out horse)) horse.Die(new Blow());

                        agentList.RemoveAt(i);
                        characterObjects.Add(baseCharacter);
                    }
                }

                if (characterObjects.Count != 0 && agentList.Count < SpawnCount)
                {
                    StartSpawn(characterObjects);
                }
            }
        }

        public void Enable(bool start)
        {
            isOn = start;
        }

        private void StartSpawn(IEnumerable<BasicCharacterObject> spawns)
        {
            if (init && forceSpawn | init && isOn)
            {
                Banner banner = Banner.CreateRandomBanner();

                MatrixFrame frame = base.GameEntity.GetGlobalFrame();
                BasicCharacterObject troop = spawns.FirstOrDefault();
                Team agentTeam = spawnTeam;
                Formation formation = agentTeam.GetFormation(troop.DefaultFormationClass);
                int formationIndex = formation.Index;

                AgentBuildData buildData = new AgentBuildData(troop);
                foreach (BasicCharacterObject character in spawns)
                {
                    if (troop.HasMount())
                    {
                        buildData.NoHorses(false);
                        buildData.MountKey(MountCreationKey.GetRandomMountKey(troop.Equipment[EquipmentIndex.ArmorItemEndSlot].Item, troop.GetMountKeySeed()));
                    }
                    else buildData.NoHorses(true);

                    buildData.InitialFrame(frame);
                    buildData.Team(agentTeam);
                    buildData.Banner(banner);
                    buildData.ClothingColor1(agentTeam.Color);
                    buildData.ClothingColor2(agentTeam.Color2);
                    buildData.TroopOrigin(agentOriginBase);
                    buildData.CivilianEquipment(mission.DoesMissionRequireCivilianEquipment);
                    buildData.Formation(formation);
                    buildData.FormationTroopCount(SpawnCount).FormationTroopIndex(formationIndex);

                    Agent agent = mission.SpawnAgent(buildData, spawnFromAgentVisuals: false, formationTroopCount: SpawnCount);
                    agent.WieldInitialWeapons();
                    agent.SetWatchState(AgentAIStateFlagComponent.WatchState.Alarmed);
                    agentList.Add(agent);
                    if (troop.HasMount()) agentHorse.Add(agent, agent.MountAgent);
                }
            }
        }
    }

    public class InvasionFlag : UsableMachine
    {
        //editor-accessible classes:
        public float FlagMaxHeight = 4f;
        public float CaptureRadius = 6f;
        public string FlagTag = "flag";
        public string SpawnerTag = "spawner";
        public int FlagOrder = 0;
        public int FlagInitialOwner;
        //

        public bool isFlagRaised
        {
            get;
            private set;
        }
        public BattleSideEnum flagOwner
        {
            get;
            private set;
        }
        public bool canAttackersCapture
        {
            get;
            private set;
        }
        public bool canDefendersCapture
        {
            get;
            private set;
        }

        private GameEntity flagChild;
        private MatrixFrame originalFlagFrame;
        private float ticker = 0f;
        private const float tickThreshold = 1f;

        
        private float flagRaiseTime = 4f;
        float flagPos = 0f;

        private Mission mission = Mission.Current;
        private List<InvasionFlag> otherLowerFlags = new List<InvasionFlag>();
        private List<InvasionFlag> otherHigherFlags = new List<InvasionFlag>();

        public List<InvasionSpawner> linkedSpawners
        {
            get;
            private set;
        }

        private bool _init = false;
        private Team allies, enemies;
        public BattleSideEnum flagMover;
        private float flagProgressAttacker, flagProgressDefender = 0;

        private uint red = Colors.Red.ToUnsignedInteger();
        private uint blue = Colors.Blue.ToUnsignedInteger();
        private uint white = Colors.White.ToUnsignedInteger();
        private uint gray = Colors.Gray.ToUnsignedInteger();

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return "Capture Point: " + FlagOrder.ToString();
        }

        public override TextObject GetActionTextForStandingPoint(UsableMissionObject usableGameObject = null)
        {
            TextObject actionText = new TextObject("");
            return null;
        }

        protected override void OnEditorTick(float dt)
        {
            base.OnEditorTick(dt);
            if (MBEditor.IsEntitySelected(base.GameEntity))
            {
                DebugExtensions.RenderDebugCircleOnTerrain(base.Scene, base.GameEntity.GetGlobalFrame(), CaptureRadius, 2868838400u);   //This shows the capture radius in the editor
            }
        }

        protected override void OnInit()
        {
            base.OnInit();
            isFlagRaised = false;
            flagChild = this.GameEntity.GetFirstChildEntityWithTag(FlagTag);
            if (flagChild != null) originalFlagFrame = flagChild.GetGlobalFrame();
            
            switch (FlagInitialOwner)
            {
                case 0:
                    flagOwner = BattleSideEnum.None;
                    break;
                case 1:
                    flagOwner = BattleSideEnum.Defender;
                    flagProgressDefender = FlagMaxHeight;
                    break;
                case 2:
                    flagOwner = BattleSideEnum.Attacker;
                    flagProgressAttacker = FlagMaxHeight;
                    break;
            }
        }

        public override void AfterMissionStart()
        {
            base.AfterMissionStart();
            flagChild.SetGlobalFrame(flagChild.GetGlobalFrame().Elevate(-FlagMaxHeight));
            flagChild.SetFactorColor(white);

            linkedSpawners = new List<InvasionSpawner>();
            List<GameEntity> childSpawnEntities = base.GameEntity.CollectChildrenEntitiesWithTag(SpawnerTag);
            if (!childSpawnEntities.IsEmpty())
            {
                foreach (GameEntity spawnEntity in childSpawnEntities)
                {
                    linkedSpawners.Add(spawnEntity.GetFirstScriptOfType<InvasionSpawner>());
                }
            }

            IEnumerable<GameEntity> scriptedEntities = mission.Scene.FindEntitiesWithTag("invasion");
            List<InvasionFlag> allOtherFlags = new List<InvasionFlag>();
            foreach (GameEntity entity in scriptedEntities)
            {
                if (entity.HasScriptOfType<InvasionFlag>())
                {
                    allOtherFlags.Add(entity.GetFirstScriptOfType<InvasionFlag>());
                }
            }
            if (allOtherFlags.Count > 1)
            {
                foreach (InvasionFlag otherFlag in allOtherFlags)
                {
                    if (otherFlag.FlagOrder < this.FlagOrder) otherLowerFlags.Add(otherFlag);
                    else if (otherFlag.FlagOrder > this.FlagOrder) otherHigherFlags.Add(otherFlag);
                }
            }
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);
            
            if (mission != null && mission.PlayerEnemyTeam != null && mission.PlayerAllyTeam != null)
            {
                allies = mission.PlayerAllyTeam;    //DEFENDERS
                enemies = mission.PlayerEnemyTeam;  //ATTACKERS
                _init = true;
            }
            if (_init)
            {
                ticker += dt;
                if (ticker >= tickThreshold)
                {
                    ticker = 0;
                    OccasionalTick();
                }

                int nearbyAllies, nearbyEnemies;

                if (canDefendersCapture) nearbyAllies = mission.GetNearbyEnemyAgentCount(enemies, base.GameEntity.GlobalPosition.AsVec2, CaptureRadius);
                else nearbyAllies = 0; 

                if (canAttackersCapture) nearbyEnemies = mission.GetNearbyEnemyAgentCount(allies, base.GameEntity.GlobalPosition.AsVec2, CaptureRadius);
                else nearbyEnemies = 0;

                float capRate;
                flagMover = BattleSideEnum.None;

                if (nearbyAllies > 0 | nearbyEnemies > 0)
                {
                    capRate = Calculate(nearbyAllies, nearbyEnemies);
                    
                    float flagRate = dt * (FlagMaxHeight / flagRaiseTime);
                    if (capRate > 0)
                    {
                        flagProgressDefender += flagRate * capRate;
                        flagProgressAttacker += -flagRate * capRate;
                        flagMover = allies.Side;
                    }
                    else if (capRate < 0)
                    {
                        flagProgressAttacker += flagRate * -capRate;
                        flagProgressDefender -= flagRate * -capRate;
                        flagMover = enemies.Side;
                    }
                    flagProgressDefender = MathF.Clamp(flagProgressDefender, 0, FlagMaxHeight);
                    flagProgressAttacker = MathF.Clamp(flagProgressAttacker, 0, FlagMaxHeight);
                    if (capRate != 0)
                    {
                        flagPos = Math.Abs(flagProgressAttacker - flagProgressDefender);
                        flagPos = Math.Min(flagPos, FlagMaxHeight);
                        MoveFlag(flagPos, flagMover);
                    }
                }

                else if (nearbyAllies == 0 && nearbyEnemies == 0)
                {
                    if (flagPos != 0 | flagPos != FlagMaxHeight)
                    {
                        float flagRate = dt * (FlagMaxHeight / flagRaiseTime);
                        float decayRate = 1f;
                        switch (flagOwner)
                        {
                            case BattleSideEnum.None:
                                flagProgressDefender += flagRate * -decayRate;
                                flagProgressAttacker += flagRate * -decayRate;
                                break;
                            case BattleSideEnum.Defender:
                                flagProgressDefender += flagRate * decayRate;
                                flagProgressAttacker += flagRate * -decayRate;
                                break;
                            case BattleSideEnum.Attacker:
                                flagProgressDefender += flagRate * -decayRate;
                                flagProgressAttacker += flagRate * decayRate;
                                break;
                        }
                        flagProgressDefender = MathF.Clamp(flagProgressDefender, 0, FlagMaxHeight);
                        flagProgressAttacker = MathF.Clamp(flagProgressAttacker, 0, FlagMaxHeight);
                        flagPos = Math.Abs(flagProgressAttacker - flagProgressDefender);

                        MoveFlag(flagPos, flagOwner);
                    }
                }
            }
        }

        private void SetSpawnerStatus(BattleSideEnum spawnerSide)
        {
            foreach(InvasionSpawner spawner in linkedSpawners)
            {
                    switch (spawnerSide)
                    {
                        case BattleSideEnum.None:
                            spawner.Enable(false);
                            break;

                        case BattleSideEnum.Defender:
                            if (spawner.FriendlySpawns) spawner.Enable(true);
                            else spawner.Enable(false);

                            break;

                        case BattleSideEnum.Attacker:
                            if (!spawner.FriendlySpawns) spawner.Enable(true);
                            else spawner.Enable(false);
                            break;
                    }
            }
        }

        private void OccasionalTick()
        {
            CheckWhoCanCapture();
        }

        private void CheckWhoCanCapture()
        {   
            //higher flags = attackers, lower flags = defenders
            bool tempCanDefendersCapture = true;
            bool tempCanAttackersCapture = true;
            if (otherHigherFlags != null)
            {
                foreach (InvasionFlag otherFlag in otherHigherFlags)
                {
                    if (otherFlag.flagOwner != BattleSideEnum.Attacker)
                    {
                        tempCanAttackersCapture = false;
                        if (otherFlag.flagOwner == BattleSideEnum.Defender) tempCanDefendersCapture = false;    //Used to determine if defending formations need to move up
                    }
                }
            }
            if (otherLowerFlags != null)
            {
                foreach (InvasionFlag otherFlag in otherLowerFlags)
                {
                    if (otherFlag.flagOwner != BattleSideEnum.Defender)
                    {
                        tempCanDefendersCapture = false;
                        if (otherFlag.flagOwner == BattleSideEnum.Attacker) tempCanAttackersCapture = false;    //Used to determine if defending formations need to move up
                    }
                }
            }
            canAttackersCapture = tempCanAttackersCapture;
            canDefendersCapture = tempCanDefendersCapture;
        }

        private void MoveFlag(float flagPos, BattleSideEnum flagMover)
        {
            MatrixFrame tempFrame = originalFlagFrame;
            flagChild.SetGlobalFrame(tempFrame.Elevate(flagPos - FlagMaxHeight));

            if (!isFlagRaised && flagPos == FlagMaxHeight)
            {
                flagOwner = flagMover;
                switch (flagOwner)
                {
                    case BattleSideEnum.Attacker:
                        flagChild.SetFactorColor(red);
                        break;
                    case BattleSideEnum.Defender:
                        flagChild.SetFactorColor(blue);
                        break;
                }
                SetSpawnerStatus(flagOwner);
                isFlagRaised = true;
            }
            if (flagPos < FlagMaxHeight * 0.05f)
            {
                flagOwner = BattleSideEnum.None;
                flagChild.SetFactorColor(white);
                SetSpawnerStatus(flagOwner);
                isFlagRaised = false;
            }
        }

        private float Calculate(int allies, int enemies)
        {
            float f_allies = (float)allies;
            float f_enemies = (float)enemies;

            if (enemies == 0 && allies > 0) return 0.5f;
            if (allies == 0 && enemies > 0) return -0.5f;

            float capRatio = ((f_allies / (f_allies + f_enemies)) - 0.5f)*2f;
            capRatio = MBMath.ClampFloat(capRatio, -1, 1);

            if (capRatio > 0.05 | capRatio < -0.05) return capRatio;

            return 0f;
        }
    }
}
