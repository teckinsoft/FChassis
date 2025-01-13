using Flux.API;
using FChassis.Core;
using static FChassis.Utils;
using System.Windows.Documents;
using System.Collections.Generic;
using System.Net;

namespace FChassis.GCodeGen;

/// <summary>
/// This class represents the feature which is a tooling, whose curve is 
/// closed and the curve exists on more than one plane through flex section.
/// Note: If the curve is open, it is Notch
/// </summary>
public class CutOut : Feature {
   #region Constructor
   public CutOut (GCodeGenerator gcgen, Tooling toolingItem,
      Tooling prevToolingItem, List<ToolingSegment> prevSegs, ToolingSegment? prevToolingSegment,
      EPlane prevPlaneType, double xStart, double xPartition, double xEnd,
       double notchApproachLength, double prevCutToolingsLength, double prevMarkToolingsLength,
            double totalMarkLength, double totalToolingCutLength, bool isFirstTooling, bool featureToBeTreatedAsCutout) {
      mFeatureToBeTreatedAsCutout = featureToBeTreatedAsCutout;
      GCGen = gcgen;
      ToolingItem = toolingItem; // Initialize the Tooling property here
      mPrevToolingItem = prevToolingItem;
      mPrevToolingSegs = prevSegs;
      mXStart = xStart; mXPartition = xPartition; mXEnd = xEnd;
      mIsFirstTooling = isFirstTooling;
      PreviousToolingSegment = prevToolingSegment;
      mPrevCutToolingsLength = prevCutToolingsLength;
      mPrevMarkToolingsLength = prevMarkToolingsLength;
      mTotalMarkLength = totalMarkLength;
      mTotalToolingCutLength = totalToolingCutLength;
      NotchApproachLength = notchApproachLength;
      mPrevPlane = prevPlaneType;
      PerformToolingSegmentation ();
   }
   #endregion

   #region Properties
   public GCodeGenerator GCGen { get; set; }
   public override List<ToolingSegment> ToolingSegments { get; set; }
   public override ToolingSegment? GetMostRecentPreviousToolingSegment () => PreviousToolingSegment;
   public Tooling ToolingItem {
      get => mT;
      set {
         // Null check to avoid NullReferenceException
         if (value == null) {
            throw new ArgumentNullException (nameof (value), "Tooling cannot be null.");
         }

         // Check if the new value is a valid Cutout
         if (!value.IsCutout () && !mFeatureToBeTreatedAsCutout)
            throw new Exception ("Not a Cutout object");

         // Assign value only if it's different from the current value
         if (mT != value) {
            mT = value;
         }
      }
   }
   public ToolingSegment Exit { get => mExitTooling; set => mExitTooling = value; }
   public ToolingSegment? PreviousToolingSegment { get; private set; }
   public double NotchApproachLength { get; set; }
   #endregion

   #region Data members
   Tooling mPrevToolingItem;
   List<ToolingSegment> mPrevToolingSegs;
   double mXStart, mXPartition, mXEnd;
   bool mIsFirstTooling;
   double mPrevCutToolingsLength, mPrevMarkToolingsLength,
            mTotalMarkLength, mTotalToolingCutLength;
   List<NotchSequenceSection> mCutOutBlocks = [];
   List<(List<ToolingSegment> Segs, bool FlexSegs)> mSegValLists = [];
   Tooling mT;
   Point3 mMostRecentPrevToolPosition;
   EPlane mPrevPlane;
   ToolingSegment mExitTooling;
   double mBlockCutLength = 0;
   double mTotalToolingsCutLength = 0;
   List<Point3> mPreWJTPts = [];
   bool mFeatureToBeTreatedAsCutout = false;
   #endregion

