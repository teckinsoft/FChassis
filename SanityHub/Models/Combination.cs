using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanityHub.Models {
   public class Combination {
      public string Name { get; set; }
      public Dictionary<string, string> Parameters { get; set; } = new ();
   }
}
