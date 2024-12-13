using System.Collections.Generic;
using Flux.API;

namespace FChassis.Core.Drawing;
public class Point3List : List<Point3> { }
public class Point3ListList : List<Point3List> { }

// ----------------------------------------------------------------
public struct PointVec (Point3 pt, Vector3 vec) {
   public Point3 Pt = pt;
   public Vector3 Vec = vec;
   public Point3 Lift (double offset) => Pt + Vec * offset;
   public double DistTo (PointVec rhs) => Pt.DistTo (rhs.Pt);
}

public class PointVecList : List<PointVec> { }