   #region Preprocessors
   /// <summary>
   /// Sanity checker for the cutting blocks. It verifies the following conditions:
   /// <list type="bullet">
   ///   <item>
   ///      <description>
   ///         The wire joint segment referred to by the cut block has 
   ///         approximately 2.0 units in the actual Tooling Segments.
   ///      </description>
   ///   </item>
   ///   <item>
   ///      <description>
   ///         The start index of the nth cutting block shall be less than or 
   ///         equal to the end index of the nth cutting block.
   ///      </description>
   ///   </item>
   ///   <item>
   ///      <description>
   ///         The end index of the nth cutting block shall always be less than 
   ///         the end index of the (n+1)th cutting block.
   ///      </description>
   ///   </item>
   /// </list>
   /// </summary>
   /// <exception cref="Exception">Throws an exception if any of the conditions fail.</exception>
   void CheckSanityOfCutOutBlocks () {
      for (int ii = 0; ii < mCutOutBlocks.Count; ii++) {
         // Check if the wire joint segment from index of cutout block 
         // is indeed approximately 2.0 units
         if (mCutOutBlocks[ii].SectionType == NotchSectionType.WireJointTraceJumpForward ||
            mCutOutBlocks[ii].SectionType == NotchSectionType.WireJointTraceJumpForwardOnFlex) {
            if (mCutOutBlocks[ii].StartIndex != mCutOutBlocks[ii].EndIndex)
               throw new Exception ("WJT start index is not equal to end");
            if (ToolingSegments[mCutOutBlocks[ii].StartIndex].Curve.Length.SLT (2.0, 0.1)) {
               int stIndex = mCutOutBlocks[ii].StartIndex;
               var llen = ToolingSegments[mCutOutBlocks[ii].StartIndex].Curve.Length;
               double cumLen = llen;
               var tn = ToolingItem.Name;
               var sIdx = stIndex;
               while (stIndex - 1 >= 0 && cumLen.SLT (2.0)) {
                  cumLen += ToolingSegments[stIndex - 1].Curve.Length;
                  if (Math.Abs (cumLen - 2.0).EQ (0, 0.1))
                     break;
                  if (Math.Abs (cumLen - 2.0).SGT (0.1))
                     throw new Exception ("WJT length != 2.0");
                  stIndex--;
               }
            } else {
               if (Math.Abs (ToolingSegments[mCutOutBlocks[ii].StartIndex].Curve.Length - 2.0).GTEQ (0.4)) {
                  throw new Exception ("WJT length != 2.0");
               }
            }


            //throw new Exception ("WJT length != 2.0");
         }


         // The start index of ii-th cutting block shall be lesser than or equal to ii-th end index
         // The end index of ii-th cutting block shall always be < the ii+1-th cutting block's end index
         if (ii + 1 < mCutOutBlocks.Count) {
            if (mCutOutBlocks[ii].StartIndex > mCutOutBlocks[ii].EndIndex)
               throw new Exception ("ii-th Start Index > ii - th End Index");
            if (mCutOutBlocks[ii].EndIndex >= mCutOutBlocks[ii + 1].StartIndex)
               throw new Exception ("ii-th End Index >= ii+1 - th Start Index");
         }

         if (mCutOutBlocks[ii].StartIndex >= ToolingSegments.Count)
            throw new Exception ("start index is greater than ToolingSegments count");
      }
   }
   void DoMachiningSegmentationForFlangesAndFlex () {
      mSegValLists = [];
      ToolingSegments = GCGen.GetSegmentsAccountedForApproachLength (ToolingItem);
      if (ToolingSegments == null || ToolingSegments.Count == 0)
         throw new Exception ("Segments accounted for entry is null");

      // ** Move to the Start point of the tooling whose normal is + Z axis **
      // The first tooling segment has to be on the web flange. UNless, rotate the list
      // by one element until the first element is on the web flange
      while (!ToolingSegments[0].Vec0.Normalized ().EQ (XForm4.mZAxis)) {
         var ts = ToolingSegments[^1];
         ToolingSegments.RemoveAt (ToolingSegments.Count - 1);
         ToolingSegments.Insert (0, ts);
      }

      // ** Create cutout blocks for machining on plane and on flex **
      mCutOutBlocks = [];
      var flexSegIndices = Notch.GetFlexSegmentIndices (ToolingSegments);
      int startIdx = -1, endIdx = -1;
      int flexCount = 0;
      NotchSequenceSection cb;
      bool isFlexToolingFinal = false;
      for (int ii = 0; ii < ToolingSegments.Count; ii++) {
         if (flexSegIndices.Count > flexCount) {
            isFlexToolingFinal = true;
            if (ii < flexSegIndices[flexCount].Item1) {
               if (startIdx == -1)
                  startIdx = ii;
            }
            if (ii == flexSegIndices[flexCount].Item1) {
               if (startIdx != -1)
                  endIdx = ii - 1;
               if (startIdx != -1 && endIdx != -1) {
                  cb = new NotchSequenceSection (startIdx, endIdx, NotchSectionType.MachineToolingForward) {
                     Flange = Utils.GetArcPlaneFlangeType (ToolingSegments[ii].Vec0, GCGen.GetXForm ())
                  };
                  mCutOutBlocks.Add (cb);
                  startIdx = flexSegIndices[flexCount].Item1;
                  endIdx = -1;
               }
            }
            if (ii == flexSegIndices[flexCount].Item2) {
               if (startIdx != -1)
                  endIdx = ii;
               if (startIdx != -1 && endIdx != -1) {
                  cb = new NotchSequenceSection (startIdx, endIdx, NotchSectionType.MachineFlexToolingForward) {
                     Flange = Utils.GetArcPlaneFlangeType (ToolingSegments[ii].Vec0, GCGen.GetXForm ())
                  };
                  mCutOutBlocks.Add (cb);
                  endIdx = -1;
                  startIdx = -1;
               }
               flexCount++;
            }
         } else {
            if (startIdx == -1) {
               startIdx = ii;
               isFlexToolingFinal = false;
            }
         }
      }
      endIdx = ToolingSegments.Count - 1;
      if (isFlexToolingFinal)
         cb = new NotchSequenceSection (startIdx, endIdx, NotchSectionType.MachineFlexToolingForward);
      else
         cb = new NotchSequenceSection (startIdx, endIdx, NotchSectionType.MachineToolingForward);

      cb.Flange = Utils.GetArcPlaneFlangeType (ToolingSegments[^1].Vec0, GCGen.GetXForm ());
      mCutOutBlocks.Add (cb);
      CheckSanityOfCutOutBlocks ();
   }

