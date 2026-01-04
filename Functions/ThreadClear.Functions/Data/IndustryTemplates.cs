using System;
using System.Collections.Generic;
using ThreadClear.Functions.Models;

namespace ThreadClear.Functions.Data
{
    public static class IndustryTemplates
    {
        public static TaxonomyData GetTemplate(string industryType)
        {
            return industryType.ToLower() switch
            {
                "legal" => GetLegalTemplate(),
                "healthcare" => GetHealthcareTemplate(),
                "finance" => GetFinanceTemplate(),
                "retail" => GetRetailTemplate(),
                "technology" => GetTechnologyTemplate(),
                _ => GetDefaultTemplate()
            };
        }

        public static List<string> GetAvailableIndustries()
        {
            return new List<string>
            {
                "default",
                "legal",
                "healthcare",
                "finance",
                "retail",
                "technology"
            };
        }

        private static TaxonomyData GetLegalTemplate()
        {
            var template = GetDefaultTemplate();

            // Add legal-specific topics
            template.Topics.AddRange(new[]
            {
                new TopicDefinition
                {
                    Key = "discovery",
                    DisplayName = "Discovery",
                    Keywords = new[] { "discovery", "subpoena", "deposition", "interrogatories", "document request" }
                },
                new TopicDefinition
                {
                    Key = "privilege",
                    DisplayName = "Privilege",
                    Keywords = new[] { "privilege", "confidential", "attorney-client", "work product" }
                },
                new TopicDefinition
                {
                    Key = "deadline_court",
                    DisplayName = "Court Deadline",
                    Keywords = new[] { "filing deadline", "court date", "hearing", "motion due", "statute of limitations" }
                },
                new TopicDefinition
                {
                    Key = "settlement",
                    DisplayName = "Settlement",
                    Keywords = new[] { "settlement", "offer", "mediation", "arbitration", "negotiate" }
                },
                new TopicDefinition
                {
                    Key = "conflict_check",
                    DisplayName = "Conflict Check",
                    Keywords = new[] { "conflict", "conflict check", "adverse party", "representation" }
                },
                new TopicDefinition
                {
                    Key = "case_status",
                    DisplayName = "Case Status",
                    Keywords = new[] { "case", "matter", "docket", "filing", "pleading" }
                }
            });

            // Add legal-specific roles
            template.Roles.AddRange(new[]
            {
                new RoleDefinition { Key = "attorney", DisplayName = "Attorney", Keywords = new[] { "esq", "attorney", "counsel", "lawyer", "jd" } },
                new RoleDefinition { Key = "paralegal", DisplayName = "Paralegal", Keywords = new[] { "paralegal", "legal assistant" } },
                new RoleDefinition { Key = "client", DisplayName = "Client", Keywords = new[] { "client" } },
                new RoleDefinition { Key = "opposing_counsel", DisplayName = "Opposing Counsel", Keywords = new[] { "opposing", "plaintiff counsel", "defendant counsel" } },
                new RoleDefinition { Key = "court", DisplayName = "Court", Keywords = new[] { "court", "judge", "clerk", "magistrate" } }
            });

            // Legal-specific severity rules
            template.SeverityRules.AddRange(new[]
            {
                new SeverityRule
                {
                    Category = "QUESTION_STATUS",
                    Value = "unanswered",
                    Condition = "topic == 'deadline_court'",
                    Severity = "critical"
                },
                new SeverityRule
                {
                    Category = "ACTION_ITEM",
                    Value = "overdue",
                    Condition = "topic == 'deadline_court'",
                    Severity = "critical"
                }
            });

            return template;
        }

