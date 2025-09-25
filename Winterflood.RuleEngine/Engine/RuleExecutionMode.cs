namespace Winterflood.RuleEngine.Engine
{
    /// <summary>
    /// Defines how a ruleset executes its rules.
    /// </summary>
    public enum RuleExecutionMode
    {
        /// <summary>
        /// Executes all rules in the ruleset, regardless of their outcome.
        /// </summary>
        All,   
        
        /// <summary>
        /// Stops execution after the first successful rule.
        /// </summary>
        StopOnFirstSuccess,

        /// <summary>
        /// Stops execution after the first failed rule.
        /// </summary>
        StopOnFirstFailure
    }
}