   void DoWireJointJumpTraceSegmentationForFlex () {
      // ** Add Wire joint sections in the cutout blocks at the start and end of the flex machining **
      double wjtLenAtFlex = GCGen.NotchWireJointDistance;
      if (wjtLenAtFlex < 0.5) wjtLenAtFlex = 2.0;
      for (int ii = 0; ii < mCutOutBlocks.Count; ii++) {
         bool nextBlockStartFlexMc = false;
         if (ii + 1 < mCutOutBlocks.Count)
            nextBlockStartFlexMc = mCutOutBlocks[ii].SectionType ==
               NotchSectionType.MachineToolingForward && mCutOutBlocks[ii + 1].SectionType == NotchSectionType.MachineFlexToolingForward;

         bool nextBlockStartMc = false;
         if (ii + 1 < mCutOutBlocks.Count)
            nextBlockStartMc = mCutOutBlocks[ii].SectionType ==
               NotchSectionType.MachineFlexToolingForward && mCutOutBlocks[ii + 1].SectionType == NotchSectionType.MachineToolingForward;

         bool needWJTForLastSection = false;
         if (ii - 1 >= 0 && mCutOutBlocks[ii].SectionType == NotchSectionType.MachineToolingForward &&
            mCutOutBlocks[ii - 1].SectionType == NotchSectionType.MachineFlexToolingForward)
            needWJTForLastSection = true;

         if (ii + 1 < mCutOutBlocks.Count && (nextBlockStartFlexMc || nextBlockStartMc || needWJTForLastSection)) {
            int mcBlockEndIndex = -1;
            if (nextBlockStartFlexMc)
               mcBlockEndIndex = mCutOutBlocks[ii].EndIndex;
            else if (nextBlockStartMc)
               mcBlockEndIndex = mCutOutBlocks[ii].EndIndex;
            bool revTrace = false;
            if (nextBlockStartFlexMc) {
               revTrace = true;
            }
            var (wjtPtAtFlex, segIndexToSplit) = Geom.EvaluatePointAndIndexAtLength (ToolingSegments, mcBlockEndIndex,
               wjtLenAtFlex, reverseTrace: revTrace);
            var splitToolSegs = Utils.SplitToolingSegmentsAtPoint (ToolingSegments, segIndexToSplit, wjtPtAtFlex,
            ToolingSegments[mcBlockEndIndex].Vec0.Normalized ());
            if (splitToolSegs.Count == 2) {
               var cutoutSegs = ToolingSegments;
               Notch.MergeSegments (ref splitToolSegs, ref cutoutSegs, segIndexToSplit);
               ToolingSegments = cutoutSegs;
               NotchSequenceSection cb;
               if (nextBlockStartFlexMc) {
                  cb = new NotchSequenceSection (segIndexToSplit + 1, segIndexToSplit + 1, NotchSectionType.WireJointTraceJumpForwardOnFlex) {
                     Flange = Utils.GetArcPlaneFlangeType (ToolingSegments[mcBlockEndIndex].Vec0.Normalized (), XForm4.IdentityXfm)
                  };
                  mCutOutBlocks.Insert (ii + 1, cb);
               } else if (nextBlockStartMc) {
                  cb = new NotchSequenceSection (segIndexToSplit, segIndexToSplit, NotchSectionType.WireJointTraceJumpForwardOnFlex) {
                     Flange = Utils.GetArcPlaneFlangeType (ToolingSegments[mcBlockEndIndex].Vec0.Normalized (), XForm4.IdentityXfm)
                  };
                  mCutOutBlocks.Insert (ii + 1, cb);
               }
               for (int jj = ii + 2; jj < mCutOutBlocks.Count; jj++) {
                  var cutoutblk = mCutOutBlocks[jj];
                  cutoutblk.StartIndex += 1;
                  cutoutblk.EndIndex += 1;
                  mCutOutBlocks[jj] = cutoutblk;
               }
            }
         }
      }
      CheckSanityOfCutOutBlocks ();
   }

