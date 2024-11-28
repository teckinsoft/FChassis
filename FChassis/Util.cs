using FChassis.GCodeGen;
using Flux.API;
using System.Collections.Generic;
using System.IO;
using static System.Math;
namespace FChassis;

using NotchAttribute = Tuple<
        Curve3, // Split curve, whose end point is the notch point
        Vector3, // Start Normal
        Vector3, // End Normal
        Vector3, // Outward Normal along flange
        Vector3, // Vector Outward to nearest boundary
        XForm4.EAxis, // Proximal boundary direction
        bool // Some boolean value
    >;

public class MachinableCutScope {
   public MachinableCutScope (CutScope cs, GCodeGenerator gCGen) {
      ArgumentNullException.ThrowIfNull (cs);
      CutScope = cs;
      ToolingScopes = cs.MachinableToolingScopes;
      mScopeWidth = ToolingScopes.Sum (t => (t.EndX - t.StartX));
      StartX = cs.StartX;
      EndX = cs.EndX;
      GCGen = gCGen;
      GCodeGenerator.CreatePartition (GCGen, ToolingScopes, MCSettings.It.OptimizePartition, /*Utils.CalculateBound3 (tlList)*/cs.Bound);
      SetData ();
      Toolings = ToolingScopes.Select (ts => ts.Tooling).ToList ();
      Bound = cs.Bound;
   }
   public MachinableCutScope (List<Tooling> toolings, GCodeGenerator gCGen) {
      CutScope = null;
      ToolingScopes = ToolingScope.CreateToolingScopes (toolings);
      mScopeWidth = ToolingScopes.Sum (t => (t.EndX - t.StartX));
      StartX = ToolingScopes.Min (ts => ts.StartX);
      EndX = ToolingScopes.Min (ts => ts.EndX);
      GCGen = gCGen;
      Toolings = ToolingScopes.Select (ts => ts.Tooling).ToList ();
      Bound = Utils.CalculateBound3 (Toolings);
      GCodeGenerator.CreatePartition (GCGen, ToolingScopes, MCSettings.It.OptimizePartition, Bound);
      SetData ();
   }

   void SetData () {
      ToolingScopesH1 = ToolingScopes.Where (t => t.Tooling.Head == 0).ToList ();
      ToolingScopesH2 = ToolingScopes.Where (t => t.Tooling.Head == 1).ToList ();
      ToolingScopesWidth = ToolingScopes.Sum (t => (t.EndX - t.StartX));
      ToolingScopesWidthH1 = ToolingScopesH1.Sum (t => (t.EndX - t.StartX));
      ToolingScopesWidthH2 = ToolingScopesH2.Sum (t => (t.EndX - t.StartX));
      ToolingsHead1 = ToolingScopes.Select (ts => ts.Tooling).Where (ts => ts.Head == 0).ToList ();
      ToolingsHead2 = ToolingScopes.Select (ts => ts.Tooling).Where (ts => ts.Head == 1).ToList ();
   }
   public CutScope CutScope { get; set; }
   public GCodeGenerator GCGen { get; set; }
   public List<ToolingScope> ToolingScopes { get; set; }
   public double StartX { get; private set; }
   public double EndX { get; private set; }
   double mScopeWidth;
   public double ScopeWidth { get => mScopeWidth; }
   public List<Tooling> Toolings { get; private set; }
   public List<Tooling> ToolingsHead1 { get; private set; }
   public List<Tooling> ToolingsHead2 { get; private set; }
   public double ToolingScopesWidth { get; set; }
   public double ToolingScopesWidthH1 { get; set; }


   public double ToolingScopesWidthH2 { get; set; }
   public List<ToolingScope> ToolingScopesH1 { get; private set; }
   public List<ToolingScope> ToolingScopesH2 { get; private set; }
   public static List<MachinableCutScope> CreateMachinableCutScopes (List<CutScope> css, GCodeGenerator gcGen) {
      return css.Select (cs => new MachinableCutScope (cs, gcGen)).ToList ();
   }
   public Bound3 Bound { get; private set; }
   //public List<Tooling> ToolingsHead1 () {
   //   return ToolingScopes.Select (ts => ts.Tooling).Where(ts=>ts.Head==0).ToList ();
   //}
   //public List<Tooling> ToolingsHead2 () {
   //   return ToolingScopes.Select (ts => ts.Tooling).Where (ts => ts.Head == 1).ToList ();
   //}
}

internal static class Extensions {
   public static double LieOn (this double f, double a, double b) => (f - a) / (b - a);

   public static bool EQ (this double a, double b) => Abs (a - b) < 1e-6;

   public static bool EQ (this double a, double b, double err) => Abs (a - b) < err;

   public static bool EQ (this float a, float b) => Abs (a - b) < 1e-6;

   public static bool EQ (this float a, float b, double err) => Abs (a - b) < err;

   public static double D2R (this int a) => a * PI / 180;

   public static bool LieWithin (this double val, double leftLimit,
                                 double rightLimit, double epsilon = 1e-6)
      => (leftLimit - epsilon < val && val < rightLimit + epsilon);
}

public class NegZException : Exception {
   // Parameterless constructor
   public NegZException () { }

   // Constructor with a message
   public NegZException (string message)
       : base (message) { }

   // Constructor with a message and an inner exception
   public NegZException (string message, Exception innerException)
       : base (message, innerException) { }
}

public class NotchCreationFailedException : Exception {
   // Parameterless constructor
   public NotchCreationFailedException () { }

   // Constructor with a message
   public NotchCreationFailedException (string message)
       : base (message) { }

   // Constructor with a message and an inner exception
   public NotchCreationFailedException (string message, Exception innerException)
       : base (message, innerException) { }
}
public enum OrdinateAxis {
   Y, Z
}

public enum MachineType {
   LCMLegacy,
   LCMMultipass2H
}

public enum EGCode {
   G0, G1, G2, G3, None
}

public enum EMove {
   Retract,
   Retract2SafeZ,
   SafeZ2SafeZ,
   SafeZ2Retract,
   Retract2Machining,
   Machining,
   RapidPosition
}

/// <summary>
/// Represents the drawable information of a G-Code segment, 
/// which is used for simulation purposes.
/// </summary>
/// <remarks>
/// The <see cref="GCodeGenerator"/> class populates a 
/// <see cref="List{GCodeDrawableSegment}"/> for each tool head.
/// This list can also be populated by parsing the G-Code directly.
/// </remarks>
public class GCodeSeg {
   EGCode mGCode;
   public EGCode GCode => mGCode;

   public GCodeSeg (Point3 stPoint, Point3 endPoint, Vector3 StNormal, Vector3 EndNormal,
                    EGCode gcmd, EMove moveType, string toolingName)
      => SetGCLine (stPoint, endPoint, StNormal, EndNormal, gcmd, moveType, toolingName);

   public GCodeSeg (Arc3 arc, Point3 stPoint, Point3 endPoint, Point3 center, double radius,
                    Vector3 StNormal, EGCode gcmd, EMove moveType, string toolingName)
      => SetGCArc (arc, stPoint, endPoint, center, radius, StNormal, gcmd, moveType, toolingName);

   public GCodeSeg (GCodeSeg rhs) {
      mStartPoint = rhs.mStartPoint;
      mEndPoint = rhs.mEndPoint;
      mRadius = rhs.mRadius;
      mCenter = rhs.mCenter;
      mStartNormal = rhs.mStartNormal;
      mEndNormal = rhs.mEndNormal;
      mMoveType = rhs.mMoveType;
      mArc = rhs.mArc;
      mToolingName = rhs.mToolingName;
      mGCode = rhs.mGCode;
   }

   Point3 mStartPoint, mEndPoint, mCenter;
   Vector3 mStartNormal, mEndNormal;
   double mRadius;
   Arc3 mArc;
   public Arc3 Arc => mArc;
   EMove mMoveType;
   public EMove MoveType => mMoveType;
   string mToolingName;
   public string ToolingName { get => mToolingName; }
   public Point3 StartPoint => mStartPoint;
   public Point3 EndPoint => mEndPoint;
   public Point3 ArcCenter => mCenter;
   public double Radius => mRadius;
   public Vector3 StartNormal => mStartNormal;
   public Vector3 EndNormal => mEndNormal;

   public double Length {
      get {
         if (mGCode is EGCode.G0 or EGCode.G1)
            return StartPoint.DistTo (EndPoint);
         else if (mGCode is EGCode.G2 or EGCode.G3)
            return mArc.Length;

         throw new NotSupportedException ("Unknown G Entity while computing length");
      }
   }

   public void SetGCLine (Point3 stPoint, Point3 endPoint,
                          Vector3 stNormal, Vector3 endNormal,
                          EGCode gcmd, EMove moveType, string toolingName) {
      if (gcmd is not EGCode.G0 and not EGCode.G1)
         throw new InvalidDataException ("The GCode cmd for line is wrong");

      mStartPoint = stPoint; mEndPoint = endPoint; mGCode = gcmd;
      mStartNormal = stNormal; mEndNormal = endNormal;
      mMoveType = moveType;
      mToolingName = toolingName;
   }

   public void SetGCArc (Arc3 arc, Point3 stPoint, Point3 endPoint,
                         Point3 center, double radius, Vector3 stNormal,
                         EGCode gcmd, EMove moveType, string toolingName) {
      if (gcmd is not EGCode.G2 and not EGCode.G3)
         throw new InvalidDataException ("The GCode cmd for Arc is wrong");

      mStartPoint = stPoint;
      mEndPoint = endPoint;
      mCenter = center;
      mRadius = radius;
      mGCode = gcmd;
      mStartNormal = stNormal;
      mArc = arc;
      mMoveType = moveType;
      mToolingName = toolingName;
   }

   public void XfmToMachine (GCodeGenerator codeGen) {
      mStartPoint = GCodeGenerator.XfmToMachine (codeGen, mStartPoint);
      mEndPoint = GCodeGenerator.XfmToMachine (codeGen, mEndPoint);
      mStartNormal = GCodeGenerator.XfmToMachineVec (codeGen, mStartNormal);
      mEndNormal = GCodeGenerator.XfmToMachineVec (codeGen, mEndNormal);
      if (IsArc ())
         mCenter = GCodeGenerator.XfmToMachine (codeGen, mCenter);
   }

   public GCodeSeg XfmToMachineNew (GCodeGenerator codeGGen) {
      GCodeSeg seg = new (this);
      seg.XfmToMachine (codeGGen);
      return seg;
   }

   public bool IsLine ()
      => mGCode is EGCode.G0 or EGCode.G1;

   public bool IsArc ()
      => mGCode is EGCode.G2 or EGCode.G3;
}

public static class Utils {
   public enum EPlane {
      YNeg,
      YPos,
      Top,
      Flex,
      None,
   }

   public enum EFlange {
      Bottom,
      Top,
      Web,
      Flex,
      None
   }

   public enum EArcSense {
      CW, CCW
   }

   //static readonly XForm4 Xfm = new ();
   public const double EpsilonVal = 1e-6;
   public static Color32 LHToolColor = new (57, 255, 20); // neon green
   public static Color32 RHToolColor = new (255, 87, 51); // Neon Red
   public static Color32 G0SegColor = Color32.White;
   public static Color32 G1SegColor = Color32.Blue;
   public static Color32 G2SegColor = Color32.Magenta;
   public static Color32 G3SegColor = Color32.Cyan;