        private static TaxonomyData GetHealthcareTemplate()
        {
            var template = GetDefaultTemplate();

            template.Topics.AddRange(new[]
            {
                new TopicDefinition
                {
                    Key = "patient_care",
                    DisplayName = "Patient Care",
                    Keywords = new[] { "patient", "treatment", "diagnosis", "symptoms", "medication", "prescription", "care plan" }
                },
                new TopicDefinition
                {
                    Key = "hipaa",
                    DisplayName = "HIPAA/Privacy",
                    Keywords = new[] { "hipaa", "privacy", "phi", "protected health", "authorization", "consent", "release" }
                },
                new TopicDefinition
                {
                    Key = "insurance_auth",
                    DisplayName = "Insurance/Authorization",
                    Keywords = new[] { "prior auth", "authorization", "insurance", "coverage", "pre-approval", "denial", "appeal" }
                },
                new TopicDefinition
                {
                    Key = "referral",
                    DisplayName = "Referral",
                    Keywords = new[] { "referral", "refer", "specialist", "consult", "consultation" }
                },
                new TopicDefinition
                {
                    Key = "lab_results",
                    DisplayName = "Lab Results",
                    Keywords = new[] { "lab", "results", "test", "bloodwork", "imaging", "scan", "mri", "ct", "xray" }
                },
                new TopicDefinition
                {
                    Key = "appointment",
                    DisplayName = "Appointment",
                    Keywords = new[] { "appointment", "schedule", "visit", "follow-up", "checkup" }
                },
                new TopicDefinition
                {
                    Key = "medication",
                    DisplayName = "Medication",
                    Keywords = new[] { "medication", "prescription", "rx", "refill", "dosage", "drug" }
                }
            });

            template.Roles.AddRange(new[]
            {
                new RoleDefinition { Key = "physician", DisplayName = "Physician", Keywords = new[] { "dr", "doctor", "md", "do", "physician" } },
                new RoleDefinition { Key = "nurse", DisplayName = "Nurse", Keywords = new[] { "rn", "nurse", "lpn", "np", "nurse practitioner" } },
                new RoleDefinition { Key = "patient", DisplayName = "Patient", Keywords = new[] { "patient" } },
                new RoleDefinition { Key = "insurance_rep", DisplayName = "Insurance Rep", Keywords = new[] { "insurance", "claims", "adjuster", "payer" } },
                new RoleDefinition { Key = "medical_assistant", DisplayName = "Medical Assistant", Keywords = new[] { "ma", "medical assistant", "cma" } },
                new RoleDefinition { Key = "pharmacist", DisplayName = "Pharmacist", Keywords = new[] { "pharmacist", "pharmacy", "rph" } }
            });

            template.SeverityRules.AddRange(new[]
            {
                new SeverityRule
                {
                    Category = "QUESTION_STATUS",
                    Value = "unanswered",
                    Condition = "topic == 'patient_care'",
                    Severity = "high"
                },
                new SeverityRule
                {
                    Category = "RISK_INDICATOR",
                    Value = "*",
                    Condition = "topic == 'hipaa'",
                    Severity = "critical"
                }
            });

            return template;
        }

