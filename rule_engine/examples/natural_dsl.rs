use expression_engine::{Context, Engine, Value};
use std::collections::BTreeMap;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let engine = Engine::new();

    let mut items = Vec::new();
    for (name, price, category) in [
        ("apple", 1.20, "fruit"),
        ("banana", 0.80, "fruit"),
        ("bread", 2.50, "bakery"),
        ("steak", 10.0, "meat"),
    ] {
        let mut m = BTreeMap::new();
        m.insert("name".to_string(), Value::String(name.into()));
        m.insert("price".to_string(), Value::Float(price));
        m.insert("category".to_string(), Value::String(category.into()));
        items.push(Value::Struct(m));
    }

    let ctx = Context::new().with_var("items", Value::List(items));

    // Natural DSL: method chain style
    let exprs = [
        "items.where(x => x.category == \"fruit\").sum(x => x.price)",
        "items.where(i => i.price > 1).select(i => i.name)",
        "items.avg(i => i.price)",
        "items.min(i => i.price)",
        "items.max(i => i.price)",
        "items.count(i => i.category == \"fruit\")",
    ];

    for e in exprs {
        let val = engine.parse_and_eval(e, &ctx)?;
        println!("{} => {}", e, val);
    }

    Ok(())
}
