using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadClear.Functions.Models
{
    public class AnalysisOptions
    {
        public bool EnableUnansweredQuestions { get; set; } = true;
        public bool EnableTensionPoints { get; set; } = true;
        public bool EnableMisalignments { get; set; } = true;
        public bool EnableConversationHealth { get; set; } = true;
        public bool EnableSuggestedActions { get; set; } = true;
    }
}