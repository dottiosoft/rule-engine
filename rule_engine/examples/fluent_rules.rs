use expression_engine as expr;
use rule_engine::{RuleChainBuilder, RuleEngine, RuleContext, Value};
use std::collections::BTreeMap;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let expr = expr::Engine::new();

    let pricing = RuleChainBuilder::new("pricing")
        .when_else("user.is_vip == true", "non_vip")
        .let_("discount", "0.20")
        .emit("discount")
        .build();

    let non_vip = RuleChainBuilder::new("non_vip")
        .when_else("cart.avg(i => i.price) > 50", "small_cart")
        .let_("discount", "0.15")
        .emit("discount")
        .build();

    let small_cart = RuleChainBuilder::new("small_cart")
        .let_("discount", "0.05")
        .emit("discount")
        .build();

    let engine = RuleEngine::new(expr)
        .with_chain(pricing)
        .with_chain(non_vip)
        .with_chain(small_cart);

    // Data
    let mut user = BTreeMap::new();
    user.insert("is_vip".into(), Value::Bool(false));

    let mut cart = Vec::new();
    for price in [30.0, 25.0, 55.0] {
        let mut m = BTreeMap::new();
        m.insert("price".into(), Value::Float(price));
        cart.push(Value::Struct(m));
    }

    let ctx = RuleContext::new().with("user", Value::Struct(user)).with("cart", Value::List(cart));

    let (outcome, audit) = engine.run("pricing", ctx)?;
    println!("Outcome: {:?}", outcome);
    for e in audit.0 {
        println!("{} => {}", e.step_name, e.expression);
    }

    Ok(())
}
