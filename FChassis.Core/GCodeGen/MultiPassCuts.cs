using Flux.API;
using FChassis.Core.Geometries;

namespace FChassis.Core.GCodeGen;

using ToolingCutScope = (double Position, ToolingScope ToolScope, bool IsStart);
using CutScopeToolingList = List<(List<Tooling> ToolingList, double XMin, double XMax)>;

public class Scope (double xmin, double xmax) {
   public double XMax { get; set; } = xmax;
   public double XMin { get; set; } = xmin;

   public static List<Scope> operator - (Scope b, Scope a) {
      List<Scope> result = [];

      // Case 1: 'a' is completely to the left of 'b' without overlap
      if (a.XMax.SLT (b.XMin)) {
         result.Add (b);
      }
      // Case 2: 'a' is completely to the right of 'b' without overlap
      else if (a.XMin.SGT (b.XMax)) {
         result.Add (b);
      }
      // Case 3: 'a' is strictly inside 'b', splitting 'b' into two separate segments
      else if (a.XMin.SGT (b.XMin) && a.XMax.SLT (b.XMax)) {
         Scope s1 = new (b.XMin, a.XMin);
         Scope s2 = new (a.XMax, b.XMax);
         result.Add (s1);
         result.Add (s2);
      }
      // Case 4: 'a' overlaps 'b' on the right side, starting from the same XMin as 'b'
      else if (a.XMin.EQ (b.XMin) && a.XMax.SLT (b.XMax)) {
         result.Add (new Scope (a.XMax, b.XMax));
      }
      // Case 5: 'a' overlaps 'b' on the left side, ending at the same XMax as 'b'
      else if (a.XMin.SGT (b.XMin) && a.XMax.EQ (b.XMax)) {
         result.Add (new Scope (b.XMin, a.XMin));
      }
      // Case 6: 'a' overlaps 'b' from the left side but does not fully cover 'b'
      else if (a.XMin.SLT (b.XMin) && a.XMax.SLT (b.XMax) && a.XMax.SGT (b.XMin)) {
         result.Add (new Scope (a.XMax, b.XMax));
      }
      // Case 7: 'a' overlaps 'b' from the right side but does not fully cover 'b'
      else if (a.XMin.SGT (b.XMin) && a.XMin.SLT (b.XMax) && a.XMax.SGT (b.XMax)) {
         result.Add (new Scope (b.XMin, a.XMin));
      }
      // Case 8: 'a' fully overlaps 'b' or matches its boundaries completely - do nothing
      else if (a.XMin.LTEQ (b.XMin) && a.XMax.GTEQ (b.XMax)) {
         // Do nothing - 'a' completely overlaps 'b'
      }
      // Default case: add 'b' if no other conditions match
      else {
         result.Add (b);
      }

      return result;
   }

   public void Assign (Scope rhs) {
      if (rhs == null) return;
      XMax = rhs.XMax; XMin = rhs.XMin;
   }
}

/// <summary>
/// ToolingScope class is a wrapper over Tooling, to contain the 
/// tooling segments as a distinct copy than the extracted tooling
/// segments from 2D, for the main purpose of storing data that is specifically
/// related to multipass machining.
/// The Tooling Scope is a characteristic of the workpiece or the Part
/// </summary>
public class ToolingScope {
   #region Data Members
   Tooling mT;
   public bool mIsLeftToRight = true;
   #endregion

   #region Constructors
   public ToolingScope (Tooling t, int index, bool isLeftToRight) {
      mT = t;
      mIsLeftToRight = isLeftToRight;
      if (mIsLeftToRight) {
         StartX = mT.Bound3.XMin;
         EndX = mT.Bound3.XMax;
      } else {
         StartX = mT.Bound3.XMax;
         EndX = mT.Bound3.XMin;
      }
      Index = index;
   }

   /// <summary>
   /// Creates the instances of ToolingScopes for each Tooling.
   /// </summary>
   /// <param name="toolings">The list of toolings as input</param>
   /// <param name="isLeftToRight">Another provision to enable machining from
   /// either side, while keeping the coordinate system of the part invariant.
   /// This is to generate the GCode for the actual LCM if needed</param>
   /// <returns></returns>
   public static List<ToolingScope> CreateToolingScopes (List<Tooling> toolings, bool isLeftToRight = true) {
      List<ToolingScope> tss = [];
      if (toolings != null) {
         for (int ii = 0; ii < toolings.Count; ii++) {
            var scope = toolings[ii];
            tss.Add (new ToolingScope (scope, ii, isLeftToRight));
         }
      }
      return tss;
   }
   #endregion

   #region Public Properties
   public double StartX { get; set; }
   public double EndX { get; set; }
   public bool ToSplit { get; set; }
   public Tooling Tooling { get => mT; set => mT = value; }
   public double Size { get => (mIsLeftToRight ? (EndX - StartX) : (StartX - EndX)); }
   public List<ToolingSegment> Segs { get => mT.Segs; }
   public int Index { get; set; }
   #endregion

   #region Predicates
   public bool IsHole () {
      if (mT.Kind == EKind.Hole || mT.Kind == EKind.Cutout || mT.Kind == EKind.Mark) return true;
      return false;
   }
   public bool IsNotch () {
      if (mT.Kind == EKind.Notch) return true;
      return false;
   }
   public bool IsProcessed { get; set; } = false;
   public bool IsNotchSplitComplete { get; set; } = false;
   public bool IsIntersect (double x, bool considerEndPoints = false) {
      if (((x - StartX) / (EndX - StartX)).LieWithin (0, 1)) {
         if (!considerEndPoints) {
            if (!(x - StartX).EQ (0) && !(x - EndX).EQ (0)) return true;
            return false;
         } else return true;
      }
      return false;
   }

   public static bool operator == (ToolingScope lhs, ToolingScope rhs) {
      if (lhs is null && rhs is null) return true;
      if (lhs is null || rhs is null) return false;
      return lhs.StartX.EQ (rhs.StartX) && lhs.EndX.EQ (rhs.EndX);
   }
   public static bool operator != (ToolingScope lhs, ToolingScope rhs) => !(lhs == rhs);

   public override bool Equals (object obj) {
      if (obj is ToolingScope other)
         return this == other;
      return false;
   }
   public override int GetHashCode () => HashCode.Combine (StartX, EndX, Index);
   #endregion
}

/// <summary>
/// CutScope class encapsulates the data and logic that is needed to hold a list
/// of Tooling Scopes. To give a context, the part is split into more than 1
/// cutscope, where each cutscope consists of tooling scopes, where each Tooling Scope
/// is a wrapper over the Tooling.
/// The number of cut scopes is dependent on the maximum cut scope length defined 
/// in the settings. For the LCM, it is 3500 mm by default.
/// 
/// While the tooling scope is a characteristic of the Workpiece or the part,
/// the cut scope is a characteristic of the machine, which is a window to look at
/// the tool scopes.
/// </summary>
public class CutScope {

