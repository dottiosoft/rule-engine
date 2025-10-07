use expression_engine::{Context, Engine, Value};

#[test]
fn arithmetic_and_logic() {
    let engine = Engine::new();
    let ctx = Context::new().with_var("x", Value::Int(10)).with_var("y", Value::Int(3));

    let cases = vec![
        ("1 + 2 * 3", Value::Float(7.0)),
        ("(1 + 2) * 3", Value::Float(9.0)),
        ("x > y and x < 20", Value::Bool(true)),
        ("x % 3 == 1", Value::Bool(true)),
        ("not false or false", Value::Bool(true)),
    ];

    for (expr, expected) in cases {
        let val = engine.parse_and_eval(expr, &ctx).unwrap();
        assert_eq!(val, expected, "expr: {}", expr);
    }
}

#[test]
fn collections_and_structs() {
    let engine = Engine::new();
    let ctx = Context::new()
        .with_var("nums", Value::List(vec![Value::Int(1), Value::Int(2), Value::Int(3)]))
        .with_var("user", {
            use std::collections::BTreeMap;
            let mut m = BTreeMap::new();
            m.insert("name".to_string(), Value::String("Alice".into()));
            m.insert("age".to_string(), Value::Int(30));
            Value::Struct(m)
        });

    let cases = vec![
        ("nums[1] == 2", Value::Bool(true)),
        ("user.name == \"Alice\"", Value::Bool(true)),
        ("len(nums) == 3", Value::Bool(true)),
        ("\"l\" contains \"l\"", Value::Bool(true)),
    ];

    for (expr, expected) in cases {
        let val = engine.parse_and_eval(expr, &ctx).unwrap();
        assert_eq!(val, expected, "expr: {}", expr);
    }
}
