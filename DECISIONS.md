# ThreadClear - Technical Decisions Log

> Paste this at the start of Claude sessions to maintain context.

## Data Models
## Architecture

### Project Structure
```
ThreadClear/
├── Extension/                    # Chrome Extension
├── Functions/ThreadClear.Functions/   # .NET Backend (Azure Functions)
├── ThreadClear.Web/              # Angular Frontend
├── index.html, styles.css, etc.  # Marketing site (static)
└── DECISIONS.md                  # This file
```

### Backend (.NET Azure Functions)
```
Functions/ThreadClear.Functions/
├── Functions/                    # HTTP endpoints
│   ├── AnalyzeConversation.cs    # Main analysis endpoint
│   ├── AnalyzeImage.cs, AnalyzeImages.cs, AnalyzeAudio.cs
│   ├── Auth.cs                   # Login/logout
│   ├── Registration.cs           # User signup
│   ├── UserProfileFunction.cs    # Profile updates
│   ├── StripeFunction.cs         # Payments
│   ├── GmailConnectFunction.cs, GmailCallbackFunction.cs, GmailStatusFunction.cs
│   ├── SlackFunction.cs, TeamsFunction.cs
│   ├── Organizations.cs          # Org management
│   ├── AdminFunctions.cs         # Admin operations
│   ├── Insights.cs, StoreInsight.cs
│   ├── Spellcheck.cs, QuickParse.cs
│   └── Helpers/                  # AuthHelper, JsonHelper
├── Services/
│   ├── Interfaces/               # Contracts
│   │   ├── IConversationAnalyzer.cs, IConversationParser.cs
│   │   ├── IAIService.cs         # AI provider abstraction
│   │   ├── IUserService.cs, IAuthService.cs
│   │   ├── IOrganizationService.cs, IOrganizationRepository.cs
│   │   ├── IInsightService.cs, IInsightRepository.cs
│   │   ├── IThreadCapsuleBuilder.cs
│   │   └── IUsageService.cs, ISpellCheckService.cs, ITaxonomyService.cs
│   └── Implementations/
│       ├── ConversationAnalyzer.cs    # Core analysis logic
│       ├── ConversationParser.cs      # Text parsing
│       ├── ThreadCapsuleBuilder.cs    # Builds ThreadCapsule data structure
│       ├── ClaudeAIService.cs         # Claude API (primary)
│       ├── AnthropicAIService.cs, OpenAIService.cs, GeminiAIService.cs
│       ├── UserService.cs, AuthService.cs
│       ├── OrganizationService.cs, OrganizationRepository.cs
│       ├── StripeService.cs, UsageService.cs
│       └── SpellCheckService.cs, TaxonomyService.cs, InsightService.cs
├── Models/
│   ├── ThreadCapsule.cs          # Core data structure
│   ├── Message.cs, Participant.cs
│   ├── DraftAnalysis.cs, AnalysisRequest.cs, AnalysisOptions.cs
│   ├── User.cs, Organization.cs, OrganizationMembership.cs
│   └── TierLimits.cs, StorableInsight.cs
├── Data/
│   └── IndustryTemplates.cs
└── Program.cs                    # DI configuration
```

### Frontend (Angular)
```
ThreadClear.Web/src/app/
├── app.component.ts              # Root + nav bar
├── app.module.ts                 # Module imports
├── app-routing.module.ts         # Routes
├── components/
│   ├── conversation-analyzer/    # Main input UI
│   ├── results-display/          # Analysis results
│   ├── dashboard/                # Org dashboard
│   ├── profile/                  # User profile, usage, integrations
│   ├── login/, register/         # Auth pages
│   ├── admin/                    # System admin
│   ├── organization-settings/    # Org admin
│   ├── connect/                  # OAuth callbacks
│   ├── privacy/, terms/          # Legal pages
├── services/
│   ├── api.service.ts            # HTTP calls to backend
│   ├── auth.service.ts           # Auth state, user observable
│   ├── admin.service.ts          # Admin operations
│   ├── organization.service.ts   # Org operations
│   ├── registration.service.ts   # Signup
│   ├── taxonomy.service.ts       # Categories
│   └── insights.service.ts       # Stored insights
├── pipes/
│   └── highlight-spelling.pipe.ts
└── environments/
    ├── environment.ts            # Local API URL
    └── environment.prod.ts       # Prod API URL
```

### Chrome Extension
```
Extension/
├── manifest.json                 # Extension config
├── popup.html, popup.js, popup.css   # Popup UI
├── content.js                    # Page content script
└── icons/                        # Extension icons
```

### Key Data Flow
1. User pastes conversation → `conversation-analyzer.component.ts`
2. Calls `api.service.ts` → POST `/api/analyze`
3. `AnalyzeConversation.cs` → `ConversationParser` → `ThreadCapsuleBuilder` → `ConversationAnalyzer`
4. `ConversationAnalyzer` uses `ClaudeAIService` for AI analysis
5. Returns `ThreadCapsule` → `results-display.component.ts` renders results