   #region Predicates
   public static bool EQ (CutScope cs, ToolingScope ts, double tol) {
      if (cs.StartX.EQ (ts.StartX, tol) && cs.EndX.EQ (ts.EndX, tol)) return true;
      return false;
   }
   public static bool EQ (CutScope cs, ToolingScope ts) {
      if ((cs.StartX - ts.StartX).EQ (0) && (cs.EndX - ts.EndX).EQ (0)) return true;
      return false;
   }
   public static bool AreIdentical (CutScope cs, ToolingScope t, double tol) {
      if (cs.StartX.EQ (t.StartX, tol) && t.EndX.EQ (cs.EndX, tol)) return true;
      return false;
   }
   public static bool AreIdentical (CutScope cs, ToolingScope t) {
      if ((cs.StartX - t.StartX).EQ (0) && (t.EndX - cs.EndX).EQ (0)) return true;
      return false;
   }
   public static bool AreIdentical (CutScope cs, Tooling t, double tol) {
      if (cs.StartX.EQ (t.Bound3.XMax, tol) && Convert.ToDouble (t.Bound3.XMin).EQ (cs.EndX, tol)) return true;
      return false;
   }
   public static bool AreIdentical (CutScope cs, Tooling t) {
      if ((cs.StartX - t.Bound3.XMax).EQ (0) && (t.Bound3.XMin - cs.EndX).EQ (0)) return true;
      return false;
   }
   public static bool IsToolingWithin (CutScope cs, ToolingScope t, double tol) {
      if (cs.mIsLeftToRight && (AreIdentical (cs, t, tol) || (cs.EndX.GTEQ (t.EndX, tol) && t.StartX.GTEQ (cs.StartX, tol))))
         return true;
      if (!cs.mIsLeftToRight && (AreIdentical (cs, t, tol) || (cs.StartX.GTEQ (t.StartX, tol) && t.EndX.GTEQ (cs.EndX, tol))))
         return true;
      return false;
   }
   public static bool IsToolingWithin (CutScope cs, ToolingScope t) {
      if (cs.mIsLeftToRight && (AreIdentical (cs, t) || (cs.EndX.GTEQ (t.EndX) && t.StartX.GTEQ (cs.StartX))))
         return true;
      if (!cs.mIsLeftToRight && (AreIdentical (cs, t) || (cs.StartX.GTEQ (t.StartX) && t.EndX.GTEQ (cs.EndX))))
         return true;
      return false;
   }
   public bool AreIdentical (ToolingScope t, double tol) {
      if (StartX.EQ (t.StartX, tol) && t.EndX.EQ (EndX, tol)) return true;
      return false;
   }
   public bool AreIdentical (ToolingScope t) {
      if ((StartX - t.StartX).EQ (0) && (t.EndX - EndX).EQ (0)) return true;
      return false;
   }
   public bool AreIdentical (Tooling t, double tol) {
      if (StartX.EQ (t.Bound3.XMax, tol) && Convert.ToDouble (t.Bound3.XMin).EQ (EndX, tol)) return true;
      return false;
   }
   public bool AreIdentical (Tooling t) {
      if ((StartX - t.Bound3.XMax).EQ (0) && (t.Bound3.XMin - EndX).EQ (0)) return true;
      return false;
   }
   public static bool IsToolingIntersect (CutScope cs, ToolingScope t, double tol) {
      if (cs.mIsLeftToRight) {
         if (CutScope.EQ (cs, t, tol)) return false;
         if ((t.EndX.SGT (cs.StartX, tol) && (cs.StartX.SGT (t.StartX, tol)))) return true;
         if ((t.EndX.SGT (cs.EndX, tol) && (cs.EndX.SGT (t.StartX, tol)))) return true;
      } else {
         if (CutScope.EQ (cs, t, tol)) return false;
         if ((t.StartX.SGT (cs.EndX, tol) && (cs.EndX.SGT (t.EndX, tol)))) return true;
         if ((t.StartX.SGT (cs.StartX, tol) && (cs.StartX.SGT (t.EndX, tol)))) return true;
      }
      return false;
   }
   public static bool IsToolingIntersect (CutScope cs, ToolingScope t) {
      if (CutScope.EQ (cs, t)) return false;
      if (cs.mIsLeftToRight) {
         if ((t.EndX.SGT (cs.StartX) && (cs.StartX.SGT (t.StartX)))) return true;
         if ((t.EndX.SGT (cs.EndX) && (cs.EndX.SGT (t.StartX)))) return true;
      } else {
         if ((t.StartX.SGT (cs.EndX) && (cs.EndX.SGT (t.EndX)))) return true;
         if ((t.StartX.SGT (cs.StartX) && (cs.StartX.SGT (t.EndX)))) return true;
      }
      return false;
   }
   public static bool IsToolingIntersectForward (CutScope cs, ToolingScope t, double tol) {
      if (CutScope.EQ (cs, t, tol)) return false;
      if (cs.mIsLeftToRight) {
         if ((cs.StartX.SLT (t.StartX, tol) && (t.StartX.SLT (cs.EndX, tol) && (cs.EndX.SLT (t.EndX, tol))))) return true;
      } else {
         if ((cs.StartX.SGT (t.StartX, tol) && (t.StartX.SGT (cs.EndX, tol) && (cs.EndX.SGT (t.EndX, tol))))) return true;
      }
      return false;
   }
   public static bool IsToolingIntersectForward (CutScope cs, ToolingScope t) {
      if (CutScope.EQ (cs, t)) return false;
      if (cs.mIsLeftToRight) {
         if (((cs.StartX.SLT (t.StartX) || cs.StartX.EQ (t.StartX)) && (t.StartX.SLT (cs.EndX) && (cs.EndX.SLT (t.EndX))))) return true;
      } else {
         if (((cs.StartX.SGT (t.StartX) || cs.StartX.EQ (t.StartX)) && (t.StartX.SGT (cs.EndX) && (cs.EndX.SGT (t.EndX))))) return true;
      }
      return false;
   }
   public static bool IsToolingIntersectReverse (CutScope cs, ToolingScope t, double tol) {
      if (CutScope.EQ (cs, t, tol)) return false;
      if (cs.mIsLeftToRight) {
         if ((t.StartX.SLT (cs.StartX, tol) && (cs.StartX.SLT (t.EndX, tol) && (t.EndX.SLT (cs.EndX, tol))))) return true;
      } else {
         if ((t.StartX.SGT (cs.StartX, tol) && (cs.StartX.SGT (t.EndX, tol) && (t.EndX.SGT (cs.EndX, tol))))) return true;
      }
      return false;
   }
   public static bool IsToolingIntersectReverse (CutScope cs, ToolingScope t) {
      if (CutScope.EQ (cs, t)) return false;
      if (cs.mIsLeftToRight) {
         if ((t.StartX.SLT (cs.StartX) && (cs.StartX.SLT (t.EndX) && (t.EndX.SLT (cs.EndX))))) return true;
      } else {
         if ((t.StartX.SGT (cs.StartX) && (cs.StartX.SGT (t.EndX) && (t.EndX.SGT (cs.EndX))))) return true;
      }
      return false;
   }
   #endregion

   #region Data Members
   double mMaxScopeLength;
   public bool mIsLeftToRight;
   Bound3 mBoundExtent;
   MultiPassCuts mMPC;
   public List<int> mToolingScopesWithin;
   public List<int> mToolingScopesIntersect;
   public List<int> mToolingScopesIntersectForward;
   public List<int> mToolingScopesIntersectReverse;
   public List<int> mHolesWithin;
   public List<int> mHolesIntersect;
   public List<int> mHolesIntersectFwd;
   public List<int> mNotchesWithin;
   public List<int> mNotchesIntersect;
   #endregion

   #region Constructor(s)   
   /// <summary>
   /// The following constructor is to represent the DEAD Band CutScope object that covers Dead Band  Area.
   /// THough this is Cut Scope, this scope is the DeadBand scope
   /// </summary>
   /// <param name="startX">StartX position of the total cut scope</param>
   /// <param name="endX">End X position of the parent cut scope</param>
   /// <param name="maximumScopeLength">The maximum length of the parent cut scope</param>
   /// <param name="deadbandWidth">The width of the scope that is uncut-able, within the parent scope</param>
   /// <param name="mpc">The reference to Multipass Cut object</param>
   /// <param name="isLeftToRight">By default it is true</param>
   public CutScope (double startX, double deadbandWidth, double MaximumScopeLength, ref MultiPassCuts mpc, bool isLeftToRight = true) {
      DeadBandWidth = deadbandWidth;
      StartX = startX + MaximumScopeLength / 2.0 - deadbandWidth / 2.0;
      EndX = startX + MaximumScopeLength / 2.0 + deadbandWidth / 2.0;
      IsUncuttableScope = true;

      StartX.Clamp (mBoundExtent.XMin, mBoundExtent.XMax);
      EndX.Clamp (mBoundExtent.XMin, mBoundExtent.XMax);

      mIsLeftToRight = isLeftToRight;
      mMPC = mpc;
      if (isLeftToRight) mMaxScopeLength = EndX - startX;
      else mMaxScopeLength = StartX - EndX;
      CutScope.SortAndReIndex (ref mpc, mIsLeftToRight);

      mToolingScopesWithin = [];
      mToolingScopesIntersect = [];
      mToolingScopesIntersectForward = [];
      mHolesIntersect = [];
      mHolesIntersectFwd = [];
      CategorizeToolings ();
      DeadBandScopeToolingIndices = [.. mToolingScopesWithin, .. mToolingScopesIntersect];
      DeadBandScope = new (StartX, EndX);
      DeadBandScopeFwdIxnToolingIndices = mToolingScopesIntersectForward;
      DeadBandScopeFwdHolesIndices = mHolesIntersect;
      DeadBandScopeFwdIxnHolesIndices = mHolesIntersectFwd;
      ComputeBound ();
   }
   public CutScope (double startX, double offset, double endXPos, Bound3 boundExtent,
      double MaximumScopeLength, double deadbandWidth, ref MultiPassCuts mpc, bool isLeftToRight) {
      mBoundExtent = boundExtent;
      if (isLeftToRight) StartX = startX + offset;
      else StartX = startX - offset;
      EndX = endXPos;
      DeadBandWidth = deadbandWidth;

      // Clampers
      StartX.Clamp (mBoundExtent.XMin, mBoundExtent.XMax);
      EndX.Clamp (mBoundExtent.XMin, mBoundExtent.XMax);

      mIsLeftToRight = isLeftToRight;
      if (endXPos > StartX && !mIsLeftToRight) throw new Exception ("CutScope.Constructor: The EndXPos < (Maxx - offset)");
      if (endXPos < StartX && mIsLeftToRight) throw new Exception ("CutScope.Constructor: The EndXPos > (Maxx - offset)");

      // Left To Right accounted for
      if (mIsLeftToRight) Size = endXPos - StartX;
      else Size = StartX - endXPos;
      mMPC = mpc;
      CutScope.SortAndReIndex (ref mpc, mIsLeftToRight);
      mToolingScopesWithin = [];
      mToolingScopesIntersect = [];
      mMaxScopeLength = MaximumScopeLength;
      CategorizeToolings ();
      if (mpc.mGC.Heads == MCSettings.EHeads.Both) {
         if (!deadbandWidth.EQ (0)) AssessDeadBandFeatIndices (ref mMPC);
      } else
         DeadBandScope = null;
      ComputeBound ();
   }

