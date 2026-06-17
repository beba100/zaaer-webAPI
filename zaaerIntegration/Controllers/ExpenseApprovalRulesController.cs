using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Data;
using zaaerIntegration.Utilities;
using System.Security.Claims;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Controller for managing Expense Approval Rules
    /// قواعد الموافقة على المصروفات
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ExpenseApprovalRulesController : ControllerBase
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ILogger<ExpenseApprovalRulesController> _logger;

        /// <summary>
        /// Constructor for ExpenseApprovalRulesController
        /// </summary>
        /// <param name="masterDbContext">Master database context</param>
        /// <param name="logger">Logger instance</param>
        public ExpenseApprovalRulesController(
            MasterDbContext masterDbContext,
            ILogger<ExpenseApprovalRulesController> logger)
        {
            _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get all expense approval rules
        /// الحصول على جميع قواعد الموافقة على المصروفات
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<ExpenseApprovalRule>), 200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetAllRules()
        {
            try
            {
                _logger.LogInformation("Fetching all expense approval rules");
                
                var rules = await _masterDbContext.ExpenseApprovalRules
                    .AsNoTracking()
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.RuleId)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} expense approval rules", rules.Count);
                
                return Ok(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expense approval rules");
                return StatusCode(500, new { error = "حدث خطأ أثناء جلب قواعد الموافقة على المصروفات", message = ex.Message });
            }
        }

        /// <summary>
        /// Get expense approval rule by ID
        /// الحصول على قاعدة موافقة بالمعرف
        /// </summary>
        /// <param name="id">Rule ID</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ExpenseApprovalRule), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetRuleById(int id)
        {
            try
            {
                _logger.LogInformation("Fetching expense approval rule with ID: {RuleId}", id);
                
                var rule = await _masterDbContext.ExpenseApprovalRules
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RuleId == id);

                if (rule == null)
                {
                    _logger.LogWarning("Expense approval rule with ID {RuleId} not found", id);
                    return NotFound(new { error = $"لم يتم العثور على قاعدة الموافقة بالمعرف {id}" });
                }

                return Ok(rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expense approval rule with ID: {RuleId}", id);
                return StatusCode(500, new { error = "حدث خطأ أثناء جلب قاعدة الموافقة", message = ex.Message });
            }
        }

        /// <summary>
        /// Create a new expense approval rule
        /// إنشاء قاعدة موافقة جديدة
        /// </summary>
        /// <param name="rule">Expense approval rule data</param>
        [HttpPost]
        [ProducesResponseType(typeof(ExpenseApprovalRule), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> CreateRule([FromBody] ExpenseApprovalRule rule)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for creating expense approval rule");
                    return BadRequest(new { error = "بيانات غير صحيحة", errors = ModelState });
                }

                _logger.LogInformation("Creating new expense approval rule: Role={RoleCode}, FromStatus={FromStatus}, NextStatus={NextStatus}",
                    rule.RoleCode, rule.FromStatus, rule.NextStatus);

                // Set created timestamp
                rule.CreatedAt = KsaTime.Now;
                
                // Get current user ID if available
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    rule.CreatedBy = userId;
                }

                // Ensure Priority has a default value
                if (rule.Priority <= 0)
                {
                    rule.Priority = 100;
                }

                // Add rule to database
                _masterDbContext.ExpenseApprovalRules.Add(rule);
                await _masterDbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully created expense approval rule with ID: {RuleId}", rule.RuleId);

                return CreatedAtAction(nameof(GetRuleById), new { id = rule.RuleId }, rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expense approval rule");
                return StatusCode(500, new { error = "حدث خطأ أثناء إنشاء قاعدة الموافقة", message = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing expense approval rule
        /// تحديث قاعدة موافقة موجودة
        /// </summary>
        /// <param name="id">Rule ID</param>
        /// <param name="rule">Updated expense approval rule data</param>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ExpenseApprovalRule), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> UpdateRule(int id, [FromBody] ExpenseApprovalRule rule)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for updating expense approval rule");
                    return BadRequest(new { error = "بيانات غير صحيحة", errors = ModelState });
                }

                if (id != rule.RuleId)
                {
                    _logger.LogWarning("Rule ID mismatch: URL ID={UrlId}, Body ID={BodyId}", id, rule.RuleId);
                    return BadRequest(new { error = "معرف القاعدة في العنوان لا يتطابق مع معرف القاعدة في البيانات" });
                }

                _logger.LogInformation("Updating expense approval rule with ID: {RuleId}", id);

                // Check if rule exists
                var existingRule = await _masterDbContext.ExpenseApprovalRules
                    .FirstOrDefaultAsync(r => r.RuleId == id);

                if (existingRule == null)
                {
                    _logger.LogWarning("Expense approval rule with ID {RuleId} not found for update", id);
                    return NotFound(new { error = $"لم يتم العثور على قاعدة الموافقة بالمعرف {id}" });
                }

                // Update properties
                existingRule.RoleCode = rule.RoleCode;
                existingRule.FromStatus = rule.FromStatus;
                existingRule.NextStatus = rule.NextStatus;
                existingRule.MinAmount = rule.MinAmount;
                existingRule.MaxAmount = rule.MaxAmount;
                existingRule.AmountComparisonOperator = rule.AmountComparisonOperator;
                existingRule.ExpenseCategoryId = rule.ExpenseCategoryId;
                existingRule.ExpenseCategoryCondition = rule.ExpenseCategoryCondition;
                existingRule.Priority = rule.Priority > 0 ? rule.Priority : existingRule.Priority;
                existingRule.IsActive = rule.IsActive;
                existingRule.Description = rule.Description;
                existingRule.UpdatedAt = KsaTime.Now;

                // Get current user ID if available
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    existingRule.UpdatedBy = userId;
                }

                // Preserve CreatedAt and CreatedBy
                // (Don't update these fields)

                // Save changes
                await _masterDbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully updated expense approval rule with ID: {RuleId}", id);

                return Ok(existingRule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expense approval rule with ID: {RuleId}", id);
                return StatusCode(500, new { error = "حدث خطأ أثناء تحديث قاعدة الموافقة", message = ex.Message });
            }
        }

        /// <summary>
        /// Delete an expense approval rule
        /// حذف قاعدة موافقة
        /// </summary>
        /// <param name="id">Rule ID</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> DeleteRule(int id)
        {
            try
            {
                _logger.LogInformation("Deleting expense approval rule with ID: {RuleId}", id);

                var rule = await _masterDbContext.ExpenseApprovalRules
                    .FirstOrDefaultAsync(r => r.RuleId == id);

                if (rule == null)
                {
                    _logger.LogWarning("Expense approval rule with ID {RuleId} not found for deletion", id);
                    return NotFound(new { error = $"لم يتم العثور على قاعدة الموافقة بالمعرف {id}" });
                }

                _masterDbContext.ExpenseApprovalRules.Remove(rule);
                await _masterDbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted expense approval rule with ID: {RuleId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expense approval rule with ID: {RuleId}", id);
                return StatusCode(500, new { error = "حدث خطأ أثناء حذف قاعدة الموافقة", message = ex.Message });
            }
        }
    }
}