   /// <summary>
   /// This method computes the angle between two vectors about X axis.
   /// The angle is made negative if the cross product of the input vectors
   /// oppose the global X axis.
   /// </summary>
   /// <param name="fromPointPV">The from vector</param>
   /// <param name="toPointPV">The to vector</param>
   /// <returns>The angle between the two vectors specified</returns>
   public static double GetAngleAboutXAxis (Vector3 fromPointPV,
                                            Vector3 toPointPV, XForm4 xfm) {
      //var trfFromPt = xfm * fromPointPV; var trfToPt = xfm * toPointPV;
      var theta = fromPointPV.AngleTo (toPointPV);
      var crossVec = Geom.Cross (fromPointPV, toPointPV);
      //if (!crossVec.IsZero && crossVec.Opposing (XForm4.mXAxis)) 
      //   theta *= -1;
      if (!crossVec.IsZero && crossVec.Opposing (xfm.XCompRot))
         theta *= -1;

      return theta;
   }

   /// <summary>
   /// This method returns the angle between the global Z axis and the normal 
   /// to either of the planes Top, or YPos, or YNeg. 
   /// </summary>
   /// <param name="planeType">One of the EPlane types.</param>
   /// <returns>EPlane Top returns 0.0, EPlane YPos returns -pi/2
   /// and EPlane YNeg returns pi/2 radians</returns>
   /// <exception cref="NotSupportedException">If EPlane is other than YPos, or YNeg or
   /// Top, a NotSupportedException is thrown</exception>
   public static double GetAngle4PlaneTypeAboutXAxis (EPlane planeType) {
      double angleBetweenStartAndEndPoints;
      if (planeType == Utils.EPlane.Top)
         angleBetweenStartAndEndPoints = 0.0;
      else if (planeType == Utils.EPlane.YPos)
         angleBetweenStartAndEndPoints = -Math.PI / 2.0;
      else if (planeType == Utils.EPlane.YNeg)
         angleBetweenStartAndEndPoints = Math.PI / 2.0;
      else
         throw new NotSupportedException ("Unsupported plane type encountered");

      return angleBetweenStartAndEndPoints;
   }

   /// <summary>
   /// This method returns the plane type on which the arc is described.
   /// </summary>
   /// <param name="vec">The vector normal to the arc from the tooling.</param>
   /// <caveat>The vector normal should not be any vector other than the one
   /// obtained from the tooling</caveat>
   /// <returns>Returns one of Eplane.Top, Eplane.YPos, Eplane.YNeg or
   /// Eplane.Top.None</returns>
   public static EPlane GetArcPlaneType (Vector3 vec, XForm4 xfm) {
      xfm ??= XForm4.IdentityXfm;
      var trVec = xfm * vec.Normalized ();
      if (Workpiece.Classify (trVec) == Workpiece.EType.Top)
         return Utils.EPlane.Top;

      if (Workpiece.Classify (trVec) == Workpiece.EType.YPos)
         return Utils.EPlane.YPos;

      if (Workpiece.Classify (trVec) == Workpiece.EType.YNeg)
         return Utils.EPlane.YNeg;

      return EPlane.None;
   }

   /// <summary>
   /// Given a normal vector, this method finds the flange type.
   /// </summary>
   /// <param name="vec">The normal vector to the flange</param>
   /// <param name="xfm">Any additional transformation to the above normal vector</param>
   /// <remarks>The flange type is machine specific. If the xfm is identity, the flange type 
   /// is assumed to be in the local coordinate system of the part</remarks>
   /// <returns>Flange type</returns>
   /// <exception cref="NotSupportedException"></exception>
   public static Utils.EFlange GetArcPlaneFlangeType (Vector3 vec, XForm4 xfm) {
      xfm ??= XForm4.IdentityXfm;
      var trVec = xfm * vec.Normalized ();
      if (Workpiece.Classify (trVec) == Workpiece.EType.Top)
         return EFlange.Web;

      if (Workpiece.Classify (trVec) == Workpiece.EType.YPos)
         return MCSettings.It.PartConfig == MCSettings.PartConfigType.LHComponent
                                                ? EFlange.Top : EFlange.Bottom;

      if (Workpiece.Classify (trVec) == Workpiece.EType.YNeg)
         return MCSettings.It.PartConfig == MCSettings.PartConfigType.LHComponent
                                                ? EFlange.Bottom : EFlange.Top;

      throw new NotSupportedException (" Flange type could not be assessed");
   }

   /// <summary>
   /// This method is a handy one to project a 3d point onto XY or XZ plane
   /// </summary>
   /// <param name="pt">The 3d point</param>
   /// <param name="ep">One of the E3Planes, YNeg, YPos or Top planes</param>
   /// <returns>The 2d point, which is the projection of 3d point on the plane</returns>
   public static Point2 ToPlane (Point3 pt, EPlane ep) => ep switch {
      EPlane.YNeg => new Point2 (pt.X, pt.Z),
      EPlane.YPos => new Point2 (pt.X, pt.Z),
      _ => new Point2 (pt.X, pt.Y),
   };

   /// <summary>
   /// This method returns the EFlange type for the tooling. 
   /// </summary>
   /// <param name="toolingItem">The tooling</param>
   /// <param name="xfm">Any additional transformation to the above normal vector</param>
   /// <remarks>The flange type is machine specific. If the xfm is identity, the flange type 
   /// is assumed to be in the local coordinate system of the part</remarks>
   /// <returns>The EFlange type for the tooling, considering if the workpiece
   /// is LH or RH component, set.</returns>
   /// <exception cref="NotSupportedException">This exception is thrown if the plane type 
   /// could not be deciphered</exception>
   public static EFlange GetFlangeType (Tooling toolingItem, XForm4 xfm) {
      xfm ??= XForm4.IdentityXfm;
      var trVec = xfm * toolingItem.Start.Vec.Normalized ();

      if (toolingItem.IsPlaneFeature ()) {
         if (Workpiece.Classify (trVec) == Workpiece.EType.Top)
            return EFlange.Web;

         // TODO IMPORTANT clarify with Dinesh top or bottom
         if (Workpiece.Classify (trVec) == Workpiece.EType.YPos)
            return MCSettings.It.PartConfig == MCSettings.PartConfigType.LHComponent
                                                   ? EFlange.Top : EFlange.Bottom;

         if (Workpiece.Classify (trVec) == Workpiece.EType.YNeg)
            return MCSettings.It.PartConfig == MCSettings.PartConfigType.LHComponent
                                                   ? EFlange.Bottom : EFlange.Top;
      }

      if (toolingItem.IsFlexFeature ())
         return Utils.EFlange.Flex;

      throw new NotSupportedException (" Flange type could not be assessed");
   }

   /// <summary>
   /// THis methos returns the plane type given the tooling
   /// </summary>
   /// <param name="toolingItem">The input tooling item.</param>
   /// <param name="xfm">Any additional transformation to the above normal vector</param>
   /// <returns>Returns one of the EPlane types such as EPlane.Top, YPos or YNeg</returns>
   /// <exception cref="InvalidOperationException">If the plane type could not be deciphered.</exception>
   public static EPlane GetPlaneType (Tooling toolingItem, XForm4 xfm) {
      xfm ??= XForm4.IdentityXfm;
      var trVec = xfm * toolingItem.Start.Vec.Normalized ();
      if (toolingItem.IsPlaneFeature ()) {
         if (Workpiece.Classify (trVec) == Workpiece.EType.Top)
            return EPlane.Top;

         if (Workpiece.Classify (trVec) == Workpiece.EType.YPos)
            return EPlane.YPos;

         if (Workpiece.Classify (trVec) == Workpiece.EType.YNeg)
            return EPlane.YNeg;
      }

      if (toolingItem.IsFlexFeature ())
         return Utils.EPlane.Flex;

      throw new InvalidOperationException (" The feature is neither plane nor flex");
   }

   public static EPlane GetPlaneType (Vector3 normal, XForm4 xfm)
      => GetArcPlaneType (normal, xfm);

   /// <summary>
   /// This method returns the Vector3 normal emanating from the E3Plane
   /// given the tooling.
   /// </summary>
   /// <param name="toolingItem">The input tooling</param>
   /// <param name="xfm">Any additional transformation to the above normal vector</param>
   /// <returns>The normal to the plane in the direction emanating 
   /// from the plane</returns>
   /// <exception cref="InvalidOperationException"></exception>
   public static Vector3 GetEPlaneNormal (Tooling toolingItem, XForm4 xfm) {
      xfm ??= XForm4.IdentityXfm;
      var trVec = xfm * toolingItem.Start.Vec.Normalized ();
      if (toolingItem.IsPlaneFeature ()) {
         if (Workpiece.Classify (trVec) == Workpiece.EType.Top)
            return XForm4.mZAxis;

         if (Workpiece.Classify (trVec) == Workpiece.EType.YPos)
            return XForm4.mYAxis;

         if (Workpiece.Classify (trVec) == Workpiece.EType.YNeg)
            return -XForm4.mYAxis;
      }

      if (toolingItem.IsFlexFeature ()) {
         var segs = toolingItem.Segs;
         Vector3 n = new (0.0, Math.Sqrt (2.0), Math.Sqrt (2.0));
         var yNegFlex = segs.Any (cutSeg => cutSeg.Vec0.Normalized ().Y < -0.1);
         if (yNegFlex)
            n = new (0.0, -Math.Sqrt (2.0), Math.Sqrt (2.0));

         return n;
      }

      throw new InvalidOperationException (" The feature is neither plane nor flex");
   }

   /// <summary>This method finds the ordinate direction of the given vector featureNormal.</summary>
   /// <returns>
   /// One of the ordinate direction (FChassisUtils.EPlane.Top or FChassisUtils.EPlane.YNeg or FChassisUtils.EPlane.YPos )
   /// which strictly aligns [ Abs(dotproduct) = 1.0 ] with the featureNormal. 
   /// Returns FChassisUtils.EPlane.Flex for other cases
   /// </returns>

   /// <summary>This method finds the ordinate direction of the given vector featureNormal.</summary>
   /// <param name="featureNormal">The normal to the feature ( line,arc or circle)</param>
   /// <param name="trf">Any additional transformation to the above normal vector</param>
   /// <returns>
   /// One of the ordinate direction (FChassisUtils.EPlane.Top or FChassisUtils.EPlane.YNeg or FChassisUtils.EPlane.YPos )
   /// which strictly aligns [ Abs(dotproduct) = 1.0 ] with the featureNormal. 
   /// Returns FChassisUtils.EPlane.Flex for other cases
   /// </returns>
   /// <exception cref="NegZException">This exception is thrown if a negative Z normal is encountered</exception>
   public static EPlane GetFeatureNormalPlaneType (Vector3 featureNormal, XForm4 trf) {
      featureNormal = featureNormal.Normalized ();
      trf ??= XForm4.IdentityXfm;
      featureNormal = trf * featureNormal;
      var zAxis = new Vector3 (0, 0, 1);
      var yAxis = new Vector3 (0, 1, 0);
      var featureNormalDotZAxis = featureNormal.Dot (zAxis);
      var featureNormalDotYAxis = featureNormal.Dot (yAxis);

      if (Math.Abs (featureNormalDotZAxis - 1.0) < EpsilonVal)
         return EPlane.Top;

      if (Math.Abs (featureNormalDotZAxis + 1.0) < EpsilonVal)
         throw new NegZException ("Negative Z axis feature normal encountered");

      if (Math.Abs (featureNormalDotYAxis - 1.0) < EpsilonVal)
         return EPlane.YPos;

      if (Math.Abs (featureNormalDotYAxis + 1.0) < EpsilonVal)
         return EPlane.YNeg;

      return EPlane.Flex;
   }