   public void AssessDeadBandFeatIndices (ref MultiPassCuts mpc) {
      if (mpc.mGC.Heads == MCSettings.EHeads.Both) {
         DeadBandCutScope = new CutScope (StartX, DeadBandWidth, mMaxScopeLength, ref mpc);
         DeadBandCutScope.DeadBandScopeToolingIndices.Sort ();
         DeadBandScopeToolingIndices = DeadBandCutScope.DeadBandScopeToolingIndices;
         DeadBandScope = new (StartX + mpc.MaximumScopeLength / 2 - DeadBandWidth / 2, StartX + mpc.MaximumScopeLength / 2 + DeadBandWidth / 2);
      }
   }

   public List<ToolingScope> DeadBandToolingScopes (List<ToolingScope> refTss = null) {
      List<ToolingScope> dbAssociatedCutScopes;
      if (refTss != null)
         dbAssociatedCutScopes = MultiPassCuts.GetToolScopesFromIndices (DeadBandScopeToolingIndices, refTss, true);
      else
         dbAssociatedCutScopes = MultiPassCuts.GetToolScopesFromIndices (DeadBandScopeToolingIndices, ToolingScopes, true);
      return dbAssociatedCutScopes;
   }
   #endregion

   #region Properties
   public double Score { get; set; }
   double DynamicScore { get; set; }
   public List<ToolingScope> mMachinableToolingScopes;
   public List<ToolingScope> MachinableToolingScopes { get => mMachinableToolingScopes; set => mMachinableToolingScopes = value; }
   double mStartX, mSize;
   public double StartX { get => mStartX; set => mStartX = value; }
   double mEndX;
   public double EndX { get => mEndX; set => mEndX = value; }
   public double Size { get => mSize; set => mSize = value; }
   public void Move (double move, double size) { StartX -= move; Size = size; }
   public bool IsCutFeasible { get => mHolesIntersect.Count == 0; }
   public bool IsLengthFeasible { get => Size.LTEQ (mMaxScopeLength); }
   public bool IsIntersectWithNotches { get => mNotchesIntersect.Count > 0; }
   public bool IsAllFeasible { get => IsLengthFeasible && !IsIntersectWithNotches && IsCutFeasible; }
   public bool IsFeasibleExceptLength { get => !IsIntersectWithNotches && IsCutFeasible; }
   public bool IsPriority2Feasible { get => IsLengthFeasible && IsCutFeasible && !IsAllFeasible; }
   public double ScopeLength { get => Math.Abs (StartX - EndX); }
   public List<ToolingScope> ToolingScopes { get => mMPC.ToolingScopes; set => mMPC.ToolingScopes = value; }
   public double DeadBandWidth { get; set; }
   public List<int> DeadBandScopeToolingIndices { get; set; } = [];
   public List<int> DeadBandScopeFwdIxnToolingIndices { get; set; } = [];
   public List<int> DeadBandScopeFwdIxnHolesIndices { get; set; } = [];
   public List<int> DeadBandScopeFwdHolesIndices { get; set; } = [];
   public bool IsUncuttableScope { get; set; }
   public Scope DeadBandScope { get; set; }
   public CutScope DeadBandCutScope { get; set; }
   Bound3 mBound;
   public Bound3 Bound {
      get {
         return mBound;
      }
   }
   #endregion

   #region Data Processors
   public void ComputeBound () {
      var associatedFeats = MultiPassCuts.GetToolScopesFromIndices (AssociatedFeatures (),
         ToolingScopes).OrderBy (f => f.StartX).ToList ();
      var allSPoints = associatedFeats
         .SelectMany (scope => scope.Tooling.Segs)
         .Select (seg => seg.Curve.Start).ToList ();
      var allEPoints = associatedFeats
         .SelectMany (scope => scope.Tooling.Segs)
         .Select (seg => seg.Curve.End).ToList ();
      List<Point3> allPoints = [.. allSPoints, .. allEPoints];

      // Min Y, Max Y, Min Z, Max Z
      if (allPoints.Count > 0) {
         double minY = allPoints.Min (point => point.Y);
         double maxY = allPoints.Max (point => point.Y);
         double minZ = allPoints.Min (point => point.Z);
         double maxZ = allPoints.Max (point => point.Z);
         mBound = new Bound3 (StartX, minY, minZ, EndX, maxY, maxZ);
      } else
         mBound = new Bound3 ();
   }
   public bool DoesIntersectAnyFeatures () => mToolingScopesIntersect.Count > 0;
   public List<int> IntersectingFeatures () => mToolingScopesIntersect;
   public List<int> ContainedFeatures () => mToolingScopesWithin;
   public List<int> AssociatedFeatures () => [.. mToolingScopesWithin, .. mToolingScopesIntersect];
   public bool DoesContainAnyFeatures () => mToolingScopesWithin.Count > 0;
   /// <summary>
   /// This method categorizes the features or toolings under four 
   /// broader categories. They are
   /// <list type="number">
   /// <item>
   /// <description>The tooling is well within the Start and End X Positions of the cut scope</description>
   /// </item>
   /// <item>
   /// <description>The tooling intersects the cut scope</description>
   /// </item>
   /// <item>
   /// <description>The tooling intersects the cut scope in the forward direction of the tooling (EndX)</description>
   /// </item>
   /// <item>
   /// <description>The tooling intersects the cut scope in the reverse direction of the tooling (StartX)</description>
   /// </item>
   /// </list>
   /// </summary>
   public void CategorizeToolings () {
      var cs = this;
      mToolingScopesWithin = [];
      mToolingScopesIntersect = [];
      mToolingScopesIntersectForward = [];
      mToolingScopesIntersectReverse = [];
      CutScope.SortAndReIndex (ref mMPC, mIsLeftToRight);
      int ii = 0;
      for (ii = 0; ii < mMPC.ToolingScopes.Count; ii++) {
         var it = mMPC.ToolingScopes[ii];
         if (it.IsProcessed) continue;
         if (CutScope.IsToolingWithin (cs, it))
            mToolingScopesWithin.Add (ii);
         if (CutScope.IsToolingIntersect (cs, it))
            mToolingScopesIntersect.Add (ii);
         if (CutScope.IsToolingIntersectForward (cs, it))
            mToolingScopesIntersectForward.Add (ii);
         if (CutScope.IsToolingIntersectReverse (cs, it))
            mToolingScopesIntersectReverse.Add (ii);
      }
      var csc = this;
      mHolesWithin = [.. mToolingScopesWithin.Where (tsix => csc.mMPC.ToolingScopes[tsix].IsHole ())];
      mNotchesWithin = [.. mToolingScopesWithin.Where (tsix => csc.mMPC.ToolingScopes[tsix].IsNotch ())];
      mHolesIntersect = [.. mToolingScopesIntersect.Where (tsix => csc.mMPC.ToolingScopes[tsix].IsHole ())];
      mNotchesIntersect = [.. mToolingScopesIntersect.Where (tsix => csc.mMPC.ToolingScopes[tsix].IsNotch ())];
      mHolesIntersectFwd = [.. mToolingScopesIntersectForward.Where (tsix => csc.mMPC.ToolingScopes[tsix].IsHole ())];
   }

   public List<int> NotchesIntersectIndices () => mNotchesIntersect;
   public List<ToolingScope> NotchesIntersect () {
      var csc = this;
      var ixnNotches = mToolingScopesIntersect.Where (tsix => csc.mMPC.ToolingScopes[tsix].IsNotch ()).Select (tsix => csc.mMPC.ToolingScopes[tsix]).ToList ();
      return ixnNotches;
   }

   public List<ToolingScope> GetForwardIxnHoles () {
      var holesIxnsFwd = MultiPassCuts.GetToolScopesFromIndices (mToolingScopesIntersectForward, ToolingScopes, excludeProcessed: true).OrderBy (t => t.StartX).ToList ();
      return holesIxnsFwd;

   }

   public List<ToolingScope> IntersectingNotches (List<ToolingScope> refTss = null) {
      if (refTss == null)
         return [.. MultiPassCuts.GetToolScopesFromIndices (mNotchesIntersect, ToolingScopes).OrderBy (t => t.StartX)];
      return [.. MultiPassCuts.GetToolScopesFromIndices (mNotchesIntersect, refTss).OrderBy (t => t.StartX)];
   }

