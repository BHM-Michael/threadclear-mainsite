// ThreadClear Browser Extension - Popup Script
// REFACTORED: Progressive loading with visual spinners for each section

// Configuration
const API_URL = 'https://threadclear-functions-fsewbzcsd8fuegdj.eastus-01.azurewebsites.net/api';

// State
let authToken = null;
let currentUser = null;
let currentCapsule = null; // Store capsule for progressive updates

// DOM Elements
const loginSection = document.getElementById('loginSection');
const userHeader = document.getElementById('userHeader');
const inputSection = document.getElementById('inputSection');
const loadingSection = document.getElementById('loadingSection');
const errorSection = document.getElementById('errorSection');
const resultsSection = document.getElementById('resultsSection');

const emailInput = document.getElementById('emailInput');
const passwordInput = document.getElementById('passwordInput');
const loginBtn = document.getElementById('loginBtn');
const loginError = document.getElementById('loginError');
const userEmail = document.getElementById('userEmail');
const logoutBtn = document.getElementById('logoutBtn');

const conversationText = document.getElementById('conversationText');
const draftText = document.getElementById('draftText');
const sourceType = document.getElementById('sourceType');

const analyzeBtn = document.getElementById('analyzeBtn');
const getSelectionBtn = document.getElementById('getSelectionBtn');
const backBtn = document.getElementById('backBtn');
const retryBtn = document.getElementById('retryBtn');

const loadingMessage = document.getElementById('loadingMessage');
const errorMessage = document.getElementById('errorMessage');

// Section states
const sectionStates = {
  health: { loading: false, complete: false, error: null },
  questions: { loading: false, complete: false, error: null },
  tensions: { loading: false, complete: false, error: null },
  actions: { loading: false, complete: false, error: null },
  misalignments: { loading: false, complete: false, error: null },
  draft: { loading: false, complete: false, error: null }
};

// Initialize
document.addEventListener('DOMContentLoaded', init);

async function init() {
  // Load stored auth
  const stored = await chrome.storage.local.get(['authToken', 'userEmail']);
  if (stored.authToken && stored.userEmail) {
    authToken = stored.authToken;
    currentUser = { email: stored.userEmail };
    showLoggedInState();
  } else {
    showLoginState();
  }

  // Event listeners
  loginBtn.addEventListener('click', login);
  logoutBtn.addEventListener('click', logout);
  analyzeBtn.addEventListener('click', analyzeProgressive);
  getSelectionBtn.addEventListener('click', getSelectedText);
  backBtn.addEventListener('click', showInput);
  retryBtn.addEventListener('click', showInput);

  // Enter key for login
  passwordInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') login();
  });
  emailInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') passwordInput.focus();
  });
}

