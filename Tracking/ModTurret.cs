using System;
using System.Collections.Generic;
using Sandbox.Engine.Utils;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRageMath;

namespace IngameScript {
  partial class Program {
    public class ModTurret : Module {

      public ModTurret(Program program) : base(program) { }

      public override int Priority => 1000;

      private ITerminalAction _shootOn;
      private ITerminalAction _shootOff;
      private List<Turret> _turrets;
      private long _targetId = 0L;
      private const float TolerableAngleDiff = 0.3f;

      public override void Initialize(IEnumerable<string> arguments)
      {
        _turrets = FindAllTurrets();
        program.logMessages.Enqueue($"Turret mod initialized with {_turrets.Count} turrets");

        if (_turrets.Count > 0)
        {
          _shootOn = _turrets[0].guns[0].GetActionWithName("Shoot_On");
          _shootOff = _turrets[0].guns[0].GetActionWithName("Shoot_Off");
        }
      }

      public override void Main(string argument, UpdateType upType)
      {
        var targets = FindTargets();
        foreach (var turret in _turrets) 
          ControlTurret(turret, targets.Count == 0 ? new long?() : targets[0]);
      }

      void ControlTurret(Turret turret, long? target)
      {
        if (!target.HasValue)
        {
          foreach (var mySmallGatlingGun in turret.guns)
            _shootOff.Apply(mySmallGatlingGun);
          return;
        }

        MyDetectedEntityInfo targetMdei = program.trackedEntities[target.Value];
        //desired rotation
        Vector3 turretPositionApprox = turret.gunsRotor.TopGrid.GridIntegerToWorld(turret.averageGunPosition);

        //expected interception time
        float expectedInterceptTime = (float) Math.Sqrt( Vector3.Distance(targetMdei.Position, turretPositionApprox)/(400 + targetMdei.Velocity.Length()));
        Vector3 positionAtIntercept = targetMdei.Position + expectedInterceptTime * targetMdei.Velocity;

        /*program.logMessages.Enqueue($"from guns: {positionAtIntercept - turretPositionApprox}");
        program.logMessages.Enqueue($"from base: {positionAtIntercept - turret.baseRotor.CubeGrid.GridIntegerToWorld(turret.baseRotor.Position)}");

        program.logMessages.Enqueue($"Target velocity : {targetMdei.Velocity}");
        program.logMessages.Enqueue($"Expected intercept time : {expectedInterceptTime}");*/

        float angleDiff = 0;
        foreach(var stator in turret.joints)
        {
          float desiredJointAngle = CalculateDesiredAngle(stator, positionAtIntercept - turretPositionApprox);
          angleDiff += RotateTowardsAngle(stator, desiredJointAngle);
        }

        //program.logMessages.Enqueue($"Angle diff : {angleDiff}");
        //program.Log($"Turret local position: {Me.CubeGrid.WorldToGridInteger(turretPositionApprox)}");
        //program.Log($"Target local position: {Me.CubeGrid.WorldToGridInteger(positionAtIntercept)}");

        if (angleDiff < TolerableAngleDiff)
        {
          bool targetBlocked = false;
          program.Me.CubeGrid.RayCastGrid( turretPositionApprox, (positionAtIntercept - turretPositionApprox),
            (pos) =>
            {
              targetBlocked = true;
              //program.logMessages.Enqueue( $"Block: {program.Me.CubeGrid.GetCubeBlock(pos).BlockDefinition.SubtypeName} " );
            } );
          foreach (var mySmallGatlingGun in turret.guns)
            (targetBlocked ? _shootOff : _shootOn).Apply(mySmallGatlingGun);
        }
      }

      List<long> FindTargets()
      {
        var possibleTargets = new List<long>();
        foreach (var trackedEntity in program.trackedEntities.Values)
        {
          if (trackedEntity.Type == MyDetectedEntityType.CharacterHuman)
          {
            possibleTargets.Clear();
            possibleTargets.Add(trackedEntity.EntityId);
            return possibleTargets;
          }
          if (Vector3.DistanceSquared(trackedEntity.Position, program.Me.Position) > 800 * 800) //out of weapons range
            continue;

          if (trackedEntity.Type == MyDetectedEntityType.Missile && IsMissileDangerous(trackedEntity))
          {
            possibleTargets.Add(trackedEntity.EntityId);
            continue;
          }
          
         /* if (trackedEntity.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)
            continue;
          
          possibleTargets.Add(trackedEntity.EntityId);*/
        }

        return possibleTargets;
      }

      private bool IsMissileDangerous(MyDetectedEntityInfo mdei)
      {
        var relativeVelocity = mdei.Velocity - program.GetMyMDEI().Velocity;
        var intersects = program.Me.WorldAABB.Intersects(new Ray(mdei.Position, relativeVelocity));
        //todo precise hit calculation
        return intersects.HasValue && intersects.Value < 800; // it's out of it's range
      }

