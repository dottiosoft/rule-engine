use std::collections::HashMap;
use std::sync::Arc;

use crate::error::{EvalError, Result};
use crate::ast::Expr;
use crate::value::Value;
use crate::eval::{Evaluator, Scope};

pub type Function = Arc<dyn Fn(&[Value]) -> Result<Value> + Send + Sync>;

pub struct FunctionRegistry {
    pub(crate) funcs: HashMap<String, Function>,
}

impl FunctionRegistry {
    pub fn new() -> Self { Self { funcs: HashMap::new() } }

    pub fn register<F>(&mut self, name: &str, f: F)
    where
        F: Fn(&[Value]) -> Result<Value> + Send + Sync + 'static,
    {
        self.funcs.insert(name.to_string(), Arc::new(f));
    }

    pub fn get(&self, name: &str) -> Option<&Function> { self.funcs.get(name) }
}

impl Default for FunctionRegistry {
    fn default() -> Self {
        let mut reg = FunctionRegistry::new();

        reg.register("len", |args| match args {
            [Value::String(s)] => Ok(Value::Int(s.chars().count() as i64)),
            [Value::List(v)] => Ok(Value::Int(v.len() as i64)),
            [Value::Struct(m)] => Ok(Value::Int(m.len() as i64)),
            _ => Err(EvalError::new("len expects one string, list, or struct").into()),
        });

        reg.register("lower", |args| match args {
            [Value::String(s)] => Ok(Value::String(s.to_lowercase())),
            _ => Err(EvalError::new("lower expects one string").into()),
        });

        reg.register("upper", |args| match args {
            [Value::String(s)] => Ok(Value::String(s.to_uppercase())),
            _ => Err(EvalError::new("upper expects one string").into()),
        });

        reg.register("abs", |args| match args {
            [Value::Int(n)] => Ok(Value::Int(n.abs())),
            [Value::Float(n)] => Ok(Value::Float(n.abs())),
            _ => Err(EvalError::new("abs expects one number").into()),
        });

        reg.register("min", |args| match args {
            [a, b] => match (a, b) {
                (Value::Int(x), Value::Int(y)) => Ok(Value::Int((*x).min(*y))),
                (Value::Float(x), Value::Float(y)) => Ok(Value::Float((*x).min(*y))),
                (Value::Int(x), Value::Float(y)) => Ok(Value::Float((*x as f64).min(*y))),
                (Value::Float(x), Value::Int(y)) => Ok(Value::Float((*x).min(*y as f64))),
                _ => Err(EvalError::new("min expects numbers").into()),
            },
            _ => Err(EvalError::new("min expects two arguments").into()),
        });

        reg.register("max", |args| match args {
            [a, b] => match (a, b) {
                (Value::Int(x), Value::Int(y)) => Ok(Value::Int((*x).max(*y))),
                (Value::Float(x), Value::Float(y)) => Ok(Value::Float((*x).max(*y))),
                (Value::Int(x), Value::Float(y)) => Ok(Value::Float((*x as f64).max(*y))),
                (Value::Float(x), Value::Int(y)) => Ok(Value::Float((*x).max(*y as f64))),
                _ => Err(EvalError::new("max expects numbers").into()),
            },
            _ => Err(EvalError::new("max expects two arguments").into()),
        });

        reg
    }
}