        private static TaxonomyData GetFinanceTemplate()
        {
            var template = GetDefaultTemplate();

            template.Topics.AddRange(new[]
            {
                new TopicDefinition
                {
                    Key = "transaction",
                    DisplayName = "Transaction",
                    Keywords = new[] { "transaction", "transfer", "wire", "ach", "payment", "deposit", "withdrawal" }
                },
                new TopicDefinition
                {
                    Key = "compliance_reg",
                    DisplayName = "Regulatory Compliance",
                    Keywords = new[] { "compliance", "sec", "finra", "aml", "kyc", "regulation", "audit", "sox" }
                },
                new TopicDefinition
                {
                    Key = "account",
                    DisplayName = "Account",
                    Keywords = new[] { "account", "balance", "statement", "portfolio", "holdings" }
                },
                new TopicDefinition
                {
                    Key = "risk_exposure",
                    DisplayName = "Risk/Exposure",
                    Keywords = new[] { "risk", "exposure", "hedge", "margin", "collateral", "leverage" }
                },
                new TopicDefinition
                {
                    Key = "fraud",
                    DisplayName = "Fraud",
                    Keywords = new[] { "fraud", "suspicious", "unauthorized", "dispute", "chargeback", "identity theft" }
                },
                new TopicDefinition
                {
                    Key = "investment",
                    DisplayName = "Investment",
                    Keywords = new[] { "investment", "portfolio", "stock", "bond", "fund", "etf", "retirement", "401k", "ira" }
                },
                new TopicDefinition
                {
                    Key = "loan",
                    DisplayName = "Loan/Credit",
                    Keywords = new[] { "loan", "credit", "mortgage", "interest rate", "principal", "amortization" }
                }
            });

            template.Roles.AddRange(new[]
            {
                new RoleDefinition { Key = "advisor", DisplayName = "Financial Advisor", Keywords = new[] { "advisor", "banker", "relationship manager", "wealth manager" } },
                new RoleDefinition { Key = "compliance_officer", DisplayName = "Compliance Officer", Keywords = new[] { "compliance", "officer", "cco" } },
                new RoleDefinition { Key = "client", DisplayName = "Client", Keywords = new[] { "client", "customer", "account holder", "investor" } },
                new RoleDefinition { Key = "analyst", DisplayName = "Analyst", Keywords = new[] { "analyst", "research" } },
                new RoleDefinition { Key = "trader", DisplayName = "Trader", Keywords = new[] { "trader", "trading desk" } }
            });

            template.SeverityRules.AddRange(new[]
            {
                new SeverityRule
                {
                    Category = "RISK_INDICATOR",
                    Value = "*",
                    Condition = "topic == 'fraud'",
                    Severity = "critical"
                },
                new SeverityRule
                {
                    Category = "RISK_INDICATOR",
                    Value = "*",
                    Condition = "topic == 'compliance_reg'",
                    Severity = "critical"
                },
                new SeverityRule
                {
                    Category = "QUESTION_STATUS",
                    Value = "unanswered",
                    Condition = "topic == 'transaction'",
                    Severity = "high"
                }
            });

            return template;
        }

        private static TaxonomyData GetRetailTemplate()
        {
            var template = GetDefaultTemplate();

            template.Topics.AddRange(new[]
            {
                new TopicDefinition
                {
                    Key = "order_status",
                    DisplayName = "Order Status",
                    Keywords = new[] { "order", "tracking", "shipment", "delivery", "shipped", "delivered" }
                },
                new TopicDefinition
                {
                    Key = "return",
                    DisplayName = "Return/Exchange",
                    Keywords = new[] { "return", "exchange", "refund", "rma", "store credit" }
                },
                new TopicDefinition
                {
                    Key = "inventory",
                    DisplayName = "Inventory",
                    Keywords = new[] { "stock", "inventory", "available", "backorder", "out of stock", "restock" }
                },
                new TopicDefinition
                {
                    Key = "promotion",
                    DisplayName = "Promotion",
                    Keywords = new[] { "coupon", "discount", "promo", "sale", "deal", "code" }
                },
                new TopicDefinition
                {
                    Key = "loyalty",
                    DisplayName = "Loyalty Program",
                    Keywords = new[] { "loyalty", "points", "rewards", "member", "tier" }
                },
                new TopicDefinition
                {
                    Key = "product_inquiry",
                    DisplayName = "Product Inquiry",
                    Keywords = new[] { "product", "item", "size", "color", "specs", "dimensions" }
                }
            });

            template.Roles.AddRange(new[]
            {
                new RoleDefinition { Key = "customer", DisplayName = "Customer", Keywords = new[] { "customer", "shopper", "buyer" } },
                new RoleDefinition { Key = "sales_rep", DisplayName = "Sales Rep", Keywords = new[] { "sales", "rep", "associate" } },
                new RoleDefinition { Key = "support", DisplayName = "Customer Support", Keywords = new[] { "support", "service", "help desk" } },
                new RoleDefinition { Key = "store_manager", DisplayName = "Store Manager", Keywords = new[] { "manager", "store manager" } }
            });

            template.SeverityRules.Add(new SeverityRule
            {
                Category = "TENSION_SIGNAL",
                Value = "escalation_threatened",
                Condition = "topic == 'return'",
                Severity = "high"
            });

            return template;
        }

