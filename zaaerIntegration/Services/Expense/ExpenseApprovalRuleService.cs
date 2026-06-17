using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Data;

namespace zaaerIntegration.Services.Expense
{
    /// <summary>
    /// Service for managing expense approval rules
    /// خدمة لإدارة قواعد موافقة المصروفات
    /// </summary>
    public class ExpenseApprovalRuleService : IExpenseApprovalRuleService
    {
        private readonly MasterDbContext _context;
        private readonly ILogger<ExpenseApprovalRuleService> _logger;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY_PREFIX = "ExpenseApprovalRules_";
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(15); // Cache for 15 minutes

        // Minimum valid amount for expenses (1 SAR)
        private const decimal MINIMUM_AMOUNT = 1.00m;

        public ExpenseApprovalRuleService(
            MasterDbContext context,
            ILogger<ExpenseApprovalRuleService> logger,
            IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Get the applicable approval rule for a given role, status, amount, and category
        /// </summary>
        public async Task<ExpenseApprovalRule?> GetApplicableRuleAsync(string roleCode, string fromStatus, decimal amount, int? expenseCategoryId = null)
        {
            try
            {
                _logger.LogInformation("🔍 [GetApplicableRule] Searching rule: Role={Role}, Status={Status}, Amount={Amount}, Category={Category}",
                    roleCode, fromStatus, amount, expenseCategoryId);

                // Validate amount first
                if (!IsValidAmount(amount))
                {
                    _logger.LogWarning("⚠️ [GetApplicableRule] Invalid amount: {Amount} (< {Minimum})", amount, MINIMUM_AMOUNT);
                    return null;
                }

                // Normalize inputs
                var normalizedRoleCode = roleCode?.Trim().ToLowerInvariant() ?? string.Empty;
                var normalizedFromStatus = fromStatus?.Trim().ToLowerInvariant() ?? string.Empty;

                // Try to get from cache first
                var cacheKey = $"{CACHE_KEY_PREFIX}{normalizedRoleCode}_{normalizedFromStatus}";
                if (!_cache.TryGetValue(cacheKey, out List<ExpenseApprovalRule>? cachedRules))
                {
                    // Load rules from database
                    cachedRules = await _context.ExpenseApprovalRules
                        .AsNoTracking()
                        .Where(r => r.IsActive &&
                                    (r.RoleCode.ToLower() == normalizedRoleCode || r.RoleCode == "*") &&
                                    (r.FromStatus.ToLower() == normalizedFromStatus || r.FromStatus == "*"))
                        .OrderBy(r => r.Priority)
                        .ToListAsync();

                    // Cache for 15 minutes
                    _cache.Set(cacheKey, cachedRules, CACHE_DURATION);
                    _logger.LogDebug("📦 [GetApplicableRule] Cached {Count} rules for Role={Role}, Status={Status}",
                        cachedRules.Count, normalizedRoleCode, normalizedFromStatus);
                }

                if (cachedRules == null || !cachedRules.Any())
                {
                    _logger.LogWarning("⚠️ [GetApplicableRule] No rules found for Role={Role}, Status={Status}",
                        normalizedRoleCode, normalizedFromStatus);
                    return null;
                }

                // Evaluate rules in priority order (lower Priority number = higher priority)
                foreach (var rule in cachedRules.OrderBy(r => r.Priority))
                {
                    if (EvaluateRule(rule, amount, expenseCategoryId))
                    {
                        _logger.LogInformation("✅ [GetApplicableRule] Found applicable rule: RuleId={RuleId}, NextStatus={NextStatus}, Priority={Priority}",
                            rule.RuleId, rule.NextStatus, rule.Priority);
                        return rule;
                    }
                }

                _logger.LogWarning("⚠️ [GetApplicableRule] No applicable rule matched for Role={Role}, Status={Status}, Amount={Amount}, Category={Category}",
                    normalizedRoleCode, normalizedFromStatus, amount, expenseCategoryId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetApplicableRule] Error getting applicable rule: Role={Role}, Status={Status}, Amount={Amount}",
                    roleCode, fromStatus, amount);
                throw;
            }
        }

