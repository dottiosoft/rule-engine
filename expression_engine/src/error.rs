use core::fmt;

pub type Result<T> = core::result::Result<T, EngineError>;

#[derive(Debug)]
pub enum EngineError {
    Parse(ParseError),
    Eval(EvalError),
}

impl fmt::Display for EngineError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            EngineError::Parse(e) => write!(f, "parse error: {}", e),
            EngineError::Eval(e) => write!(f, "eval error: {}", e),
        }
    }
}

impl std::error::Error for EngineError {}

#[derive(Debug, Clone)]
pub struct ParseError {
    pub message: String,
    pub position: usize,
}

impl ParseError {
    pub fn new(message: impl Into<String>, position: usize) -> Self {
        Self { message: message.into(), position }
    }
}

impl fmt::Display for ParseError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{} at position {}", self.message, self.position)
    }
}

impl From<ParseError> for EngineError {
    fn from(e: ParseError) -> Self { EngineError::Parse(e) }
}

#[derive(Debug, Clone)]
pub struct EvalError {
    pub message: String,
}

impl EvalError {
    pub fn new(message: impl Into<String>) -> Self {
        Self { message: message.into() }
    }
}

impl fmt::Display for EvalError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.message)
    }
}

impl From<EvalError> for EngineError {
    fn from(e: EvalError) -> Self { EngineError::Eval(e) }
}
