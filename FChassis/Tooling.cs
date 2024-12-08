using FChassis.GCodeGen;
using Flux.API;
using System.ComponentModel;
namespace FChassis;

public readonly struct PointVec {
   public PointVec (Point3 pt, Vector3 vec) => (Pt, Vec) = (pt, vec);
   public readonly Point3 Pt;
   public readonly Vector3 Vec;
   public readonly Point3 Lift (double offset) => Pt + Vec * offset;
   public readonly double DistTo (PointVec rhs) => Pt.DistTo (rhs.Pt);
}

public enum EKind { Hole, Notch, Mark, Cutout };
public enum ECutKind {
   Top2YNeg, YNegToYPos, Top,
   YPos, YNeg, Top2YPos, YPosFlex, YNegFlex, None
};
public struct ToolingSegment {
   Curve3 mCurve;
   Vector3 mVec0;
   Vector3 mVec1;
   bool mIsValid = true;
   public ToolingSegment ((Curve3, Vector3, Vector3) vtSeg) {
      mCurve = vtSeg.Item1;
      mVec0 = vtSeg.Item2;
      mVec1 = vtSeg.Item3;
   }
   public ToolingSegment (Curve3 crv, Vector3 vec0, Vector3 vec1) {
      mCurve = crv;
      mVec0 = vec0;
      mVec1 = vec1;
   }
   public ToolingSegment (ToolingSegment rhs) {
      mCurve = rhs.mCurve;
      mVec0 = rhs.mVec0;
      mVec1 = rhs.mVec1;
   }
   public readonly void Deconstruct (out Curve3 curve, out Vector3 vec0, out Vector3 vec1) {
      curve = this.Curve;
      vec0 = this.Vec0;
      vec1 = this.Vec1;
   }
   public Curve3 Curve { readonly get => mCurve; set => mCurve = value; }
   public Vector3 Vec0 { readonly get => mVec0; set => mVec0 = value; }
   public Vector3 Vec1 { readonly get => mVec1; set => mVec1 = value; }
   public bool IsValid { readonly get => mIsValid; set => mIsValid = value; }
}
public class Tooling {
   public Tooling (Workpiece wp, E3Thick ent,
                   Pline shape, EKind kind) {
      Traces.Add ((ent, shape));
      Kind = kind; Work = wp;
   }

   public Tooling (Workpiece wp, EKind kind) {
      Kind = kind; Work = wp;
   }

   public Tooling Clone () {
      // Create a new Tooling object
      var clonedTooling = new Tooling (this.Work, this.Kind) {
         SeqNo = this.SeqNo,
         NotchKind = this.NotchKind,
         ProfileKind = this.ProfileKind,
         CutoutKind = this.CutoutKind,
         mPerimeter = this.mPerimeter,
         mSegs = this.mSegs.Select (seg => new ToolingSegment (Geom.CloneCurve (seg.Curve, seg.Vec0), seg.Vec0, seg.Vec1)).ToList (),
         mBound3 = this.mBound3,
         mName = this.mName,
         IsSingleHead1 = this.IsSingleHead1,
         IsSingleHead2 = this.IsSingleHead2,
         mHead = this.mHead,
         PostRoute = this.PostRoute.Select (pv => new PointVec (pv.Pt, pv.Vec)).ToList (),
         ShouldConsiderReverseRef = this.ShouldConsiderReverseRef
      };

      // Clone the Traces list
      clonedTooling.Traces.AddRange (this.Traces.Select (trace => (trace.Ent, trace.Trace.Clone ())));
      return clonedTooling;
   }

   public const double mJoinableLengthToClose = 1.0;
   public const double mNotchJoinableLengthToClose = 5.0;
   public readonly Workpiece Work;
   public readonly List<(E3Thick Ent, Pline Trace)> Traces = [];
   public int SeqNo = -1;
   public EKind Kind;
   public ECutKind NotchKind = ECutKind.None;
   public ECutKind CutoutKind = ECutKind.None;
   public ECutKind ProfileKind = ECutKind.None;
   double mPerimeter = -1.0;
   List<ToolingSegment> mSegs = [];

   public List<ToolingSegment> Segs {
      get {
         if (mSegs.Count == 0)
            mSegs = ExtractSegs.ToList ();
         return mSegs;
      }

      set { mSegs = value; }
   }

