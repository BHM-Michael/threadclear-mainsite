# Draft Analysis Feature - Implementation Plan

## Overview
Add a second input field for draft messages. Analyze the draft in context of the conversation to show if it's appropriate, addresses outstanding questions, and predicts impact.

**Estimated Total Time:** 60-75 minutes

---

## Backend Changes

### 1. Update `AnalysisRequest.cs`
**File:** `C:\Projects\ThreadClear\Functions\ThreadClear.Functions\Models\AnalysisRequest.cs`

Add property:
```csharp
/// <summary>
/// Optional draft reply to analyze in context of the conversation
/// </summary>
public string? DraftMessage { get; set; }
```

---

### 2. Create `DraftAnalysis.cs` (new file)
**File:** `C:\Projects\ThreadClear\Functions\ThreadClear.Functions\Models\DraftAnalysis.cs`

```csharp
namespace ThreadClear.Functions.Models
{
    public class DraftAnalysis
    {
        public ToneAssessment Tone { get; set; } = new();
        public List<QuestionCoverage> QuestionsCovered { get; set; } = new();
        public List<string> QuestionsIgnored { get; set; } = new();
        public List<string> NewQuestionsIntroduced { get; set; } = new();
        public List<RiskFlag> RiskFlags { get; set; } = new();
        public int CompletenessScore { get; set; }
        public List<string> Suggestions { get; set; } = new();
        public string OverallAssessment { get; set; } = string.Empty;
        public bool ReadyToSend { get; set; }
    }

    public class ToneAssessment
    {
        public string Tone { get; set; } = string.Empty;  // friendly, neutral, formal, defensive, aggressive, dismissive
        public bool MatchesConversationTone { get; set; }
        public string EscalationRisk { get; set; } = "none";  // none, low, medium, high
        public string Explanation { get; set; } = string.Empty;
    }

    public class QuestionCoverage
    {
        public string Question { get; set; } = string.Empty;
        public bool Addressed { get; set; }
        public string? HowAddressed { get; set; }
    }

    public class RiskFlag
    {
        public string Type { get; set; } = string.Empty;  // ambiguity, tension, legal, commitment, deadline
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "low";  // low, medium, high
        public string Suggestion { get; set; } = string.Empty;
    }
}
```

---

### 3. Add method to ConversationAnalyzer

**Interface:** `IConversationAnalyzer.cs`
```csharp
Task<DraftAnalysis> AnalyzeDraft(ThreadCapsule conversation, string draftMessage);
```

**Implementation:** `ConversationAnalyzer.cs`
- New method that calls AI service with draft analysis prompt
- Returns structured `DraftAnalysis` object

**AI Prompt:**
```
You are analyzing a draft reply in the context of an ongoing conversation.

CONVERSATION:
{conversation - formatted messages}

UNANSWERED QUESTIONS FROM CONVERSATION:
{list from conversation analysis}

DRAFT REPLY:
{draftMessage}

Analyze the draft and respond ONLY with valid JSON (no markdown, no explanation):

{
  "tone": {
    "tone": "friendly|neutral|formal|defensive|aggressive|dismissive",
    "matchesConversationTone": true/false,
    "escalationRisk": "none|low|medium|high",
    "explanation": "brief explanation of tone assessment"
  },
  "questionsCovered": [
    {
      "question": "the original question from conversation",
      "addressed": true/false,
      "howAddressed": "explanation of how draft addresses it, or null"
    }
  ],
  "questionsIgnored": ["list of unanswered questions NOT addressed by draft"],
  "newQuestionsIntroduced": ["any new questions the draft asks"],
  "riskFlags": [
    {
      "type": "ambiguity|tension|legal|commitment|deadline",
      "description": "what the risk is",
      "severity": "low|medium|high",
      "suggestion": "how to mitigate"
    }
  ],
  "completenessScore": 7,
  "suggestions": [
    "Consider acknowledging their concern about X",
    "You might want to clarify the timeline"
  ],
  "overallAssessment": "Brief 1-2 sentence summary of whether this draft is ready to send",
  "readyToSend": true/false
}
```

---

### 4. Update `AnalyzeConversation.cs` endpoint
**File:** `C:\Projects\ThreadClear\Functions\ThreadClear.Functions\Functions\AnalyzeConversation.cs`

After existing analysis, add:
```csharp
// Analyze draft if provided
DraftAnalysis? draftAnalysis = null;
if (!string.IsNullOrWhiteSpace(request.DraftMessage))
{
    draftAnalysis = await _analyzer.AnalyzeDraft(capsule, request.DraftMessage);
}

// Include in response
var response = req.CreateResponse(HttpStatusCode.OK);
await response.WriteAsJsonAsync(new
{
    success = true,
    capsule = capsule,
    parsingMode = modeUsed,
    draftAnalysis = draftAnalysis  // null if no draft provided
});
```

---

## Frontend Changes

### 5. Update `analyze.component.html`
**File:** `C:\Projects\ThreadClear\ThreadClear.Web\src\app\components\analyze\analyze.component.html`

