use expression_engine::{Context, Engine, Value};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let engine = Engine::new();
    let ctx = Context::new()
        .with_var("x", Value::Int(3))
        .with_var("y", Value::Float(4.5))
        .with_var("name", Value::String("Alice".into()))
        .with_var("nums", Value::List(vec![Value::Int(1), Value::Int(2), Value::Int(3)]));

    for expr in [
        "1 + 2 * 3",
        "(1 + 2) * 3",
        "x + y",
        "name == \"Alice\" and len(nums) == 3",
        "[1,2,3][1] == 2",
        "{a: 1, b: 2}.a == 1",
        "not false and true",
        "\"al\" contains \"a\"",
    ] {
        let val = engine.parse_and_eval(expr, &ctx)?;
        println!("{} => {}", expr, val);
    }

    Ok(())
}