// Authentication
async function login() {
  const email = emailInput.value.trim();
  const password = passwordInput.value;

  if (!email || !password) {
    showLoginError('Please enter email and password');
    return;
  }

  loginBtn.disabled = true;
  loginBtn.textContent = 'Signing in...';
  hideLoginError();

  try {
    const response = await fetch(`${API_URL}/auth/extension-login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ email, password })
    });

    const data = await response.json();

    if (data.Success || data.success) {
      authToken = data.Token || data.token;
      currentUser = data.User || data.user;

      // Store in chrome.storage
      await chrome.storage.local.set({
        authToken: authToken,
        userEmail: currentUser.Email || currentUser.email
      });

      showLoggedInState();
      getSelectedText(); // Try to get selected text
    } else {
      showLoginError(data.Error || data.error || 'Login failed');
    }
  } catch (error) {
    console.error('Login error:', error);
    showLoginError('Connection failed. Please try again.');
  } finally {
    loginBtn.disabled = false;
    loginBtn.textContent = 'Sign In';
  }
}

async function logout() {
  authToken = null;
  currentUser = null;
  await chrome.storage.local.remove(['authToken', 'userEmail']);
  showLoginState();
}

function showLoginState() {
  loginSection.classList.remove('hidden');
  userHeader.classList.add('hidden');
  inputSection.classList.add('hidden');
  loadingSection.classList.add('hidden');
  errorSection.classList.add('hidden');
  resultsSection.classList.add('hidden');
  
  emailInput.value = '';
  passwordInput.value = '';
  hideLoginError();
}

function showLoggedInState() {
  loginSection.classList.add('hidden');
  userHeader.classList.remove('hidden');
  inputSection.classList.remove('hidden');
  loadingSection.classList.add('hidden');
  errorSection.classList.add('hidden');
  resultsSection.classList.add('hidden');

  userEmail.textContent = currentUser.Email || currentUser.email;
}

function showLoginError(message) {
  loginError.textContent = message;
  loginError.classList.remove('hidden');
}

function hideLoginError() {
  loginError.classList.add('hidden');
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
    console.log('Could not get selection (may be a restricted page):', error.message);
  }
}

// Show/hide sections
function showInput() {
  loginSection.classList.add('hidden');
  userHeader.classList.remove('hidden');
  inputSection.classList.remove('hidden');
  loadingSection.classList.add('hidden');
  errorSection.classList.add('hidden');
  resultsSection.classList.add('hidden');
}

function showLoading() {
  loginSection.classList.add('hidden');
  userHeader.classList.remove('hidden');
  inputSection.classList.add('hidden');
  loadingSection.classList.remove('hidden');
  errorSection.classList.add('hidden');
  resultsSection.classList.add('hidden');
  loadingMessage.textContent = 'Parsing conversation...';
}

function showError(message) {
  loginSection.classList.add('hidden');
  userHeader.classList.remove('hidden');
  inputSection.classList.add('hidden');
  loadingSection.classList.add('hidden');
  errorSection.classList.remove('hidden');
  resultsSection.classList.add('hidden');
  errorMessage.textContent = message;
}

function showResults() {
  loginSection.classList.add('hidden');
  userHeader.classList.remove('hidden');
  inputSection.classList.add('hidden');
  loadingSection.classList.add('hidden');
  errorSection.classList.add('hidden');
  resultsSection.classList.remove('hidden');
}

// ============================================================
// PROGRESSIVE ANALYSIS - The main refactored function
// ============================================================
async function analyzeProgressive() {
  const text = conversationText.value.trim();
  
  if (!text) {
    alert('Please enter or select a conversation to analyze.');
    return;
  }

  if (!authToken) {
    showLoginState();
    showLoginError('Please sign in to analyze conversations');
    return;
  }

  // Reset all section states
  resetSectionStates();
  showLoading();
  
  const hasDraft = draftText.value.trim();

  try {
    // PHASE 1: Quick parse (instant, no AI)
    const parseResponse = await fetch(`${API_URL}/parse/quick`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${authToken}`
      },
      body: JSON.stringify({ conversationText: text })
    });

    if (parseResponse.status === 401) {
      await logout();
      showLoginError('Session expired. Please sign in again.');
      return;
    }

    if (!parseResponse.ok) {
      throw new Error(`Parse error: ${parseResponse.status}`);
    }

    const parseResult = await parseResponse.json();

    // Initialize capsule with parsed data - user sees this immediately
    currentCapsule = {
      CapsuleId: 'tc-' + Date.now(),
      SourceType: parseResult.sourceType || sourceType.value,
      Participants: parseResult.participants || [],
      Messages: parseResult.messages || [],
      Metadata: parseResult.metadata || {},
      Summary: null,
      Analysis: {
        UnansweredQuestions: null,
        TensionPoints: null,
        Misalignments: null,
        ConversationHealth: null
      },
      SuggestedActions: null
    };

    // Show results immediately with loading spinners
    initializeProgressiveUI(hasDraft);
    showResults();

    // PHASE 2: Fire all AI analysis sections in parallel
    const sections = ['health', 'questions', 'tensions', 'actions', 'misalignments'];
    
    const sectionPromises = sections.map(section => 
      loadSection(text, section)
    );

    // Also analyze draft if provided
    if (hasDraft) {
      sectionPromises.push(loadDraftAnalysis(text, hasDraft));
    }

    // Wait for all to complete (they update UI as they finish)
    await Promise.allSettled(sectionPromises);

  } catch (error) {
    console.error('Analysis error:', error);
    showError(error.message || 'Failed to analyze conversation. Please try again.');
  }
}