        /// <summary>
        /// Evaluate if a rule matches the given amount and category
        /// </summary>
        private bool EvaluateRule(ExpenseApprovalRule rule, decimal amount, int? expenseCategoryId)
        {
            // Check category condition first (if specified)
            if (rule.ExpenseCategoryId.HasValue)
            {
                var categoryMatches = rule.ExpenseCategoryCondition?.ToLowerInvariant() switch
                {
                    "equals" => expenseCategoryId == rule.ExpenseCategoryId.Value,
                    "not_equals" => expenseCategoryId != rule.ExpenseCategoryId.Value,
                    _ => expenseCategoryId == rule.ExpenseCategoryId.Value // Default to equals
                };

                if (!categoryMatches)
                {
                    _logger.LogDebug("🔍 [EvaluateRule] Category mismatch: Rule Category={RuleCategory}, Expense Category={ExpenseCategory}, Condition={Condition}",
                        rule.ExpenseCategoryId, expenseCategoryId, rule.ExpenseCategoryCondition);
                    return false;
                }
            }

            // Check amount condition
            var operatorStr = rule.AmountComparisonOperator?.Trim().ToLowerInvariant() ?? "";

            // Handle "between" operator
            if (operatorStr == "between" && rule.MinAmount.HasValue && rule.MaxAmount.HasValue)
            {
                var matches = amount >= rule.MinAmount.Value && amount <= rule.MaxAmount.Value;
                _logger.LogDebug("🔍 [EvaluateRule] Between check: Amount={Amount}, Min={Min}, Max={Max}, Matches={Matches}",
                    amount, rule.MinAmount.Value, rule.MaxAmount.Value, matches);
                return matches;
            }

            // Handle other operators
            bool amountMatches = operatorStr switch
            {
                "<=" => rule.MaxAmount.HasValue && amount <= rule.MaxAmount.Value,
                "<" => rule.MaxAmount.HasValue && amount < rule.MaxAmount.Value,
                ">=" => rule.MinAmount.HasValue && amount >= rule.MinAmount.Value,
                ">" => rule.MinAmount.HasValue && amount > rule.MinAmount.Value,
                "between" => false, // Already handled above
                _ => true // No operator or amount constraints = matches all amounts
            };

            if (!amountMatches)
            {
                _logger.LogDebug("🔍 [EvaluateRule] Amount mismatch: Amount={Amount}, Min={Min}, Max={Max}, Operator={Operator}",
                    amount, rule.MinAmount, rule.MaxAmount, operatorStr);
                return false;
            }

            // If no amount constraints, check if rule applies to all amounts
            if (!rule.MinAmount.HasValue && !rule.MaxAmount.HasValue && string.IsNullOrWhiteSpace(operatorStr))
            {
                _logger.LogDebug("✅ [EvaluateRule] Rule applies to all amounts: RuleId={RuleId}", rule.RuleId);
                return true;
            }

            return amountMatches;
        }

        /// <summary>
        /// Get the next status for an expense based on approval rules
        /// </summary>
        public async Task<string?> GetNextStatusAsync(string roleCode, string fromStatus, decimal amount, int? expenseCategoryId = null)
        {
            var rule = await GetApplicableRuleAsync(roleCode, fromStatus, amount, expenseCategoryId);
            return rule?.NextStatus;
        }

        /// <summary>
        /// Get all active rules for a specific role and status
        /// </summary>
        public async Task<IEnumerable<ExpenseApprovalRule>> GetRulesAsync(string roleCode, string fromStatus)
        {
            try
            {
                var normalizedRoleCode = roleCode?.Trim().ToLowerInvariant() ?? string.Empty;
                var normalizedFromStatus = fromStatus?.Trim().ToLowerInvariant() ?? string.Empty;

                var rules = await _context.ExpenseApprovalRules
                    .AsNoTracking()
                    .Where(r => r.IsActive &&
                                (r.RoleCode.ToLower() == normalizedRoleCode || r.RoleCode == "*") &&
                                (r.FromStatus.ToLower() == normalizedFromStatus || r.FromStatus == "*"))
                    .OrderBy(r => r.Priority)
                    .ToListAsync();

                return rules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [GetRules] Error getting rules: Role={Role}, Status={Status}",
                    roleCode, fromStatus);
                throw;
            }
        }

        /// <summary>
        /// Validate if an amount meets the minimum requirement (>= 1 SAR)
        /// </summary>
        public bool IsValidAmount(decimal amount)
        {
            return amount >= MINIMUM_AMOUNT;
        }
    }
}

