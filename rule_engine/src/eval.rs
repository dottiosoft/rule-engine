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
            Expr::Lambda { .. } => Err(EvalError::new("lambdas must be passed to higher-order functions").into()),
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
                // Built-ins with lambda support and collection operations
                match (name.as_str(), args.as_slice()) {
                    ("filter", [list_expr, Expr::Lambda { param, body }]) => {
                        let list_val = self.eval(list_expr, scope)?;
                        let list = match list_val { Value::List(v) => v, _ => return Err(EvalError::new("filter expects list as first argument").into()) };
                        let mut out = Vec::new();
                        for item in list.into_iter() {
                            let mut inner = scope.clone();
                            inner.vars.insert(param.clone(), item.clone());
                            let cond = self.eval(body, &inner)?;
                            match cond {
                                Value::Bool(true) => out.push(item),
                                Value::Bool(false) => {}
                                _ => return Err(EvalError::new("filter lambda must return bool").into()),
                            }
                        }
                        return Ok(Value::List(out));
                    }
                    ("map", [list_expr, Expr::Lambda { param, body }]) => {
                        let list_val = self.eval(list_expr, scope)?;
                        let list = match list_val { Value::List(v) => v, _ => return Err(EvalError::new("map expects list as first argument").into()) };
                        let mut out = Vec::with_capacity(list.len());
                        for item in list.into_iter() {
                            let mut inner = scope.clone();
                            inner.vars.insert(param.clone(), item);
                            out.push(self.eval(body, &inner)?);
                        }
                        return Ok(Value::List(out));
                    }
                    ("sum_by", [list_expr, Expr::Lambda { param, body }]) => {
                        let list_val = self.eval(list_expr, scope)?;
                        let list = match list_val { Value::List(v) => v, _ => return Err(EvalError::new("sum_by expects list as first argument").into()) };
                        let mut total: f64 = 0.0;
                        for item in list.into_iter() {
                            let mut inner = scope.clone();
                            inner.vars.insert(param.clone(), item);
                            let v = self.eval(body, &inner)?;
                            match v {
                                Value::Int(n) => total += n as f64,
                                Value::Float(n) => total += n,
                                _ => return Err(EvalError::new("sum_by lambda must return number").into()),
                            }
                        }
                        return Ok(Value::Float(total));
                    }
                    ("sum", [list_expr]) => {
                        let list_val = self.eval(list_expr, scope)?;
                        let list = match list_val { Value::List(v) => v, _ => return Err(EvalError::new("sum expects a list").into()) };
                        let mut total: f64 = 0.0;
                        for v in list.iter() {
                            match v {
                                Value::Int(n) => total += *n as f64,
                                Value::Float(n) => total += *n,
                                _ => return Err(EvalError::new("sum expects numeric list").into()),
                            }
                        }
                        return Ok(Value::Float(total));
                    }
                    ("count", [list_expr]) => {
                        let list_val = self.eval(list_expr, scope)?;
                        let list = match list_val { Value::List(v) => v, _ => return Err(EvalError::new("count expects a list").into()) };
                        return Ok(Value::Int(list.len() as i64));
                    }
                    ("count", [list_expr, Expr::Lambda { param, body }]) => {
                        let list_val = self.eval(list_expr, scope)?;
                        let list = match list_val { Value::List(v) => v, _ => return Err(EvalError::new("count(list, lambda) expects a list").into()) };
                        let mut c: i64 = 0;
                        for item in list.into_iter() {
                            let mut inner = scope.clone();
                            inner.vars.insert(param.clone(), item);
                            let cond = self.eval(body, &inner)?;
                            match cond { Value::Bool(true) => c += 1, Value::Bool(false) => {}, _ => return Err(EvalError::new("count lambda must return bool").into()) }
                        }
                        return Ok(Value::Int(c));
                    }
                    ("any", [list_expr, Expr::Lambda { param, body }]) => {
                        let list_val = self.eval(list_expr, scope)?;
                        let list = match list_val { Value::List(v) => v, _ => return Err(EvalError::new("any expects list").into()) };
                        for item in list.into_iter() {
                            let mut inner = scope.clone();
                            inner.vars.insert(param.clone(), item);
                            let cond = self.eval(body, &inner)?;
                            match cond { Value::Bool(true) => return Ok(Value::Bool(true)), Value::Bool(false) => {}, _ => return Err(EvalError::new("any lambda must return bool").into()) }
                        }
                        return Ok(Value::Bool(false));
                    }
                    ("all", [list_expr, Expr::Lambda { param, body }]) => {
                        let list_val = self.eval(list_expr, scope)?;
                        let list = match list_val { Value::List(v) => v, _ => return Err(EvalError::new("all expects list").into()) };
                        for item in list.into_iter() {
                            let mut inner = scope.clone();
                            inner.vars.insert(param.clone(), item);
                            let cond = self.eval(body, &inner)?;
                            match cond { Value::Bool(true) => {}, Value::Bool(false) => return Ok(Value::Bool(false)), _ => return Err(EvalError::new("all lambda must return bool").into()) }
                        }
                        return Ok(Value::Bool(true));
                    }
                    _ => {}
                }
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