   Bound3? mBound3 = null;
   public double XMin { get => mBound3.Value.XMin; }
   public double XMax { get => mBound3.Value.XMax; }
   public Bound3 Bound3 {
      get => mBound3.Value;
      set => mBound3 = value;
   }
   string mName;
   public string Name {
      get => mName;
      set => mName = value;
   }
   public string FeatType { get; set; }
   public bool IsSingleHead1 { get; set; }
   public bool IsSingleHead2 { get; set; }
   int mHead = -1;
   public int Head {
      get => mHead;
      set => mHead = value;
   }

   public PointVec Start
      => Project (Traces[0].Ent, Traces[0].Trace.P1);

   public PointVec End
      => Project (Traces[^1].Ent, Traces[^1].Trace.P2);

   public List<PointVec> PostRoute = [];

   public bool ShouldConsiderReverseRef { get; set; }
   void OffsetStartingTraceToE3PlaneRef () {
      int i = Traces.FindIndex (item => item.Ent is E3Plane);
      if (i > 0)
         Traces.Skip (i).Concat (Traces.Take (i)).ToList ();
   }

   public Tooling JoinTo (Tooling b, double minJoinDist = mJoinableLengthToClose) {
      var t = new Tooling (Work, Kind);
      if (End.Pt.DistTo (b.Start.Pt) < minJoinDist) {
         t.Traces.AddRange (Traces);
         t.Traces.AddRange (b.Traces);
      } else if (Start.Pt.DistTo (b.End.Pt) < minJoinDist) {
         t.Traces.AddRange (b.Traces);
         t.Traces.AddRange (Traces);
      } else if (Start.Pt.DistTo (b.Start.Pt) < minJoinDist
                 && !(End.Pt.DistTo (b.End.Pt) < minJoinDist)) {
         b.Reverse ();
         t.Traces.AddRange (b.Traces); t.Traces.AddRange (Traces);
      } else if (End.Pt.DistTo (b.End.Pt) < minJoinDist
                 && !(Start.Pt.DistTo (b.Start.Pt) < minJoinDist)) {
         b.Reverse ();
         t.Traces.AddRange (Traces); t.Traces.AddRange (b.Traces);
      } else
         t = null;

      return t;
   }

   public bool IsClosed (double tol) => End.Pt.DistTo (Start.Pt) <= tol;

   public void IdentifyCutout () {
      // If closed tooling, it is marked as CUTOUT
      if (IsClosed (mJoinableLengthToClose)) {
         Kind = EKind.Cutout;
         OffsetStartingTraceToE3PlaneRef ();
      }
   }

   public void DrawWaypoints (Color32 color, double height) {
      Lux.HLR = true;
      Lux.Color = new Color32 (96, color.R, color.G, color.B);
      for (int i = 1; i < PostRoute.Count; i++) {
         PointVec pv0 = PostRoute[i - 1], pv1 = PostRoute[i];
         Lux.Draw (EDraw.Quad, [pv0.Pt, pv1.Pt,
                                pv1.Pt + pv1.Vec * height,
                                pv0.Pt + pv0.Vec * height]);
      }

      Lux.Color = color;
      for (int i = 1; i < PostRoute.Count; i++) {
         PointVec pv0 = PostRoute[i - 1], pv1 = PostRoute[i];
         Lux.Draw (EDraw.Lines, [pv0.Pt, pv1.Pt]);
      }
   }

   public double Perimeter {
      get {
         if (mPerimeter < 0.0)
            mPerimeter = mPerimeter = Segs.Sum (seg => seg.Curve.Length);

         return mPerimeter;
      }
   }

   public void DrawSegs (Color32 color, double height) {
      bool first = true;
      Lux.HLR = true;
      List<(PointVec PV, bool Stencil)> pvs = [];
      foreach (var (Curve, Vec0, Vec1) in Segs) {
         if (first) {
            pvs.Add ((new (Curve.Start, Vec0), true));
            first = false;
         }

         if (Curve is Arc3 arc) {
            var pts = arc.Discretize (0.1).ToList ();
            for (int i = 1; i < pts.Count; i++)
               pvs.Add ((new (pts[i], Vec1), i == pts.Count - 1));
         } else
            pvs.Add ((new (Curve.End, Vec1), true));
      }

      Lux.Color = new Color32 (96, color.R, color.G, color.B);
      for (int i = 1; i < pvs.Count; i++) {
         PointVec pv0 = pvs[i - 1].PV, pv1 = pvs[i].PV;
         Lux.Draw (EDraw.Quad, [pv0.Pt, pv1.Pt, pv1.Pt + pv1.Vec * height, pv0.Pt + pv0.Vec * height]);
      }

      Lux.Color = color;
      for (int i = 1; i < pvs.Count; i++) {
         PointVec pv0 = pvs[i - 1].PV, pv1 = pvs[i].PV;
         Lux.Draw (EDraw.Lines, [pv0.Pt + pv0.Vec * height, pv1.Pt + pv1.Vec * height]);
      }

      foreach (var (pv, stencil) in pvs) {
         if (stencil)
            Lux.Draw (EDraw.Lines, [pv.Pt, pv.Pt + pv.Vec * height]);
      }
   }