### AI Service Abstraction
- `IAIService` interface allows swapping providers
- Implementations: Claude (primary), Anthropic, OpenAI, Gemini
- Hybrid approach: 80-90% regex parsing, 10-20% AI enhancement

### Authentication
- Session timeout: 30 minutes of inactivity
- Activity events: mousedown, mousemove, keypress, scroll, touchstart
- Timer resets on any user activity
- Credentials stored in localStorage as base64

### User Model
- Properties: `email`, `role`, `displayName`, `permissions`, `plan`, `stripeCustomerId`, `stripeSubscriptionId`
- Roles: `user`, `admin`
- **API returns PascalCase** (e.g., `Role`, `DisplayName`) - must normalize to camelCase in `auth.service.ts`
- Normalized properties: `role`, `displayName`, `permissions`, `plan` (add more as needed)
- User plan is used when user has no organization
- Added `plan?: string` to User interface in auth.service.ts
- Added `updateCurrentUser(user)` method to AuthService for syncing profile changes to navbar

### Organization
- **Organizations are optional** - users can operate without one
- If no org, hide org-related UI (don't show "Personal Workspace")
- `hasOrganization` flag in app.component.ts controls visibility
- Org membership is in OrganizationMemberships table (not on Users table)
- Organization plan takes precedence over user plan

### UsageTracking
- Columns: UserId, OrganizationId (nullable), Period, AnalysisCount, GmailThreadsAnalyzed, SpellChecksRun, AITokensUsed
- OrganizationId is nullable - allows tracking for users without orgs
- Usage rolls up to org level if user is in an org

### TierLimits Table
- Database-driven tier configuration (no hardcoded limits)
- Columns: TierName, MonthlyAnalyses, MonthlyGmailThreads, MonthlySpellChecks, MonthlyAITokens, PriceMonthly, StripePriceId
- Tiers: free (10 analyses), pro (100 analyses, $19.99), enterprise (10000 analyses, $99.99)

### UserIntegrations Table
- Columns: Id, UserId (UNIQUEIDENTIFIER), Provider, Email, RefreshToken, AccessToken, TokenExpiry, Scopes, CreatedAt, UpdatedAt
- UserId matches Users.Id type (UNIQUEIDENTIFIER, not INT)
- Unique constraint on (UserId, Provider)
- Provider values: 'gmail', 'outlook', 'slack' (future)

## Frontend (Angular)

### Navigation
- Nav bar only shows when logged in AND not on auth pages
- User name displayed in top-right with gear icon for profile
- Settings link only visible when `isAdmin` (system admin)
- Admin link only visible when `user.role === 'admin'`

### Pages
- `/analyze` - main conversation analysis
- `/dashboard` - org dashboard
- `/settings` - org settings (admin only)
- `/admin` - system admin (admin only)
- `/profile` - user profile with plan/usage/upgrade/integrations (all users)

### Features Implemented
- Spell check: checkbox toggle, wavy underlines for errors
- Usage tracking: UsageService, UsageTracking table
- Progressive loading: 2-3 second time-to-first-content
- Conversation Health: bar chart styling complete
- Profile page with usage display and Stripe upgrade
- Profile: Edit display name (inline edit, syncs to navbar)
- Profile: Change password (with validation, success banner)
- Profile: Selection-based plan picker (select then save)
- Profile: Gmail integration status and connect button

### Patterns
- Use `*ngIf` for conditional rendering
- Subscribe to observables in `ngOnInit`
- Guard against null user emissions resetting state (wrap in `if (user)`)
- Save form values to local variables BEFORE async calls (avoid closure issues)

## Backend (Azure Functions C#)

### Architecture
- Clean architecture with dependency injection
- Hybrid parsing: 80-90% regex, 10-20% AI
- Ephemeral processing - zero data retention of conversation content

### Database
- Azure SQL Database (free tier)
- **`Plan` is a reserved SQL keyword** - always use `[Plan]` in queries
- DatabaseKeepAlive timer function runs every 5 minutes to prevent sleep

### Configuration
- **Use `configuration["SqlConnectionString"]`** - NOT `GetConnectionString("SqlConnectionString")`
- Azure config keys are at root level, not in ConnectionStrings section
- For local development, add secrets to `local.settings.json` (gitignored)

### Endpoints
- `/api/stripe/checkout` - creates Stripe checkout session
- `/api/stripe/webhook` - handles Stripe events
- `/api/stripe/cancel` - cancels subscription, downgrades to free
- `/api/usage/me` - returns user's current usage and limits
- `/api/users/me/profile` - update display name (PUT)
- `/api/users/me/password` - change password (PUT)
- `/api/gmail/connect` - initiates Gmail OAuth flow
- `/api/gmail/callback` - handles Google OAuth redirect, stores tokens
- `/api/integrations/{userId}/gmail` - returns Gmail connection status

### Stripe Integration
- **Test mode** - switch to live when ready with EIN
- Publishable key: `pk_test_51SqJtF...`
- Secret key: in Azure config as `StripeSecretKey`
- Webhook secret: in Azure config as `StripeWebhookSecret`
- Pro price ID: `price_1SqK1qFCegaUzUfPwrO5qEDF`
- Enterprise price ID: `price_1SqK2JFCegaUzUfPXooTuvHF`
- Webhook URL: `https://threadclear-functions-fsewbzcsd8fuegdj.eastus-01.azurewebsites.net/api/stripe/webhook`
- Billing Portal URL (test): `https://billing.stripe.com/p/login/test_00w7sL3GYdcC3yw9jleIw00`
- Events: checkout.session.completed, customer.subscription.deleted, invoice.payment_failed

### Gmail/Google Integration
- Google Cloud project: `ThreadClear` (under bhm-it.com temporarily)
- **TODO**: Transfer project ownership to michael@threadclear.com when Workspace unlocks
- Gmail API enabled
- OAuth scopes: `gmail.readonly`, `userinfo.email`
- OAuth consent screen: External, Testing mode
- Test users: michael@bhm-it.com
- Azure config keys: `GoogleClientId`, `GoogleClientSecret`, `GoogleRedirectUri`
- Redirect URI: `https://threadclear-functions-fsewbzcsd8fuegdj.eastus-01.azurewebsites.net/api/gmail/callback`

## Working Patterns with Claude

### Before Making Changes
1. **Commit first** - `git add . && git commit -m "before changes"`
2. **Upload current files** - not old copies from Downloads
3. **Ask for line-by-line edits** - not full file replacements
4. **Test one file at a time** - don't replace 4 files at once
5. **If something breaks** - `git restore .` to undo uncommitted changes

### Code Changes
- **Never paste full file replacements** - too easy to overwrite existing changes
- Provide only the specific lines/sections changing
- Use diff-style or "find and replace" format
- When in doubt, ask before suggesting large changes
- **User makes changes independently** - don't assume code is the same as last seen; ask or request current version before editing

### Git Commits
- Provide full runnable `git add` and `git commit` commands
- Windows Command Prompt doesn't handle multi-line commits well - use single line
- Example: `git commit -m "feat: add Gmail OAuth integration"`
- **Commit after each successful change** - small commits are easier to revert

### Deployment
- Frontend: `ng build --configuration=production` then `swa deploy`
- Backend: Visual Studio → Publish (NOT `func azure functionapp publish`)

## Rejected Approaches
- Full file replacements in code suggestions (too risky)
- `GetConnectionString()` for Azure config (keys are at root level)
- Multi-step upgrade buttons per plan (use selection-based picker instead)

## Deferred Features
- **User invite emails**: Manual link copy/paste for MVP, SendGrid integration later
- **Gmail Add-on sidebar**: Apps Script, Marketplace submission (8-12 hours)
- **Google Workspace**: michael@threadclear.com account locked, needs review (up to 72 hours)

## Completed (Jan 16, 2026)
- ✅ Fixed navbar user display (displayName, org name)
- ✅ Fixed admin visibility (Settings/Admin nav items)
- ✅ Database keep-alive timer function (every 5 min)
- ✅ Session timeout (30 min inactivity)
- ✅ TierLimits table with database-driven limits
- ✅ Stripe integration (products, checkout, webhooks)
- ✅ Profile page with usage and upgrade buttons
- ✅ /usage/me endpoint

## Completed (Jan 17, 2026)
- ✅ Google Cloud project setup (Gmail API, OAuth consent, credentials)
- ✅ UserIntegrations table for OAuth tokens
- ✅ Gmail OAuth flow (connect, callback, status endpoints)
- ✅ "Connect Gmail" button on profile page
- ✅ Profile: Edit display name with navbar sync
- ✅ Profile: Change password functionality
- ✅ Profile: Selection-based plan picker (select then save)
- ✅ Stripe cancel subscription endpoint
- ✅ IUserService.UpdatePassword method
- ✅ AuthService.updateCurrentUser method
- ✅ StripeService: GetUserSubscriptionId, UpdateUserPlan overload

## Next Up (Gmail Integration Remaining)
- Phase 1C: Fetch & analyze Gmail threads (~1.5 hours)
  - Service to fetch recent threads using stored tokens
  - Token refresh logic
  - Run threads through ConversationAnalyzer
  - Aggregate results
- Phase 1D: Daily digest email via SendGrid (~1 hour)
  - SendGrid account setup + API key
  - Email template with analysis summary
  - Timer-triggered Azure Function
  
## Completed (Jan 22, 2026)
- ✅ Purple theme: header, analyzer, results display
- ✅ Updated nav icons (line chart, person)
- ✅ Transparent logo
- ✅ Added Architecture section to DECISIONS.md
- ✅ Added git workflow guidelines (Before Making Changes)  

---
*Last updated: January 22, 2026*
