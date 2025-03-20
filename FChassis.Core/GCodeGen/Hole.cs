using Flux.API;

namespace FChassis.Core.GCodeGen;

/// <summary>
/// The Hole class represents cut holes exclusive to flanges.
/// </summary>
public class Hole : ToolingFeature {
   #region Data Members
   double mLeastCurveLength = 0.5;
   Tooling mPrevToolingItem = null;
   bool mIsLastToolingItem = false;
   double mXStart, mXEnd, mXPartition;
   List<ToolingSegment> mPrevToolingSegs;
   bool mIsFirstTooling = false;
   ToolingSegment? mPrevToolingSegment;
   Bound3 mBound;

   // Local copy of tooling segments which are modified 
   // for G code writing. This is not same as Tooling.Segs
   // and do not use Tooling.Segs for holes.
   List<ToolingSegment> mToolingSegments = [];
   ToolingSegment? mLastToolingSegment;
   double mTotalToolingCutLength, mPrevCutToolingsLength;
   double mPrevMarkToolingsLength, mTotalMarkLength;
   #endregion

   #region External References
   public GCodeGenerator GCGen;
   #endregion

   #region Properties
   public Tooling ToolingItem { get; set; }
   #endregion

   #region Constructor(s)
   public Hole (Tooling toolingItem, GCodeGenerator gcgen, double xStart, double xEnd, double xPartition,
      List<ToolingSegment> prevToolingSegs, ToolingSegment? prevToolingSegment, Bound3 bound,
      double prevCutToolingsLength, double prevMarkToolingsLength, double totalMarkLength, double totalToolingCutLength,
      bool firstTooling = false, Tooling prevToolingItem = null, bool isLastTooling = false
      ) {
      ToolingItem = toolingItem;
      GCGen = gcgen;
      mIsLastToolingItem = isLastTooling;
      mPrevToolingItem = prevToolingItem;
      mXStart = xStart; mXEnd = xEnd; mXPartition = xPartition;
      mPrevToolingSegs = prevToolingSegs;
      mIsFirstTooling = firstTooling;
      mPrevToolingSegment = prevToolingSegment;
      mBound = bound;
      mLastToolingSegment = null;
      mPrevCutToolingsLength = prevCutToolingsLength;
      mTotalToolingCutLength = totalToolingCutLength;
      mPrevMarkToolingsLength = prevMarkToolingsLength;
      mTotalMarkLength = totalMarkLength;
      mToolingSegments = Utils.GetSegmentsAccountedForApproachLength (ToolingItem, GCGen, mLeastCurveLength);
   }
   #endregion

   #region Base class overriders
   /// <summary>
   /// Returns the modified Tooling Segments
   /// </summary>
   public override List<ToolingSegment> ToolingSegments { get => mToolingSegments; set => mToolingSegments = value; }

   /// <summary>
   /// Returns the last tooling segment of the segments that this hole feature 
   /// is made of
   /// </summary>
   /// <returns></returns>
   public override ToolingSegment? GetMostRecentPreviousToolingSegment () => mLastToolingSegment;
   #endregion

   #region G Code Writers
   /// <summary>
   /// This method actually writes the G Code
   /// </summary>
   public override void WriteTooling () {
      GCGen.InitializeToolingBlock (ToolingItem, mPrevToolingItem, /*frameFeed,*/
               mXStart, mXPartition, mXEnd, ToolingSegments, isValidNotch: false, isFlexCut: false,
               mIsLastToolingItem/*, isToBeTreatedAsCutOut: false*/);

      if (ToolingSegments == null || ToolingSegments?.Count == 0) return;

      GCGen.PrepareforToolApproach (ToolingItem, ToolingSegments, mPrevToolingSegment, mPrevToolingItem,
         mPrevToolingSegs, mIsFirstTooling, isValidNotch: false);

      // Output the Cutting offset. Customer need to cut hole slightly larger than given in geometry
      // We are using G42 than G41 while cutting holes
      // If we are reversing y and not reversing x. We are in 4th quadrant. Flip 42 or 41
      // Tool diameter compensation
      var isFromWebNotch = Utils.IsMachiningFromWebFlange (ToolingSegments, 0);
      if (GCGen.IsRapidMoveToPiercingPositionWithPingPong)
         GCGen.WriteToolCorrectionData (ToolingItem, isFromWebNotch, isFlexTooling: false);
      else {
         GCGen.RapidMoveToPiercingPosition (ToolingSegments[0].Curve.Start, ToolingSegments[0].Vec0, usePingPongOption: true);
         GCGen.WriteToolCorrectionData (ToolingItem, isFromWebNotch, isFlexTooling: false);
      }
      GCGen.RapidMoveToPiercingPosition (ToolingSegments[0].Curve.Start, ToolingSegments[0].Vec0, usePingPongOption: false);

      // ** Machining **
      if (GCGen.CreateDummyBlock4Master) return;
      mLastToolingSegment = GCGen.WriteTooling (ToolingSegments, ToolingItem);

      // ** Tooling block finalization - Start**
      GCGen.FinalizeToolingBlock (ToolingItem, mPrevCutToolingsLength,
            //mPrevMarkToolingsLength,
            mTotalToolingCutLength);
   }
   #endregion
}