        private static TaxonomyData GetTechnologyTemplate()
        {
            var template = GetDefaultTemplate();

            template.Topics.AddRange(new[]
            {
                new TopicDefinition
                {
                    Key = "bug",
                    DisplayName = "Bug/Defect",
                    Keywords = new[] { "bug", "defect", "error", "crash", "broken", "issue", "regression" }
                },
                new TopicDefinition
                {
                    Key = "feature_request",
                    DisplayName = "Feature Request",
                    Keywords = new[] { "feature", "enhancement", "request", "wishlist", "improvement" }
                },
                new TopicDefinition
                {
                    Key = "outage",
                    DisplayName = "Outage/Incident",
                    Keywords = new[] { "outage", "down", "incident", "unavailable", "degraded", "p1", "sev1" }
                },
                new TopicDefinition
                {
                    Key = "security",
                    DisplayName = "Security",
                    Keywords = new[] { "security", "vulnerability", "breach", "cve", "patch", "exploit" }
                },
                new TopicDefinition
                {
                    Key = "integration",
                    DisplayName = "Integration",
                    Keywords = new[] { "api", "integration", "webhook", "connect", "sync", "endpoint" }
                },
                new TopicDefinition
                {
                    Key = "deployment",
                    DisplayName = "Deployment",
                    Keywords = new[] { "deploy", "release", "rollout", "update", "version", "ci/cd" }
                },
                new TopicDefinition
                {
                    Key = "performance",
                    DisplayName = "Performance",
                    Keywords = new[] { "performance", "slow", "latency", "timeout", "optimization" }
                }
            });

            template.Roles.AddRange(new[]
            {
                new RoleDefinition { Key = "developer", DisplayName = "Developer", Keywords = new[] { "dev", "developer", "engineer", "swe" } },
                new RoleDefinition { Key = "devops", DisplayName = "DevOps/SRE", Keywords = new[] { "devops", "sre", "ops", "infrastructure" } },
                new RoleDefinition { Key = "product", DisplayName = "Product", Keywords = new[] { "pm", "product manager", "product owner", "po" } },
                new RoleDefinition { Key = "qa", DisplayName = "QA", Keywords = new[] { "qa", "test", "tester", "quality" } },
                new RoleDefinition { Key = "support_tech", DisplayName = "Tech Support", Keywords = new[] { "support", "helpdesk", "tier 1", "tier 2" } }
            });

            template.SeverityRules.AddRange(new[]
            {
                new SeverityRule
                {
                    Category = "TENSION_SIGNAL",
                    Value = "*",
                    Condition = "topic == 'outage'",
                    Severity = "critical"
                },
                new SeverityRule
                {
                    Category = "RISK_INDICATOR",
                    Value = "*",
                    Condition = "topic == 'security'",
                    Severity = "critical"
                }
            });

            return template;
        }

