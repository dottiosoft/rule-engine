use std::collections::BTreeMap;

use crate::ast::Expr;
use crate::error::{EvalError, Result};
use crate::functions::FunctionRegistry;
use crate::operators::OperatorRegistry;
use crate::value::Value;

#[derive(Clone, Default)]
pub struct Scope {
    pub vars: BTreeMap<String, Value>,
}

impl Scope {
    pub fn with_var(mut self, name: impl Into<String>, value: Value) -> Self {
        self.vars.insert(name.into(), value);
        self
    }

    pub fn get(&self, name: &str) -> Option<&Value> { self.vars.get(name) }
}

pub struct Evaluator<'a> {
    pub funcs: &'a FunctionRegistry,
    pub ops: &'a OperatorRegistry,
}

impl<'a> Evaluator<'a> {
    pub fn new(funcs: &'a FunctionRegistry, ops: &'a OperatorRegistry) -> Self { Self { funcs, ops } }

    pub fn eval(&self, expr: &Expr, scope: &Scope) -> Result<Value> {
        match expr {
            Expr::Literal(v) => Ok(v.clone()),
            Expr::Ident(name) => scope.get(name).cloned().ok_or_else(|| EvalError::new(format!("unknown identifier '{}'" , name)).into()),
            Expr::Unary { op, expr } => {
                let v = self.eval(expr, scope)?;
                let f = match op {
                    crate::ast::UnaryOp::Not => self.ops.get_unary("!").cloned(),
                    crate::ast::UnaryOp::Neg => self.ops.get_unary("-u").cloned(),
                    crate::ast::UnaryOp::Pos => self.ops.get_unary("+u").cloned(),
                    crate::ast::UnaryOp::WordNot => self.ops.get_unary("not").cloned(),
                }.ok_or_else(|| EvalError::new("unknown unary operator"))?;
                f(&v)
            }
            Expr::Binary { left, op, right } => {
                let l = self.eval(left, scope)?;
                // short-circuit for boolean
                if op == "||" {
                    if let Value::Bool(b) = l { if b { return Ok(Value::Bool(true)); } }
                }
                if op == "&&" {
                    if let Value::Bool(b) = l { if !b { return Ok(Value::Bool(false)); } }
                }
                let r = self.eval(right, scope)?;
                let spec = self.ops.get_binary(op).ok_or_else(|| EvalError::new(format!("unknown operator '{}'" , op)))?;
                (spec.func)(&l, &r)
            }
            Expr::Call { name, args } => {
                let f = self.funcs.get(name).cloned().ok_or_else(|| EvalError::new(format!("unknown function '{}'" , name)))?;
                let mut evaled = Vec::with_capacity(args.len());
                for a in args { evaled.push(self.eval(a, scope)?); }
                f(&evaled)
            }
            Expr::Member { target, member } => {
                let t = self.eval(target, scope)?;
                match t {
                    Value::Struct(map) => map.get(member).cloned().ok_or_else(|| EvalError::new(format!("unknown field '{}'" , member)).into()),
                    _ => Err(EvalError::new("member access expects a struct").into()),
                }
            }
            Expr::Index { target, index } => {
                let t = self.eval(target, scope)?;
                let i = self.eval(index, scope)?;
                match (t, i) {
                    (Value::List(v), Value::Int(idx)) => v.get(idx as usize).cloned().ok_or_else(|| EvalError::new("index out of range").into()),
                    (Value::Struct(m), Value::String(k)) => m.get(&k).cloned().ok_or_else(|| EvalError::new("missing key").into()),
                    _ => Err(EvalError::new("indexing mismatch").into()),
                }
            }
            Expr::StructLiteral(fields) => {
                let mut map = BTreeMap::new();
                for (k, v) in fields { map.insert(k.clone(), self.eval(v, scope)?); }
                Ok(Value::Struct(map))
            }
            Expr::ListLiteral(items) => {
                let mut out = Vec::with_capacity(items.len());
                for it in items { out.push(self.eval(it, scope)?); }
                Ok(Value::List(out))
            }
            Expr::EnumVariant { name, payload } => {
                let p = match payload { Some(b) => Some(Box::new(self.eval(b, scope)?)), None => None };
                Ok(Value::Enum { name: name.clone(), payload: p })
            }
        }
    }
}
