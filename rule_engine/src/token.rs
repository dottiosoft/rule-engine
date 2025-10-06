use crate::error::ParseError;

#[derive(Debug, Clone, PartialEq)]
pub enum TokenKind {
    Ident(String),
    Number(String),
    String(String),

    True,
    False,
    Null,

    // punctuation
    LParen,
    RParen,
    LBracket,
    RBracket,
    LBrace,
    RBrace,
    Comma,
    Colon,
    Dot,

    // operators
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Bang,
    AndAnd,
    OrOr,
    EqEq,
    NotEq,
    Lt,
    Le,
    Gt,
    Ge,

    Eof,
}

#[derive(Debug, Clone)]
pub struct Token {
    pub kind: TokenKind,
    pub position: usize,
}

pub struct Lexer<'a> {
    input: &'a str,
    chars: std::str::Chars<'a>,
    peeked: Option<char>,
    pos: usize,
}

impl<'a> Lexer<'a> {
    pub fn new(input: &'a str) -> Self {
        Self { input, chars: input.chars(), peeked: None, pos: 0 }
    }

    fn next_char(&mut self) -> Option<char> {
        let ch = if let Some(c) = self.peeked.take() { Some(c) } else { self.chars.next() };
        if let Some(c) = ch { self.pos += c.len_utf8(); }
        ch
    }

    fn peek_char(&mut self) -> Option<char> {
        if self.peeked.is_none() {
            self.peeked = self.chars.next();
        }
        self.peeked
    }

    fn skip_ws(&mut self) {
        while let Some(c) = self.peek_char() {
            if c.is_whitespace() { self.next_char(); } else { break; }
        }
    }

    fn read_while<F: Fn(char) -> bool>(&mut self, pred: F) -> String {
        let mut s = String::new();
        while let Some(c) = self.peek_char() {
            if pred(c) {
                s.push(self.next_char().unwrap());
            } else { break; }
        }
        s
    }

    fn read_ident(&mut self, start_pos: usize) -> Token {
        let mut s = String::new();
        if let Some(c) = self.peek_char() { if c.is_ascii_alphabetic() || c == '_' { s.push(self.next_char().unwrap()); }}
        s.push_str(&self.read_while(|c| c.is_ascii_alphanumeric() || c == '_' ));
        let kind = match s.as_str() {
            "true" => TokenKind::True,
            "false" => TokenKind::False,
            "null" => TokenKind::Null,
            _ => TokenKind::Ident(s),
        };
        Token { kind, position: start_pos }
    }

    fn read_number(&mut self, start_pos: usize, first: char) -> Token {
        let mut s = String::new();
        s.push(first);
        s.push_str(&self.read_while(|c| c.is_ascii_digit()));
        if let Some('.') = self.peek_char() {
            s.push(self.next_char().unwrap());
            s.push_str(&self.read_while(|c| c.is_ascii_digit()));
            // optional exponent
            if let Some('e') | Some('E') = self.peek_char() {
                s.push(self.next_char().unwrap());
                if let Some('+') | Some('-') = self.peek_char() { s.push(self.next_char().unwrap()); }
                s.push_str(&self.read_while(|c| c.is_ascii_digit()));
            }
        }
        Token { kind: TokenKind::Number(s), position: start_pos }
    }

    fn read_string(&mut self, start_pos: usize) -> Result<Token, ParseError> {
        let mut s = String::new();
        // consume opening quote
        let _ = self.next_char();
        while let Some(c) = self.next_char() {
            match c {
                '"' => return Ok(Token { kind: TokenKind::String(s), position: start_pos }),
                '\\' => {
                    if let Some(esc) = self.next_char() {
                        match esc {
                            'n' => s.push('\n'),
                            't' => s.push('\t'),
                            'r' => s.push('\r'),
                            '"' => s.push('"'),
                            '\\' => s.push('\\'),
                            other => {
                                s.push(other); // unknown escapes are literal
                            }
                        }
                    } else {
                        return Err(ParseError::new("unterminated escape in string", start_pos));
                    }
                }
                other => s.push(other),
            }
        }
        Err(ParseError::new("unterminated string literal", start_pos))
    }