Add second textarea below conversation input:
```html
<!-- Existing conversation input -->
<div class="input-section">
  <label>Conversation</label>
  <textarea [(ngModel)]="conversationText" placeholder="Paste conversation here..."></textarea>
</div>

<!-- NEW: Draft input -->
<div class="input-section">
  <label>Your Draft Reply (optional)</label>
  <textarea [(ngModel)]="draftMessage" placeholder="Paste your draft response to analyze..."></textarea>
  <small>Leave empty to analyze conversation only</small>
</div>
```

---

### 6. Update `analyze.component.ts`
**File:** `C:\Projects\ThreadClear\ThreadClear.Web\src\app\components\analyze\analyze.component.ts`

Add property:
```typescript
draftMessage: string = '';
```

Update request building:
```typescript
const request: any = {
  conversationText: this.conversationText,
  sourceType: this.sourceType,
  parsingMode: this.parsingMode,
  draftMessage: this.draftMessage || null,  // Add this
  // ... existing permission flags
};
```

Add property for results:
```typescript
draftAnalysis: any = null;
```

In subscribe success:
```typescript
this.draftAnalysis = response.draftAnalysis;
```

---

### 7. Update results display in `analyze.component.html`

Add new section for draft analysis (show only when draftAnalysis exists):
```html
<!-- Draft Analysis Results -->
<div *ngIf="draftAnalysis" class="draft-analysis-section">
  <h3>Draft Analysis</h3>
  
  <!-- Ready to Send indicator -->
  <div class="ready-indicator" [class.ready]="draftAnalysis.readyToSend" [class.needs-work]="!draftAnalysis.readyToSend">
    {{ draftAnalysis.readyToSend ? '✓ Ready to Send' : '⚠ Needs Work' }}
  </div>
  
  <!-- Overall Assessment -->
  <p class="overall-assessment">{{ draftAnalysis.overallAssessment }}</p>
  
  <!-- Tone Assessment -->
  <div class="tone-section">
    <h4>Tone</h4>
    <span class="tone-badge">{{ draftAnalysis.tone.tone }}</span>
    <span *ngIf="draftAnalysis.tone.escalationRisk !== 'none'" class="risk-badge {{ draftAnalysis.tone.escalationRisk }}">
      Escalation Risk: {{ draftAnalysis.tone.escalationRisk }}
    </span>
    <p>{{ draftAnalysis.tone.explanation }}</p>
  </div>
  
  <!-- Questions Addressed -->
  <div *ngIf="draftAnalysis.questionsCovered?.length" class="questions-section">
    <h4>Questions Addressed</h4>
    <ul>
      <li *ngFor="let q of draftAnalysis.questionsCovered" [class.addressed]="q.addressed" [class.missed]="!q.addressed">
        {{ q.question }}
        <span *ngIf="q.addressed">✓</span>
        <span *ngIf="!q.addressed">✗</span>
      </li>
    </ul>
  </div>
  
  <!-- Questions Ignored -->
  <div *ngIf="draftAnalysis.questionsIgnored?.length" class="warning-section">
    <h4>⚠ Questions Not Addressed</h4>
    <ul>
      <li *ngFor="let q of draftAnalysis.questionsIgnored">{{ q }}</li>
    </ul>
  </div>
  
  <!-- Risk Flags -->
  <div *ngIf="draftAnalysis.riskFlags?.length" class="risks-section">
    <h4>Risk Flags</h4>
    <div *ngFor="let risk of draftAnalysis.riskFlags" class="risk-item {{ risk.severity }}">
      <strong>{{ risk.type }}</strong>: {{ risk.description }}
      <p class="suggestion">💡 {{ risk.suggestion }}</p>
    </div>
  </div>
  
  <!-- Suggestions -->
  <div *ngIf="draftAnalysis.suggestions?.length" class="suggestions-section">
    <h4>Suggestions</h4>
    <ul>
      <li *ngFor="let s of draftAnalysis.suggestions">{{ s }}</li>
    </ul>
  </div>
  
  <!-- Completeness Score -->
  <div class="completeness">
    <h4>Completeness Score</h4>
    <div class="score-bar">
      <div class="score-fill" [style.width.%]="draftAnalysis.completenessScore * 10"></div>
    </div>
    <span>{{ draftAnalysis.completenessScore }}/10</span>
  </div>
</div>
```

---

### 8. Add CSS styles
Add styles for the new draft analysis section:
- `.ready-indicator` with green/yellow colors
- `.tone-badge` styling
- `.risk-item` with severity colors (low=yellow, medium=orange, high=red)
- `.score-bar` for visual completeness indicator

---

## Testing Checklist

- [ ] Conversation only (no draft) - works as before
- [ ] Conversation + draft - shows both analyses
- [ ] Draft with good tone - shows "Ready to Send"
- [ ] Draft missing questions - shows warnings
- [ ] Draft with risks - shows risk flags
- [ ] Empty draft field - treated as no draft

---

## Deployment

1. Build and test locally
2. Deploy backend: Visual Studio > Publish
3. Deploy frontend: `ng build --configuration=production` then `swa deploy`

---

## Future Enhancements

- **Rewrite suggestions** - AI suggests alternative phrasing
- **Side-by-side comparison** - show draft vs suggested improvements
- **Save drafts** - store draft iterations for comparison
- **Browser extension** - auto-populate conversation from Gmail/Slack