   public static bool ToTreatAsCutOut (List<ToolingSegment> segs, Bound3 fullPartBound, double minCutOutLengthThreshold) {

      // condition to introduce wire joints on web flange
      bool toIntroduceWJT = false;
      var segBounds = Utils.GetToolingSegmentsBounds (segs, fullPartBound);
      if (Math.Abs ((double)(segBounds.YMax)).GTEQ (minCutOutLengthThreshold) ||
         Math.Abs ((double)(segBounds.YMin)).GTEQ (minCutOutLengthThreshold)) {
         foreach (var seg in segs) {
            if (Utils.GetArcPlaneFlangeType (seg.Vec0, XForm4.IdentityXfm) != EFlange.Web)
               return false;
         }
         toIntroduceWJT = true;
      }
      return toIntroduceWJT;
   }
   /// <summary>
   /// This method computes the PRE Wirejoint points. These points
   /// are the last points of the tooling block, from which a wire joint
   /// distance is left out. The condition for inserting these PRE Wire Joint
   /// Jump Traces is when the Y Max or Y Min is greater than or equal to 
   /// MinCutOutLengthThreshold (200 mm by default)
   /// </summary>
   /// <exception cref="Exception"></exception>
   void ComputePreWireJointPoints () {
      mPreWJTPts = [];

      // condition to introduce wire joints on web flange
      bool toIntroduceWJT = ToTreatAsCutOut (ToolingSegments, GCGen.Process.Workpiece.Bound, GCGen.MinCutOutLengthThreshold);

      // Create ePoints by filtering and projecting mCutOutBlocks
      var ePoints = mCutOutBlocks
          .Where (block => block.SectionType == NotchSectionType.MachineToolingForward &&
          block.Flange == EFlange.Web && toIntroduceWJT)
          .Select (block => (
              StPoint: ToolingSegments[block.StartIndex].Curve.Start,
              EndPoint: ToolingSegments[block.EndIndex].Curve.End))
          .ToList ();
      if (ePoints.Count == 0)
         return;

      // Validate and populate preWJTPts
      var preWJTPts = new List<Point3> ();
      List<double> distances = [];
      foreach (var (StPoint, EndPoint) in ePoints) {
         // Find the start index
         int eStIdx = ToolingSegments.FindIndex (t => t.Curve.Start.EQ (StPoint!));

         // Validate start index
         if (eStIdx == -1)
            throw new Exception ("Start point not found in ToolingSegments");

         // Find the end index
         int eEndIdx = ToolingSegments.FindIndex (t =>
             t.Curve.End.EQ (EndPoint!) && ToolingSegments.IndexOf (t) > eStIdx);

         // Validate end index
         if (eEndIdx == -1) {
            eEndIdx = ToolingSegments.FindIndex (t =>
             t.Curve.End.EQ (EndPoint!));
            if (eEndIdx == -1)
               throw new Exception ("End point not found in ToolingSegments");
            else if (eEndIdx == 0)
               eEndIdx = eStIdx;
         }

         // Calculate distance
         Point3 modStartPoint;
         double dist;
         if (ToolingSegments.Count == 2 && Utils.IsCircle (ToolingSegments[eEndIdx].Curve)) {
            modStartPoint = ToolingSegments[eEndIdx].Curve.Start;
            dist = ToolingSegments[eEndIdx].Curve.Length;
            dist += ToolingSegments[eStIdx].Curve.Length;
         } else {
            dist = Geom.GetLengthBetween (ToolingSegments, StPoint, EndPoint, inSameOrder: true);
         }
         distances.Add (dist);

         // Populate points
         PopulatePreWJTPts (StPoint, eStIdx, eEndIdx, preWJTPts);
         //preWJTPts.Remove (preWJTPts[^1]);
      }

      //dd.Clear ();
      //Point3 stPt = ToolingSegments.FindIndex (t => Geom.IsPointOnCurve (t.Curve, preWJTPts[ii], t.Vec0));
      //for (int ii = 0; ii < preWJTPts.Count; ii++) {
      //   var dist = Geom.GetLengthBetween (ToolingSegments, StPoint, EndPoint, inSameOrder: true);
      //}
      // ** Get the distance between the st and end points **
      CheckSanityOfCutOutBlocks ();

      var dd = mDistances;
      // ** Segment the ToolingSegments at Pre Wire Joint Points **
      for (int ii = 0; ii < preWJTPts.Count; ii++) {
         var segIndexToSplit = ToolingSegments.FindIndex (t => Geom.IsPointOnCurve (t.Curve, preWJTPts[ii], t.Vec0));
         var cbIdx = mCutOutBlocks.FindIndex (cb => cb.StartIndex <= segIndexToSplit && segIndexToSplit <= cb.EndIndex);

         Point3 stPt;
         if (ii == 0) stPt = ToolingSegments[segIndexToSplit].Curve.Start;
         else stPt = preWJTPts[ii - 1];
         var d = Geom.GetLengthBetween (ToolingSegments, stPt, preWJTPts[ii], inSameOrder: true);

         var splitToolSegs = Utils.SplitToolingSegmentsAtPoint (ToolingSegments, segIndexToSplit, preWJTPts[ii],
                                    ToolingSegments[segIndexToSplit].Vec0.Normalized ());
         if (splitToolSegs.Count == 2) {
            var cutoutSegs = ToolingSegments;
            Notch.MergeSegments (ref splitToolSegs, ref cutoutSegs, segIndexToSplit);
            ToolingSegments = cutoutSegs;

            var cbBlock = mCutOutBlocks[cbIdx];
            var prevEndIndex = cbBlock.EndIndex;
            cbBlock.EndIndex = segIndexToSplit;
            mCutOutBlocks[cbIdx] = cbBlock;

            NotchSequenceSection cb = new (segIndexToSplit + 1, prevEndIndex + 1, NotchSectionType.MachineToolingForward) {
               // Set the flange to wweb. 
               // Note: The wire joint points are computed
               // only for web flange machining segments
               Flange = EFlange.Web
            };
            mCutOutBlocks.Insert (cbIdx + 1, cb);

            int incr = 1; // one for WJT and the other for actual index of the next start
            for (int kk = cbIdx + 2; kk < mCutOutBlocks.Count; kk++) {
               var cutoutblk = mCutOutBlocks[kk];
               cutoutblk.StartIndex += incr;
               cutoutblk.EndIndex += incr;
               mCutOutBlocks[kk] = cutoutblk;
            }
         }
         mPreWJTPts.Add ((preWJTPts[ii]));
         CheckSanityOfCutOutBlocks ();
      }
      CheckSanityOfCutOutBlocks ();
   }

