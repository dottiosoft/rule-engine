use std::collections::HashMap;

use crate::ast::{Expr, UnaryOp};
use crate::error::{ParseError, Result};
use crate::operators::{Assoc, OperatorRegistry};
use crate::token::{Lexer, Token, TokenKind};
use crate::value::Value;

pub struct Parser<'a> {
    tokens: Vec<Token>,
    pos: usize,
    // custom word operators with precedence and associativity
    word_ops: HashMap<String, (u8, Assoc)>,
    _phantom: std::marker::PhantomData<&'a ()>,
}

impl<'a> Parser<'a> {
    pub fn new(input: &str, ops: &OperatorRegistry) -> Result<Self> {
        let tokens = Lexer::new(input).tokenize()?;
        let mut word_ops = HashMap::new();
        for (name, spec) in ops.binary.iter() {
            if is_word(name) { word_ops.insert(name.clone(), (spec.precedence, spec.associativity)); }
        }
        Ok(Self { tokens, pos: 0, word_ops, _phantom: std::marker::PhantomData })
    }

    fn peek(&self) -> &Token { &self.tokens[self.pos] }

    fn at(&self, kind: &TokenKind) -> bool { &self.tokens[self.pos].kind == kind }

    fn bump(&mut self) -> &Token { let t = &self.tokens[self.pos]; self.pos += 1; t }

    fn expect(&mut self, kind: TokenKind) -> Result<()> {
        let t = self.bump();
        if t.kind != kind { return Err(ParseError::new(format!("expected {:?}, found {:?}", kind, t.kind), t.position).into()); }
        Ok(())
    }

    pub fn parse_expression(&mut self) -> Result<Expr> {
        self.parse_bp(0)
    }

    fn parse_bp(&mut self, min_bp: u8) -> Result<Expr> {
        let mut lhs = self.parse_prefix()?;
        loop {
            // Postfix: member, index, call
            match self.tokens[self.pos].kind.clone() {
                TokenKind::Dot => {
                    let _ = self.bump();
                    let t = self.bump();
                    let name = match &t.kind { TokenKind::Ident(s) => s.clone(), _ => return Err(ParseError::new("expected identifier after '.'", t.position).into()) };
                    lhs = Expr::Member { target: Box::new(lhs), member: name };
                    continue;
                }
                TokenKind::LBracket => {
                    let _ = self.bump();
                    let idx = self.parse_expression()?;
                    let t = self.bump();
                    if t.kind != TokenKind::RBracket { return Err(ParseError::new("expected ']'", t.position).into()); }
                    lhs = Expr::Index { target: Box::new(lhs), index: Box::new(idx) };
                    continue;
                }
                TokenKind::LParen => {
                    // Only call if lhs is Ident or Member (treat as Call with name string for Ident)
                    if let Expr::Ident(name) = lhs.clone() {
                        let args = self.parse_arg_list()?;
                        lhs = Expr::Call { name, args };
                        continue;
                    }
                    // If not an identifier, stop postfix and handle as grouping in prefix in next iteration
                }
                _ => {}
            }

            // Infix
            let (op, lbp, rbp) = match self.peek_infix_op() {
                Some((op, lbp, rbp)) if lbp >= min_bp => (op, lbp, rbp),
                _ => break,
            };
            // consume operator token(s)
            self.consume_infix_token(&op)?;
            let rhs = self.parse_bp(rbp)?;
            lhs = Expr::Binary { left: Box::new(lhs), op, right: Box::new(rhs) };
        }
        Ok(lhs)
    }

