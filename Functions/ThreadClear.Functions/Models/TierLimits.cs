using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadClear.Functions.Models
{
    public class TierLimits
    {
        public string TierName { get; set; } = "free";
        public int MonthlyAnalyses { get; set; }
        public int MonthlyGmailThreads { get; set; }
        public int MonthlySpellChecks { get; set; }
        public int MonthlyAITokens { get; set; }
    }
}
