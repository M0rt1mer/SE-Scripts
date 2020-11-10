using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript {
  static class Extension {

    public static Vector3 Transform( this Matrix3x3 matrix, Vector3 vector ) {

      return new Vector3(matrix.Col0.Dot(vector), matrix.Col1.Dot(vector), matrix.Col2.Dot(vector));
    }

    public static void RayCastGrid(this IMyCubeGrid grid, Vector3 rayStart, Vector3 direction, Action<Vector3I> visitor)
    {
      Matrix worldToLocal = Matrix.Invert(grid.WorldMatrix);

      direction.Normalize();
      Vector3 transformedDirection = Vector3.TransformNormal(direction, worldToLocal);

      Vector3 currentPointF = Vector3.Transform(rayStart, worldToLocal) / grid.GridSize;
      Vector3I currentPoint = Vector3I.Round(currentPointF);

      if (!Inside(grid.Min, grid.Max, currentPoint))
      {
        //find intersection
        BoundingBoxI bbI = new BoundingBoxI(grid.Min, grid.Max);
        var hit = bbI.Intersects( new Ray(currentPointF, transformedDirection) );
        if (!hit.HasValue)
        {
          //Program.instance.Log($"No misses bounding box");
          return; //no intersection
        }
        currentPointF += (float)hit.Value * transformedDirection;
        currentPoint = Vector3I.Round(currentPointF);
      }

      //Program.instance.Log($"Starting at: {currentPoint}");
      //Program.instance.Log($"Direction at: {transformedDirection}");

      while (Inside(grid.Min, grid.Max, currentPoint))
      {
        if (grid.CubeExists(currentPoint))
          visitor(currentPoint);

        float toXShift = ClosestGridBlockChange(currentPointF.X, transformedDirection.X);
        float toYShift = ClosestGridBlockChange(currentPointF.Y, transformedDirection.Y);
        float toZShift = ClosestGridBlockChange(currentPointF.Z, transformedDirection.Z);
        
        currentPointF += transformedDirection * Min(toXShift, toYShift, toZShift);
        currentPoint = Vector3I.Round(currentPointF);

        //Program.instance.Log($"Shift by: {toXShift}, {toYShift}, {toZShift}");
        //Program.instance.Log($"Walk:{currentPoint}");
      }

    }

    private static float Min(float a, float b, float c)
    {
      return (float) Math.Min(a, Math.Min(b, c));
    }

    private static bool Inside(Vector3I min, Vector3 max, Vector3 valToTest)
    {
      return min.X <= valToTest.X &&
             min.Y <= valToTest.Y &&
             min.Z <= valToTest.Z &&
             max.X >= valToTest.X &&
             max.Y >= valToTest.Y &&
             max.Z >= valToTest.Z;
    }

    public static float ClosestGridBlockChange(float current, float direction)
    {
      const float epsilon = 0.01f;
      if (direction > 0)
      {
        var distance = (float)(Math.Ceiling(current) - current);
        return (distance < epsilon) ? 1 : (distance / direction);
      }
      else if(direction < 0)
      {
        var distance = (float)(Math.Floor(current) - current);
        return (distance > -epsilon) ? 1 : (distance / direction); //both negative -> positive
      }
      else
        return float.MaxValue;
    }

  }
}
