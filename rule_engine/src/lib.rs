//! Rule Engine library with a simple DSL parser and evaluator.
//!
//! This crate provides:
//! - A tokenizer and Pratt parser for a small expression DSL
//! - An AST and evaluator supporting primitives, lists, structs, and enums
//! - Built-in boolean, arithmetic, and comparison operators
//! - A registry for custom functions and custom operators
//!
//! See examples in the `examples/` directory.

mod error;
mod value;
mod token;
mod ast;
mod parser;
mod functions;
mod operators;
mod eval;
mod engine;
mod rules;

pub use crate::ast::{Expr, UnaryOp};
pub use crate::engine::{Context, Engine};
pub use crate::engine::{Context as ExpressionContext, Engine as ExpressionEngine};
pub use crate::error::{EngineError, EvalError, ParseError, Result};
pub use crate::functions::{Function, FunctionRegistry};
pub use crate::operators::{Assoc, BinaryOpSpec, OperatorRegistry};
pub use crate::value::Value;
pub use crate::rules::*;

