using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.Enums
{
    public class ProcessingProgress
    {
        
        public string CurrentOperation { get; set; } = string.Empty;
        public double PercentageComplete { get; set; } = 0;
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int TotalRequest { get; set; }
    }

}
