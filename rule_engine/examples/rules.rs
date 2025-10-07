use expression_engine as expr;
use rule_engine::{RuleAction, RuleChain, RuleEngine, RuleStep, RuleContext, Value};
use std::collections::BTreeMap;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Expression engine supports complex predicates with and/or/not and HOFs
    let expr = expr::Engine::new();

    // Build a simple rule chain
    let mut chain = RuleChain { name: "pricing".into(), steps: vec![
        RuleStep { name: "check_vip".into(), action: RuleAction::When { expr: "user.is_vip == true".into(), on_false: Some("non_vip".into()) }},
        RuleStep { name: "vip_discount".into(), action: RuleAction::Let { key: "discount".into(), expr: "0.20".into() }},
        RuleStep { name: "emit".into(), action: RuleAction::Emit { expr: "discount".into() }},
    ]};

    let non_vip = RuleChain { name: "non_vip".into(), steps: vec![
        RuleStep { name: "expensive_cart".into(), action: RuleAction::When { expr: "sum_by(cart, i => i.price) > 100 or any(cart, i => i.price > 50)".into(), on_false: Some("small_cart".into()) }},
        RuleStep { name: "big_discount".into(), action: RuleAction::Let { key: "discount".into(), expr: "0.15".into() }},
        RuleStep { name: "emit".into(), action: RuleAction::Emit { expr: "discount".into() }},
    ]};

    let small_cart = RuleChain { name: "small_cart".into(), steps: vec![
        RuleStep { name: "base_discount".into(), action: RuleAction::Let { key: "discount".into(), expr: "0.05".into() }},
        RuleStep { name: "emit".into(), action: RuleAction::Emit { expr: "discount".into() }},
    ]};

    let mut re = RuleEngine::new(expr);
    re.add_chain(chain);
    re.add_chain(non_vip);
    re.add_chain(small_cart);

    // Build dataset
    let mut user = BTreeMap::new();
    user.insert("is_vip".into(), Value::Bool(false));

    let mut cart = Vec::new();
    for price in [30.0, 25.0, 55.0] {
        let mut m = BTreeMap::new();
        m.insert("price".into(), Value::Float(price));
        cart.push(Value::Struct(m));
    }

    let ctx = RuleContext::new().with("user", Value::Struct(user)).with("cart", Value::List(cart));

    let (outcome, audit) = re.run("pricing", ctx)?;

    println!("Outcome: {:?}", outcome);
    for e in audit.0 {
        println!("{} => {}", e.step_name, e.expression);
    }

    Ok(())
}