    pub fn tokenize(mut self) -> Result<Vec<Token>, ParseError> {
        let mut tokens = Vec::new();
        loop {
            self.skip_ws();
            let pos = self.pos;
            let ch = match self.peek_char() { Some(c) => c, None => break };
            let tok = match ch {
                'a'..='z' | 'A'..='Z' | '_' => {
                    self.read_ident(pos)
                }
                '0'..='9' => {
                    let first = self.next_char().unwrap();
                    self.read_number(pos, first)
                }
                '"' => self.read_string(pos)?,
                '(' => { self.next_char(); Token { kind: TokenKind::LParen, position: pos } }
                ')' => { self.next_char(); Token { kind: TokenKind::RParen, position: pos } }
                '[' => { self.next_char(); Token { kind: TokenKind::LBracket, position: pos } }
                ']' => { self.next_char(); Token { kind: TokenKind::RBracket, position: pos } }
                '{' => { self.next_char(); Token { kind: TokenKind::LBrace, position: pos } }
                '}' => { self.next_char(); Token { kind: TokenKind::RBrace, position: pos } }
                ',' => { self.next_char(); Token { kind: TokenKind::Comma, position: pos } }
                ':' => { self.next_char(); Token { kind: TokenKind::Colon, position: pos } }
                '.' => { self.next_char(); Token { kind: TokenKind::Dot, position: pos } }
                '+' => { self.next_char(); Token { kind: TokenKind::Plus, position: pos } }
                '-' => { self.next_char(); Token { kind: TokenKind::Minus, position: pos } }
                '*' => { self.next_char(); Token { kind: TokenKind::Star, position: pos } }
                '/' => { self.next_char(); Token { kind: TokenKind::Slash, position: pos } }
                '%' => { self.next_char(); Token { kind: TokenKind::Percent, position: pos } }
                '!' => {
                    self.next_char();
                    if let Some('=') = self.peek_char() {
                        self.next_char(); Token { kind: TokenKind::NotEq, position: pos }
                    } else {
                        Token { kind: TokenKind::Bang, position: pos }
                    }
                }
                '&' => {
                    self.next_char();
                    if let Some('&') = self.peek_char() { self.next_char(); Token { kind: TokenKind::AndAnd, position: pos } } else {
                        return Err(ParseError::new("unexpected '&' (did you mean '&&'?)", pos));
                    }
                }
                '|' => {
                    self.next_char();
                    if let Some('|') = self.peek_char() { self.next_char(); Token { kind: TokenKind::OrOr, position: pos } } else {
                        return Err(ParseError::new("unexpected '|' (did you mean '||'?)", pos));
                    }
                }
                '=' => {
                    self.next_char();
                    if let Some('=') = self.peek_char() { self.next_char(); Token { kind: TokenKind::EqEq, position: pos } } else {
                        return Err(ParseError::new("unexpected '=' (did you mean '==')?", pos));
                    }
                }
                '<' => {
                    self.next_char();
                    if let Some('=') = self.peek_char() { self.next_char(); Token { kind: TokenKind::Le, position: pos } } else {
                        Token { kind: TokenKind::Lt, position: pos }
                    }
                }
                '>' => {
                    self.next_char();
                    if let Some('=') = self.peek_char() { self.next_char(); Token { kind: TokenKind::Ge, position: pos } } else {
                        Token { kind: TokenKind::Gt, position: pos }
                    }
                }
                _ => {
                    return Err(ParseError::new(format!("unexpected character '{}'", ch), pos));
                }
            };
            tokens.push(tok);
        }
        tokens.push(Token { kind: TokenKind::Eof, position: self.pos });
        Ok(tokens)
    }
}
