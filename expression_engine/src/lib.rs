//! Expression Engine library: DSL tokenizer, parser, evaluator.

mod error;
mod value;
mod token;
mod ast;
mod parser;
mod functions;
mod operators;
mod eval;
mod engine;

pub use crate::ast::{Expr, UnaryOp};
pub use crate::engine::{Context, Engine};
pub use crate::error::{EngineError, EvalError, ParseError, Result};
pub use crate::functions::{Function, FunctionRegistry};
pub use crate::operators::{Assoc, BinaryOpSpec, OperatorRegistry};
pub use crate::value::Value;