   void DoWireJointJumpTraceSegmentationForFlanges () {
      // ** Compute Pre Wire Joint Points on the tooling segments
      ComputePreWireJointPoints ();
      //var nonWebCOIdx = mCutOutBlocks.FindIndex (co => co.Flange != EFlange.Web);
      //if (nonWebCOIdx != -1) throw new Exception ("In DoWireJointJumpTraceSegmentationForFlanges: One of the CutOut notch section's flange is not Web");
      // ** Create Wire joint jump trace cutout blocks
      for (int ii = 0; ii < mPreWJTPts.Count; ii++) {
         for (int jj = 0; jj < mCutOutBlocks.Count; jj++) {
            var preWJTPtIndex = ToolingSegments.FindIndex (t => t.Curve.End.EQ (mPreWJTPts[ii]));
            if (preWJTPtIndex == -1) throw new Exception ("The index of pre wire joint point in tooling segments is -1");

            if (mCutOutBlocks[jj].StartIndex <= preWJTPtIndex && preWJTPtIndex <= mCutOutBlocks[jj].EndIndex &&
               mCutOutBlocks[jj].SectionType == NotchSectionType.MachineToolingForward) {
               var (wjtPt, segIndexToSplit) = Geom.GetPointAtLengthFrom (mPreWJTPts[ii], GCGen.NotchWireJointDistance, ToolingSegments);
               var splitToolSegs = Utils.SplitToolingSegmentsAtPoint (ToolingSegments, segIndexToSplit, wjtPt,
                                    ToolingSegments[segIndexToSplit].Vec0.Normalized ());
               if (splitToolSegs.Count == 2) {

                  var cutoutSegs = ToolingSegments;
                  Notch.MergeSegments (ref splitToolSegs, ref cutoutSegs, segIndexToSplit);
                  ToolingSegments = cutoutSegs;

                  var stIndex = mCutOutBlocks[jj].StartIndex; var endIndex = mCutOutBlocks[jj].EndIndex;
                  int cbIncr = jj;
                  NotchSequenceSection cb;
                  if (stIndex < segIndexToSplit) {
                     cb = mCutOutBlocks[jj];
                     cb.EndIndex = segIndexToSplit - 1;
                     mCutOutBlocks[jj] = cb;
                     cbIncr++;
                  }

                  cb = new (segIndexToSplit, segIndexToSplit, NotchSectionType.WireJointTraceJumpForward) {
                     Flange = EFlange.Web
                  };

                  mCutOutBlocks.Insert (cbIncr, cb);
                  cbIncr++;
                  int incr = 1; // one for WJT and the other for actual index of the next start
                  incr = segIndexToSplit - mCutOutBlocks[cbIncr].StartIndex + 1;
                  for (int kk = cbIncr; kk < mCutOutBlocks.Count; kk++) {
                     var cutoutblk = mCutOutBlocks[kk];
                     cutoutblk.StartIndex += incr;
                     cutoutblk.EndIndex += incr;
                     mCutOutBlocks[kk] = cutoutblk;
                  }
                  CheckSanityOfCutOutBlocks ();
               } else if (splitToolSegs.Count == 1) {
                  // This means, the segment with index "segIndexToSplit" is the
                  // resulting segment after split. This means, the end point is the
                  // interested point.

                  // So the cut block ending with segIndexToSplit-1 is intact. 
                  // A new cut block with index segIndexToSplit is to be made WireJointTraceJumpForward
                  // and inserted at jj+1

                  // The previous cut block which has index from segIndexToSplit to prevEndIndex
                  // needs to be changed its start index alone to segIndexToSplit+1
                  CheckSanityOfCutOutBlocks ();
                  var nextStartIndex = mCutOutBlocks[jj + 1].StartIndex;
                  var nextEndIndex = mCutOutBlocks[jj + 1].EndIndex;
                  // Reassign the end index of the current cutout block
                  NotchSequenceSection cb;
                  if (nextStartIndex == segIndexToSplit) {
                     cb = new (segIndexToSplit, segIndexToSplit, NotchSectionType.WireJointTraceJumpForward) {
                        Flange = EFlange.Web
                     };
                     mCutOutBlocks.Insert (jj + 1, cb);
                     cb = mCutOutBlocks[jj + 2];
                     cb.StartIndex = segIndexToSplit + 1;
                     mCutOutBlocks[jj + 2] = cb;
                  }
                  if (mCutOutBlocks[jj].StartIndex == segIndexToSplit) {
                     var cbb = mCutOutBlocks[jj];
                     cbb.SectionType = NotchSectionType.WireJointTraceJumpForward;
                     mCutOutBlocks[jj] = cbb;
                  }
                  CheckSanityOfCutOutBlocks ();
               }
               break;
            }
         }
      }
   }

