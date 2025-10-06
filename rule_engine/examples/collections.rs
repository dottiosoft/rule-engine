use rule_engine::{Context, Engine, Value};
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

    // Sum prices for fruit category
    let sum_expr = "sum_by(filter(items, x => x.category == \"fruit\"), x => x.price)";
    let total = engine.parse_and_eval(sum_expr, &ctx)?;
    println!("{} => {}", sum_expr, total);

    // Build list of names for items over $1
    let names_expr = "map(filter(items, i => i.price > 1), i => i.name)";
    let names = engine.parse_and_eval(names_expr, &ctx)?;
    println!("{} => {}", names_expr, names);

    // Count of bakery items
    let count_expr = "count(items, i => i.category == \"bakery\")";
    let count = engine.parse_and_eval(count_expr, &ctx)?;
    println!("{} => {}", count_expr, count);

    Ok(())
}