   /// <summary>
   /// This method splits a tooling scope if the position of X of any feature
   /// (read as hole or notch or mark or cutout) happens strictly within 
   /// ( which means that the position of X does not equals the position of start of 
   /// X of any feature ) the tool scope. 
   /// A new tool scope is created for each split. The underlying tooling will e split later.
   /// </summary>
   /// <param name="tss">List of tool scopes</param>
   /// <param name="ixnX">Absolute position of X of a feature to be tested if that feature is contained 
   /// by more than two tooling scopes by means of intersection check</param>
   /// <param name="bound">The bounding box</param>
   /// <param name="thresholdLength">This is to mark a minimum length below which the feature will 
   /// not be cut</param>
   /// <param name="splitNotches">Boolean switch if notches have to be split</param>
   /// <param name="splitNonNotches">Boolean switch if non-notches have to be split</param>
   /// <returns>Returns the tuple of List of tool scopes and a bool flag if the initial list has been modified by split</returns>
   public static Tuple<List<ToolingScope>, bool> SplitToolingScopesAtIxn (List<ToolingScope> tss, double ixnX,
      Bound3 bound, GCodeGenerator gcGen, double thresholdLength = -1, bool splitNotches = true, bool splitNonNotches = false) {
      var res = tss;
      bool listModified = false;
      for (int ii = 0; ii < res.Count; ii++) {
         var ts = tss[ii];
         if (ts.IsNotchSplitComplete) continue;
         if (ts.IsNotch () && !splitNotches) continue;
         if (!ts.IsNotch () && !splitNonNotches) continue;
         bool canSplit = true;
         if (thresholdLength > 0 && (ts.Size.SLT (thresholdLength))) canSplit = false;
         if (!canSplit) continue;
         if (ts.IsIntersect (ixnX, considerEndPoints: false)) {
            // Remove the element at index i
            res.RemoveAt (ii);
            ToolingScope ts1 = new (ts.Tooling, ii, ts.mIsLeftToRight) {
               EndX = ixnX,
               ToSplit = true,
               Tooling = ts.Tooling.Clone ()
            };
            ts1.Tooling.Name += "-Split-1";
            ts1.Tooling.FeatType = ts.Tooling.FeatType + "-Split-1";

            // Split segments of ts1.Tooling
            ts1.Tooling.Segs = Utils.SplitNotchToScope (ts1, ts1.mIsLeftToRight, 1e-4);
            ts1.Tooling.Bound3 = Utils.CalculateBound3 (ts1.Tooling.Segs, bound);
            ts1.Tooling.NotchKind = Tooling.GetCutKind (ts1.Tooling, gcGen.GetXForm ());
            ts1.Tooling.ProfileKind = Tooling.GetCutKind (ts1.Tooling, XForm4.IdentityXfm);
            ts1.Tooling.RefTooling = ts.Tooling;
            ToolingScope ts2 = new (ts.Tooling, ii + 1, ts.mIsLeftToRight) {
               StartX = ixnX,
               ToSplit = true,
               Tooling = ts.Tooling.Clone ()
            };
            ts2.Tooling.Name += "-Split-2";
            ts2.Tooling.FeatType = ts.Tooling.FeatType + "-Split-2";
            ts2.Tooling.RefTooling = ts.Tooling;

            // Split segments of ts2.Tooling
            ts2.Tooling.Segs = Utils.SplitNotchToScope (ts2, ts2.mIsLeftToRight, 1e-4);
            ts2.Tooling.Bound3 = Utils.CalculateBound3 (ts2.Tooling.Segs, bound);
            ts2.Tooling.NotchKind = Tooling.GetCutKind (ts2.Tooling, gcGen.GetXForm ());
            ts2.Tooling.ProfileKind = Tooling.GetCutKind (ts2.Tooling, XForm4.IdentityXfm);

            // Insert two new elements at the same index i
            res.Insert (ii, ts1);
            res.Insert (ii + 1, ts2);
            listModified = true;
            ii += 1; // Increment the index to move past the newly inserted elements
         }
      }
      if (res.Count == 0) res = tss;
      return new Tuple<List<ToolingScope>, bool> (res, listModified);
   }

   /// <summary>
   /// This method reindexes the tooling scopes in multi passcut object. 
   /// It also orders the tooling scopes by asending or descending order first
   /// by StartX of tooling scope and then by EndX
   /// </summary>
   /// <param name="mpc">The input multipass object</param>
   /// <param name="isLeftToRight">A flag that tells if the job is fed in the left to right (default)
   /// or right to left order</param>
   public static void SortAndReIndex (ref MultiPassCuts mpc, bool isLeftToRight = true) {
      if (isLeftToRight)
         mpc.ToolingScopes = [.. mpc.ToolingScopes.OrderBy (t => t.StartX).ThenBy (t => t.EndX)];
      else mpc.ToolingScopes = [.. mpc.ToolingScopes.OrderByDescending (t => t.StartX).ThenByDescending (t => t.EndX)];
      for (int ii = 0; ii < mpc.ToolingScopes.Count; ii++) {
         var ts = mpc.ToolingScopes[ii];
         ts.Index = ii;
         mpc.ToolingScopes[ii] = ts;
      }
   }
   #endregion
}

/// <summary>
/// MultiPassCuts class is a container, processor, and G Code writer for lengthy parts
/// whose length exceeds the maximum cut scope length specified in the settings.
/// 
/// This class manages the list of cut scopes after optimally splitting them based on
/// the following parameters:
/// 
/// <list type="number">
///   <item>
///      <description>Flange Cutting Order (for each head):</description>
///         <list type="bullet">
///            <item><description>Bottom flange</description></item>
///            <item><description>Top flange</description></item>
///            <item><description>Web flange</description></item>
///         </list>
///   </item>
///   <item>
///      <description>Feature Cutting Order:</description>
///         <list type="bullet">
///            <item><description>Holes</description></item>
///            <item><description>Notches</description></item>
///            <item><description>Cutouts</description></item>
///            <item><description>Markings</description></item>
///         </list>
///   </item>
///   <item>
///      <description>Optimization objectives include:</description>
///         <list type="bullet">
///            <item><description>Minimizing the cutting times of both heads</description></item>
///            <item><description>Minimizing the wait time of one head for the other</description></item>
///            <item><description>Avoiding situations where one head waits for the other to complete</description></item>
///            <item><description>Preventing one head from coming into close proximity to the other</description></item>
///         </list>
///   </item>
/// </list>
/// </summary>
public class MultiPassCuts {
   #region Enums
   enum PassType {
      CutScope,
      DeadBand
   }
   #endregion

   #region Data Members
   List<ToolingScope> mTStarts, mTEnds;
   List<ToolingScope> mTscs;
   bool mIsLeftToRight;
   public List<CutScope> MachinableCutScopes;
   List<ToolingScope> mToolScopes = [];
   List<ToolingCutScope> mOrderedToolScopes = [];
   #endregion

   #region Properties
   public List<ToolingScope> ToolingScopes { get => mTscs; set { mTscs = value; } }
   public GCodeGenerator mGC;
   public List<List<GCodeSeg>[]> CutScopeTraces { get; set; }
   public double MaximumWorkpieceLength { get; set; }
   public double MaximumScopeLength { get; set; }
   public bool MaximizeFrameLengthInMultipass { get; set; }
   public double MinimumThresholdNotchLength { get; set; }
   public double XMax { get; set; }
   public double XMin { get; set; }
   public Bound3 Bound { get; set; }
   public Model3 Model { get; set; }
   public static double Tolerance { get; } = 1e-4;
   public double DeadBandWidth { get; set; }
   #endregion

   #region Data Processors
   public void ClearZombies () => CutScopeTraces.Clear ();
   #endregion

   #region Enums
   public enum ToolingAssocType {
      Start,
      End,
      BetEndStart,
      Intersect,
      None
   }
   #endregion

   #region Constructor(s)
   public MultiPassCuts (List<Tooling> ts, GCodeGenerator gen, Model3 model,
      bool isLeftToRight,
      double maxFrameLengthInMultipass, double deadbandWidth,
      bool maximizeFrameLengthInMultipass,
      double minThresholdNotchLength = 15.0) {
      mGC = gen;
      XMax = model.Bound.XMax;
      XMin = model.Bound.XMin;
      Bound = model.Bound;
      Model = model;
      mIsLeftToRight = isLeftToRight;
      MaximumWorkpieceLength = XMax - XMin;
      DeadBandWidth = deadbandWidth;
      ToolingScopes = ToolingScope.CreateToolingScopes (ts, mIsLeftToRight);
      if (mIsLeftToRight) {
         ToolingScopes = [.. ToolingScopes.OrderBy (ts => ts.StartX)];
         mTStarts = [.. ToolingScopes.OrderBy (ts => ts.StartX)];
         mTEnds = [.. ToolingScopes.OrderBy (ts => ts.EndX)];
         mOrderedToolScopes = [.. mOrderedToolScopes.OrderBy (e => e.Position)];

      } else {
         ToolingScopes = [.. ToolingScopes.OrderByDescending (ts => ts.StartX)];
         mTStarts = [.. ToolingScopes.OrderByDescending (ts => ts.StartX)];
         mTEnds = [.. ToolingScopes.OrderByDescending (ts => ts.EndX)];
         mOrderedToolScopes = [.. mOrderedToolScopes.OrderByDescending (e => e.Position)];
      }
      MaximumScopeLength = maxFrameLengthInMultipass;
      MaximizeFrameLengthInMultipass = maximizeFrameLengthInMultipass;
      mOrderedToolScopes.AddRange (mTStarts.Select (ts => (ts.StartX, ts, true)));
      mOrderedToolScopes.AddRange (mTStarts.Select (ts => (ts.EndX, ts, false)));
      mOrderedToolScopes.AddRange (mTEnds.Select (ts => (ts.StartX, ts, true)));
      mOrderedToolScopes.AddRange (mTEnds.Select (ts => (ts.EndX, ts, false)));

      // Filter out duplicates based on tolerance
      var filteredToolScopes = new List<(double Position, ToolingScope ToolScope, bool IsStart)> ();
      var duplicates = new Dictionary<double, bool> ();

      foreach (var e in mOrderedToolScopes) {
         if (!duplicates.Any (seen => Math.Abs (seen.Key - e.Position) < 1e-6 && seen.Value == e.IsStart)) {
            filteredToolScopes.Add (e);
            duplicates[e.Position] = e.IsStart;
         }
      }
      mOrderedToolScopes = filteredToolScopes;
      for (int ii = 0; ii < mOrderedToolScopes.Count; ii++) {
         var (_, toolScope, isStart) = mOrderedToolScopes[ii];
         if (isStart) mToolScopes.Add (toolScope);
      }
      MinimumThresholdNotchLength = minThresholdNotchLength;
   }
   #endregion

