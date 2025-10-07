use std::collections::BTreeMap;

use expression_engine::{Engine as ExpressionEngine, Context as ExpressionContext, Value, Result};

#[derive(Clone, Debug)]
pub struct RuleContext {
    pub data: BTreeMap<String, Value>,
}

impl RuleContext {
    pub fn new() -> Self { Self { data: BTreeMap::new() } }
    pub fn with(mut self, key: impl Into<String>, value: Value) -> Self { self.data.insert(key.into(), value); self }
}

#[derive(Clone, Debug)]
pub enum RuleOutcome {
    Continue,
    Branch(String),
    Emit(Value),
}

#[derive(Clone, Debug)]
pub struct AuditEvent {
    pub step_name: String,
    pub expression: String,
    pub result: Value,
    pub outcome: RuleOutcome,
}

#[derive(Clone, Debug)]
pub struct AuditLog(pub Vec<AuditEvent>);

impl AuditLog {
    pub fn new() -> Self { Self(Vec::new()) }
    pub fn record(&mut self, event: AuditEvent) { self.0.push(event); }
}

#[derive(Clone)]
pub enum RuleAction {
    // Evaluate expression; when true continue, when false branch or stop
    When { expr: String, on_false: Option<String> },
    // Evaluate expression and store into context key
    Let { key: String, expr: String },
    // Evaluate expression and emit as outcome
    Emit { expr: String },
    // Invoke another chain by name
    Call { chain: String },
}

#[derive(Clone)]
pub struct RuleStep {
    pub name: String,
    pub action: RuleAction,
}

pub struct RuleChainBuilder {
    name: String,
    steps: Vec<RuleStep>,
}

impl RuleChainBuilder {
    pub fn new(name: impl Into<String>) -> Self { Self { name: name.into(), steps: Vec::new() } }
    pub fn when(mut self, expr: impl Into<String>) -> Self { self.steps.push(RuleStep { name: "when".into(), action: RuleAction::When { expr: expr.into(), on_false: None } }); self }
    pub fn when_else(mut self, expr: impl Into<String>, on_false_chain: impl Into<String>) -> Self { self.steps.push(RuleStep { name: "when".into(), action: RuleAction::When { expr: expr.into(), on_false: Some(on_false_chain.into()) } }); self }
    pub fn let_(mut self, key: impl Into<String>, expr: impl Into<String>) -> Self { self.steps.push(RuleStep { name: "let".into(), action: RuleAction::Let { key: key.into(), expr: expr.into() } }); self }
    pub fn emit(mut self, expr: impl Into<String>) -> Self { self.steps.push(RuleStep { name: "emit".into(), action: RuleAction::Emit { expr: expr.into() } }); self }
    pub fn call(mut self, chain: impl Into<String>) -> Self { self.steps.push(RuleStep { name: "call".into(), action: RuleAction::Call { chain: chain.into() } }); self }
    pub fn build(self) -> RuleChain { RuleChain { name: self.name, steps: self.steps } }
}

#[derive(Clone, Default)]
pub struct RuleChain {
    pub name: String,
    pub steps: Vec<RuleStep>,
}

pub struct RuleEngine {
    pub expr: ExpressionEngine,
    pub chains: BTreeMap<String, RuleChain>,
}

impl RuleEngine {
    pub fn new(expr: ExpressionEngine) -> Self { Self { expr, chains: BTreeMap::new() } }

    pub fn add_chain(&mut self, chain: RuleChain) { self.chains.insert(chain.name.clone(), chain); }

    pub fn with_chain(mut self, chain: RuleChain) -> Self { self.add_chain(chain); self }

    pub fn run(&self, chain_name: &str, mut rule_ctx: RuleContext) -> Result<(Option<Value>, AuditLog)> {
        let mut audit = AuditLog::new();
        let mut current_chain = chain_name.to_string();
        let mut pc: usize = 0;
        let mut last_out: Option<Value> = None;

        loop {
            let chain = match self.chains.get(&current_chain) { Some(c) => c, None => break };
            if pc >= chain.steps.len() { break; }
            let step = &chain.steps[pc];

            let mut expr_ctx = ExpressionContext::new();
            for (k, v) in rule_ctx.data.iter() { expr_ctx = expr_ctx.with_var(k, v.clone()); }

            match &step.action {
                RuleAction::When { expr, on_false } => {
                    let val = self.expr.parse_and_eval(expr, &expr_ctx)?;
                    let outcome = match val {
                        Value::Bool(true) => RuleOutcome::Continue,
                        Value::Bool(false) => {
                            if let Some(branch) = on_false.clone() {
                                RuleOutcome::Branch(branch)
                            } else { RuleOutcome::Continue }
                        }
                        _ => RuleOutcome::Continue,
                    };
                    audit.record(AuditEvent { step_name: step.name.clone(), expression: expr.clone(), result: val, outcome: outcome.clone() });
                    match outcome {
                        RuleOutcome::Continue => { pc += 1; }
                        RuleOutcome::Branch(target) => { current_chain = target; pc = 0; }
                        RuleOutcome::Emit(_) => unreachable!(),
                    }
                }
                RuleAction::Let { key, expr } => {
                    let val = self.expr.parse_and_eval(expr, &expr_ctx)?;
                    rule_ctx.data.insert(key.clone(), val.clone());
                    audit.record(AuditEvent { step_name: step.name.clone(), expression: expr.clone(), result: val, outcome: RuleOutcome::Continue });
                    pc += 1;
                }
                RuleAction::Emit { expr } => {
                    let val = self.expr.parse_and_eval(expr, &expr_ctx)?;
                    audit.record(AuditEvent { step_name: step.name.clone(), expression: expr.clone(), result: val.clone(), outcome: RuleOutcome::Emit(val.clone()) });
                    last_out = Some(val);
                    break;
                }
                RuleAction::Call { chain } => {
                    // branch to another chain; after completion, continue current chain
                    audit.record(AuditEvent { step_name: step.name.clone(), expression: format!("call {}", chain), result: Value::Null, outcome: RuleOutcome::Branch(chain.clone()) });
                    current_chain = chain.clone();
                    pc = 0;
                }
            }
        }

        Ok((last_out, audit))
    }
}
