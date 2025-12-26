// ThreadClear Browser Extension - Popup Script

// Configuration
const API_URL = 'https://threadclear-functions-fsewbzcsd8fuegdj.eastus-01.azurewebsites.net/api';
const FUNCTION_KEY = '';

// DOM Elements
const inputSection = document.getElementById('inputSection');
const loadingSection = document.getElementById('loadingSection');
const errorSection = document.getElementById('errorSection');
const resultsSection = document.getElementById('resultsSection');

const conversationText = document.getElementById('conversationText');
const draftText = document.getElementById('draftText');
const sourceType = document.getElementById('sourceType');

const analyzeBtn = document.getElementById('analyzeBtn');
const getSelectionBtn = document.getElementById('getSelectionBtn');
const backBtn = document.getElementById('backBtn');
const retryBtn = document.getElementById('retryBtn');

const loadingMessage = document.getElementById('loadingMessage');
const errorMessage = document.getElementById('errorMessage');

// Progress messages
const progressMessages = [
  { text: 'Parsing conversation...', delay: 0 },
  { text: 'Identifying participants...', delay: 1500 },
  { text: 'Detecting unanswered questions...', delay: 3000 },
  { text: 'Analyzing tension points...', delay: 5000 },
  { text: 'Assessing conversation health...', delay: 7000 },
  { text: 'Generating insights...', delay: 9000 },
  { text: 'Analyzing your draft...', delay: 11000 },
  { text: 'Finalizing results...', delay: 13000 }
];

let progressTimeouts = [];

// Event Listeners
document.addEventListener('DOMContentLoaded', init);

function init() {
  analyzeBtn.addEventListener('click', analyze);
  getSelectionBtn.addEventListener('click', getSelectedText);
  backBtn.addEventListener('click', showInput);
  retryBtn.addEventListener('click', showInput);

  // Try to get selected text on popup open
  getSelectedText();
}

// Get selected text from active tab
async function getSelectedText() {
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    
    if (!tab || !tab.id) {
      console.log('No active tab found');
      return;
    }

    const results = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: () => window.getSelection().toString()
    });

    if (results && results[0] && results[0].result) {
      conversationText.value = results[0].result.trim();
    }
  } catch (error) {
    // Silently fail - user can paste manually
    console.log('Could not get selection (may be a restricted page):', error.message);
  }
}

// Show/hide sections
function showInput() {
  inputSection.classList.remove('hidden');
  loadingSection.classList.add('hidden');
  errorSection.classList.add('hidden');
  resultsSection.classList.add('hidden');
  clearProgressTimeouts();
}

function showLoading() {
  inputSection.classList.add('hidden');
  loadingSection.classList.remove('hidden');
  errorSection.classList.add('hidden');
  resultsSection.classList.add('hidden');
  startProgressMessages();
}

function showError(message) {
  inputSection.classList.add('hidden');
  loadingSection.classList.add('hidden');
  errorSection.classList.remove('hidden');
  resultsSection.classList.add('hidden');
  errorMessage.textContent = message;
  clearProgressTimeouts();
}

function showResults() {
  inputSection.classList.add('hidden');
  loadingSection.classList.add('hidden');
  errorSection.classList.add('hidden');
  resultsSection.classList.remove('hidden');
  clearProgressTimeouts();
}

// Progress messages
function startProgressMessages() {
  clearProgressTimeouts();
  progressMessages.forEach(msg => {
    const timeout = setTimeout(() => {
      loadingMessage.textContent = msg.text;
    }, msg.delay);
    progressTimeouts.push(timeout);
  });
}

function clearProgressTimeouts() {
  progressTimeouts.forEach(t => clearTimeout(t));
  progressTimeouts = [];
}