   #region Predicates
   public static bool IsMultipassCutTask (Model3 model) {
      if ((((double)(model.Bound.XMax - model.Bound.XMin)).SLT (MCSettings.It.MaxFrameLength))) return false;
      return true;
   }
   #endregion

   #region Optimizers

   /// <summary>
   /// This method is a quasi optimal cut scope computer, which considers the spatial parameters of the part alone, 
   /// and the makespan, (which is the total time taken to cut). 
   /// This method has a set of heuristic constraints applied, (which makes it quasi optimal), which are
   /// <list type="number">
   ///   <item>
   ///   <description>The cutscope length shall be strived to be kept at maximum scope length if the option
   ///   Maximize Frame Length is opted. In order to keep this maximum frame length per cut scope, 
   ///   if a notch intersects at this length, the notch is split into two.</description>
   ///   </item>
   ///   <item>
   ///   <description>If Minimize the notch split is opted, the maximum cut scope length is relaxed to have shorter values
   ///   so that a notch split, if can be avoided, shall be avoided</description>
   ///   </item>
   /// </list>
   /// </summary>
   /// <exception cref="InvalidOperationException">Every tool scope should be assessed to check for the two options.If 
   /// a tool scope is not considered, it is thrown as an exception. This error may happen if the two optimizing 
   /// criteria compete with each other and one or more tool scopes is left out without consideration.</exception>
   public void ComputeQuasiOptimalCutScopes () {
      var mpc = this;
      MachinableCutScopes = MultiPassCuts.GetQuasiOptimalCutScopes (ref mpc, mIsLeftToRight, mpc.MaximumScopeLength,
            this.XMin, this.XMax, MaximizeFrameLengthInMultipass, MinimumThresholdNotchLength);

      // Intentionally throw exception if even one of the Toolscopes is not accommodated in one of the 
      // cutscopes. This is set as IsProcessed flag on the ToolScope
      for (int ii = 0; ii < MachinableCutScopes.Count; ii++) {
         for (int jj = 0; jj < MachinableCutScopes[ii].MachinableToolingScopes.Count; jj++)
            if (!MachinableCutScopes[ii].MachinableToolingScopes[jj].IsProcessed)
               throw new Exception ("One or more ToolScopes have not been processed.");
      }
   }

   /// <summary>
   /// This is a utility method to get all the unprocessed tool scopes.
   /// </summary>
   /// <param name="cs">The input cut scope</param>
   /// <param name="tss">The list of tooling scopes.</param>
   /// <returns>List of tooling scopes which are not processed</returns>
   static List<ToolingScope> GetUncutToolingScopesWithin (CutScope cs, List<ToolingScope> tss) {
      List<int> featsWithInIxs = [];
      if (cs.mIsLeftToRight)
         featsWithInIxs = [.. cs.mToolingScopesWithin.OrderBy (tsix => tss[tsix].StartX)];
      else
         featsWithInIxs = [.. cs.mToolingScopesWithin.OrderByDescending (tsix => tss[tsix].StartX)];
      featsWithInIxs = [.. featsWithInIxs.Where (index => !cs.DeadBandScopeToolingIndices.Contains (index))];
      var featsWithIn = featsWithInIxs
          .Where (index => index >= 0 && index < tss.Count) // Ensure index is valid
                                                            //.Where (index => !cs.DeadBandScopeToolingIndices.Contains (index)) // Exclude deadband indices
          .Select (index => tss[index])
          .Where (ts => ts.IsProcessed == false) // Only unprocessed tooling scopes
          .ToList ();
      return featsWithIn;
   }

   public static List<ToolingScope> GetToolScopesFromIndices (List<int> tsIxs, List<ToolingScope> refTss, bool excludeProcessed = false) {
      var tss = tsIxs.Where (index => index >= 0 && index < refTss.Count) // Ensure index is valid
          .Select (index => refTss[index])
          .Where (ts => (excludeProcessed && ts.IsProcessed == false) || excludeProcessed == false)
          .OrderBy (ts => ts.StartX)
          .ToList ();
      return tss;
   }

   static List<ToolingScope> GetDeadBandToolingScopes (CutScope deadBandCutScope, List<ToolingScope> tss) {
      var deadBandUnprocessedToolings = deadBandCutScope.DeadBandScopeToolingIndices
          .Where (index => index >= 0 && index < tss.Count) // Ensure index is valid
          .Select (index => tss[index])
          .Where (ts => ts.IsProcessed == false) // Only unprocessed tooling scopes
          .OrderBy (ts => ts.StartX)
          .ToList ();
      return deadBandUnprocessedToolings;
   }

   /// <summary>
   /// This method prepares the cut spans with the priority prescribed, which is 
   /// if the maximal cut scope be retained and any notches not conforming to be cut
   /// OR prioritize no notch is split while trading in reducing the max cut span
   /// </summary>
   /// <param name="mpc">The multipass cut object</param>
   /// <param name="isLeftToRight">Provision to include feeding the sheet metal in both 
   /// the directions.</param>
   /// <param name="maxScopeLength">The maximum scope length</param>
   /// <param name="minXWorkPiece">The minimum X position</param>
   /// <param name="maxXWorkPiece">The maximum X position</param>
   /// <param name="bbox">The bounding box of the entire part</param>
   /// <param name="maximizeScopeLength">Priority criterian</param>
   /// <param name="minimumThresholdNotchLength">The minimum length of notch which is 
   /// practical w.r.t machine considering its control system.</param>
   /// <returns>The quasi optimized list of cut scopes.</returns>
   /// <exception cref="Exception">An exception is thrown if one ore more of the 
   /// features can not be accommodated within the cutscope</exception>
   public static List<CutScope> GetQuasiOptimalCutScopes (ref MultiPassCuts mpc, bool isLeftToRight,
   double maxScopeLength, double minXWorkPiece, double maxXWorkPiece,
   bool maximizeScopeLength, double minimumThresholdNotchLength) {
      PassType passType = PassType.CutScope;
      bool minimizeNotchSplits = !maximizeScopeLength;
      List<CutScope> resCSS = [];
      if (mpc.ToolingScopes.Count == 0) return resCSS;
      CutScope.SortAndReIndex (ref mpc, isLeftToRight);

      double offset = 0, startXPos = 0, endXPos = 0;
      if (mpc.mGC.Heads == MCSettings.EHeads.Both) {
         if (isLeftToRight) {
            offset = mpc.ToolingScopes[0].StartX - minXWorkPiece;
            startXPos = minXWorkPiece;
            if (offset < 0) offset = 0;
            endXPos = startXPos + offset + maxScopeLength;
         } else {
            offset = maxXWorkPiece - mpc.ToolingScopes[0].StartX;
            if (offset < 0) offset = 0;
            startXPos = maxXWorkPiece;
            endXPos = startXPos - offset - maxScopeLength;
         }
      } else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right) {
         offset = mpc.ToolingScopes[0].StartX - minXWorkPiece;
         startXPos = minXWorkPiece;
         if (offset < 0) offset = 0;
         endXPos = startXPos + offset + ((maxScopeLength - mpc.DeadBandWidth) / 2.0);
      } else throw new Exception ("In GetQuasiOptimalCutScopes: Unknown head");

