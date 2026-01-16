# ThreadClear - Technical Decisions Log

> Paste this at the start of Claude sessions to maintain context.

## Data Models

### Authentication
- Session timeout: 30 minutes of inactivity
- Activity events: mousedown, mousemove, keypress, scroll, touchstart
- Timer resets on any user activity
- Credentials stored in localStorage as base64

### User Model
- Properties: `email`, `role`, `displayName`, `permissions`, `plan`, `stripeCustomerId`, `stripeSubscriptionId`
- Roles: `user`, `admin`
- **API returns PascalCase** (e.g., `Role`, `DisplayName`) - must normalize to camelCase in `auth.service.ts`
- Normalized properties: `role`, `displayName`, `permissions` (add more as needed)
- User plan is used when user has no organization

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
- `/profile` - user profile with plan/usage/upgrade (all users)

### Features Implemented
- Spell check: checkbox toggle, wavy underlines for errors
- Usage tracking: UsageService, UsageTracking table
- Progressive loading: 2-3 second time-to-first-content
- Conversation Health: bar chart styling complete
- Profile page with usage display and Stripe upgrade

### Patterns
- Use `*ngIf` for conditional rendering
- Subscribe to observables in `ngOnInit`
- Guard against null user emissions resetting state (wrap in `if (user)`)

## Backend (Azure Functions C#)

### Architecture
- Clean architecture with dependency injection
- Hybrid parsing: 80-90% regex, 10-20% AI
- Ephemeral processing - zero data retention of conversation content

### Database
- Azure SQL Database (free tier)
- **`Plan` is a reserved SQL keyword** - always use `[Plan]` in queries
- DatabaseKeepAlive timer function runs every 5 minutes to prevent sleep

### Endpoints
- `/api/stripe/checkout` - creates Stripe checkout session
- `/api/stripe/webhook` - handles Stripe events
- `/api/usage/me` - returns user's current usage and limits

### Stripe Integration
- **Test mode** - switch to live when ready with EIN
- Publishable key: `pk_test_51SqJtF...`
- Secret key: in Azure config as `StripeSecretKey`
- Webhook secret: in Azure config as `StripeWebhookSecret`
- Pro price ID: `price_1SqK1qFCegaUzUfPwrO5qEDF`
- Enterprise price ID: `price_1SqK2JFCegaUzUfPXooTuvHF`
- Webhook URL: `https://threadclear-functions-fsewbzcsd8fuegdj.eastus-01.azurewebsites.net/api/stripe/webhook`
- Events: checkout.session.completed, customer.subscription.deleted, invoice.payment_failed

## Working Patterns with Claude

### Code Changes
- **Never paste full file replacements** - too easy to overwrite existing changes
- Provide only the specific lines/sections changing
- Use diff-style or "find and replace" format
- When in doubt, ask before suggesting large changes
- **User makes changes independently** - don't assume code is the same as last seen; ask or request current version before editing

## Rejected Approaches
<!-- Add things we tried and decided against -->

## Deferred Features
- **User invite emails**: Manual link copy/paste for MVP, SendGrid integration later
- **Gmail integration**: Estimated 12-18 hours total
  - Part 1: Daily digest email (4-6 hours) - Gmail API OAuth, timer function, SendGrid
  - Part 2: Gmail Add-on sidebar (8-12 hours) - Apps Script, Marketplace submission
  - Dependencies: Google Cloud Console project, Gmail API, OAuth consent screen, SendGrid account
  - Recommendation: Start with Part 1 (daily digest)

## Completed This Session (Jan 16, 2026)
- ✅ Fixed navbar user display (displayName, org name)
- ✅ Fixed admin visibility (Settings/Admin nav items)
- ✅ Database keep-alive timer function (every 5 min)
- ✅ Session timeout (30 min inactivity)
- ✅ TierLimits table with database-driven limits
- ✅ Stripe integration (products, checkout, webhooks)
- ✅ Profile page with usage and upgrade buttons
- ✅ /usage/me endpoint

---
*Last updated: January 16, 2026*