    fn parse_prefix(&mut self) -> Result<Expr> {
        let t = self.bump().clone();
        match t.kind {
            TokenKind::True => Ok(Expr::Literal(Value::Bool(true))),
            TokenKind::False => Ok(Expr::Literal(Value::Bool(false))),
            TokenKind::Null => Ok(Expr::Literal(Value::Null)),
            TokenKind::Number(ref s) => {
                if s.contains(['.', 'e', 'E']) { Ok(Expr::Literal(Value::Float(s.parse().unwrap()))) }
                else { Ok(Expr::Literal(Value::Int(s.parse().unwrap()))) }
            }
            TokenKind::String(s) => Ok(Expr::Literal(Value::String(s))),
            TokenKind::Ident(name) => {
                // Enum variant if capitalized and followed by '(' or not
                if name == "not" {
                    // word unary operator
                    let expr = self.parse_bp(7)?;
                    return Ok(Expr::Unary { op: UnaryOp::WordNot, expr: Box::new(expr) });
                }

                // Lambda parameter => body
                if self.at(&TokenKind::Arrow) {
                    let _ = self.bump(); // '=>'
                    let body = self.parse_expression()?;
                    return Ok(Expr::Lambda { param: name, body: Box::new(body) });
                }

                if is_capitalized(&name) {
                    if self.at(&TokenKind::LParen) {
                        let args = self.parse_arg_list()?;
                        if args.len() != 1 {
                            return Err(ParseError::new("enum variant expects 0 or 1 argument", t.position).into());
                        }
                        return Ok(Expr::EnumVariant { name, payload: Some(Box::new(args.into_iter().next().unwrap())) });
                    } else {
                        return Ok(Expr::EnumVariant { name, payload: None });
                    }
                }
                Ok(Expr::Ident(name))
            }
            TokenKind::LParen => {
                let e = self.parse_expression()?;
                let t2 = self.bump();
                if t2.kind != TokenKind::RParen { return Err(ParseError::new("expected ')'", t2.position).into()); }
                Ok(e)
            }
            TokenKind::LBracket => {
                let mut items = Vec::new();
                if !self.at(&TokenKind::RBracket) {
                    loop {
                        items.push(self.parse_expression()?);
                        let t = self.bump();
                        match t.kind {
                            TokenKind::Comma => continue,
                            TokenKind::RBracket => break,
                            _ => return Err(ParseError::new("expected ',' or ']' in list", t.position).into()),
                        }
                    }
                } else {
                    let _ = self.bump(); // consume ]
                }
                Ok(Expr::ListLiteral(items))
            }
            TokenKind::LBrace => {
                let mut fields = Vec::new();
                if !self.at(&TokenKind::RBrace) {
                    loop {
                        let key_tok = self.bump().clone();
                        let key = match key_tok.kind {
                            TokenKind::Ident(s) => s,
                            _ => return Err(ParseError::new("expected identifier key in struct literal", key_tok.position).into()),
                        };
                        let colon = self.bump();
                        if colon.kind != TokenKind::Colon { return Err(ParseError::new("expected ':' after key", colon.position).into()); }
                        let value = self.parse_expression()?;
                        let t = self.bump();
                        match t.kind {
                            TokenKind::Comma => fields.push((key, value)),
                            TokenKind::RBrace => { fields.push((key, value)); break; }
                            _ => return Err(ParseError::new("expected ',' or '}' in struct literal", t.position).into()),
                        }
                    }
                } else {
                    let _ = self.bump(); // consume }
                }
                Ok(Expr::StructLiteral(fields))
            }
            TokenKind::Bang => Ok(Expr::Unary { op: UnaryOp::Not, expr: Box::new(self.parse_bp(7)?) }),
            TokenKind::Minus => Ok(Expr::Unary { op: UnaryOp::Neg, expr: Box::new(self.parse_bp(7)?) }),
            TokenKind::Plus => Ok(Expr::Unary { op: UnaryOp::Pos, expr: Box::new(self.parse_bp(7)?) }),
            other => Err(ParseError::new(format!("unexpected token: {:?}", other), t.position).into()),
        }
    }

    fn parse_arg_list(&mut self) -> Result<Vec<Expr>> {
        // assumes current token is '('
        let _open = self.bump(); // '('
        let mut args = Vec::new();
        if !self.at(&TokenKind::RParen) {
            loop {
                args.push(self.parse_expression()?);
                let t = self.bump();
                match t.kind {
                    TokenKind::Comma => continue,
                    TokenKind::RParen => break,
                    _ => return Err(ParseError::new("expected ',' or ')' in call", t.position).into()),
                }
            }
        } else {
            let _ = self.bump(); // consume ')'
        }
        Ok(args)
    }

    fn peek_infix_op(&self) -> Option<(String, u8, u8)> {
        let (op_str, precedence, assoc) = match self.tokens[self.pos].kind.clone() {
            TokenKind::OrOr => ("||".to_string(), 1, Assoc::Left),
            TokenKind::AndAnd => ("&&".to_string(), 2, Assoc::Left),
            TokenKind::EqEq => ("==".to_string(), 3, Assoc::Left),
            TokenKind::NotEq => ("!=".to_string(), 3, Assoc::Left),
            TokenKind::Lt => ("<".to_string(), 4, Assoc::Left),
            TokenKind::Le => ("<=".to_string(), 4, Assoc::Left),
            TokenKind::Gt => (">".to_string(), 4, Assoc::Left),
            TokenKind::Ge => (">=".to_string(), 4, Assoc::Left),
            TokenKind::Plus => ("+".to_string(), 5, Assoc::Left),
            TokenKind::Minus => ("-".to_string(), 5, Assoc::Left),
            TokenKind::Star => ("*".to_string(), 6, Assoc::Left),
            TokenKind::Slash => ("/".to_string(), 6, Assoc::Left),
            TokenKind::Percent => ("%".to_string(), 6, Assoc::Left),
            TokenKind::Ident(ref s) => {
                if let Some((prec, assoc)) = self.word_ops.get(s) { (s.clone(), *prec, *assoc) } else { return None }
            }
            _ => return None,
        };
        let (lbp, rbp) = match assoc {
            Assoc::Left => (precedence, precedence + 1),
            Assoc::Right => (precedence, precedence),
        };
        Some((op_str, lbp, rbp))
    }

    fn consume_infix_token(&mut self, op: &str) -> Result<()> {
        match (op, &self.tokens[self.pos].kind) {
            ("||", TokenKind::OrOr) | ("&&", TokenKind::AndAnd) | ("==", TokenKind::EqEq) |
            ("!=", TokenKind::NotEq) | ("<", TokenKind::Lt) | ("<=", TokenKind::Le) |
            (">", TokenKind::Gt) | (">=", TokenKind::Ge) | ("+", TokenKind::Plus) |
            ("-", TokenKind::Minus) | ("*", TokenKind::Star) | ("/", TokenKind::Slash) |
            ("%", TokenKind::Percent) => { let _ = self.bump(); Ok(()) }
            _ => {
                // assume word operator
                let t = self.bump();
                match &t.kind {
                    TokenKind::Ident(s) if s == op => Ok(()),
                    _ => Err(ParseError::new("expected operator", t.position).into()),
                }
            }
        }
    }
}

fn is_capitalized(s: &str) -> bool { s.chars().next().map(|c| c.is_ascii_uppercase()).unwrap_or(false) }
fn is_word(s: &str) -> bool { s.chars().all(|c| c.is_ascii_alphabetic() || c == '_') }