   List<double> mDistances = [];
   // Local function for populating preWJTPts
   void PopulatePreWJTPts (Point3 startPoint, int startIndex, int endIndex, List<Point3> points) {
      var iPoint = startPoint;
      if (startIndex > endIndex) throw new Exception ("Start index > End Index. Wrong!");

      // Compute 25%, 50% and 75% lengths for segments from startIndex to endIndex
      double[] lengths = { 0, 0, 0 };
      double[] percentages = { 0.05, 0.25, 0.5, 0.75 };
      var toolingLengthBetween = Geom.GetLengthBetween (ToolingSegments, startIndex, endIndex);
      for (int ii = 0; ii < percentages.Length; ii++) {
         var percentLength = toolingLengthBetween * percentages[ii];
         var (preWJTPt, segIndexToSplit) = Geom.GetPointAtLengthFrom (iPoint, percentLength, ToolingSegments);
         if (segIndexToSplit == -1 || segIndexToSplit < startIndex) break;
         double dist;
         try {
            var isCircle = Utils.IsCircle (ToolingSegments[segIndexToSplit].Curve);
            //if (isCircle)
            //   iPoint = ToolingSegments[segIndexToSplit].Curve.Start;
            //if (ii == 0) iPoint = startPoint;
            //else iPoint = points[^1];

            dist = Geom.GetLengthBetween (ToolingSegments, iPoint, preWJTPt, inSameOrder: true);
            mDistances.Add (dist);
            //if (percentages[ii].SGT (0.5) && isCircle) {
            //   var (cen, rad) = Geom.EvaluateCenterAndRadius (ToolingSegments[segIndexToSplit].Curve as Arc3);
            //   dist = ToolingSegments[segIndexToSplit].Curve.Length - dist;
            //}
         } catch (Exception) { break; }
         if (segIndexToSplit == -1 || segIndexToSplit > endIndex) {
            if (segIndexToSplit == -1)
               throw new Exception ("In PopulatePreWJTPts: Segment Index to split is -1");
            else
               throw new Exception ("In PopulatePreWJTPts: segIndexToSplit > endIndex");
         }

         //iPoint = preWJTPt;
         points.Add (preWJTPt);
      }
      //while (true) {
      //   var (preWJTPt, segIndexToSplit) = Geom.GetPointAtLengthFrom (iPoint, GCGen.MinNotchLengthThreshold, ToolingSegments);
      //   if (segIndexToSplit == -1 || segIndexToSplit < startIndex) break;
      //   double dist;
      //   try {
      //      dist = Geom.GetLengthBetween (ToolingSegments, iPoint, preWJTPt, inSameOrder: true);
      //   } catch (Exception) { break; }
      //   if (segIndexToSplit == -1 || segIndexToSplit > endIndex || dist < (GCGen.MinNotchLengthThreshold / 2))
      //      break;

      //   iPoint = preWJTPt;
      //   points.Add (preWJTPt);
      //}
   }

   /// <summary>
   /// This is the method that segments ( splits and merges) Tooling Segments 
   /// </summary>
   /// <exception cref="Exception"></exception>
   void PerformToolingSegmentation () {
      if (GCGen.CreateDummyBlock4Master)
         return;
      DoMachiningSegmentationForFlangesAndFlex ();
      DoWireJointJumpTraceSegmentationForFlex ();
      DoWireJointJumpTraceSegmentationForFlanges ();
   }
   #endregion