   public void DrawSeqNo (double height) {
      if (SeqNo < 0)
         return;

      Lux.HLR = false;
      double length = Traces.Sum (a => a.Trace.Perimeter) / 2, start = 0;
      foreach (var (ent, trace) in Traces) {
         foreach (var seg in trace.Segs) {
            double end = start + seg.Length;
            if (end > length) {
               double lie = length.LieOn (start, end);
               var pv = Project (ent, seg.GetPointAt (lie));
               Point3 pt = pv.Pt + pv.Vec * height;
               Lux.Color = Color32.Black;
               Lux.DrawBillboardText ((SeqNo + 1).ToString (), pt, 16);
               return;
            }

            start = end;
         }
      }
   }

   public void DrawToolingName (double height) {
      Lux.HLR = false;
      double length = Traces.Sum (a => a.Trace.Perimeter) / 2, start = 0;
      var (ent, trace) = Traces[0];
      {
         var seg = trace.Segs.ToList ()[(int)((double)trace.Segs.ToList ().Count / 2.0)];
         {
            double end = start + seg.Length;
            double lie = length.LieOn (start, end);
            var pv = Project (ent, seg.GetPointAt (lie));
            Point3 pt = pv.Pt + pv.Vec * height;
            Lux.Color = Color32.Black;
            Lux.DrawBillboardText (Name.ToString (), pt, 16);
            return;
         }
      }
   }

   public static Point2 Unproject (Point3 pt, E3Plane ep) {
      var pt2 = pt * ep.Xfm.GetInverse ();
      return new (pt2.X, pt2.Y);
   }

   public static ECutKind GetCutKind (Tooling cut, XForm4 trf) {
      var segs = cut.Segs;
      ECutKind cutKindAtFlex = ECutKind.None, cutKindAtFlange = ECutKind.None;
      bool YNegPlaneFeat = segs.Any (cutSeg => ((trf * cutSeg.Vec0.Normalized ()).Y).EQ (-1));
      bool YPosPlaneFeat = segs.Any (cutSeg => ((trf * cutSeg.Vec0.Normalized ()).Y).EQ (1));
      bool TopPlaneFeat = segs.Any (cutSeg => ((trf * cutSeg.Vec0.Normalized ()).Z).EQ (1));
      foreach (var seg in segs) {
         var nn = (trf * seg.Vec0.Normalized ());
         if (nn.Y < -1e-6 && nn.Y.SGT (-1.0)) {
            cutKindAtFlex = ECutKind.YNegFlex;
            break;
         } else if (nn.Y > 1e-6 && nn.Y.SLT (1.0)) {
            cutKindAtFlex = ECutKind.YPosFlex;
            break;
         }
      }
      if (TopPlaneFeat && YPosPlaneFeat && YNegPlaneFeat)
         cutKindAtFlange = ECutKind.YNegToYPos;
      else if (TopPlaneFeat && (YNegPlaneFeat || cutKindAtFlex == ECutKind.YNegFlex))
         cutKindAtFlange = ECutKind.Top2YNeg;
      else if (TopPlaneFeat && (YNegPlaneFeat || cutKindAtFlex == ECutKind.YPosFlex))
         cutKindAtFlange = ECutKind.Top2YPos;
      else if (TopPlaneFeat)
         cutKindAtFlange = ECutKind.Top;
      else if (YNegPlaneFeat)
         cutKindAtFlange = ECutKind.YNeg;
      else if (YPosPlaneFeat)
         cutKindAtFlange = ECutKind.YPos;
      if (cutKindAtFlex == ECutKind.None && cutKindAtFlange == ECutKind.None)
         throw new Exception ("Unsupported Notch Type");
      return cutKindAtFlange != ECutKind.None ? cutKindAtFlange : cutKindAtFlex;
   }

