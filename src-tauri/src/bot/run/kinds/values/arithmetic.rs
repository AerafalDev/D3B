//! The arithmetic of whole numbers (the owner, 2026-11-17): two inputs each, a result too large blocking rather than
//! wrapping; ÷ and % of round down and a remainder is never negative (settled alone); ÷ or a remainder by 0 blocks on
//! its node, a 0 typed there being a problem already.

use serde_json::Value;

use super::super::super::behaviour::{Behaviour, Cx, Fault};

/// What an arithmetic node does with its two whole numbers.
#[derive(Clone, Copy)]
pub(super) enum Operation {
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
    Min,
    Max,
}

/// `a` and `b` made one by `operation`.
fn apply(operation: Operation, a: i64, b: i64) -> Result<i64, Fault> {
    let zero = || Fault::DividedByZero { pin: "b".to_owned() };
    match operation {
        Operation::Add => a.checked_add(b).ok_or(Fault::TooLarge),
        Operation::Subtract => a.checked_sub(b).ok_or(Fault::TooLarge),
        Operation::Multiply => a.checked_mul(b).ok_or(Fault::TooLarge),
        Operation::Divide | Operation::Remainder if b == 0 => Err(zero()),
        Operation::Divide => floor_div(a, b),
        // -1 divides every number: the remainder is 0, though `i64::MIN`'s quotient would not fit.
        Operation::Remainder if b == -1 => Ok(0),
        Operation::Remainder => a.checked_rem_euclid(b).ok_or(Fault::TooLarge),
        Operation::Min => Ok(a.min(b)),
        Operation::Max => Ok(a.max(b)),
    }
}

/// `a` ÷ `b` rounded down, towards minus infinity: -7 ÷ 2 is -4.
fn floor_div(a: i64, b: i64) -> Result<i64, Fault> {
    let quotient = a.checked_div(b).ok_or(Fault::TooLarge)?;
    let inexact = a.checked_rem(b).ok_or(Fault::TooLarge)? != 0;
    Ok(if inexact && (a < 0) != (b < 0) {
        quotient - 1
    } else {
        quotient
    })
}

/// `+`, `−`, `×`, `÷`, remainder, min or max of `a` and `b`.
pub(super) struct Arithmetic(pub(super) Operation);

impl Behaviour for Arithmetic {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let (a, b) = (cx.number("a")?, cx.number("b")?);
        Ok(Value::from(apply(self.0, a, b)?))
    }
}

/// Clamp: `value` kept from `low` to `high`, bounds the other way round swapped.
pub(super) struct Clamp;

impl Behaviour for Clamp {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let (value, low, high) = (cx.number("value")?, cx.number("low")?, cx.number("high")?);
        Ok(Value::from(value.clamp(low.min(high), low.max(high))))
    }
}

/// N % of X: `percent` hundredths of `of`, rounded down.
pub(super) struct PercentOf;

impl Behaviour for PercentOf {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        // Wide, so that only a result too large blocks, not the product on the way.
        let product = i128::from(cx.number("percent")?) * i128::from(cx.number("of")?);
        let result = product.div_euclid(100);
        Ok(Value::from(i64::try_from(result).map_err(|_| Fault::TooLarge)?))
    }
}