   /// <summary>
   /// This method finds the cross product between two vectors (a cross b)
   /// </summary>
   /// <param name="a">First vector</param>
   /// <param name="b">Seond vector</param>
   /// <returns></returns>
   public static Vector3 CrossProduct (Vector3 a, Vector3 b) {
      return new Vector3 (
          a.Y * b.Z - a.Z * b.Y,
          a.Z * b.X - a.X * b.Z,
          a.X * b.Y - a.Y * b.X);
   }

   public static bool IsCircle (Curve3 curve)
      => curve != null && curve is Arc3 && curve.Start.EQ (curve.End);

   public static bool IsArc (Curve3 curve)
      => curve != null && curve is Arc3 && !curve.Start.EQ (curve.End);

   /// <summary>
   /// This method creates and returns a point, which is moved from the 
   /// input point, alone the direction specified upto a specific distance
   /// along that vector.
   /// </summary>
   /// <param name="pt">The input ref point</param>
   /// <param name="dir">The direction along which the new point has to be computed</param>
   /// <param name="moveLength">The distance by which the new point has to be moved 
   /// along the direction</param>
   /// <returns>The new point from a ref point, along a direction, by a specific distance.</returns>
   public static Point3 MovePoint (Point3 pt, Vector3 dir, double moveLength) {
      var normDir = dir.Normalized ();
      return (pt + normDir * moveLength);
   }

   /// <summary>
   /// This method discretizes a given arc with no of steps input
   /// </summary>
   /// <param name="seg">The given arc segment</param>
   /// <param name="steps">No of steps</param>
   /// <returns>List of Tuples of the intermediate points with a linearly interpolated normals</returns>
   public static List<Tuple<Point3, Vector3>> DiscretizeArc (GCodeSeg seg, int steps) {
      List<Tuple<Point3, Vector3>> res = null;
      if (!seg.IsArc () || (seg.GCode != EGCode.G2 && seg.GCode != EGCode.G3))
         return res;

      res = [];
      double theta = 2 * Math.PI;
      Vector3 center2Start = seg.StartPoint - seg.ArcCenter;
      Vector3 center2End = seg.EndPoint - seg.ArcCenter;
      Vector3 crossVec = Geom.Cross (center2Start, center2End);
      if (crossVec.Length.EQ (0)) {
         var otherPt = Geom.P2V (Geom.Evaluate (seg.Arc, 0.5, seg.StartNormal));
         crossVec = Geom.Cross (center2Start, otherPt);
      }

      if (!seg.StartPoint.DistTo (seg.EndPoint).EQ (0)) {
         var val = center2Start.Dot (center2End) / (center2Start.Length * center2End.Length);
         if (val < -1)
            val += 1e-6;
         else if (val > 1)
            val -= 1e-6;

         val = val.Clamp (-1.0, 1.0);
         theta = Math.Acos (val);

         if (seg.GCode == EGCode.G3 && crossVec.Dot (seg.StartNormal) < 0.0)
            theta = 2 * Math.PI - theta;
         else if (seg.GCode == EGCode.G2 && crossVec.Dot (seg.StartNormal) > 0.0)
            theta = 2 * Math.PI - theta;

         if (seg.GCode == EGCode.G2 && theta > 0.0)
            theta = -theta;
      }

      double delAlpha = theta / steps;
      for (int k = 0; k <= steps; k++) {
         double alphaK = k * delAlpha;
         Vector3 comp1 = center2Start * Math.Cos (alphaK);
         Vector3 comp2 = Geom.Cross (seg.StartNormal, center2Start) * Math.Sin (alphaK);
         Vector3 comp3 = seg.StartNormal * seg.StartNormal.Dot (center2Start) * (1.0 - Math.Cos (alphaK));
         Vector3 vRot = comp1 + comp2 + comp3;
         res.Add (new Tuple<Point3, Vector3> (seg.ArcCenter + Geom.V2P (vRot), seg.StartNormal));
      }

      return res;
   }

   /// <summary>
   /// This method discretizes a given line segment
   /// </summary>
   /// <param name="seg">Segment to be discretized</param>
   /// <param name="steps">No of each discretized line segments</param>
   /// <returns>A list of tuples of intermediate points, interpolated with normals</returns>
   public static List<Tuple<Point3, Vector3>> DiscretizeLine (GCodeSeg seg, int steps) {
      List<Tuple<Point3, Vector3>> res = null;
      if (seg.IsArc () || (seg.GCode != EGCode.G0 && seg.GCode != EGCode.G1))
         return res;

      res = [];
      double stepLength = seg.StartPoint.DistTo (seg.EndPoint) / steps;
      var prevNormal = seg.StartNormal.Normalized ();

      // For smooth transitioning of the tool, the normal's change from previous seg's last point to the 
      // current seg's last point should be gradual. This requires linear interpolation.
      res.Add (new Tuple<Point3, Vector3> (seg.StartPoint, seg.StartNormal.Normalized ()));
      for (int k = 1; k < steps - 1; k++) {
         var pt1 = Utils.MovePoint (seg.StartPoint,
                                    Geom.P2V (seg.EndPoint) - Geom.P2V (seg.StartPoint), k * stepLength);
         var angleBetweenStNormalEndNormal = Utils.GetAngleAboutXAxis (seg.StartNormal,
                                                                       seg.EndNormal,
                                                                       XForm4.IdentityXfm);
         if (angleBetweenStNormalEndNormal.EQ (0.0))
            res.Add (new Tuple<Point3, Vector3> (pt1, seg.StartNormal.Normalized ()));
         else if ((Math.Abs ((seg.EndPoint - seg.StartPoint).Normalized ().Dot (XForm4.mZAxis)) - 1.0).EQ (0.0)) {
            var t = (double)k / (double)(steps - 1);
            var newNormal = seg.StartNormal.Normalized () * (1 - t) + seg.EndNormal.Normalized () * t;
            newNormal = newNormal.Normalized ();
            res.Add (new Tuple<Point3, Vector3> (pt1, newNormal));
         } else {
            var pt0 = Utils.MovePoint (seg.StartPoint,
                                       Geom.P2V (seg.EndPoint) - Geom.P2V (seg.StartPoint),
                                       (k - 1) * stepLength);
            var pt2 = Utils.MovePoint (seg.StartPoint,
                                       Geom.P2V (seg.EndPoint) - Geom.P2V (seg.StartPoint),
                                       (k + 1) * stepLength);
            var norm1 = Geom.Cross ((pt1 - pt0), XForm4.mXAxis);
            if (norm1.Opposing (prevNormal))
               norm1 = -norm1;

            prevNormal = norm1;

            var norm2 = Geom.Cross ((pt2 - pt1), XForm4.mXAxis);
            if (norm2.Opposing (prevNormal))
               norm2 = -norm2;

            var norm = ((norm2 + norm1) * 0.5).Normalized ();
            prevNormal = norm;
            res.Add (new Tuple<Point3, Vector3> (pt1, norm));
         }
      }
      res.Add (new Tuple<Point3, Vector3> (seg.EndPoint, seg.EndNormal.Normalized ()));
      return res;
   }

   /// <summary>
   /// This is an utility method to return the ordinate vector from
   /// input aaxis
   /// </summary>
   /// <param name="axis">The axis</param>
   /// <returns>The vector that the input axis' points to</returns>
   /// <exception cref="NotSupportedException">If an axis is non-ordinate, an exception is
   /// thrown</exception>
   public static Vector3 GetUnitVector (XForm4.EAxis axis) {
      Vector3 res = axis switch {
         XForm4.EAxis.NegZ => -XForm4.mZAxis,
         XForm4.EAxis.Z => XForm4.mZAxis,
         XForm4.EAxis.NegX => -XForm4.mXAxis,
         XForm4.EAxis.X => XForm4.mXAxis,
         XForm4.EAxis.NegY => -XForm4.mYAxis,
         XForm4.EAxis.Y => XForm4.mYAxis,
         _ => throw new NotSupportedException ("Unsupported XForm.EAxis type")
      };

      return res;
   }

   /// <summary>
   /// This method returns the scrap side or material removal side direction w.r.t the tooling start segment
   /// w.r.t to the mid point of the first segment
   /// </summary>
   /// <param name="tooling"></param>
   /// <returns>If Line or Arc Thsi returns the mid point of the tooling segment along with the material 
   /// removal side direction evaluated at the mid point.
   /// If Circle, this returns the start point of the circle and the material removal direction 
   /// evaluated at the starting point.</returns>
   /// </exception>
   public static Tuple<Point3, Vector3> GetMaterialRemovalSideDirection (Tooling tooling) {
      var segmentsList = tooling.Segs.ToList ();
      var toolingPlaneNormal = segmentsList[0].Vec0;

      // Tooling direction as the direction of the st to end point in the case of line OR
      // tangent int he direction of start to end of the arc in the case of an arc
      Vector3 toolingDir;
      Point3 newToolingEntryPoint;
      if (Utils.IsCircle (segmentsList[0].Curve))
         newToolingEntryPoint = segmentsList[0].Curve.Start;
      else
         newToolingEntryPoint = Geom.GetMidPoint (segmentsList[0].Curve, toolingPlaneNormal);

      if (segmentsList[0].Curve is Arc3 arc)
         (toolingDir, _) = Geom.EvaluateTangentAndNormalAtPoint (arc, newToolingEntryPoint, toolingPlaneNormal);
      else
         toolingDir = (segmentsList[0].Curve.End - segmentsList[0].Curve.Start).Normalized ();

      // Ref points along the direction of the binormal, which is along or opposing the direction
      // in which the material removal side exists.
      var biNormal = Geom.Cross (toolingDir, toolingPlaneNormal).Normalized ();
      Vector3 scrapSideDirection = biNormal.Normalized ();
      if (Geom.Cross (toolingDir, biNormal).Opposing (toolingPlaneNormal))
         scrapSideDirection = -biNormal;

      return new (newToolingEntryPoint, scrapSideDirection);
   }

   /// <summary>
   /// This method returns the vector from a point on a contour towards nearest 
   /// proximal boundary that is happening on -X or -Z axis. The magnitude of this 
   /// vector is the distance to boundary from the given point
   /// </summary>
   /// <param name="pt">The input point</param>
   /// <param name="bound">The bounds of the current tooling</param>
   /// <param name="toolingItem">The tooling</param>
   /// <param name="proxBdy">The ordinate Axis of the proximal boundary vector. This is significant 
   /// if the given point is itself is at X=0.</param>
   /// <returns>The vector from the given point to the point on the nearest boundary along -X or X or -Z</returns>
   /// <exception cref="Exception">If the notch type is of type unknown, an exception is thrown</exception>
   static Vector3 GetVectorToProximalBoundary (Point3 pt, Bound3 bound, ToolingSegment seg,
                                               ECutKind profileKind, out XForm4.EAxis proxBdy) {
      Vector3 res;
      Point3 bdyPtXMin, bdyPtXMax, bdyPtZMin;
      switch (profileKind) {
         case ECutKind.Top:
         case ECutKind.YPosFlex:
         case ECutKind.YNegFlex:
            if (pt.DistTo (bdyPtXMin = new Point3 (bound.XMin, pt.Y, pt.Z))
                  < pt.DistTo (bdyPtXMax = new Point3 (bound.XMax, pt.Y, pt.Z))) {
               res = bdyPtXMin - pt;
               proxBdy = XForm4.EAxis.NegX;
            } else {
               res = bdyPtXMax - pt;
               proxBdy = XForm4.EAxis.X;
            }

            if (profileKind == ECutKind.YPosFlex || profileKind == ECutKind.YNegFlex) {
               Vector3 p1p2;

               if (seg.Curve is Arc3 arc) {
                  (var _, p1p2) = Geom.EvaluateTangentAndNormalAtPoint (arc, pt, seg.Vec0);
               } else
                  p1p2 = (seg.Curve.End - seg.Curve.Start).Normalized ();

               var planeType = Utils.GetArcPlaneType (seg.Vec0, XForm4.IdentityXfm);
               if (planeType == EPlane.YNeg || planeType == EPlane.YPos) {
                  var resDir = res.Normalized (); // along x direction

                  // p1p2 may be along -Z direction
                  if (p1p2.Opposing (resDir))
                     p1p2 *= -1;

                  var thetaP1P2WithX = Math.Acos (p1p2.Dot (resDir)); // Keep res if theta is max
                  var thetaP1P2WithMinusZ = Math.Acos (p1p2.Dot (XForm4.mZAxis * -1));
                  if (thetaP1P2WithMinusZ > thetaP1P2WithX)
                     return new Vector3 (0, 0, bound.ZMin);
                  else
                     return res;
               }
            }
            break;

         case ECutKind.YPos:
         case ECutKind.YNeg:
            bdyPtZMin = new Point3 (pt.X, pt.Y, bound.ZMin);
            res = bdyPtZMin - pt;
            proxBdy = XForm4.EAxis.NegZ;
            break;

         default:
            throw new Exception ("Unknown notch type encountered");
      }

      return res;
   }

