using Flux.API;
namespace FChassis.GCodeGen;

using static FChassis.MCSettings;
using static FChassis.Utils;
using NotchAttribute = Tuple<
        Curve3, // Split curve, whose end point is the notch point
        Vector3, // Start Normal
        Vector3, // End Normal
        Vector3, // Outward Normal along flange
        Vector3, // Vector Outward to nearest boundary
        XForm4.EAxis, // Proximal boundary direction
        bool>;
using CutScopeToolingList = List<(List<Tooling> ToolingList, double XMin, double XMax)>;

#region Data structures and Enums used Notch Computation
/// <summary>
/// The NotchPointInfo structure holds a list of notch specific points list
/// against the index of the occuring in the List of Tooling Segments and
/// the percentage of the length (by prescription).
/// </summary>
public struct NotchPointInfo (int sgIndx, Point3 pt, double percent, string position) {
   public string mPosition = position;
   public int mSegIndex = sgIndx;
   public List<Point3> mPoints = [pt];
   public double mPercentage = percent;

   // Method for deep copy
   public NotchPointInfo DeepCopy () {
      // Create a new instance with the same values
      var copy = new NotchPointInfo {
         mSegIndex = this.mSegIndex,
         mPercentage = this.mPercentage,
         mPoints = new List<Point3> (this.mPoints) // Deep copy the list
      };
      return copy;
   }
}


/// <summary>
/// The following enums signify the various cutting or rapid positioning strokes used during notch cutting.
/// <list type="number">
/// <item>
///     <description>WireJointApproach: This approach is mandatory to start the non-edge notch tooling. 
///     The tool describes a set of cutting strokes as follows:</description>
///     <list type="bullet">
///         <item>Approach to the midpoint of the line segment from the notch point at 50% of the length to the 
///         part boundary (in the scrap side direction).</item>
///         <item>Cutting stroke from the above midpoint to the part boundary in the scrap side direction.</item>
///         <item>Rapid positioning to the midpoint.</item>
///         <item>Cutting stroke from the midpoint to the 50% point on the tooling segment.</item>
///     </list>
/// </item>
/// <item>
///     <description>ApproachOnReEntry: This approach involves moving from one end of the non-edge notch tooling
///     to the midpoint of the WireJointApproach midpoint and making a cutting stroke from that midpoint
///     to the 50% notch point on the tooling.</description>
/// </item>
/// <item>
///     <description>GambitMachiningAt50Reverse: This operation machines a distance of wire joint length 
///     from the 50% notch point in the reverse direction of the notch tooling.</description>
/// </item>
/// <item>
///     <description>GambitMachiningAt50Forward: This operation machines a distance of wire joint length 
///     from the 50% notch point in the forward direction of the notch tooling.</description>
/// </item>
/// <item>
///     <description>MachineToolingForward: This operation machines the notch tooling profile that occurs
///     on the Web, Bottom, or Top Flanges in the forward direction of the notch tooling.</description>
/// </item>
/// <item>
///     <description>MachineToolingReverse: This operation machines the notch tooling profile that occurs
///     on the Web, Bottom, or Top Flanges in the reverse direction of the notch tooling.</description>
/// </item>
/// <item>
///     <description>MachineFlexToolingReverse: This operation machines the notch tooling profile that occurs
///     on the Flex section in the reverse direction of the notch tooling.</description>
/// </item>
/// <item>
///     <description>MachineFlexToolingForward: This operation machines the notch tooling profile that occurs
///     on the Flex section in the forward direction of the notch tooling.</description>
/// </item>
/// </list>
/// </summary>
public enum NotchSectionType {
   /// <summary>
   /// This is mandatory to start the non-edge notch tooling. 
   /// The tool describes a set of the following cutting strokes
   /// -> Approach to the mid point of the line segment from notch point at 50% of the length to the 
   /// part boundary (in scrap side direction)
   /// -> Cutting stroke from the above mid point to the part boundary in the scrap side direction
   /// -> Rapid position to the mid point
   /// -> Cutting stroke from mid point to the 50% point on the tooling segment
   /// </summary>
   WireJointApproach,

   /// <summary>
   /// This is the approach from one end of the non-edge notch tooling
   /// to the mid point of the WireJointApproach mid point and a cutting stroke from that mid point
   /// to the 50% notch point on the tooling
   /// </summary>
   ApproachOnReEntry,

   /// <summary>
   /// This is to machine a distance of wire joint distance 
   /// from 50% notch point in the reverse order of the notch tooling
   /// </summary>
   GambitMachiningAt50Reverse,

   /// <summary>
   /// This is to machine a distance of wire joint distance 
   /// from 50% notch point in the forward order of the notch tooling
   /// </summary>
   GambitMachiningAt50Forward,

   /// <summary>
   /// This is to machine the notch tooling profile that occurs
   /// on Web or Bottom or Top Flanges in the forward direction of the notch tooling
   /// </summary>
   MachineToolingForward,

   /// <summary>
   /// This is to machine the notch tooling profile that occurs
   /// on Web or Bottom or Top Flanges in the reverse direction of the notch tooling
   /// </summary>
   MachineToolingReverse,

   /// <summary>
   /// This is to machine the notch tooling profile that occurs
   /// on the Flex section in the reverse direction of the notch tooling
   /// </summary>
   MachineFlexToolingReverse,

   /// <summary>
   /// This is to machine the notch tooling profile that occurs
   /// on the Flex section in the forward direction of the notch tooling
   /// </summary>
   MachineFlexToolingForward,

   /// <summary>
   /// This is to introduce a partly joined arrangement with a distance of 
   /// wire joint distance specified in the settings in the reverse direction 
   /// of the notch tooling
   /// </summary>
   WireJointTraceJumpReverse,

   /// <summary>
   /// This is to introduce a partly joined arrangement with a distance of 
   /// wire joint distance specified in the settings in the forward direction 
   /// of the notch tooling
   /// </summary>
   WireJointTraceJumpForward,

   /// <summary>
   /// This is a directive to move to the mid point of the intitial segment defined 
   /// in "WireJointApproach", to reposition the tooling head
   /// </summary>
   MoveToMidApproach
}

/// <summary>
/// The following structure holds notch specific ( prescribed by process team) points
/// along the notch. The integers are the indices of the list of tooling segments, whose 
/// end points are the notch specific points at which either the entry to the cutting 
/// profile happens or a wire joint is introduced, or an initial gambit action to move
/// in the forward or reverse direction of wire joint distance happens.
/// </summary>
public struct NotchSegmentIndices {
   public NotchSegmentIndices () { }
   public int segIndexAt25pc = -1, segIndexAt50pc = -1, segIndexAt75pc = -1,
      segIndexAtWJTPost25pc = -1, segIndexAtWJTPost50pc = -1, /*segIndexAtWJTPre50pc = -1,*/
      segIndexAtWJTPost75pc = -1, segIndexAtWJTPreApproach = -1, segIndexAtWJTApproach = -1, segIndexAtWJTPostApproach = -1;
   public List<Tuple<int, int, int, int>> flexIndices = [];
}

/// <summary>
/// The entire notch tooling is subdivided into multiple tooling blocks.
/// The following structure holds the sub section of the notch tooling
/// </summary>
public struct NotchSequenceSection {
   public NotchSequenceSection () { }
   public int mStartIndex = -1;
   public int mEndIndex = -1;
   public NotchSectionType mSectionType;
}
#endregion

/// <summary>
/// The class Notch holds all the sequences of actions from creating notch specific points
/// through writing the notch. The sequence is modularized as much as it is needed. Once the 
/// the final prescription is made from the process team, more optimizations will be done
/// </summary>
public class Notch {
   #region Enums
   enum IndexType {
      Max,
      Zero,
      PreApproach,
      PostApproach,
      Approach,
      At75,
      Post75,
      Post50,
      At50,
      Post25,
      At25,
      Flex2End,
      Flex2Start,
      Flex2AfterEnd,
      Flex2BeforeStart,
      Flex1AfterEnd,
      Flex1End,
      Flex1Start,
      Flex1BeforeStart,
      None
   }
   #endregion

   #region Constructor(s)
   public Notch (Tooling toolingItem, Bound3 bound, Bound3 fullPartBound, GCodeGenerator gcodeGen, Tooling prevToolingItem,
      ToolingSegment? prevToolingSegment, List<ToolingSegment> prevToolingSegs, bool firstTooling,
      EPlane prevPlaneType, /*double frameFeed,*/
      double xStart, double xPartition, double xEnd,
      double notchWireJointDistance, double notchApproachLength, double minNotchThresholdLength, double[] percentlength,
      double totalPrevCutToolingsLength, double totalToolingsCutLength, double curveLeastLength = 0.5) {
      if (!toolingItem.IsNotch ()) throw new Exception ("Can not create a notch from a non-notch feature");
      mToolingItem = toolingItem;
      mBound = bound;
      mFullPartBound = fullPartBound;
      mNotchApproachLength = notchApproachLength;
      mNotchWireJointDistance = notchWireJointDistance;
      mCurveLeastLength = curveLeastLength;
      mPercentLength = percentlength;
      mGCodeGen = gcodeGen;
      mPrevPlane = prevPlaneType;
      mSegments.AddRange ([.. mToolingItem.Segs]);
      mTotalToolingsCutLength = totalToolingsCutLength;
      mCutLengthTillPrevTooling = totalPrevCutToolingsLength;
      mSegments = [.. mToolingItem.Segs];
      mXStart = xStart; mXPartition = xPartition; mXEnd = xEnd;
      MinNotchLengthThreshold = minNotchThresholdLength;
      EdgeNotch = false;
      PreviousTooling = prevToolingItem;
      PreviousToolingSegment = prevToolingSegment;
      mFirstTooling = firstTooling;
      mPrevToolingSegments = prevToolingSegs;
      if (Notch.IsEdgeNotch (mGCodeGen.Process.Workpiece.Bound, toolingItem, percentlength, notchApproachLength, curveLeastLength))
         EdgeNotch = true;
      else {
         if (mToolingItem.FeatType.Contains ("Split"))
            mSplit = true;
         mToolingPerimeter = 0;
         mSegments.Sum (t => mToolingPerimeter += t.Curve.Length);
         mShortPerimeterNotch = false;
         if (mToolingPerimeter < MinNotchLengthThreshold || (mSegments.Last ().Curve.End.DistTo (mSegments.First ().Curve.Start).LTEQ (MinNotchLengthThreshold)))
            mShortPerimeterNotch = true;
         Utils.FixSanityOfToolingSegments (ref mSegments);
         Utils.MarkfeasibleSegments (ref mSegments);
         ComputeNotchParameters ();
      }
   }
   #endregion

   #region Caching tool position
   ToolingSegment mExitTooling;
   Point3 mRecentToolPosition;
   double mXStart, mXPartition, mXEnd;
   public ToolingSegment Exit { get => mExitTooling; set => mExitTooling = value; }
   #endregion

   #region External references
   GCodeGenerator mGCodeGen;
   static Bound3 mBound;
   static Bound3 mFullPartBound;
   Tooling mToolingItem;
   EPlane mPrevPlane = EPlane.None;
   bool mFirstTooling = false;
   List<ToolingSegment> mPrevToolingSegments;
   public EPlane PrevPlane { get => mPrevPlane; set => mPrevPlane = value; }
   public Tooling PreviousTooling { get; set; }
   public ToolingSegment? PreviousToolingSegment { get; set; }
   #endregion

   #region Tunable Parameters / Setting Prescriptions
   double mCurveLeastLength;
   // As desired by the machine team
   double[] mPercentLength = [0.25, 0.5, 0.75];
   double mNotchWireJointDistance = 2.0;
   public double NotchWireJointDistance { get => mNotchWireJointDistance; set => mNotchWireJointDistance = value; }
   double mNotchApproachLength = 5.0;
   public double NotchApproachLength { get => mNotchApproachLength; set => mNotchApproachLength = value; }
   bool mSplit = false;
   #endregion

   #region Intermediate Data Structures
   NotchSegmentIndices mNotchIndices;
   List<NotchAttribute> mNotchAttrs = [];
   List<NotchSequenceSection> mNotchSequences = [];
   Point3?[] mWireJointPts = [null, null, null, null];
   List<Point3> mFlexWireJointPts = [];
   double mBlockCutLength = 0;
   double mTotalToolingsCutLength = 0;
   double mCutLengthTillPrevTooling = 0;

   // Find the flex segment indices
   List<Tuple<int, int>> mFlexIndices = [];

   // The indices of segs on whose segment the 25%, 50% and 75% of the length occurs
   int?[] mSegIndices = [null, null, null]; int mSegsCount = 0;

   // The point on the segment which shall participate in notch tooling
   Point3?[] mNotchPoints = new Point3?[3];
   List<NotchPointInfo> mNotchPointsInfo = [];
   int mApproachIndex = 1;
   double minThresholdSegLen = 15.0;
   bool mShortPerimeterNotch = false;
   List<int> mInvalidIndices = [];
   double mToolingPerimeter = 0;
   #endregion

   #region Public Properties
   public List<NotchAttribute> NotchAttributes { get => mNotchAttrs; }
   List<ToolingSegment> mSegments = [];
   public bool EdgeNotch { get; set; }
   public double MinNotchLengthThreshold { get; set; }
   #endregion

   #region Notch parameters computing methods
   /// <summary>
   /// This method recomputes the 25%, 50%, and 75% notch points if the previous 
   /// computation finds the locations existing within the flexes. A heuristic is used, where
   /// 25% and 75% notch points are recomputed only if the distance from the notch point to the start (for the 25%-th
   /// notch point) or to the end (for the 75%-th notch point) is more than 200 units (mm).
   /// If the length is less than 200 units, the corresponding notch point is excluded by setting its index to -1.
   /// </summary>
   /// <param name="segs">The list of tooling segments.</param>
   /// <param name="notchPtCountIndex">Index indicating whether it is the 25%, 50%, or 75% notch point (0, 1, 2, respectively).
   /// </param>
   /// <param name="notchPt">The given notch point.</param>
   /// <param name="thresholdNotchLenForNotchApproach">The length threshold for deciding if the notch point needs to be recomputed. 
   /// The value used is 200 units.</param>
   /// <param name="segIndices">The indices of the 25%, 50%, or 75% notch point occurrences on the list of tooling segments.</param>
   /// <param name="notchPoints">The array of notch points at 25%, 50%, and 75% lengths.</param>

