use std::collections::HashMap;
use std::sync::Arc;

use crate::error::{EvalError, Result};
use crate::value::Value;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Assoc { Left, Right }

pub struct BinaryOpSpec {
    pub precedence: u8,
    pub associativity: Assoc,
    pub func: Arc<dyn Fn(&Value, &Value) -> Result<Value> + Send + Sync>,
}

pub struct OperatorRegistry {
    pub(crate) unary: HashMap<String, Arc<dyn Fn(&Value) -> Result<Value> + Send + Sync>>,
    pub(crate) binary: HashMap<String, BinaryOpSpec>,
}

impl OperatorRegistry {
    pub fn new() -> Self { Self { unary: HashMap::new(), binary: HashMap::new() } }

    pub fn register_unary<F>(&mut self, name: &str, f: F)
    where
        F: Fn(&Value) -> Result<Value> + Send + Sync + 'static,
    {
        self.unary.insert(name.to_string(), Arc::new(f));
    }

    pub fn register_binary<F>(&mut self, name: &str, precedence: u8, associativity: Assoc, f: F)
    where
        F: Fn(&Value, &Value) -> Result<Value> + Send + Sync + 'static,
    {
        self.binary.insert(name.to_string(), BinaryOpSpec { precedence, associativity, func: Arc::new(f) });
    }

    pub fn get_unary(&self, name: &str) -> Option<&Arc<dyn Fn(&Value) -> Result<Value> + Send + Sync>> {
        self.unary.get(name)
    }

    pub fn get_binary(&self, name: &str) -> Option<&BinaryOpSpec> {
        self.binary.get(name)
    }
}

impl Default for OperatorRegistry {
    fn default() -> Self {
        let mut reg = OperatorRegistry::new();

        // Unary operators
        reg.register_unary("!", |v| match v {
            Value::Bool(b) => Ok(Value::Bool(!b)),
            _ => Err(EvalError::new(format!("operator '!' expects bool, got {}", v.type_name())).into()),
        });
        reg.register_unary("not", |v| match v {
            Value::Bool(b) => Ok(Value::Bool(!b)),
            _ => Err(EvalError::new(format!("operator 'not' expects bool, got {}", v.type_name())).into()),
        });
        reg.register_unary("-u", |v| match v {
            Value::Int(n) => Ok(Value::Int(-n)),
            Value::Float(n) => Ok(Value::Float(-n)),
            _ => Err(EvalError::new(format!("unary '-' expects number, got {}", v.type_name())).into()),
        });
        reg.register_unary("+u", |v| match v {
            Value::Int(n) => Ok(Value::Int(*n)),
            Value::Float(n) => Ok(Value::Float(*n)),
            _ => Err(EvalError::new(format!("unary '+' expects number, got {}", v.type_name())).into()),
        });

        // Binary operators: precedence roughly based on Rust
        // 1: ||
        reg.register_binary("||", 1, Assoc::Left, |a, b| match (a, b) {
            (Value::Bool(x), Value::Bool(y)) => Ok(Value::Bool(*x || *y)),
            _ => Err(EvalError::new("operator '||' expects bool operands").into()),
        });
        // 2: &&
        reg.register_binary("&&", 2, Assoc::Left, |a, b| match (a, b) {
            (Value::Bool(x), Value::Bool(y)) => Ok(Value::Bool(*x && *y)),
            _ => Err(EvalError::new("operator '&&' expects bool operands").into()),
        });
        reg.register_binary("or", 1, Assoc::Left, |a, b| match (a, b) {
            (Value::Bool(x), Value::Bool(y)) => Ok(Value::Bool(*x || *y)),
            _ => Err(EvalError::new("operator 'or' expects bool operands").into()),
        });
        reg.register_binary("and", 2, Assoc::Left, |a, b| match (a, b) {
            (Value::Bool(x), Value::Bool(y)) => Ok(Value::Bool(*x && *y)),
            _ => Err(EvalError::new("operator 'and' expects bool operands").into()),
        });

        // 3: equality
        reg.register_binary("==", 3, Assoc::Left, |a, b| Ok(Value::Bool(a == b)));
        reg.register_binary("!=", 3, Assoc::Left, |a, b| Ok(Value::Bool(a != b)));

        // 4: comparisons
        reg.register_binary("<", 4, Assoc::Left, cmp_lt);
        reg.register_binary("<=", 4, Assoc::Left, cmp_le);
        reg.register_binary(">", 4, Assoc::Left, cmp_gt);
        reg.register_binary(">=", 4, Assoc::Left, cmp_ge);

        // 5: addition/subtraction
        reg.register_binary("+", 5, Assoc::Left, add_values);
        reg.register_binary("-", 5, Assoc::Left, sub_values);

        // 6: multiplication/division/modulo
        reg.register_binary("*", 6, Assoc::Left, mul_values);
        reg.register_binary("/", 6, Assoc::Left, div_values);
        reg.register_binary("%", 6, Assoc::Left, rem_values);

        // 3: word operator example: contains
        reg.register_binary("contains", 3, Assoc::Left, |a, b| match (a, b) {
            (Value::String(s), Value::String(substr)) => Ok(Value::Bool(s.contains(substr))),
            (Value::List(xs), v) => Ok(Value::Bool(xs.contains(v))),
            _ => Err(EvalError::new("'contains' expects string or list on left").into()),
        });

        reg
    }
}

