use crate::error::{EngineError, Result};
use crate::eval::{Evaluator, Scope};
use crate::functions::FunctionRegistry;
use crate::operators::OperatorRegistry;
use crate::parser::Parser;
use crate::value::Value;

pub struct Context {
    pub scope: Scope,
}

impl Context {
    pub fn new() -> Self { Self { scope: Scope::default() } }
    pub fn with_var(mut self, name: impl Into<String>, value: Value) -> Self { self.scope = self.scope.with_var(name, value); self }
}

pub struct Engine {
    pub funcs: FunctionRegistry,
    pub ops: OperatorRegistry,
}

impl Engine {
    pub fn new() -> Self { Self { funcs: FunctionRegistry::default(), ops: OperatorRegistry::default() } }

    pub fn with_function(mut self, name: &str, f: impl Fn(&[Value]) -> Result<Value> + Send + Sync + 'static) -> Self {
        self.funcs.register(name, f);
        self
    }

    pub fn with_operator(mut self, name: &str, precedence: u8, assoc: crate::operators::Assoc, f: impl Fn(&Value, &Value) -> Result<Value> + Send + Sync + 'static) -> Self {
        self.ops.register_binary(name, precedence, assoc, f);
        self
    }

    pub fn parse(&self, input: &str) -> Result<crate::ast::Expr> {
        let mut p = Parser::new(input, &self.ops)?;
        p.parse_expression().map_err(EngineError::from)
    }

    pub fn eval(&self, expr: &crate::ast::Expr, ctx: &Context) -> Result<Value> {
        let ev = Evaluator::new(&self.funcs, &self.ops);
        ev.eval(expr, &ctx.scope).map_err(EngineError::from)
    }

    pub fn parse_and_eval(&self, input: &str, ctx: &Context) -> Result<Value> {
        let expr = self.parse(input)?;
        self.eval(&expr, ctx)
    }
}