   /// <summary>
   /// This is the primary method to evaluate the notch point on a tooling item. The tooling item contains
   /// the segments, which are a list of Larcs (Line and Arcs), Line3 in 2d and 3d and planar arcs.
   /// </summary>
   /// <param name="segments">The list of tooling segments on which the notch points need to be evaluated</param>
   /// <param name="percentage">This is the ratio of the length from start of the tooling segments' total length.
   /// <param name="leastCurveLength">This least possible length of the curve, 
   /// below which it is assumed a curve of zero length</param>
   /// <returns>A tuple of the index of the occurance of the point and the point itself, at the percentage of 
   /// the total length of the entire tooling</returns>
   /// <exception cref="Exception">An exception is thrown if the percentage is < 0 or more than 100</exception>
   public static Tuple<int, Point3> GetNotchPointsOccuranceParams (List<ToolingSegment> segments,
                                                                   double percentage, double leastCurveLength = 0.5) {
      if (percentage < 1e-6 || percentage > 1.0 - 1e-6)
         throw new Exception ("Notch entry points can not be lesser 0% or more than 100%");

      var totalSegsLength = segments.Sum (seg => seg.Curve.Length);
      double percentLength = percentage * totalSegsLength;
      double segmentLengthAtNotch = 0;
      int jj = 0;
      while (segmentLengthAtNotch < percentLength) {
         segmentLengthAtNotch += segments[jj].Curve.Length;
         jj++;
      }

      var segmentLength = percentLength;
      int occuranceIndex = jj - 1;
      double previousCurveLengths = 0.0;
      for (int kk = occuranceIndex - 1; kk >= 0; kk--)
         previousCurveLengths += segments[kk].Curve.Length;

      segmentLength -= previousCurveLengths;

      // 25% of length can not happen to be almost close to the first segment's start point
      // but shall happen for the second segment onwards
      Point3 notchPoint;
      Tuple<int, Point3> notchPointOccuranceParams;
      var distToPrevSegsEndPoint = leastCurveLength;
      if (segmentLength < distToPrevSegsEndPoint)
         // in case of segmentLength is less than threshold, the notch attr is set as the 
         // index of the previous segments index and the point to be the end point of the 
         // previous segment's index.
         notchPointOccuranceParams = new (occuranceIndex - 1, segments[occuranceIndex - 1].Curve.End);
      else if (segments[occuranceIndex].Curve.Length - segmentLength < distToPrevSegsEndPoint)
         notchPointOccuranceParams = new (occuranceIndex, segments[occuranceIndex].Curve.End);
      else {
         notchPoint = Geom.GetPointAtLengthFromStart (segments[occuranceIndex].Curve,
                                                      segments[occuranceIndex].Vec0.Normalized (),
                                                      segmentLength);

         notchPointOccuranceParams = new (occuranceIndex, notchPoint);
      }

      return notchPointOccuranceParams;
   }

   /// <summary>
   /// This method computes the notch attribute at a given notch point and the index of the tooling segments.
   /// This notch attribute contains the curve segment. The end point of the curve segment will be the notch point
   /// 
   /// TODO: The notch attribute is yet to be optimized. It will be optimized in the subsequent iterations.
   /// </summary>
   /// <param name="bound">The bounds of the tooling</param>
   /// <param name="toolingItem">The tooling item, which has the list of Larcs.</param>
   /// <param name="segments">The list of Larcs</param>
   /// <param name="segIndex">The index of the list of Larcs at which the notch point exists</param>
   /// <param name="notchPoint">The notch point, in the given context, is an unique point on the curve at which 
   /// it is desired to approach the cutting tool to start cutting the segments or leave gap with a small cut 
   /// so as to make it easy to remove the possibly heavy scrap material after cut,</param>
   /// <returns>The notch atrribute, which is a tuple of the following. The Curve, Start normal, The end Normal
   /// Normal along the flange of the curve segment, The outward vector to the nearest proximal boundary 
   /// The EAxis to the proximal boundary ( in the case of the previous vector being 0) and a flag</returns>
   /// <exception cref="NotSupportedException">An exception is thrown if the outward vector does not point 
   /// to the NegX, or X or Neg Z</exception>
   public static NotchAttribute ComputeNotchAttribute (Bound3 bound, Tooling toolingItem,
                                                       List<ToolingSegment> segments,
                                                       int segIndex, Point3 notchPoint) {
      if (segIndex == -1)
         return new NotchAttribute (null, new Vector3 (), new Vector3 (),
                                    new Vector3 (), new Vector3 (),
                                    XForm4.EAxis.Z, false);

      XForm4.EAxis proxBdyStart;
      Vector3 outwardNormalAlongFlange;
      Vector3 vectorOutwardAtStart, vectorOutwardAtEnd, vectorOutwardAtSpecPoint;
      if (segments[segIndex].Curve is Arc3 arc) {
         (var center, _) = Geom.EvaluateCenterAndRadius (arc);
         var vectorOutwardAtSpecPt = GetVectorToProximalBoundary (notchPoint, bound,
                                                                  segments[segIndex], toolingItem.ProfileKind,
                                                                  out proxBdyStart);
         var flangeNormalVecAtSpecPt = (notchPoint - center).Normalized ();
         if (GetUnitVector (proxBdyStart).Opposing (flangeNormalVecAtSpecPt)) flangeNormalVecAtSpecPt *= -1.0;
         return new NotchAttribute (segments[segIndex].Curve, segments[segIndex].Vec0.Normalized (),
                                    segments[segIndex].Vec1.Normalized (),
                                    flangeNormalVecAtSpecPt.Normalized (),
                                    vectorOutwardAtSpecPt, proxBdyStart, true);
      } else {
         var line = segments[segIndex].Curve as Line3;
         var p1p2 = line.End - line.Start;
         vectorOutwardAtStart = GetVectorToProximalBoundary (line.Start, bound, segments[segIndex],
                                                             toolingItem.ProfileKind, out proxBdyStart);
         vectorOutwardAtEnd = GetVectorToProximalBoundary (line.End, bound, segments[segIndex],
                                                           toolingItem.ProfileKind, out _);
         vectorOutwardAtSpecPoint = GetVectorToProximalBoundary (notchPoint, bound, segments[segIndex],
                                                           toolingItem.ProfileKind, out _);
         Vector3 bdyVec = proxBdyStart switch {
            XForm4.EAxis.NegX => -XForm4.mXAxis,
            XForm4.EAxis.X => XForm4.mXAxis,
            XForm4.EAxis.NegZ => -XForm4.mZAxis,
            _ => throw new NotSupportedException ("Outward vector can not be other than NegX, X, and NegZ")
         };

         outwardNormalAlongFlange = new Vector3 ();
         if (vectorOutwardAtStart.Length.EQ (0) || vectorOutwardAtEnd.Length.EQ (0))
            outwardNormalAlongFlange = bdyVec;
         else {
            int nc = 0;
            do {
               if (!Geom.Cross (p1p2, bdyVec).Length.EQ (0)) {
                  outwardNormalAlongFlange = Geom.Cross (segments[segIndex].Vec0.Normalized (),
                                                         p1p2).Normalized ();
                  if (outwardNormalAlongFlange.Opposing (bdyVec))
                     outwardNormalAlongFlange *= -1.0;
                  break;
               } else
                  p1p2 = Geom.Perturb (p1p2);

               ++nc;
               if (nc > 10)
                  break;
            } while (true);
         }
      }

      return new NotchAttribute (segments[segIndex].Curve,
                                 segments[segIndex].Vec0.Normalized (),
                                 segments[segIndex].Vec1.Normalized (),
                                 outwardNormalAlongFlange.Normalized (),
                                 vectorOutwardAtSpecPoint, proxBdyStart, true);
   }

   /// <summary>
   /// This method is used to check the sanity of the tooling segments by checking the 
   /// G0 continuity
   /// </summary>
   /// <param name="segs">The input list of tooling segments</param>
   /// <exception cref="Exception">An exception is thrown if any segmnt misses 
   /// G0 continuity with its neighbor (with in a general tolerance of 1e-6)</exception>
   public static void CheckSanityOfToolingSegments (List<ToolingSegment> segs) {
      for (int ii = 1; ii < segs.Count; ii++) {
         //var dist = segs[ii - 1].Curve.End.DistTo (segs[ii].Curve.Start);
         if (!segs[ii - 1].Curve.End.DistTo (segs[ii].Curve.Start).EQ (0, 1e-3))
            throw new Exception ("There is a discontinuity in tooling segments");
      }
   }

   /// <summary>
   /// This method shall be invoked if the tooling segments exhibit a C-0 discontinuity 
   /// This is addressed by the following strategy.
   /// if (i-1)th line seg ends at discontinuity with i-th segment, a new line seg is created
   /// in the place of i-1th seg, whose start position is old i-1-th pos and end pos is i-th seg's start.
   /// if (i-1)th arc seg ends at discontinuity with i-th segment, a new line seg is created
   /// a new line is created from i-1-th arc seg's end to i-th seg's end and replaced with i-th seg
   /// if (i-1)th line seg ends at discontinuity with i-th arc segment, a new line seg is created
   /// from i-1-th seg's start to i-th seg's start and replaced with i-1th line segment
   /// if (i-1)th arc seg ends at a discontinuity with i-th arc seg, then a line segment is 
   /// inserted in between the two arc segments
   /// </summary>
   /// <param name="segs"></param>
   /// <returns></returns>
   /// <exception cref="Exception">Throws an exception if the types of the segments are neither arc
   /// nor line</exception>
   public static List<ToolingSegment> FixSanityOfToolingSegments (ref List<ToolingSegment> segs) {
      var fixedSegs = segs;
      for (int ii = 1; ii < fixedSegs.Count; ii++) {
         if (!fixedSegs[ii - 1].Curve.End.DistTo (fixedSegs[ii].Curve.Start).EQ (0)) {
            if (fixedSegs[ii - 1].Curve is Line3 || fixedSegs[ii].Curve is Line3) {
               var newLine = new Line3 (fixedSegs[ii - 1].Curve.Start, fixedSegs[ii].Curve.Start);
               var newTS = Geom.CreateToolingSegmentForCurve (newLine as Curve3, fixedSegs[ii - 1].Vec0, fixedSegs[ii].Vec0);
               fixedSegs[ii - 1] = newTS;
            } else if (fixedSegs[ii - 1].Curve is Arc3 || fixedSegs[ii].Curve is Line3) {
               var newLine = new Line3 (fixedSegs[ii - 1].Curve.End, fixedSegs[ii].Curve.End);
               var newTS = Geom.CreateToolingSegmentForCurve (newLine as Curve3, fixedSegs[ii - 1].Vec1, fixedSegs[ii].Vec1);
               fixedSegs[ii] = newTS;
            } else if (fixedSegs[ii - 1].Curve is Line3 || fixedSegs[ii].Curve is Arc3) {
               var newLine = new Line3 (fixedSegs[ii - 1].Curve.Start, fixedSegs[ii].Curve.Start);
               var newTS = Geom.CreateToolingSegmentForCurve (newLine as Curve3, fixedSegs[ii - 1].Vec0, fixedSegs[ii].Vec0);
               fixedSegs[ii - 1] = newTS;
            } else if (fixedSegs[ii - 1].Curve is Arc3 || fixedSegs[ii].Curve is Arc3) {
               // Create a link line between arcs
               var newLine = new Line3 (fixedSegs[ii - 1].Curve.End, fixedSegs[ii].Curve.Start);
               var newTS = Geom.CreateToolingSegmentForCurve (newLine as Curve3, fixedSegs[ii - 1].Vec1, fixedSegs[ii].Vec0);
               fixedSegs.Insert (ii, newTS); ii--;
            } else
               throw new Exception ("Utils.FixSanityOfToolingSegments: Unknown segment type encountered");
         }
      }

      CheckSanityOfToolingSegments (fixedSegs);
      return fixedSegs;
   }