fn num_pair(a: &Value, b: &Value) -> Result<(f64, f64)> {
    match (a, b) {
        (Value::Int(x), Value::Int(y)) => Ok((*x as f64, *y as f64)),
        (Value::Int(x), Value::Float(y)) => Ok((*x as f64, *y)),
        (Value::Float(x), Value::Int(y)) => Ok((*x, *y as f64)),
        (Value::Float(x), Value::Float(y)) => Ok((*x, *y)),
        _ => Err(EvalError::new("operator expects numbers").into()),
    }
}

fn add_values(a: &Value, b: &Value) -> Result<Value> {
    match (a, b) {
        (Value::Int(x), Value::Int(y)) => Ok(Value::Int(x + y)),
        (Value::Float(x), Value::Float(y)) => Ok(Value::Float(x + y)),
        (Value::Int(x), Value::Float(y)) => Ok(Value::Float(*x as f64 + *y)),
        (Value::Float(x), Value::Int(y)) => Ok(Value::Float(*x + *y as f64)),
        (Value::String(x), Value::String(y)) => Ok(Value::String(format!("{}{}", x, y))),
        _ => Err(EvalError::new("'+' expects numbers or strings").into()),
    }
}

fn sub_values(a: &Value, b: &Value) -> Result<Value> {
    let (x, y) = num_pair(a, b)?;
    Ok(Value::Float(x - y))
}

fn mul_values(a: &Value, b: &Value) -> Result<Value> {
    let (x, y) = num_pair(a, b)?;
    Ok(Value::Float(x * y))
}

fn div_values(a: &Value, b: &Value) -> Result<Value> {
    let (x, y) = num_pair(a, b)?;
    if y == 0.0 { return Err(EvalError::new("division by zero").into()); }
    Ok(Value::Float(x / y))
}

fn rem_values(a: &Value, b: &Value) -> Result<Value> {
    match (a, b) {
        (Value::Int(x), Value::Int(y)) if *y != 0 => Ok(Value::Int(x % y)),
        _ => Err(EvalError::new("'%' expects non-zero integer rhs").into()),
    }
}

fn cmp_lt(a: &Value, b: &Value) -> Result<Value> { cmp(a, b, |o| o.is_lt()) }
fn cmp_le(a: &Value, b: &Value) -> Result<Value> { cmp(a, b, |o| o.is_le()) }
fn cmp_gt(a: &Value, b: &Value) -> Result<Value> { cmp(a, b, |o| o.is_gt()) }
fn cmp_ge(a: &Value, b: &Value) -> Result<Value> { cmp(a, b, |o| o.is_ge()) }

fn cmp<F: Fn(std::cmp::Ordering) -> bool>(a: &Value, b: &Value, f: F) -> Result<Value> {
    use std::cmp::Ordering;
    let ord = match (a, b) {
        (Value::Int(x), Value::Int(y)) => x.cmp(y),
        (Value::Float(x), Value::Float(y)) => x.partial_cmp(y).ok_or_else(|| EvalError::new("invalid float comparison"))?,
        (Value::Int(x), Value::Float(y)) => (*x as f64).partial_cmp(y).ok_or_else(|| EvalError::new("invalid float comparison"))?,
        (Value::Float(x), Value::Int(y)) => x.partial_cmp(&(*y as f64)).ok_or_else(|| EvalError::new("invalid float comparison"))?,
        (Value::String(x), Value::String(y)) => x.cmp(y),
        (Value::Bool(x), Value::Bool(y)) => x.cmp(y),
        _ => return Err(EvalError::new("incompatible types for comparison").into()),
    };
    Ok(Value::Bool(f(ord)))
}
