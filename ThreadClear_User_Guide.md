# ThreadClear User Guide

## What is ThreadClear?

ThreadClear is a conversation analysis platform that helps you identify communication issues in your emails, chats, and messages. It detects:

- **Unanswered Questions** - Questions that were asked but never addressed
- **Tension Points** - Signs of conflict, frustration, or disagreement
- **Misalignments** - Differing expectations or misunderstandings
- **Conversation Health Score** - Overall communication effectiveness rating
- **Suggested Actions** - Recommendations to improve the conversation

---

## Getting Started

### Creating an Account

1. Go to [app.threadclear.com](https://app.threadclear.com)
2. Click **Sign Up**
3. Enter your email and create a password
4. Verify your email address
5. Log in to start analyzing conversations

### Your First Analysis

1. Copy a conversation (email thread, chat, text messages)
2. Paste it into the analysis box
3. Click **Analyze**
4. Review your results

---

## Using ThreadClear

### Web App (app.threadclear.com)

The web app is your main dashboard for analyzing conversations.

**To analyze a conversation:**
1. Log in at [app.threadclear.com](https://app.threadclear.com)
2. Go to **Analyze**
3. Paste your conversation text
4. Click **Analyze**

**Supported input methods:**
- **Text paste** - Copy/paste any conversation
- **Image upload** - Upload screenshots of conversations (up to 10 images)
- **Audio upload** - Upload voice recordings for transcription and analysis

---

## Integrations

ThreadClear integrates with the tools you already use. Each integration lets you analyze conversations without leaving your workflow.

---

### Microsoft Outlook Integration

Analyze email threads directly from your Outlook inbox.

#### Installation (Admin)
1. Your Microsoft 365 admin deploys the add-in from the Admin Center
2. The add-in appears automatically in your Outlook

#### Installation (Individual)
1. In Outlook, click **Get Add-ins** (or **Store**)
2. Search for "ThreadClear"
3. Click **Add**

#### How to Use

1. **Open an email** you want to analyze
2. Look for the **ThreadClear** button in the ribbon or message toolbar
3. Click **ThreadClear** → **Analyze This Thread**
4. View your analysis results in the sidebar

**What gets analyzed:**
- The email thread you're viewing
- All messages in the conversation chain

**Tips:**
- Works in Outlook desktop, web, and mobile
- Your email content is processed securely and not stored

---

### Slack Integration

Analyze conversations directly in Slack using slash commands.

#### Installation (Workspace Admin)
1. Visit the ThreadClear Slack app page
2. Click **Add to Slack**
3. Authorize the app for your workspace
4. The `/threadclear` command is now available

#### How to Use

**Analyze pasted text:**
```
/threadclear Customer: Your price is too high.
Seller: We offer premium features.
Customer: I'm not convinced.
```

**Get help:**
```
/threadclear help
```

**Check your usage:**
```
/threadclear status
```

**Connect to your ThreadClear account (for unlimited analyses):**
```
/threadclear connect
```

#### Usage Limits
- **Free:** 20 analyses per month per workspace
- **Connected to Pro org:** Unlimited analyses

#### Connecting Your Workspace

To get unlimited analyses:
1. Type `/threadclear connect` in Slack
2. Click the **Connect Workspace** button
3. Log in to your ThreadClear account
4. Click **Connect Workspace**
5. Your Slack workspace is now linked!

---

### Microsoft Teams Integration

Analyze conversations in Teams by chatting with the ThreadClear bot.

#### Installation (Admin)
1. Your Teams admin uploads the ThreadClear app to the Teams Admin Center
2. The app is deployed to users in your organization

#### Installation (Individual)
1. In Teams, click **Apps** in the sidebar
2. Search for "ThreadClear"
3. Click **Add**

#### How to Use

**Direct message the bot:**
1. Find **ThreadClear** in your Apps or Chats
2. Start a conversation
3. Paste the text you want to analyze
4. Send the message
5. Receive your analysis results

**In a channel:**
1. Add ThreadClear to your Team
2. @mention the bot with text to analyze:
```
@ThreadClear Customer: Your price is too high.
Seller: We offer premium features.
Customer: I'm not convinced.
```

**Available commands:**
- `help` - See how to use ThreadClear
- `status` - Check your usage and connection status
- `connect` - Link your workspace to your ThreadClear account

#### Usage Limits
- **Free:** 20 analyses per month per tenant
- **Connected to Pro org:** Unlimited analyses

#### Connecting Your Teams Tenant

To get unlimited analyses:
1. Message the bot: `connect`
2. Click the **Connect Organization** button
3. Log in to your ThreadClear account
4. Click **Connect Workspace**
5. Your Teams tenant is now linked!

---

## Understanding Your Results

### Health Score

The health score (0-100%) indicates overall conversation quality:

| Score | Meaning |
|-------|---------|
| 80-100% | Healthy - Good communication |
| 60-79% | Fair - Some issues to address |
| 40-59% | Concerning - Multiple issues present |
| 0-39% | Critical - Significant communication problems |

### Risk Level

- 🟢 **Low** - Conversation is on track
- 🟡 **Medium** - Some attention needed
- 🔴 **High** - Immediate attention recommended

### Analysis Components

**Responsiveness** - Are people responding to each other?

**Clarity** - Is communication clear and unambiguous?

**Alignment** - Are participants on the same page?

---

## Organizations

ThreadClear supports team and organization accounts for businesses.

### Creating an Organization

1. Log in to ThreadClear
2. Go to **Settings** → **Organization**
3. Click **Create Organization**
4. Enter your organization name and industry
5. Invite team members

### Inviting Team Members

1. Go to **Settings** → **Organization** → **Members**
2. Click **Invite Member**
3. Enter their email address
4. Select their role (Admin, Member)
5. They'll receive an invitation email

### Roles

- **Owner** - Full control, billing, can delete organization
- **Admin** - Manage members, settings, and integrations
- **Member** - Use ThreadClear for analysis

### Connecting Integrations to Your Organization

When you connect Slack or Teams to your organization:
- All workspace/tenant users get access
- Usage counts against your organization's plan
- Pro organizations get unlimited analyses

---

## Plans and Pricing

### Free Plan
- 20 analyses per month
- Web app access
- Basic integrations

### Pro Plan
- Unlimited analyses
- All integrations
- Priority support
- Organization features

### Enterprise Plan
- Everything in Pro
- Self-hosted deployment option
- Dedicated support
- Custom integrations

Visit [threadclear.com/pricing](https://threadclear.com/pricing) for current pricing.

---

## Privacy and Security

### Your Data is Safe

- **Ephemeral processing** - Conversation content is analyzed and immediately discarded
- **No storage** - We do not store your conversation text
- **Encryption** - All data is encrypted in transit and at rest
- **Compliance** - Suitable for regulated industries

### What We Store

- Account information (email, password hash)
- Usage statistics (number of analyses)
- Organization and membership data

### What We Don't Store

- Your conversation content
- Email bodies
- Chat messages
- Any text you submit for analysis

See our full [Privacy Policy](https://app.threadclear.com/privacy) for details.

---

## Troubleshooting

### "Authentication required" error

- Make sure you're logged in
- Try logging out and back in
- Clear your browser cache

### Outlook add-in not appearing

- Check with your Microsoft 365 admin
- Try restarting Outlook
- Ensure the add-in is enabled in your settings

### Slack command not working

- Make sure ThreadClear is installed in your workspace
- Check `/threadclear status` to see if you're connected
- If you see "operation_timeout", try again (database may be warming up)

### Teams bot not responding

- Make sure the app is installed
- Try messaging the bot directly instead of in a channel
- Check with your Teams admin

### Analysis taking too long

- Large conversations may take 15-20 seconds
- If it times out, try a shorter portion of the conversation

---

## Support

Need help? Contact us:

- **Email:** support@threadclear.com
- **Website:** [threadclear.com](https://threadclear.com)

---

## Quick Reference

| Platform | How to Analyze |
|----------|----------------|
| **Web App** | Paste text at app.threadclear.com |
| **Outlook** | Click ThreadClear button → Analyze This Thread |
| **Slack** | `/threadclear [paste conversation]` |
| **Teams** | Message @ThreadClear with your conversation |

| Command | Slack | Teams |
|---------|-------|-------|
| Analyze | `/threadclear [text]` | `@ThreadClear [text]` |
| Help | `/threadclear help` | `help` |
| Status | `/threadclear status` | `status` |
| Connect | `/threadclear connect` | `connect` |

---

*Last updated: January 2026*