   /// <summary>
   /// This method is used to split the given tooling segments as defined by the points
   /// prescribed in the notchPointsInfo list
   /// </summary>
   /// <param name="segments">The input segments and also the output</param>
   /// <param name="notchPtsInfo">The input notchPointsInfo list and also the output</param>
   /// <param name="tolerance">The epsilon tolerance, which is by default 1e-6</param>
   public static void SplitToolingSegmentsAtPoints (ref List<ToolingSegment> segments,
                                                    ref List<NotchPointInfo> notchPtsInfo,
                                                    double tolerance = 1e-6) {
      for (int ii = 0; ii < notchPtsInfo.Count; ii++) {
         if (notchPtsInfo[ii].mSegIndex == -1) continue;
         var crvs = Geom.SplitCurve (segments[notchPtsInfo[ii].mSegIndex].Curve,
                                     notchPtsInfo[ii].mPoints,
                                     segments[notchPtsInfo[ii].mSegIndex].Vec0.Normalized (),
                                     deltaBetween: 0, tolerance);
         int stIndex = notchPtsInfo[ii].mSegIndex;
         List<NotchPointInfo> newNPInfo = [];
         if (crvs.Count > 1) {
            var toolSegsForCrvs = Geom.CreateToolingSegmentForCurves (segments[notchPtsInfo[ii].mSegIndex], crvs);
            segments.RemoveAt (notchPtsInfo[ii].mSegIndex);
            segments.InsertRange (notchPtsInfo[ii].mSegIndex, toolSegsForCrvs);

            // Create new entries for notchPointsInfo for segindex 
            for (int jj = 0; jj < crvs.Count; jj++) {
               int nptIdx = notchPtsInfo[ii].mPoints
                                 .FindIndex (pt => pt.DistTo (crvs[jj].End).EQ (0));
               if (nptIdx != -1) {
                  NotchPointInfo nInfo = new () {
                     mSegIndex = stIndex++,
                     mPercentage = notchPtsInfo[ii].mPercentage,
                     mPoints = [],
                     mPosition = notchPtsInfo[ii].mPosition
                  };

                  nInfo.mPoints.Add (crvs[jj].End);
                  newNPInfo.Add (nInfo);
               }
            }

            notchPtsInfo.RemoveAt (ii);
            notchPtsInfo.InsertRange (ii, newNPInfo);

            // Update SegIndex in indexObjects
            for (int jj = ii + newNPInfo.Count; jj < notchPtsInfo.Count; jj++) {
               var npInfoObj = notchPtsInfo[jj];
               npInfoObj.mSegIndex += crvs.Count - 1;
               if (notchPtsInfo[jj].mSegIndex != -1)
                  notchPtsInfo[jj] = npInfoObj;
            }
         }
      }
   }

   /// <summary>
   /// This mwthod is a wrapper to Geom.SplitCurve, which splits the list of toolingSegments input 
   /// at the given point.
   /// </summary>
   /// <param name="segments">The list of tooling segments</param>
   /// <param name="segIndex">The segment's index in the list of tooling segment</param>
   /// <param name="point">The point at which the split is needed</param>
   /// <param name="fpn">The Feature Plane Normal, which should be the local normal to the segment</param>
   /// <param name="tolerance">The epsilon tolerance, which is by default 1e-6</param>
   /// <returns>The list of tooling segments that got created. If no curves were split, it returns an 
   /// empty list</returns>
   public static List<ToolingSegment> SplitToolingSegmentsAtPoint (
                                          List<ToolingSegment> segments,
                                          int segIndex, Point3 point, Vector3 fpn,
                                          double tolerance = 1e-6) {
      List<Point3> intPoints = [point];
      // Consistency check
      if (segments.Count == 0 || segIndex < 0 || segIndex >= segments.Count ||
         !Geom.IsPointOnCurve (segments[segIndex].Curve, point, fpn, tolerance))
         //return toolSegs;
         throw new Exception ("SplitToolingSegmentsAtPoint: Point not on the curve");

      var crvs = Geom.SplitCurve (segments[segIndex].Curve, intPoints, fpn,
                                  deltaBetween: 0, tolerance);
      var toolSegsForCrvs = Geom.CreateToolingSegmentForCurves (segments[segIndex], crvs);

      return toolSegsForCrvs;
   }

   /// <summary>
   /// This method is a predicate that tells if a segment is on the E3Flex. 
   /// TODO: In the subsequent iterations, an elegant way will be found to check 
   /// if the segment is on the E3Flex using projection/unprojection
   /// </summary>
   /// <param name="stNormal">The start normal of the segment</param>
   /// <param name="endNormal">The end normal of the segment.</param>
   /// <returns></returns>
   /// <exception cref="Exception"></exception>
   public static bool IsToolingOnFlex (Vector3 stNormal, Vector3 endNormal) {
      stNormal = stNormal.Normalized (); endNormal = endNormal.Normalized ();
      if ((stNormal.Dot (XForm4.mZAxis).EQ (1) && endNormal.Dot (XForm4.mZAxis).EQ (1))
           || (stNormal.Dot (XForm4.mYAxis).EQ (1) && endNormal.Dot (XForm4.mYAxis).EQ (1))
           || (stNormal.Dot (-XForm4.mYAxis).EQ (1) && endNormal.Dot (-XForm4.mYAxis).EQ (1)))
         return false;
      else if (stNormal.Dot (-XForm4.mZAxis).EQ (1)
               || endNormal.Dot (-XForm4.mZAxis).EQ (1))
         throw new Exception ("Negative Z axis normal encountered");

      return true;
   }

   /// <summary>
   /// This is a utility method that computes the distance between the the segments, including the 
   /// start and end segment.
   /// </summary>
   /// <param name="segs">The list of tooling segments</param>
   /// <param name="stIndex">The start index of the tooling segment</param>
   /// <param name="endIndex">The end index of the tooling segment</param>
   /// <returns>The length of all the curves from start segment to end segment, including the start 
   /// and the end segment</returns>
   public static double GetDistanceBetween (List<ToolingSegment> segs, int stIndex, int endIndex) {
      double length = 0.0;
      for (int ii = stIndex; ii < endIndex; ii++)
         length += segs[ii].Curve.Length;
      return length;
   }

   /// <summary>
   /// This method is used to find that segment on which a given tooling length occurs. 
   /// </summary>
   /// <param name="segments">The input tooling segments</param>
   /// <param name="toolingLength">The tooling length from start of the segment</param>
   /// <returns>A tuple of the index of the segment on which the input tooling length happens and the
   /// incremental length of the index-th segment from its own start</returns>
   public static Tuple<int, double> GetSegmentLengthAndIndexForToolingLength (List<ToolingSegment> segments, double toolingLength) {
      double segmentLengthAtNotch = 0;
      int jj = 0;
      while (segmentLengthAtNotch < toolingLength) {
         segmentLengthAtNotch += segments[jj].Curve.Length;
         jj++;
      }

      var lengthInLastSegment = toolingLength;
      int occuranceIndex = jj - 1;
      double previousCurveLengths = 0.0;
      for (int kk = occuranceIndex - 1; kk >= 0; kk--)
         previousCurveLengths += segments[kk].Curve.Length;

      lengthInLastSegment -= previousCurveLengths;
      return new Tuple<int, double> (occuranceIndex, lengthInLastSegment);
   }

   /// <summary>
   /// This method is used to find the length of the tooling segments from start index to end index 
   /// of the list of tooling segments, INCLUDING the start and the end segment
   /// </summary>
   /// <param name="segments">The input tooling segments</param>
   /// <param name="fromIdx">The index of the start segment</param>
   /// <param name="toIdx">The index of the end segment</param>
   /// <returns>The length of the tooling segments from start to end index including the start and 
   /// end the segments</returns>
   public static double GetLengthBetweenTooling (List<ToolingSegment> segments, int fromIdx, int toIdx) {
      if (fromIdx > toIdx)
         (fromIdx, toIdx) = (toIdx, fromIdx);

      if (fromIdx == toIdx)
         return segments[fromIdx].Curve.Length;

      double lengthBetween = segments.Skip (fromIdx + 1)
                                     .Take (toIdx - fromIdx + 1)
                                     .Sum (segment => segment.Curve.Length);

      return lengthBetween;
   }

   /// <summary>
   /// This method is used to find the length of the tooling segments from start point and the 
   /// end point on list of tooling segments. 
   /// </summary>
   /// <param name="segments">The input tooling segments</param>
   /// <param name="fromIdx">The from point on one of the segments</param>
   /// <param name="toIdx">The to point on one of the segments</param>
   /// <returns>The length of the tooling segments from start to end points 
   /// which is the sum of the start point to end of the start segment, 
   /// the lengths of all the tooling segments inbetween the start and end segments
   /// and the length of the last segment from start point of that segment To the given
   /// To Point</returns>
   public static double GetLengthBetweenTooling (List<ToolingSegment> segments, Point3 fromPt, Point3 toPt) {
      var fromPtOnSegment = segments.Select ((segment, index) => new { Segment = segment, Index = index })
                                          .FirstOrDefault (x => Geom.IsPointOnCurve (x.Segment.Curve, fromPt,
                                                                                     x.Segment.Vec0.Normalized ()));

      bool fromPtOnSegmentExists = fromPtOnSegment != null;
      if (!fromPtOnSegmentExists)
         throw new Exception ("GetLengthBetweenTooling: From pt is not on segment");


      var toPtOnSegment = segments.Select ((segment, index) => new { Segment = segment, Index = index })
                                       .FirstOrDefault (x => Geom.IsPointOnCurve (x.Segment.Curve, toPt,
                                                                                    x.Segment.Vec0.Normalized ()));

      bool toPtOnSegmentExists = toPtOnSegment != null;
      if (!toPtOnSegmentExists)
         throw new Exception ("GetLengthBetweenTooling: To pt is not on segment");

      // Swap the objects if from index is > to Index
      if (fromPtOnSegment.Index > toPtOnSegment.Index) {
         (fromPtOnSegment, toPtOnSegment) = (toPtOnSegment, fromPtOnSegment);
         (fromPt, toPt) = (toPt, fromPt);
      }

      double fromPtSegLength = Geom.GetLengthBetween (fromPtOnSegment.Segment.Curve, fromPt, fromPtOnSegment.Segment.Curve.End,
                                                      fromPtOnSegment.Segment.Vec0.Normalized ());
      double toPtSegLength = Geom.GetLengthBetween (toPtOnSegment.Segment.Curve, toPt, toPtOnSegment.Segment.Curve.End,
                                                    toPtOnSegment.Segment.Vec0.Normalized ());
      double intermediateLength = 0;
      if (fromPtOnSegment.Index != toPtOnSegment.Index && fromPtOnSegment.Index + 1 < segments.Count
          && toPtOnSegment.Index - 1 >= 0)
         intermediateLength = GetLengthBetweenTooling (segments, fromPtOnSegment.Index + 1,
                                                       toPtOnSegment.Index - 1);

      double length = intermediateLength + (fromPtSegLength + toPtSegLength);
      return length;
   }