   #region G Code Writers
   public override void WriteTooling () {
      bool continueMachining = false;
      for (int ii = 0; ii < mCutOutBlocks.Count; ii++) {
         var cutoutSequence = mCutOutBlocks[ii];
         switch (cutoutSequence.SectionType) {
            case NotchSectionType.WireJointTraceJumpForward:
            case NotchSectionType.WireJointTraceJumpForwardOnFlex:
               if (ii == 0) throw new Exception ("CutOut writing starts from Wire Joint Jump Trace, which is wrong");
               Vector3 scrapSideNormal = Utils.GetMaterialRemovalSideDirection (ToolingSegments[cutoutSequence.StartIndex],
                  ToolingSegments[cutoutSequence.StartIndex].Curve.End);
               string comment = "(( ** CutOut: Wire Joint Jump Trace Forward Direction ** ))";
               var refTS = ToolingSegments[cutoutSequence.StartIndex];
               if (cutoutSequence.SectionType == NotchSectionType.WireJointTraceJumpReverse) {
                  refTS = Geom.GetReversedToolingSegment (refTS);
                  comment = "((** CutOut: Wire Joint Jump Trace Reverse Direction ** ))";
               }
               bool isNextSeqFlexMc = (mCutOutBlocks[ii + 1].SectionType == NotchSectionType.MachineFlexToolingForward);
               EFlange flangeType = Utils.GetArcPlaneFlangeType (refTS.Vec1,
               GCGen.GetXForm ());
               GCGen.WriteWireJointTrace (refTS, scrapSideNormal,
                  mMostRecentPrevToolPosition, NotchApproachLength, ref mPrevPlane, flangeType, ToolingItem,
                  ref mBlockCutLength, mTotalToolingsCutLength, mXStart, mXPartition, mXEnd,
                     isNextSeqFlexMc, isValidNotch: false, nextBeginFlexMachining: false, comment);
               PreviousToolingSegment = new (refTS.Curve, PreviousToolingSegment.Value.Vec1, refTS.Vec0);
               mMostRecentPrevToolPosition = GCGen.GetLastToolHeadPosition ().Item1;
               continueMachining = true;
               break;
            case NotchSectionType.MachineToolingForward: {
                  Tuple<Point3, Vector3> cutoutEntry;
                  if (cutoutSequence.StartIndex > cutoutSequence.EndIndex)
                     throw new Exception ("In CutOut.WriteTooling: MachineToolingForward : startIndex > endIndex");
                  if (!continueMachining)
                     GCGen.InitializeNotchToolingBlock (ToolingItem, prevToolingItem: null, ToolingSegments,
                        ToolingSegments[cutoutSequence.StartIndex].Vec0, mXStart, mXPartition, mXEnd, isFlexCut: false, ii == mCutOutBlocks.Count - 1,
                        /*isToBeTreatedAsCutOut: mFeatureToBeTreatedAsCutout,*/ isValidNotch: false, cutoutSequence.StartIndex, cutoutSequence.EndIndex,
                        comment: "CutOutSequence: Machining Forward Direction");
                  else {
                     string titleComment = $"( CutOutSequence: Machining Forward Direction )";
                     GCGen.WriteLineStatement (titleComment);
                  }
                  if (ii == 0) {
                     cutoutEntry = Tuple.Create (ToolingSegments[0].Curve.Start, ToolingSegments[0].Vec0);
                     GCGen.PrepareforToolApproach (ToolingItem, ToolingSegments, PreviousToolingSegment, mPrevToolingItem,
                        mPrevToolingSegs, mIsFirstTooling, isValidNotch: false, cutoutEntry);
                     if (!GCGen.IsRapidMoveToPiercingPositionWithPingPong)
                        GCGen.RapidMoveToPiercingPosition (ToolingSegments[0].Curve.Start, ToolingSegments[0].Vec0, usePingPongOption: true);
                  }
                  if (!continueMachining) {
                     //if (ii == 0)
                     //   GCGen.RapidMoveToPiercingPosition (ToolingSegments[cutoutSequence.StartIndex].Curve.Start,
                     //      ToolingSegments[cutoutSequence.StartIndex].Vec0, usePingPongOption: false);
                     //else {
                     //   GCGen.WriteLineStatement ("ToolPlane\t( Confirm Cutting Plane )");
                     //   GCGen.RapidMoveToPiercingPosition (ToolingSegments[cutoutSequence.StartIndex].Curve.Start,
                     //      ToolingSegments[cutoutSequence.StartIndex].Vec0, usePingPongOption: true);
                     //}
                     if (ii == 0) {
                        cutoutEntry = Tuple.Create (ToolingSegments[0].Curve.Start, ToolingSegments[0].Vec0);
                        GCGen.MoveToMachiningStartPosition (cutoutEntry.Item1, cutoutEntry.Item2, ToolingItem.Name);
                     }
                     var isFromWebFlange = Utils.IsMachiningFromWebFlange (ToolingSegments, cutoutSequence.StartIndex);
                     //GCGen.RapidMoveToPiercingPosition (ToolingSegments[cutoutSequence.StartIndex].Curve.Start,
                     //      ToolingSegments[cutoutSequence.StartIndex].Vec0, usePingPongOption: true);
                     GCGen.WriteToolCorrectionData (ToolingItem, isFromWebFlange);
                     GCGen.RapidMoveToPiercingPosition (ToolingSegments[cutoutSequence.StartIndex].Curve.Start,
                           ToolingSegments[cutoutSequence.StartIndex].Vec0, usePingPongOption: false);
                     GCGen.EnableMachiningDirective ();
                  }
                  for (int jj = cutoutSequence.StartIndex; jj <= cutoutSequence.EndIndex; jj++) {
                     mExitTooling = ToolingSegments[jj];
                     GCGen.WriteCurve (ToolingSegments[jj], ToolingItem.Name);
                     mBlockCutLength += ToolingSegments[jj].Curve.Length;
                  }
                  PreviousToolingSegment = ToolingSegments[cutoutSequence.EndIndex];
                  GCGen.DisableMachiningDirective ();
                  mMostRecentPrevToolPosition = GCGen.GetLastToolHeadPosition ().Item1;
                  GCGen.FinalizeNotchToolingBlock (ToolingItem, mBlockCutLength, mTotalToolingsCutLength);
                  continueMachining = false;
               }
               break;
            case NotchSectionType.MachineFlexToolingForward: {
                  if (ii == 0) throw new Exception ("CutOut writing starts from Flex side machining, which is wrong");
                  if (cutoutSequence.StartIndex > cutoutSequence.EndIndex)
                     throw new Exception ("In CutOut.WriteTooling: MachineFlexToolingForward : startIndex > endIndex");
                  if (!continueMachining)
                     GCGen.InitializeNotchToolingBlock (ToolingItem, prevToolingItem: null, ToolingSegments, ToolingSegments[cutoutSequence.StartIndex].Vec0,
                        mXStart, mXPartition, mXEnd, isFlexCut: true, ii == mCutOutBlocks.Count - 1,
                        //isToBeTreatedAsCutOut:mFeatureToBeTreatedAsCutout,
                        isValidNotch: false,
                        cutoutSequence.StartIndex,
                        cutoutSequence.EndIndex, refSegIndex: cutoutSequence.StartIndex,
                        "CutOutSequence: Flex machining Forward Direction");
                  {
                     if (!continueMachining) {
                        var isFromWebFlange = Utils.IsMachiningFromWebFlange (ToolingSegments, cutoutSequence.StartIndex);
                        GCGen.RapidMoveToPiercingPosition (ToolingSegments[cutoutSequence.StartIndex].Curve.Start,
                           ToolingSegments[cutoutSequence.StartIndex].Vec0, usePingPongOption: true);
                        GCGen.WriteToolCorrectionData (ToolingItem, isFromWebFlange);
                        GCGen.RapidMoveToPiercingPosition (ToolingSegments[cutoutSequence.StartIndex].Curve.Start,
                           ToolingSegments[cutoutSequence.StartIndex].Vec0, usePingPongOption: false);
                        GCGen.EnableMachiningDirective ();
                     }
                     GCGen.WriteLineStatement ("( CutOutSequence: Machining in Flex in Forward Direction )");
                     for (int jj = cutoutSequence.StartIndex; jj <= cutoutSequence.EndIndex; jj++) {
                        GCGen.WriteCurve (ToolingSegments[jj], ToolingItem.Name, isFlexSection: true);
                        mExitTooling = ToolingSegments[jj];
                        mBlockCutLength += ToolingSegments[jj].Curve.Length;
                        PreviousToolingSegment = ToolingSegments[jj];
                     }

                     // The next in sequence has to be wire joint jump trace and so
                     // continueMachining is made to false
                     GCGen.DisableMachiningDirective ();
                     mMostRecentPrevToolPosition = GCGen.GetLastToolHeadPosition ().Item1;
                  }
                  GCGen.FinalizeNotchToolingBlock (ToolingItem, mBlockCutLength, mTotalToolingsCutLength);
               }
               continueMachining = false;
               break;

            default:
               throw new Exception ("Undefined CutOut sequence");
         }
      }
   }

