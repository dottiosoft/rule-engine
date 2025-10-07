use crate::value::Value;

#[derive(Debug, Clone, PartialEq)]
pub enum UnaryOp {
    Not,
    Neg,
    Pos,
    WordNot, // 'not'
}

#[derive(Debug, Clone, PartialEq)]
pub enum Expr {
    Literal(Value),
    Ident(String),
    Unary { op: UnaryOp, expr: Box<Expr> },
    Binary { left: Box<Expr>, op: String, right: Box<Expr> },
    Call { name: String, args: Vec<Expr> },
    MethodCall { target: Box<Expr>, name: String, args: Vec<Expr> },
    Lambda { param: String, body: Box<Expr> },
    Member { target: Box<Expr>, member: String },
    Index { target: Box<Expr>, index: Box<Expr> },
    StructLiteral(Vec<(String, Expr)>),
    ListLiteral(Vec<Expr>),
    EnumVariant { name: String, payload: Option<Box<Expr>> },
}