   /// <summary>
   /// This method is used to find the length of the segments between the given point
   /// occuring on a tooling segment AND the lengths of all segments upto the last segment
   /// </summary>
   /// <param name="segments">The input segments list</param>
   /// <param name="pt">The given point</param>
   /// <returns>the length of the segments between the given point occuring on a tooling segment 
   /// AND the lengths of all segments upto the last segment</returns>
   /// <exception cref="Exception">An exception is thrown if the given point is not on any of the 
   /// input list of tooling segments</exception>
   public static double GetLengthFromEndToolingToPosition (List<ToolingSegment> segments, Point3 pt) {
      var result = segments.Select ((segment, index) => new { Segment = segment, Index = index })
                           .FirstOrDefault (x => Geom.IsPointOnCurve (x.Segment.Curve, pt,
                                                                      x.Segment.Vec0.Normalized ()));

      bool ptOnSegment = result != null;
      int idx = ptOnSegment ? result.Index : -1;
      if (!ptOnSegment)
         throw new Exception ("GetLengthFromEndToolingToPosition: Given pt is not on any segment");

      double length = segments.Skip (idx + 1).Sum (segment => segment.Curve.Length);
      double idxthSegLengthFromPt = Geom.GetLengthBetween (segments[idx].Curve, pt,
                                                           segments[idx].Curve.End,
                                                           segments[idx].Vec0.Normalized ());
      length += idxthSegLengthFromPt;
      return length;
   }

   /// <summary>
   /// This method computes the bounds in 3D of a set of points
   /// </summary>
   /// <param name="points">The input set of 3d points</param>
   /// <returns>The bounds in 3d</returns>
   public static Bound3 GetPointsBounds (List<Point3> points) {
      // Calculate max and min values for X, Y, Z
      var (maxX, minX, maxY, minY, maxZ, minZ) = (
          points.Max (p => p.X), points.Min (p => p.X),
          points.Max (p => p.Y), points.Min (p => p.Y),
          points.Max (p => p.Z), points.Min (p => p.Z)
      );

      Bound3 bounds = new (minX, minY, minZ, maxX, maxY, maxZ);
      return bounds;
   }

   /// <summary>
   /// This method returns the bounds 3d of a list of tooling segments from
   /// the starting index to the end index. If startIndex is -1, all the items 
   /// in the list are considered
   /// </summary>
   /// <param name="toolingSegs">The list of tooling segments</param>
   /// <param name="startIndex">The start index </param>
   /// <param name="endIndex">The end index</param>
   /// <returns></returns>
   public static Bound3 GetToolingSegmentsBounds (List<ToolingSegment> toolingSegs, Bound3 extentBox,
                                                  int startIndex = -1, int endIndex = -1) {
      List<ToolingSegment> toolingSegsSub = [];
      if (startIndex != -1) {
         int increment = startIndex <= endIndex ? 1 : -1;
         for (int ii = startIndex; (startIndex <= endIndex ? ii <= endIndex : ii >= endIndex);
            ii += increment)

            toolingSegsSub.Add (toolingSegs[ii]);
      } else
         toolingSegsSub = toolingSegs;

      // Extract all Point3 instances from Start and End properties of Curve3
      //var points = toolingSegsSub.SelectMany (seg => new[] { seg.Curve.Start, seg.Curve.End });
      return Utils.CalculateBound3 (toolingSegsSub, extentBox);
      //return GetPointsBounds (points.ToList ());
   }

   /// <summary>
   /// This method is used to find the length of the segments between the given point
   /// occuring on a tooling segment AND the lengths of all segments upto the first segment
   /// </summary>
   /// <param name="segments">The input segments list</param>
   /// <param name="pt">The given point</param>
   /// <returns>the length of the segments between the given point occuring on a tooling segment 
   /// AND the lengths of all segments upto the first segment</returns>
   /// <exception cref="Exception">An exception is thrown if the given point is not on any of the 
   /// input list of tooling segments</exception>
   public static double GetLengthFromStartToolingToPosition (List<ToolingSegment> segments, Point3 pt) {
      double length = 0.0;
      var result = segments.Select ((segment, index) => new { Segment = segment, Index = index })
                                 .FirstOrDefault (x => Geom.IsPointOnCurve (x.Segment.Curve, pt, x.Segment.Vec0.Normalized ()));

      bool ptOnSegment = result != null;
      int idx = ptOnSegment ? result.Index : -1;

      if (!ptOnSegment)
         throw new Exception ("GetLengthFromStartToolingToPosition: Given pt is not on any segment");

      length = segments.Take (idx - 1).Sum (segment => segment.Curve.Length);
      length += Geom.GetLengthBetween (segments[idx].Curve, segments[idx].Curve.Start, pt,
                                       segments[idx].Vec0.Normalized ());
      return length;
   }

   /// <summary>
   /// This method calculates the non-associated (ordinate) bounding box of the set of 
   /// tooling segments
   /// </summary>
   /// <param name="toolingSegments">The input tooling segments</param>
   /// <param name="partBBox">The overall bounding box containing the tooling segments. This 
   /// is used for clamping the limits</param>
   /// <returns>An non-associated bounding box  type Bound3
   /// </returns>
   public static Bound3 CalculateBound3 (List<ToolingSegment> toolingSegments, Bound3 partBBox) {
      if (toolingSegments == null || toolingSegments.Count == 0)
         throw new ArgumentException ("Tooling segments list cannot be null or empty.");

      Bound3? cumBBox = null;
      foreach (var seg in toolingSegments) {
         Bound3 bbox = Geom.ComputeBBox (seg.Curve, seg.Vec0, partBBox);
         //if (cumBBox == null) cumBBox = bbox;
         //else cumBBox = cumBBox + bbox;
         cumBBox = cumBBox == null ? bbox : cumBBox + bbox;
      }

      return cumBBox.Value;
   }

   /// <summary>
   /// Calculates the bounding box of the list of toolings.
   /// </summary>
   /// <param name="cuts">List of toolings</param>
   /// <returns></returns>
   public static Bound3 CalculateBound3 (List<Tooling> cuts) {
      var bounds = cuts.SelectMany (cut => new[] { cut.Bound3 });
      return new Bound3 (bounds.Min (b => b.XMin),
                         bounds.Min (b => b.YMin),
                         bounds.Min (b => b.ZMin),
                         bounds.Max (b => b.XMax),
                         bounds.Max (b => b.YMax),
                         bounds.Max (b => b.ZMax));
   }

   /// <summary>
   /// This method is used to compute 3d point intersecting the 
   /// segment in list of segments, whose X value alone is specified. 
   /// </summary>
   /// <param name="segs">The input tooling segments</param>
   /// <param name="xVal">The X Value at which the intersection parameters are to be 
   /// calculated</param>
   /// <returns>A tuple of 3d Point, parameter with in the index-th segment,
   /// index of the segment and flag true or false, if the intersection happens 
   /// between 0 - 1 parameter</returns>
   public static Tuple<Point3, double, int, bool> GetPointParamsAtXVal (List<ToolingSegment> segs, double xVal) {
      double t = -1.0;
      Point3 p = new ();
      int kk;
      bool ixn = false;
      for (kk = 0; kk < segs.Count; kk++) {
         if ((segs[kk].Curve.Start.X - xVal).EQ (0))
            return new Tuple<Point3, double, int, bool> (segs[kk].Curve.Start, 0, kk, true);

         if ((segs[kk].Curve.End.X - xVal).EQ (0))
            return new Tuple<Point3, double, int, bool> (segs[kk].Curve.End, 1, kk, true);

         if (segs[kk].Curve is Arc3 arc) {
            var (c, r) = Geom.EvaluateCenterAndRadius (arc);
            var p1 = new Point3 (xVal, segs[kk].Curve.Start.Y,
                                 c.Z + Math.Sqrt (r * r - (xVal - c.X) * (xVal - c.X)));
            var p2 = new Point3 (xVal, segs[kk].Curve.Start.Y,
                                 c.Z - Math.Sqrt (r * r - (xVal - c.X) * (xVal - c.X)));
            // Find out which of the above points exists with in the segment

            if (Geom.IsPointOnCurve (segs[kk].Curve, p1, segs[kk].Vec0))
               p = p1;
            else if (Geom.IsPointOnCurve (segs[kk].Curve, p2, segs[kk].Vec0))
               p = p2;
            else
               continue;

            t = Geom.GetParamAtPoint (arc, p, segs[kk].Vec0);
         } else {
            var x1 = segs[kk].Curve.Start.X; var x2 = segs[kk].Curve.End.X;
            var z1 = segs[kk].Curve.Start.Z; var z2 = segs[kk].Curve.End.Z;
            t = (xVal - x1) / (x2 - x1);
            var z = z1 + t * (z2 - z1);
            p = new Point3 (xVal, segs[kk].Curve.Start.Y, z);
         }

         if (t.LieWithin (0, 1)) {
            ixn = true;
            break;
         }
      }

      if (kk == segs.Count)
         return new Tuple<Point3, double, int, bool> (new Point3 (double.MinValue, double.MinValue, double.MinValue), -1, -1, ixn);

      return new Tuple<Point3, double, int, bool> (p, t, kk, ixn);
   }