      // Input to the following while loop are startXPos <-- Starting position of the new cutScope
      // endXPos <-- The position at which the cut scope is desired to end
      // offset <-- The initial gap from the startXPos. This is the StartX of the first tooling scope
      // for the first cut scope otherwise "0"
      int uptoIndicesProcessed = -1;
      bool firstRun = true;
      int count = 0;
      HashSet<double> singularEndXPositions = [];
      Scope currDBScope = new (0, 0), prevDBScope = new (0, 0);
      Dictionary<ToolingScope, bool> processedTSSKVPairs = [];
      int pass = 1;
      while (true) {
         // Order by decending StartX keeps the highest StartX at the 0th element. 
         // Create the CutScope from MaximumWorkpieceLength to highest startX toolscope.
         CutScope cs = null;
         if (mpc.mGC.Heads == MCSettings.EHeads.Both) {
            cs = new (startXPos, offset, endXPos, mpc.Model.Bound, maxScopeLength, mpc.DeadBandWidth,
            ref mpc, mpc.mIsLeftToRight);
         } else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right) {
            cs = new (startXPos, offset, endXPos, mpc.Model.Bound, maxScopeLength, mpc.DeadBandWidth,
            ref mpc, mpc.mIsLeftToRight);
         } else throw new Exception ("No tool head is active or set");
         startXPos = cs.StartX;
         currDBScope = cs.DeadBandScope;
         var tss = mpc.ToolingScopes;
         var endXCache = endXPos;
         var stXCache = startXPos;

         bool splitToolScopes1 = false, splitToolScopes2 = false;
         List<ToolingScope> notchesIxnDeadBand = cs.DeadBandScope == null
          ? [] : cs.DeadBandCutScope?.NotchesIntersect () ?? [];