function resetSectionStates() {
  Object.keys(sectionStates).forEach(key => {
    sectionStates[key] = { loading: false, complete: false, error: null };
  });
}

// Load a single section and update UI when done
async function loadSection(conversationText, section) {
  setSectionLoading(section, true);
  
  try {
    const response = await fetch(`${API_URL}/analyze/section`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${authToken}`
      },
      body: JSON.stringify({ conversationText, section })
    });

    if (!response.ok) {
      throw new Error(`Section ${section} error: ${response.status}`);
    }

    const result = await response.json();
    
    if (result.success) {
      updateSectionData(section, result.data);
      setSectionComplete(section);
    } else {
      console.error(`Section ${section} failed:`, result.error);
      setSectionError(section, result.error || 'Analysis failed');
    }
  } catch (error) {
    console.error(`Error loading section ${section}:`, error);
    setSectionError(section, error.message);
  }
}

// Load draft analysis
async function loadDraftAnalysis(conversationText, draftMessage) {
  setSectionLoading('draft', true);
  
  try {
    const response = await fetch(`${API_URL}/analyze/section`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${authToken}`
      },
      body: JSON.stringify({ 
        conversationText, 
        section: 'draft',
        draftMessage 
      })
    });

    if (!response.ok) {
      throw new Error(`Draft analysis error: ${response.status}`);
    }

    const result = await response.json();
    
    if (result.success) {
      displayDraftAnalysis(normalizeDraftAnalysis(result.data));
      setSectionComplete('draft');
    } else {
      setSectionError('draft', result.error || 'Draft analysis failed');
    }
  } catch (error) {
    console.error('Error analyzing draft:', error);
    setSectionError('draft', error.message);
  }
}

// Update section data in the capsule and refresh that section's UI
function updateSectionData(section, data) {
  if (!currentCapsule) return;

  switch (section) {
    case 'summary':
      currentCapsule.Summary = data;
      updateSummaryUI(data);
      break;
    case 'questions':
      // Unwrap: data = { UnansweredQuestions: [...] }
      const questions = data?.UnansweredQuestions || data?.unansweredQuestions || data || [];
      currentCapsule.Analysis.UnansweredQuestions = questions;
      updateQuestionsUI(questions);
      break;
    case 'tensions':
      // Unwrap: data = { TensionPoints: [...] }
      const tensions = data?.TensionPoints || data?.tensionPoints || data || [];
      currentCapsule.Analysis.TensionPoints = tensions;
      updateTensionsUI(tensions);
      break;
    case 'health':
      // Unwrap: data = { ConversationHealth: {...} }
      const health = data?.ConversationHealth || data?.conversationHealth || data;
      currentCapsule.Analysis.ConversationHealth = health;
      updateHealthUI(health);
      break;
    case 'actions':
      // Unwrap: data = { SuggestedActions: [...] }
      const actions = data?.SuggestedActions || data?.suggestedActions || data || [];
      currentCapsule.SuggestedActions = actions;
      updateActionsUI(actions);
      break;
    case 'misalignments':
      // Unwrap: data = { Misalignments: [...] }
      const misalignments = data?.Misalignments || data?.misalignments || data || [];
      currentCapsule.Analysis.Misalignments = misalignments;
      updateMisalignmentsUI(misalignments);
      break;
  }
}

// ============================================================
// SECTION LOADING STATE UI
// ============================================================

function setSectionLoading(section, isLoading) {
  sectionStates[section].loading = isLoading;
  
  const sectionMap = {
    health: 'healthSection',
    questions: 'unansweredSection',
    tensions: 'tensionSection',
    actions: 'actionsSection',
    misalignments: 'misalignmentSection',
    draft: 'draftAnalysis'
  };
  
  const sectionEl = document.getElementById(sectionMap[section]);
  if (!sectionEl) return;
  
  // Show section with loading state
  sectionEl.classList.remove('hidden');
  
  // Find or create spinner
  let spinner = sectionEl.querySelector('.section-spinner');
  let content = sectionEl.querySelector('.section-content');
  
  if (isLoading) {
    if (!spinner) {
      spinner = createSpinner();
      // Insert after the header
      const header = sectionEl.querySelector('h3, .section-header');
      if (header) {
        header.after(spinner);
      } else {
        sectionEl.prepend(spinner);
      }
    }
    spinner.classList.remove('hidden');
    if (content) content.classList.add('hidden');
  } else {
    if (spinner) spinner.classList.add('hidden');
    if (content) content.classList.remove('hidden');
  }
}

