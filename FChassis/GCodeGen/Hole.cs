using Flux.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace FChassis.GCodeGen;
public class Hole : Feature {
   public Tooling ToolingItem { get; set; }
   public GCodeGenerator GCGen;
   double mLeastCurveLength = 0.5;
   Tooling mPrevToolingItem = null;
   bool mIsLastToolingItem = false;
   double mXStart, mXEnd, mXPartition;
   List<ToolingSegment> mPrevToolingSegs;
   bool mIsFirstTooling = false;
   ToolingSegment? mPrevToolingSegment;
   Bound3 mBound;
   List<ToolingSegment> mToolingSegments = [];
   ToolingSegment? mLastToolingSegment;
   double mTotalToolingCutLength, mPrevCutToolingsLength;
   public Hole (Tooling toolingItem, GCodeGenerator gcgen, double xStart, double xEnd, double xPartition,
      List<ToolingSegment> prevToolingSegs, ToolingSegment? prevToolingSegment, Bound3 bound,
      double prevCutToolingsLength, double totalToolingCutLength,
      bool firstTooling = false, Tooling prevToolingItem = null, bool isLastTooling = false, double leastCurveLen = 0.5) {
      ToolingItem = toolingItem;
      GCGen = gcgen;
      mIsLastToolingItem = isLastTooling;
      mPrevToolingItem = prevToolingItem;
      mXStart = xStart; mXEnd = xEnd; mXPartition = xPartition;
      mPrevToolingSegs = prevToolingSegs;
      mIsFirstTooling = firstTooling;
      mPrevToolingSegment = prevToolingSegment;
      mBound = bound;
      mLastToolingSegment = new ();
      mPrevCutToolingsLength = prevCutToolingsLength;
      mTotalToolingCutLength = totalToolingCutLength;
      mToolingSegments = Utils.GetSegmentsAccountedForApproachLength (ToolingItem, GCGen, mLeastCurveLength);
   }
   public override List<ToolingSegment> ToolingSegments { get => mToolingSegments; set => mToolingSegments = value; }
   public override ToolingSegment? GetLastToolingSegment () => mLastToolingSegment;

   public override void WriteTooling () {
      GCGen.InitializeToolingBlock (ToolingItem, mPrevToolingItem, /*frameFeed,*/
               mXStart, mXPartition, mXEnd, ToolingSegments, /*isValidNotch:*/false, /*isFlexCut*/false,
               mIsLastToolingItem);

      if (ToolingSegments == null || ToolingSegments?.Count == 0) return;

      GCGen.PrepareforToolApproach (ToolingItem, ToolingSegments, mPrevToolingSegment, mPrevToolingItem, mPrevToolingSegs, mIsFirstTooling, isValidNotch:false);
      //int CCNo = Utils.GetFlangeType (ToolingItem, GCGen.GetXForm ()) == Utils.EFlange.Web ? WebCCNo : FlangeCCNo;
      //if (toolingItem.IsCircle ()) {
      //   var evalValue = Geom.EvaluateCenterAndRadius (ToolingItem.Segs.ToList ()[0].Curve as Arc3);
      //   if (mControlDiameter.Any (a => a.EQ (2 * evalValue.Item2))) CCNo = 4;
      //} else if (toolingItem.IsNotch ()) CCNo = 1;
      //int outCCNO = CCNo;
      //if (ToolingItem.IsFlexCutout ()) outCCNO = 1;

      // Output the Cutting offset. Customer need to cut hole slightly larger than given in geometry
      // We are using G42 than G41 while cutting holes
      // If we are reversing y and not reversing x. We are in 4th quadrant. Flip 42 or 41
      // Tool diameter compensation
      //GCGen.WriteToolCorrectionData (toolingItem);
      //if (!toolingItem.IsMark ())
      // ** Machining **
      mLastToolingSegment = GCGen.WriteTooling (ToolingSegments, ToolingItem, mBound, mPrevCutToolingsLength, mTotalToolingCutLength, /*frameFeed*/
            mXStart, mXPartition, mXEnd);
   }
}