   void RecomputeNotchPointsWithinFlex (List<ToolingSegment> segs, int notchPtCountIndex, Point3 notchPt,
      double thresholdNotchLenForNotchApproach, ref int?[] segIndices, ref Point3?[] notchPoints) {
      double? lenToToolingEnd = null;
      if (notchPtCountIndex == 2) // Notch point at 75% of the tooling length is within a flex section
         lenToToolingEnd = Utils.GetLengthFromEndToolingToPosition (segs, notchPt);
      else if (notchPtCountIndex == 0) // Notch point at 25% of the tooling length is within a flex section
         lenToToolingEnd = Utils.GetLengthFromStartToolingToPosition (segs, notchPt);
      if (lenToToolingEnd != null) {
         if (lenToToolingEnd.Value > thresholdNotchLenForNotchApproach) {
            // Add new notch point at approx mid
            double percent;
            if (notchPtCountIndex == 2) percent = mPercentLength[2] = 0.875;
            else percent = mPercentLength[0] = 0.125;
            var (sgIdx, npt) = Utils.GetNotchPointsOccuranceParams (segs, percent, mCurveLeastLength);
            segIndices[notchPtCountIndex] = sgIdx; notchPoints[notchPtCountIndex] = npt;
         } else
            // Mark this notch as false or delete
            segIndices[notchPtCountIndex] = null; notchPoints[notchPtCountIndex] = null;
      } else {
         // Handle 50% pc case here
         var (sgIdx, npt) = Utils.GetNotchPointsOccuranceParams (segs, 0.4, mCurveLeastLength);
         mPercentLength[1] = 0.4;
         segIndices[notchPtCountIndex] = sgIdx; notchPoints[notchPtCountIndex] = npt;
      }
   }

   /// <summary>
   /// This method recomputes the notch points for length ratios of 25% and 75% if
   /// these points exist within the flex section of the tooling. The following actions will be taken:
   /// <list type="number">
   /// <item>
   ///     <description>If the notch points are within "minThresholdLenFromNPToFlexPt" units 
   ///     from the nearest flex and outside the flex, these notch points are removed.</description>
   /// </item>
   /// <item>
   ///     <description>If the notch points occur outside the flex section and if the distance from
   ///     the extreme section (start for 25% and end for 75%) is more than "thresholdNotchLenForNotchApproach",
   ///     the 25%-th notch point is recomputed at 0.125-th length and the 75%-th notch point is recomputed
   ///     at 0.875-th length of the notch tooling.</description>
   /// </item>
   /// <item>
   ///     <description>If the notch point at 50%-th length lies within the flex, another notch point
   ///     is computed at 40% of the total tooling length from the start.</description>
   /// </item>
   /// </list>
   /// </summary>
   /// <param name="segs">The input tooling segments list, which should be further segmented.</param>
   /// <param name="flexIndices">The list of tuples containing the start and end indices of the flex.</param>
   /// <param name="notchPoints">The existing notch points at 25%, 50%, and 75% of the total tooling length.</param>
   /// <param name="segIndices">The array of indices of the list of tooling segments where the 
   /// notch points occur.</param>
   /// <param name="mPercentLength">The input specification of the percentage length (25%, 50%, 75%).</param>
   /// <param name="minThresholdLenFromNPToFlexPt">The minimum length of the tooling segment below which
   /// a notch point is considered invalid and removed if it occurs.</param>
   /// <param name="thresholdNotchLenForNotchApproach">The threshold length of the tooling segments that
   /// allows for recomputing notch points at 25% and 75%. If the length from this notch point to the nearest
   /// end is less than this threshold, it is not required to create a new notch point as removing the scrap
   /// part is manageable.</param>
   void RecomputeNotchPointsAgainstFlexNotch (List<ToolingSegment> segs, List<Tuple<int, int>> flexIndices,
      ref Point3?[] notchPoints, ref int?[] segIndices, double[] mPercentLength, double minThresholdLenFromNPToFlexPt,
      double thresholdNotchLenForNotchApproach) {
      int index = 0;
      while (index < mPercentLength.Length) {
         if (notchPoints[index] == null) { index++; continue; }
         var (IsWithinAnyFlex, StartIndex, EndIndex) = IsPointWithinFlex (flexIndices, segs, notchPoints[index].Value, minThresholdLenFromNPToFlexPt);

         if (IsWithinAnyFlex)
            RecomputeNotchPointsWithinFlex (segs, index, notchPoints[index].Value, thresholdNotchLenForNotchApproach, ref segIndices, ref notchPoints);
         else if (segIndices[index] != -1) {
            if (StartIndex != -1) {
               var fromNPTToFlexStart = Utils.GetLengthBetweenTooling (segs, notchPoints[index].Value, segs[StartIndex].Curve.Start);
               if (fromNPTToFlexStart < 10.0) {
                  var (newNPTAtIndex, idx) = Geom.GetToolingPointAndIndexAtLength (segs, segIndices[index].Value, 11.0/*length offset for approach pt*/,
                        reverseTrace: true);
                  segIndices[index] = idx; notchPoints[index] = newNPTAtIndex;
               }
            }
            if (EndIndex != -1) {
               var fromNPTToFlexEnd = Utils.GetLengthBetweenTooling (segs, notchPoints[index].Value, segs[EndIndex].Curve.End);
               if (fromNPTToFlexEnd < 10.0) {
                  var (newNPTAtIndex, idx) = Geom.GetToolingPointAndIndexAtLength (segs, segIndices[index].Value, 11.0/*length offset for approach pt*/,
                        reverseTrace: false);
                  segIndices[index] = idx; notchPoints[index] = newNPTAtIndex;
               }
            }
         }
         index++;
      }
   }

   /// <summary>
   /// The following method computes the wire joint positions
   /// at 25%, 50% and 75% of the lengths and splits the tooling segments in such a way that 
   /// the end point of the segment is the notch or wire joint point
   /// </summary>
   /// <param name="segs">The input tooling segments list</param>
   /// <param name="notchPoints">The input prescribed notch points</param>
   /// <param name="notchPointsInfo">A data structure that stores one notch or wire joint
   /// point per unique index of the list of tooling items after splitting. The end point is
   /// the notch or wire joint distance point</param>
   /// <param name="atLength">A variable that holds the wire joint length</param>
   public void ComputeWireJointPositionsOnFlanges (List<ToolingSegment> segs, Point3?[] notchPoints,
      ref List<NotchPointInfo> notchPointsInfo, double atLength, int approachSegmentIndex) {
      // Split the tooling segments at wire joint length from notch points 
      mWireJointPts = [null, null, null, null];
      int ptCount = 0;
      for (int ii = 0; ii < notchPoints.Length; ii++) {
         string pos = ii switch {
            0 => "@25",
            1 => "@50",
            2 => "@75",
            _ => ""
         };
         var segIndex = notchPointsInfo.Where (n => n.mPosition == pos).ToList ()[0].mSegIndex;
         if (notchPoints[ii] == null || segIndex == -1) { ptCount++; continue; }

         // Find the index of the occurrence of the point where Curve3.End matches the given point
         var notchPointIndex = segs.FindIndex (s => s.Curve.End.DistTo (notchPoints[ii].Value).EQ (0));

         // If the wire Joint Distance is close to 0.0, this should not affect
         // the parameters of the notch at 50% of the length (pre, @50 and post)
         if (atLength < 0.5 && ii == approachSegmentIndex) atLength = 2.0;
         (mWireJointPts[ptCount], var segIndexToSplit) = Geom.GetToolingPointAndIndexAtLength (segs, notchPointIndex,
            atLength/*, segs[notchPointIndex].Item2.Normalized ()*/);
         var splitToolSegs = Utils.SplitToolingSegmentsAtPoint (segs, segIndexToSplit, mWireJointPts[ptCount].Value,
            segs[notchPointIndex].Vec0.Normalized (), tolerance: mSplit == true ? 1e-4 : 1e-6);

         // Make the NotchPointsInfo to contain unique entries by having unique index of the
         // tooling segments list per point (notch or wire joint)
         MergeSegments (ref splitToolSegs, ref segs, segIndexToSplit);

         // Update the notchPointsINfo
         pos = "";
         double percent = 0;
         if (ii == approachSegmentIndex) {
            switch (ii) {
               case 0: pos = "@2501"; percent = 0.2501; break;
               case 1: pos = "@5001"; percent = 0.5001; break;
               case 2: pos = "@7501"; percent = 0.7501; break;
               default: break;
            }
         } else {
            switch (ii) {
               case 0: pos = "@2501"; percent = 0.2501; break;
               case 1: pos = "@5001"; percent = 0.5001; break;
               case 2: pos = "@7501"; percent = 0.7501; break;
               default: break;
            }
         }
         Utils.UpdateNotchPointsInfo (segs, ref notchPointsInfo, pos, percent, splitToolSegs[0].Curve.End);
         Utils.CheckSanityNotchPointsInfo (segs, notchPointsInfo);

         // Atapproach index...
         if (ii == approachSegmentIndex) {
            ptCount++;
            notchPointIndex = segs.FindIndex (s => s.Curve.End.DistTo (notchPoints[ii].Value).EQ (0));
            (mWireJointPts[ptCount], segIndexToSplit) = Geom.GetToolingPointAndIndexAtLength (segs, notchPointIndex, atLength,
               reverseTrace: true);
            splitToolSegs = Utils.SplitToolingSegmentsAtPoint (segs, segIndexToSplit, mWireJointPts[ptCount].Value,
               segs[notchPointIndex].Vec0.Normalized ());
            MergeSegments (ref splitToolSegs, ref segs, segIndexToSplit);
            switch (ii) {
               case 0: pos = "@2499"; percent = 0.2499; break;
               case 1: pos = "@4999"; percent = 0.4999; break;
               case 2: pos = "@7499"; percent = 0.7499; break;
               default: break;
            }
            Utils.UpdateNotchPointsInfo (segs, ref notchPointsInfo, pos, percent, splitToolSegs[0].Curve.End);
            Utils.CheckSanityNotchPointsInfo (segs, notchPointsInfo);
            (mWireJointPts[ptCount], mWireJointPts[ptCount - 1]) = (mWireJointPts[ptCount - 1], mWireJointPts[ptCount]);
         }
         ptCount++;
      }
   }

   /// <summary>
   /// The following method computes the indices of all the notch points and wire joint points that are occurring
   /// on the list of segmented tooling segments for each of the above points. 
   /// Note: The notch point or the wire joint lengthed points occur as the end point of the tooling segment
   /// </summary>
   /// <param name="segs">The segmented tooling segments list</param>
   /// <param name="notchPoints">The points at the prescribed lengths of tooling (25%, 50% and 75%)</param>
   /// <param name="wjtPoints">The points on the list of tooling segments where wire joint jump trace is desired</param>
   /// <param name="flexWjtPoints">The start and the end points of the flex tooling which is also treated 
   /// as wire joint jump trace</param>
   public void ComputeNotchToolingIndices (List<ToolingSegment> segs, Point3?[] notchPoints,
      Point3?[] wjtPoints, List<Point3> flexWjtPoints) {
      mNotchIndices = new NotchSegmentIndices ();
      int ptCount = 0;
      for (int ii = 0; ii < notchPoints.Length; ii++) {
         if (notchPoints[ii] == null) {
            ptCount++;
            continue;
         }
         int notchPointIndexPostSplit = -1;
         notchPointIndexPostSplit = segs
             .Select ((segment, idx) => new { segment, idx })
             .Where (x => x.segment.Curve.End.DistTo (notchPoints[ii].Value).EQ (0, mSplit ? 1e-4 : 1e-6))
             .Select (x => x.idx)
             .FirstOrDefault ();
         int wjtPointIndexPostSplit = -1;
         if (wjtPoints[ptCount] != null)
            wjtPointIndexPostSplit = segs
                .Select ((segment, idx) => new { segment, idx })
                .Where (x => x.segment.Curve.End.DistTo (wjtPoints[ptCount].Value).EQ (0, mSplit ? 1e-4 : 1e-6))
                .Select (x => x.idx)
                .FirstOrDefault ();
         if (ii == mApproachIndex) {
            mNotchIndices.segIndexAtWJTApproach = notchPointIndexPostSplit;
            mNotchIndices.segIndexAtWJTPreApproach = wjtPointIndexPostSplit;
            if (ii == 0 && ii != mApproachIndex && mNotchPointsInfo.Where (np => np.mPosition == "@25").FirstOrDefault ().mSegIndex != -1) {
               mNotchIndices.segIndexAt25pc = notchPointIndexPostSplit;
               mNotchIndices.segIndexAtWJTPost25pc = wjtPointIndexPostSplit;
            } else if (ii == 1 && ii != mApproachIndex && mNotchPointsInfo.Where (np => np.mPosition == "@50").FirstOrDefault ().mSegIndex != -1) {
               mNotchIndices.segIndexAt50pc = notchPointIndexPostSplit;
               //mNotchIndices.segIndexAtWJTPre50pc = wjtPointIndexPostSplit;
               mNotchIndices.segIndexAtWJTPost50pc = wjtPointIndexPostSplit;
            } else if (ii == 2 && ii != mApproachIndex && mNotchPointsInfo.Where (np => np.mPosition == "@75").FirstOrDefault ().mSegIndex != -1) {
               mNotchIndices.segIndexAt75pc = notchPointIndexPostSplit;
               mNotchIndices.segIndexAtWJTPost75pc = wjtPointIndexPostSplit;
            }
         } else {
            if (ii == 0 && ii != mApproachIndex && mNotchPointsInfo.Where (np => np.mPosition == "@25").FirstOrDefault ().mSegIndex != -1) {
               mNotchIndices.segIndexAt25pc = notchPointIndexPostSplit;
               mNotchIndices.segIndexAtWJTPost25pc = wjtPointIndexPostSplit;
            } else if (ii == 1 && ii != mApproachIndex && mNotchPointsInfo.Where (np => np.mPosition == "@50").FirstOrDefault ().mSegIndex != -1) {
               mNotchIndices.segIndexAt50pc = notchPointIndexPostSplit;
               //mNotchIndices.segIndexAtWJTPre50pc = wjtPointIndexPostSplit;
               mNotchIndices.segIndexAtWJTPost50pc = wjtPointIndexPostSplit;
            } else if (ii == 2 && ii != mApproachIndex && mNotchPointsInfo.Where (np => np.mPosition == "@75").FirstOrDefault ().mSegIndex != -1) {
               mNotchIndices.segIndexAt75pc = notchPointIndexPostSplit;
               mNotchIndices.segIndexAtWJTPost75pc = wjtPointIndexPostSplit;
            }
         }
         if (ii == mApproachIndex) {
            ptCount++;
            if (wjtPoints[ptCount] == null) continue;
            wjtPointIndexPostSplit = segs
                .Select ((segment, idx) => new { segment, idx })
                .Where (x => x.segment.Curve.End.DistTo (wjtPoints[ptCount].Value).EQ (0, mSplit ? 1e-4 : 1e-6))
                .Select (x => x.idx)
                .FirstOrDefault ();
            mNotchIndices.segIndexAtWJTPostApproach = wjtPointIndexPostSplit;
         }
         ptCount++;
      }
      for (int ii = 0; ii < flexWjtPoints.Count; ii += 4) {
         var preSegFlexStPointIndex = segs
             .Select ((segment, idx) => new { segment, idx })
             .Where (x => x.segment.Curve.End.DistTo (flexWjtPoints[ii]).EQ (0, mSplit ? 1e-4 : 1e-6))
             .Select (x => x.idx)
             .FirstOrDefault ();
         var flexStPointIndex = segs
             .Select ((segment, idx) => new { segment, idx })
             .Where (x => x.segment.Curve.End.DistTo (flexWjtPoints[ii + 1]).EQ (0, mSplit ? 1e-4 : 1e-6))
             .Select (x => x.idx)
             .FirstOrDefault ();
         var flexEndPointIndex = segs
             .Select ((segment, idx) => new { segment, idx })
             .Where (x => x.segment.Curve.End.DistTo (flexWjtPoints[ii + 2]).EQ (0, mSplit ? 1e-4 : 1e-6))
             .Select (x => x.idx)
             .FirstOrDefault ();
         var postFlexEndPointIndex = segs
             .Select ((segment, idx) => new { segment, idx })
             .Where (x => x.segment.Curve.End.DistTo (flexWjtPoints[ii + 3]).EQ (0, mSplit ? 1e-4 : 1e-6))
             .Select (x => x.idx)
             .FirstOrDefault ();
         Tuple<int, int, int, int> flexIndices = new (preSegFlexStPointIndex, flexStPointIndex, flexEndPointIndex, postFlexEndPointIndex);
         mNotchIndices.flexIndices.Add (flexIndices);
      }

      // Set wirejoint indices to -1 if they exist in between the flex indices
      for (int ii = 0; ii < mNotchIndices.flexIndices.Count; ii++) {
         List<int> flexIndices = [mNotchIndices.flexIndices[ii].Item1, mNotchIndices.flexIndices[ii].Item2, mNotchIndices.flexIndices[ii].Item3, mNotchIndices.flexIndices[ii].Item4];
         flexIndices.Sort ();
         if (mNotchIndices.segIndexAt25pc >= flexIndices[0] && mNotchIndices.segIndexAt25pc <= flexIndices[3]) {
            mNotchIndices.segIndexAt25pc = -1;
            mNotchIndices.segIndexAtWJTPost25pc = -1;
         }
         if (mNotchIndices.segIndexAt75pc >= flexIndices[0] && mNotchIndices.segIndexAt75pc <= flexIndices[3]) {
            mNotchIndices.segIndexAt75pc = -1;
            mNotchIndices.segIndexAtWJTPost75pc = -1;
         }
      }
   }