      private float CalculateDesiredAngle(IMyMotorStator AZ, Vector3 toTarget)
      {
        Vector3 origin = AZ.CubeGrid.GridIntegerToWorld(AZ.Position);
        Vector3 rotorLf = AZ.CubeGrid.GridIntegerToWorld(AZ.Position + Base6Directions.GetIntVector(AZ.Orientation.Left)) - origin;
        Vector3 rotorFw = AZ.CubeGrid.GridIntegerToWorld(AZ.Position + Base6Directions.GetIntVector(AZ.Orientation.Forward)) - origin;

        rotorLf.Normalize();
        rotorFw.Normalize();

        Vector3 normalizedDir;
        Vector3.Normalize(ref toTarget, out normalizedDir);

        return (float) Math.Atan2(Vector3.Dot(normalizedDir, rotorLf), -Vector3.Dot(normalizedDir, rotorFw));
      }

      // old way, now I use CalculateDesiredAngle on each rotor, which seems fine
      public float CalcElevation(Vector3 dir, Vector3 planeX, Vector3 planeY)
      {
        Vector3 Up = Vector3.Cross(planeY, planeX);
        Up.Normalize();
        double projectX = Vector3.Dot(planeX, dir);
        double projectY = Vector3.Dot(planeX, dir);
        double projectUp = Vector3.Dot(Up, dir);

        double xySize = Math.Sqrt(projectX * projectX + projectY * projectY);

        program.Echo("xySize: " + xySize + " Up " + projectUp);

        return -(float) Math.Atan2(projectUp, -xySize);
      }

      public float RotateTowardsAngle(IMyMotorStator stator, float targetAngle)
      {
        float angleDiff = (float) ((targetAngle - stator.Angle + Math.PI * 2) % (Math.PI * 2));

        //Echo( " " + angleDiff );

        float speed = Math.Min(30, Math.Abs(angleDiff) * 30);

        if (angleDiff > 0 & angleDiff < 3.2)
          stator.TargetVelocityRPM = speed;
        else
          stator.TargetVelocityRPM = -speed;

        if (Math.Abs(angleDiff) > 0.01)
          return Math.Abs(angleDiff);
        else
          stator.TargetVelocityRPM = 0;
        return 0; //at target
      }

      struct Turret
      {
        public readonly IMyMotorStator[] joints;
        public readonly IMySmallGatlingGun[] guns;
        public Vector3I averageGunPosition;

        public IMyMotorStator baseRotor => joints[0];
        public IMyMotorStator gunsRotor => joints[joints.Length-1];

        public Turret(IMyMotorStator[] joints, IMySmallGatlingGun[] guns)
        {
          this.joints = joints;
          this.guns = guns;
          averageGunPosition = Vector3I.Zero;
          foreach (var mySmallGatlingGun in guns)
            averageGunPosition += mySmallGatlingGun.Position;
          averageGunPosition /= guns.Length;
        }
      }

      private List<Turret> FindAllTurrets()
      {
        List<IMyMotorStator> turretBases = new List<IMyMotorStator>();
        GridTerminalSystem.GetBlocksOfType<IMyMotorStator>( turretBases,  (block) => block.CubeGrid == Me.CubeGrid && block.CustomName.StartsWith("[TR") );
        List<Turret> turrets = new List<Turret>();
        foreach (var myMotorStator in turretBases)
        {
          var turret = BuildTurret(myMotorStator);
          if (turret.joints != null)
          {
            turrets.Add(turret);
          }
        }
        return turrets;
      }

      private Turret BuildTurret(IMyMotorStator turretBase)
      {
        List<IMyMotorStator> notProccessed = new List<IMyMotorStator>(new[] { turretBase });
        List<IMyMotorStator> processed = new List<IMyMotorStator>();
        while (notProccessed.Count == 1)
        {
          var current = notProccessed.Pop();
          var grid = current.TopGrid;
          GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(notProccessed, (block) => block.CubeGrid == grid );
          processed.Add(current);
        }

        if (notProccessed.Count > 1)
        {
          program.logMessages.Enqueue($"Turret {turretBase.Name} has multiple rotors on same level");
          return new Turret();
        }

        // processed already contains at least turretBase
        var weaponGrid = processed[processed.Count - 1].TopGrid;
        var gatlings = new List<IMySmallGatlingGun>();
        GridTerminalSystem.GetBlocksOfType<IMySmallGatlingGun>(gatlings, (block) => block.CubeGrid == weaponGrid);
        if (gatlings.Count == 0)
        {
          program.logMessages.Enqueue($"Turret {turretBase.Name} has no weapons");
          return new Turret();
        }

        return new Turret(processed.ToArray(), gatlings.ToArray());
      }

    }
  }
}