   public IEnumerable<ToolingSegment> ExtractSegs {
      get {
         PointVec? prevpvb = null;
         foreach (var (ent, pline0) in Traces) {
            var pline = pline0;
            if (ent is E3Flex)
               pline = pline.DiscretizeP (0.1);
            var seggs = pline.Segs.ToList ();
            foreach (var seg in pline.Segs) {
               PointVec pva = Project (ent, seg.A),
                        pvb = Project (ent, seg.B);
               if (prevpvb != null) {
                  if (pva.DistTo (prevpvb.Value) < mNotchJoinableLengthToClose
                      && pva.DistTo (prevpvb.Value) > 1.0) {
                     var line = new Line3 (prevpvb.Value.Pt, pva.Pt);
                     yield return new (line, prevpvb.Value.Vec, pva.Vec);
                  }
               }

               if (seg.IsCurved) {
                  // If this is a curved segment in 2D, then it lies on a plane and we can simply convert
                  // it to an Arc3 (all the normals along this curve are pointing in the same direction)
                  PointVec pvm1 = Project (ent, seg.GetPointAt (0.5)),
                           pvm2 = Project (ent, seg.GetPointAt (0.75));
                  var arc = new Arc3 (pva.Pt, pvm1.Pt, pvm2.Pt, pvb.Pt);
                  prevpvb = pva; prevpvb = pvb;
                  yield return new (arc, pva.Vec, pvb.Vec);
               } else {
                  // If this is a line in 2D space, it might be lofted into an arc in 3D (if it lies
                  // on a flex). So use the difference between start and end normals to figure out how many
                  // segments to divide this into
                  double angDiff = pva.Vec.AngleTo (pvb.Vec),
                         angStep = 5.D2R ();
                  int steps = 1 + (int)(angDiff / angStep);
                  for (int i = 0; i < steps; i++) {
                     double start = i / (double)steps, end = (i + 1) / (double)steps;
                     PointVec ps = Project (ent, seg.GetPointAt (start)),
                              pe = Project (ent, seg.GetPointAt (end));
                     var line = new Line3 (ps.Pt, pe.Pt);
                     prevpvb = pva; prevpvb = pvb;
                     yield return new (line, ps.Vec, pe.Vec);
                  }
               }
            }
         }
      }
   }

   public void Reverse () {
      Traces.ForEach (trace => trace.Trace.Reverse ());
      Traces.Reverse ();
      // Note: Segs is the most recent recomputed segments 
      // list. So, Segs will not be overwritten from Tooling.
      if (Segs.Count == 0)
         Segs = ExtractSegs.ToList ();
      else
         Segs = Geom.GetReversedToolingSegments (Segs);
   }

   public PointVec Project (E3Thick ent, Point2 pt) {
      Point3 pt3; Vector3 vec;
      if (ent is E3Plane ep) {
         vec = Workpiece.Classify (ep) switch {
            Workpiece.EType.YNeg => new Vector3 (0, -1, 0),
            Workpiece.EType.YPos => new Vector3 (0, 1, 0),
            _ => new Vector3 (0, 0, 1)
         };

         pt3 = pt * ep.Xfm;
      } else if (ent is E3Flex ef) {
         (pt3, vec) = ef.Project (pt);
         var (a, b) = ef.Axis; Point3 mid = pt3.SnapToLine (a, b);
         if (vec.Opposing (pt3 - mid))
            vec = -vec; // Get the 'outward facing' vector

         pt3 += (vec * ef.Thickness / 2);
      } else
         throw new NotImplementedException ();

      return new (pt3, vec);
   }

   public bool IsCircle () {
      var segsList = Segs.ToList ();

      // Assuming that circle wil be the only segment
      return segsList[0].Curve is Arc3 arc && arc.Start.EQ (arc.End);
   }

   // Any feature other than Mark, that has a closed contour, 
   // AND also passes through E3Flex is a Cutout
   public bool IsCutout () => (Kind == EKind.Cutout);
   public bool IsMark () => (Kind == EKind.Mark);

   // Any feature which is other than Mark, that has an open profile
   // is a Notch
   public bool IsNotch () => (Kind == EKind.Notch);
   public bool IsHole () => (Kind == EKind.Hole);

   // Any feature such as Notch or hole,
   // which features on the E3Flex is a
   // FlexFeature
   public bool IsFlexFeature () => Traces.Any (a => a.Ent is E3Flex);

   // Features either Notch or hole,
   // which feature only on the E3Planes
   // and not on any E3Flex is PlaneFeature
   public bool IsPlaneFeature () => !IsFlexFeature ();

   public bool IsFlexHole () => IsHole () && IsFlexFeature ();
   public bool IsFlexCutout () => IsCutout () && IsFlexFeature ();
}