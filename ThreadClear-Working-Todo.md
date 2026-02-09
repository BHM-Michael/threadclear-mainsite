# ThreadClear Working Todo List
*Last updated: February 6, 2026*

---

## 🔴 HIGH PRIORITY

| # | Item | Effort |
|---|------|--------|
| 0 | **Analysis persistence schema + API** — analysis results table (analysisId, userId, orgId nullable, source, channelLabel, timestamp, healthScore, riskLevel, participantCount) + findings table (findingType, category, severity — no content/names). Wire analyze endpoint to save metrics. Prerequisite for #2 and #3. | 4-6 hrs |
| 1 | Gmail Add-on sidebar — real-time inbox analysis | 6-8 hrs |
| 2 | UI refresh / Analysis Hub — modernize app.threadclear.com, unified view of all analyses across integrations | 6-8 hrs |
| 3 | Revisit org dashboard design — clarify audience, refine Top Topics/Trends | 3-4 hrs |
| 4 | Registration admin approval | 2 hrs |

## 🟠 MEDIUM-HIGH PRIORITY

| # | Item | Effort |
|---|------|--------|
| 5 | Demo video/GIF | 2-3 hrs |
| 6 | FAQ section on site | 1 hr |
| 7 | Cross-platform identity mapping — alias storage, unified analysis, opt-in score tracking | 8-12 hrs |

## 🟡 MEDIUM PRIORITY

| # | Item | Effort |
|---|------|--------|
| 8 | Outlook AppSource submission — Partner Center, manifest, store assets, listing, testing | 4-6 hrs |
| 9 | Integrate Grok analysis prompt — industry taxonomy, sales coaching variant, defensive JSON | 3-4 hrs |
| 10 | User docs | 2-3 hrs |
| 11 | Manager drill-down — tension trend insights, summarized (no raw threads), category breakdowns, coaching prompts. Privacy-first: patterns not messages. | TBD |

## 🟢 LOWER PRIORITY

| # | Item | Effort |
|---|------|--------|
| 12 | Browser extension enhancements | 3-4 hrs |
| 13 | Sales coaching mode (test with stepson) | 2-3 hrs |
| 14 | Compare AI services / Azure AI eval for enterprise tier | 2-3 hrs |
| 15 | IP strategy | 1 hr |
| 16 | Android XML upload/fix | 1-2 hrs |
| 17 | Transfer Google Cloud project ownership to michael@threadclear.com | 30 min |

## ⚠️ Also Needs Attention

- **Slack workspace expired** — need new free workspace + reinstall app + new bot token (30 min when ready)
- **Analysis persistence** — each integration needs to call the save endpoint after analysis so "View Full Report" works (ties into #0 and #2)

## 📐 Key Architecture Decisions (from Feb 4 discussion)

- **Privacy model**: Store aggregate metrics + finding categories only. No conversation content, no names, no quotes ever.
- **Org vs solo**: `organizationId` nullable — org users roll up to org dashboard, solo users see personal trends.
- **Integration flow**: Analyze → save metrics via API → hub displays history. Each integration calls the same save endpoint.
- **Manager drill-down**: See category/type/severity per channel, then go look at the actual conversation themselves. ThreadClear is a compass, not a recording device.
- **Build sequence**: Schema → API → Update integrations → Build hub UI

## ✅ Completed (from original list of 15)

- Login timeout
- Header modernization
- "Go to Analyzer" button removed
- Sitewide modern UI
- Draft div collapsed by default
- Site color scheme
- Analysis result colors improved
- Pricing section removed
- Icons updated