// Analyze conversation
async function analyze() {
  const text = conversationText.value.trim();
  
  if (!text) {
    alert('Please enter or select a conversation to analyze.');
    return;
  }

  showLoading();

  try {
    const requestBody = {
      conversationText: text,
      sourceType: sourceType.value,
      parsingMode: 2, // Auto
      draftMessage: draftText.value.trim() || null
    };

    let url = `${API_URL}/analyze`;
    if (FUNCTION_KEY) {
      url += `?code=${FUNCTION_KEY}`;
    }

    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(requestBody)
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status}`);
    }

    const data = await response.json();

    if (data.success) {
      displayResults(data);
    } else {
      throw new Error(data.error || 'Analysis failed');
    }
  } catch (error) {
    console.error('Analysis error:', error);
    showError(error.message || 'Failed to analyze conversation. Please try again.');
  }
}

// Display results
function displayResults(data) {
  const capsule = data.capsule;
  const draftAnalysis = normalizeDraftAnalysis(data.draftAnalysis);

  // Draft Analysis
  const draftSection = document.getElementById('draftAnalysis');
  if (draftAnalysis) {
    draftSection.classList.remove('hidden');
    
    // Ready indicator
    const readyIndicator = document.getElementById('readyIndicator');
    readyIndicator.className = `ready-indicator ${draftAnalysis.readyToSend ? 'ready' : 'needs-work'}`;
    readyIndicator.textContent = draftAnalysis.readyToSend ? '✓ Ready to Send' : '⚠ Needs Work';

    // Overall assessment
    document.getElementById('overallAssessment').textContent = draftAnalysis.overallAssessment || '';

    // Completeness score
    const score = draftAnalysis.completenessScore || 0;
    document.getElementById('completenessBar').style.width = `${score * 10}%`;
    document.getElementById('completenessScore').textContent = `${score}/10`;

    // Tone
    const toneSection = document.getElementById('toneSection');
    const toneBadges = document.getElementById('toneBadges');
    const toneExplanation = document.getElementById('toneExplanation');
    
    if (draftAnalysis.tone) {
      toneSection.classList.remove('hidden');
      toneBadges.innerHTML = '';
      
      if (draftAnalysis.tone.tone) {
        toneBadges.innerHTML += `<span class="badge tone">${draftAnalysis.tone.tone}</span>`;
      }
      
      const matchClass = draftAnalysis.tone.matchesConversationTone ? 'match' : 'no-match';
      const matchText = draftAnalysis.tone.matchesConversationTone ? '✓ Matches tone' : '⚠ Different tone';
      toneBadges.innerHTML += `<span class="badge ${matchClass}">${matchText}</span>`;
      
      if (draftAnalysis.tone.escalationRisk && draftAnalysis.tone.escalationRisk !== 'none') {
        toneBadges.innerHTML += `<span class="badge escalation-${draftAnalysis.tone.escalationRisk}">Escalation: ${draftAnalysis.tone.escalationRisk}</span>`;
      }
      
      toneExplanation.textContent = draftAnalysis.tone.explanation || '';
    } else {
      toneSection.classList.add('hidden');
    }

    // Questions ignored
    const questionsIgnoredSection = document.getElementById('questionsIgnoredSection');
    const questionsIgnoredList = document.getElementById('questionsIgnoredList');
    if (draftAnalysis.questionsIgnored && draftAnalysis.questionsIgnored.length > 0) {
      questionsIgnoredSection.classList.remove('hidden');
      questionsIgnoredList.innerHTML = draftAnalysis.questionsIgnored
        .map(q => `<li>${escapeHtml(q)}</li>`)
        .join('');
    } else {
      questionsIgnoredSection.classList.add('hidden');
    }

    // Risk flags
    const riskFlagsSection = document.getElementById('riskFlagsSection');
    const riskFlagsList = document.getElementById('riskFlagsList');
    if (draftAnalysis.riskFlags && draftAnalysis.riskFlags.length > 0) {
      riskFlagsSection.classList.remove('hidden');
      riskFlagsList.innerHTML = draftAnalysis.riskFlags.map(risk => `
        <div class="risk-flag ${risk.severity || 'low'}">
          <div class="risk-flag-header">
            <span class="risk-flag-type">${escapeHtml(risk.type || '')}</span>
            <span class="risk-flag-severity">${escapeHtml(risk.severity || 'low')}</span>
          </div>
          <p class="risk-flag-description">${escapeHtml(risk.description || '')}</p>
          ${risk.suggestion ? `<p class="risk-flag-suggestion">💡 ${escapeHtml(risk.suggestion)}</p>` : ''}
        </div>
      `).join('');
    } else {
      riskFlagsSection.classList.add('hidden');
    }

    // Suggestions
    const suggestionsSection = document.getElementById('suggestionsSection');
    const suggestionsList = document.getElementById('suggestionsList');
    if (draftAnalysis.suggestions && draftAnalysis.suggestions.length > 0) {
      suggestionsSection.classList.remove('hidden');
      suggestionsList.innerHTML = draftAnalysis.suggestions
        .map(s => `<li>${escapeHtml(s)}</li>`)
        .join('');
    } else {
      suggestionsSection.classList.add('hidden');
    }
  } else {
    draftSection.classList.add('hidden');
  }

  // Conversation Analysis
  const analysis = capsule.Analysis || capsule.analysis;
  
  // Health Score
  const health = analysis?.ConversationHealth || analysis?.conversationHealth;
  const healthScoreCircle = document.getElementById('healthScoreCircle');
  const healthScoreValue = document.getElementById('healthScoreValue');
  const riskLevel = document.getElementById('riskLevel');
  const healthSummary = document.getElementById('healthSummary');

  if (health) {
    const score = Math.round((health.HealthScore || health.healthScore || 0) * 100);
    healthScoreValue.textContent = score;
    
    const level = (health.RiskLevel || health.riskLevel || 'medium').toLowerCase();
    healthScoreCircle.className = `score-circle ${score >= 70 ? 'high' : score >= 40 ? 'medium' : 'low'}`;
    
    riskLevel.textContent = `${level} risk`;
    riskLevel.className = `risk-badge ${level}`;
    
    const issues = health.Issues || health.issues || [];
    healthSummary.textContent = issues.slice(0, 2).join('. ') || 'No major issues detected.';
  }

  // Unanswered Questions
  const unansweredSection = document.getElementById('unansweredSection');
  const unansweredList = document.getElementById('unansweredList');
  const unanswered = analysis?.UnansweredQuestions || analysis?.unansweredQuestions || [];
  
  if (unanswered.length > 0) {
    unansweredSection.classList.remove('hidden');
    unansweredList.innerHTML = unanswered.map(q => 
      `<li>❓ ${escapeHtml(q.Question || q.question || '')} <em style="color:#888; font-size:11px;">- ${escapeHtml(q.AskedBy || q.askedBy || 'Unknown')}</em></li>`
    ).join('');
  } else {
    unansweredSection.classList.add('hidden');
  }

  // Tension Points
  const tensionSection = document.getElementById('tensionSection');
  const tensionList = document.getElementById('tensionList');
  const tension = analysis?.TensionPoints || analysis?.tensionPoints || [];
  
  if (tension.length > 0) {
    tensionSection.classList.remove('hidden');
    tensionList.innerHTML = tension.map(t => `
      <div class="tension-item">
        <div class="tension-item-header">
          <span class="tension-item-type">${escapeHtml(t.Type || t.type || 'Tension')}</span>
          <span class="tension-item-severity">${escapeHtml(t.Severity || t.severity || 'medium')}</span>
        </div>
        <p class="tension-item-description">${escapeHtml(t.Description || t.description || '')}</p>
      </div>
    `).join('');
  } else {
    tensionSection.classList.add('hidden');
  }

  // Suggested Actions
  const actionsSection = document.getElementById('actionsSection');
  const actionsList = document.getElementById('actionsList');
  const actions = capsule.SuggestedActions || capsule.suggestedActions || [];
  
  if (actions.length > 0) {
    actionsSection.classList.remove('hidden');
    actionsList.innerHTML = actions.map(a => `
      <div class="action-item">
        <div class="action-item-header">
          <span class="action-item-text">${escapeHtml(a.Action || a.action || '')}</span>
          <span class="action-item-priority ${(a.Priority || a.priority || 'medium').toLowerCase()}">${escapeHtml(a.Priority || a.priority || 'Medium')}</span>
        </div>
        ${a.Reasoning || a.reasoning ? `<p class="action-item-reasoning">${escapeHtml(a.Reasoning || a.reasoning)}</p>` : ''}
      </div>
    `).join('');
  } else {
    actionsSection.classList.add('hidden');
  }

  showResults();
}

// Normalize draft analysis (handle PascalCase from API)
function normalizeDraftAnalysis(draft) {
  if (!draft) return null;

  const toneRaw = draft.Tone || draft.tone;
  const tone = toneRaw ? {
    tone: toneRaw.Tone || toneRaw.tone || '',
    matchesConversationTone: toneRaw.MatchesConversationTone ?? toneRaw.matchesConversationTone ?? true,
    escalationRisk: toneRaw.EscalationRisk || toneRaw.escalationRisk || 'none',
    explanation: toneRaw.Explanation || toneRaw.explanation || ''
  } : null;

  const questionsCoveredRaw = draft.QuestionsCovered || draft.questionsCovered || [];
  const questionsCovered = questionsCoveredRaw.map(q => ({
    question: q.Question || q.question || '',
    addressed: q.Addressed ?? q.addressed ?? false,
    howAddressed: q.HowAddressed || q.howAddressed || null
  }));

  const riskFlagsRaw = draft.RiskFlags || draft.riskFlags || [];
  const riskFlags = riskFlagsRaw.map(r => ({
    type: r.Type || r.type || '',
    description: r.Description || r.description || '',
    severity: r.Severity || r.severity || 'low',
    suggestion: r.Suggestion || r.suggestion || ''
  }));

  return {
    tone: tone,
    questionsCovered: questionsCovered,
    questionsIgnored: draft.QuestionsIgnored || draft.questionsIgnored || [],
    newQuestionsIntroduced: draft.NewQuestionsIntroduced || draft.newQuestionsIntroduced || [],
    riskFlags: riskFlags,
    completenessScore: draft.CompletenessScore ?? draft.completenessScore ?? 0,
    suggestions: draft.Suggestions || draft.suggestions || [],
    overallAssessment: draft.OverallAssessment || draft.overallAssessment || '',
    readyToSend: draft.ReadyToSend ?? draft.readyToSend ?? false
  };
}

// Utility: Escape HTML
function escapeHtml(text) {
  if (!text) return '';
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}