   /// <summary>
   /// This method is to split a tooling scope. Please note that the tooling scope is
   /// a wrapper over tooling. The tooling has a list of tooling segments. The split will happen
   /// based on the X values stored in the tooling scope object.
   /// </summary>
   /// <param name="ts">The input tooling scope.</param>
   /// <param name="isLeftToRight">A provision (flag) if the part is machined in forward or
   /// reverse to the legacy direction</param>
   /// <returns>List of tooling segments, split.</returns>
   /// <exception cref="Exception">This exception is thrown if the tooling does not intersect
   /// between the X values stored in the tooling scope.</exception>
   public static List<ToolingSegment> SplitNotchToScope (ToolingScope ts, bool isLeftToRight) {
      var segs = ts.Tooling.Segs; var toolingItem = ts.Tooling;
      List<ToolingSegment> resSegs = [];
      if (segs[^1].Curve.End.X < segs[0].Curve.Start.X && (ts.Tooling.ProfileKind == ECutKind.YPos || ts.Tooling.ProfileKind == ECutKind.YNeg))
         throw new Exception ("The notch direction in X is opposite to the direction of the part");

      var startX = ts.StartX; var endX = ts.EndX;
      double xPartition;
      bool maxSideToPartition = false;
      if (isLeftToRight) {
         if ((toolingItem.Bound3.XMax - ts.EndX).EQ (0)) {
            xPartition = startX;
            maxSideToPartition = true;
         } else if ((toolingItem.Bound3.XMin - ts.StartX).EQ (0))
            xPartition = endX;
         else
            throw new Exception ("ToolingScope does not match with Tooling: In left to right");
      } else {
         if ((toolingItem.Bound3.XMax - ts.StartX).EQ (0)) {
            xPartition = endX;
            maxSideToPartition = true;
         } else if ((toolingItem.Bound3.XMin - ts.EndX).EQ (0))
            xPartition = startX;
         else
            throw new Exception ("ToolingScope does not match with Tooling: In Right to left");
      }

      var (notchXPt, paramAtIxn, index, doesIntersect) = GetPointParamsAtXVal (segs, xPartition);
      List<ToolingSegment> splitSegs; Point3 lineEndPoint; Line3 line;
      if (doesIntersect) {
         splitSegs = SplitToolingSegmentsAtPoint (segs, index, notchXPt, segs[index].Vec0.Normalized ());
         lineEndPoint = new Point3 (notchXPt.X, notchXPt.Y, segs[0].Curve.Start.Z);

         // Create a new line tooling segment.
         //if (splitSegs.Count > 0 && lineEndPoint != null) {
         if (maxSideToPartition) {
            // Take all toolingSegments from Last toolingSegmen to index-1, add the 0th index of splitSegs, add it to the lastTSG.
            line = new Line3 (lineEndPoint, notchXPt);
            var lastTSG = Geom.CreateToolingSegmentForCurve (line as Curve3, segs[index].Vec0.Normalized (), segs[index].Vec0.Normalized ());
            resSegs.Add (lastTSG);
            resSegs.Add (splitSegs[1]);
            for (int ii = index + 1; ii < segs.Count; ii++)
               resSegs.Add (segs[ii]);
         } else {
            line = new Line3 (notchXPt, lineEndPoint);
            var lastTSG = Geom.CreateToolingSegmentForCurve (line as Curve3, segs[index].Vec0.Normalized (), segs[index].Vec0.Normalized ());
            for (int ii = 0; ii < index; ii++)
               resSegs.Add (segs[ii]);

            resSegs.Add (splitSegs[0]);
            resSegs.Add (lastTSG);
         }
      }

      return resSegs;
   }

   /// <summary>
   /// This method is to check, at any instance, if the notch points info data structure
   /// is valid with regards to the input tooling segments.
   /// <remarks> Notch Point Info structure stores, the index of the element in the tooling segments list,
   /// and an array of points. This serves as follows.
   /// If the notch occurances are not created at 25/50/75 percent of the lengths, the method that computes
   /// them, needs a data structure that gives index (of the segment in segs) and the list of points that
   /// occur in that index.
   /// Once the occurances are computed and the segments are split at those points, to make the segment's end point
   /// to be the point of interest, the notch pointS info, the list contains, each element, which has an unique
   /// index, ONLY ONE POINT (instead of an array) which the end point of that index-th segment.
   /// This end point coordinates are verified to be identical with the index-th points in the segments
   /// This filters any error while preparing the segments for notch
   /// </remarks>
   /// </summary>
   /// <param name="segs">The input list of segments</param>
   /// <param name="npsInfo">The data structure that holds the notch points specs</param>
   /// <exception cref="Exception">Exception is thrown if an error is found</exception>
   public static void CheckSanityNotchPointsInfo (List<ToolingSegment> segs,
                                                  List<NotchPointInfo> npsInfo) {
      for (int ii = 0; ii < npsInfo.Count; ii++) {
         if (npsInfo[ii].mSegIndex == -1) continue;
         var npInfoPt = npsInfo[ii].mPoints[0];
         var segEndPt = segs[npsInfo[ii].mSegIndex].Curve.End;
         if (!npInfoPt.DistTo (segEndPt).EQ (0))
            throw new Exception ("NOtchpoint and segment's point do not match");
      }
   }

   /// <summary>
   /// This method is used to find if a segment in the segments list is CONCAVE,
   /// and so the segment can not be used for the notch spec point creation
   /// <remarks> The algorithm checks for any two points on the segment (after discretizing) if the
   /// sign of the Y unless Z, unless X values difference is the same as the difference between
   /// (in the same order) end to start of the segment. If this is violated, then the outward vector
   /// to the nearest boundary from a point in the invalid segment will intersect one of the other 
   /// tooling segments before reaching the boundary thus making the notch speific approach or reentry
   /// completely wrong</remarks>
   /// </summary>
   /// <param name="segments">The input list of segments</param>
   /// <exception cref="Exception">If the sign can not evaluated</exception>
   public static void MarkfeasibleSegments (ref List<ToolingSegment> segments) {
      int sgn;
      string about = "";
      sgn = Math.Sign (segments.Last ().Curve.End.Y - segments.First ().Curve.Start.Y);
      if (sgn != 0)
         about = "Y";

      if (sgn == 0) {
         sgn = Math.Sign (segments.Last ().Curve.End.Z - segments.First ().Curve.Start.Z);
         about = "Z";
      }

      if (sgn == 0) {
         sgn = Math.Sign (segments.Last ().Curve.End.X - segments.First ().Curve.Start.X);
         about = "X";
      }

      if (sgn == 0)
         throw new Exception ("Sign of the tooling segment (end-start) computation ambiguous");

      for (int ii = 0; ii < segments.Count; ii++) {
         if (segments[ii].Curve is Line3) {
            double diff = -1;
            if (about == "Y")
               diff = segments[ii].Curve.End.Y - segments[ii].Curve.Start.Y;
            else if (about == "Z")
               diff = segments[ii].Curve.End.Z - segments[ii].Curve.Start.Z;
            else if (about == "X")
               diff = segments[ii].Curve.End.X - segments[ii].Curve.Start.X;

            if (diff.EQ (0))
               continue;
            var segSign = Math.Sign (diff);
            if (segSign != 0 && segSign != sgn) {
               var seg = segments[ii];
               seg.IsValid = false;
               segments[ii] = seg;
            }
         } else {
            var arcPts = (segments[ii].Curve as Arc3).Discretize (0.1).ToList ();
            bool broken = false;
            for (int jj = 1; jj < arcPts.Count; jj++) {
               var diff = arcPts[jj].Y - arcPts[jj - 1].Y;
               if (diff.EQ (0))
                  continue;

               var segSign = Math.Sign (diff);
               if (segSign != 0 && segSign != sgn) {
                  var seg = segments[ii];
                  seg.IsValid = false;
                  segments[ii] = seg;
                  broken = true;
                  break;
               }
            }

            if (broken)
               continue;
         }
      }
   }

   /// <summary>
   /// This method finds the index of each notch spec point in the input notch points list
   /// in the input tooling segments.
   /// </summary>
   /// <param name="segs">The input tooling segments</param>
   /// <param name="notchPointsInfo">The input notch points info, also used to mark the indices</param>
   public static void ReIndexNotchPointsInfo (List<ToolingSegment> segs, ref List<NotchPointInfo> notchPointsInfo) {
      // Update the ordinate notch points ( 25,50, and 75)
      string[] atPos = ["@25", "@50", "@75"];
      int posCnt = 0;
      for (int ii = 0; ii < notchPointsInfo.Count; ii++) {
         var npinfo = notchPointsInfo[ii];
         if (npinfo.mSegIndex != -1) {
            int index = segs.FindIndex (s => s.Curve.End.DistTo (npinfo.mPoints[0]).EQ (0));
            npinfo.mSegIndex = index;
         }

         if (npinfo.mPosition == "@25" || npinfo.mPosition == "@50" || npinfo.mPosition == "@75")
            npinfo.mPosition = atPos[posCnt++];

         notchPointsInfo[ii] = npinfo;
      }

      notchPointsInfo = [.. notchPointsInfo.OrderBy (n => n.mPercentage)];
   }

   /// <summary>
   /// This method updates the notch points list for the given position, percentage and point
   /// </summary>
   /// <param name="segs">The input segments</param>
   /// <param name="notchPointsInfo">The input/output notch points info</param>
   /// <param name="position">a string token as symbol</param>
   /// <param name="percent">The parameter of the point in the tooling segments</param>
   /// <param name="pt">The actual point</param>
   /// <exception cref="Exception">If the given point is not participating in the tooling segments list</exception>
   public static void UpdateNotchPointsInfo (List<ToolingSegment> segs,
                                             ref List<NotchPointInfo> notchPointsInfo,
      string position, double percent, Point3 pt) {
      var npinfo = new NotchPointInfo () {
         mPercentage = percent,
         mPoints = [],
         mPosition = position
      };
      npinfo.mPoints.Add (pt);
      int index = segs.FindIndex (s => s.Curve.End.DistTo (pt).EQ (0));
      if (index == -1)
         throw new Exception ("Index = -1 for notch spec point in mSegments");

      npinfo.mSegIndex = index;
      notchPointsInfo.Add (npinfo);
      ReIndexNotchPointsInfo (segs, ref notchPointsInfo);
   }