function setSectionComplete(section) {
  sectionStates[section].loading = false;
  sectionStates[section].complete = true;
  sectionStates[section].error = null;
  
  // Remove spinner, show content
  setSectionLoading(section, false);
}

function setSectionError(section, errorMsg) {
  sectionStates[section].loading = false;
  sectionStates[section].complete = false;
  sectionStates[section].error = errorMsg;
  
  const sectionMap = {
    health: 'healthSection',
    questions: 'unansweredSection',
    tensions: 'tensionSection',
    actions: 'actionsSection',
    misalignments: 'misalignmentSection',
    draft: 'draftAnalysis'
  };
  
  const sectionEl = document.getElementById(sectionMap[section]);
  if (!sectionEl) return;
  
  // Hide spinner
  const spinner = sectionEl.querySelector('.section-spinner');
  if (spinner) spinner.classList.add('hidden');
  
  // Show error
  let errorEl = sectionEl.querySelector('.section-error');
  if (!errorEl) {
    errorEl = document.createElement('div');
    errorEl.className = 'section-error';
    const header = sectionEl.querySelector('h3, .section-header');
    if (header) {
      header.after(errorEl);
    } else {
      sectionEl.prepend(errorEl);
    }
  }
  errorEl.innerHTML = `<span class="error-icon">⚠</span> ${escapeHtml(errorMsg)}`;
  errorEl.classList.remove('hidden');
}

function createSpinner() {
  const spinner = document.createElement('div');
  spinner.className = 'section-spinner';
  spinner.innerHTML = `
    <div class="spinner-dots">
      <span></span><span></span><span></span>
    </div>
    <span class="spinner-text">Analyzing...</span>
  `;
  return spinner;
}

// ============================================================
// UI UPDATE FUNCTIONS - Update individual sections
// ============================================================

function initializeProgressiveUI(hasDraft) {
  // Health section - show loading
  const healthSection = document.getElementById('healthSection');
  if (healthSection) {
    healthSection.classList.remove('hidden');
    setSectionLoading('health', true);
  }
  
  // Questions section - show loading
  const unansweredSection = document.getElementById('unansweredSection');
  if (unansweredSection) {
    unansweredSection.classList.remove('hidden');
    setSectionLoading('questions', true);
  }
  
  // Tensions section - show loading
  const tensionSection = document.getElementById('tensionSection');
  if (tensionSection) {
    tensionSection.classList.remove('hidden');
    setSectionLoading('tensions', true);
  }
  
  // Actions section - show loading
  const actionsSection = document.getElementById('actionsSection');
  if (actionsSection) {
    actionsSection.classList.remove('hidden');
    setSectionLoading('actions', true);
  }
  
  // Draft section - only if there's a draft
  const draftSection = document.getElementById('draftAnalysis');
  if (draftSection) {
    if (hasDraft) {
      draftSection.classList.remove('hidden');
      setSectionLoading('draft', true);
    } else {
      draftSection.classList.add('hidden');
    }
  }
}

function updateSummaryUI(summary) {
  const summaryEl = document.getElementById('conversationSummary');
  if (summaryEl && summary) {
    summaryEl.textContent = summary;
  }
}