   //public void WriteGCode (List<ToolingSegment> toolingSegs) {
   //   (var curve, var CurveStartNormal, _) = toolingSegs[0];
   //   Utils.EPlane previousPlaneType = Utils.EPlane.None;
   //   Utils.EPlane currPlaneType;
   //   if (ToolingItem.IsFlexFeature ()) currPlaneType = Utils.EPlane.Flex;
   //   else currPlaneType = Utils.GetFeatureNormalPlaneType (CurveStartNormal, GCGen.GetXForm ());
   //   // Write any feature other than notch
   //   GCGen.MoveToMachiningStartPosition (curve.Start, CurveStartNormal, ToolingItem.Name);
   //   {
   //      bool isFromWebFlange = true;
   //      if (Math.Abs (toolingSegs[0].Vec0.Y) > Math.Abs (toolingSegs[0].Vec0.Z))
   //         isFromWebFlange = false;
   //      GCGen.WriteToolCorrectionData (ToolingItem, isFromWebFlange);
   //      // Write all other features such as Holes, Cutouts and edge notches
   //      GCGen.EnableMachiningDirective ();
   //      for (int i = 0; i < toolingSegs.Count; i++) {
   //         var (Curve, startNormal, endNormal) = toolingSegs[i];
   //         startNormal = startNormal.Normalized ();
   //         endNormal = endNormal.Normalized ();
   //         var startPoint = Curve.Start;
   //         var endPoint = Curve.End;
   //         if (i > 0) currPlaneType = Utils.GetFeatureNormalPlaneType (endNormal, GCGen.GetXForm ());

   //         if (Curve is Arc3) { // This is a 2d arc. 
   //            var arcPlaneType = Utils.GetArcPlaneType (startNormal, GCGen.GetXForm ());
   //            var arcFlangeType = Utils.GetArcPlaneFlangeType (startNormal, GCGen.GetXForm ());
   //            (var center, _) = Geom.EvaluateCenterAndRadius (Curve as Arc3);
   //            GCGen.WriteArc (Curve as Arc3, arcPlaneType, arcFlangeType, center, startPoint, endPoint, startNormal,
   //               ToolingItem.Name);
   //         } else GCGen.WriteLine (endPoint, startNormal, endNormal, currPlaneType, previousPlaneType,
   //            Utils.GetFlangeType (ToolingItem, new ()), ToolingItem.Name);
   //         previousPlaneType = currPlaneType;
   //      }
   //      GCGen.DisableMachiningDirective ();
   //      PreviousToolingSegment = toolingSegs[^1];
   //   }
   //}
   #endregion
}