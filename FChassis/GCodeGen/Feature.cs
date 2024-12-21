using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FChassis.GCodeGen;

 public abstract class Feature
 {
   public abstract List<ToolingSegment> ToolingSegments {  get; set; }
   public abstract void WriteTooling ();
   public abstract ToolingSegment? GetLastToolingSegment ();
 }