function updateQuestionsUI(questions) {
  const unansweredSection = document.getElementById('unansweredSection');
  const unansweredList = document.getElementById('unansweredList');
  
  if (!unansweredSection || !unansweredList) return;
  
  // Wrap content if not already wrapped
  if (!unansweredList.parentElement.classList.contains('section-content')) {
    const wrapper = document.createElement('div');
    wrapper.className = 'section-content';
    unansweredList.parentNode.insertBefore(wrapper, unansweredList);
    wrapper.appendChild(unansweredList);
  }
  
  const content = unansweredSection.querySelector('.section-content');
  
  if (questions && questions.length > 0) {
    unansweredSection.classList.remove('hidden');
    unansweredList.innerHTML = questions.map(q => 
      `<li>❓ ${escapeHtml(q.Question || q.question || '')} <em style="color:#888; font-size:11px;">- ${escapeHtml(q.AskedBy || q.askedBy || 'Unknown')}</em></li>`
    ).join('');
    if (content) content.classList.remove('hidden');
  } else {
    // No questions - show "none found" message
    unansweredList.innerHTML = '<li class="empty-state">No unanswered questions detected ✓</li>';
    if (content) content.classList.remove('hidden');
  }
}

function updateTensionsUI(tensions) {
  const tensionSection = document.getElementById('tensionSection');
  const tensionList = document.getElementById('tensionList');
  
  if (!tensionSection || !tensionList) return;
  
  // Wrap content if not already wrapped
  if (!tensionList.parentElement.classList.contains('section-content')) {
    const wrapper = document.createElement('div');
    wrapper.className = 'section-content';
    tensionList.parentNode.insertBefore(wrapper, tensionList);
    wrapper.appendChild(tensionList);
  }
  
  const content = tensionSection.querySelector('.section-content');
  
  if (tensions && tensions.length > 0) {
    tensionSection.classList.remove('hidden');
    tensionList.innerHTML = tensions.map(t => `
      <div class="tension-item">
        <div class="tension-item-header">
          <span class="tension-item-type">${escapeHtml(t.Type || t.type || 'Tension')}</span>
          <span class="tension-item-severity">${escapeHtml(t.Severity || t.severity || 'medium')}</span>
        </div>
        <p class="tension-item-description">${escapeHtml(t.Description || t.description || '')}</p>
      </div>
    `).join('');
    if (content) content.classList.remove('hidden');
  } else {
    tensionList.innerHTML = '<div class="empty-state">No tension points detected ✓</div>';
    if (content) content.classList.remove('hidden');
  }
}

function updateHealthUI(health) {
  const healthScoreCircle = document.getElementById('healthScoreCircle');
  const healthScoreValue = document.getElementById('healthScoreValue');
  const riskLevel = document.getElementById('riskLevel');
  const healthSummary = document.getElementById('healthSummary');

  if (health) {
    const score = Math.round(health.OverallScore || health.HealthScore || health.healthScore || 0);
    if (healthScoreValue) healthScoreValue.textContent = score;
    
    const level = (health.RiskLevel || health.riskLevel || 'medium').toLowerCase();
    if (healthScoreCircle) {
      healthScoreCircle.className = `score-circle ${score >= 70 ? 'high' : score >= 40 ? 'medium' : 'low'}`;
    }
    
    if (riskLevel) {
      riskLevel.textContent = `${level} risk`;
      riskLevel.className = `risk-badge ${level}`;
    }
    
    const issues = health.Issues || health.issues || [];
    const reasoning = health.Reasoning || health.reasoning || '';
    if (healthSummary) {
      healthSummary.textContent = issues.length > 0 
        ? issues.slice(0, 2).join('. ') 
        : reasoning || 'No major issues detected.';
    }
  }
}

function updateActionsUI(actions) {
  const actionsSection = document.getElementById('actionsSection');
  const actionsList = document.getElementById('actionsList');
  
  if (!actionsSection || !actionsList) return;
  
  // Wrap content if not already wrapped
  if (!actionsList.parentElement.classList.contains('section-content')) {
    const wrapper = document.createElement('div');
    wrapper.className = 'section-content';
    actionsList.parentNode.insertBefore(wrapper, actionsList);
    wrapper.appendChild(actionsList);
  }
  
  const content = actionsSection.querySelector('.section-content');
  
  if (actions && actions.length > 0) {
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
    if (content) content.classList.remove('hidden');
  } else {
    actionsList.innerHTML = '<div class="empty-state">No suggested actions</div>';
    if (content) content.classList.remove('hidden');
  }
}

