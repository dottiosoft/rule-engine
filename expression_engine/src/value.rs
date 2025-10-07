use std::collections::BTreeMap;
use std::fmt;

#[derive(Clone, Debug, PartialEq)]
pub enum Value {
    Null,
    Bool(bool),
    Int(i64),
    Float(f64),
    String(String),
    List(Vec<Value>),
    Struct(BTreeMap<String, Value>),
    Enum { name: String, payload: Option<Box<Value>> },
}

impl Value {
    pub fn type_name(&self) -> &'static str {
        match self {
            Value::Null => "null",
            Value::Bool(_) => "bool",
            Value::Int(_) => "int",
            Value::Float(_) => "float",
            Value::String(_) => "string",
            Value::List(_) => "list",
            Value::Struct(_) => "struct",
            Value::Enum { .. } => "enum",
        }
    }

    pub fn as_bool(&self) -> Option<bool> { if let Value::Bool(b) = self { Some(*b) } else { None } }
    pub fn as_int(&self) -> Option<i64> { if let Value::Int(n) = self { Some(*n) } else { None } }
    pub fn as_float(&self) -> Option<f64> { if let Value::Float(n) = self { Some(*n) } else { None } }
    pub fn as_string(&self) -> Option<&str> { if let Value::String(s) = self { Some(s) } else { None } }

    pub fn get_field(&self, key: &str) -> Option<&Value> {
        match self {
            Value::Struct(map) => map.get(key),
            _ => None,
        }
    }

    pub fn get_index(&self, idx: &Value) -> Option<&Value> {
        match (self, idx) {
            (Value::List(v), Value::Int(i)) => v.get(*i as usize),
            (Value::Struct(m), Value::String(k)) => m.get(k),
            _ => None,
        }
    }
}

impl fmt::Display for Value {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Value::Null => write!(f, "null"),
            Value::Bool(b) => write!(f, "{}", b),
            Value::Int(n) => write!(f, "{}", n),
            Value::Float(n) => write!(f, "{}", n),
            Value::String(s) => write!(f, "\"{}\"", s),
            Value::List(v) => {
                write!(f, "[")?;
                for (i, item) in v.iter().enumerate() {
                    if i > 0 { write!(f, ", ")?; }
                    write!(f, "{}", item)?;
                }
                write!(f, "]")
            }
            Value::Struct(map) => {
                write!(f, "{{")?;
                let mut first = true;
                for (k, v) in map.iter() {
                    if !first { write!(f, ", ")?; }
                    first = false;
                    write!(f, "{}: {}", k, v)?;
                }
                write!(f, "}}")
            }
            Value::Enum { name, payload } => {
                if let Some(p) = payload {
                    write!(f, "{}({})", name, p)
                } else {
                    write!(f, "{}", name)
                }
            }
        }
    }
}