   /// <summary>
   /// This method writes Rapid position G Code statement as [G0  X Y Z Angle]
   /// </summary>
   /// <param name="sw">The stream writer</param>
   /// <param name="x">X Coordinate</param>
   /// <param name="y">Y Coordinate</param>
   /// <param name="z">Z Coordinate</param>
   /// <param name="a">Angle in degrees</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void RapidPosition (StreamWriter sw, double x, double y, double z,
                                     double a, MachineType machine = MachineType.LCMMultipass2H,
                                     bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun) return;
      sw.WriteLine ("G0 X{0} Y{1} Z{2} A{3}", x.ToString ("F3"),
                                              y.ToString ("F3"),
                                              z.ToString ("F3"),
                                              a.ToString ("F3"));
   }

   /// <summary>
   /// This method writes Rapid position G Code statement as [G0  X Y/Z  Comment]
   /// Y/Z means either of one.
   /// </summary>
   /// <param name="sw">The streamwriter</param>
   /// <param name="x">X Coordinate</param>
   /// <param name="oaxis">Either Y oor Z ordinate axis</param>
   /// <param name="val">the coordinate value aling the above ordinate axis</param>
   /// <param name="comment">G Code comment</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void RapidPosition (StreamWriter sw, double x, OrdinateAxis oaxis,
                                     double val, string comment,
                                     MachineType machine = MachineType.LCMMultipass2H,
                                     bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun) return;
      if (oaxis == OrdinateAxis.Y)
         sw.WriteLine ("G0 X{0} Y{1} ({2})", x.ToString ("F3"),
                                             val.ToString ("F3"), comment);
      else if (oaxis == OrdinateAxis.Z)
         sw.WriteLine ("G0 X{0} Z{1} ({2})", x.ToString ("F3"),
                                             val.ToString ("F3"), comment);
   }

   /// <summary>
   /// This method writes the ready to machining G Code statement as [G1  X Y Z Angle Comment]
   /// Though this G1 machining statement, in the context, this is more of a ready to machining
   /// statement.
   /// </summary>
   /// <param name="sw">The stream writer</param>
   /// <param name="x">X Coordinate</param>
   /// <param name="y">Y coordinate</param>
   /// <param name="z">Z Coordinate</param>
   /// <param name="a">Angle aboit X axis in degrees</param>
   /// <param name="f">Feed rate. This differentiates this statement from machining statement</param>
   /// <param name="comment">G Code comment</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void LinearMachining (StreamWriter sw, double x, double y, double z, double a, double f, string comment = "",
      MachineType machine = MachineType.LCMMultipass2H, bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun)
         return;

      sw.WriteLine ("G1 X{0} Y{1} Z{2} A{3} F{4} ({5})", x.ToString ("F3"),
                                                         y.ToString ("F3"),
                                                         z.ToString ("F3"),
                                                         a.ToString ("F3"),
                                                         f, comment);
   }

   /// <summary>
   /// This method writes the linear machining statement as [G1  X Y Z A Comment ]
   /// </summary>
   /// <param name="sw">The stream writer</param>
   /// <param name="x">X Coordinate</param>
   /// <param name="y">Y Coordinate</param>
   /// <param name="z">Z Coordinate</param>
   /// <param name="a">Angle about X axis in degrees</param>
   /// <param name="comment">G Code comment</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void LinearMachining (StreamWriter sw, double x, double y, double z, double a, string comment = "",
      MachineType machine = MachineType.LCMMultipass2H, bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun)
         return;

      sw.WriteLine ("G1 X{0} Y{1} Z{2} A{3} {4}", x.ToString ("F3"),
                                                  y.ToString ("F3"),
                                                  z.ToString ("F3"),
                                                  a.ToString ("F3"),
                                                  comment);
   }

   /// <summary>
   /// This method writes the linear machining statement as [G1  X Y Z Comment ]
   /// </summary>
   /// <param name="sw">The stream writer</param>
   /// <param name="x">X Coordinate</param>
   /// <param name="y">Y Coordinate</param>
   /// <param name="z">Z Coordinate</param>
   /// <param name="comment">G Code comment</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void LinearMachining (StreamWriter sw, double x, double y, double z,
                                       string comment = "", MachineType machine = MachineType.LCMMultipass2H,
                                       bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun)
         return;

      sw.WriteLine ("G1 X{0} Y{1} Z{2} {3}", x.ToString ("F3"),
                                             y.ToString ("F3"),
                                             z.ToString ("F3"),
                                             comment);
   }

   /// <summary>
   /// This method writes the linear machining statement as [G1  X  Y/Z A Comment ]
   /// Y/Z means either Y or Z.
   /// </summary>
   /// <param name="sw">The streamwriter</param>
   /// <param name="x">X Coordinate</param>
   /// <param name="oaxis">Either Y oor Z ordinate axis</param>
   /// <param name="val">the coordinate value aling the above ordinate axis</param>
   /// <param name="a">Angle about X axis in degrees</param>
   /// <param name="comment">G Code comment</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void LinearMachining (StreamWriter sw, double x, OrdinateAxis oaxis,
                                       double val, double a, string comment = "",
                                       MachineType machine = MachineType.LCMMultipass2H,
                                       bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun)
         return;

      if (oaxis == OrdinateAxis.Y)
         sw.WriteLine ("G1 X{0} Y{1} A{2} {3}", x.ToString ("F3"), val.ToString ("F3"),
                                                a.ToString ("F3"), comment);
      else if (oaxis == OrdinateAxis.Z)
         sw.WriteLine ("G1 X{0} Z{1} A{2} {3}", x.ToString ("F3"), val.ToString ("F3"),
                                                a.ToString ("F3"), comment);
   }

   /// <summary>
   /// This method writes the linear machining statement as [G1  X  Y/Z Comment ]
   /// Y/Z means either Y or Z.
   /// </summary>
   /// <param name="sw">The streamwriter</param>
   /// <param name="x">X Coordinate</param>
   /// <param name="oaxis">Either Y oor Z ordinate axis</param>
   /// <param name="val">the coordinate value aling the above ordinate axis</param>
   /// <param name="comment">G Code comment</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void LinearMachining (StreamWriter sw, double x, OrdinateAxis oaxis,
                                       double val, string comment = "",
      MachineType machine = MachineType.LCMMultipass2H, bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun)
         return;

      if (oaxis == OrdinateAxis.Y)
         sw.WriteLine ("G1 X{0} Y{1} {2}", x.ToString ("F3"),
                                           val.ToString ("F3"), comment);
      else if (oaxis == OrdinateAxis.Z)
         sw.WriteLine ("G1 X{0} Z{1} {2}", x.ToString ("F3"),
                                           val.ToString ("F3"), comment);
   }

   /// <summary>
   /// This method is used to write circular machining statement as G{2}{3} I val J val
   /// where {2}{3} means either 2 ( clockwise) or 3 (counter-clockwise)
   /// </summary>
   /// <param name="sw">The stream writer</param>
   /// <param name="arcSense">clockwise or counter clockwise</param>
   /// <param name="i">The X-axis offset from the start point of the arc to the center of the arc.</param>
   /// <param name="oaxis">The ordinate axis, Y, which means val is J and if ordinate axis is Z, val is K</param>
   /// <param name="val">The Y/Z-axis offset from the start point of the arc to the center of the arc.</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void CircularMachining (StreamWriter sw, Utils.EArcSense arcSense,
                                         double i, OrdinateAxis oaxis, double val,
                                         EFlange flange, MachineType machine = MachineType.LCMMultipass2H,
                                         bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun)
         return;

      // LCMMultipass2H machine's G2 and G3 functions are reversed in sense for BOTTOM flange. So
      // CW is G3 and counter-clockwise is G2
      if (oaxis == OrdinateAxis.Y) {
         if (machine == MachineType.LCMMultipass2H && flange == EFlange.Bottom)
            sw.Write ("G{0} I{1} J{2} ( Circle )", arcSense == Utils.EArcSense.CW ? 3 : 2, i.ToString ("F3"),
                                                   val.ToString ("F3"));
         else
            sw.Write ("G{0} I{1} J{2} ( Circle )", arcSense == Utils.EArcSense.CW ? 2 : 3, i.ToString ("F3"),
                                                   val.ToString ("F3"));
      } else if (oaxis == OrdinateAxis.Z) {
         if (machine == MachineType.LCMMultipass2H && flange == EFlange.Bottom)
            sw.Write ("G{0} I{1} K{2} ( Circle )", arcSense == Utils.EArcSense.CW ? 3 : 2, i.ToString ("F3"),
                                                   val.ToString ("F3"));
         else
            sw.Write ("G{0} I{1} K{2} ( Circle )", arcSense == Utils.EArcSense.CW ? 2 : 3, i.ToString ("F3"),
                                                   val.ToString ("F3"));
      }

      sw.WriteLine ();
   }

   /// <summary>
   /// This method is used to write partially-circular (Arc) machining statement as 
   /// G{2}{3} I val J val X Y where {2}{3} means either 2 ( clockwise) or 3 (counter-clockwise)
   /// </summary>
   /// <param name="sw">The stream writer</param>
   /// <param name="arcSense">clockwise or counter clockwise</param>
   /// <param name="i">The X-axis offset from the start point of the arc to the center of the arc.</param>
   /// <param name="oaxis">The ordinate axis, Y, which means val is J and if ordinate axis is Z, val is K</param>
   /// <param name="val">The Y/Z-axis offset from the start point of the arc to the center of the arc.</param>
   /// <param name="x">This represents the end point of the arc on the X </param>
   /// <param name="y">This represents the end point of the arc on the Y/Z axis</param>
   /// <param name="machine">Machine type</param>
   /// <param name="slaveRun">The flag specifies if the head is a slave. In the case of slave for 
   /// machine type LCMMultipass2H, no g code statement is written</param>
   public static void ArcMachining (StreamWriter sw, Utils.EArcSense arcSense,
                                    double i, OrdinateAxis oaxis, double val,
                                    double x, double y, EFlange flange,
                                    MachineType machine = MachineType.LCMMultipass2H,
                                    bool slaveRun = false) {
      if (machine == MachineType.LCMMultipass2H && slaveRun)
         return;

      // LCMMultipass2H machine's G2 and G3 functions are reversed in sense for BOTTOM flange. So
      // CW is G3 and counter-clockwise is G2
      if (oaxis == OrdinateAxis.Y) {
         if (machine == MachineType.LCMMultipass2H && flange == EFlange.Bottom)
            sw.Write ("G{0} I{1} J{2}", arcSense == Utils.EArcSense.CW ? 3 : 2, i.ToString ("F3"),
                                        val.ToString ("F3"));
         else
            sw.Write ("G{0} I{1} J{2}", arcSense == Utils.EArcSense.CW ? 2 : 3, i.ToString ("F3"),
                                        val.ToString ("F3"));

         sw.Write (" X{0} Y{1}", x.ToString ("F3"), y.ToString ("F3"));
      } else if (oaxis == OrdinateAxis.Z) {
         if (machine == MachineType.LCMMultipass2H && flange == EFlange.Bottom)
            sw.Write ("G{0} I{1} K{2}", arcSense == Utils.EArcSense.CW ? 3 : 2, i.ToString ("F3"),
                                        val.ToString ("F3"));
         else
            sw.Write ("G{0} I{1} K{2}", arcSense == Utils.EArcSense.CW ? 2 : 3, i.ToString ("F3"),
                                        val.ToString ("F3"));

         sw.Write (" X{0} Z{1}", x.ToString ("F3"), y.ToString ("F3"));
      }

      sw.WriteLine ();
   }

   /// <summary>
   /// This method is the calling point for G Code Generation. Currently, there are two types of 
   /// machines, <c>Legacy</c> and <c>LCMMultipass2H</c>. The latter is the Laser Cutting Machine
   /// supporting multipass cuts. This method calls the G Code generation for the respective machines
   /// </summary>
   /// <param name="gcodeGen">G Code Generator</param>
   /// <param name="testing">Boolean flag if G Code is run for testing sanity</param>
   /// <returns>Returns the List of G Codes, for Head 1 and Head 2, generated for each cut scope. 
   /// For a single pass legacy, the wrapper list holds only one Cut Scope's G Codes for Head 1 and Head 2</returns>
   public static List<List<GCodeSeg>> ComputeGCode (GCodeGenerator gcodeGen, bool testing = false) {
      List<List<GCodeSeg>> traces = [[], []];

      // Check if the workpiece needs a multipass cutting
      if (gcodeGen.EnableMultipassCut && gcodeGen.Process.Workpiece.Model.Bound.XMax - gcodeGen.Process.Workpiece.Model.Bound.XMin >= gcodeGen.MaxFrameLength)
         gcodeGen.EnableMultipassCut = true;

      if (testing)
         gcodeGen.CreatePartition (gcodeGen.Process.Workpiece.Cuts, /*optimize*/false,
                                   gcodeGen.Process.Workpiece.Model.Bound);
      else {
         // Sanity test might have changed the instance of setting properties
         gcodeGen.SetFromMCSettings ();
         gcodeGen.ResetBookKeepers ();
      }
      if (gcodeGen.EnableMultipassCut && MultiPassCuts.IsMultipassCutTask (gcodeGen.Process.Workpiece.Model)) {
         var mpc = new MultiPassCuts (gcodeGen.Process.Workpiece.Cuts, gcodeGen, gcodeGen.Process.Workpiece.Model, SettingServices.It.LeftToRightMachining,
            gcodeGen.MaxFrameLength, gcodeGen.DeadbandWidth, gcodeGen.MaximizeFrameLengthInMultipass);
         mpc.ComputeQuasiOptimalCutScopes ();
         mpc.GenerateGCode ();
         traces[0] = mpc.CutScopeTraces[0][0];
         traces[1] = mpc.CutScopeTraces[0][1];
      } else {
         var prevVal = gcodeGen.EnableMultipassCut;
         gcodeGen.EnableMultipassCut = false;
         gcodeGen.CreatePartition (gcodeGen.Process.Workpiece.Cuts, gcodeGen.OptimizePartition, gcodeGen.Process.Workpiece.Model.Bound);
         gcodeGen.GenerateGCode (0);
         gcodeGen.GenerateGCode (1);
         traces[0] = gcodeGen.CutScopeTraces[0][0];
         traces[1] = gcodeGen.CutScopeTraces[0][1];
         gcodeGen.EnableMultipassCut = prevVal;
      }

      return traces;
   }
}