function updateMisalignmentsUI(misalignments) {
  const misalignSection = document.getElementById('misalignmentSection');
  const misalignList = document.getElementById('misalignmentList');
  
  if (!misalignSection) return;
  
  if (misalignments && misalignments.length > 0) {
    misalignSection.classList.remove('hidden');
    if (misalignList) {
      misalignList.innerHTML = misalignments.map(m => `
        <div class="misalignment-item">
          <p>${escapeHtml(m.Description || m.description || '')}</p>
        </div>
      `).join('');
    }
  } else {
    misalignSection.classList.add('hidden');
  }
}

// ============================================================
// DRAFT ANALYSIS DISPLAY
// ============================================================

function displayDraftAnalysis(draftAnalysis) {
  const draftSection = document.getElementById('draftAnalysis');
  if (!draftAnalysis || !draftSection) return;
  
  // Hide spinner, show content
  const spinner = draftSection.querySelector('.section-spinner');
  if (spinner) spinner.classList.add('hidden');
  
  draftSection.classList.remove('hidden');
  
  // Ready indicator
  const readyIndicator = document.getElementById('readyIndicator');
  if (readyIndicator) {
    readyIndicator.className = `ready-indicator ${draftAnalysis.readyToSend ? 'ready' : 'needs-work'}`;
    readyIndicator.textContent = draftAnalysis.readyToSend ? '✓ Ready to Send' : '⚠ Needs Work';
  }

  // Overall assessment
  const overallAssessment = document.getElementById('overallAssessment');
  if (overallAssessment) {
    overallAssessment.textContent = draftAnalysis.overallAssessment || '';
  }

  // Completeness score
  const completenessBar = document.getElementById('completenessBar');
  const completenessScore = document.getElementById('completenessScore');
  const score = draftAnalysis.completenessScore || 0;
  if (completenessBar) completenessBar.style.width = `${score * 10}%`;
  if (completenessScore) completenessScore.textContent = `${score}/10`;

  // Tone
  const toneSection = document.getElementById('toneSection');
  const toneBadges = document.getElementById('toneBadges');
  const toneExplanation = document.getElementById('toneExplanation');
  
  if (draftAnalysis.tone && toneSection) {
    toneSection.classList.remove('hidden');
    if (toneBadges) {
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
    }
    
    if (toneExplanation) {
      toneExplanation.textContent = draftAnalysis.tone.explanation || '';
    }
  } else if (toneSection) {
    toneSection.classList.add('hidden');
  }

  // Questions ignored
  const questionsIgnoredSection = document.getElementById('questionsIgnoredSection');
  const questionsIgnoredList = document.getElementById('questionsIgnoredList');
  if (draftAnalysis.questionsIgnored && draftAnalysis.questionsIgnored.length > 0 && questionsIgnoredSection) {
    questionsIgnoredSection.classList.remove('hidden');
    if (questionsIgnoredList) {
      questionsIgnoredList.innerHTML = draftAnalysis.questionsIgnored
        .map(q => `<li>${escapeHtml(q)}</li>`)
        .join('');
    }
  } else if (questionsIgnoredSection) {
    questionsIgnoredSection.classList.add('hidden');
  }

  // Risk flags
  const riskFlagsSection = document.getElementById('riskFlagsSection');
  const riskFlagsList = document.getElementById('riskFlagsList');
  if (draftAnalysis.riskFlags && draftAnalysis.riskFlags.length > 0 && riskFlagsSection) {
    riskFlagsSection.classList.remove('hidden');
    if (riskFlagsList) {
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
    }
  } else if (riskFlagsSection) {
    riskFlagsSection.classList.add('hidden');
  }

  // Suggestions
  const suggestionsSection = document.getElementById('suggestionsSection');
  const suggestionsList = document.getElementById('suggestionsList');
  if (draftAnalysis.suggestions && draftAnalysis.suggestions.length > 0 && suggestionsSection) {
    suggestionsSection.classList.remove('hidden');
    if (suggestionsList) {
      suggestionsList.innerHTML = draftAnalysis.suggestions
        .map(s => `<li>${escapeHtml(s)}</li>`)
        .join('');
    }
  } else if (suggestionsSection) {
    suggestionsSection.classList.add('hidden');
  }
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
