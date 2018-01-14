using System.Collections.Generic;

namespace Prometheus.Engine.ReferenceTrack
{
    /// <summary>
    /// Holds the conditional assignment for a given reference.
    /// </summary>
    public class ConditionalAssignment {
        //TODO: split to members
        public List<string> Conditions { get; set; }
        //TODO: more context here
        public string Reference { get; set; }
        //TODO: assigned identifier: more context here on location
        public string ReferenceLocation { get; set; }

        public ConditionalAssignment()
        {
            Conditions = new List<string>();
        }
    }
}