        public static TaxonomyData GetDefaultTemplate()
        {
            return new TaxonomyData
            {
                Categories = new List<CategoryDefinition>
                {
                    new CategoryDefinition
                    {
                        Key = "QUESTION_STATUS",
                        DisplayName = "Question Status",
                        Description = "Tracks whether questions in the conversation were addressed",
                        Values = new List<ValueDefinition>
                        {
                            new ValueDefinition { Key = "unanswered", DisplayName = "Unanswered", Template = "{role} inquiry regarding {topic} was not addressed" },
                            new ValueDefinition { Key = "repeated_unanswered", DisplayName = "Repeatedly Unanswered", Template = "{role} asked about {topic} multiple times without response" },
                            new ValueDefinition { Key = "partially_answered", DisplayName = "Partially Answered", Template = "{role} inquiry about {topic} was partially addressed" },
                            new ValueDefinition { Key = "deflected", DisplayName = "Deflected", Template = "{role} question about {topic} was deflected" }
                        }
                    },
                    new CategoryDefinition
                    {
                        Key = "TENSION_SIGNAL",
                        DisplayName = "Tension Signal",
                        Description = "Identifies moments of conflict or frustration",
                        Values = new List<ValueDefinition>
                        {
                            new ValueDefinition { Key = "urgency_expressed", DisplayName = "Urgency Expressed", Template = "{role} expressed urgency regarding {topic}" },
                            new ValueDefinition { Key = "frustration_expressed", DisplayName = "Frustration Expressed", Template = "{role} expressed frustration regarding {topic}" },
                            new ValueDefinition { Key = "repetition_required", DisplayName = "Repetition Required", Template = "{role} had to repeat themselves regarding {topic}" },
                            new ValueDefinition { Key = "escalation_threatened", DisplayName = "Escalation Threatened", Template = "{role} threatened escalation regarding {topic}" },
                            new ValueDefinition { Key = "escalation_occurred", DisplayName = "Escalation Occurred", Template = "Escalation occurred regarding {topic}" }
                        }
                    },
                    new CategoryDefinition
                    {
                        Key = "COMMITMENT",
                        DisplayName = "Commitment",
                        Description = "Tracks promises and commitments made",
                        Values = new List<ValueDefinition>
                        {
                            new ValueDefinition { Key = "with_deadline", DisplayName = "With Deadline", Template = "{role} committed to {topic} with specific deadline" },
                            new ValueDefinition { Key = "vague_timeline", DisplayName = "Vague Timeline", Template = "{role} committed to {topic} without specific deadline" },
                            new ValueDefinition { Key = "no_timeline", DisplayName = "No Timeline", Template = "{role} committed to {topic} with no timeline" },
                            new ValueDefinition { Key = "missed", DisplayName = "Missed", Template = "{role} missed commitment regarding {topic}" }
                        }
                    },
                    new CategoryDefinition
                    {
                        Key = "RESPONSE_PATTERN",
                        DisplayName = "Response Pattern",
                        Description = "Characterizes response behaviors",
                        Values = new List<ValueDefinition>
                        {
                            new ValueDefinition { Key = "delayed", DisplayName = "Delayed Response", Template = "Delayed response regarding {topic}" },
                            new ValueDefinition { Key = "dismissive", DisplayName = "Dismissive", Template = "Dismissive response regarding {topic}" },
                            new ValueDefinition { Key = "low_responsiveness", DisplayName = "Low Responsiveness", Template = "Low overall responsiveness in conversation" },
                            new ValueDefinition { Key = "low_clarity", DisplayName = "Low Clarity", Template = "Low clarity in communication" }
                        }
                    },
                    new CategoryDefinition
                    {
                        Key = "RISK_INDICATOR",
                        DisplayName = "Risk Indicator",
                        Description = "Flags potential risks in the conversation",
                        Values = new List<ValueDefinition>
                        {
                            new ValueDefinition { Key = "legal_language", DisplayName = "Legal Language", Template = "Legal language detected regarding {topic}" },
                            new ValueDefinition { Key = "regulatory_mention", DisplayName = "Regulatory Mention", Template = "Regulatory mention regarding {topic}" },
                            new ValueDefinition { Key = "financial_dispute", DisplayName = "Financial Dispute", Template = "Financial dispute regarding {topic}" },
                            new ValueDefinition { Key = "service_failure", DisplayName = "Service Failure", Template = "Service failure regarding {topic}" }
                        }
                    },
                    new CategoryDefinition
                    {
                        Key = "DECISION",
                        DisplayName = "Decision",
                        Description = "Tracks decisions made in the conversation",
                        Values = new List<ValueDefinition>
                        {
                            new ValueDefinition { Key = "made", DisplayName = "Decision Made", Template = "Decision made regarding {topic}" },
                            new ValueDefinition { Key = "pending", DisplayName = "Decision Pending", Template = "Decision pending regarding {topic}" },
                            new ValueDefinition { Key = "reversed", DisplayName = "Decision Reversed", Template = "Decision reversed regarding {topic}" }
                        }
                    },
                    new CategoryDefinition
                    {
                        Key = "ACTION_ITEM",
                        DisplayName = "Action Item",
                        Description = "Tracks tasks and follow-ups",
                        Values = new List<ValueDefinition>
                        {
                            new ValueDefinition { Key = "assigned", DisplayName = "Assigned", Template = "Action assigned to {role} regarding {topic}" },
                            new ValueDefinition { Key = "completed", DisplayName = "Completed", Template = "Action completed by {role} regarding {topic}" },
                            new ValueDefinition { Key = "overdue", DisplayName = "Overdue", Template = "Action overdue for {role} regarding {topic}" }
                        }
                    },
                    new CategoryDefinition
                    {
                        Key = "MISALIGNMENT",
                        DisplayName = "Misalignment",
                        Description = "Identifies misunderstandings or conflicting expectations",
                        Values = new List<ValueDefinition>
                        {
                            new ValueDefinition { Key = "detected", DisplayName = "Misalignment Detected", Template = "Misalignment detected regarding {topic}" },
                            new ValueDefinition { Key = "low_alignment_score", DisplayName = "Low Alignment Score", Template = "Low alignment score in conversation" }
                        }
                    }
                },
                Topics = new List<TopicDefinition>
                {
                    new TopicDefinition { Key = "warranty", DisplayName = "Warranty", Keywords = new[] { "warranty", "guarantee", "coverage", "repair", "replacement" } },
                    new TopicDefinition { Key = "pricing", DisplayName = "Pricing", Keywords = new[] { "price", "cost", "fee", "charge", "rate", "discount", "quote" } },
                    new TopicDefinition { Key = "delivery", DisplayName = "Delivery", Keywords = new[] { "delivery", "shipping", "ship", "arrive", "tracking", "shipment" } },
                    new TopicDefinition { Key = "timeline", DisplayName = "Timeline", Keywords = new[] { "when", "deadline", "date", "schedule", "timeline", "by friday", "asap", "eta" } },
                    new TopicDefinition { Key = "billing", DisplayName = "Billing", Keywords = new[] { "invoice", "bill", "payment", "refund", "credit", "charge" } },
                    new TopicDefinition { Key = "technical_issue", DisplayName = "Technical Issue", Keywords = new[] { "error", "bug", "broken", "not working", "issue", "problem", "crash" } },
                    new TopicDefinition { Key = "contract", DisplayName = "Contract", Keywords = new[] { "contract", "agreement", "terms", "renewal", "cancellation" } },
                    new TopicDefinition { Key = "service", DisplayName = "Service", Keywords = new[] { "service", "support", "help", "assistance" } },
                    new TopicDefinition { Key = "product", DisplayName = "Product", Keywords = new[] { "product", "item", "order", "purchase" } },
                    new TopicDefinition { Key = "policy", DisplayName = "Policy", Keywords = new[] { "policy", "rule", "procedure", "compliance" } },
                    new TopicDefinition { Key = "general", DisplayName = "General", Keywords = Array.Empty<string>() }
                },
                Roles = new List<RoleDefinition>
                {
                    new RoleDefinition { Key = "customer", DisplayName = "Customer", Keywords = new[] { "customer", "client", "buyer", "user" } },
                    new RoleDefinition { Key = "representative", DisplayName = "Representative", Keywords = new[] { "rep", "agent", "support", "csr", "service" } },
                    new RoleDefinition { Key = "manager", DisplayName = "Manager", Keywords = new[] { "manager", "supervisor", "lead", "director" } },
                    new RoleDefinition { Key = "vendor", DisplayName = "Vendor", Keywords = new[] { "vendor", "supplier", "partner" } },
                    new RoleDefinition { Key = "internal_team_member", DisplayName = "Internal Team Member", Keywords = new[] { "team", "colleague" } },
                    new RoleDefinition { Key = "unknown", DisplayName = "Unknown", Keywords = Array.Empty<string>() }
                },
                SeverityRules = new List<SeverityRule>()
            };
        }
    }
}