   /// <summary>
   /// The following method creates a Notch Sequence Section with user inputs
   /// </summary>
   /// <param name="startIndex">The start index of the list of tooling segments</param>
   /// <param name="endIndex">The end index of the list of tooling segments</param>
   /// <param name="notchSectionType">The type of tooling block that is desired</param>
   /// <returns></returns>
   /// <exception cref="Exception">The method expects that the start and end end indices are in non 
   /// decreasing order. Unless, this throws an exception. In the case of reversed tooling segment
   /// too, the start and end index should be prescribed in the same non-decreasing order</exception>
   NotchSequenceSection CreateNotchSequence (int startIndex, int endIndex, NotchSectionType notchSectionType) {
      if (notchSectionType == NotchSectionType.MachineToolingForward) {
         if (startIndex > endIndex) throw new Exception ("StartIndex < endIndex for forward machiniing");
      }
      var nsq = new NotchSequenceSection () {
         mStartIndex = startIndex,
         mEndIndex = endIndex,
         mSectionType = notchSectionType
      };
      return nsq;
   }

   /// <summary>
   /// Creates notch sequences in the reverse direction of the list of tooling,
   /// taking into account the notch and wirejoint points and flex sections 
   /// for various possible occurrences of notch points and flex sections.
   /// </summary>
   /// <param name="mNotchIndices">The indices of the notch and wirejoint points.</param>
   /// <returns>A list of assembled notch sequence sections to be used for generating G Code.</returns>
   /// <exception cref="Exception">Thrown when the notch sequences do not follow the correct directional
   /// or sequential order. If the order is incorrect, an exception will be thrown.</exception>
   List<NotchSequenceSection> CreateNotchReverseSequences () {
      bool appAt25 = false, appAt50 = false, appAt75 = false;
      if (mNotchIndices.segIndexAt25pc == mNotchIndices.segIndexAtWJTApproach) appAt25 = true;
      else if (mNotchIndices.segIndexAt50pc == mNotchIndices.segIndexAtWJTApproach) appAt50 = true;
      else if (mNotchIndices.segIndexAt75pc == mNotchIndices.segIndexAtWJTApproach) appAt75 = true;
      List<(int Index, IndexType Type)> notchIndexSequence = [
    (0, IndexType.Zero),
    (mNotchIndices.segIndexAtWJTPreApproach, IndexType.PreApproach),
    (mNotchIndices.segIndexAtWJTApproach, IndexType.Approach),
    (mNotchIndices.segIndexAtWJTPostApproach, IndexType.PostApproach),
    (mSegments.Count-1, IndexType.Max)
      ];
      if (!appAt25) {
         if (mNotchIndices.segIndexAt25pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAt25pc, IndexType.At25));
         if (mNotchIndices.segIndexAtWJTPost25pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAtWJTPost25pc, IndexType.Post25));
      }
      if (!appAt50) {
         if (mNotchIndices.segIndexAt50pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAt50pc, IndexType.At50));
         if (mNotchIndices.segIndexAtWJTPost50pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAtWJTPost50pc, IndexType.Post50));
      }
      if (!appAt75) {
         if (mNotchIndices.segIndexAt75pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAt75pc, IndexType.At75));
         if (mNotchIndices.segIndexAtWJTPost75pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAtWJTPost75pc, IndexType.Post75));
      }
      if (mNotchIndices.flexIndices.Count > 0) {
         notchIndexSequence.Add ((mNotchIndices.flexIndices[0].Item1, IndexType.Flex1BeforeStart));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[0].Item2, IndexType.Flex1Start));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[0].Item3, IndexType.Flex1End));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[0].Item4, IndexType.Flex1AfterEnd));
      }
      if (mNotchIndices.flexIndices.Count > 1) {
         notchIndexSequence.Add ((mNotchIndices.flexIndices[1].Item1, IndexType.Flex2BeforeStart));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[1].Item2, IndexType.Flex2Start));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[1].Item3, IndexType.Flex2End));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[1].Item4, IndexType.Flex2AfterEnd));
      }

      // Sort the collection in descending order of Index
      notchIndexSequence = [.. notchIndexSequence.OrderByDescending (item => item.Index)];
      IndexType prevIdxType = IndexType.None;
      int prevIdx = -1;
      bool started = false;
      List<NotchSequenceSection> reverseNotchSequences = [];
      for (int ii = 0; ii < notchIndexSequence.Count; ii++) {
         if (notchIndexSequence[ii].Index == -1) break;
         if (prevIdxType == IndexType.Zero && prevIdxType != IndexType.At25) throw new Exception ("Notch reached zero and then continuing. Wrong");
         if (prevIdx == notchIndexSequence[ii].Index && prevIdx != 0)
            throw new Exception ("Two notch sequence indices are the same. Wrong");
         int startIndex = -1;
         switch (notchIndexSequence[ii].Type) {
            case IndexType.PostApproach:
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               started = true;
               break;
            case IndexType.Flex2AfterEnd:
               if (!started) continue;
               if (prevIdxType == IndexType.PostApproach) startIndex = prevIdx;
               else startIndex = prevIdx - 1;
               if (startIndex >= notchIndexSequence[ii].Index + 1)
                  reverseNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index + 1, NotchSectionType.MachineToolingReverse));
               else throw new Exception ("Start < end Index");
               reverseNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpReverse));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex2End:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex2AfterEnd) throw new Exception ("Prev and curr idx types are not compatible");
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex2Start:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex2End) throw new Exception ("Prev and curr idx types are not compatible");
               reverseNotchSequences.Add (CreateNotchSequence (prevIdx, notchIndexSequence[ii].Index, NotchSectionType.MachineFlexToolingReverse));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex2BeforeStart:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex2Start) throw new Exception ("Prev and curr idx types are not compatible");
               reverseNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpReverse));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex1AfterEnd:
               if (!started) continue;
               if (prevIdx - 1 >= notchIndexSequence[ii].Index + 1) {
                  if (prevIdxType == IndexType.PostApproach) startIndex = prevIdx;
                  else startIndex = prevIdx - 1;
                  if (startIndex >= notchIndexSequence[ii].Index + 1)
                     reverseNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index + 1, NotchSectionType.MachineToolingReverse));
                  else throw new Exception ("Start < end Index");
               }
               reverseNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpReverse));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex1End:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex1AfterEnd) throw new Exception ("Prev and curr idx types are not compatible");
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex1Start:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex1End) throw new Exception ("Prev and curr idx types are not compatible");
               reverseNotchSequences.Add (CreateNotchSequence (prevIdx, notchIndexSequence[ii].Index, NotchSectionType.MachineFlexToolingReverse));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex1BeforeStart:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex1Start) throw new Exception ("Prev and curr idx types are not compatible");
               reverseNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpReverse));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Post50:
            case IndexType.Post25:
               if (!started) continue;
               if (prevIdxType == IndexType.Flex1BeforeStart || prevIdxType == IndexType.Flex2BeforeStart ||
                  notchIndexSequence[ii].Index != mNotchIndices.segIndexAtWJTPostApproach) {
                  if (prevIdxType == IndexType.PostApproach) startIndex = prevIdx;
                  else startIndex = prevIdx - 1;
                  if (startIndex >= notchIndexSequence[ii].Index + 1) {
                     reverseNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index + 1, NotchSectionType.MachineToolingReverse));
                     prevIdxType = notchIndexSequence[ii].Type;
                     prevIdx = notchIndexSequence[ii].Index;
                  } else throw new Exception ("prevIdx - 1 > notchIndexSequence[ii].Index + 1 iS FALSE");
                  if (notchIndexSequence[ii].Index != mNotchIndices.segIndexAtWJTPostApproach)
                     reverseNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpReverse));
               }
               break;
            case IndexType.At25:
            case IndexType.At50:
               if (!started) continue;
               break;
            case IndexType.Zero:
               if (!started) continue;
               if (prevIdx != notchIndexSequence[ii].Index && notchIndexSequence[ii - 1].Index != mNotchIndices.segIndexAtWJTPost25pc &&
                  notchIndexSequence[ii - 1].Index != mNotchIndices.segIndexAtWJTApproach &&
                  notchIndexSequence[ii - 1].Type != IndexType.Flex1BeforeStart &&
                  notchIndexSequence[ii - 1].Type != IndexType.Flex2BeforeStart) startIndex = prevIdx;
               else startIndex = prevIdx - 1;
               reverseNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index, NotchSectionType.MachineToolingReverse));
               prevIdx = notchIndexSequence[ii].Index;
               break;
            default:
               break;
         };
      }
      if (prevIdx > 0)
         reverseNotchSequences.Add (CreateNotchSequence (prevIdx, 0, NotchSectionType.MachineFlexToolingReverse));
      return reverseNotchSequences;
   }

   /// <summary>
   /// The following method creates Notch sequences in the forward direction of the 
   /// list of tooling, considering the notch and wirejoint points and flex sections 
   /// for various possible occurances of notch points and flex sections.
   /// </summary>
   /// <param name="mNotchIndices">The indices of the notch, wire joint points</param>
   /// <returns>A list of assembled notch sequence sections to be used for writing G Code</returns>
   /// <exception cref="Exception">The notch sequences happen in a strong directional
   /// order. If this directional/sequencial order is wrong, exception will be thrown</exception>
   List<NotchSequenceSection> CreateNotchForwardSequences () {
      bool appAt25 = false, appAt50 = false, appAt75 = false;
      if (mNotchIndices.segIndexAt25pc == mNotchIndices.segIndexAtWJTApproach) appAt25 = true;
      else if (mNotchIndices.segIndexAt50pc == mNotchIndices.segIndexAtWJTApproach) appAt50 = true;
      else if (mNotchIndices.segIndexAt75pc == mNotchIndices.segIndexAtWJTApproach) appAt75 = true;
      List<(int Index, IndexType Type)> notchIndexSequence = [
    (0, IndexType.Zero),
    (mNotchIndices.segIndexAtWJTPreApproach, IndexType.PreApproach),
    (mNotchIndices.segIndexAtWJTApproach, IndexType.Approach),
    (mNotchIndices.segIndexAtWJTPostApproach, IndexType.PostApproach),
    (mSegments.Count-1, IndexType.Max)
      ];
      if (!appAt25) {
         if (mNotchIndices.segIndexAt25pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAt25pc, IndexType.At25));
         if (mNotchIndices.segIndexAtWJTPost25pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAtWJTPost25pc, IndexType.Post25));
      }
      if (!appAt50) {
         if (mNotchIndices.segIndexAt50pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAt50pc, IndexType.At50));
         if (mNotchIndices.segIndexAtWJTPost50pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAtWJTPost50pc, IndexType.Post50));
      }
      if (!appAt75) {
         if (mNotchIndices.segIndexAt75pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAt75pc, IndexType.At75));
         if (mNotchIndices.segIndexAtWJTPost75pc != -1) notchIndexSequence.Add ((mNotchIndices.segIndexAtWJTPost75pc, IndexType.Post75));
      }
      if (mNotchIndices.flexIndices.Count > 0) {
         notchIndexSequence.Add ((mNotchIndices.flexIndices[0].Item1, IndexType.Flex1BeforeStart));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[0].Item2, IndexType.Flex1Start));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[0].Item3, IndexType.Flex1End));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[0].Item4, IndexType.Flex1AfterEnd));
      }
      if (mNotchIndices.flexIndices.Count > 1) {
         notchIndexSequence.Add ((mNotchIndices.flexIndices[1].Item1, IndexType.Flex2BeforeStart));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[1].Item2, IndexType.Flex2Start));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[1].Item3, IndexType.Flex2End));
         notchIndexSequence.Add ((mNotchIndices.flexIndices[1].Item4, IndexType.Flex2AfterEnd));
      }

      // Sort the collection in ascending order of Index
      notchIndexSequence = [.. notchIndexSequence.OrderBy (item => item.Index)];
      IndexType prevIdxType = IndexType.None;
      int prevIdx = -1;
      bool started = false;
      List<NotchSequenceSection> forwardNotchSequences = [];
      for (int ii = 0; ii < notchIndexSequence.Count; ii++) {
         if (notchIndexSequence[ii].Index == -1) continue;
         if (prevIdxType == IndexType.Max && prevIdxType != IndexType.Post75) throw new Exception ("IndexType.Max is referred to by two entries");
         if (prevIdx == notchIndexSequence[ii].Index && prevIdx != notchIndexSequence[^1].Index)
            throw new Exception ("Two notch sequence indices are the same. Wrong");
         int startIndex = -1;
         switch (notchIndexSequence[ii].Type) {
            case IndexType.PreApproach:
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               started = true;
               break;
            case IndexType.Flex2BeforeStart:
               if (!started) continue;
               startIndex = prevIdx + 1;
               if (startIndex < notchIndexSequence[ii].Index - 1) {
                  forwardNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index - 1, NotchSectionType.MachineToolingForward));
                  forwardNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpForward));
                  prevIdxType = notchIndexSequence[ii].Type;
                  prevIdx = notchIndexSequence[ii].Index;
               }
               break;
            case IndexType.Flex2Start:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex2BeforeStart) throw new Exception ("Prev and curr idx types are not compatible");
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex2End:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex2Start) throw new Exception ("Prev and curr idx types are not compatible");
               forwardNotchSequences.Add (CreateNotchSequence (prevIdx, notchIndexSequence[ii].Index, NotchSectionType.MachineFlexToolingForward));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex2AfterEnd:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex2End) throw new Exception ("Prev and curr idx types are not compatible");
               forwardNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpReverse));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex1BeforeStart:
               if (!started) continue;
               startIndex = prevIdx + 1;
               if (startIndex <= notchIndexSequence[ii].Index - 1) {
                  forwardNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index - 1, NotchSectionType.MachineToolingForward));
                  forwardNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpForward));
                  prevIdxType = notchIndexSequence[ii].Type;
                  prevIdx = notchIndexSequence[ii].Index;
               }
               break;
            case IndexType.Flex1Start:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex1BeforeStart) throw new Exception ("Prev and curr idx types are not compatible");
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex1End:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex1Start) throw new Exception ("Prev and curr idx types are not compatible");
               forwardNotchSequences.Add (CreateNotchSequence (prevIdx, notchIndexSequence[ii].Index, NotchSectionType.MachineFlexToolingForward));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.Flex1AfterEnd:
               if (!started) continue;
               if (prevIdxType != IndexType.Flex1End) throw new Exception ("Prev and curr idx types are not compatible");
               forwardNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpReverse));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;
            case IndexType.At75:
            case IndexType.At50:
            case IndexType.At25:
               if (!started) continue;
               startIndex = prevIdx + 1;
               if (prevIdxType == IndexType.Flex1AfterEnd || prevIdxType == IndexType.Flex2AfterEnd || notchIndexSequence[ii].Index != mNotchIndices.segIndexAtWJTPreApproach) {
                  if (startIndex < notchIndexSequence[ii].Index)
                     forwardNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index, NotchSectionType.MachineToolingForward));
                  prevIdxType = notchIndexSequence[ii].Type;
                  prevIdx = notchIndexSequence[ii].Index;
               }
               break;
            case IndexType.Post25:
            case IndexType.Post50:
            case IndexType.Post75:
               if (!started) continue;
               forwardNotchSequences.Add (CreateNotchSequence (notchIndexSequence[ii].Index, notchIndexSequence[ii].Index, NotchSectionType.WireJointTraceJumpForward));
               prevIdxType = notchIndexSequence[ii].Type;
               prevIdx = notchIndexSequence[ii].Index;
               break;

            case IndexType.Max:
               if (!started) continue;
               if (notchIndexSequence.Last ().Index != prevIdx)
                  startIndex = prevIdx + 1;
               else startIndex = prevIdx;
               if (startIndex <= notchIndexSequence[ii].Index) {
                  forwardNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index, NotchSectionType.MachineToolingForward));
                  prevIdxType = notchIndexSequence[ii].Type;
               } else {
                  if (notchIndexSequence[^2].Type != IndexType.Post75 && notchIndexSequence[^2].Type != IndexType.Flex1AfterEnd &&
                     notchIndexSequence[^2].Type != IndexType.Flex2AfterEnd)
                     forwardNotchSequences.Add (CreateNotchSequence (startIndex, notchIndexSequence[ii].Index, NotchSectionType.MachineToolingForward));
               }
               prevIdx = notchIndexSequence[ii].Index;
               break;
            default:
               break;
         };
      }
      return forwardNotchSequences;
   }

   /// <summary>
   /// Creates a sequence section to enter or re-enter the notch tooling 
   /// before or after machining the first part. The following steps are involved:
   /// <list type="number">
   /// <item>
   ///     <description>The notch tooling is machined, allowing the scrap-side material 
   ///     to connect with the required side at smaller lengths (wire joint distances). 
   ///     A direct approach to the tooling segment is completely avoided.</description>
   /// </item>
   /// <item>
   ///     <description>A cutting stroke is made from the approximate midpoint of the 
   ///     scrap-side material to the nearest edge.</description>
   /// </item>
   /// <item>
   ///     <description>Another cutting stroke is made from the midpoint to the 50%-th notch point.</description>
   /// </item>
   /// <item>
   ///     <description>Machining of the notch starts in the direction that reaches the 
   ///     0 of X the fastest.</description>
   /// </item>
   /// <item>
   ///     <description>Further cutting occurs from the midpoint to the 50%-th notch point, 
   ///     with machining in the direction opposite to the previously traced path.</description>
   /// </item>
   /// </list>
   /// </summary>
   /// <param name="reEntry">Indicates whether this is a re-entry (True) or a first-time entry (False).</param>
   /// <returns>A structure that holds the type of the sequence section.</returns>
   NotchSequenceSection CreateApproachToNotchSequence (bool reEntry = false) {
      // Create first notch sequence
      NotchSequenceSection nsq = new () { mSectionType = NotchSectionType.WireJointApproach };
      if (reEntry) nsq.mSectionType = NotchSectionType.ApproachOnReEntry;
      return nsq;
   }

   /// <summary>
   /// This method is an internal utility to store unique set of 
   /// the index of the tooling segment against the notch point (the end
   /// point of that index-th segment). 
   /// Reason: There are cases where more than 1 notch points occur in the 
   /// same index-th tooling segment. This case is split so that one 
   /// index-th tooling segment does not contain more than 1 notch point
   /// </summary>
   /// <param name="splitToolSegs">The tooling segments that are already split at
   /// the points of interest</param>
   /// <param name="segs">The parent segments list of the tooling, which needs to be 
   /// corrected with the split segments</param>
   /// <param name="notchPointsInfo">The structure that holds the index and the single 
   /// notch point</param>
   /// <param name="index">The index at which the split has happened</param>
   void MergeSegments (ref List<ToolingSegment> splitToolSegs, ref List<ToolingSegment> segs, int segIndexToSplit) {
      if (splitToolSegs.Count > 1) {
         segs.RemoveAt (segIndexToSplit);
         segs.InsertRange (segIndexToSplit, splitToolSegs);
      }
   }

   /// <summary>
   /// This method the first in the series of notch points computations. Given 
   /// the interested locations (at 25%, 50% and 75%), this method computes 
   /// the list of 3 tuples, where the first item stores the indices of the 
   /// tooling segments on which the notch point occurs and the second item in the
   /// tuple is the notch point itself on the tooling.
   /// </summary>
   void ComputeNotchPointOccurances () {
      mSegsCount = 0;
      var newPercents = mPercentLength;
      while (mSegsCount < mPercentLength.Length) {
         if ((mSegsCount == 0 || mSegsCount == 2) && mNotchWireJointDistance < 0.5) {
            NotchPointInfo np = new (-1, new Point3 (), mSegsCount == 0 ? 0.25 : (mSegsCount == 1 ? 0.50 : 0.75),
               mSegsCount == 0 ? "@25" : (mSegsCount == 1 ? "@50" : "@75"));
            mNotchPointsInfo.Add (np);
         } else {
            var (segIndex, npt) = Utils.GetNotchPointsOccuranceParams (mSegments, mPercentLength[mSegsCount], mCurveLeastLength);
            mSegIndices[mSegsCount] = segIndex; mNotchPoints[mSegsCount] = npt;

            // Find the notch point with the specified segIndex
            int npFoundIndex = mNotchPointsInfo.FindIndex (np => np.mSegIndex == segIndex);
            var invalidSeg = !mSegments[segIndex].IsValid;
            double percent = mPercentLength[mSegsCount];
            if (invalidSeg) mInvalidIndices.Add (segIndex);
            int segIx = segIndex; Point3 pt = npt;

            // If the notch is not short one, and if the segment is invalid on account of its concavity
            // (where in the vector towards the nearest flange boundary intersects with other segment(s) 
            // of the notc), move the spec point forward to the nearest probable point on the segments.
            if (!mShortPerimeterNotch) {
               double ptLenOnSegIx = -100;
               while (invalidSeg) {
                  percent += 0.01;
                  (segIx, pt) = Utils.GetNotchPointsOccuranceParams (mSegments, percent, mCurveLeastLength);
                  mNotchPoints[mSegsCount] = pt;
                  invalidSeg = !mSegments[segIx].IsValid;
                  if (invalidSeg) continue;
                  // Correct the new point on the index to be at least 15 mm
                  try {
                     ptLenOnSegIx = Geom.GetLengthAtPoint (mSegments[segIx].Curve, pt, mSegments[segIx].Vec0);
                  } catch (Exception) { continue; }
                  if (ptLenOnSegIx > 0 && ptLenOnSegIx < minThresholdSegLen) {
                     try {
                        pt = Geom.GetPointAtLengthFromStart (mSegments[segIx].Curve, mSegments[segIx].Vec0, minThresholdSegLen);
                        if (!Geom.IsPointOnCurve (mSegments[segIx].Curve, pt, mSegments[segIx].Vec0))
                           throw new Exception ("Point not on the curve");
                     } catch (Exception) { continue; }
                  }
               }
               mPercentLength = [mSegsCount != 0 ? mPercentLength[0] : percent, mSegsCount != 1 ? mPercentLength[1] : percent, mSegsCount != 2 ? mPercentLength[2] : percent];
               mSegIndices[mSegsCount] = segIx; mNotchPoints[mSegsCount] = pt;
            } else {
               if (invalidSeg) {
                  mSegIndices[mSegsCount] = -1;
               }
            }
            if (npFoundIndex != -1) mNotchPointsInfo[npFoundIndex].mPoints.Add (npt);
            else {
               NotchPointInfo np = new (invalidSeg ? -1 : segIx, pt, mSegsCount == 0 ? 0.25 : (mSegsCount == 1 ? 0.50 : 0.75),
                  mSegsCount == 0 ? "@25" : (mSegsCount == 1 ? "@50" : "@75"));
               mNotchPointsInfo.Add (np);
            }
         }
         mSegsCount++;
      }
   }

   /// <summary>
   /// This method collects the tooling segments' indices for the flex section AND
   /// also collects the wire joint points which are at wireJointDistance to the start
   /// of the flex(es) and wireJointDistance from the end of the flex(es).
   /// </summary>
   /// <remarks>
   /// <note>
   /// If the wire joint distance is prescribed as zero in the settings, the wire joint
   /// points computation is not affected. A value of 2.0 units is assumed. This is because, as per the 
   /// requirement, wire joint distance prescription affects only the points at 25% and 75% of 
   /// the lengthened points on the notch.
   /// </note>
   /// </remarks>
   void ComputeWireJointPositionsOnFlexes () {
      for (int ii = 0; ii < mFlexIndices.Count; ii++) {
         mFlexIndices = GetFlexSegmentIndices (mSegments);
         Point3 preFlexSegStPt; int preFlexSegIndex;
         int segIndexPrevFlexSegStart = mFlexIndices[ii].Item1 - 1; // Index of the segment which is fully tooled
         double wireJointDist = mNotchWireJointDistance;
         List<ToolingSegment> splitToolSegs = [];
         if (wireJointDist < 0.5) wireJointDist = 2.0;
         if (segIndexPrevFlexSegStart < 0) {
            segIndexPrevFlexSegStart = 0;
            (preFlexSegStPt, preFlexSegIndex) = Geom.GetToolingPointAndIndexAtLength (mSegments, segIndexPrevFlexSegStart,
               wireJointDist /*mSegments[segIndexPrevFlexSegStart].Item2.Normalized (),*/);
            splitToolSegs = Utils.SplitToolingSegmentsAtPoint (mSegments, preFlexSegIndex, preFlexSegStPt,
               mSegments[segIndexPrevFlexSegStart].Vec0.Normalized ());
         } else {
            (preFlexSegStPt, preFlexSegIndex) = Geom.GetToolingPointAndIndexAtLength (mSegments, segIndexPrevFlexSegStart,
               wireJointDist, /*mSegments[segIndexPrevFlexSegStart].Item2.Normalized (),*/ reverseTrace: true);
            splitToolSegs = Utils.SplitToolingSegmentsAtPoint (mSegments, preFlexSegIndex, preFlexSegStPt,
               mSegments[segIndexPrevFlexSegStart].Vec0.Normalized ());
         }

         MergeSegments (ref splitToolSegs, ref mSegments, preFlexSegIndex);
         Utils.ReIndexNotchPointsInfo (mSegments, ref mNotchPointsInfo);
         Utils.CheckSanityOfToolingSegments (mSegments);
         mFlexIndices = GetFlexSegmentIndices (mSegments);

         var (flexStPtIndex, _) = mFlexIndices[ii];
         if (flexStPtIndex == 0) flexStPtIndex = 1; // Test_Partha
         mFlexWireJointPts.Add (mSegments[flexStPtIndex - 1].Curve.End);
         mFlexWireJointPts.Add (mSegments[flexStPtIndex].Curve.End);

         Point3 postFlexSegEndPt; int postFlexSegEndIndex;
         int segIndexFlexEnd = mFlexIndices[ii].Item2;
         (postFlexSegEndPt, postFlexSegEndIndex) = Geom.GetToolingPointAndIndexAtLength (mSegments, segIndexFlexEnd, wireJointDist
            /*,mSegments[segIndexFlexEnd].Item2.Normalized ()*/);
         splitToolSegs = Utils.SplitToolingSegmentsAtPoint (mSegments, postFlexSegEndIndex, postFlexSegEndPt,
            mSegments[postFlexSegEndIndex].Vec0.Normalized ());
         MergeSegments (ref splitToolSegs, ref mSegments, postFlexSegEndIndex);
         Utils.ReIndexNotchPointsInfo (mSegments, ref mNotchPointsInfo);
         Utils.CheckSanityOfToolingSegments (mSegments);
         mFlexIndices = GetFlexSegmentIndices (mSegments);
         mFlexWireJointPts.Add (mSegments[mFlexIndices[ii].Item2].Curve.End);
         mFlexWireJointPts.Add (mSegments[mFlexIndices[ii].Item2 + 1].Curve.End);
      }
      for (int ii = 0; ii < mNotchPointsInfo.Count; ii++) {
         if (mNotchWireJointDistance < 0.5 && ii != 1) {
            var npInfo = mNotchPointsInfo[ii];
            npInfo.mSegIndex = -1;
            mNotchPointsInfo[ii] = npInfo;
         }
      }
   }

   /// <summary>
   /// This method creates and populates various notch parameters and data structures required 
   /// for tooling operations. It performs the following actions:
   /// <list type="number">
   /// <item>
   ///     <description>Splits the list of tooling segments at all notch points, wire joint points, 
   ///     and at the start and end of flex sections.</description>
   /// </item>
   /// <item>
   ///     <description>Records indices and coordinates of the notch points where occurrences happen.</description>
   /// </item>
   /// <item>
   ///     <description>Identifies wire joint points, their lengths, and their respective indices.</description>
   /// </item>
   /// <item>
   ///     <description>Determines the indices of points at the start and end of flex sections.
   ///     <list type="bullet">
   ///         <item>
   ///             <description>The initial segment whose end marks the starting point of the flex section.
   ///             This segment's length is equivalent to the wire joint distance.</description>
   ///         </item>
   ///         <item>
   ///             <description>The segment that represents the first segment of the flex section.</description>
   ///         </item>
   ///         <item>
   ///             <description>The index of the segment that marks the end of the flex section.</description>
   ///         </item>
   ///         <item>
   ///             <description>The segment whose starting point is the ending point of the flex section.
   ///             Its length is also the wire joint distance.</description>
   ///         </item>
   ///     </list>
   ///     </description>
   /// </item>
   /// <item>
   ///     <description>Stores sequentially ordered indices for the segments and points.</description>
   /// </item>
   /// </list>
   /// </summary>
   void ComputeNotchParameters () {
      Utils.CheckSanityOfToolingSegments (mSegments);
      if (!mToolingItem.IsNotch ()) return;

      // Find the flex segment indices
      mFlexIndices = GetFlexSegmentIndices (mSegments);

      // The indices of mSegments on whose segment the 25%, 50% and 75% of the length occurs
      mSegIndices = [null, null, null]; mSegsCount = 0;

      // The point on the segment which shall participate in notch tooling
      mNotchPoints = new Point3?[3];
      mNotchPointsInfo = [];

      // Compute the occurances of the notch points
      // at 25%, 50% and 75% of the total tooling lengths
      ComputeNotchPointOccurances ();
      if (mShortPerimeterNotch) {
         if (!mInvalidIndices.Contains (1)) mApproachIndex = 1;
         else if (!mInvalidIndices.Contains (0)) mApproachIndex = 0;
         else if (mInvalidIndices.Contains (2)) mApproachIndex = 2;
         else throw new Exception ("Notch Indices are invalid for all of 25, 50 and 75% of notch lengths");
      }
      var ptAt75 = mNotchPoints[2].Value;
      var ptAt50 = mNotchPoints[mApproachIndex].Value;
      var (lenAt75pc, _) = Geom.GetLengthAtPoint (mSegments, ptAt75);
      var (lenAtLen50, _) = Geom.GetLengthAtPoint (mSegments, ptAt50);
      var endPercent = 0.75;
      while (true) {
         endPercent += 0.01;
         if (lenAtLen50 >= (lenAt75pc - minThresholdSegLen)) {
            mPercentLength = [mPercentLength[0], mPercentLength[mApproachIndex], endPercent];
            ComputeNotchPointOccurances ();
         } else break;
         ptAt75 = mNotchPoints[2].Value;
         lenAt75pc = Geom.GetLengthAtPoint (mSegments[mSegIndices[2].Value].Curve, ptAt75, mSegments[mSegIndices[2].Value].Vec0);
      }

      //var npi = mNotchPointsInfo;
      // Find if any of the notch point is with in the flex indices
      double minThresholdLenFromNPToFlexPt = 15;
      double thresholdNotchLenForNotchApproach = 200.0;

      // Recompute or refuse the notch points if they occur within flex sections. The way to refuse the notch point
      // its participation is by setting its index = -1
      RecomputeNotchPointsAgainstFlexNotch (mSegments, mFlexIndices, ref mNotchPoints, ref mSegIndices, mPercentLength,
         minThresholdLenFromNPToFlexPt, thresholdNotchLenForNotchApproach);

      // Split the curves and modify the indices and segments in segments and
      // in mNotchPointsInfo
      SplitToolingSegmentsAtPoints (ref mSegments, ref mNotchPointsInfo);
      Utils.ReIndexNotchPointsInfo (mSegments, ref mNotchPointsInfo);
      Utils.CheckSanityNotchPointsInfo (mSegments, mNotchPointsInfo);

      // Run the sanity test on the segments after split
      Utils.CheckSanityOfToolingSegments (mSegments);

      // Compute the notch attributes
      mNotchAttrs = GetNotchAttributes (ref mSegments, ref mNotchPointsInfo, mFullPartBound, mToolingItem);

      // Check the sanity of the segments of the notch.
      Utils.CheckSanityOfToolingSegments (mSegments);

      // Compute the wire joint positions on the flanges, which are intentionally created discontinuities to allow for
      // a small strip (wire notch distance) to hold on to the otherwise cut parts, which require a minimal
      // physical force to cut away the scrap side material
      ComputeWireJointPositionsOnFlanges (mSegments, mNotchPoints, ref mNotchPointsInfo, mNotchWireJointDistance, mApproachIndex);
      Utils.CheckSanityNotchPointsInfo (mSegments, mNotchPointsInfo);

      // Compute the wire joint positions on the flexes. The start and end positions of the 
      // flexes are created with this wire joints
      ComputeWireJointPositionsOnFlexes ();
      Utils.CheckSanityNotchPointsInfo (mSegments, mNotchPointsInfo);

      // Compute the indices of notch points and wire joint skip(jump) trace points
      ComputeNotchToolingIndices (mSegments, mNotchPoints, mWireJointPts, mFlexWireJointPts);
      Utils.CheckSanityNotchPointsInfo (mSegments, mNotchPointsInfo);

      // Create the list of notch sequence sections. Each section is a local action directive to
      // cut with a specific category. This is also the location where the sequences shall be modified
      // based on the need.
      mNotchSequences.Clear ();
      mNotchSequences.Add (CreateApproachToNotchSequence ());

      // Assemble the tooling sequence sections
      //int forwardStartIndex = mNotchIndices.segIndexAtWJTPreApproach;
      if (IsForwardFirstNotchTooling (mSegments)) {
         mNotchSequences.Add (CreateNotchSequence (mNotchIndices.segIndexAtWJTApproach, mNotchIndices.segIndexAtWJTApproach, NotchSectionType.GambitMachiningAt50Reverse));
         mNotchSequences.AddRange (CreateNotchForwardSequences ());
         mNotchSequences.Add (CreateNotchSequence (mSegments.Count - 1, -1, NotchSectionType.MoveToMidApproach));
         mNotchSequences.Add (CreateApproachToNotchSequence (reEntry: true));
         mNotchSequences.Add (CreateNotchSequence (mNotchIndices.segIndexAtWJTPostApproach, mNotchIndices.segIndexAtWJTPostApproach, NotchSectionType.GambitMachiningAt50Forward));
         mNotchSequences.AddRange (CreateNotchReverseSequences ());

      } else {
         mNotchSequences.Add (CreateNotchSequence (mNotchIndices.segIndexAtWJTPostApproach, mNotchIndices.segIndexAtWJTPostApproach, NotchSectionType.GambitMachiningAt50Forward));
         mNotchSequences.AddRange (CreateNotchReverseSequences ());
         mNotchSequences.Add (CreateNotchSequence (0, -1, NotchSectionType.MoveToMidApproach));
         mNotchSequences.Add (CreateApproachToNotchSequence (reEntry: true));
         mNotchSequences.Add (CreateNotchSequence (mNotchIndices.segIndexAtWJTApproach, mNotchIndices.segIndexAtWJTApproach, NotchSectionType.GambitMachiningAt50Reverse));
         mNotchSequences.AddRange (CreateNotchForwardSequences ());
      }
   }

   /// <summary>
   /// This following method is used to quickly compute notch data to decide if the notch is 
   /// valid.
   /// </summary>
   /// <param name="segs">The input tooling segments</param>
   /// <param name="percentPos">The positions of the points occuring in the interested order</param>
   /// <param name="curveLeastLength">The least count length of the curve</param>
   /// <returns></returns>
   public static Tuple<int[], Point3[]> ComputeNotchPointOccuranceParams (List<ToolingSegment> segs, double[] percentPos, double curveLeastLength) {
      int count = 0;
      Point3[] notchPoints = new Point3[3];
      int[] segIndices = [-1, -1, -1];
      List<NotchPointInfo> notchPointsInfo = [];
      while (count < percentPos.Length) {
         List<ToolingSegment> splitCurves = [];
         (segIndices[count], notchPoints[count]) = Utils.GetNotchPointsOccuranceParams (segs, percentPos[count], curveLeastLength);
         notchPointsInfo.FindIndex (np => np.mSegIndex == segIndices[count]);

         // Find the notch point with the specified segIndex
         var index = notchPointsInfo.FindIndex (np => np.mSegIndex == segIndices[count]);
         if (index != -1) notchPointsInfo[index].mPoints.Add (notchPoints[count]);
         else {
            NotchPointInfo np = new (segIndices[count], notchPoints[count], count == 0 ? 25 : (count == 1 ? 50 : 75),
               count == 0 ? "@25" : (count == 1 ? "@50" : "@75"));
            notchPointsInfo.Add (np);
         }
         count++;
      }
      return new Tuple<int[], Point3[]> (segIndices, notchPoints);
   }

   /// <summary>
   /// This method computes the notch positions fo the entry machining to the 
   /// notch profile. The tool first reaches the position namely, n1, which is 
   /// offset at right angles to the line joining 50% lengthed point and the nearest
   /// boundary along the flange. The tool starts machining from n1 through nMid1 and 
   /// to the end of the flange. It again rapid positions at n2, starts machining from
   /// n2 through nMid2 and to the 50$ point.
   /// </summary>
   /// <param name="toolingItem">The input tooling item</param>
   /// <param name="segs">Preprocessed segments</param>
   /// <param name="notchAttrs">The notch attributes</param>
   /// <param name="bound">The total bound of the part</param>
   /// <param name="approachIndex">The segment index in the segs</param>
   /// <param name="wireJointDistance">The input prescription for allowing a small
   /// joint to prevent the iron sheet from falling after machining</param>
   /// <returns></returns>
   public static (Point3 FirstEntryPt, Point3 FirstMidPt, Point3 FlangeEndPt,
                Point3 SecondEntryPt, Point3 SecondMidPt, Point3 ToolingApproachPt)
   GetNotchApproachPositions (Tooling toolingItem,
                           List<ToolingSegment> segs,
                           List<NotchAttribute> notchAttrs,
                           Bound3 bound,
                           int approachIndex,
                           double wireJointDistance) {

      var planeNormal = notchAttrs[approachIndex].Item3.Normalized ();
      Point3 flangeBoundaryEnd;

      // In order to find the best flange end point somewhere mid between the start and
      // end of notch tooling, to be far away from the starting and end points of the
      // segnments' start and end, a measure of MIN (| p->Sp and p->Ep | ) is found.
      // This is a generalization of taking the mid point of between the start and
      // end points of the segments. If the notch is only on one of the flanges,
      // a mid point would suffice. But if the notch is on flex or on multiple flanges,
      // the above idea is the best. For any point to be equi distant and on the part,
      // a MIN (| p->Sp and p->Ep | ) holds good.
      Point3 bestPoint = new ();
      int bestSegIndex = -1;
      bool bestPointFound = false;
      double thresholdLengthFromStartToApproachPt = 50.0;
      (var segIndex, _) = Utils.GetNotchPointsOccuranceParams (segs, 0.5, 0.5);

      double pToCurveEndAlongX = double.MaxValue, pToCurveEndAlongY = double.MaxValue;
      double pToCurveStartAlongX = double.MaxValue, pToCurveStartAlongY = double.MaxValue;

      var flangeTypeAt50pc = Utils.GetArcPlaneFlangeType (segs[segIndex].Vec0, XForm4.IdentityXfm);
      var flangeTypeAtSegStart = Utils.GetArcPlaneFlangeType (segs.First ().Vec0, XForm4.IdentityXfm);
      var flangeTypeAtSegEnd = Utils.GetArcPlaneFlangeType (segs.Last ().Vec0, XForm4.IdentityXfm);
      if (flangeTypeAt50pc == flangeTypeAtSegStart && ((flangeTypeAtSegStart == EFlange.Top || flangeTypeAtSegStart == EFlange.Bottom)))
         // Compare Distance in X
         pToCurveStartAlongX = Math.Abs (segs.First ().Curve.Start.X - segs[segIndex].Curve.End.X);
      if (flangeTypeAt50pc == flangeTypeAtSegEnd && ((flangeTypeAtSegEnd == EFlange.Top || flangeTypeAtSegEnd == EFlange.Bottom)))
         // Compare Distance in X
         pToCurveEndAlongX = Math.Abs (segs.Last ().Curve.End.X - segs[segIndex].Curve.End.X);
      if (flangeTypeAt50pc == flangeTypeAtSegEnd && flangeTypeAtSegEnd == EFlange.Web)
         // Compute distance in Y
         pToCurveEndAlongY = Math.Abs (segs.Last ().Curve.End.Y - segs[segIndex].Curve.End.Y);
      if (flangeTypeAt50pc == flangeTypeAtSegStart && flangeTypeAtSegStart == EFlange.Web)
         // Compute distance in Y
         pToCurveStartAlongY = Math.Abs (segs.First ().Curve.Start.Y - segs[segIndex].Curve.End.Y);

      if (pToCurveStartAlongX != double.MaxValue && pToCurveEndAlongX != double.MaxValue) {
         if (pToCurveStartAlongX < pToCurveEndAlongX && pToCurveStartAlongX > thresholdLengthFromStartToApproachPt &&
            (flangeTypeAtSegStart == EFlange.Top || flangeTypeAtSegStart == EFlange.Bottom)) {
            bestPoint = segs[segIndex].Curve.End;
            bestSegIndex = segIndex; bestPointFound = true;
         } else if (pToCurveEndAlongX < pToCurveStartAlongX && pToCurveEndAlongX > thresholdLengthFromStartToApproachPt &&
            (flangeTypeAtSegEnd == EFlange.Top || flangeTypeAtSegEnd == EFlange.Bottom)) {
            bestPoint = segs[segIndex].Curve.End;
            bestSegIndex = segIndex; bestPointFound = true;
         }
      } else if (pToCurveStartAlongY != double.MaxValue && pToCurveEndAlongY != double.MaxValue) {
         if (pToCurveStartAlongY < pToCurveEndAlongY && pToCurveStartAlongY > thresholdLengthFromStartToApproachPt && flangeTypeAtSegStart == EFlange.Web) {
            bestPoint = segs[segIndex].Curve.End;
            bestSegIndex = segIndex; bestPointFound = true;
         } else if (pToCurveEndAlongY < pToCurveStartAlongY && pToCurveStartAlongY > thresholdLengthFromStartToApproachPt && flangeTypeAtSegEnd == EFlange.Web) {
            bestPoint = segs[segIndex].Curve.End;
            bestSegIndex = segIndex; bestPointFound = true;
         }
      }else if (pToCurveEndAlongX  !=  double.MaxValue && ((flangeTypeAtSegEnd == EFlange.Top || flangeTypeAtSegEnd == EFlange.Bottom))){
         bestPoint = segs[segIndex].Curve.End;
         bestSegIndex = segIndex; bestPointFound = true;
      }

      // Special Notch
      if (!bestPointFound) {
         Point3[] paramPts = new Point3[51];
         double[] percentPos = new double[51];
         double stPercent = 0.25; double incr = 0.01;
         for (int ii = 0; ii < 51; ii++) percentPos[ii] = stPercent + ii * incr;

         int[] segIndices = new int[51];
         for (int ii = 0; ii < 51; ii++)
            (segIndices[ii], paramPts[ii]) = Utils.GetNotchPointsOccuranceParams (segs, percentPos[ii], 0.5);
         var Sp = segs.First ().Curve.Start; var Ep = segs.Last ().Curve.End;

         // By default, 1-th index is assumed to be approach index.
         double minDifference = double.MaxValue;

         // Loop through paramPts[] to find the point that minimizes the distance difference
         for (int i = 0; i < paramPts.Length; i++) {
            var p = paramPts[i];
            double distToStart = p.DistTo (Sp);  // Distance to the start point
            double distToEnd = p.DistTo (Ep);    // Distance to the end point
            double diff = Math.Abs (distToStart - distToEnd);

            if (diff < minDifference) {
               minDifference = diff;
               bestPoint = p;
               bestSegIndex = segIndices[i];  // Get the corresponding segIndices[]
               bestPointFound = true;
            }
         }
      }
      if (!bestPointFound) throw new Exception ("Best mid point can not be found");

      // For the best point find the notch attribute info. We are interested in finding the 
      // flange end point, which is given by item5 of NotchAttribute
      (_, _, _, _, var bestOutVec, _, _) = ComputeNotchAttribute (bound, toolingItem, segs, bestSegIndex, bestPoint);
      flangeBoundaryEnd = bestPoint + bestOutVec;


      // The point on the segment at the end of the approachIndex-th segment
      Point3 notchPointAtApproachpc = notchAttrs[approachIndex].Item1.End;

      // Vector from approachIndex-th segment end point TO flangeBoundaryEnd
      var outwardVec = flangeBoundaryEnd - notchPointAtApproachpc;
      var outwardVecDir = outwardVec.Normalized ();

      // Notch spec Mid point
      Point3 nMid1 = notchPointAtApproachpc + outwardVec * 0.5;
      double gap = wireJointDistance > 0.5 ? wireJointDistance : 2.0;

      // Notch Spec second Mid point
      Point3 nMid2 = nMid1 - outwardVecDir * gap;

      // Notch spec wire joint points for mid1 and mid2
      Point3 n1, n2;
      if (Utils.GetPlaneType (planeNormal, XForm4.IdentityXfm) == EPlane.Top) {
         n1 = nMid1 + XForm4.mYAxis * wireJointDistance;
         if ((n1 - nMid1).Opposing (outwardVecDir)) n1 = nMid1 - XForm4.mYAxis * wireJointDistance;
         n2 = nMid2 + XForm4.mYAxis * wireJointDistance;
         if ((n2 - nMid2).Opposing (outwardVecDir)) n2 = nMid2 - XForm4.mYAxis * wireJointDistance;
      } else {
         n1 = nMid1 + XForm4.mXAxis * wireJointDistance;
         if ((n1 - nMid1).Opposing (outwardVecDir)) n1 = nMid1 - XForm4.mXAxis * wireJointDistance;
         n2 = nMid2 + XForm4.mXAxis * wireJointDistance;
         if ((n2 - nMid2).Opposing (outwardVecDir)) n2 = nMid2 - XForm4.mXAxis * wireJointDistance;
      }
      return (n1, nMid1, flangeBoundaryEnd, n2, nMid2, notchPointAtApproachpc);
   }
   #endregion

   #region G Code writer
   /// <summary>
   /// This method writes the G Code for the notch tooling. 
   /// Prerequisite: The method ComputeNotchParameters() should be
   /// called before calling this method. 
   /// </summary>
   /// <exception cref="Exception">Exception will be thrown if the indices do not conform to
   /// the order.</exception>
   public void WriteTooling () {
      if (EdgeNotch) {
         WriteEdgeNotch ();
         return;
      }
      var (n1, nMid1, flangeEnd, n2, nMid2, notchPointAtApproachpc) = GetNotchApproachPositions (mToolingItem, mSegments, mNotchAttrs,
         mFullPartBound, mApproachIndex, mNotchWireJointDistance);
      var (_, notchApproachStNormal, notchApproachEndNormal, _, _, _, _) = mNotchAttrs[mApproachIndex];
      mBlockCutLength = mCutLengthTillPrevTooling;
      for (int ii = 0; ii < mNotchSequences.Count; ii++) {
         var notchSequence = mNotchSequences[ii];
         switch (notchSequence.mSectionType) {
            case NotchSectionType.WireJointApproach: {
                  Utils.EPlane currPlaneType = Utils.GetFeatureNormalPlaneType (notchApproachEndNormal, new ());
                  List<Point3> pts = [];
                  pts.Add (nMid2); pts.Add (n2);
                  pts.Add (flangeEnd);
                  pts.Add (notchPointAtApproachpc);
                  mGCodeGen.InitializeNotchToolingBlock (mToolingItem, prevToolingItem: null, pts, notchApproachStNormal,
                     mXStart, mXPartition, mXEnd, /*isFlexCut:*/ false, ii == mNotchSequences.Count - 1, "Notch: Wire Joint Approach to the Tooling");

                  mGCodeGen.PrepareforToolApproach (mToolingItem, mSegments, PreviousToolingSegment, PreviousTooling, mPrevToolingSegments, mFirstTooling, isValidNotch: true);
                  mGCodeGen.WriteToolCorrectionData (mToolingItem);
                  var notchEntry = GetNotchEntry ();
                  mGCodeGen.MoveToMachiningStartPosition (notchEntry.Item1, notchEntry.Item2, mToolingItem.Name);

                  mGCodeGen.EnableMachiningDirective ();

                  // *** Moving to the mid point wire joint distance ***
                  mGCodeGen.WriteLine (nMid1, notchApproachStNormal, notchApproachEndNormal, currPlaneType,
                     mPrevPlane, Utils.GetArcPlaneFlangeType (notchApproachEndNormal.Normalized (),
                     mGCodeGen.PartConfigType == PartConfigType.LHComponent ? GCodeGenerator.LHCSys : GCodeGenerator.RHCSys),
                     mToolingItem.Name);

                  mGCodeGen.WriteLine (flangeEnd, notchApproachStNormal,
                     notchApproachEndNormal, currPlaneType, mPrevPlane,
                     Utils.GetArcPlaneFlangeType (notchApproachEndNormal.Normalized (),
                     mGCodeGen.PartConfigType == PartConfigType.LHComponent ? GCodeGenerator.LHCSys : GCodeGenerator.RHCSys), mToolingItem.Name);
                  mGCodeGen.DisableMachiningDirective ();
                  mBlockCutLength += n1.DistTo (nMid1);
                  mBlockCutLength += nMid1.DistTo (flangeEnd);
                  mGCodeGen.FinalizeNotchToolingBlock (mToolingItem, mBlockCutLength, mTotalToolingsCutLength);

                  // *** Retract and move to next machining start point n2
                  mGCodeGen.MoveToRetract (n1, notchApproachEndNormal, mToolingItem.Name);
                  mGCodeGen.MoveToMachiningStartPosition (n2, notchApproachStNormal, mToolingItem.Name);

                  pts.Clear ();
                  pts.Add (nMid1); pts.Add (n1); pts.Add (notchPointAtApproachpc);
                  mGCodeGen.InitializeNotchToolingBlock (mToolingItem, prevToolingItem: null, pts, notchApproachStNormal,
                     mXStart, mXPartition, mXEnd, /*isFlexCut:*/ false, ii == mNotchSequences.Count - 1, "Notch: Wire Joint Approach to the Tooling");
                  mGCodeGen.EnableMachiningDirective ();

                  // *** Start machining from n2 -> nMid2 -> 50% dist end point ***
                  mGCodeGen.WriteLine (nMid2, notchApproachStNormal, notchApproachEndNormal, currPlaneType,
                     mPrevPlane, Utils.GetArcPlaneFlangeType (notchApproachEndNormal.Normalized (),
                     mGCodeGen.PartConfigType == PartConfigType.LHComponent ? GCodeGenerator.LHCSys : GCodeGenerator.RHCSys),
                     mToolingItem.Name);

                  // @Notchpoint 50
                  mGCodeGen.WriteLine (mSegments[mNotchIndices.segIndexAtWJTApproach].Curve.End, notchApproachStNormal,
                     notchApproachEndNormal, currPlaneType, mPrevPlane,
                     Utils.GetArcPlaneFlangeType (notchApproachEndNormal.Normalized (),
                     mGCodeGen.PartConfigType == PartConfigType.LHComponent ? GCodeGenerator.LHCSys : GCodeGenerator.RHCSys), mToolingItem.Name);
                  mGCodeGen.DisableMachiningDirective ();
                  mBlockCutLength += n2.DistTo (nMid2);
                  mBlockCutLength += nMid2.DistTo (mSegments[mNotchIndices.segIndexAtWJTApproach].Curve.End);
                  mGCodeGen.FinalizeNotchToolingBlock (mToolingItem, mBlockCutLength, mTotalToolingsCutLength);
                  mRecentToolPosition = mGCodeGen.GetLastToolHeadPosition ().Item1;
               }
               break;
            case NotchSectionType.ApproachOnReEntry: {
                  Utils.EPlane currPlaneType = Utils.GetFeatureNormalPlaneType (notchApproachEndNormal, new ());

                  // @Notchpoint at approach
                  notchPointAtApproachpc = mSegments[mNotchIndices.segIndexAtWJTApproach].Curve.End;

                  List<Point3> pts = [];
                  pts.Add (notchPointAtApproachpc); pts.Add (mRecentToolPosition);
                  pts.Add (n1); pts.Add (nMid1);
                  mGCodeGen.InitializeNotchToolingBlock (mToolingItem, prevToolingItem: null, pts, notchApproachStNormal,
                     mXStart, mXPartition, mXEnd, /*isFlexCut:*/ false, ii == mNotchSequences.Count - 1, "Notch: Direct Approach to the Tooling");
                  mGCodeGen.EnableMachiningDirective ();
                  mGCodeGen.WriteLine (mSegments[mNotchIndices.segIndexAtWJTApproach].Curve.End, notchApproachStNormal,
                     notchApproachEndNormal, currPlaneType, mPrevPlane,
                     Utils.GetArcPlaneFlangeType (notchApproachEndNormal.Normalized (),
                     mGCodeGen.PartConfigType == PartConfigType.LHComponent ? GCodeGenerator.LHCSys : GCodeGenerator.RHCSys), mToolingItem.Name);
                  mGCodeGen.DisableMachiningDirective ();
                  mBlockCutLength += mRecentToolPosition.DistTo (mSegments[mNotchIndices.segIndexAtWJTApproach].Curve.End);
                  mGCodeGen.FinalizeNotchToolingBlock (mToolingItem, mBlockCutLength, mTotalToolingsCutLength);
                  mRecentToolPosition = mGCodeGen.GetLastToolHeadPosition ().Item1;
               }
               break;
            case NotchSectionType.GambitMachiningAt50Forward:
            case NotchSectionType.GambitMachiningAt50Reverse: {
                  ToolingSegment segment = mSegments[notchSequence.mStartIndex];
                  if (notchSequence.mSectionType == NotchSectionType.GambitMachiningAt50Reverse)
                     segment = Geom.GetReversedToolingSegment (mSegments[notchSequence.mStartIndex], tolerance: mSplit ? 1e-4 : 1e-6);
                  mGCodeGen.WriteCurve (segment, mToolingItem.Name);
                  mBlockCutLength += segment.Curve.Length;
                  mGCodeGen.FinalizeNotchToolingBlock (mToolingItem, mBlockCutLength, mTotalToolingsCutLength);
                  mRecentToolPosition = mGCodeGen.GetLastToolHeadPosition ().Item1;
                  break;
               }
            case NotchSectionType.WireJointTraceJumpForward:
            case NotchSectionType.WireJointTraceJumpReverse: {
                  NotchAttribute notchAttr;
                  if (notchSequence.mSectionType == NotchSectionType.WireJointTraceJumpForward)
                     notchAttr = ComputeNotchAttribute (mFullPartBound, mToolingItem, mSegments, notchSequence.mStartIndex,
                        mSegments[notchSequence.mStartIndex].Curve.End);
                  else
                     notchAttr = ComputeNotchAttribute (mFullPartBound, mToolingItem, mSegments, notchSequence.mStartIndex,
                        mSegments[notchSequence.mStartIndex].Curve.Start);
                  Vector3 scrapSideNormal;
                  if (Math.Abs (mSegments[notchSequence.mStartIndex].Vec0.Normalized ().Z - 1.0).EQ (0, mSplit ? 1e-4 : 1e-6) ||
                     Math.Abs (-mSegments[notchSequence.mStartIndex].Vec0.Normalized ().Y + 1.0).EQ (0, mSplit ? 1e-4 : 1e-6) ||
                     Math.Abs (mSegments[notchSequence.mStartIndex].Vec0.Normalized ().Y - 1.0).EQ (0, mSplit ? 1e-4 : 1e-6))
                     scrapSideNormal = notchAttr.Item4;
                  else scrapSideNormal = notchAttr.Item5;
                  bool zeroVec = scrapSideNormal.IsZero;
                  Point3 pt = mSegments[notchSequence.mStartIndex].Curve.End;
                  Vector3 stNormal = mSegments[notchSequence.mStartIndex].Vec0.Normalized ();
                  Vector3 endNormal = mSegments[notchSequence.mStartIndex].Vec1.Normalized ();
                  string comment = "(( ** Notch: Wire Joint Jump Trace Forward Direction ** ))";
                  if (notchSequence.mSectionType == NotchSectionType.WireJointTraceJumpReverse) {
                     pt = mSegments[notchSequence.mStartIndex].Curve.Start;
                     stNormal = mSegments[notchSequence.mStartIndex].Vec1.Normalized ();
                     endNormal = mSegments[notchSequence.mStartIndex].Vec0.Normalized ();
                     comment = "((** Notch: Wire Joint Jump Trace Reverse Direction ** ))";
                  }
                  EFlange flangeType = Utils.GetArcPlaneFlangeType (endNormal,
                     mGCodeGen.PartConfigType == PartConfigType.LHComponent ? GCodeGenerator.LHCSys : GCodeGenerator.RHCSys);
                  mGCodeGen.WriteWireJointTraceForNotch (pt, stNormal, endNormal, scrapSideNormal,
                     mRecentToolPosition, NotchApproachLength, ref mPrevPlane, flangeType, mToolingItem,
                     ref mBlockCutLength, mTotalToolingsCutLength, mXStart, mXPartition, mXEnd, comment);
                  mRecentToolPosition = mGCodeGen.GetLastToolHeadPosition ().Item1;
               }
               break;
            case NotchSectionType.MachineToolingForward: {
                  if (notchSequence.mStartIndex > notchSequence.mEndIndex)
                     throw new Exception ("In WriteNotch: MachineToolingForward : startIndex > endIndex");
                  mGCodeGen.InitializeNotchToolingBlock (mToolingItem, prevToolingItem: null, mSegments,
                     mSegments[notchSequence.mStartIndex].Vec0, mXStart, mXPartition, mXEnd, isFlexCut: false, ii == mNotchSequences.Count - 1,
                     notchSequence.mStartIndex, notchSequence.mEndIndex,
                     comment: "Notch: Machining Forward Direction");
                  mGCodeGen.EnableMachiningDirective ();
                  for (int jj = notchSequence.mStartIndex; jj <= notchSequence.mEndIndex; jj++) {
                     mExitTooling = mSegments[jj];
                     mGCodeGen.WriteCurve (mSegments[jj], mToolingItem.Name);
                     mBlockCutLength += mSegments[jj].Curve.Length;
                  }
                  mGCodeGen.DisableMachiningDirective ();
                  mRecentToolPosition = mGCodeGen.GetLastToolHeadPosition ().Item1;
                  mGCodeGen.FinalizeNotchToolingBlock (mToolingItem, mBlockCutLength, mTotalToolingsCutLength);
               }
               break;
            case NotchSectionType.MachineToolingReverse: {
                  if (notchSequence.mStartIndex < notchSequence.mEndIndex)
                     throw new Exception ("In WriteNotch: MachineToolingReverse : startIndex < endIndex");
                  mGCodeGen.InitializeNotchToolingBlock (mToolingItem, prevToolingItem: null, mSegments, mSegments[notchSequence.mStartIndex].Vec0,
                     mXStart, mXPartition, mXEnd, isFlexCut: false, ii == mNotchSequences.Count - 1, notchSequence.mStartIndex, notchSequence.mEndIndex, comment: "Notch: Machining Reverse Direction");
                  mGCodeGen.EnableMachiningDirective ();
                  for (int jj = notchSequence.mStartIndex; jj >= notchSequence.mEndIndex; jj--) {
                     mExitTooling = Geom.GetReversedToolingSegment (mSegments[jj], tolerance: mSplit ? 1e-4 : 1e-6);
                     mGCodeGen.WriteCurve (mExitTooling, mToolingItem.Name);
                     mBlockCutLength += mExitTooling.Curve.Length;
                  }
                  mGCodeGen.DisableMachiningDirective ();
                  mRecentToolPosition = mGCodeGen.GetLastToolHeadPosition ().Item1;
                  mGCodeGen.FinalizeNotchToolingBlock (mToolingItem, mBlockCutLength, mTotalToolingsCutLength);
               }
               break;
            case NotchSectionType.MachineFlexToolingReverse: {
                  if (notchSequence.mStartIndex < notchSequence.mEndIndex) throw new Exception ("In WriteNotchGCode: MachineFlexToolingReverse : startIndex < endIndex");
                  mGCodeGen.InitializeNotchToolingBlock (mToolingItem, prevToolingItem: null, mSegments, mSegments[notchSequence.mStartIndex].Vec0,
                     mXStart, mXPartition, mXEnd, isFlexCut: true, ii == mNotchSequences.Count - 1, notchSequence.mStartIndex, notchSequence.mEndIndex, circularMotionCmd: false, "Notch: Flex machining Reverse Direction");
                  mGCodeGen.EnableMachiningDirective ();
                  for (int jj = notchSequence.mStartIndex; jj >= notchSequence.mEndIndex; jj--) {
                     var segment = Geom.GetReversedToolingSegment (mSegments[jj], tolerance: mSplit ? 1e-4 : 1e-6);
                     mGCodeGen.WriteCurve (segment, mToolingItem.Name);
                     mBlockCutLength += segment.Curve.Length;
                  }
                  mGCodeGen.DisableMachiningDirective ();
                  mRecentToolPosition = mGCodeGen.GetLastToolHeadPosition ().Item1;
                  mGCodeGen.FinalizeNotchToolingBlock (mToolingItem, mBlockCutLength, mTotalToolingsCutLength);
               }
               break;
            case NotchSectionType.MachineFlexToolingForward: {
                  if (notchSequence.mStartIndex > notchSequence.mEndIndex)
                     throw new Exception ("In WriteNotch: MachineFlexToolingForward : startIndex > endIndex");
                  mGCodeGen.InitializeNotchToolingBlock (mToolingItem, prevToolingItem: null, mSegments, mSegments[notchSequence.mStartIndex].Vec0,
                     mXStart, mXPartition, mXEnd, isFlexCut: true, ii == mNotchSequences.Count - 1, notchSequence.mStartIndex, notchSequence.mEndIndex, circularMotionCmd: false,
                     "Notch: Flex machining Forward Direction");
                  mGCodeGen.EnableMachiningDirective ();
                  for (int jj = notchSequence.mStartIndex; jj <= notchSequence.mEndIndex; jj++) {
                     mGCodeGen.WriteCurve (mSegments[jj], mToolingItem.Name);
                     mBlockCutLength += mSegments[jj].Curve.Length;
                  }
                  mGCodeGen.DisableMachiningDirective ();
                  mRecentToolPosition = mGCodeGen.GetLastToolHeadPosition ().Item1;
                  mGCodeGen.FinalizeNotchToolingBlock (mToolingItem, mBlockCutLength, mTotalToolingsCutLength);
               }
               break;
            case NotchSectionType.MoveToMidApproach: {
                  Point3 prevEndPoint = mExitTooling.Curve.End;
                  Vector3 PrevEndNormal = mExitTooling.Vec1.Normalized ();
                  mGCodeGen.MoveToRetract (prevEndPoint, PrevEndNormal, mToolingItem.Name);
                  mGCodeGen.MoveToNextTooling (PrevEndNormal, mExitTooling, nMid2, notchApproachStNormal,
                     "Moving from one end of tooling to mid of tooling",
                     "", false, isMark:false);
                  mGCodeGen.MoveToMachiningStartPosition (nMid2, notchApproachStNormal, mToolingItem.Name);
                  mRecentToolPosition = nMid2;
               }
               break;
            default:
               throw new Exception ("Undefined notch sequence");
         }
      }
   }

   public void WriteEdgeNotch () {
      foreach (var seg in mSegments) {
         mGCodeGen.EnableMachiningDirective ();
         mGCodeGen.WriteCurve (seg, mToolingItem.Name);
         mGCodeGen.DisableMachiningDirective ();
      }
      Exit = mSegments[^1];
   }
   #endregion

   #region Getters / Predicates
   /// <summary>
   /// This method computes a list of tuples representing the start and end indices of the tooling
   /// segments that occur on the Flex.
   /// </summary>
   /// <param name="segs">The input list of tooling segments.</param>
   /// <returns>A list of tuples, where each tuple contains the start and end indices of the tooling
   /// segments that occur on the Flex. The method assumes that there are two flex toolings on the notch tooling.</returns>
   public static List<Tuple<int, int>> GetFlexSegmentIndices (List<ToolingSegment> segs) {
      // Find the flex segment indices
      List<Tuple<int, int>> flexIndices = [];
      int flexStartIndex = -1, flexEndIndex;
      for (int ii = 0; ii < segs.Count; ii++) {
         var (_, stNormal, endNormal) = segs[ii];
         if (Utils.IsToolingOnFlex (stNormal, endNormal)) {
            if (flexStartIndex == -1) flexStartIndex = ii;
         } else if (flexStartIndex != -1) {
            flexEndIndex = ii - 1;
            var indxes = new Tuple<int, int> (flexStartIndex, flexEndIndex);
            flexIndices.Add (indxes);
            flexStartIndex = -1;
         }
      }
      return flexIndices;
   }

   /// <summary>
   /// This method is used to find of the notch occurs only on the endge of the 
   /// part. This case is mostly for testing purpose.
   /// </summary>
   /// <param name="bound">The bound3d of the entire part or the toling of the notch 
   /// based on the need</param>
   /// <param name="toolingItem">The tooling item</param>
   /// <param name="percentPos">The positions of the points occuring in the interested order</param>
   /// <param name="notchApproachLength">The notch approach length</param>
   /// <param name="leastCurveLength">The practical least length of the curve that can 
   /// <returns></returns>
   public static bool IsEdgeNotch (Bound3 bound, Tooling toolingItem,
      double[] percentPos, double notchApproachLength, double leastCurveLength) {
      var attrs = GetNotchApproachParams (bound, toolingItem, percentPos, notchApproachLength, leastCurveLength);
      if (toolingItem.IsNotch () && attrs.Count == 0) return true;
      return false;
   }

   /// <summary>
   /// A predicate method that returns if the given "notchPoint" is within the 
   /// flex section of tooling, considering a minimum thershold "minThresholdLenFromNPToFlexPt"
   /// outside of flex also as inside
   /// </summary>
   /// <param name="flexIndices">The list of flexe indices where each item is a tuple 
   /// of start and end index in the tooling segments</param>
   /// <param name="segs">The input tooling segments</param>
   /// <param name="notchPoint">The input notch point</param>
   /// <param name="minThresholdLenFromNPToFlexPt">The minimum threshold distance of the 
   /// notch point from the nearest flex start/end point, even if outside, is considered
   /// to be inside.</param>
   /// <returns>A tuple of bool: if the notch point is within the flex, 
   /// Start Index and End Index</returns>
   (bool IsWithinAnyFlex, int StartIndex, int EndIndex) IsPointWithinFlex (List<Tuple<int, int>> flexIndices, List<ToolingSegment> segs, Point3 notchPoint,
      double minThresholdLenFromNPToFlexPt) {
      bool isWithinAnyFlex = false;
      int stIndex = -1, endIndex = -1;
      foreach (var flexIdx in flexIndices) {
         var flexToolingLen = Utils.GetLengthBetweenTooling (segs, flexIdx.Item1, flexIdx.Item2);
         var lenNPToFlexStPt = Utils.GetLengthBetweenTooling (segs, notchPoint, segs[flexIdx.Item1].Curve.Start);
         var lenNPToFlexEndPt = Utils.GetLengthBetweenTooling (segs, notchPoint, segs[flexIdx.Item2].Curve.End);
         var residue = lenNPToFlexStPt + lenNPToFlexEndPt - flexToolingLen;
         if (lenNPToFlexStPt < minThresholdLenFromNPToFlexPt || lenNPToFlexEndPt < minThresholdLenFromNPToFlexPt ||
            Math.Abs (residue).EQ (0, 1e-2)) {
            isWithinAnyFlex = true;
            stIndex = flexIdx.Item1; endIndex = flexIdx.Item2;
            break;
         }
      }
      return new (isWithinAnyFlex, stIndex, endIndex);
   }

   /// <summary>
   /// This method computes the total machinable length of a notch with approach.
   /// Note: A notch with approach is that notch that does not occur on the part's 
   /// edge.
   /// </summary>
   /// <param name="bound">The bound3d of the entire part or the toling of the notch 
   /// based on the need</param>
   /// <param name="toolingItem">The notch tooling item</param>
   /// <param name="percentPos">The array of percentages at which notch points are desired</param>
   /// <param name="notchWireJointDistance">The gap that is intended to make the sheet metal hold up
   /// even after the cut, which shall require a little physical force to break away the scrap side</param>
   /// <param name="notchApproachLength">The length of the laser cutting line length that is desired to
   /// tread before the tooling segment is reached to cut</param>
   /// <param name="leastCurveLength">The least length of the curve (0.5 ideally) below which it is 
   /// assumed that there is no curve</param>
   /// <returns>The overall length of the cut (this includes tooling and other cutting strokes for approach etc.)</returns>
   public static double GetTotalNotchToolingLength (Bound3 bound, Tooling toolingItem,
      double[] percentPos, double notchWireJointDistance, double notchApproachLength, double leastCurveLength) {
      var attrs = GetNotchApproachParams (bound, toolingItem, percentPos, notchApproachLength, leastCurveLength);
      double totalMachiningLength = 0;

      // Computation of total machining length
      var outwardVec = attrs[1].Item3;
      var outwardVecDir = outwardVec.Normalized ();

      // For gambit move from @50
      totalMachiningLength += 2 * 2; // Two times 2.0 length

      // For notch approach dist 
      int notchApproachDistCount = 0;

      // For notch approach cut ( entry)
      notchApproachDistCount += 2;
      int wireJointDistCount = 0;

      // For flexes: Subtract wirejointDist count one for each flex if wireJointDist > 0.5
      // Each wire joint trace at flex has one notchApproachDistCount added
      var segs = toolingItem.Segs.ToList ();
      var flexIndices = GetFlexSegmentIndices (segs);
      if (flexIndices.Count > 0) {
         if (notchWireJointDistance > 0.5) wireJointDistCount -= 2;
         notchApproachDistCount += 2;
         if (flexIndices.Count > 1) {
            notchApproachDistCount += 2;
            if (notchWireJointDistance > 0.5) wireJointDistCount -= 2;
         }
      }

      // Assuming that 25% and 75% cut length wire joint traces
      // exist ( wireJointDistance is not zero) and are outside 
      // the flexes.
      wireJointDistCount -= 2;
      notchApproachDistCount += 2;

      // Account for totalCutLength from above counts
      totalMachiningLength += (notchApproachDistCount * notchApproachLength);
      totalMachiningLength += (wireJointDistCount * notchWireJointDistance);

      // To account for notch approach
      Point3 notchPointAt50pc = attrs[1].Item1;
      Point3 nMid1 = notchPointAt50pc + outwardVec * 0.5;
      Point3 nMid2 = notchWireJointDistance > 0.5 ? nMid1 - outwardVecDir * notchWireJointDistance : nMid1 - outwardVecDir * 2.0;

      // nMid1 to end of the part along the outward vector
      totalMachiningLength += nMid1.DistTo (notchPointAt50pc + outwardVec);

      // Two times tracing from nMid2 (inside) to the 50% lengthed segment's end, one for 
      // initial approach and the other for re-entry
      totalMachiningLength += 2 * nMid2.DistTo (attrs[1].Item1);

      // Add the length of all the tooling segment of the notch
      foreach (var (crv, _, _) in segs) totalMachiningLength += crv.Length;
      return totalMachiningLength;
   }

   /// <summary>
   /// This method is used to compute the entry point to the notch tooling.
   /// Unlike the other features such as holes etc., where the entry is the 
   /// first segment's starting point in the list of tooling segments, Notch
   /// is handled differently, where the entry is not on the tooling or on any edge.
   /// It is an approximate midpoint from the point at 50% of the length of the tooling
   /// to the end point on the nearest boundary direction.
   /// </summary>
   /// <param name="bound">The bound of the tooling item</param>
   /// <param name="toolingItem">The input tooling item of this notch</param>
   /// <param name="percentPos">The positions of the points occuring in the interested order</param>
   /// <param name="notchApproachLength">Notch Approach length</param>
   /// <param name="curveLeastLength">The least count of the curve length</param>
   /// <returns>A point and the vector, where the point is the wirejointDistance offset
   /// from the approximate mid point of the segment FROM 50% distance of the tooling segments TO
   /// the nearest boundary on the flange. The vector is the flamnge normal at the above point</returns>
   public static ValueTuple<Point3, Vector3> GetNotchEntry (Bound3 bound, Tooling toolingItem,
      double[] percentPos, double notchApproachLength, double wireJointDistance, double curveLeastLength = 0.5) {
      List<Tuple<Point3, Vector3, Vector3>> attrs = [];
      var segs = toolingItem.Segs.ToList ();
      if (!toolingItem.IsNotch ()) return new ValueTuple<Point3, Vector3> (segs[0].Curve.Start, segs[0].Vec0);
      Point3[] notchPoints;
      int[] segIndices;
      (segIndices, notchPoints) = ComputeNotchPointOccuranceParams (segs, percentPos, curveLeastLength);
      List<int> invalidSegIndices = [];
      Utils.MarkfeasibleSegments (ref segs);
      for (int ii = 0; ii < 3; ii++)
         if (!segs[segIndices[ii]].IsValid) invalidSegIndices.Add (ii);
      var notchPointsInfo = GetNotchPointsInfo (segIndices, notchPoints, percentPos.Length);

      // Split the curves and modify the indices and segments in segments and
      // in notchPointsInfo
      SplitToolingSegmentsAtPoints (ref segs, ref notchPointsInfo);
      var notchAttrs = GetNotchAttributes (ref segs, ref notchPointsInfo, bound, toolingItem);
      foreach (var notchAttr in notchAttrs) {
         var (_, _, endNormal, _, ToNearestBdyVec, _, _) = notchAttr;
         var approachEndPoint = notchAttr.Item1.End;
         if (ToNearestBdyVec.Length > notchApproachLength - Utils.EpsilonVal) {
            var res = new Tuple<Point3, Vector3, Vector3> (approachEndPoint + ToNearestBdyVec, endNormal,
               ToNearestBdyVec);
            attrs.Add (res);
         } else {
            attrs.Clear ();
            break;
         }
      }
      if (attrs.Count > 0) {
         int approachSegIndex;
         if (!invalidSegIndices.Contains (1)) approachSegIndex = 1;
         else if (!invalidSegIndices.Contains (0)) approachSegIndex = 0;
         else if (invalidSegIndices.Contains (2)) approachSegIndex = 2;
         else throw new Exception ("Notch Indices are invalid for all of 25, 50 and 75% of notch lengths");

         // @Notchpoint at aporoach
         var (n1, _, _, _, _, _) = GetNotchApproachPositions
            (toolingItem, segs, notchAttrs, bound, approachSegIndex, wireJointDistance);
         return new ValueTuple<Point3, Vector3> (n1, notchAttrs[1].Item2.Normalized ());
      } else return new ValueTuple<Point3, Vector3> (segs[0].Curve.Start, segs[0].Vec0);
   }

   /// <summary>
   /// This method is used to compute the entry point to the notch tooling.
   /// Unlike the other features such as holes etc., where the entry is the 
   /// first segment's starting point in the list of tooling segments, Notch
   /// is handled differently, where the entry is not on the tooling or on any edge.
   /// It is an approximate midpoint from the point at 50% of the length of the tooling
   /// to the end point on the nearest boundary direction.
   /// </summary>
   /// <returns>A point and the vector, where the point is the wirejointDistance offset
   /// from the approximate mid point of the segment FROM 50% distance of the tooling segments TO
   /// the nearest boundary on the flange. The vector is the flamnge normal at the above point</returns>
   public ValueTuple<Point3, Vector3> GetNotchEntry () {
      if (EdgeNotch) {
         var (curve, stNoral, _) = mToolingItem.Segs.ToList ().First ();
         return new ValueTuple<Point3, Vector3> (curve.Start, stNoral);
      } else
         return Notch.GetNotchEntry (mFullPartBound, mToolingItem, mPercentLength, mNotchApproachLength,
            mNotchWireJointDistance, mCurveLeastLength);
   }

   /// <summary>
   /// This is an utility method that creates from the indices of segments and notch points array to 
   /// NotchPointsInfo data strcuture to ascertain the uniqueness of the segment's index against a (only one)
   /// point after the input tooling segment is split.
   /// </summary>
   /// <param name="segIndices">An array of indices at which the notch points occur</param>
   /// <param name="notchPoints">The array of notch points</param>
   /// <param name="count">The counter: 0 means 25%, 1 means 50% and 2 means 75%</param>
   /// <returns>A list of the NotchPointsInfo that contains the unique set of index against
   /// the notch point</returns>
   public static List<NotchPointInfo> GetNotchPointsInfo (int[] segIndices, Point3[] notchPoints, int count) {
      List<NotchPointInfo> notchPointsInfo = [];
      for (int ii = 0; ii < count; ii++) {
         notchPointsInfo.FindIndex (np => np.mSegIndex == segIndices[ii]);

         // Find the notch point with the specified segIndex
         var index = notchPointsInfo.FindIndex (np => np.mSegIndex == segIndices[ii]);
         if (index != -1) notchPointsInfo[index].mPoints.Add (notchPoints[ii]);
         else {
            NotchPointInfo np = new (segIndices[ii], notchPoints[ii], ii == 0 ? 25 : (ii == 1 ? 50 : 75),
               ii == 0 ? "@25" : (ii == 1 ? "@50" : "@75"));
            notchPointsInfo.Add (np);
         }
      }
      return notchPointsInfo;
   }

   /// <summary>
   /// This method computes the vital notch parameters such as the points, normals and the
   /// direction to the nearest boundary at all the interested positions in the notch
   /// </summary>
   /// <param name="model">Model is used to get the bounds of the tooling</param>
   /// <param name="toolingItem">The input tooling item</param>
   /// <param name="percentPos">The positions of the interested points at lengths</param>
   /// <param name="notchApproachDistance">The approach distance for the notch</param>
   /// <param name="curveLeastLength">The least length of the curve, below which the curve data is 
   /// removed</param>
   /// <returns>A list of tuples that contain the notch point, normal at the point
   /// and the direction to the nearest boundary</returns>
   public static List<Tuple<Point3, Vector3, Vector3>> GetNotchApproachParams (Bound3 bound, Tooling toolingItem,
      double[] percentPos, double notchApproachDistance, double curveLeastLength) {
      List<Tuple<Point3, Vector3, Vector3>> attrs = [];
      var segs = toolingItem.Segs.ToList ();
      if (!toolingItem.IsNotch ()) return attrs;
      Point3[] notchPoints;
      int[] segIndices;
      (segIndices, notchPoints) = ComputeNotchPointOccuranceParams (segs, percentPos, curveLeastLength);
      var notchPointsInfo = GetNotchPointsInfo (segIndices, notchPoints, percentPos.Length);

      // Split the curves and modify the indices and segments in segments and
      // in notchPointsInfo
      SplitToolingSegmentsAtPoints (ref segs, ref notchPointsInfo);
      var notchAttrs = GetNotchAttributes (ref segs, ref notchPointsInfo, bound, toolingItem);
      foreach (var notchAttr in notchAttrs) {
         var (_, _, endNormal, _, ToNearestBdyVec, _, _) = notchAttr;
         var approachEndPoint = notchAttr.Item1.End;
         if (ToNearestBdyVec.Length > /*notchApproachDistance*/1.0 - Utils.EpsilonVal) { // TODO Revisit for notchApproachDistance
            var res = new Tuple<Point3, Vector3, Vector3> (approachEndPoint + ToNearestBdyVec, endNormal, ToNearestBdyVec);
            attrs.Add (res);
         } else {
            attrs.Clear ();
            break;
         }
      }
      return attrs;
   }

   /// <summary>
   /// This method is used to obtain the direction in which the notch
   /// shall be machined upon the first entry. 
   /// </summary>
   /// <param name="segs">The input segments of the tooling</param>
   /// <returns>True if machining be in the forward direction. False otherwise.</returns>
   bool IsForwardFirstNotchTooling (List<ToolingSegment> segs) {
      bool forwardNotchTooling;
      if (segs[0].Curve.Start.X - mBound.XMin < mBound.XMax - segs[0].Curve.Start.X) {
         if (segs[^1].Curve.End.X < segs[0].Curve.Start.X) forwardNotchTooling = true;
         else forwardNotchTooling = false;
      } else {
         if (segs[^1].Curve.End.X > segs[0].Curve.Start.X) forwardNotchTooling = true;
         else forwardNotchTooling = false;
      }
      return forwardNotchTooling;
   }

   /// <summary>
   /// This method is used to compute all the notch attributes given the 
   /// the input List of NotchPointInfo. 
   /// Important note: Before calling this method, the input tooling segments are to be split
   /// at the occurance of the notch points. The expected output of this pre-step is to have a data strcuture 
   /// (NotchPOintInfo) where each one has the segmet index and only one point at that index. 
   /// The tooling segments are split in such a way that the notch points or any other characteristic points
   /// are at the end of the index-th segment.
   /// </summary>
   /// <param name="segments">The input list of tooling segments</param>
   /// <param name="notchPointsInfo">The List of NotchPointInfo where each item as exactly one
   /// index of the segment in the list and only one point, which should be the end point of 
   /// the index-th segment in the tooling segments list</param>
   /// <param name="bound">The bound of the tooling item</param>
   /// <param name="toolingItem">The tooling item.</param>
   /// <returns></returns>
   /// <exception cref="Exception">An exception is thrown if the pre-step to split the tooling segments is not 
   /// made. This is checked if each of the NotchPointInfo has only one point for the index (of the segment)</exception>
   public static List<NotchAttribute> GetNotchAttributes (ref List<ToolingSegment> segments,
      ref List<NotchPointInfo> notchPointsInfo, Bound3 bound, Tooling toolingItem) {
      List<NotchAttribute> notchAttrs = [];

      // Assertion that each notch point info should have only one point after split
      // The notch point of the segIndex-th segment is the end point of the segment
      for (int ii = 0; ii < notchPointsInfo.Count; ii++) {
         if (notchPointsInfo[ii].mSegIndex == -1) continue;
         var pts = notchPointsInfo[ii].mPoints;
         if (pts.Count != 1) throw new Exception ($"GetNotchAttributes: List<NotchPointInfo> notchPointsInfo {ii}th indexth points size != 1");
      }

      // Compute the notch attributes
      for (int ii = 0; ii < notchPointsInfo.Count; ii++) {
         var newNotchAttr = ComputeNotchAttribute (bound, toolingItem, segments, notchPointsInfo[ii].mSegIndex, notchPointsInfo[ii].mPoints[0]);
         notchAttrs.Add (newNotchAttr);
      }
      return notchAttrs;
   }
}
#endregion