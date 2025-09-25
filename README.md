# Winterflood Rule Engine for .NET

## Overview

The **Winterflood Rule Engine** provides a framework for defining and executing business rules within a structured, reusable, and modular ruleset. The engine enables dynamic rule evaluation on input data, with flexible execution strategies.

### Features

- Define rules and rulesets with nested structures.
- Support for different execution modes (`All`, `StopOnFirstSuccess`, `StopOnFirstFailure`).
- Logging and rule evaluation tracking.
- Lazy rule evaluation for optimized performance.
- Fluent rule composition and extension support.

## Installation

Ensure your project has the necessary dependencies:

```c#
# Install the required NuGet packages
dotnet add package Winterflood.RuleEngine
```

## Usage

Defining a Rule
Rules are defined using the Rule<TData> class with various constructor options.

### 1. Basic Rule with Default Success

```csharp
var rule = 
    new Rule<PersonData>(
        // Rule Name
        "BasicRule",
        // On Success
        (data, context) => "Success!"
    );
```

### 2. Rule with Condition

```csharp
var ruleWithCondition = 
    new Rule<PersonData>(
        // Rule Name
        "AgeCheckRule",
        // Condition
        (data, context) => data.Age >= 18,
        // On Success
        (data, context) => "Valid Age"
    );
```

### 3. Rule with Success and Failure Actions

```csharp
var ruleWithFailure = 
    new Rule<PersonData>(
        // Rule Name
        "AgeValidationRule",
        // Condition
        (data, context) => data.Age >= 18,
        // On Success
        (data, context) => "Age Valid",
        // On Failure
        (data, context) => "Age must be 18 or older"
    );
```

### 4. Reusable Rule best practice
```csharp    
public class AgeValidationRule() : Rule<PersonData>(
    // Rule Name
    PersonRuleConstants.AgeValidationRule,
    // Condition
    (data, context) => data.Age >= 18,
    // On Success
    (data, context) => "Age Valid",
    // On Failure
    (data, context) => "Age must be 18 or older"
);
```

## Creating a RuleSet
A RuleSet<TData> groups multiple rules together for execution.

### 1. Basic Ruleset
```csharp
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var ruleSet = 
    new RuleSet<PersonData>(
        // RuleSet Name
        "PersonValidation", 
        RuleExecutionMode.All, 
        loggerFactory);

ruleSet.AddRule(() => ruleWithFailure);
```

### 2. Reusable RuleSet 
```csharp
public static class PersonValidationRuleSet
{
    public static IRuleSet<PersonData> CreateRuleSet(ILoggerFactory loggerFactory)
    {
        var ruleset =
            new RuleSet<PersonData>(
                nameof(PersonValidationRuleSet),
                RuleExecutionMode.All,
                loggerFactory);

        ruleset.AddRule(() => 
            new Rule<PersonData>(
                // Rule Name
                PersonRuleConstants.AgeValidationRule,
                // Condition
                (data, context) => data.Age >= 18,
                // On Success
                (data, context) => "Age Valid",
                // On Failure
                (data, context) => "Age must be 18 or older"
        ));
        
        // OR the following best practice
        
        ruleset.AddRule(() => new AgeValidationRule());

        return ruleset;
    }
}
```

## Evaluating a RuleSet

```csharp
var context = new RulesetContext();
var personData = 
    new PersonData 
    {
        Name = "Thanos", 
        Age = 999
    };

bool result = ruleSet.Evaluate(personData, context);

Console.WriteLine($"RuleSet Evaluation Result: {result}");
```

## Execution Modes
### 1. All Execution Mode

All rules are evaluated regardless of success or failure.

```csharp
var allRuleSet = new RuleSet<PersonData, PersonOutput>("AllExecutionModeRuleset", RuleExecutionMode.All, loggerFactory);
allRuleSet.AddRule(() => ruleWithFailure);
```

### 2. Short Circuit Execution Mode

Stops execution when a rule fails.

```csharp
var shortCircuitRuleSet = new RuleSet<PersonData>("ShortCircuitRules", RuleExecutionMode.StopOnFirstFailure, loggerFactory);
shortCircuitRuleSet.AddRule(() => ruleWithFailure);
```

## Nested Rulesets
A RuleSet can invoke another RuleSet as a rule.

```csharp
var addressRuleset = 
    new RuleSet<AddressData>(
        "AddressValidation", 
        RuleExecutionMode.All, 
        loggerFactory);

addressRuleset.AddRule(() => new CityValidationRule());
addressRuleset.AddRule(() => new CountryValidationRule());

ruleSet.Add(
    addressRuleset
        .AsRule()
        .Bind<PersonData, AddressData>(
            parent => new AddressData { City = "NewYork", Country = "USA" },
            (parent, child) => 
            {
                parent.AddressCityMessage = child.CityMessage; 
                Parent.AddressCountryMessage = child.CountryMessage;
            }
        ));

```