         // Split intersectingScopes if they are notches
         if (mpc.mGC.Heads == MCSettings.EHeads.Both) {
            (mpc.ToolingScopes, splitToolScopes1) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes, currDBScope.XMin, mpc.Bound,
                              mpc.mGC,
                              thresholdLength: 100, splitNotches: true, splitNonNotches: false);
            (mpc.ToolingScopes, splitToolScopes2) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes, currDBScope.XMax, mpc.Bound,
                                 mpc.mGC,
                                 thresholdLength: 100, splitNotches: true, splitNonNotches: false);
         } else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right) {
            (mpc.ToolingScopes, splitToolScopes1) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes,
               startXPos + ((maxScopeLength - mpc.DeadBandWidth) / 2.0), mpc.Bound, mpc.mGC,
               thresholdLength: 100, splitNotches: true, splitNonNotches: false);
         }

         if (splitToolScopes1 || splitToolScopes1) {
            cs = new (startXPos, offset, endXPos, mpc.Model.Bound, maxScopeLength, mpc.DeadBandWidth,
            ref mpc, mpc.mIsLeftToRight);
            startXPos = cs.StartX;
            currDBScope = cs.DeadBandScope;
            tss = mpc.ToolingScopes;
            endXCache = endXPos;
            stXCache = startXPos;
            splitToolScopes1 = false;
            splitToolScopes2 = false;
         }
         var dbAssociatedCutScopes = GetToolScopesFromIndices (cs.DeadBandScopeToolingIndices, tss, true);
         var featsWithIn = GetUncutToolingScopesWithin (cs, tss);
         //bool startXPosChanged = false;
         bool endXPosChanged = false;
         var dbTsc = cs.DeadBandToolingScopes (tss);
         //if (featsWithIn.Count > 0 && !(featsWithIn[0].StartX - startXPos).EQ (0) && // If there is a need
         //   (dbTsc.Count>0&& featsWithIn[0].StartX.SLT (dbTsc.First ().StartX) )) {

         //   // Otherwise, the StartX changes to the after end of dead scope
         //   // which will omit the dead band altogether
         //   startXPos = featsWithIn[0].StartX;
         //   endXPos = Math.Min (startXPos + maxScopeLength, mpc.XMax);
         //   startXPosChanged = true;
         //}
         if (firstRun) firstRun = false;

         // Recreate cutscope if there is a change in startXPos
         //if (startXPosChanged) {
         //   cs = new (startXPos, 0, endXPos, mpc.Model.Bound, maxScopeLength, mpc.DeadBandWidth, ref mpc, mpc.mIsLeftToRight);
         //   tss = mpc.ToolingScopes;
         //   featsWithIn = GetUncutToolingScopesWithin (cs, tss);
         //   currDBScope = cs.DeadBandScope;
         //}
         List<int> ixnNotchesIndxs = [];
         bool overrideMinNotchCutOption = false;
         do {
            overrideMinNotchCutOption = false;
            endXPosChanged = false;

            // Get the intersecting holes. Move the startX to the highest startx of the ixnHoles
            var ixnHolesIdxs = cs.mToolingScopesIntersectForward.Where (tsix => tss[tsix].IsHole ()).ToList ();
            var ixnHoles = ixnHolesIdxs
               .Where (index => index >= 0 && index < tss.Count) // Ensure index is valid
               .Select (index => tss[index])
               .ToList ();
            if (mpc.mIsLeftToRight) ixnHoles = [.. ixnHoles.OrderBy (tl => tl.StartX)];
            else ixnHoles = [.. ixnHoles.OrderByDescending (tl => tl.StartX)];

            // If there are no notches intersecting, the new EndXPos will be the minimum StartX
            // in the ixnHoles tooling scopes, for left to right machining, and maximum StartX otherwise.
            // Though we need to keep max Scope length, if there are intersecting holes,
            // the new end position will be the starting point of the first hole.
            if (ixnHoles.Count > 0) {
               endXPos = ixnHoles[0].StartX;
               cs = new (startXPos, 0, endXPos, mpc.Model.Bound, maxScopeLength, mpc.DeadBandWidth, ref mpc, mpc.mIsLeftToRight);
               tss = mpc.ToolingScopes;
               featsWithIn = GetUncutToolingScopesWithin (cs, tss);
               currDBScope = cs.DeadBandScope;
            }

            // Check if any notches are intersecting
            // Get the ixn notches
            ixnNotchesIndxs = [.. cs.mToolingScopesIntersectForward.Where (tsix => tss[tsix].IsNotch () && !tss[tsix].IsProcessed)];
            List<ToolingScope> ixnNotches = [.. ixnNotchesIndxs
               .Where (index => index >= 0 && index < tss.Count) // Ensure index is valid
               .Select (index => tss[index])];
            if (ixnNotches.Count > 0) {

               // Split the Notch(es) that intersect the dead band area. This is a mandatory one and does
               // not adhere to the min notch split options. Splitting shall be called only after possible resizing
               // of CutScope
               bool splitToolScopes = false;
               if (mpc.mGC.Heads == MCSettings.EHeads.Both) {
                  if (passType == PassType.CutScope)
                     (mpc.ToolingScopes, splitToolScopes) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes, currDBScope.XMin, mpc.Bound,
                              mpc.mGC, thresholdLength: 100, splitNotches: true, splitNonNotches: false);
                  else
                     (mpc.ToolingScopes, splitToolScopes) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes, cs.StartX, mpc.Bound,
                              mpc.mGC, thresholdLength: 100, splitNotches: true, splitNonNotches: false);
               } else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right) {
                  (mpc.ToolingScopes, splitToolScopes) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes,
                     startXPos + ((maxScopeLength - mpc.DeadBandWidth) / 2.0), mpc.Bound,
                              mpc.mGC, thresholdLength: 100, splitNotches: true, splitNonNotches: false);
               }

               // After splitting the notch into two, Sort and gether tool scopes that are outside the dead band
               if (splitToolScopes) {
                  CutScope.SortAndReIndex (ref mpc, mpc.mIsLeftToRight);
                  cs.CategorizeToolings ();
                  cs.AssessDeadBandFeatIndices (ref mpc);
                  tss = mpc.ToolingScopes;
                  featsWithIn = GetUncutToolingScopesWithin (cs, tss);
               }

               splitToolScopes = false;
               if (mpc.mGC.Heads == MCSettings.EHeads.Both) {
                  if (passType == PassType.CutScope)
                     (mpc.ToolingScopes, splitToolScopes) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes, currDBScope.XMax, mpc.Bound,
                              mpc.mGC, thresholdLength: 100, splitNotches: true, splitNonNotches: false);
                  else
                     (mpc.ToolingScopes, splitToolScopes) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes, cs.EndX, mpc.Bound,
                                 mpc.mGC, thresholdLength: 100, splitNotches: true, splitNonNotches: false);
               }

               // After splitting the notch into two, Sort and gether tool scopes that are outside the dead band
               if (splitToolScopes) {
                  CutScope.SortAndReIndex (ref mpc, mpc.mIsLeftToRight);
                  cs.CategorizeToolings ();
                  cs.AssessDeadBandFeatIndices (ref mpc);
                  tss = mpc.ToolingScopes;
                  featsWithIn = GetUncutToolingScopesWithin (cs, tss);
               }
               splitToolScopes = false;
            }

            // Order the notches in descenind order of StartX
            // Order by decending StartX keeps the highest StartX at the 0th element. 
            if (mpc.mIsLeftToRight)
               ixnNotches = [.. ixnNotches.OrderBy (tl => tl.StartX)];
            else
               ixnNotches = [.. ixnNotches.OrderByDescending (tl => tl.StartX)];

            // If the nearing notch bounds are within the maximum cutting scope from current start X
            // that notch is added within the feats within list.
            for (int kk = 0; kk < ixnNotches.Count; kk++) {
               var bound = Utils.GetToolingSegmentsBounds (ixnNotches[kk].Segs, mpc.Model.Bound);
               double dist = 0;
               if (mpc.mIsLeftToRight) {
                  if (mpc.mGC.Heads == MCSettings.EHeads.Both)
                     dist = bound.XMax - startXPos;
                  else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right)
                     dist = endXPos - startXPos;
                  if (dist < 0) throw new Exception ("Left to right pass is not proper in multipass notch");
               } else {
                  dist = startXPos - bound.XMin;
                  if (dist < 0) throw new Exception ("right to left pass is not proper in multipass notch");
               }
               if ((dist.LTEQ (maxScopeLength))) {
                  // The notch extents are with in the current scope. Even if the startXPos 
                  // changes in the optim iteration (here), this notch can fully be machined. 
                  // Add this notch in the featsWithIn list, mark it processed and remove it 
                  // from ixnNotches list
                  featsWithIn.Add (ixnNotches[kk]);
                  ixnNotches.RemoveAt (kk);
                  kk--;
               }
            }

            // If the current end position X of the cutscope is very close to the
            // nearing notch's start or end, the end position of current scope is set to 
            // the start or end of the notch.
            if (ixnNotches.Count > 0) {
               var notchTSegs = ixnNotches[0].Tooling.Segs.ToList ();
               double toolingLength = notchTSegs.Sum (t => t.Curve.Length);
               var (notchXPt, param, index, ixn) = Utils.GetPointParamsAtXVal (notchTSegs, endXPos);
               if (ixn) {
                  var (len, idx) = Geom.GetLengthAtPoint (notchTSegs, notchXPt);
                  if (idx == -1) throw new Exception ("GetQuasiOptimalCutScopes: Notch Ixn point returns -1 as index");

                  // If the notch ixn is close to the end of the notch, recompute points at
                  // extreme positions of the notch where the point will not be treated as
                  // "almost the extreme point". If its the end of the notch, toolingLength - len < minThreshold
                  // so add minThreshold with (toolingLength - len). for the starting extreme, 
                  // len < minThreshold, so add minThreshold with len. 
                  // Recomputing the point on the notch is done to stay relevant w.r.t max scope while
                  // going very near the notch ends.
                  Point3 notchXPt2;
                  if ((Math.Abs (toolingLength - len).LTEQ (minimumThresholdNotchLength))) {
                     (notchXPt2, index) = Geom.GetPointAtLength (notchTSegs, toolingLength - (len + minimumThresholdNotchLength));
                     endXPos = notchXPt2.X;
                     endXPosChanged = true;
                  } else if ((len.LTEQ (minimumThresholdNotchLength))) {
                     (notchXPt2, index) = Geom.GetPointAtLength (notchTSegs, (len + minimumThresholdNotchLength));
                     endXPos = notchXPt2.X;
                     endXPosChanged = true;
                  }
               }
            }

            // Terminating criteria
            if (ixnHolesIdxs.Count == 0 && ixnNotchesIndxs.Count == 0) {
               // Set the startX as the first feature's StartX from MaximumWorkpieceLength ( The biggest StartX )
               tss = mpc.ToolingScopes;
               featsWithIn = GetUncutToolingScopesWithin (cs, tss);
               uptoIndicesProcessed += featsWithIn.Count;
               break;
            }
            bool toolScopesSplit = false;
            bool split2 = false;
            bool split1 = false;

            // If minimizeNotchSplits is requested, and if largest of notches' startX is
            // greater than the "endXPos", make that startX as the current "endXPos".
            if (!overrideMinNotchCutOption && minimizeNotchSplits && ixnNotches.Count > 0
               && (((ixnNotches[0].EndX.SLT (endXPos) && !mpc.mIsLeftToRight) ||
               ((ixnNotches[0].EndX.SGT (endXPos) && mpc.mIsLeftToRight))))) {
               if ((Math.Abs (ixnNotches[0].StartX - ixnNotches[0].EndX).SGT (maxScopeLength) && mpc.mGC.Heads == MCSettings.EHeads.Both) ||
                  (Math.Abs (ixnNotches[0].StartX - ixnNotches[0].EndX).SGT ((maxScopeLength - mpc.DeadBandWidth) / 2.0) &&
                  (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right))) {
                  (mpc.ToolingScopes, split2) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes, endXPos, mpc.Bound,
                     mpc.mGC, thresholdLength: maxScopeLength, splitNotches: true, splitNonNotches: false); // TODO non notches was true, I nmade it false

                  // After splitting the notch into two, Sort and gether tool scopes that are outside the dead band
                  if (split2) {
                     CutScope.SortAndReIndex (ref mpc, mpc.mIsLeftToRight);
                     if (mpc.mGC.Heads == MCSettings.EHeads.Both)
                        cs.AssessDeadBandFeatIndices (ref mpc);
                     tss = mpc.ToolingScopes;
                  }
               } else {
                  endXPos = ixnNotches[0].StartX;
                  if ((endXPos - startXPos).EQ (0)) {
                     bool exists = singularEndXPositions.Any (v => v.EQ (endXPos));
                     if (exists) { // Go for splitting the notch
                        if (mpc.mGC.Heads == MCSettings.EHeads.Both)
                           endXPos = startXPos + maxScopeLength;
                        else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right)
                           endXPos = startXPos + ((maxScopeLength - mpc.DeadBandWidth) / 2.0);
                        overrideMinNotchCutOption = true;
                     } else {
                        singularEndXPositions.Add (endXPos);
                        if (mpc.mGC.Heads == MCSettings.EHeads.Both)
                           endXPos = startXPos + maxScopeLength;  // TODO Check. Previously this was endXPos = maxScopeLength;
                        else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right)
                           endXPos = startXPos + ((maxScopeLength - mpc.DeadBandWidth) / 2.0);
                     }
                     endXPosChanged = true;
                  }
               }
            }
            if (endXPosChanged || toolScopesSplit) {
               if (Math.Abs (startXPos - endXPos).EQ (0)) throw new Exception ("GetQuasiOptimalCutScopes: Start and end position are the same");
               cs = new (startXPos, 0, endXPos, mpc.Model.Bound, maxScopeLength, mpc.DeadBandWidth, ref mpc, mpc.mIsLeftToRight);
               tss = mpc.ToolingScopes;
               featsWithIn = GetUncutToolingScopesWithin (cs, tss);
               currDBScope = cs.DeadBandScope;
            }
            if ((maximizeScopeLength || overrideMinNotchCutOption) && ixnHolesIdxs.Count == 0 && ixnNotches.Count > 0) {
               // In the case of maximizeScopeLength = true AND
               // notches still existing to cut. Perform the split here
               // If maximum scope length is to be maximized, the toolscopes of notches
               // have to be split. Unless, startXPos needs to be modified
               if (maximizeScopeLength && ixnNotchesIndxs.Count > 0) {
                  // splitNonNotches is set to true to throw exception
                  //toolScopes = [.. toolScopes.OrderByDescending (t => t.StartX)];
                  // After splitting the notch into two, Sort and gether tool scopes that are outside the dead band

                  (mpc.ToolingScopes, split1) = CutScope.SplitToolingScopesAtIxn (mpc.ToolingScopes, endXPos, mpc.Bound,
                     mpc.mGC, thresholdLength: -1, splitNotches: true, splitNonNotches: false);
                  if (split1) {
                     CutScope.SortAndReIndex (ref mpc, mpc.mIsLeftToRight);
                     cs.CategorizeToolings ();
                     cs.AssessDeadBandFeatIndices (ref mpc);
                     tss = mpc.ToolingScopes;
                  }
               }
            }
            if (split1 || split2) {
               cs.ToolingScopes = mpc.ToolingScopes;
               cs.CategorizeToolings ();
            }
         } while (true);

         // Get the list of tooling scopes which needs to be machined and set it to the CutScope
         List<int> tscWithinIdxs = [];
         if (mpc.mGC.Heads == MCSettings.EHeads.Both)
            tscWithinIdxs = [.. cs.mToolingScopesWithin.Where (tsix => tss[tsix].IsProcessed == false &&
            !cs.DeadBandScopeToolingIndices.Contains (tsix))];
         else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right)
            tscWithinIdxs = [.. cs.mToolingScopesWithin.Where (tsix => tss[tsix].IsProcessed == false)];

         if (tscWithinIdxs.Count > 0) {
            var toolingScopesToBeMachined = tscWithinIdxs
               .Where (index => index >= 0 && index < tss.Count) // Ensure index is valid
               .Select (index => tss[index])
               .ToList ();
            for (int nn = 0; nn < toolingScopesToBeMachined.Count; nn++) {
               if (toolingScopesToBeMachined[nn].Tooling.IsNotch ())
                  toolingScopesToBeMachined[nn].IsNotchSplitComplete = true;
            }
            cs.MachinableToolingScopes = toolingScopesToBeMachined;
            foreach (var tsc in cs.MachinableToolingScopes) {
               if (processedTSSKVPairs.ContainsKey (tsc))
                  throw new Exception ("Previously handled tool scope found again in dictionary");

               // Set IsProcessed to true on the actual instance of ToolingScope
               tsc.IsProcessed = true;

               // Mark the ToolingScope as processed in the dictionary
               processedTSSKVPairs.Add (tsc, true);
            }
            foreach (var tssc in cs.MachinableToolingScopes)
               if (!tssc.IsProcessed) throw new Exception ("One of the tooling scopes was not set as processed");
            cs.ToolingScopes = mpc.ToolingScopes;
            cs.CategorizeToolings ();

            // Add the cutscope
            if (cs.MachinableToolingScopes.Count > 0) resCSS.Add (cs);
         } else {
            cs.CategorizeToolings ();

            // No "within features" found. Move the EndXPos to the start of the intersection feature
            // that has the lowest startX for left to right pass
            List<int> ixnFeatIdxs = [];
            if (mpc.mGC.Heads == MCSettings.EHeads.Both)
               ixnFeatIdxs = [.. cs.mToolingScopesIntersectForward.Where (tsix => !tss[tsix].IsProcessed &&
               !cs.DeadBandScopeToolingIndices.Contains (tsix))];
            else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right)
               ixnFeatIdxs = [.. cs.mToolingScopesIntersectForward.Where (tsix => !tss[tsix].IsProcessed)];

            if (ixnFeatIdxs.Count > 0) {
               var ixnFeats = ixnFeatIdxs
                  .Where (index => index >= 0 && index < tss.Count) // Ensure index is valid
                  .Select (index => tss[index])
                  .ToList ();

               // Order the ixn holes in ascending order of StartX so that 0th element has
               // the least X value for left to right pass
               if (mpc.mIsLeftToRight)
                  ixnFeats = [.. ixnFeats.OrderBy (tl => tl.StartX)];
               else
                  ixnFeats = [.. ixnFeats.OrderByDescending (tl => tl.StartX)];

               // If there are no notches intersecting, the new startX will be the maximum StartX.
               if (cs.IsIntersectWithNotches)
                  throw new Exception ("Notch intersection still found.");
               if (ixnFeats.Count > 0) endXPos = ixnFeats[0].StartX;
            }
         }
         foreach (var toolScopeItem in processedTSSKVPairs) {
            ToolingScope currentToolScope = toolScopeItem.Key;

            // Find the ToolScope in mpc.toolscopes which equals the dictionary item
            ToolingScope foundToolScope = null;
            foreach (var mpcTs in mpc.ToolingScopes) {
               if (mpcTs == currentToolScope) {
                  foundToolScope = mpcTs;
                  break;
               }
            }
            if (foundToolScope != null) {
               // Check if the found ToolScope is marked as processed
               if (!foundToolScope.IsProcessed)
                  throw new Exception ($"Tooling scope for the current item is not marked for cut. Error in optimization detected.");
            } else
               throw new Exception ($"Tooling scope not found in mpc.toolscopes for the given dictionary key.");
         }
         //var dbb = cs.DeadBandScope;
         var forwardIxnHoles = cs.GetForwardIxnHoles ();
         var featsWithinByEndX = featsWithIn.OrderBy (t => t.EndX).ToList ();
         if (mpc.mGC.Heads == MCSettings.EHeads.Both) {
            if (passType == PassType.CutScope) {
               // The next pass is to move utp the deadband beginning and
               // the cutscope width is the width of dead band.
               var dbTScopesAscStartX = GetDeadBandToolingScopes (cs, tss);
               var dbTScopesAscEndX = dbTScopesAscStartX.OrderBy (t => t.EndX).ToList ();
               if (dbTScopesAscStartX.Count > 0) {
                  var stxToolScPos = dbTScopesAscStartX[0].StartX;
                  var endxToolScPos = dbTScopesAscEndX[^1].EndX;
                  startXPos += ((endxToolScPos - stxToolScPos) < cs.DeadBandWidth ? cs.DeadBandWidth : (endxToolScPos - stxToolScPos));
                  endXPos += ((endxToolScPos - stxToolScPos) < cs.DeadBandWidth ? cs.DeadBandWidth : (endxToolScPos - stxToolScPos));
               } else {
                  if (featsWithinByEndX.Count > 0) startXPos = featsWithinByEndX[^1].EndX;
                  else if (forwardIxnHoles.Count > 0)
                     startXPos = forwardIxnHoles.First ().StartX;
                  else
                     startXPos = cs.EndX;

                  endXPos = Math.Min (startXPos + maxScopeLength, mpc.XMax);
               }
               passType = PassType.DeadBand;
            } else {
               // The next pass is CutScope Max, which is to move to the work
               // to the max frame width.
               var dbTScopesAscStartX = GetDeadBandToolingScopes (cs, tss);
               var dbTScopesAscEndX = dbTScopesAscStartX.OrderBy (t => t.EndX).ToList ();
               if (dbTScopesAscStartX.Count > 0) {
                  var stxToolScPos = dbTScopesAscStartX[0].StartX;
                  var endxToolScPos = dbTScopesAscEndX[^1].EndX;
                  startXPos += ((endxToolScPos - stxToolScPos) < cs.DeadBandWidth ? cs.DeadBandWidth : (endxToolScPos - stxToolScPos));
                  endXPos += ((endxToolScPos - stxToolScPos) < cs.DeadBandWidth ? cs.DeadBandWidth : (endxToolScPos - stxToolScPos));
               } else {
                  if (forwardIxnHoles.Count > 0)
                     endXPos = forwardIxnHoles.First ().StartX;
                  else
                     startXPos = cs.EndX;
               }
               passType = PassType.CutScope;
               if (forwardIxnHoles.Count > 0)
                  endXPos = forwardIxnHoles.First ().StartX;
               else
                  endXPos = Math.Min (startXPos + maxScopeLength, mpc.XMax);
            }
         } else if (mpc.mGC.Heads == MCSettings.EHeads.Left || mpc.mGC.Heads == MCSettings.EHeads.Right) {
            if (featsWithinByEndX.Count > 0 && featsWithinByEndX[^1].EndX.SGT (startXPos + ((maxScopeLength - mpc.DeadBandWidth) / 2.0)))
               throw new Exception ("In GetQuasiOptimalCutScopes: For Left Head, the EndX of last Feature is more than stX+(msl-db)/2");
            startXPos = featsWithinByEndX[^1].EndX;
            endXPos = startXPos + ((maxScopeLength - mpc.DeadBandWidth) / 2.0);
         }

         offset = 0;
         count++;
         pass++;

         // Terminate the while loop if condition is met
         if (startXPos.GTEQ (mpc.Model.Bound.XMax)) break;
      }
      ;
      foreach (var toolScopeItem in processedTSSKVPairs) {
         ToolingScope currentToolScope = toolScopeItem.Key;

         // Find the ToolScope in mpc.toolscopes which equals the dictionary item
         ToolingScope foundToolScope = mpc.ToolingScopes.FirstOrDefault (ts => ts == currentToolScope);

         if (foundToolScope != null) {
            // Check if the found ToolScope is marked as processed
            if (!foundToolScope.IsProcessed) {
               throw new Exception ($"Tooling scope for the current item is not marked for cut. Error in optimization detected.");
            }
         } else {
            throw new Exception ($"Tooling scope not found in mpc.toolscopes for the given dictionary key.");
         }
      }
      return resCSS;
   }

   /// <summary>
   /// This method writes the G Code for the multipass case of sheet metal feeding in LCM. 
   /// The WriteTooling method should not be direcltly called if the machine is multipass 2H 
   /// </summary>
   public void GenerateGCode () {
      // Allocate for CutscopeTraces
      mGC.AllocateCutScopeTraces (MachinableCutScopes.Count);
      var mcCutScopes = MachinableCutScope.CreateMachinableCutScopes (MachinableCutScopes, mGC);
      var prevPartRatio = mGC.PartitionRatio;
      if (!mGC.OptimizePartition) mGC.PartitionRatio = 0.5;
      if (mGC.Heads == MCSettings.EHeads.Left || mGC.Heads == MCSettings.EHeads.Right) mGC.PartitionRatio = 1.0;
      mGC.BlockNumber = 0;
      //if (mGC.Heads == MCSettings.EHeads.Both) {
      mGC.GenerateGCode (GCodeGenerator.ToolHeadType.Master, mcCutScopes);
      mGC.GenerateGCode (GCodeGenerator.ToolHeadType.Slave, mcCutScopes);
      //}else if ( mGC.Heads == MCSettings.EHeads.Left)
      // mGC.GenerateGCode (GCodeGenerator.ToolHeadType.Master, mcCutScopes);
      //else if (mGC.Heads == MCSettings.EHeads.Right)
      // mGC.GenerateGCode (GCodeGenerator.ToolHeadType.Slave, mcCutScopes);
      mGC.BlockNumber = 0;
      CutScopeTraces = mGC.CutScopeTraces;
      mGC.PartitionRatio = prevPartRatio;
   }
   #endregion
}